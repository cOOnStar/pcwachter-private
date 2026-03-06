from __future__ import annotations

import base64
import hashlib
import hmac
import logging
import re
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import httpx
from fastapi import HTTPException, UploadFile
from sqlalchemy import select
from sqlalchemy.orm import Session
from starlette.concurrency import run_in_threadpool

from ..keycloak_admin import fetch_keycloak_user, keycloak_admin_configured
from ..models import Notification, SupportAttachment, SupportIdentityLink, SupportPortalSettings, SupportTicketSyncState
from ..settings import settings

logger = logging.getLogger(__name__)

_DEFAULT_SUPPORT_SETTINGS_ID = 1
_DEFAULT_MAINTENANCE_MESSAGE = (
    "Der Supportbereich ist gerade in Wartung. Bitte versuchen Sie es spaeter erneut."
)
_FILENAME_SANITIZE_RE = re.compile(r"[^A-Za-z0-9._-]+")
_DIR_SANITIZE_RE = re.compile(r"[^A-Za-z0-9._-]+")
_SUPPORT_STATE_LABELS: dict[str, str] = {
    "new": "Neu",
    "open": "Offen",
    "pending reminder": "Wartend",
    "pending close": "Wartet auf Abschluss",
    "closed": "Geschlossen",
    "merged": "Zusammengefuehrt",
}


@dataclass(slots=True)
class SupportProfile:
    user_id: str
    email: str
    first_name: str
    last_name: str


@dataclass(slots=True)
class EffectiveSupportSettings:
    allow_customer_group_selection: bool
    customer_visible_group_ids: list[int]
    default_group_id: int | None
    default_priority_id: int | None
    uploads_enabled: bool
    uploads_max_bytes: int
    uploads_max_bytes_ceiling: int
    maintenance_mode: bool
    maintenance_message: str
    storage_root: str


def utcnow() -> datetime:
    return datetime.now(timezone.utc)


def normalize_email(value: object) -> str | None:
    if value is None:
        return None
    text = str(value).strip().lower()
    return text or None


def normalize_name(value: object) -> str | None:
    if value is None:
        return None
    text = str(value).strip()
    return text or None


def parse_zammad_datetime(value: object) -> datetime | None:
    text = normalize_name(value)
    if not text:
        return None

    normalized = text.replace(" UTC", "+00:00").replace("Z", "+00:00")
    try:
        parsed = datetime.fromisoformat(normalized)
    except ValueError:
        for pattern in (
            "%Y-%m-%d %H:%M:%S %z",
            "%Y-%m-%dT%H:%M:%S.%f%z",
            "%Y-%m-%dT%H:%M:%S%z",
        ):
            try:
                parsed = datetime.strptime(normalized, pattern)
                break
            except ValueError:
                continue
        else:
            return None

    if parsed.tzinfo is None:
        return parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc)


def coerce_int(value: object) -> int | None:
    if value is None:
        return None
    try:
        return int(str(value).strip())
    except (TypeError, ValueError):
        return None


def coerce_bool(value: object) -> bool:
    if isinstance(value, bool):
        return value
    if isinstance(value, (int, float)):
        return bool(value)
    if value is None:
        return False
    return str(value).strip().lower() in {"1", "true", "yes", "on"}


def fallback_names_from_email(email: str) -> tuple[str, str]:
    local_part = email.split("@", 1)[0].strip()
    tokens = [token for token in re.split(r"[._-]+", local_part) if token]
    if not tokens:
        return "PCWaechter", "Kunde"
    first_name = tokens[0].capitalize()
    if len(tokens) == 1:
        return first_name, "Kunde"
    last_name = " ".join(token.capitalize() for token in tokens[1:])
    return first_name, last_name


def zammad_headers() -> dict[str, str]:
    if not settings.ZAMMAD_API_TOKEN:
        raise HTTPException(status_code=503, detail="support_not_configured")
    return {
        "Authorization": f"Token token={settings.ZAMMAD_API_TOKEN}",
        "Content-Type": "application/json",
    }


def require_zammad_configured() -> None:
    if not settings.ZAMMAD_BASE_URL.strip() or not settings.ZAMMAD_API_TOKEN.strip():
        raise HTTPException(status_code=503, detail="support_not_configured")


def zammad_base_url() -> str:
    require_zammad_configured()
    return settings.ZAMMAD_BASE_URL.rstrip("/")


