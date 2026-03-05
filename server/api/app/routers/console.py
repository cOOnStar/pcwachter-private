from datetime import datetime, timedelta, timezone
import uuid

import httpx
from fastapi import APIRouter, Depends, HTTPException, Query
from pydantic import BaseModel, Field
from sqlalchemy import String, cast, func, or_, select
from sqlalchemy.orm import Session

from ..db import get_db
from ..models import Device, DeviceInventory, KbArticle, License, Notification, Plan, TelemetrySnapshot
from ..schemas import (
    AgentInfo,
    DeviceDetailResponse,
    DeviceListItem,
    DeviceListResponse,
    LatestInventoryResponse,
    LicenseCreateRequest,
    LicenseCreateResponse,
    LicenseInfo,
    NetworkInfo,
    OSInfo,
    PlanItemExtended,
    PlanListResponseExtended,
    PlanUpsertRequest,
    PlanUpsertRequestExtended,
    PublishPriceRequest,
    PublishPriceResponse,
    StripePlanStatusResponse,
    SubscriptionItem,
    SubscriptionListResponse,
    SubscriptionPatchRequest,
)
from ..security import require_api_key
from ..security_jwt import require_console_owner, require_console_user, require_home_user
from ..settings import settings

router = APIRouter(prefix="/console", tags=["console"])
MANAGED_CONSOLE_ROLES = {"owner", "admin", "manager", "user"}
MANAGED_ROLE_ASSIGNMENTS = {"owner", "admin", "user"}
ROLE_GROUPS = {
    "owner": "console-owner",
    "admin": "console-admin",
    "user": "console-user",
}


class AccountRoleUpdateRequest(BaseModel):
    role: str = Field(min_length=1, max_length=32)


def utcnow() -> datetime:
    return datetime.now(timezone.utc)


def is_online(last_seen_at: datetime | None) -> bool:
    if not last_seen_at:
        return False
    delta = utcnow() - last_seen_at
    return delta.total_seconds() < settings.ONLINE_THRESHOLD_SECONDS


def _severity_for_telemetry(category: str, summary: str | None, payload: dict | None) -> str:
    haystack = " ".join(
        [
            (category or "").lower(),
            (summary or "").lower(),
            str(payload or {}).lower(),
        ]
    )
    if any(token in haystack for token in ("critical", "krit", "failed", "error", "deaktiv", "outdated", "suspicious")):
        return "critical"
    if any(token in haystack for token in ("warning", "warn", "pending", "low", "alt", "expir")):
        return "warning"
    return "info"


def _first_attr(raw: dict | None, key: str) -> str | None:
    if not raw:
        return None
    value = raw.get(key)
    if isinstance(value, list):
        return str(value[0]) if value else None
    if value is None:
        return None
    return str(value)


def _display_role_from_roles(roles: list[str]) -> str:
    if "owner" in roles:
        return "Owner"
    if "admin" in roles:
        return "Admin"
    if "manager" in roles:
        return "Manager"
    if "user" in roles:
        return "Benutzer"
    return "Benutzer"


def _status_from_keycloak(enabled: bool) -> str:
    return "active" if enabled else "inactive"


def _iso_from_keycloak_ts(value: int | None) -> str | None:
    if not value:
        return None
    try:
        return datetime.fromtimestamp(value / 1000, tz=timezone.utc).isoformat()
    except Exception:
        return None


def _keycloak_admin_configured() -> bool:
    return bool(settings.KEYCLOAK_ADMIN_USER and settings.KEYCLOAK_ADMIN_PASSWORD)


def _keycloak_admin_token() -> str:
    if not _keycloak_admin_configured():
        raise RuntimeError("keycloak admin credentials missing")

    token_url = f"{settings.KEYCLOAK_URL}/realms/master/protocol/openid-connect/token"
    data = {
        "grant_type": "password",
        "client_id": settings.KEYCLOAK_ADMIN_CLIENT_ID,
        "username": settings.KEYCLOAK_ADMIN_USER,
        "password": settings.KEYCLOAK_ADMIN_PASSWORD,
    }
    if settings.KEYCLOAK_ADMIN_CLIENT_SECRET:
        data["client_secret"] = settings.KEYCLOAK_ADMIN_CLIENT_SECRET

    response = httpx.post(token_url, data=data, timeout=15)
    if not response.is_success:
        raise RuntimeError(f"keycloak token request failed ({response.status_code})")

    token = response.json().get("access_token")
    if not token:
        raise RuntimeError("keycloak token missing")
    return token


def _load_keycloak_accounts() -> list[dict]:
    token = _keycloak_admin_token()
    base = f"{settings.KEYCLOAK_URL}/admin/realms/{settings.KEYCLOAK_REALM}"
    headers = {"Authorization": f"Bearer {token}"}

    users_resp = httpx.get(
        f"{base}/users",
        params={"max": 500, "briefRepresentation": "false"},
        headers=headers,
        timeout=20,
    )
    if not users_resp.is_success:
        raise RuntimeError(f"keycloak users request failed ({users_resp.status_code})")

    users = users_resp.json()
    accounts: list[dict] = []
    for user in users:
        user_id = str(user.get("id") or "")
        if not user_id:
            continue

        roles: list[str] = []
        try:
            roles_resp = httpx.get(
                f"{base}/users/{user_id}/role-mappings/realm/composite",
                headers=headers,
                timeout=10,
            )
            if roles_resp.is_success:
                roles = [str(role.get("name")) for role in roles_resp.json() if role.get("name")]
        except Exception:
            roles = []

        full_name = (
            user.get("firstName") and user.get("lastName") and f"{user.get('firstName')} {user.get('lastName')}"
        ) or user.get("firstName") or user.get("lastName") or user.get("username") or user.get("email") or "unknown"
        attrs = user.get("attributes") if isinstance(user.get("attributes"), dict) else {}
        last_login = _first_attr(attrs, "lastLogin") or _first_attr(attrs, "last_login")

        accounts.append(
            {
                "id": user_id,
                "name": str(full_name),
                "email": str(user.get("email") or ""),
                "role": _display_role_from_roles(roles),
                "roleKeys": roles,
                "status": _status_from_keycloak(bool(user.get("enabled", True))),
                "created": _iso_from_keycloak_ts(user.get("createdTimestamp")),
                "lastLogin": last_login,
            }
        )

    return accounts


def _normalize_assignable_role(value: str) -> str:
    role = str(value or "").strip().lower()
    if role == "benutzer":
        role = "user"
    if role not in MANAGED_ROLE_ASSIGNMENTS:
        raise HTTPException(status_code=400, detail="invalid role (allowed: user, admin, owner)")
    return role


def _keycloak_admin_context() -> tuple[str, dict]:
    token = _keycloak_admin_token()
    base = f"{settings.KEYCLOAK_URL}/admin/realms/{settings.KEYCLOAK_REALM}"
    return base, {"Authorization": f"Bearer {token}"}


def _keycloak_load_realm_composite_roles(base: str, headers: dict, user_id: str) -> list[dict]:
    response = httpx.get(
        f"{base}/users/{user_id}/role-mappings/realm/composite",
        headers=headers,
        timeout=10,
    )
    if not response.is_success:
        raise RuntimeError(f"keycloak role mappings request failed ({response.status_code})")
    payload = response.json()
    return payload if isinstance(payload, list) else []


def _keycloak_realm_role_representation(base: str, headers: dict, role_name: str) -> dict:
    response = httpx.get(
        f"{base}/roles/{role_name}",
        headers=headers,
        timeout=10,
    )
    if not response.is_success:
        raise RuntimeError(f"keycloak role lookup failed for {role_name} ({response.status_code})")
    payload = response.json()
    if not isinstance(payload, dict):
        raise RuntimeError(f"keycloak role lookup returned invalid payload for {role_name}")
    return payload


def _keycloak_group_by_name(base: str, headers: dict, group_name: str) -> dict:
    response = httpx.get(
        f"{base}/groups",
        params={"search": group_name, "briefRepresentation": "true", "max": 200},
        headers=headers,
        timeout=15,
    )
    if not response.is_success:
        raise RuntimeError(f"keycloak groups request failed ({response.status_code})")
    groups = response.json()
    if not isinstance(groups, list):
        raise RuntimeError("keycloak groups response invalid")
    for group in groups:
        if not isinstance(group, dict):
            continue
        if str(group.get("name") or "").strip() == group_name:
            return group
    raise RuntimeError(f"required keycloak group missing: {group_name}")


