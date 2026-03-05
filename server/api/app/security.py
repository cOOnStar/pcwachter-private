import hashlib
from datetime import datetime, timezone

from fastapi import Depends, Header, HTTPException
from sqlalchemy import select
from sqlalchemy.orm import Session

from .db import get_db
from .settings import settings


def _parse_keys(raw: str) -> set[str]:
    return {item.strip() for item in raw.split(",") if item.strip()}


def _hash_token(plain: str) -> str:
    return hashlib.sha256(plain.encode()).hexdigest()


def require_api_key(
    x_api_key: str | None = Header(default=None),
    x_agent_api_key: str | None = Header(default=None),
) -> str:
    configured = _parse_keys(settings.API_KEYS) | _parse_keys(settings.AGENT_API_KEYS)
    if not configured:
        raise HTTPException(status_code=503, detail="api authentication not configured")

    supplied = (x_api_key or x_agent_api_key or "").strip()
    if not supplied or supplied not in configured:
        raise HTTPException(status_code=401, detail="invalid api key")

    return supplied


def require_agent_auth(
    x_api_key: str | None = Header(default=None),
    x_agent_api_key: str | None = Header(default=None),
    x_device_token: str | None = Header(default=None),
    db: Session = Depends(get_db),
) -> str:
    """Accept either a static API key OR a device token issued at registration."""
    # 1. Try static API key first (backward compat)
    configured = _parse_keys(settings.API_KEYS) | _parse_keys(settings.AGENT_API_KEYS)
    supplied_key = (x_api_key or x_agent_api_key or "").strip()
    if supplied_key and supplied_key in configured:
        return f"apikey:{supplied_key}"

    # 2. Try device token
    if x_device_token:
        from .models import DeviceToken
        token_hash = _hash_token(x_device_token.strip())
        now = datetime.now(timezone.utc)
        dt = db.execute(
            select(DeviceToken).where(
                DeviceToken.token_hash == token_hash,
                DeviceToken.revoked_at.is_(None),
            )
        ).scalar_one_or_none()
        if dt and (dt.expires_at is None or dt.expires_at > now):
            dt.last_used_at = now
            db.commit()
            return f"device:{dt.device_install_id}"

    raise HTTPException(status_code=401, detail="invalid credentials")


def require_agent_register(
    x_api_key: str | None = Header(default=None),
    x_agent_api_key: str | None = Header(default=None),
    x_agent_bootstrap_key: str | None = Header(default=None),
) -> str:
    """Auth for /agent/register.

    Accepts:
    - X-Agent-Bootstrap-Key: new bootstrap-secret (preferred)
    - Legacy static API key: only when ALLOW_LEGACY_API_KEY_REGISTER=True

    Set AGENT_BOOTSTRAP_KEY + ALLOW_LEGACY_API_KEY_REGISTER=false once all agents migrated.
    """
    bootstrap_key = settings.AGENT_BOOTSTRAP_KEY.strip()
    supplied_bootstrap = (x_agent_bootstrap_key or "").strip()

    # Bootstrap-key configured: register MUST use bootstrap-key (no API-key fallback).
    if bootstrap_key:
        if supplied_bootstrap and supplied_bootstrap == bootstrap_key:
            return "bootstrap"
        raise HTTPException(status_code=401, detail="invalid register credentials")

    # Bootstrap-key missing: either explicit legacy mode OR fail-fast (503).
    if settings.ALLOW_LEGACY_API_KEY_REGISTER:
        configured = _parse_keys(settings.API_KEYS) | _parse_keys(settings.AGENT_API_KEYS)
        supplied_key = (x_api_key or x_agent_api_key or "").strip()
        if supplied_key and supplied_key in configured:
            return f"apikey:{supplied_key}"
        raise HTTPException(status_code=401, detail="invalid register credentials")

    raise HTTPException(
        status_code=503,
        detail="agent_bootstrap_not_configured",
    )