def raise_zammad_unreachable(operation: str, exc: httpx.RequestError) -> None:
    logger.warning("Zammad request error during %s: %s", operation, exc)
    raise HTTPException(status_code=502, detail=f"zammad_unreachable during {operation}") from exc


def zammad_error_message(response: httpx.Response) -> str:
    try:
        payload = response.json()
    except ValueError:
        payload = None

    if isinstance(payload, dict):
        for key in ("error_human", "error", "detail", "message"):
            value = payload.get(key)
            if isinstance(value, str) and value.strip():
                return value.strip()

    text = response.text.strip()
    return text or f"zammad_error: {response.status_code}"


def support_storage_root() -> Path:
    return Path(settings.UPLOAD_DIR).expanduser() / "support"


def _safe_dir_segment(value: str) -> str:
    cleaned = _DIR_SANITIZE_RE.sub("_", value.strip())
    return cleaned or "anonymous"


def safe_filename(filename: str) -> str:
    base = Path(filename or "attachment.bin").name.strip() or "attachment.bin"
    stem = _FILENAME_SANITIZE_RE.sub("_", Path(base).stem).strip("._") or "attachment"
    suffix = _FILENAME_SANITIZE_RE.sub("", Path(base).suffix)
    if suffix and not suffix.startswith("."):
        suffix = f".{suffix}"
    return f"{stem}{suffix}" if suffix else stem


def get_or_create_support_settings(db: Session) -> SupportPortalSettings:
    row = db.get(SupportPortalSettings, _DEFAULT_SUPPORT_SETTINGS_ID)
    if row is not None:
        return row

    default_group_id = settings.ZAMMAD_DEFAULT_GROUP_ID if settings.ZAMMAD_DEFAULT_GROUP_ID > 0 else None
    customer_visible_group_ids = [default_group_id] if default_group_id is not None else []
    default_priority_id = 2
    row = SupportPortalSettings(
        id=_DEFAULT_SUPPORT_SETTINGS_ID,
        allow_customer_group_selection=False,
        customer_visible_group_ids=customer_visible_group_ids,
        default_group_id=default_group_id,
        default_priority_id=default_priority_id,
        uploads_enabled=True,
        uploads_max_bytes=max(1, int(settings.SUPPORT_ATTACHMENT_MAX_BYTES)),
        maintenance_mode=False,
        maintenance_message=_DEFAULT_MAINTENANCE_MESSAGE,
    )
    db.add(row)
    db.commit()
    db.refresh(row)
    return row


def get_effective_support_settings(db: Session) -> EffectiveSupportSettings:
    row = get_or_create_support_settings(db)
    default_group_id = (
        int(row.default_group_id)
        if row.default_group_id is not None
        else (settings.ZAMMAD_DEFAULT_GROUP_ID if settings.ZAMMAD_DEFAULT_GROUP_ID > 0 else None)
    )
    raw_visible = row.customer_visible_group_ids if isinstance(row.customer_visible_group_ids, list) else []
    customer_visible_group_ids = [
        int(value)
        for value in raw_visible
        if isinstance(value, int) or (isinstance(value, str) and value.strip().isdigit())
    ]
    if default_group_id is not None and default_group_id not in customer_visible_group_ids:
        customer_visible_group_ids = [*customer_visible_group_ids, default_group_id]
    customer_visible_group_ids = list(dict.fromkeys(customer_visible_group_ids))

    uploads_max_bytes_ceiling = max(1, int(settings.SUPPORT_ATTACHMENT_MAX_BYTES))
    configured_uploads_max_bytes = int(row.uploads_max_bytes or uploads_max_bytes_ceiling)
    uploads_max_bytes = min(max(1, configured_uploads_max_bytes), uploads_max_bytes_ceiling)

    maintenance_message = normalize_name(row.maintenance_message) or _DEFAULT_MAINTENANCE_MESSAGE

    return EffectiveSupportSettings(
        allow_customer_group_selection=bool(row.allow_customer_group_selection),
        customer_visible_group_ids=customer_visible_group_ids,
        default_group_id=default_group_id,
        default_priority_id=int(row.default_priority_id) if row.default_priority_id is not None else 2,
        uploads_enabled=bool(row.uploads_enabled),
        uploads_max_bytes=uploads_max_bytes,
        uploads_max_bytes_ceiling=uploads_max_bytes_ceiling,
        maintenance_mode=bool(row.maintenance_mode),
        maintenance_message=maintenance_message,
        storage_root=str(support_storage_root()),
    )


