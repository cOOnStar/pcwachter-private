"""Monthly report endpoint.

GET /reports/monthly?device_install_id=...
Returns a 30-day telemetry summary per category for the given device.
Requires a valid home/user JWT.
"""
from __future__ import annotations

from datetime import datetime, timedelta, timezone
from typing import Any

from fastapi import APIRouter, Depends, HTTPException, Query
from pydantic import BaseModel
from sqlalchemy import select
from sqlalchemy.orm import Session

from ..db import get_db
from ..models import Device, TelemetrySnapshot
from ..security_jwt import require_home_user

router = APIRouter(prefix="/reports", tags=["reports"])


class CategorySummary(BaseModel):
    category: str
    count: int
    latest_at: str | None
    latest_summary: str | None
    latest_payload: dict[str, Any] | None


class MonthlyReportResponse(BaseModel):
    device_install_id: str
    host_name: str | None
    period_start: str
    period_end: str
    categories: list[CategorySummary]
    total_snapshots: int


@router.get("/monthly", response_model=MonthlyReportResponse)
def get_monthly_report(
    device_install_id: str = Query(..., min_length=3, max_length=128),
    db: Session = Depends(get_db),
    _user: dict = Depends(require_home_user),
) -> MonthlyReportResponse:
    device = db.execute(
        select(Device).where(Device.device_install_id == device_install_id.strip())
    ).scalar_one_or_none()
    if not device:
        raise HTTPException(status_code=404, detail="device not found")

    now = datetime.now(timezone.utc)
    period_start = now - timedelta(days=30)

    rows = db.execute(
        select(TelemetrySnapshot)
        .where(
            TelemetrySnapshot.device_id == device.id,
            TelemetrySnapshot.received_at >= period_start,
        )
        .order_by(TelemetrySnapshot.received_at.desc())
    ).scalars().all()

    by_cat: dict[str, list[TelemetrySnapshot]] = {}
    for snap in rows:
        by_cat.setdefault(snap.category, []).append(snap)

    categories: list[CategorySummary] = []
    for cat, snaps in sorted(by_cat.items()):
        latest = snaps[0]
        categories.append(
            CategorySummary(
                category=cat,
                count=len(snaps),
                latest_at=latest.received_at.isoformat(),
                latest_summary=latest.summary,
                latest_payload=latest.payload,
            )
        )

    return MonthlyReportResponse(
        device_install_id=device_install_id,
        host_name=device.host_name,
        period_start=period_start.isoformat(),
        period_end=now.isoformat(),
        categories=categories,
        total_snapshots=len(rows),
    )