def _keycloak_update_user_role(user_id: str, target_role: str) -> list[str]:
    base, headers = _keycloak_admin_context()
    user_resp = httpx.get(f"{base}/users/{user_id}", headers=headers, timeout=10)
    if user_resp.status_code == 404:
        raise HTTPException(status_code=404, detail="account not found in keycloak")
    if not user_resp.is_success:
        raise RuntimeError(f"keycloak user lookup failed ({user_resp.status_code})")

    current_roles = _keycloak_load_realm_composite_roles(base, headers, user_id)
    current_names = {str(role.get("name") or "").strip().lower() for role in current_roles if isinstance(role, dict)}

    roles_to_remove = [
        role for role in current_roles
        if isinstance(role, dict)
        and str(role.get("name") or "").strip().lower() in MANAGED_CONSOLE_ROLES
        and str(role.get("name") or "").strip().lower() != target_role
    ]
    if roles_to_remove:
        remove_resp = httpx.request(
            "DELETE",
            f"{base}/users/{user_id}/role-mappings/realm",
            headers=headers,
            json=roles_to_remove,
            timeout=10,
        )
        if not remove_resp.is_success:
            raise RuntimeError(f"keycloak role removal failed ({remove_resp.status_code})")

    if target_role not in current_names:
        role_repr = _keycloak_realm_role_representation(base, headers, target_role)
        add_resp = httpx.post(
            f"{base}/users/{user_id}/role-mappings/realm",
            headers=headers,
            json=[role_repr],
            timeout=10,
        )
        if not add_resp.is_success:
            raise RuntimeError(f"keycloak role assignment failed ({add_resp.status_code})")

    group_by_role = {
        role: _keycloak_group_by_name(base, headers, group_name)
        for role, group_name in ROLE_GROUPS.items()
    }
    target_group_id = str(group_by_role[target_role].get("id") or "").strip()
    target_group_name = str(group_by_role[target_role].get("name") or "").strip()
    if not target_group_id:
        raise RuntimeError(f"missing id for keycloak group {target_group_name or target_role}")

    user_groups_resp = httpx.get(
        f"{base}/users/{user_id}/groups",
        params={"briefRepresentation": "true", "max": 200},
        headers=headers,
        timeout=10,
    )
    if not user_groups_resp.is_success:
        raise RuntimeError(f"keycloak user groups request failed ({user_groups_resp.status_code})")
    user_groups = user_groups_resp.json()
    if not isinstance(user_groups, list):
        raise RuntimeError("keycloak user groups response invalid")

    managed_group_names = {name for name in ROLE_GROUPS.values()}
    assigned_group_names = {
        str(group.get("name") or "").strip()
        for group in user_groups
        if isinstance(group, dict)
    }
    for group in user_groups:
        if not isinstance(group, dict):
            continue
        group_name = str(group.get("name") or "").strip()
        group_id = str(group.get("id") or "").strip()
        if not group_id or group_name not in managed_group_names or group_name == target_group_name:
            continue
        leave_resp = httpx.delete(
            f"{base}/users/{user_id}/groups/{group_id}",
            headers=headers,
            timeout=10,
        )
        if not leave_resp.is_success:
            raise RuntimeError(f"keycloak group removal failed for {group_name} ({leave_resp.status_code})")

    if target_group_name not in assigned_group_names:
        join_resp = httpx.put(
            f"{base}/users/{user_id}/groups/{target_group_id}",
            headers=headers,
            timeout=10,
        )
        if not join_resp.is_success:
            raise RuntimeError(f"keycloak group assignment failed for {target_group_name} ({join_resp.status_code})")

    final_roles = _keycloak_load_realm_composite_roles(base, headers, user_id)
    return sorted(
        {
            str(role.get("name") or "").strip().lower()
            for role in final_roles
            if isinstance(role, dict) and str(role.get("name") or "").strip()
        }
    )


# ---------------------------------------------------------------------------
# Agent / internal API-key endpoints (unchanged behaviour)
# ---------------------------------------------------------------------------

@router.get("/devices", response_model=DeviceListResponse, dependencies=[Depends(require_api_key)])
def list_devices(
    search: str | None = Query(default=None),
    status: str | None = Query(default=None, description="online|offline"),
    limit: int = Query(default=50, ge=1, le=200),
    offset: int = Query(default=0, ge=0),
    db: Session = Depends(get_db),
):
    stmt = select(Device).order_by(Device.last_seen_at.desc().nullslast(), Device.created_at.desc())
    if search:
        like = f"%{search}%"
        stmt = stmt.where((Device.host_name.ilike(like)) | (Device.primary_ip.ilike(like)))

    total = db.execute(select(func.count()).select_from(stmt.subquery())).scalar_one()
    devices = db.execute(stmt.limit(limit).offset(offset)).scalars().all()

    items: list[DeviceListItem] = []
    for d in devices:
        online = is_online(d.last_seen_at)
        if status == "online" and not online:
            continue
        if status == "offline" and online:
            continue

        items.append(
            DeviceListItem(
                device_id=d.id,
                host_name=d.host_name,
                os_name=d.os_name,
                os_version=d.os_version,
                last_seen_at=d.last_seen_at,
                online=online,
                primary_ip=d.primary_ip,
                agent_version=d.agent_version,
            )
        )

    return DeviceListResponse(items=items, total=total)


@router.get("/devices/{device_id}", response_model=DeviceDetailResponse, dependencies=[Depends(require_api_key)])
def device_detail(device_id: str, db: Session = Depends(get_db)):
    device = db.get(Device, device_id)
    if not device:
        raise HTTPException(status_code=404, detail="not found")

    return DeviceDetailResponse(
        device_id=device.id,
        device_install_id=device.device_install_id,
        host_name=device.host_name,
        os=OSInfo(name=device.os_name, version=device.os_version, build=device.os_build),
        agent=AgentInfo(version=device.agent_version, channel=device.agent_channel),
        network=NetworkInfo(primary_ip=device.primary_ip, macs=(device.macs or {}).get("macs", [])),
        last_seen_at=device.last_seen_at,
        online=is_online(device.last_seen_at),
    )


@router.get("/devices/{device_id}/inventory/latest", response_model=LatestInventoryResponse, dependencies=[Depends(require_api_key)])
def latest_inventory(device_id: str, db: Session = Depends(get_db)):
    device = db.get(Device, device_id)
    if not device:
        raise HTTPException(status_code=404, detail="not found")

    inv = db.execute(
        select(DeviceInventory)
        .where(DeviceInventory.device_id == device.id)
        .order_by(DeviceInventory.collected_at.desc())
        .limit(1)
    ).scalar_one_or_none()

    if not inv:
        raise HTTPException(status_code=404, detail="no inventory yet")

    return LatestInventoryResponse(inventory_id=inv.id, collected_at=inv.collected_at, inventory=inv.payload)


# ---------------------------------------------------------------------------
# Console browser endpoints  (Keycloak JWT auth)
# ---------------------------------------------------------------------------

@router.get("/ui/devices")
def ui_list_devices(
    search: str | None = Query(default=None),
    status: str | None = Query(default=None),
    limit: int = Query(default=100, ge=1, le=500),
    offset: int = Query(default=0, ge=0),
    db: Session = Depends(get_db),
    _user: dict = Depends(require_console_user),
):
    stmt = select(Device).order_by(Device.last_seen_at.desc().nullslast(), Device.created_at.desc())
    if search:
        like = f"%{search}%"
        stmt = stmt.where((Device.host_name.ilike(like)) | (Device.primary_ip.ilike(like)))

    total = db.execute(select(func.count()).select_from(stmt.subquery())).scalar_one()
    devices = db.execute(stmt.limit(limit).offset(offset)).scalars().all()

    items = []
    for d in devices:
        online = is_online(d.last_seen_at)
        if status == "online" and not online:
            continue
        if status == "offline" and online:
            continue
        items.append(
            {
                "id": str(d.id),
                "hostname": d.host_name or "unknown",
                "os": f"{d.os_name or ''} {d.os_version or ''}".strip() or "-",
                "agent": d.agent_version or "-",
                "lastSeen": d.last_seen_at.isoformat() if d.last_seen_at else None,
                "online": online,
                "ip": d.primary_ip,
                "blocked": d.blocked,
                "desktopVersion": d.desktop_version,
                "updaterVersion": d.updater_version,
                "updateChannel": d.update_channel,
            }
        )

    return {"items": items, "total": total}


@router.get("/ui/telemetry")
def ui_list_telemetry(
    limit: int = Query(default=50, ge=1, le=200),
    offset: int = Query(default=0, ge=0),
    category: str | None = Query(default=None),
    db: Session = Depends(get_db),
    _user: dict = Depends(require_console_user),
):
    stmt = (
        select(TelemetrySnapshot, Device.host_name)
        .join(Device, TelemetrySnapshot.device_id == Device.id)
        .order_by(TelemetrySnapshot.received_at.desc())
    )
    if category:
        stmt = stmt.where(TelemetrySnapshot.category == category)

    total_stmt = select(func.count(TelemetrySnapshot.id))
    if category:
        total_stmt = total_stmt.where(TelemetrySnapshot.category == category)
    total = db.execute(total_stmt).scalar_one()

    rows = db.execute(stmt.limit(limit).offset(offset)).all()

    items = []
    for snap, hostname in rows:
        items.append(
            {
                "id": str(snap.id),
                "category": snap.category,
                "device": hostname or "unknown",
                "receivedAt": snap.received_at.isoformat(),
                "summary": snap.summary,
                "source": snap.source,
                "severity": _severity_for_telemetry(snap.category, snap.summary, snap.payload),
            }
        )

    return {"items": items, "total": total}


@router.get("/ui/licenses")
def ui_list_licenses(
    limit: int = Query(default=100, ge=1, le=500),
    offset: int = Query(default=0, ge=0),
    db: Session = Depends(get_db),
    _user: dict = Depends(require_console_user),
):
    stmt = select(License).order_by(License.created_at.desc())
    total = db.execute(select(func.count()).select_from(stmt.subquery())).scalar_one()
    licenses = db.execute(stmt.limit(limit).offset(offset)).scalars().all()

    now = utcnow()
    items = []
    for lic in licenses:
        state = lic.state
        if state == "activated" and lic.expires_at and lic.expires_at <= now:
            state = "expired"

        items.append(
            {
                "id": lic.license_key,
                "tier": lic.tier,
                "state": state,
                "durationDays": lic.duration_days,
                "issuedAt": lic.issued_at.isoformat(),
                "activatedAt": lic.activated_at.isoformat() if lic.activated_at else None,
                "expiresAt": lic.expires_at.isoformat() if lic.expires_at else None,
                "activatedDeviceId": lic.activated_device_install_id,
                "activatedByUserId": lic.activated_by_user_id,
            }
        )

    return {"items": items, "total": total}


