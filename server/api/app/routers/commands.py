"""Device Commands – remote command dispatch.

Admin:
  GET  /console/ui/devices/{device_id}/commands   – list commands for a device
  POST /console/ui/devices/{device_id}/commands   – issue a new command
  PATCH /console/ui/commands/{command_id}/cancel  – cancel a pending command

Agent:
  GET  /agent/commands                            – poll for pending commands (marks as sent)
  POST /agent/commands/{command_id}/result        – report execution result
"""
from __future__ import annotations

from datetime import datetime, timezone
from typing import Any

from fastapi import APIRouter, Depends, HTTPException, Query
from pydantic import BaseModel, Field
from sqlalchemy import select
from sqlalchemy.orm import Session

from ..db import get_db
from ..models import Device, DeviceCommand
from ..security import require_agent_auth
from ..security_jwt import require_console_owner, require_console_user

router = APIRouter(tags=["commands"])

ALLOWED_COMMANDS = {
    "restart_agent",
    "run_scan",
    "update_agent",
    "clear_findings",
    "collect_inventory",
    "ping",
}


# ---------------------------------------------------------------------------
# Schemas
# ---------------------------------------------------------------------------

class CommandItem(BaseModel):
    id: str
    device_id: str
    command: str
    payload: dict[str, Any] | None
    status: str
    issued_by_admin_id: str | None
    issued_at: str
    sent_at: str | None
    done_at: str | None
    result: dict[str, Any] | None


class CommandListResponse(BaseModel):
    items: list[CommandItem]
    total: int


class IssueCommandRequest(BaseModel):
    command: str = Field(min_length=1, max_length=64)
    payload: dict[str, Any] | None = None


class CommandResultRequest(BaseModel):
    success: bool
    output: dict[str, Any] | None = None
    error: str | None = None


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _to_item(c: DeviceCommand) -> CommandItem:
    return CommandItem(
        id=str(c.id),
        device_id=str(c.device_id),
        command=c.command,
        payload=c.payload,
        status=c.status,
        issued_by_admin_id=c.issued_by_admin_id,
        issued_at=c.issued_at.isoformat(),
        sent_at=c.sent_at.isoformat() if c.sent_at else None,
        done_at=c.done_at.isoformat() if c.done_at else None,
        result=c.result,
    )


# ---------------------------------------------------------------------------
# Admin endpoints
# ---------------------------------------------------------------------------

@router.get("/console/ui/devices/{device_id}/commands", response_model=CommandListResponse)
def list_device_commands(
    device_id: str,
    status: str | None = Query(default=None),
    limit: int = Query(default=50, ge=1, le=200),
    offset: int = Query(default=0, ge=0),
    db: Session = Depends(get_db),
    _user: dict = Depends(require_console_user),
) -> CommandListResponse:
    device = db.execute(
        select(Device).where(Device.device_install_id == device_id.strip())
    ).scalar_one_or_none()
    if not device:
        raise HTTPException(status_code=404, detail="device not found")

    stmt = select(DeviceCommand).where(DeviceCommand.device_id == device.id)
    if status:
        stmt = stmt.where(DeviceCommand.status == status)

    from sqlalchemy import func
    total = db.execute(select(func.count()).select_from(stmt.subquery())).scalar_one()
    rows = db.execute(stmt.order_by(DeviceCommand.issued_at.desc()).limit(limit).offset(offset)).scalars().all()
    return CommandListResponse(items=[_to_item(c) for c in rows], total=total)


@router.post("/console/ui/devices/{device_id}/commands", response_model=CommandItem)
def issue_command(
    device_id: str,
    payload: IssueCommandRequest,
    db: Session = Depends(get_db),
    owner: dict = Depends(require_console_owner),
) -> CommandItem:
    if payload.command not in ALLOWED_COMMANDS:
        raise HTTPException(
            status_code=422,
            detail=f"unknown command '{payload.command}'. Allowed: {sorted(ALLOWED_COMMANDS)}",
        )

    device = db.execute(
        select(Device).where(Device.device_install_id == device_id.strip())
    ).scalar_one_or_none()
    if not device:
        raise HTTPException(status_code=404, detail="device not found")

    cmd = DeviceCommand(
        device_id=device.id,
        command=payload.command,
        payload=payload.payload,
        status="pending",
        issued_by_admin_id=str(owner.get("sub", "")),
    )
    db.add(cmd)
    db.commit()
    db.refresh(cmd)
    return _to_item(cmd)


@router.patch("/console/ui/commands/{command_id}/cancel", response_model=CommandItem)
def cancel_command(
    command_id: str,
    db: Session = Depends(get_db),
    _owner: dict = Depends(require_console_owner),
) -> CommandItem:
    cmd = db.get(DeviceCommand, command_id)
    if not cmd:
        raise HTTPException(status_code=404, detail="command not found")
    if cmd.status not in ("pending",):
        raise HTTPException(status_code=409, detail=f"cannot cancel command in status '{cmd.status}'")
    cmd.status = "cancelled"
    db.commit()
    db.refresh(cmd)
    return _to_item(cmd)


# ---------------------------------------------------------------------------
# Agent endpoints
# ---------------------------------------------------------------------------

@router.get("/agent/commands", response_model=CommandListResponse)
def agent_poll_commands(
    device_install_id: str = Query(..., min_length=3, max_length=128),
    db: Session = Depends(get_db),
    _auth: str = Depends(require_agent_auth),
) -> CommandListResponse:
    """Agent polls for pending commands; marks them as 'sent'."""
    device = db.execute(
        select(Device).where(Device.device_install_id == device_install_id.strip())
    ).scalar_one_or_none()
    if not device:
        raise HTTPException(status_code=404, detail="device not found")

    now = datetime.now(timezone.utc)
    pending = db.execute(
        select(DeviceCommand)
        .where(DeviceCommand.device_id == device.id, DeviceCommand.status == "pending")
        .order_by(DeviceCommand.issued_at.asc())
    ).scalars().all()

    for cmd in pending:
        cmd.status = "sent"
        cmd.sent_at = now

    db.commit()
    return CommandListResponse(items=[_to_item(c) for c in pending], total=len(pending))


@router.post("/agent/commands/{command_id}/result", response_model=CommandItem)
def agent_command_result(
    command_id: str,
    payload: CommandResultRequest,
    db: Session = Depends(get_db),
    _auth: str = Depends(require_agent_auth),
) -> CommandItem:
    """Agent reports execution result for a command."""
    cmd = db.get(DeviceCommand, command_id)
    if not cmd:
        raise HTTPException(status_code=404, detail="command not found")

    now = datetime.now(timezone.utc)
    cmd.status = "done" if payload.success else "failed"
    cmd.done_at = now
    cmd.result = {
        "success": payload.success,
        "output": payload.output,
        "error": payload.error,
    }
    db.commit()
    db.refresh(cmd)
    return _to_item(cmd)
