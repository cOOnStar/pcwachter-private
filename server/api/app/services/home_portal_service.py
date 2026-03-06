from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
import threading
import uuid

import httpx
from fastapi import HTTPException
from sqlalchemy import and_, func, or_, select
from sqlalchemy.orm import Session

from ..models import (
    Device,
    DeviceHistoryEntry,
    HomeUserProfile,
    License,
    LicenseAuditLog,
    LicenseDeviceAssignment,
    Plan,
)

_LATEST_RELEASE_CACHE: dict[str, object] = {"expires_at": datetime.min.replace(tzinfo=timezone.utc), "payload": None}
_LATEST_RELEASE_LOCK = threading.Lock()
_GITHUB_RELEASE_URL = "https://api.github.com/repos/cOOnStar/pcwaechter-public-release/releases/latest"


@dataclass(slots=True)
class PortalLicenseSnapshot:
    license_row: License
    plan: Plan | None
    active_devices: list[str]


def utcnow() -> datetime:
    return datetime.now(timezone.utc)


def ensure_home_profile(db: Session, keycloak_user_id: str) -> HomeUserProfile:
    row = db.get(HomeUserProfile, keycloak_user_id)
    if row is not None:
        return row

    row = HomeUserProfile(keycloak_user_id=keycloak_user_id)
    db.add(row)
    db.commit()
    db.refresh(row)
    return row


def tier_label(tier: str | None) -> str:
    normalized = (tier or "").strip().lower()
    if normalized == "professional":
        return "Professional"
    if normalized == "standard":
        return "Standard"
    if normalized == "enterprise":
        return "Enterprise"
    if normalized == "trial":
        return "Standard"
    if normalized == "unlimited":
        return "Professional"
    return "Standard"


def license_display_name(license_row: License, plan: Plan | None = None) -> str:
    if license_row.display_name:
        return license_row.display_name
    if plan and plan.label:
        return plan.label
    return f"PC-Waechter {tier_label(license_row.tier)}"


def format_date_de(value: datetime | None) -> str:
    if value is None:
        return "Unbegrenzt"
    localized = value.astimezone(timezone.utc)
    return localized.strftime("%d.%m.%Y")


def derive_license_status(license_row: License) -> str:
    now = utcnow()
    if license_row.state in {"expired", "revoked", "blocked"}:
        return "Abgelaufen"
    if license_row.expires_at is None:
        return "Aktiv"
    if license_row.expires_at <= now:
        return "Abgelaufen"
    if license_row.expires_at <= now + timedelta(days=45):
        return "Läuft bald ab"
    return "Aktiv"


def summarize_license_capacity(license_row: License, plan: Plan | None, device_count: int) -> tuple[int, int]:
    max_devices = int(license_row.max_devices or 0)
    if max_devices <= 0 and plan and plan.max_devices:
        max_devices = int(plan.max_devices)
    if max_devices <= 0:
        max_devices = max(1, device_count or 1)
    return device_count, max_devices


def record_license_audit(
    db: Session,
    *,
    license_id: uuid.UUID,
    action: str,
    description: str,
    actor_name: str,
    details: dict | None = None,
    created_at: datetime | None = None,
) -> LicenseAuditLog:
    row = LicenseAuditLog(
        license_id=license_id,
        action=action,
        description=description,
        actor_name=actor_name,
        details=details,
    )
    if created_at is not None:
        row.created_at = created_at
    db.add(row)
    db.flush()
    return row


def record_device_history(
    db: Session,
    *,
    device_install_id: str,
    event_type: str,
    message: str,
    keycloak_user_id: str | None = None,
    meta: dict | None = None,
    created_at: datetime | None = None,
) -> DeviceHistoryEntry:
    row = DeviceHistoryEntry(
        device_install_id=device_install_id,
        keycloak_user_id=keycloak_user_id,
        event_type=event_type,
        message=message,
        meta=meta,
    )
    if created_at is not None:
        row.created_at = created_at
    db.add(row)
    db.flush()
    return row


def active_license_owner_id(license_row: License) -> str | None:
    return (license_row.owner_user_id or license_row.activated_by_user_id or "").strip() or None


def get_plan_map(db: Session) -> dict[str, Plan]:
    rows = db.execute(select(Plan)).scalars().all()
    return {row.id: row for row in rows}