def support_settings_payload(config: EffectiveSupportSettings) -> dict[str, Any]:
    return {
        "allow_customer_group_selection": config.allow_customer_group_selection,
        "customer_visible_group_ids": config.customer_visible_group_ids,
        "default_group_id": config.default_group_id,
        "default_priority_id": config.default_priority_id,
        "uploads_enabled": config.uploads_enabled,
        "uploads_max_bytes": config.uploads_max_bytes,
        "uploads_max_bytes_ceiling": config.uploads_max_bytes_ceiling,
        "maintenance_mode": config.maintenance_mode,
        "maintenance_message": config.maintenance_message,
        "storage_root": config.storage_root,
    }


async def build_support_profile(user: dict) -> SupportProfile:
    user_id = str(user.get("sub") or "").strip()
    if not user_id:
        raise HTTPException(status_code=401, detail="user_sub_missing")

    email = normalize_email(user.get("email"))
    first_name = normalize_name(user.get("given_name") or user.get("firstName"))
    last_name = normalize_name(user.get("family_name") or user.get("lastName"))

    if keycloak_admin_configured():
        try:
            record = await run_in_threadpool(fetch_keycloak_user, user_id)
            email = normalize_email(record.get("email")) or email
            first_name = normalize_name(record.get("firstName")) or first_name
            last_name = normalize_name(record.get("lastName")) or last_name
        except HTTPException as exc:
            if exc.status_code in {404, 502, 503}:
                logger.warning("support profile fallback to token for %s: %s", user_id, exc.detail)
            else:
                raise

    if not email:
        raise HTTPException(status_code=400, detail="user_email_missing")

    if not first_name and not last_name:
        first_name, last_name = fallback_names_from_email(email)

    return SupportProfile(
        user_id=user_id,
        email=email,
        first_name=first_name or "",
        last_name=last_name or "",
    )


async def resolve_zammad_customer_role_id(client: httpx.AsyncClient) -> int:
    if settings.ZAMMAD_CUSTOMER_ROLE_ID > 0:
        return settings.ZAMMAD_CUSTOMER_ROLE_ID

    try:
        response = await client.get(f"{zammad_base_url()}/api/v1/roles", headers=zammad_headers())
    except httpx.RequestError as exc:
        logger.warning("zammad role lookup failed, fallback to role 3: %s", exc)
        return 3

    if response.is_success:
        payload = response.json()
        if isinstance(payload, list):
            customer_role = next(
                (
                    row
                    for row in payload
                    if isinstance(row, dict)
                    and str(row.get("name") or "").strip().lower() == "customer"
                    and row.get("id") is not None
                ),
                None,
            )
            if customer_role is not None:
                return int(customer_role["id"])

    logger.warning("zammad customer role lookup unsuccessful, fallback to role 3")
    return 3


async def find_zammad_user_record_by_email(email: str, client: httpx.AsyncClient) -> dict[str, Any] | None:
    try:
        response = await client.get(
            f"{zammad_base_url()}/api/v1/users/search",
            params={"query": email},
            headers=zammad_headers(),
        )
    except httpx.RequestError as exc:
        raise_zammad_unreachable("users/search", exc)

    if response.status_code >= 400:
        raise HTTPException(status_code=502, detail=f"zammad_user_search_failed: {response.status_code}")

    payload = response.json()
    if isinstance(payload, list):
        for row in payload:
            if isinstance(row, dict) and row.get("id") is not None:
                return row
    return None


async def get_zammad_user_record(zammad_user_id: int, client: httpx.AsyncClient) -> dict[str, Any] | None:
    try:
        response = await client.get(
            f"{zammad_base_url()}/api/v1/users/{zammad_user_id}",
            headers=zammad_headers(),
        )
    except httpx.RequestError as exc:
        raise_zammad_unreachable("users/get", exc)

    if response.status_code == 404:
        return None
    if response.status_code >= 400:
        raise HTTPException(status_code=502, detail=f"zammad_user_get_failed: {response.status_code}")

    payload = response.json()
    return payload if isinstance(payload, dict) else None