@router.get("/ui/accounts")
def ui_accounts(
    search: str | None = Query(default=None),
    role: str | None = Query(default=None),
    status: str | None = Query(default=None),
    limit: int = Query(default=100, ge=1, le=500),
    offset: int = Query(default=0, ge=0),
    _user: dict = Depends(require_console_user),
):
    if not _keycloak_admin_configured():
        raise HTTPException(status_code=503, detail="keycloak admin credentials missing")
    try:
        accounts = _load_keycloak_accounts()
    except Exception as exc:
        raise HTTPException(status_code=502, detail=f"keycloak users request failed: {exc}") from exc

    role_filter = (role or "").strip().lower()
    status_filter = (status or "").strip().lower()
    search_filter = (search or "").strip().lower()

    filtered: list[dict] = []
    for item in accounts:
        role_value = str(item.get("role", "")).lower()
        status_value = str(item.get("status", "")).lower()
        search_blob = " ".join(
            [
                str(item.get("name") or ""),
                str(item.get("email") or ""),
                str(item.get("id") or ""),
                role_value,
            ]
        ).lower()

        if role_filter and role_filter not in role_value:
            continue
        if status_filter and status_filter != status_value:
            continue
        if search_filter and search_filter not in search_blob:
            continue

        filtered.append(item)

    filtered.sort(key=lambda x: (str(x.get("name") or "").lower(), str(x.get("email") or "").lower()))
    total = len(filtered)
    items = filtered[offset: offset + limit]
    return {"items": items, "total": total}


@router.patch("/ui/accounts/{account_id}/role")
def ui_update_account_role(
    account_id: str,
    payload: AccountRoleUpdateRequest,
    _owner: dict = Depends(require_console_owner),
):
    if not _keycloak_admin_configured():
        raise HTTPException(status_code=503, detail="keycloak admin credentials missing")

    target_role = _normalize_assignable_role(payload.role)
    try:
        role_keys = _keycloak_update_user_role(account_id, target_role)
    except HTTPException:
        raise
    except Exception as exc:
        raise HTTPException(status_code=502, detail=f"keycloak role update failed: {exc}") from exc

    return {
        "id": account_id,
        "role": _display_role_from_roles(role_keys),
        "roleKeys": role_keys,
    }


@router.get("/ui/dashboard")
def ui_dashboard(
    db: Session = Depends(get_db),
    _user: dict = Depends(require_console_user),
):
    now = utcnow()
    threshold = now - timedelta(seconds=settings.ONLINE_THRESHOLD_SECONDS)
    since_24h = now - timedelta(hours=24)

    total_devices = db.execute(select(func.count(Device.id))).scalar_one()
    online_devices = db.execute(
        select(func.count(Device.id)).where(Device.last_seen_at >= threshold)
    ).scalar_one()
    telemetry_24h = db.execute(
        select(func.count(TelemetrySnapshot.id)).where(TelemetrySnapshot.received_at >= since_24h)
    ).scalar_one()
    total_licenses = db.execute(select(func.count(License.id))).scalar_one()
    active_licenses = db.execute(
        select(func.count(License.id)).where(License.state == "activated")
    ).scalar_one()

    recent_rows = db.execute(
        select(TelemetrySnapshot, Device.host_name)
        .join(Device, TelemetrySnapshot.device_id == Device.id)
        .order_by(TelemetrySnapshot.received_at.desc())
        .limit(10)
    ).all()

    activity = [
        {
            "id": str(snap.id),
            "type": snap.category,
            "device": hostname or "unknown",
            "time": snap.received_at.isoformat(),
            "summary": snap.summary,
        }
        for snap, hostname in recent_rows
    ]

    return {
        "kpis": {
            "totalDevices": total_devices,
            "onlineDevices": online_devices,
            "telemetry24h": telemetry_24h,
            "totalLicenses": total_licenses,
            "activeLicenses": active_licenses,
        },
        "recentActivity": activity,
    }


@router.get("/ui/activity-feed")
def ui_activity_feed(
    limit: int = Query(default=50, ge=1, le=200),
    offset: int = Query(default=0, ge=0),
    db: Session = Depends(get_db),
    _user: dict = Depends(require_console_user),
):
    fetch = limit + offset  # fetch enough to support the offset window

    events: list[tuple[datetime, dict]] = []

    telemetry_rows = db.execute(
        select(TelemetrySnapshot, Device.host_name)
        .join(Device, TelemetrySnapshot.device_id == Device.id)
        .order_by(TelemetrySnapshot.received_at.desc())
        .limit(fetch)
    ).all()
    for snap, host_name in telemetry_rows:
        severity = _severity_for_telemetry(snap.category, snap.summary, snap.payload)
        events.append(
            (
                snap.received_at,
                {
                    "id": str(snap.id),
                    "type": "telemetry",
                    "user": "System",
                    "action": "hat Telemetrie empfangen",
                    "target": host_name or "unknown",
                    "description": snap.summary or snap.category,
                    "timestamp": snap.received_at.isoformat(),
                    "category": "telemetry",
                    "severity": severity,
                },
            )
        )

    license_rows = db.execute(
        select(License).order_by(License.updated_at.desc()).limit(fetch)
    ).scalars().all()
    for lic in license_rows:
        stamp = lic.activated_at or lic.updated_at or lic.created_at
        if not stamp:
            continue
        action = "Lizenz aktiviert" if lic.state == "activated" else f"Lizenzstatus: {lic.state}"
        actor = lic.activated_by_user_id or "System"
        target = lic.license_key
        desc = f"Tarif {lic.tier}"
        if lic.expires_at:
            desc = f"{desc}, läuft ab {lic.expires_at.date().isoformat()}"
        events.append(
            (
                stamp,
                {
                    "id": f"license:{lic.id}",
                    "type": "license",
                    "user": actor,
                    "action": action,
                    "target": target,
                    "description": desc,
                    "timestamp": stamp.isoformat(),
                    "category": "license",
                    "severity": "warning" if lic.state in {"expired", "revoked"} else "info",
                },
            )
        )

    device_rows = db.execute(
        select(Device).order_by(Device.created_at.desc()).limit(fetch)
    ).scalars().all()
    for device in device_rows:
        stamp = device.created_at or device.last_seen_at
        if not stamp:
            continue
        events.append(
            (
                stamp,
                {
                    "id": f"device:{device.id}",
                    "type": "device",
                    "user": "System",
                    "action": "hat ein Gerät registriert",
                    "target": device.host_name or device.device_install_id,
                    "description": f"Install-ID {device.device_install_id}",
                    "timestamp": stamp.isoformat(),
                    "category": "device",
                    "severity": "info",
                },
            )
        )

    events.sort(key=lambda pair: pair[0], reverse=True)
    total = len(events)
    items = [payload for _, payload in events[offset: offset + limit]]
    return {"items": items, "total": total}


@router.get("/ui/knowledge-base")
def ui_knowledge_base(
    search: str | None = Query(default=None, max_length=200),
    limit: int = Query(default=50, ge=1, le=200),
    offset: int = Query(default=0, ge=0),
    db: Session = Depends(get_db),
    _user: dict = Depends(require_console_user),
):
    """Return published KB articles with optional full-text search on title/body."""
    q = select(KbArticle).where(KbArticle.published == True)  # noqa: E712

    if search and search.strip():
        term = f"%{search.strip()}%"
        q = q.where(
            or_(
                KbArticle.title.ilike(term),
                KbArticle.body_md.ilike(term),
            )
        )

    total: int = db.execute(
        select(func.count()).select_from(q.subquery())
    ).scalar_one()

    rows = db.execute(
        q.order_by(KbArticle.updated_at.desc()).limit(limit).offset(offset)
    ).scalars().all()

    items = [
        {
            "id": str(a.id),
            "title": a.title,
            "category": a.category,
            "tags": a.tags if isinstance(a.tags, list) else [],
            "updated_at": a.updated_at.isoformat(),
            "summary": (a.body_md[:160].rstrip() + "…") if a.body_md else "",
        }
        for a in rows
    ]
    return {"items": items, "total": total}


@router.get("/ui/audit-log")
def ui_audit_log(
    limit: int = Query(default=100, ge=1, le=500),
    offset: int = Query(default=0, ge=0),
    db: Session = Depends(get_db),
    _user: dict = Depends(require_console_user),
):
    logs: list[tuple[datetime, dict]] = []

    license_rows = db.execute(
        select(License).order_by(License.updated_at.desc()).limit(500)
    ).scalars().all()
    for lic in license_rows:
        stamp = lic.updated_at or lic.created_at
        if not stamp:
            continue
        actor = lic.activated_by_user_id or "System"
        action = (
            "Lizenz aktiviert"
            if lic.state == "activated"
            else "Lizenzstatus aktualisiert"
        )
        logs.append(
            (
                stamp,
                {
                    "id": f"audit:license:{lic.id}",
                    "time": stamp.isoformat(),
                    "actor": actor,
                    "action": action,
                    "target": lic.license_key,
                    "ip": "-",
                    "result": "success",
                },
            )
        )

    telemetry_rows = db.execute(
        select(TelemetrySnapshot, Device.host_name, Device.primary_ip)
        .join(Device, TelemetrySnapshot.device_id == Device.id)
        .order_by(TelemetrySnapshot.received_at.desc())
        .limit(500)
    ).all()
    for snap, host_name, primary_ip in telemetry_rows:
        severity = _severity_for_telemetry(snap.category, snap.summary, snap.payload)
        logs.append(
            (
                snap.received_at,
                {
                    "id": f"audit:telemetry:{snap.id}",
                    "time": snap.received_at.isoformat(),
                    "actor": "System",
                    "action": f"Telemetrie {snap.category}",
                    "target": host_name or "unknown",
                    "ip": primary_ip or "-",
                    "result": "failed" if severity == "critical" else "success",
                },
            )
        )

    device_rows = db.execute(
        select(Device).order_by(Device.created_at.desc()).limit(500)
    ).scalars().all()
    for device in device_rows:
        if not device.created_at:
            continue
        logs.append(
            (
                device.created_at,
                {
                    "id": f"audit:device:{device.id}",
                    "time": device.created_at.isoformat(),
                    "actor": "System",
                    "action": "Gerät registriert",
                    "target": device.host_name or device.device_install_id,
                    "ip": device.primary_ip or "-",
                    "result": "success",
                },
            )
        )

    logs.sort(key=lambda pair: pair[0], reverse=True)
    total = len(logs)
    items = [entry for _, entry in logs[offset: offset + limit]]
    return {"items": items, "total": total}


