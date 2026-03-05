from datetime import datetime, timezone

from fastapi import APIRouter, Depends
from sqlalchemy import select
from sqlalchemy.orm import Session

from ..db import get_db
from ..models import Device, TelemetrySnapshot
from ..schemas import OkResponse, TelemetrySnapshotIngestRequest, TelemetryUpdateRequest
from ..security import require_api_key
from .rules import evaluate_rules_for_device

router = APIRouter(prefix="/telemetry", tags=["telemetry"], dependencies=[Depends(require_api_key)])


def utcnow() -> datetime:
    return datetime.now(timezone.utc)


def get_or_create_device(db: Session, install_id: str, host_name: str) -> Device:
    normalized = install_id.strip()
    device = db.execute(select(Device).where(Device.device_install_id == normalized)).scalar_one_or_none()
    if device is None:
        device = Device(device_install_id=normalized, host_name=host_name.strip())
        db.add(device)
    else:
        device.host_name = host_name.strip() or device.host_name

    device.last_seen_at = utcnow()
    return device


@router.post("/update", response_model=OkResponse)
def ingest_update(payload: TelemetryUpdateRequest, db: Session = Depends(get_db)):
    device = get_or_create_device(db, payload.device_install_id, payload.host_name)

    if payload.result == "success":
        summary = f"success {payload.old_version or '-'} -> {payload.new_version or '-'}"
    elif payload.result == "rolled_back":
        summary = f"rolled_back to {payload.old_version or '-'}"
    elif payload.result == "deferred":
        summary = "deferred (offline/transient)"
    elif payload.result == "silent_not_supported":
        summary = "silent_not_supported"
    else:
        summary = f"failed ({payload.exit_code})"

    db.add(
        TelemetrySnapshot(
            device_id=device.id,
            category="update",
            payload=payload.model_dump(mode="json"),
            summary=summary,
            source=payload.source,
        )
    )

    db.commit()
    return OkResponse(ok=True)


@router.post("/snapshot", response_model=OkResponse)
def ingest_snapshot(payload: TelemetrySnapshotIngestRequest, db: Session = Depends(get_db)):
    device = get_or_create_device(db, payload.device_install_id, payload.host_name)

    db.add(
        TelemetrySnapshot(
            device_id=device.id,
            category=payload.category,
            payload=payload.payload,
            summary=payload.summary,
            source=payload.source,
        )
    )

    db.flush()
    try:
        evaluate_rules_for_device(db, device.id, payload.category, payload.payload or {})
    except Exception:
        pass  # Rule evaluation must never block telemetry ingest

    db.commit()
    return OkResponse(ok=True)