def active_assignment_rows_for_user(db: Session, keycloak_user_id: str) -> list[LicenseDeviceAssignment]:
    return db.execute(
        select(LicenseDeviceAssignment).where(
            LicenseDeviceAssignment.keycloak_user_id == keycloak_user_id,
            LicenseDeviceAssignment.released_at.is_(None),
        )
    ).scalars().all()


def active_assignments_for_license(db: Session, license_id: uuid.UUID) -> list[LicenseDeviceAssignment]:
    return db.execute(
        select(LicenseDeviceAssignment).where(
            LicenseDeviceAssignment.license_id == license_id,
            LicenseDeviceAssignment.released_at.is_(None),
        )
    ).scalars().all()


def active_assignment_for_device(db: Session, device_install_id: str) -> LicenseDeviceAssignment | None:
    return db.execute(
        select(LicenseDeviceAssignment).where(
            LicenseDeviceAssignment.device_install_id == device_install_id,
            LicenseDeviceAssignment.released_at.is_(None),
        )
    ).scalar_one_or_none()


def lookup_license_by_device(db: Session, device_install_id: str) -> License | None:
    assignment = active_assignment_for_device(db, device_install_id)
    if assignment is not None:
        return db.get(License, assignment.license_id)
    return db.execute(
        select(License)
        .where(License.activated_device_install_id == device_install_id)
        .order_by(License.activated_at.desc().nullslast(), License.created_at.desc())
        .limit(1)
    ).scalar_one_or_none()


def ensure_legacy_assignment(db: Session, license_row: License) -> None:
    if not license_row.activated_device_install_id:
        return

    existing = db.execute(
        select(LicenseDeviceAssignment).where(
            LicenseDeviceAssignment.license_id == license_row.id,
            LicenseDeviceAssignment.device_install_id == license_row.activated_device_install_id,
            LicenseDeviceAssignment.released_at.is_(None),
        )
    ).scalar_one_or_none()
    if existing is not None:
        return

    db.add(
        LicenseDeviceAssignment(
            license_id=license_row.id,
            device_install_id=license_row.activated_device_install_id,
            keycloak_user_id=active_license_owner_id(license_row) or "",
            assigned_by_user_name="System",
            assigned_at=license_row.activated_at or license_row.created_at or utcnow(),
        )
    )
    db.flush()


def release_assignment(
    db: Session,
    assignment: LicenseDeviceAssignment,
    *,
    actor_name: str,
    create_audit: bool = True,
) -> None:
    if assignment.released_at is not None:
        return

    assignment.released_at = utcnow()
    license_row = db.get(License, assignment.license_id)
    if license_row is not None and license_row.activated_device_install_id == assignment.device_install_id:
        replacement = db.execute(
            select(LicenseDeviceAssignment)
            .where(
                LicenseDeviceAssignment.license_id == license_row.id,
                LicenseDeviceAssignment.released_at.is_(None),
                LicenseDeviceAssignment.id != assignment.id,
            )
            .order_by(LicenseDeviceAssignment.assigned_at.desc())
            .limit(1)
        ).scalar_one_or_none()
        license_row.activated_device_install_id = replacement.device_install_id if replacement else None

    if create_audit and license_row is not None:
        record_license_audit(
            db,
            license_id=license_row.id,
            action="device_removed",
            description=f'Gerät "{assignment.device_install_id}" entkoppelt',
            actor_name=actor_name,
            details={"deviceInstallId": assignment.device_install_id},
        )
    record_device_history(
        db,
        device_install_id=assignment.device_install_id,
        event_type="action",
        message="Lizenzzuweisung aufgehoben",
        keycloak_user_id=assignment.keycloak_user_id,
        meta={"licenseId": str(assignment.license_id)},
    )
    db.flush()


