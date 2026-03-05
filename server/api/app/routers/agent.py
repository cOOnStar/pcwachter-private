import hashlib
import secrets
from datetime import datetime, timedelta, timezone

from fastapi import APIRouter, Depends, HTTPException, Request
from slowapi import Limiter
from slowapi.util import get_remote_address
from sqlalchemy import select
from sqlalchemy.orm import Session

limiter = Limiter(key_func=get_remote_address)

from ..db import get_db
from ..models import Device, DeviceInventory, DeviceToken, License, Plan
from ..schemas import (
    AgentHeartbeatRequest,
    AgentHeartbeatResponse,
    AgentInventoryRequest,
    AgentInventoryResponse,
    AgentRegisterRequest,
    AgentRegisterResponse,
    AgentTokenRotateResponse,
)
from ..security import require_agent_auth, require_agent_register

router = APIRouter(prefix="/agent", tags=["agent"])

_TOKEN_LIFETIME_DAYS = 30


def utcnow() -> datetime:
    return datetime.now(timezone.utc)


def _hash_token(plain: str) -> str:
    return hashlib.sha256(plain.encode()).hexdigest()


def _get_or_create_device(db: Session, device_install_id: str) -> Device:
    normalized = device_install_id.strip()
    device = db.execute(select(Device).where(Device.device_install_id == normalized)).scalar_one_or_none()
    if device is None:
        device = Device(device_install_id=normalized)
        db.add(device)
    return device


def _issue_device_token(db: Session, device_install_id: str) -> str:
    plain = secrets.token_urlsafe(32)
    token_hash = _hash_token(plain)
    expires_at = utcnow() + timedelta(days=_TOKEN_LIFETIME_DAYS)
    db.add(DeviceToken(
        device_install_id=device_install_id.strip(),
        token_hash=token_hash,
        expires_at=expires_at,
    ))
    return plain


def _check_device_limit(db: Session, device_install_id: str) -> None:
    normalized = device_install_id.strip()
    license_row = db.execute(
        select(License)
        .where(License.activated_device_install_id == normalized)
        .where(License.state == "activated")
        .limit(1)
    ).scalar_one_or_none()

    if not license_row or not license_row.activated_by_user_id:
        return

    plan = db.execute(select(Plan).where(Plan.id == license_row.tier)).scalar_one_or_none()
    if not plan or plan.max_devices is None:
        return

    from sqlalchemy import func as sqlfunc, distinct
    active_device_count = db.execute(
        select(sqlfunc.count(distinct(License.activated_device_install_id)))
        .where(
            License.activated_by_user_id == license_row.activated_by_user_id,
            License.state == "activated",
            License.activated_device_install_id.is_not(None),
        )
    ).scalar() or 0

    if active_device_count > plan.max_devices:
        raise HTTPException(
            status_code=403,
            detail={"error": "device_limit_exceeded", "limit": plan.max_devices},
        )


@router.post("/register", response_model=AgentRegisterResponse, dependencies=[Depends(require_agent_register)])
@limiter.limit("10/minute")
def register(request: Request, payload: AgentRegisterRequest, db: Session = Depends(get_db)):
    device = _get_or_create_device(db, payload.device_install_id)
    _check_device_limit(db, payload.device_install_id.strip())

    device.host_name = payload.hostname or device.host_name

    if payload.os:
        device.os_name = payload.os.name or device.os_name
        device.os_version = payload.os.version or device.os_version
        device.os_build = payload.os.build or device.os_build

    if payload.agent:
        device.agent_version = payload.agent.version or device.agent_version
        device.agent_channel = payload.agent.channel or device.agent_channel

    if payload.network:
        device.primary_ip = payload.network.primary_ip or device.primary_ip
        device.macs = {"macs": payload.network.macs}

    device.last_seen_at = utcnow()
    device_token = _issue_device_token(db, payload.device_install_id)

    db.commit()
    db.refresh(device)

    return AgentRegisterResponse(
        device_id=device.id,
        poll_interval_seconds=30,
        server_time=utcnow(),
        device_token=device_token,
    )


@router.post("/heartbeat", response_model=AgentHeartbeatResponse)
def heartbeat(
    payload: AgentHeartbeatRequest,
    db: Session = Depends(get_db),
    _auth: str = Depends(require_agent_auth),
):
    device = db.execute(select(Device).where(Device.device_install_id == payload.device_install_id.strip())).scalar_one_or_none()
    if device is None:
        raise HTTPException(status_code=404, detail="device not registered")

    device.last_seen_at = utcnow()
    db.commit()
    return AgentHeartbeatResponse(ok=True, server_time=utcnow())


@router.post("/inventory", response_model=AgentInventoryResponse)
def inventory(
    payload: AgentInventoryRequest,
    db: Session = Depends(get_db),
    _auth: str = Depends(require_agent_auth),
):
    device = db.execute(select(Device).where(Device.device_install_id == payload.device_install_id.strip())).scalar_one_or_none()
    if device is None:
        raise HTTPException(status_code=404, detail="device not registered")

    inv = DeviceInventory(device_id=device.id, collected_at=payload.collected_at, payload=payload.inventory)
    db.add(inv)
    device.last_seen_at = utcnow()

    db.commit()
    db.refresh(inv)
    return AgentInventoryResponse(ok=True, inventory_id=inv.id)


@router.post("/token/rotate", response_model=AgentTokenRotateResponse)
def rotate_token(
    request: Request,
    db: Session = Depends(get_db),
    auth: str = Depends(require_agent_auth),
):
    """Revoke the current device token and issue a fresh one.

    Accepted auth: static API key OR an existing (non-expired, non-revoked) device token.
    The new token is returned once — store it securely.
    """
    # Determine which device_install_id to rotate for
    if auth.startswith("device:"):
        device_install_id = auth[len("device:"):]
    else:
        # API-key auth: device_install_id must be supplied in query param
        device_install_id = request.query_params.get("device_install_id", "").strip()
        if not device_install_id:
            raise HTTPException(status_code=422, detail="device_install_id required when authenticating with api key")

    # Verify device exists
    device = db.execute(select(Device).where(Device.device_install_id == device_install_id)).scalar_one_or_none()
    if device is None:
        raise HTTPException(status_code=404, detail="device not registered")

    # Revoke all active tokens for this device
    now = utcnow()
    active_tokens = db.execute(
        select(DeviceToken).where(
            DeviceToken.device_install_id == device_install_id,
            DeviceToken.revoked_at.is_(None),
        )
    ).scalars().all()
    for tok in active_tokens:
        tok.revoked_at = now

    # Issue fresh token
    new_plain = _issue_device_token(db, device_install_id)
    db.commit()

    return AgentTokenRotateResponse(device_token=new_plain, server_time=utcnow())
