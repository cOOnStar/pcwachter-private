from __future__ import annotations

from datetime import datetime, timezone
import uuid

from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy import func, select, update
from sqlalchemy.orm import Session

from ..db import get_db
from ..models import Notification
from ..security_jwt import require_home_user

router = APIRouter(prefix="/notifications", tags=["notifications"])


def _utcnow() -> datetime:
    return datetime.now(timezone.utc)


def _require_user_sub(user: dict) -> str:
    user_id = str(user.get("sub") or "").strip()
    if not user_id:
        raise HTTPException(status_code=401, detail="user_sub_missing")
    return user_id


@router.get("")
def list_notifications(
    limit: int = Query(default=50, ge=1, le=200),
    offset: int = Query(default=0, ge=0),
    unread_only: bool = Query(default=False),
    db: Session = Depends(get_db),
    user: dict = Depends(require_home_user),
):
    user_id = _require_user_sub(user)
    conditions = [Notification.user_id == user_id]
    if unread_only:
        conditions.append(Notification.read_at.is_(None))

    total = db.execute(
        select(func.count()).select_from(Notification).where(*conditions)
    ).scalar_one()
    rows = db.execute(
        select(Notification)
        .where(*conditions)
        .order_by(Notification.created_at.desc())
        .limit(limit)
        .offset(offset)
    ).scalars().all()

    return {
        "items": [
            {
                "id": str(row.id),
                "type": row.type,
                "title": row.title,
                "body": row.body,
                "meta": row.meta,
                "created_at": row.created_at.isoformat() if row.created_at else None,
                "read_at": row.read_at.isoformat() if row.read_at else None,
            }
            for row in rows
        ],
        "total": int(total),
    }


@router.post("/{notification_id}/read")
def mark_notification_read(
    notification_id: str,
    db: Session = Depends(get_db),
    user: dict = Depends(require_home_user),
):
    user_id = _require_user_sub(user)
    try:
        notification_uuid = uuid.UUID(notification_id.strip())
    except ValueError as exc:
        raise HTTPException(status_code=404, detail="notification_not_found") from exc

    row = db.execute(
        select(Notification).where(
            Notification.id == notification_uuid,
            Notification.user_id == user_id,
        )
    ).scalar_one_or_none()
    if row is None:
        raise HTTPException(status_code=404, detail="notification_not_found")

    if row.read_at is None:
        row.read_at = _utcnow()
        db.commit()
        db.refresh(row)

    return {"ok": True, "id": str(row.id), "read_at": row.read_at.isoformat() if row.read_at else None}


@router.post("/read-all")
def mark_all_notifications_read(
    db: Session = Depends(get_db),
    user: dict = Depends(require_home_user),
):
    user_id = _require_user_sub(user)
    now = _utcnow()
    result = db.execute(
        update(Notification)
        .where(Notification.user_id == user_id, Notification.read_at.is_(None))
        .values(read_at=now)
    )
    db.commit()
    updated = int(result.rowcount or 0)
    return {"ok": True, "updated": updated}