@router.get("/ui/database/hosts")
def ui_database_hosts(
    search: str | None = Query(default=None),
    db: Session = Depends(get_db),
    _user: dict = Depends(require_console_user),
):
    stmt = (
        select(
            Device.id,
            Device.device_install_id,
            Device.host_name,
            Device.last_seen_at,
            func.count(TelemetrySnapshot.id).label("record_count"),
        )
        .outerjoin(TelemetrySnapshot, TelemetrySnapshot.device_id == Device.id)
        .group_by(Device.id, Device.device_install_id, Device.host_name, Device.last_seen_at)
        .order_by(Device.last_seen_at.desc().nullslast())
    )
    if search:
        like = f"%{search}%"
        stmt = stmt.where(or_(Device.host_name.ilike(like), Device.device_install_id.ilike(like)))

    rows = db.execute(stmt).all()
    items = [
        {
            "id": str(row.id),
            "deviceInstallId": row.device_install_id,
            "hostname": row.host_name or row.device_install_id,
            "lastSeen": row.last_seen_at.isoformat() if row.last_seen_at else None,
            "recordCount": int(row.record_count or 0),
        }
        for row in rows
    ]
    return {"items": items, "total": len(items)}


@router.get("/ui/database/payloads")
def ui_database_payloads(
    device_id: str = Query(..., description="UUID id oder device_install_id"),
    limit: int = Query(default=50, ge=1, le=500),
    offset: int = Query(default=0, ge=0),
    db: Session = Depends(get_db),
    _user: dict = Depends(require_console_user),
):
    device = db.execute(
        select(Device).where(
            or_(cast(Device.id, String) == device_id.strip(), Device.device_install_id == device_id.strip())
        )
    ).scalar_one_or_none()
    if not device:
        raise HTTPException(status_code=404, detail="device not found")

    stmt = (
        select(TelemetrySnapshot)
        .where(TelemetrySnapshot.device_id == device.id)
        .order_by(TelemetrySnapshot.received_at.desc())
    )
    total = db.execute(select(func.count()).select_from(stmt.subquery())).scalar_one()
    rows = db.execute(stmt.limit(limit).offset(offset)).scalars().all()

    category_counts_rows = db.execute(
        select(TelemetrySnapshot.category, func.count(TelemetrySnapshot.id))
        .where(TelemetrySnapshot.device_id == device.id)
        .group_by(TelemetrySnapshot.category)
        .order_by(func.count(TelemetrySnapshot.id).desc(), TelemetrySnapshot.category.asc())
    ).all()
    category_counts = [{"name": cat, "count": int(cnt)} for cat, cnt in category_counts_rows]

    payloads = [
        {
            "id": str(row.id),
            "receivedAt": row.received_at.isoformat(),
            "category": row.category,
            "summary": row.summary,
            "source": row.source,
            "payload": row.payload,
        }
        for row in rows
    ]

    return {
        "device": {
            "id": str(device.id),
            "deviceInstallId": device.device_install_id,
            "hostname": device.host_name or device.device_install_id,
            "lastSeen": device.last_seen_at.isoformat() if device.last_seen_at else None,
        },
        "categoryCounts": category_counts,
        "items": payloads,
        "total": total,
    }


@router.get("/ui/server/containers")
def ui_server_containers(
    _user: dict = Depends(require_console_user),
):
    """Docker container status (requires /var/run/docker.sock mounted)."""
    try:
        import docker as docker_sdk

        client = docker_sdk.from_env()
        containers = client.containers.list(all=True)

        result = []
        for c in containers:
            cpu_pct = 0.0
            mem_mb = 0

            if c.status == "running":
                try:
                    stats_raw = c.stats(stream=False)
                    cpu_delta = (
                        stats_raw["cpu_stats"]["cpu_usage"]["total_usage"]
                        - stats_raw["precpu_stats"]["cpu_usage"]["total_usage"]
                    )
                    sys_delta = (
                        stats_raw["cpu_stats"]["system_cpu_usage"]
                        - stats_raw["precpu_stats"]["system_cpu_usage"]
                    )
                    num_cpus = stats_raw["cpu_stats"].get("online_cpus", 1)
                    if sys_delta > 0:
                        cpu_pct = round((cpu_delta / sys_delta) * num_cpus * 100.0, 1)
                    mem_bytes = stats_raw["memory_stats"].get("usage", 0)
                    mem_mb = round(mem_bytes / 1024 / 1024)
                except Exception:
                    pass

            result.append(
                {
                    "name": c.name,
                    "status": c.status,
                    "image": c.image.tags[0] if c.image.tags else c.image.short_id,
                    "cpuPercent": cpu_pct,
                    "memoryMb": mem_mb,
                }
            )

        return {"containers": result}

    except Exception as e:
        return {"containers": [], "error": str(e)}


@router.get("/ui/server/host")
def ui_server_host(
    _user: dict = Depends(require_console_user),
):
    """Host-System Metriken (CPU, RAM, Disk, Uptime)."""
    try:
        import time

        import psutil

        cpu_percent = psutil.cpu_percent(interval=0.2)
        mem = psutil.virtual_memory()
        disk = psutil.disk_usage("/")
        uptime_seconds = int(time.time() - psutil.boot_time())

        return {
            "cpu_percent": cpu_percent,
            "memory": {
                "total_mb": round(mem.total / 1024 / 1024),
                "used_mb": round(mem.used / 1024 / 1024),
                "percent": mem.percent,
            },
            "disk": {
                "total_gb": round(disk.total / 1024 / 1024 / 1024, 1),
                "used_gb": round(disk.used / 1024 / 1024 / 1024, 1),
                "percent": disk.percent,
            },
            "uptime_seconds": uptime_seconds,
        }
    except Exception as e:
        return {"error": str(e)}


# ---------------------------------------------------------------------------
# Plans (public + admin)
# ---------------------------------------------------------------------------

_DEFAULT_PLANS: list[dict] = [
    {"id": "trial",        "label": "Testversion",    "price_eur": 0.0,  "duration_days": 7,   "max_devices": 1,    "sort_order": 0, "grace_period_days": 0,  "feature_flags": {"auto_fix": False, "reports": False, "priority_support": False}},
    {"id": "standard",     "label": "Standard",       "price_eur": 4.99, "duration_days": 30,  "max_devices": 3,    "sort_order": 1, "grace_period_days": 7,  "feature_flags": {"auto_fix": True,  "reports": False, "priority_support": False}},
    {"id": "professional", "label": "Professional",   "price_eur": 49.99,"duration_days": 365, "max_devices": None, "sort_order": 2, "grace_period_days": 14, "feature_flags": {"auto_fix": True,  "reports": True,  "priority_support": True}},
    {"id": "unlimited",    "label": "Unbegrenzt",     "price_eur": None, "duration_days": None,"max_devices": None, "sort_order": 3, "grace_period_days": 0,  "feature_flags": {"auto_fix": True,  "reports": True,  "priority_support": True}},
    {"id": "custom",       "label": "Custom",         "price_eur": None, "duration_days": None,"max_devices": None, "sort_order": 4, "grace_period_days": 7,  "feature_flags": {"auto_fix": True,  "reports": True,  "priority_support": False}},
]


def _seed_default_plans(db: Session) -> None:
    for p in _DEFAULT_PLANS:
        existing = db.get(Plan, p["id"])
        if existing is None:
            db.add(Plan(**p, is_active=True))
    db.commit()


def _plan_to_item(p: Plan) -> PlanItemExtended:
    return PlanItemExtended(
        id=p.id,
        label=p.label,
        price_eur=p.price_eur,
        duration_days=p.duration_days,
        max_devices=p.max_devices,
        is_active=p.is_active,
        sort_order=p.sort_order,
        feature_flags=p.feature_flags,
        grace_period_days=p.grace_period_days,
        stripe_price_id=p.stripe_price_id,
        amount_cents=p.amount_cents,
        currency=p.currency or "eur",
        price_version=p.price_version or 1,
        stripe_product_id=p.stripe_product_id,
    )


@router.get("/ui/plans", response_model=PlanListResponseExtended)
def ui_list_plans(
    db: Session = Depends(get_db),
    _user: dict = Depends(require_console_user),
):
    _seed_default_plans(db)
    plans = db.execute(select(Plan).order_by(Plan.sort_order, Plan.id)).scalars().all()
    items = [_plan_to_item(p) for p in plans]
    return PlanListResponseExtended(items=items, total=len(items))


@router.get("/public/plans", response_model=PlanListResponseExtended)
def public_list_plans(
    db: Session = Depends(get_db),
):
    """Public plan list for home portal pricing/checkout."""
    _seed_default_plans(db)
    plans = db.execute(
        select(Plan)
        .where(Plan.is_active.is_(True))
        .order_by(Plan.sort_order, Plan.id)
    ).scalars().all()
    items = [_plan_to_item(p) for p in plans]
    return PlanListResponseExtended(items=items, total=len(items))


@router.put("/ui/plans/{plan_id}", response_model=PlanItemExtended)
def ui_upsert_plan(
    plan_id: str,
    payload: PlanUpsertRequestExtended,
    db: Session = Depends(get_db),
    _owner: dict = Depends(require_console_owner),
):
    plan = db.get(Plan, plan_id.strip().lower())
    if plan is None:
        plan = Plan(id=plan_id.strip().lower())
        db.add(plan)

    plan.label = payload.label
    plan.price_eur = payload.price_eur
    plan.duration_days = payload.duration_days
    plan.max_devices = payload.max_devices
    plan.is_active = payload.is_active
    plan.sort_order = payload.sort_order
    if payload.feature_flags is not None:
        plan.feature_flags = payload.feature_flags
    plan.grace_period_days = payload.grace_period_days
    if payload.stripe_price_id is not None:
        plan.stripe_price_id = payload.stripe_price_id
    if payload.amount_cents is not None:
        plan.amount_cents = payload.amount_cents
    if payload.currency:
        plan.currency = payload.currency
    db.commit()
    db.refresh(plan)

    return _plan_to_item(plan)


