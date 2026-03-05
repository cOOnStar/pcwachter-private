from datetime import datetime, timezone

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy import and_, func, select
from sqlalchemy.orm import Session

from ..db import get_db
from ..models import Device, TelemetrySnapshot
from ..schemas import DeviceOverviewItem, DeviceOverviewLatest, DeviceOverviewResponse, OkResponse, TelemetrySnapshotEnvelope
from ..security import require_api_key

router = APIRouter(prefix="/admin", tags=["admin"], dependencies=[Depends(require_api_key)])


def utcnow() -> datetime:
    return datetime.now(timezone.utc)


@router.get("/devices/overview", response_model=DeviceOverviewResponse)
def devices_overview(db: Session = Depends(get_db)):
    latest_ranked = (
        select(
            TelemetrySnapshot.id.label("snapshot_id"),
            TelemetrySnapshot.device_id.label("device_id"),
            TelemetrySnapshot.category.label("category"),
            TelemetrySnapshot.received_at.label("received_at"),
            TelemetrySnapshot.summary.label("summary"),
            TelemetrySnapshot.payload.label("payload"),
            TelemetrySnapshot.source.label("source"),
            func.row_number()
            .over(
                partition_by=(TelemetrySnapshot.device_id, TelemetrySnapshot.category),
                order_by=TelemetrySnapshot.received_at.desc(),
            )
            .label("rn"),
        )
        .subquery()
    )

    latest = select(latest_ranked).where(latest_ranked.c.rn == 1).subquery()

    rows = db.execute(
        select(
            Device.device_install_id,
            Device.host_name,
            Device.last_seen_at,
            latest.c.snapshot_id,
            latest.c.category,
            latest.c.received_at,
            latest.c.summary,
            latest.c.payload,
            latest.c.source,
        )
        .outerjoin(latest, latest.c.device_id == Device.id)
        .order_by(Device.last_seen_at.desc().nullslast(), Device.created_at.desc())
    ).all()

    by_device: dict[str, DeviceOverviewItem] = {}
    for row in rows:
        install_id = row.device_install_id
        if install_id not in by_device:
            by_device[install_id] = DeviceOverviewItem(
                device_install_id=install_id,
                host_name=row.host_name,
                last_seen_at=row.last_seen_at,
                latest=DeviceOverviewLatest(),
            )

        if row.category:
            envelope = TelemetrySnapshotEnvelope(
                id=row.snapshot_id,
                received_at=row.received_at,
                summary=row.summary,
                payload=row.payload,
                source=row.source,
            )
            if row.category in {"memory", "ssd", "antivirus", "update"}:
                setattr(by_device[install_id].latest, row.category, envelope)

    items = list(by_device.values())
    return DeviceOverviewResponse(items=items, total=len(items))


@router.delete("/devices/{device_install_id}", response_model=OkResponse)
def delete_device(device_install_id: str, db: Session = Depends(get_db)):
    device = db.execute(select(Device).where(Device.device_install_id == device_install_id.strip())).scalar_one_or_none()
    if not device:
        raise HTTPException(status_code=404, detail="device not found")

    db.query(TelemetrySnapshot).filter(TelemetrySnapshot.device_id == device.id).delete(synchronize_session=False)
    db.delete(device)
    db.commit()
    return OkResponse(ok=True)


@router.delete("/devices/{device_install_id}/snapshots/{snapshot_id}", response_model=OkResponse)
def delete_snapshot(device_install_id: str, snapshot_id: str, db: Session = Depends(get_db)):
    device = db.execute(select(Device).where(Device.device_install_id == device_install_id.strip())).scalar_one_or_none()
    if not device:
        raise HTTPException(status_code=404, detail="device not found")

    deleted = db.query(TelemetrySnapshot).filter(
        and_(TelemetrySnapshot.device_id == device.id, TelemetrySnapshot.id == snapshot_id)
    ).delete(synchronize_session=False)

    if not deleted:
        raise HTTPException(status_code=404, detail="snapshot not found")

    device.last_seen_at = utcnow()
    db.commit()
    return OkResponse(ok=True)
