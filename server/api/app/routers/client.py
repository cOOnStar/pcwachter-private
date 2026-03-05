from __future__ import annotations

from datetime import datetime, timezone

from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel, Field
from sqlalchemy import select
from sqlalchemy.orm import Session
from typing import Optional

from ..db import get_db
from ..models import Device
from ..security import require_agent_auth

router = APIRouter(prefix="/client", tags=["client"])


class ClientStatusIn(BaseModel):
    device_install_id: str = Field(..., min_length=3, max_length=128)
    desktop_version: Optional[str] = Field(None, max_length=64)
    updater_version: Optional[str] = Field(None, max_length=64)
    update_channel: Optional[str] = Field(None, max_length=32)


class OkResponse(BaseModel):
    ok: bool = True


@router.post("/status", response_model=OkResponse)
def post_client_status(
    payload: ClientStatusIn,
    db: Session = Depends(get_db),
    _auth: str = Depends(require_agent_auth),
) -> OkResponse:
    """Desktop/Updater melden ihre installierten Versionen und Update-Channel.

    Auth: Device Token (preferred) oder statischer API-Key (legacy).
    """
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
