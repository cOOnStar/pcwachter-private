"""Feature Overrides / Kill-Switch / Rollout Router.

Canonical: /api/v1/console/ui/features/*
Legacy:    /console/ui/features/*  (via _LegacyDeprecationMiddleware in main.py)

Scope values:
  global  – applies to all users/devices (target_id must be NULL)
  plan    – applies to a specific plan (target_id = plan_id)
  user    – applies to a specific Keycloak user (target_id = user_id)
  device  – applies to a specific device UUID (target_id = device_id)
"""

from datetime import datetime, timezone

from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel, Field
from sqlalchemy import select
from sqlalchemy.orm import Session

from ..db import get_db
from ..models import FeatureOverride
from ..security_jwt import require_console_owner, require_console_user

router = APIRouter(prefix="/console", tags=["features"])

VALID_SCOPES = {"global", "plan", "user", "device"}


def utcnow() -> datetime:
    return datetime.now(timezone.utc)


# ---------------------------------------------------------------------------
# Schemas
# ---------------------------------------------------------------------------


class FeatureOverrideIn(BaseModel):
    feature_key: str = Field(min_length=1, max_length=64)
    enabled: bool = True
    rollout_percent: int = Field(default=100, ge=0, le=100)
    scope: str = Field(default="global")
    target_id: str | None = Field(default=None)
    version_min: str | None = None
    platform: str = "all"
    notes: str | None = None


class FeatureOverrideOut(BaseModel):
    id: str
    feature_key: str
    enabled: bool
    rollout_percent: int
    scope: str
    target_id: str | None
    version_min: str | None
    platform: str
    notes: str | None
    updated_at: str


def _override_out(fo: FeatureOverride) -> dict:
    return {
        "id": str(fo.id),
        "feature_key": fo.feature_key,
        "enabled": fo.enabled,
        "rollout_percent": fo.rollout_percent,
        "scope": fo.scope,
        "target_id": fo.target_id,
        "version_min": fo.version_min,
        "platform": fo.platform,
        "notes": fo.notes,
        "updated_at": fo.updated_at.isoformat() if fo.updated_at else utcnow().isoformat(),
    }


def _normalize(payload: FeatureOverrideIn) -> tuple[str, str | None]:
    """Normalize and validate scope/target_id.

    Returns (scope, target_id).
    Raises HTTP 422 on constraint violations.
    """
    scope = (payload.scope or "global").strip().lower()
    if scope not in VALID_SCOPES:
        raise HTTPException(
            status_code=422,
            detail=f"invalid scope '{scope}'; allowed: {', '.join(sorted(VALID_SCOPES))}",
        )

    if scope == "global":
        target_id = None  # always NULL for global
    else:
        if not payload.target_id or not payload.target_id.strip():
            raise HTTPException(
                status_code=422,
                detail=f"target_id is required for scope='{scope}'",
            )
        target_id = payload.target_id.strip()

    return scope, target_id


# ---------------------------------------------------------------------------
# Endpoints
# ---------------------------------------------------------------------------


@router.get("/ui/features/overrides")
def list_feature_overrides(
    db: Session = Depends(get_db),
    _user: dict = Depends(require_console_user),
):
    """List all feature overrides (read: any console role)."""
    rows = db.execute(
        select(FeatureOverride).order_by(FeatureOverride.feature_key, FeatureOverride.scope)
    ).scalars().all()
    return {"items": [_override_out(r) for r in rows], "total": len(rows)}


@router.post("/ui/features/overrides")
def upsert_feature_override(
    payload: FeatureOverrideIn,
    db: Session = Depends(get_db),
    _owner: dict = Depends(require_console_owner),
):
    """Create or update a feature override (admin only).

    Uniqueness key: (feature_key, scope, COALESCE(target_id, '')).
    """
    scope, target_id = _normalize(payload)

    # Find existing row by (feature_key, scope, target_id).
    coalesced = target_id or ""
    existing = db.execute(
        select(FeatureOverride).where(
            FeatureOverride.feature_key == payload.feature_key,
            FeatureOverride.scope == scope,
            # Use COALESCE logic in Python since target_id can be NULL.
        )
    ).scalars().all()

    row: FeatureOverride | None = None
    for r in existing:
        r_target = r.target_id or ""
        if r_target == coalesced:
            row = r
            break

    if row is None:
        row = FeatureOverride(
            feature_key=payload.feature_key,
            scope=scope,
            target_id=target_id,
        )
        db.add(row)

    row.enabled = payload.enabled
    row.rollout_percent = payload.rollout_percent
    row.version_min = payload.version_min
    row.platform = payload.platform or "all"
    row.notes = payload.notes
    row.updated_at = utcnow()
    db.commit()
    db.refresh(row)
    return _override_out(row)


@router.post("/ui/features/{feature_key}/disable")
def disable_feature(
    feature_key: str,
    db: Session = Depends(get_db),
    _owner: dict = Depends(require_console_owner),
):
    """Emergency kill-switch: upsert global override with enabled=False, rollout_percent=0 (admin only)."""
    key = feature_key.strip()
    if not key:
        raise HTTPException(status_code=422, detail="feature_key is required")

    # Find or create the global override row.
    existing = db.execute(
        select(FeatureOverride).where(
            FeatureOverride.feature_key == key,
            FeatureOverride.scope == "global",
            FeatureOverride.target_id.is_(None),
        )
    ).scalar_one_or_none()

    if existing is None:
        existing = FeatureOverride(
            feature_key=key,
            scope="global",
            target_id=None,
        )
        db.add(existing)

    existing.enabled = False
    existing.rollout_percent = 0
    existing.updated_at = utcnow()
    db.commit()
    db.refresh(existing)
    return {"ok": True, "override": _override_out(existing)}