async def create_zammad_user(profile: SupportProfile, client: httpx.AsyncClient) -> int:
    customer_role_id = await resolve_zammad_customer_role_id(client)
    payload: dict[str, Any] = {
        "email": profile.email,
        "login": profile.email,
        "firstname": profile.first_name,
        "lastname": profile.last_name,
        "role_ids": [customer_role_id],
    }
    if settings.ZAMMAD_DEFAULT_ORG_ID > 0:
        payload["organization_id"] = settings.ZAMMAD_DEFAULT_ORG_ID

    try:
        response = await client.post(
            f"{zammad_base_url()}/api/v1/users",
            headers=zammad_headers(),
            json=payload,
        )
    except httpx.RequestError as exc:
        raise_zammad_unreachable("users/create", exc)

    if response.status_code in {409, 422}:
        existing = await find_zammad_user_record_by_email(profile.email, client)
        if existing is not None:
            return int(existing["id"])

    if response.status_code >= 400:
        logger.warning("zammad user create failed for %s: %s", profile.email, zammad_error_message(response))
        raise HTTPException(status_code=502, detail="support_customer_create_failed")

    payload = response.json()
    user_id = payload.get("id") if isinstance(payload, dict) else None
    if user_id is None:
        raise HTTPException(status_code=502, detail="support_customer_create_failed")
    return int(user_id)


async def update_zammad_user_profile(
    *,
    zammad_user_id: int,
    existing_record: dict[str, Any] | None,
    profile: SupportProfile,
    client: httpx.AsyncClient,
) -> None:
    existing_record = existing_record or {}
    existing_email = normalize_email(existing_record.get("email"))
    existing_first_name = normalize_name(existing_record.get("firstname"))
    existing_last_name = normalize_name(existing_record.get("lastname"))
    existing_login = normalize_email(existing_record.get("login"))

    update_payload: dict[str, Any] = {}
    if existing_email != profile.email:
        update_payload["email"] = profile.email
    if existing_first_name != profile.first_name:
        update_payload["firstname"] = profile.first_name
    if existing_last_name != profile.last_name:
        update_payload["lastname"] = profile.last_name
    if not existing_login or (existing_email and existing_login == existing_email):
        update_payload["login"] = profile.email

    if not update_payload:
        return

    try:
        response = await client.put(
            f"{zammad_base_url()}/api/v1/users/{zammad_user_id}",
            headers=zammad_headers(),
            json=update_payload,
        )
    except httpx.RequestError as exc:
        raise_zammad_unreachable("users/update", exc)

    if response.status_code >= 400:
        logger.warning(
            "zammad user update failed for keycloak=%s zammad=%s: %s",
            profile.user_id,
            zammad_user_id,
            zammad_error_message(response),
        )
        raise HTTPException(status_code=502, detail="support_sync_failed")


def upsert_support_identity_link(db: Session, *, keycloak_user_id: str, zammad_user_id: int, email: str) -> None:
    link = db.execute(
        select(SupportIdentityLink).where(SupportIdentityLink.keycloak_user_id == keycloak_user_id)
    ).scalar_one_or_none()
    if link is None:
        link = SupportIdentityLink(
            keycloak_user_id=keycloak_user_id,
            zammad_user_id=zammad_user_id,
            last_synced_email=email,
        )
        db.add(link)
    else:
        link.zammad_user_id = zammad_user_id
        link.last_synced_email = email
    db.flush()


async def resolve_or_create_zammad_customer_id(
    *,
    db: Session,
    profile: SupportProfile,
    client: httpx.AsyncClient,
    create_if_missing: bool,
) -> int | None:
    link = db.execute(
        select(SupportIdentityLink).where(SupportIdentityLink.keycloak_user_id == profile.user_id)
    ).scalar_one_or_none()

    if link is not None:
        current_record = await get_zammad_user_record(int(link.zammad_user_id), client)
        if current_record is not None:
            await update_zammad_user_profile(
                zammad_user_id=int(link.zammad_user_id),
                existing_record=current_record,
                profile=profile,
                client=client,
            )
            upsert_support_identity_link(
                db,
                keycloak_user_id=profile.user_id,
                zammad_user_id=int(link.zammad_user_id),
                email=profile.email,
            )
            db.commit()
            return int(link.zammad_user_id)

        db.delete(link)
        db.commit()

    existing_record = await find_zammad_user_record_by_email(profile.email, client)
    if existing_record is not None:
        zammad_user_id = int(existing_record["id"])
        await update_zammad_user_profile(
            zammad_user_id=zammad_user_id,
            existing_record=existing_record,
            profile=profile,
            client=client,
        )
        upsert_support_identity_link(
            db,
            keycloak_user_id=profile.user_id,
            zammad_user_id=zammad_user_id,
            email=profile.email,
        )
        db.commit()
        return zammad_user_id

    if not create_if_missing:
        return None

    zammad_user_id = await create_zammad_user(profile, client)
    upsert_support_identity_link(
        db,
        keycloak_user_id=profile.user_id,
        zammad_user_id=zammad_user_id,
        email=profile.email,
    )
    db.commit()
    return zammad_user_id