# ---------------------------------------------------------------------------
# Plan Stripe price publish / status
# ---------------------------------------------------------------------------

from ..services.stripe_service import (
    create_new_price_for_plan,
    ensure_product_for_plan,
    migrate_subscriptions_to_price,
)


@router.post("/ui/plans/{plan_id}/publish-price", response_model=PublishPriceResponse)
def ui_publish_plan_price(
    plan_id: str,
    payload: PublishPriceRequest,
    db: Session = Depends(get_db),
    _owner: dict = Depends(require_console_owner),
):
    """
    Publish a new Stripe Price for the plan and migrate all active subscriptions
    to it (Mode A: new price takes effect at next renewal – proration_behavior=none).

    Prerequisites:
    - plan.amount_cents must be set
    - plan.is_active must be True
    - STRIPE_SECRET_KEY must be configured
    """
    import logging as _logging

    _log = _logging.getLogger("pcw.console.publish_price")

    if not settings.STRIPE_SECRET_KEY:
        raise HTTPException(status_code=503, detail="Stripe not configured (STRIPE_SECRET_KEY missing)")

    plan = db.get(Plan, plan_id)
    if not plan:
        raise HTTPException(status_code=404, detail="plan not found")
    if not plan.is_active:
        raise HTTPException(status_code=400, detail="plan is not active")
    if not plan.amount_cents or plan.amount_cents <= 0:
        raise HTTPException(
            status_code=400,
            detail="plan.amount_cents must be set and > 0 before publishing a Stripe price",
        )

    old_price_id: str | None = plan.stripe_price_id

    if payload.dry_run:
        return PublishPriceResponse(
            plan_id=plan_id,
            old_price_id=old_price_id,
            new_price_id="(dry-run)",
            migrated=0,
            failed=0,
            failed_subscription_ids=[],
            mode=payload.mode,
            took_ms=0,
        )

    try:
        new_price_id = create_new_price_for_plan(plan, db)
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc
    except Exception as exc:
        _log.exception("Stripe price creation failed for plan %s", plan_id)
        raise HTTPException(status_code=502, detail=f"Stripe error: {exc}") from exc

    summary = migrate_subscriptions_to_price(plan, new_price_id, old_price_id, db)

    return PublishPriceResponse(
        plan_id=plan_id,
        old_price_id=old_price_id,
        new_price_id=new_price_id,
        migrated=summary.migrated_count,
        failed=summary.failed_count,
        failed_subscription_ids=summary.failed_subscription_ids,
        mode=payload.mode,
        took_ms=summary.took_ms,
    )


@router.get("/ui/plans/{plan_id}/stripe-status", response_model=StripePlanStatusResponse)
def ui_plan_stripe_status(
    plan_id: str,
    db: Session = Depends(get_db),
    _user: dict = Depends(require_console_user),
):
    """Return Stripe product/price info and active subscription count for a plan."""
    from ..models import Subscription as _Sub

    plan = db.get(Plan, plan_id)
    if not plan:
        raise HTTPException(status_code=404, detail="plan not found")

    active_statuses = {"active", "trialing", "past_due"}
    count_active = db.execute(
        select(func.count(_Sub.id)).where(
            _Sub.plan_id == plan_id,
            _Sub.stripe_subscription_id.isnot(None),
            _Sub.status.in_(active_statuses),
        )
    ).scalar_one()

    return StripePlanStatusResponse(
        plan_id=plan_id,
        stripe_product_id=plan.stripe_product_id,
        stripe_price_id=plan.stripe_price_id,
        price_version=plan.price_version,
        amount_cents=plan.amount_cents,
        currency=plan.currency,
        count_active_subs=count_active,
    )


# ---------------------------------------------------------------------------
# License admin (create + revoke)
# ---------------------------------------------------------------------------

import secrets
import string


def _generate_license_key() -> str:
    alphabet = string.ascii_uppercase + string.digits
    segments = ["".join(secrets.choice(alphabet) for _ in range(4)) for _ in range(3)]
    return "-".join(segments)


TIER_DURATION: dict[str, int | None] = {
    "trial":        7,
    "standard":     30,
    "professional": 365,
    "unlimited":    None,
    "custom":       None,
}


@router.post("/ui/licenses", response_model=LicenseCreateResponse)
def ui_create_licenses(
    payload: LicenseCreateRequest,
    db: Session = Depends(get_db),
    _owner: dict = Depends(require_console_owner),
):
    plan = db.get(Plan, payload.tier)
    duration_days = plan.duration_days if plan else TIER_DURATION.get(payload.tier)

    created: list[LicenseInfo] = []
    for _ in range(payload.quantity):
        for _attempt in range(10):
            key = _generate_license_key()
            exists = db.execute(select(License).where(License.license_key == key)).scalar_one_or_none()
            if not exists:
                break
        else:
            raise HTTPException(status_code=500, detail="could not generate unique license key")

        lic = License(
            license_key=key,
            tier=payload.tier,
            duration_days=duration_days,
            state="issued",
        )
        db.add(lic)
        db.flush()
        created.append(
            LicenseInfo(
                license_key=lic.license_key,
                tier=lic.tier,
                state=lic.state,
                duration_days=lic.duration_days,
                issued_at=lic.issued_at,
                activated_at=None,
                expires_at=None,
                activated_device_install_id=None,
                activated_by_user_id=None,
            )
        )

    db.commit()
    return LicenseCreateResponse(ok=True, licenses=created)


@router.patch("/ui/licenses/{license_key}/revoke")
def ui_revoke_license(
    license_key: str,
    db: Session = Depends(get_db),
    _owner: dict = Depends(require_console_owner),
):
    key = license_key.strip().upper().replace(" ", "")
    lic = db.execute(select(License).where(License.license_key == key)).scalar_one_or_none()
    if not lic:
        raise HTTPException(status_code=404, detail="license not found")
    if lic.state == "revoked":
        raise HTTPException(status_code=409, detail="license already revoked")

    lic.state = "revoked"
    db.commit()
    return {"ok": True, "license_key": lic.license_key, "state": "revoked"}


# ---------------------------------------------------------------------------
# Subscription admin
# ---------------------------------------------------------------------------

@router.get("/ui/subscriptions", response_model=SubscriptionListResponse)
def ui_list_subscriptions(
    limit: int = Query(default=50, ge=1, le=200),
    offset: int = Query(default=0, ge=0),
    db: Session = Depends(get_db),
    _owner: dict = Depends(require_console_owner),
):
    from ..models import Subscription as _Sub
    total = db.execute(select(func.count(_Sub.id))).scalar_one()
    rows = db.execute(
        select(_Sub).order_by(_Sub.created_at.desc()).limit(limit).offset(offset)
    ).scalars().all()
    items = [
        SubscriptionItem(
            id=str(s.id),
            keycloak_user_id=s.keycloak_user_id,
            plan_id=s.plan_id,
            status=s.status,
            stripe_customer_id=s.stripe_customer_id,
            stripe_subscription_id=s.stripe_subscription_id,
            allow_self_cancel=s.allow_self_cancel,
            current_period_end=s.current_period_end.isoformat() if s.current_period_end else None,
        )
        for s in rows
    ]
    return SubscriptionListResponse(items=items, total=total)


@router.get("/ui/subscriptions/{sub_id}", response_model=SubscriptionItem)
def ui_get_subscription(
    sub_id: str,
    db: Session = Depends(get_db),
    _owner: dict = Depends(require_console_owner),
):
    from ..models import Subscription as _Sub
    sub = db.get(_Sub, sub_id)
    if not sub:
        raise HTTPException(status_code=404, detail="subscription not found")
    return SubscriptionItem(
        id=str(sub.id),
        keycloak_user_id=sub.keycloak_user_id,
        plan_id=sub.plan_id,
        status=sub.status,
        stripe_customer_id=sub.stripe_customer_id,
        stripe_subscription_id=sub.stripe_subscription_id,
        allow_self_cancel=sub.allow_self_cancel,
        current_period_end=sub.current_period_end.isoformat() if sub.current_period_end else None,
    )


@router.patch("/ui/subscriptions/{sub_id}", response_model=SubscriptionItem)
def ui_patch_subscription(
    sub_id: str,
    payload: SubscriptionPatchRequest,
    db: Session = Depends(get_db),
    _owner: dict = Depends(require_console_owner),
):
    from ..models import Subscription as _Sub
    sub = db.get(_Sub, sub_id)
    if not sub:
        raise HTTPException(status_code=404, detail="subscription not found")
    sub.allow_self_cancel = payload.allow_self_cancel
    db.commit()
    db.refresh(sub)
    return SubscriptionItem(
        id=str(sub.id),
        keycloak_user_id=sub.keycloak_user_id,
        plan_id=sub.plan_id,
        status=sub.status,
        stripe_customer_id=sub.stripe_customer_id,
        stripe_subscription_id=sub.stripe_subscription_id,
        allow_self_cancel=sub.allow_self_cancel,
        current_period_end=sub.current_period_end.isoformat() if sub.current_period_end else None,
    )


# ---------------------------------------------------------------------------
# Notifications (generiert aus bestehenden Daten)
# ---------------------------------------------------------------------------

