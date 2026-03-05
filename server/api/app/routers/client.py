from __future__ import annotations

from datetime import datetime, timezone
from typing import Any, Optional

from fastapi import APIRouter, Depends, HTTPException, Query
from pydantic import BaseModel, Field
from sqlalchemy import select
from sqlalchemy.orm import Session

from ..db import get_db
from ..models import ClientConfig, Device
from ..security import require_agent_auth
from ..security_jwt import require_console_owner, require_console_user

router = APIRouter(prefix="/client", tags=["client"])


class ClientStatusIn(BaseModel):
    device_install_id: str = Field(..., min_length=3, max_length=128)
    desktop_version: Optional[str] = Field(None, max_length=64)
    updater_version: Optional[str] = Field(None, max_length=64)
    update_channel: Optional[str] = Field(None, max_length=32)


class OkResponse(BaseModel):
    ok: bool = True


class ClientConfigItem(BaseModel):
    id: str
    scope: str
    scope_id: str | None
    config: dict[str, Any]
    updated_at: str
    updated_by_admin_id: str | None


class ClientConfigUpsertRequest(BaseModel):
    scope: str = "global"
    scope_id: str | None = None
    config: dict[str, Any]


class ClientConfigResponse(BaseModel):
    # Merged config delivered to the client (global merged with device-specific)
    config: dict[str, Any]


# ---------------------------------------------------------------------------
# Agent/Client endpoints
# ---------------------------------------------------------------------------

@router.post("/status", response_model=OkResponse)
def post_client_status(
    payload: ClientStatusIn,
    db: Session = Depends(get_db),
    _auth: str = Depends(require_agent_auth),
) -> OkResponse:
    """Desktop/Updater melden ihre installierten Versionen und Update-Channel."""
    device = db.execute(
        select(Device).where(Device.device_install_id == payload.device_install_id.strip())
    ).scalar_one_or_none()
    if not device:
        raise HTTPException(status_code=404, detail="device not found")

    if payload.desktop_version is not None:
        device.desktop_version = payload.desktop_version
    if payload.updater_version is not None:
        device.updater_version = payload.updater_version
    if payload.update_channel is not None:
        device.update_channel = payload.update_channel

    device.last_seen_at = datetime.now(timezone.utc)
    db.commit()
    return OkResponse()


@router.get("/config", response_model=ClientConfigResponse)
def get_client_config(
    device_install_id: str = Query(..., min_length=3, max_length=128),
    db: Session = Depends(get_db),
    _auth: str = Depends(require_agent_auth),
) -> ClientConfigResponse:
    """Return merged config for a device (global overridden by device-specific)."""
    # Global config
    global_row = db.execute(
        select(ClientConfig).where(ClientConfig.scope == "global", ClientConfig.scope_id.is_(None))
    ).scalar_one_or_none()
    merged: dict[str, Any] = dict(global_row.config) if global_row and global_row.config else {}

    # Device-specific override
    device_row = db.execute(
        select(ClientConfig).where(
            ClientConfig.scope == "device",
            ClientConfig.scope_id == device_install_id.strip(),
        )
    ).scalar_one_or_none()
    if device_row and device_row.config:
        merged.update(device_row.config)

    return ClientConfigResponse(config=merged)


# ---------------------------------------------------------------------------
# Admin endpoints
# ---------------------------------------------------------------------------

@router.get("/console/ui/client-config", response_model=list[ClientConfigItem])
def list_client_configs(
    db: Session = Depends(get_db),
    _user: dict = Depends(require_console_user),
) -> list[ClientConfigItem]:
    rows = db.execute(
        select(ClientConfig).order_by(ClientConfig.scope, ClientConfig.scope_id)
    ).scalars().all()
    return [
        ClientConfigItem(
            id=str(r.id),
            scope=r.scope,
            scope_id=r.scope_id,
            config=r.config if isinstance(r.config, dict) else {},
            updated_at=r.updated_at.isoformat(),
            updated_by_admin_id=r.updated_by_admin_id,
        )
        for r in rows
    ]


@router.post("/console/ui/client-config", response_model=ClientConfigItem)
def upsert_client_config(
    payload: ClientConfigUpsertRequest,
    db: Session = Depends(get_db),
    owner: dict = Depends(require_console_owner),
) -> ClientConfigItem:
    if payload.scope not in ("global", "device", "user"):
        raise HTTPException(status_code=422, detail="invalid scope")
    if payload.scope == "global" and payload.scope_id is not None:
        raise HTTPException(status_code=422, detail="scope_id must be null for global scope")
    if payload.scope != "global" and not payload.scope_id:
        raise HTTPException(status_code=422, detail="scope_id required for non-global scope")

    existing = db.execute(
        select(ClientConfig).where(
            ClientConfig.scope == payload.scope,
            ClientConfig.scope_id == payload.scope_id if payload.scope_id else ClientConfig.scope_id.is_(None),
        )
    ).scalar_one_or_none()

    admin_id = str(owner.get("sub", ""))
    now = datetime.now(timezone.utc)

    if existing is None:
        existing = ClientConfig(
            scope=payload.scope,
            scope_id=payload.scope_id,
            config=payload.config,
            updated_by_admin_id=admin_id,
        )
        db.add(existing)
    else:
        existing.config = payload.config
        existing.updated_at = now
        existing.updated_by_admin_id = admin_id

    db.commit()
    db.refresh(existing)
    return ClientConfigItem(
        id=str(existing.id),
        scope=existing.scope,
        scope_id=existing.scope_id,
        config=existing.config if isinstance(existing.config, dict) else {},
        updated_at=existing.updated_at.isoformat(),
        updated_by_admin_id=existing.updated_by_admin_id,
    )