async def sync_zammad_profile_for_identity(
    *,
    db: Session,
    keycloak_user_id: str,
    email: str,
    first_name: str,
    last_name: str,
) -> list[str]:
    if not settings.ZAMMAD_BASE_URL.strip() or not settings.ZAMMAD_API_TOKEN.strip():
        return []

    profile = SupportProfile(
        user_id=keycloak_user_id,
        email=email,
        first_name=first_name,
        last_name=last_name,
    )

    try:
        async with httpx.AsyncClient(timeout=20) as client:
            await resolve_or_create_zammad_customer_id(
                db=db,
                profile=profile,
                client=client,
                create_if_missing=False,
            )
    except HTTPException as exc:
        if exc.status_code in {502, 503}:
            return [str(exc.detail)]
        raise
    except Exception:
        logger.exception("unexpected zammad sync error for keycloak user %s", keycloak_user_id)
        return ["support_sync_failed"]

    return []


async def load_zammad_catalog(client: httpx.AsyncClient) -> dict[str, list[dict[str, Any]]]:
    require_zammad_configured()
    catalog: dict[str, list[dict[str, Any]]] = {}

    endpoints = {
        "groups": "/api/v1/groups",
        "priorities": "/api/v1/ticket_priorities",
        "states": "/api/v1/ticket_states",
    }
    for name, path in endpoints.items():
        try:
            response = await client.get(f"{zammad_base_url()}{path}", headers=zammad_headers())
        except httpx.RequestError as exc:
            raise_zammad_unreachable(name, exc)
        if response.status_code >= 400:
            raise HTTPException(status_code=502, detail=f"zammad_{name}_lookup_failed: {response.status_code}")
        payload = response.json()
        if not isinstance(payload, list):
            raise HTTPException(status_code=502, detail=f"zammad_{name}_unexpected_response")
        catalog[name] = [row for row in payload if isinstance(row, dict)]

    return catalog


async def load_zammad_customer_details(customer_id: int | None, client: httpx.AsyncClient) -> dict[str, Any] | None:
    if customer_id is None:
        return None
    return await get_zammad_user_record(int(customer_id), client)


async def save_support_attachment(
    *,
    db: Session,
    user_id: str,
    file: UploadFile,
    max_size: int,
) -> SupportAttachment:
    raw = await file.read(max_size + 1)
    if len(raw) > max_size:
        raise HTTPException(status_code=413, detail="attachment_too_large")
    if not raw:
        raise HTTPException(status_code=400, detail="empty_attachment")

    filename = safe_filename(file.filename or "attachment.bin")
    mime_type = (file.content_type or "application/octet-stream").strip() or "application/octet-stream"
    attachment_id = uuid.uuid4()
    user_dir = support_storage_root() / _safe_dir_segment(user_id)
    user_dir.mkdir(parents=True, exist_ok=True)
    storage_path = user_dir / f"{attachment_id}-{filename}"
    storage_path.write_bytes(raw)

    row = SupportAttachment(
        id=attachment_id,
        keycloak_user_id=user_id,
        filename=filename,
        mime_type=mime_type,
        size_bytes=len(raw),
        sha256=hashlib.sha256(raw).hexdigest(),
        storage_path=str(storage_path),
    )
    db.add(row)
    db.commit()
    db.refresh(row)
    return row


def support_attachment_payload(row: SupportAttachment) -> dict[str, Any]:
    return {
        "id": str(row.id),
        "filename": row.filename,
        "mime_type": row.mime_type,
        "size": row.size_bytes,
        "created_at": row.created_at.isoformat() if row.created_at else None,
    }