@router.get("/ui/notifications")
def ui_notifications(
    limit: int = Query(default=50, ge=1, le=200),
    db: Session = Depends(get_db),
    _user: dict = Depends(require_console_user),
):
    user_id = str(_user.get("sub") or "").strip()
    if not user_id:
        raise HTTPException(status_code=401, detail="user_sub_missing")

    generated: list[dict] = []
    now = utcnow()

    # Kritische + Warning Telemetrie der letzten 24h
    since_24h = now - timedelta(hours=24)
    tele_rows = db.execute(
        select(TelemetrySnapshot, Device.host_name)
        .join(Device, TelemetrySnapshot.device_id == Device.id)
        .where(TelemetrySnapshot.received_at >= since_24h)
        .order_by(TelemetrySnapshot.received_at.desc())
        .limit(200)
    ).all()
    for snap, hostname in tele_rows:
        severity = _severity_for_telemetry(snap.category, snap.summary, snap.payload)
        if severity in ("critical", "warning"):
            generated.append({
                "id": f"telemetry:{snap.id}",
                "type": "telemetry",
                "severity": severity,
                "title": f"{snap.category.capitalize()} Alert – {hostname or 'unknown'}",
                "message": snap.summary or snap.category,
                "timestamp": snap.received_at.isoformat(),
                "read": False,
            })

    # Ablaufende Lizenzen (nächste 7 Tage)
    soon = now + timedelta(days=7)
    expiring = db.execute(
        select(License).where(
            License.state == "activated",
            License.expires_at.isnot(None),
            License.expires_at <= soon,
            License.expires_at > now,
        ).order_by(License.expires_at.asc())
    ).scalars().all()
    for lic in expiring:
        days_left = max(0, (lic.expires_at - now).days)
        generated.append({
            "id": f"license:expiring:{lic.id}",
            "type": "license",
            "severity": "warning",
            "title": f"Lizenz läuft in {days_left} Tag(en) ab",
            "message": f"{lic.license_key} ({lic.tier})",
            "timestamp": now.isoformat(),
            "read": False,
        })

    # Abgelaufene Lizenzen
    expired = db.execute(
        select(License).where(License.state == "expired").order_by(License.expires_at.desc()).limit(20)
    ).scalars().all()
    for lic in expired:
        stamp = lic.expires_at or lic.updated_at or now
        generated.append({
            "id": f"license:expired:{lic.id}",
            "type": "license",
            "severity": "critical",
            "title": "Lizenz abgelaufen",
            "message": f"{lic.license_key} ({lic.tier})",
            "timestamp": stamp.isoformat(),
            "read": False,
        })

    # Geräte offline > 1 Stunde (aber aktiv in letzten 7 Tagen)
    offline_since = now - timedelta(hours=1)
    active_since = now - timedelta(days=7)
    offline_devices = db.execute(
        select(Device).where(
            Device.last_seen_at.isnot(None),
            Device.last_seen_at < offline_since,
            Device.last_seen_at > active_since,
        ).order_by(Device.last_seen_at.desc()).limit(20)
    ).scalars().all()
    for device in offline_devices:
        generated.append({
            "id": f"device:offline:{device.id}",
            "type": "device",
            "severity": "warning",
            "title": "Gerät offline",
            "message": device.host_name or device.device_install_id,
            "timestamp": device.last_seen_at.isoformat(),
            "read": False,
        })

    generated.sort(key=lambda x: x["timestamp"], reverse=True)

    # Persist generated notifications (idempotent by user_id + external_id in meta).
    created_any = False
    for item in generated:
        external_id = str(item["id"])
        existing = db.execute(
            select(Notification).where(
                Notification.user_id == user_id,
                Notification.meta["external_id"].astext == external_id,
            )
        ).scalar_one_or_none()
        if existing is None:
            db.add(
                Notification(
                    user_id=user_id,
                    type=str(item["type"]),
                    title=str(item["title"]),
                    body=str(item["message"]),
                    meta={
                        "severity": str(item.get("severity") or "info"),
                        "external_id": external_id,
                    },
                )
            )
            created_any = True
    if created_any:
        db.commit()

    total = db.execute(
        select(func.count()).select_from(Notification).where(Notification.user_id == user_id)
    ).scalar_one()
    rows = db.execute(
        select(Notification)
        .where(Notification.user_id == user_id)
        .order_by(Notification.created_at.desc())
        .limit(limit)
    ).scalars().all()

    items = [
        {
            "id": str(row.id),
            "type": row.type,
            "severity": (row.meta or {}).get("severity", "info"),
            "title": row.title,
            "message": row.body,
            "timestamp": row.created_at.isoformat() if row.created_at else None,
            "read": row.read_at is not None,
        }
        for row in rows
    ]
    return {"items": items, "total": int(total)}


@router.post("/ui/notifications/{notification_id}/read")
def ui_notification_mark_read(
    notification_id: str,
    db: Session = Depends(get_db),
    _user: dict = Depends(require_console_user),
):
    user_id = str(_user.get("sub") or "").strip()
    if not user_id:
        raise HTTPException(status_code=401, detail="user_sub_missing")

    try:
        notif_uuid = uuid.UUID(notification_id.strip())
    except ValueError:
        return {"ok": True, "id": notification_id}

    row = db.execute(
        select(Notification).where(Notification.id == notif_uuid, Notification.user_id == user_id)
    ).scalar_one_or_none()
    if row and row.read_at is None:
        row.read_at = utcnow()
        db.commit()

    return {"ok": True, "id": notification_id}


# ---------------------------------------------------------------------------
# Global Search
# ---------------------------------------------------------------------------

@router.get("/ui/search")
def ui_search(
    q: str = Query(..., min_length=2, description="Suchbegriff"),
    limit: int = Query(default=20, ge=1, le=100),
    db: Session = Depends(get_db),
    _user: dict = Depends(require_console_user),
):
    results: list[dict] = []
    like = f"%{q}%"

    devices = db.execute(
        select(Device).where(
            or_(Device.host_name.ilike(like), Device.primary_ip.ilike(like), Device.device_install_id.ilike(like))
        ).limit(limit)
    ).scalars().all()
    for d in devices:
        results.append({
            "type": "device",
            "id": str(d.id),
            "title": d.host_name or d.device_install_id,
            "subtitle": d.primary_ip or "-",
            "url": f"/devices/{d.id}",
        })

    licenses = db.execute(
        select(License).where(
            or_(License.license_key.ilike(like), License.tier.ilike(like))
        ).limit(limit)
    ).scalars().all()
    for lic in licenses:
        results.append({
            "type": "license",
            "id": str(lic.id),
            "title": lic.license_key,
            "subtitle": f"{lic.tier} – {lic.state}",
            "url": f"/licenses/{lic.license_key}",
        })

    tele_rows = db.execute(
        select(TelemetrySnapshot, Device.host_name)
        .join(Device, TelemetrySnapshot.device_id == Device.id)
        .where(
            or_(
                TelemetrySnapshot.summary.ilike(like),
                TelemetrySnapshot.category.ilike(like),
                Device.host_name.ilike(like),
            )
        )
        .order_by(TelemetrySnapshot.received_at.desc())
        .limit(limit)
    ).all()
    for snap, hostname in tele_rows:
        results.append({
            "type": "telemetry",
            "id": str(snap.id),
            "title": f"{snap.category.capitalize()} – {hostname or 'unknown'}",
            "subtitle": snap.summary or "-",
            "url": f"/telemetry/{snap.id}",
        })

    return {"items": results[:limit], "total": len(results), "query": q}


# ---------------------------------------------------------------------------
# Telemetrie Charts
# ---------------------------------------------------------------------------

@router.get("/ui/telemetry/chart")
def ui_telemetry_chart(
    category: str = Query(..., description="memory|ssd|antivirus|update"),
    hours: int = Query(default=24, ge=1, le=168),
    device_id: str | None = Query(default=None),
    db: Session = Depends(get_db),
    _user: dict = Depends(require_console_user),
):
    now = utcnow()
    since = now - timedelta(hours=hours)

    stmt = (
        select(TelemetrySnapshot, Device.host_name)
        .join(Device, TelemetrySnapshot.device_id == Device.id)
        .where(
            TelemetrySnapshot.category == category,
            TelemetrySnapshot.received_at >= since,
        )
        .order_by(TelemetrySnapshot.received_at.asc())
    )

    if device_id:
        device = db.execute(
            select(Device).where(
                or_(cast(Device.id, String) == device_id.strip(), Device.device_install_id == device_id.strip())
            )
        ).scalar_one_or_none()
        if device:
            stmt = stmt.where(TelemetrySnapshot.device_id == device.id)

    rows = db.execute(stmt).all()
    points = [
        {
            "timestamp": snap.received_at.isoformat(),
            "device": hostname or "unknown",
            "summary": snap.summary,
            "severity": _severity_for_telemetry(snap.category, snap.summary, snap.payload),
            "id": str(snap.id),
        }
        for snap, hostname in rows
    ]
    return {"category": category, "hours": hours, "points": points, "total": len(points)}


# ---------------------------------------------------------------------------
# Server Services (Docker Container als Services)
# ---------------------------------------------------------------------------

@router.get("/ui/server/services")
def ui_server_services(
    _user: dict = Depends(require_console_user),
):
    try:
        import docker as docker_sdk

        client = docker_sdk.from_env()
        containers = client.containers.list(all=True)
        services = [
            {
                "name": c.name,
                "status": c.status,
                "running": c.status == "running",
                "image": c.image.tags[0] if c.image.tags else c.image.short_id,
                "startedAt": c.attrs.get("State", {}).get("StartedAt"),
            }
            for c in containers
        ]
        return {"items": services, "total": len(services)}
    except Exception as e:
        return {"items": [], "total": 0, "error": str(e)}


# ---------------------------------------------------------------------------
# Knowledge Base (statische Artikel)
# ---------------------------------------------------------------------------