def assign_license_to_device(
    db: Session,
    *,
    license_row: License,
    device_install_id: str,
    keycloak_user_id: str,
    actor_name: str,
) -> LicenseDeviceAssignment:
    ensure_legacy_assignment(db, license_row)
    plan = db.execute(select(Plan).where(Plan.id == license_row.tier)).scalar_one_or_none()

    current_assignment = db.execute(
        select(LicenseDeviceAssignment).where(
            LicenseDeviceAssignment.license_id == license_row.id,
            LicenseDeviceAssignment.device_install_id == device_install_id,
            LicenseDeviceAssignment.released_at.is_(None),
        )
    ).scalar_one_or_none()
    if current_assignment is not None:
        return current_assignment

    existing_device_assignment = active_assignment_for_device(db, device_install_id)
    if existing_device_assignment is not None and existing_device_assignment.license_id != license_row.id:
        release_assignment(db, existing_device_assignment, actor_name=actor_name)

    active_count = db.execute(
        select(func.count(LicenseDeviceAssignment.id)).where(
            LicenseDeviceAssignment.license_id == license_row.id,
            LicenseDeviceAssignment.released_at.is_(None),
        )
    ).scalar_one()
    _, max_devices = summarize_license_capacity(license_row, plan, int(active_count))
    if int(active_count) >= max_devices:
        raise HTTPException(status_code=409, detail="license_device_slots_exhausted")

    assignment = LicenseDeviceAssignment(
        license_id=license_row.id,
        device_install_id=device_install_id,
        keycloak_user_id=keycloak_user_id,
        assigned_by_user_name=actor_name,
    )
    db.add(assignment)

    license_row.owner_user_id = keycloak_user_id
    if not license_row.activated_by_user_id:
        license_row.activated_by_user_id = keycloak_user_id
    if not license_row.activated_device_install_id:
        license_row.activated_device_install_id = device_install_id

    record_license_audit(
        db,
        license_id=license_row.id,
        action="device_added",
        description=f'Gerät "{device_install_id}" zugewiesen',
        actor_name=actor_name,
        details={"deviceInstallId": device_install_id},
    )
    record_device_history(
        db,
        device_install_id=device_install_id,
        event_type="action",
        message=f"Mit Lizenz {license_display_name(license_row, plan)} verknuepft",
        keycloak_user_id=keycloak_user_id,
        meta={"licenseId": str(license_row.id)},
    )
    db.flush()
    return assignment


def owned_license_rows(db: Session, keycloak_user_id: str) -> list[License]:
    rows = db.execute(
        select(License).where(
            or_(
                License.owner_user_id == keycloak_user_id,
                and_(License.owner_user_id.is_(None), License.activated_by_user_id == keycloak_user_id),
            )
        )
        .order_by(License.expires_at.asc().nulls_last(), License.created_at.desc())
    ).scalars().all()
    for row in rows:
        ensure_legacy_assignment(db, row)
    return rows


def release_payload_to_portal(payload: dict) -> dict:
    assets = []
    for asset in payload.get("assets") or []:
        if not isinstance(asset, dict):
            continue
        assets.append(
            {
                "id": asset.get("id"),
                "name": asset.get("name"),
                "size": int(asset.get("size") or 0),
                "download_count": int(asset.get("download_count") or 0),
                "browser_download_url": asset.get("browser_download_url"),
            }
        )

    return {
        "tag_name": payload.get("tag_name"),
        "name": payload.get("name"),
        "body": payload.get("body") or "",
        "published_at": payload.get("published_at"),
        "html_url": payload.get("html_url"),
        "assets": assets,
    }


async def fetch_latest_release() -> dict | None:
    now = utcnow()
    with _LATEST_RELEASE_LOCK:
        expires_at = _LATEST_RELEASE_CACHE["expires_at"]
        payload = _LATEST_RELEASE_CACHE["payload"]
        if isinstance(expires_at, datetime) and expires_at > now and isinstance(payload, dict):
            return payload

    try:
        async with httpx.AsyncClient(timeout=15) as client:
            response = await client.get(
                _GITHUB_RELEASE_URL,
                headers={"Accept": "application/vnd.github+json"},
            )
    except httpx.RequestError as exc:
        raise HTTPException(status_code=502, detail="release_lookup_failed") from exc

    if response.status_code == 404:
        return None
    if response.status_code >= 400:
        raise HTTPException(status_code=502, detail="release_lookup_failed")

    raw = response.json()
    payload = release_payload_to_portal(raw if isinstance(raw, dict) else {})
    with _LATEST_RELEASE_LOCK:
        _LATEST_RELEASE_CACHE["payload"] = payload
        _LATEST_RELEASE_CACHE["expires_at"] = now + timedelta(minutes=5)
    return payload


def detect_device_type(device: Device) -> str:
    haystack = " ".join(
        part for part in [device.host_name or "", device.os_name or "", device.os_version or ""] if part
    ).lower()
    if "server" in haystack:
        return "Server"
    if "laptop" in haystack or "notebook" in haystack:
        return "Laptop"
    return "Desktop"