def materialize_stored_attachments(
    *,
    db: Session,
    attachment_ids: list[uuid.UUID],
    requester_user_id: str,
    elevated: bool,
) -> tuple[list[dict[str, Any]], list[SupportAttachment]]:
    if not attachment_ids:
        return [], []

    rows = db.execute(
        select(SupportAttachment).where(SupportAttachment.id.in_(attachment_ids))
    ).scalars().all()
    row_by_id = {row.id: row for row in rows}
    missing_ids = [attachment_id for attachment_id in attachment_ids if attachment_id not in row_by_id]
    if missing_ids:
        raise HTTPException(status_code=404, detail="attachment_not_found")

    materialized: list[dict[str, Any]] = []
    ordered_rows: list[SupportAttachment] = []
    for attachment_id in attachment_ids:
        row = row_by_id[attachment_id]
        if not elevated and row.keycloak_user_id != requester_user_id:
            raise HTTPException(status_code=404, detail="attachment_not_found")
        if row.consumed_at is not None:
            raise HTTPException(status_code=409, detail="attachment_already_used")

        path = Path(row.storage_path)
        if not path.exists():
            raise HTTPException(status_code=410, detail="attachment_missing")

        raw = path.read_bytes()
        materialized.append(
            {
                "filename": row.filename,
                "data": base64.b64encode(raw).decode("ascii"),
                "mime-type": row.mime_type,
            }
        )
        ordered_rows.append(row)

    return materialized, ordered_rows


def mark_attachments_consumed(
    db: Session,
    rows: list[SupportAttachment],
    *,
    zammad_ticket_id: int | None,
    zammad_article_id: int | None = None,
) -> None:
    consumed_at = utcnow()
    for row in rows:
        row.consumed_at = consumed_at
        row.zammad_ticket_id = zammad_ticket_id
        row.zammad_article_id = zammad_article_id
    if rows:
        db.commit()


def should_block_customer_support_action(config: EffectiveSupportSettings, *, elevated: bool) -> bool:
    return bool(config.maintenance_mode and not elevated)


def verify_zammad_webhook_hmac(secret: str, body: bytes, signature_header: str | None) -> bool:
    header = normalize_name(signature_header)
    if not secret or not header:
        return False
    if header.lower().startswith("sha1="):
        header = header[5:]
    digest = hmac.new(secret.encode("utf-8"), body, hashlib.sha1).hexdigest()
    return hmac.compare_digest(digest, header)


def _notification_exists(db: Session, *, user_id: str, external_id: str) -> bool:
    row = db.execute(
        select(Notification).where(
            Notification.user_id == user_id,
            Notification.meta["external_id"].astext == external_id,
        )
    ).scalar_one_or_none()
    return row is not None


def upsert_user_notification(
    db: Session,
    *,
    user_id: str,
    type_: str,
    title: str,
    body: str,
    external_id: str,
    meta: dict[str, Any] | None = None,
) -> bool:
    if _notification_exists(db, user_id=user_id, external_id=external_id):
        return False

    payload_meta = dict(meta or {})
    payload_meta["external_id"] = external_id
    db.add(
        Notification(
            user_id=user_id,
            type=type_,
            title=title,
            body=body,
            meta=payload_meta,
        )
    )
    db.flush()
    return True


def support_state_label(state: str | None) -> str:
    normalized = (normalize_name(state) or "").lower()
    return _SUPPORT_STATE_LABELS.get(normalized, normalize_name(state) or "Aktualisiert")


def support_state_severity(state: str | None) -> str:
    normalized = (normalize_name(state) or "").lower()
    if normalized in {"pending reminder", "pending close"}:
        return "warning"
    return "info"


def _article_sender_is_agent(article: dict[str, Any]) -> bool:
    sender_id = coerce_int(article.get("sender_id"))
    if sender_id == 1:
        return True
    sender = (normalize_name(article.get("sender")) or "").lower()
    return sender == "agent"