_KB_ARTICLES = [
    {"id": "kb-1", "category": "Lizenzen",     "title": "Lizenzschlüssel aktivieren",         "summary": "Anleitung zur Aktivierung eines PCWächter-Lizenzschlüssels auf einem Gerät.",              "tags": ["lizenz", "aktivierung", "agent"]},
    {"id": "kb-2", "category": "Installation", "title": "PCWächter Agent installieren",        "summary": "Schritt-für-Schritt Anleitung zur Installation des PCWächter Agents auf Windows.",         "tags": ["agent", "installation", "windows"]},
    {"id": "kb-3", "category": "Telemetrie",   "title": "Telemetrie-Kategorien erklärt",       "summary": "Übersicht der Kategorien: memory, ssd, antivirus, update und ihre Bedeutung.",             "tags": ["telemetrie", "kategorien", "memory", "ssd"]},
    {"id": "kb-4", "category": "Verwaltung",   "title": "Benutzerrollen verwalten",            "summary": "Wie man Benutzern Rollen zuweist (Owner, Admin, User) und was diese Rollen erlauben.",     "tags": ["rollen", "benutzer", "keycloak", "rechte"]},
    {"id": "kb-5", "category": "Lizenzen",     "title": "Lizenzpläne anpassen",                "summary": "Preise und Laufzeiten der Lizenzpläne live ändern ohne Code-Deployment.",                  "tags": ["pläne", "preise", "laufzeit", "admin"]},
    {"id": "kb-6", "category": "Geräte",       "title": "Gerät aus der Verwaltung entfernen",  "summary": "So entfernt man ein Gerät vollständig aus dem PCWächter-System.",                          "tags": ["gerät", "entfernen", "delete"]},
    {"id": "kb-7", "category": "Server",       "title": "Server-Health überwachen",            "summary": "Übersicht der Server-Metriken: CPU, RAM, Festplatte und Docker-Container-Status.",         "tags": ["server", "health", "monitoring", "docker"]},
    {"id": "kb-8", "category": "Sicherheit",   "title": "Audit-Log verstehen",                 "summary": "Was wird im Audit-Log protokolliert und wie liest man die Einträge richtig.",              "tags": ["audit", "sicherheit", "protokoll", "log"]},
    {"id": "kb-9", "category": "Lizenzen",     "title": "Trial-Lizenzen verwalten",            "summary": "Testlizenzen (7 Tage) ausstellen, verlängern oder in bezahlte Pläne umwandeln.",          "tags": ["trial", "testversion", "lizenz", "upgrade"]},
    {"id": "kb-10","category": "Geräte",       "title": "Online-Status eines Geräts prüfen",   "summary": "Wann gilt ein Gerät als online? Schwellwert und Heartbeat-Logik erklärt.",                 "tags": ["online", "heartbeat", "status", "gerät"]},
]


@router.get("/ui/knowledge-base")
def ui_knowledge_base(
    search: str | None = Query(default=None),
    category: str | None = Query(default=None),
    _user: dict = Depends(require_console_user),
):
    articles = _KB_ARTICLES

    if category:
        articles = [a for a in articles if a["category"].lower() == category.strip().lower()]

    if search:
        q = search.strip().lower()
        articles = [
            a for a in articles
            if q in a["title"].lower() or q in a["summary"].lower() or any(q in t for t in a["tags"])
        ]

    return {"items": articles, "total": len(articles)}


# ---------------------------------------------------------------------------
# Figma Make Preview – read-only snapshot, auth via X-Preview-Key header
# ---------------------------------------------------------------------------

from fastapi import Header as _Header


@router.get("/ui/preview")
def ui_preview(
    x_preview_key: str | None = _Header(default=None),
    db: Session = Depends(get_db),
):
    """Read-only data snapshot for Figma Make. Protected by X-Preview-Key header."""
    if not x_preview_key or x_preview_key != settings.FIGMA_PREVIEW_KEY:
        raise HTTPException(status_code=401, detail="invalid or missing X-Preview-Key")

    now = utcnow()

    # KPIs
    total_devices = db.execute(select(func.count(Device.id))).scalar_one()
    online_threshold = now - timedelta(seconds=settings.ONLINE_THRESHOLD_SECONDS)
    online_devices = db.execute(
        select(func.count(Device.id)).where(Device.last_seen_at >= online_threshold)
    ).scalar_one()
    since_24h = now - timedelta(hours=24)
    telemetry_24h = db.execute(
        select(func.count(TelemetrySnapshot.id)).where(TelemetrySnapshot.received_at >= since_24h)
    ).scalar_one()
    total_licenses = db.execute(select(func.count(License.id))).scalar_one()
    active_licenses = db.execute(
        select(func.count(License.id)).where(License.state == "activated")
    ).scalar_one()

    # Sample devices (up to 5)
    device_rows = db.execute(
        select(Device).order_by(Device.last_seen_at.desc().nullslast()).limit(5)
    ).scalars().all()
    devices = [
        {
            "id": str(d.id),
            "hostname": d.host_name or d.device_install_id,
            "os": d.os_name or "unknown",
            "agent": d.agent_version or "unknown",
            "lastSeen": d.last_seen_at.isoformat() if d.last_seen_at else None,
            "online": is_online(d.last_seen_at),
            "ip": d.primary_ip,
        }
        for d in device_rows
    ]

    # Sample licenses (up to 5)
    from ..routers.license import to_info as _lic_to_info
    lic_rows = db.execute(
        select(License).order_by(License.issued_at.desc().nullslast()).limit(5)
    ).scalars().all()
    licenses = [
        {
            "id": l.license_key,
            "tier": l.tier,
            "state": l.state,
            "issuedAt": l.issued_at.isoformat() if l.issued_at else None,
            "expiresAt": l.expires_at.isoformat() if l.expires_at else None,
            "activatedAt": l.activated_at.isoformat() if l.activated_at else None,
            "activatedDeviceId": l.activated_device_install_id,
        }
        for l in lic_rows
    ]

    # Sample telemetry (up to 5)
    tele_rows = db.execute(
        select(TelemetrySnapshot, Device.host_name)
        .join(Device, TelemetrySnapshot.device_id == Device.id)
        .order_by(TelemetrySnapshot.received_at.desc())
        .limit(5)
    ).all()
    telemetry = [
        {
            "id": str(snap.id),
            "category": snap.category,
            "device": hostname or "unknown",
            "receivedAt": snap.received_at.isoformat(),
            "summary": snap.summary,
            "severity": _severity_for_telemetry(snap.category, snap.summary, snap.payload),
        }
        for snap, hostname in tele_rows
    ]

    # Plans
    plans = db.execute(select(Plan).order_by(Plan.sort_order)).scalars().all()
    plans_data = [
        {
            "id": p.id,
            "label": p.label,
            "price_eur": p.price_eur,
            "duration_days": p.duration_days,
            "max_devices": p.max_devices,
        }
        for p in plans
    ]

    return {
        "kpis": {
            "totalDevices": total_devices,
            "onlineDevices": online_devices,
            "telemetry24h": telemetry_24h,
            "totalLicenses": total_licenses,
            "activeLicenses": active_licenses,
        },
        "devices": devices,
        "licenses": licenses,
        "telemetry": telemetry,
        "plans": plans_data,
    }


# ---------------------------------------------------------------------------
# Device detail (JWT auth) + block/unblock
# ---------------------------------------------------------------------------


@router.get("/ui/devices/{device_id}/detail")
def ui_device_detail(
    device_id: str,
    db: Session = Depends(get_db),
    _user: dict = Depends(require_console_user),
):
    """Full device detail including versions and online status (JWT auth)."""
    device = db.execute(
        select(Device).where(
            or_(cast(Device.id, String) == device_id.strip(), Device.device_install_id == device_id.strip())
        )
    ).scalar_one_or_none()
    if not device:
        raise HTTPException(status_code=404, detail="device not found")

    return {
        "id": str(device.id),
        "deviceInstallId": device.device_install_id,
        "hostname": device.host_name or "unknown",
        "os": f"{device.os_name or ''} {device.os_version or ''}".strip() or None,
        "osName": device.os_name,
        "osVersion": device.os_version,
        "osBuild": device.os_build,
        "agentVersion": device.agent_version,
        "agentChannel": device.agent_channel,
        "desktopVersion": device.desktop_version,
        "updaterVersion": device.updater_version,
        "updateChannel": device.update_channel,
        "primaryIp": device.primary_ip,
        "lastSeen": device.last_seen_at.isoformat() if device.last_seen_at else None,
        "online": is_online(device.last_seen_at),
        "blocked": device.blocked,
        "createdAt": device.created_at.isoformat() if device.created_at else None,
    }


@router.post("/ui/devices/{device_id}/block")
def ui_block_device(
    device_id: str,
    db: Session = Depends(get_db),
    _owner: dict = Depends(require_console_owner),
):
    device = db.execute(
        select(Device).where(
            or_(cast(Device.id, String) == device_id.strip(), Device.device_install_id == device_id.strip())
        )
    ).scalar_one_or_none()
    if not device:
        raise HTTPException(status_code=404, detail="device not found")
    if device.blocked:
        raise HTTPException(status_code=409, detail="device already blocked")

    device.blocked = True
    db.commit()
    return {"ok": True, "deviceId": str(device.id), "blocked": True}


@router.post("/ui/devices/{device_id}/unblock")
def ui_unblock_device(
    device_id: str,
    db: Session = Depends(get_db),
    _owner: dict = Depends(require_console_owner),
):
    device = db.execute(
        select(Device).where(
            or_(cast(Device.id, String) == device_id.strip(), Device.device_install_id == device_id.strip())
        )
    ).scalar_one_or_none()
    if not device:
        raise HTTPException(status_code=404, detail="device not found")
    if not device.blocked:
        raise HTTPException(status_code=409, detail="device not blocked")

    device.blocked = False
    db.commit()
    return {"ok": True, "deviceId": str(device.id), "blocked": False}


