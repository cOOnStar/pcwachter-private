from __future__ import annotations

from datetime import datetime
from typing import Optional

from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel, Field
from sqlalchemy.orm import Session

# NOTE: Adjust imports to your repo structure:
from app.db import get_db  # unknown: verify actual path in your repo
from app.models import Device  # server/api/app/models.py


router = APIRouter(prefix="/client", tags=["client"])


class ClientStatusIn(BaseModel):
    device_install_id: str = Field(..., min_length=3, max_length=128)
    desktop_version: Optional[str] = Field(None, max_length=64)
    updater_version: Optional[str] = Field(None, max_length=64)
    update_channel: Optional[str] = Field(None, max_length=32)


class OkResponse(BaseModel):
    ok: bool = True


@router.post("/status", response_model=OkResponse)
def post_client_status(payload: ClientStatusIn, db: Session = Depends(get_db)) -> OkResponse:
    # Auth decision is repo-specific:
    # - Option A: device token (preferred)
    # - Option B: user JWT (desktop)
    # Implement dependency here once you decide:
    #   current_user = Depends(require_user_jwt)
    #   or device = Depends(require_device_token)

    device = db.query(Device).filter(Device.device_install_id == payload.device_install_id).one_or_none()
    if not device:
        raise HTTPException(status_code=404, detail="device not found")

    if payload.desktop_version is not None:
        device.desktop_version = payload.desktop_version
    if payload.updater_version is not None:
        device.updater_version = payload.updater_version
    if payload.update_channel is not None:
        device.update_channel = payload.update_channel

    device.last_seen_at = datetime.utcnow()
    db.add(device)
    db.commit()
    return OkResponse()