def process_zammad_webhook_event(
    *,
    db: Session,
    event_id: str,
    payload: dict[str, Any] | None,
) -> dict[str, Any]:
    if not isinstance(payload, dict):
        return {"notifications_created": 0}

    ticket = payload.get("ticket")
    if not isinstance(ticket, dict):
        return {"notifications_created": 0}

    ticket_id = coerce_int(ticket.get("id"))
    customer_id = coerce_int(ticket.get("customer_id"))
    if ticket_id is None or customer_id is None:
        return {"notifications_created": 0}

    link = db.execute(
        select(SupportIdentityLink).where(SupportIdentityLink.zammad_user_id == customer_id)
    ).scalar_one_or_none()
    if link is None:
        return {"notifications_created": 0, "ticket_id": ticket_id}

    state = normalize_name(ticket.get("state")) or normalize_name(ticket.get("state_name"))
    ticket_number = normalize_name(ticket.get("number")) or str(ticket_id)
    ticket_title = normalize_name(ticket.get("title")) or "Ohne Betreff"
    updated_at = parse_zammad_datetime(ticket.get("updated_at"))
    last_contact_agent_at = parse_zammad_datetime(ticket.get("last_contact_agent_at"))
    last_contact_customer_at = parse_zammad_datetime(ticket.get("last_contact_customer_at"))

    article = payload.get("article") if isinstance(payload.get("article"), dict) else {}
    article_id = coerce_int(article.get("id"))
    article_internal = coerce_bool(article.get("internal"))
    article_created_at = parse_zammad_datetime(article.get("created_at"))
    notification_payload = payload.get("notification") if isinstance(payload.get("notification"), dict) else {}
    changes_text = normalize_name(notification_payload.get("changes"))

    snapshot = db.execute(
        select(SupportTicketSyncState).where(
            SupportTicketSyncState.keycloak_user_id == link.keycloak_user_id,
            SupportTicketSyncState.zammad_ticket_id == ticket_id,
        )
    ).scalar_one_or_none()
    if snapshot is None:
        snapshot = SupportTicketSyncState(
            keycloak_user_id=link.keycloak_user_id,
            zammad_ticket_id=ticket_id,
        )
        db.add(snapshot)
        db.flush()

    notifications_created = 0

    if article_id is not None and not article_internal and _article_sender_is_agent(article):
        if snapshot.last_public_agent_article_id != article_id:
            preview = normalize_name(article.get("body")) or "Ihr Support-Ticket wurde aktualisiert."
            preview = preview.replace("\r\n", "\n").replace("\r", "\n").split("\n", 1)[0][:220]
            if not preview:
                preview = "Ihr Support-Ticket wurde aktualisiert."
            if upsert_user_notification(
                db,
                user_id=link.keycloak_user_id,
                type_="support.ticket.reply",
                title=f"Support hat Ticket #{ticket_number} beantwortet",
                body=f"\"{ticket_title}\" wartet auf Ihre Rueckmeldung. {preview}",
                external_id=f"support:ticket:{ticket_id}:agent-article:{article_id}",
                meta={
                    "severity": "info",
                    "href": "/support",
                    "action_label": "Verlauf oeffnen",
                    "ticket_id": str(ticket_id),
                    "ticket_number": ticket_number,
                    "ticket_state": state,
                    "article_id": article_id,
                },
            ):
                notifications_created += 1

    previous_state = normalize_name(snapshot.last_state)
    state_changed = False
    normalized_state = state.lower() if state else None
    if previous_state and state and previous_state.lower() != normalized_state:
        state_changed = True
    elif not previous_state and changes_text and "ticket.state" in changes_text.lower():
        state_changed = True
    elif not previous_state and normalized_state in {"closed", "pending reminder", "pending close"}:
        state_changed = True

    if state_changed:
        state_label = support_state_label(state)
        if upsert_user_notification(
            db,
            user_id=link.keycloak_user_id,
            type_="support.ticket.state",
            title=f"Ticket #{ticket_number} ist jetzt {state_label.lower()}",
            body=f"\"{ticket_title}\" wurde auf {state_label.lower()} gesetzt.",
            external_id=f"support:ticket:{ticket_id}:state:{event_id}",
            meta={
                "severity": support_state_severity(state),
                "href": "/support",
                "action_label": "Ticket ansehen",
                "ticket_id": str(ticket_id),
                "ticket_number": ticket_number,
                "ticket_state": state,
            },
        ):
            notifications_created += 1

    snapshot.ticket_number = ticket_number
    snapshot.ticket_title = ticket_title
    snapshot.last_state = state
    snapshot.last_ticket_updated_at = updated_at
    snapshot.last_contact_agent_at = last_contact_agent_at
    snapshot.last_contact_customer_at = last_contact_customer_at
    if article_id is not None and not article_internal and _article_sender_is_agent(article):
        snapshot.last_public_agent_article_id = article_id
        snapshot.last_public_agent_article_at = article_created_at

    db.flush()
    return {
        "notifications_created": notifications_created,
        "ticket_id": ticket_id,
        "keycloak_user_id": link.keycloak_user_id,
    }