# ---------------------------------------------------------------------------
# Device update-channel override
# ---------------------------------------------------------------------------

VALID_UPDATE_CHANNELS = {"stable", "beta", "internal"}


class UpdateChannelRequest(BaseModel):
    update_channel: str = Field(min_length=1, max_length=32)


@router.patch("/ui/devices/{device_id}/update-channel")
def ui_set_device_update_channel(
    device_id: str,
    payload: UpdateChannelRequest,
    db: Session = Depends(get_db),
    _owner: dict = Depends(require_console_owner),
):
    """Override the update channel for a device (stable|beta|internal)."""
    channel = payload.update_channel.strip().lower()
    if channel not in VALID_UPDATE_CHANNELS:
        raise HTTPException(
            status_code=400,
            detail=f"invalid update_channel; allowed: {', '.join(sorted(VALID_UPDATE_CHANNELS))}",
        )

    device = db.execute(
        select(Device).where(
            or_(cast(Device.id, String) == device_id.strip(), Device.device_install_id == device_id.strip())
        )
    ).scalar_one_or_none()
    if not device:
        raise HTTPException(status_code=404, detail="device not found")

    device.update_channel = channel
    db.commit()
    return {"ok": True, "deviceId": str(device.id), "update_channel": device.update_channel}


# ---------------------------------------------------------------------------
# License: generate (alias) + revoke (POST) + block/unblock + PATCH
# ---------------------------------------------------------------------------


class LicensePatchRequest(BaseModel):
    expires_at: str | None = None   # ISO-8601 datetime string or null
    notes: str | None = None        # free text (stored in license_key comment – informational only)


@router.post("/ui/licenses/generate")
def ui_generate_licenses(
    payload: LicenseCreateRequest,
    db: Session = Depends(get_db),
    _owner: dict = Depends(require_console_owner),
):
    """Generate license keys – canonical POST endpoint (replaces legacy POST /ui/licenses)."""
    plan = db.get(Plan, payload.tier)
    duration_days = plan.duration_days if plan else TIER_DURATION.get(payload.tier)

    created: list[LicenseInfo] = []
    for _ in range(payload.quantity):
        for _attempt in range(10):
            key = _generate_license_key()
            exists = db.execute(select(License).where(License.license_key == key)).scalar_one_or_none()
            if not exists:
                break
        else:
            raise HTTPException(status_code=500, detail="could not generate unique license key")

        lic = License(
            license_key=key,
            tier=payload.tier,
            duration_days=duration_days,
            state="issued",
        )
        db.add(lic)
        db.flush()
        created.append(
            LicenseInfo(
                license_key=lic.license_key,
                tier=lic.tier,
                state=lic.state,
                duration_days=lic.duration_days,
                issued_at=lic.issued_at,
                activated_at=None,
                expires_at=None,
                activated_device_install_id=None,
                activated_by_user_id=None,
            )
        )

    db.commit()
    return LicenseCreateResponse(ok=True, licenses=created)


@router.post("/ui/licenses/{license_key}/revoke")
def ui_revoke_license_post(
    license_key: str,
    db: Session = Depends(get_db),
    _owner: dict = Depends(require_console_owner),
):
    """Revoke a license (POST action variant; canonical endpoint)."""
    key = license_key.strip().upper().replace(" ", "")
    lic = db.execute(select(License).where(License.license_key == key)).scalar_one_or_none()
    if not lic:
        raise HTTPException(status_code=404, detail="license not found")
    if lic.state == "revoked":
        raise HTTPException(status_code=409, detail="license already revoked")

    lic.state = "revoked"
    db.commit()
    return {"ok": True, "license_key": lic.license_key, "state": "revoked"}


@router.post("/ui/licenses/{license_key}/block")
def ui_block_license(
    license_key: str,
    db: Session = Depends(get_db),
    _owner: dict = Depends(require_console_owner),
):
    key = license_key.strip().upper().replace(" ", "")
    lic = db.execute(select(License).where(License.license_key == key)).scalar_one_or_none()
    if not lic:
        raise HTTPException(status_code=404, detail="license not found")
    if lic.state == "blocked":
        raise HTTPException(status_code=409, detail="license already blocked")
    if lic.state == "revoked":
        raise HTTPException(status_code=409, detail="cannot block a revoked license")

    lic.state = "blocked"
    db.commit()
    return {"ok": True, "license_key": lic.license_key, "state": "blocked"}


@router.post("/ui/licenses/{license_key}/unblock")
def ui_unblock_license(
    license_key: str,
    db: Session = Depends(get_db),
    _owner: dict = Depends(require_console_owner),
):
    """Unblock a license using state-machine rule:
    - 'activated' if activated_at is set and (expires_at is null or expires_at > now)
    - 'issued' otherwise
    """
    key = license_key.strip().upper().replace(" ", "")
    lic = db.execute(select(License).where(License.license_key == key)).scalar_one_or_none()
    if not lic:
        raise HTTPException(status_code=404, detail="license not found")
    if lic.state != "blocked":
        raise HTTPException(status_code=409, detail="license is not blocked")

    now = utcnow()
    if lic.activated_at and (lic.expires_at is None or lic.expires_at > now):
        lic.state = "activated"
    else:
        lic.state = "issued"

    db.commit()
    return {"ok": True, "license_key": lic.license_key, "state": lic.state}


# ---------------------------------------------------------------------------
# Home Portal – Device endpoints (scoped to the authenticated home user)
# ---------------------------------------------------------------------------

class HomeDeviceItem(BaseModel):
    device_install_id: str
    host_name: str | None = None
    os_name: str | None = None
    os_version: str | None = None
    last_seen_at: datetime | None = None
    online: bool = False
    primary_ip: str | None = None


class HomeDeviceListResponse(BaseModel):
    items: list[HomeDeviceItem]
    total: int


class HomeDeviceRenameRequest(BaseModel):
    name: str = Field(min_length=1, max_length=255)


@router.get("/home/devices", response_model=HomeDeviceListResponse)
def home_list_devices(
    db: Session = Depends(get_db),
    _user: dict = Depends(require_home_user),
):
    """List devices activated for the current home portal user (via their licenses)."""
    user_id = _user.get("sub")
    if not user_id:
        raise HTTPException(status_code=401, detail="invalid token: missing sub")

    install_ids = db.execute(
        select(License.activated_device_install_id)
        .where(
            License.activated_by_user_id == user_id,
            License.activated_device_install_id.is_not(None),
        )
    ).scalars().all()

    if not install_ids:
        return HomeDeviceListResponse(items=[], total=0)

    devices = db.execute(
        select(Device).where(Device.device_install_id.in_(install_ids))
    ).scalars().all()

    items = [
        HomeDeviceItem(
            device_install_id=d.device_install_id,
            host_name=d.host_name,
            os_name=d.os_name,
            os_version=d.os_version,
            last_seen_at=d.last_seen_at,
            online=is_online(d.last_seen_at),
            primary_ip=d.primary_ip,
        )
        for d in devices
    ]
    return HomeDeviceListResponse(items=items, total=len(items))


@router.patch("/home/devices/{device_install_id}/name")
def home_rename_device(
    device_install_id: str,
    payload: HomeDeviceRenameRequest,
    db: Session = Depends(get_db),
    _user: dict = Depends(require_home_user),
):
    """Rename (set host_name) on a device owned by the current home user."""
    user_id = _user.get("sub")
    if not user_id:
        raise HTTPException(status_code=401, detail="invalid token: missing sub")

    owned = db.execute(
        select(License.activated_device_install_id).where(
            License.activated_by_user_id == user_id,
            License.activated_device_install_id == device_install_id,
        )
    ).scalar_one_or_none()
    if not owned:
        raise HTTPException(status_code=404, detail="device not found or not owned by user")

    device = db.execute(
        select(Device).where(Device.device_install_id == device_install_id)
    ).scalar_one_or_none()
    if not device:
        raise HTTPException(status_code=404, detail="device record not found")

    device.host_name = payload.name.strip()
    db.commit()
    return {"ok": True, "device_install_id": device_install_id, "host_name": device.host_name}


@router.delete("/home/devices/{device_install_id}")
def home_revoke_device(
    device_install_id: str,
    db: Session = Depends(get_db),
    _user: dict = Depends(require_home_user),
):
    """Revoke device: clears activated_device_install_id from the user's license."""
    user_id = _user.get("sub")
    if not user_id:
        raise HTTPException(status_code=401, detail="invalid token: missing sub")

    license_row = db.execute(
        select(License).where(
            License.activated_by_user_id == user_id,
            License.activated_device_install_id == device_install_id,
        )
    ).scalar_one_or_none()
    if not license_row:
        raise HTTPException(status_code=404, detail="device not found or not owned by user")

    license_row.activated_device_install_id = None
    db.commit()
    return {"ok": True}


@router.patch("/ui/licenses/{license_key}")
def ui_patch_license(
    license_key: str,
    payload: LicensePatchRequest,
    db: Session = Depends(get_db),
    _owner: dict = Depends(require_console_owner),
):
    """Patch expiry date (and optional notes) on a license."""
    key = license_key.strip().upper().replace(" ", "")
    lic = db.execute(select(License).where(License.license_key == key)).scalar_one_or_none()
    if not lic:
        raise HTTPException(status_code=404, detail="license not found")

    if payload.expires_at is not None:
        if payload.expires_at == "":
            lic.expires_at = None
        else:
            try:
                from datetime import datetime as _dt
                lic.expires_at = _dt.fromisoformat(payload.expires_at.replace("Z", "+00:00"))
            except ValueError as exc:
                raise HTTPException(status_code=422, detail=f"invalid expires_at: {exc}") from exc

    db.commit()
    db.refresh(lic)
    return {
        "ok": True,
        "license_key": lic.license_key,
        "state": lic.state,
        "expires_at": lic.expires_at.isoformat() if lic.expires_at else None,
    }
