from datetime import datetime, timedelta, timezone

from fastapi import APIRouter, Depends, Header, HTTPException, Query
from sqlalchemy import select
from sqlalchemy.orm import Session

from ..db import get_db
from ..models import License, Plan, Subscription
from ..schemas import LicenseActivateRequest, LicenseActivateResponse, LicenseInfo, LicenseMeResponse, LicenseStatusResponse
from ..security import require_api_key
from ..security_jwt import require_verified_token

router = APIRouter(prefix="/license", tags=["license"])

ALLOWED_TIERS = {"trial", "standard", "professional", "unlimited", "custom"}

# Default feature flags per tier when plan has no feature_flags set
DEFAULT_FEATURES: dict[str, dict[str, bool]] = {
    "trial":        {"auto_fix": False, "reports": False, "priority_support": False},
    "standard":     {"auto_fix": True,  "reports": False, "priority_support": False},
    "professional": {"auto_fix": True,  "reports": True,  "priority_support": True},
    "unlimited":    {"auto_fix": True,  "reports": True,  "priority_support": True},
    "custom":       {"auto_fix": True,  "reports": True,  "priority_support": False},
}


def utcnow() -> datetime:
    return datetime.now(timezone.utc)


def normalize_license_key(value: str) -> str:
    return value.strip().upper().replace(" ", "")


def materialize_expiry(db: Session, license_row: License) -> None:
    if license_row.state == "activated" and license_row.expires_at and license_row.expires_at <= utcnow():
        license_row.state = "expired"
        db.commit()
        db.refresh(license_row)


def _user_has_used_trial(db: Session, user_id: str) -> bool:
    """Check if a Keycloak user has already activated a trial license."""
    existing = db.execute(
        select(License).where(
            License.tier == "trial",
            License.activated_by_user_id == user_id,
            License.state.in_(["activated", "expired"]),
        )
    ).scalars().first()
    return existing is not None


def to_info(license_row: License) -> LicenseInfo:
    tier = license_row.tier if license_row.tier in ALLOWED_TIERS else "trial"
    state = license_row.state if license_row.state in {"issued", "activated", "expired", "revoked"} else "issued"
    return LicenseInfo(
        license_key=license_row.license_key,
        tier=tier,
        state=state,
        duration_days=license_row.duration_days,
        issued_at=license_row.issued_at,
        activated_at=license_row.activated_at,
        expires_at=license_row.expires_at,
        activated_device_install_id=license_row.activated_device_install_id,
        activated_by_user_id=license_row.activated_by_user_id,
    )


@router.post("/activate", response_model=LicenseActivateResponse, dependencies=[Depends(require_api_key)])
def activate_license(payload: LicenseActivateRequest, db: Session = Depends(get_db)):
    key = normalize_license_key(payload.license_key)
    device_install_id = payload.device_install_id.strip()
    user_id = (payload.keycloak_user_id or "").strip() or None

    if not key:
        raise HTTPException(status_code=400, detail="license_key required")
    if not device_install_id:
        raise HTTPException(status_code=400, detail="device_install_id required")

    license_row = db.execute(select(License).where(License.license_key == key)).scalar_one_or_none()
    if not license_row:
        raise HTTPException(status_code=404, detail="license not found")

    materialize_expiry(db, license_row)

    if license_row.state in {"expired", "revoked"}:
        raise HTTPException(status_code=409, detail=f"license is {license_row.state}")

    activated_now = False
    if license_row.state == "issued":
        # Trial-already-used check
        if license_row.tier == "trial" and user_id and _user_has_used_trial(db, user_id):
            raise HTTPException(status_code=409, detail="trial_already_used")

        now = utcnow()
        license_row.activated_at = now
        license_row.activated_device_install_id = device_install_id
        license_row.activated_by_user_id = user_id

        if license_row.duration_days is None:
            license_row.expires_at = None
            license_row.state = "activated"
        else:
            license_row.expires_at = now + timedelta(days=license_row.duration_days)
            license_row.state = "expired" if license_row.expires_at <= now else "activated"

        db.commit()
        db.refresh(license_row)
        activated_now = True
    elif license_row.state == "activated":
        if license_row.activated_device_install_id and license_row.activated_device_install_id != device_install_id:
            raise HTTPException(status_code=409, detail="license already activated on another device")

        if not license_row.activated_device_install_id:
            license_row.activated_device_install_id = device_install_id
            if user_id and not license_row.activated_by_user_id:
                license_row.activated_by_user_id = user_id
            db.commit()
            db.refresh(license_row)

    materialize_expiry(db, license_row)
    return LicenseActivateResponse(ok=True, activated_now=activated_now, license=to_info(license_row))


@router.get("/me", response_model=LicenseMeResponse)
def license_me(
    device_install_id: str | None = Query(default=None),
    license_key: str | None = Query(default=None),
    claims: dict = Depends(require_verified_token),
    db: Session = Depends(get_db),
):
    bearer_sub = str(claims.get("sub") or "").strip()
    if not bearer_sub:
        raise HTTPException(status_code=401, detail="invalid bearer token")

    if not device_install_id and not license_key:
        raise HTTPException(status_code=400, detail="device_install_id or license_key required")

    license_row: License | None = None
    if license_key:
        normalized = normalize_license_key(license_key)
        license_row = db.execute(select(License).where(License.license_key == normalized)).scalar_one_or_none()
    else:
        device_id = (device_install_id or "").strip()
        license_row = db.execute(
            select(License)
            .where(License.activated_device_install_id == device_id)
            .order_by(License.activated_at.desc().nullslast(), License.created_at.desc())
            .limit(1)
        ).scalar_one_or_none()

    if not license_row:
        raise HTTPException(status_code=404, detail="license not found")

    if license_row.activated_by_user_id and license_row.activated_by_user_id != bearer_sub:
        raise HTTPException(status_code=403, detail="license does not belong to this user")

    materialize_expiry(db, license_row)
    return LicenseMeResponse(ok=True, license=to_info(license_row))


@router.get("/status", response_model=LicenseStatusResponse, dependencies=[])
def license_status(
    device_install_id: str | None = Query(default=None),
    license_key: str | None = Query(default=None),
    authorization: str | None = Header(default=None),
    x_api_key: str | None = Header(default=None),
    x_agent_api_key: str | None = Header(default=None),
    db: Session = Depends(get_db),
):
    """
    Returns the current license status including plan details and feature flags.
    Accepts either:
      - Bearer <keycloak_access_token>  (desktop client, home portal)
      - X-Api-Key / X-Agent-Api-Key     (agent service, fallback)
    When a Bearer token is provided the device_install_id must belong to the
    authenticated user (sub claim), preventing cross-user license probing.
    """
    from ..settings import settings as _settings

    bearer_sub: str | None = None

    if authorization and authorization.startswith("Bearer "):
        claims = require_verified_token(authorization)
        bearer_sub = claims.get("sub") or None
        if not bearer_sub:
            raise HTTPException(status_code=401, detail="invalid bearer token")
    else:
        # Fall back to API key auth (agent use-case)
        configured = {k.strip() for k in (_settings.API_KEYS + "," + _settings.AGENT_API_KEYS).split(",") if k.strip()}
        supplied = (x_api_key or x_agent_api_key or "").strip()
        if not configured or not supplied or supplied not in configured:
            raise HTTPException(status_code=401, detail="missing or invalid credentials")

    license_row: License | None = None
    if not device_install_id and not license_key:
        if not bearer_sub:
            raise HTTPException(status_code=400, detail="device_install_id or license_key required")
        # Bearer-only call (e.g. post-checkout polling): look up by Keycloak user ID
        license_row = db.execute(
            select(License)
            .where(License.activated_by_user_id == bearer_sub)
            .order_by(License.activated_at.desc().nullslast(), License.created_at.desc())
            .limit(1)
        ).scalar_one_or_none()
    elif license_key:
        normalized = normalize_license_key(license_key)
        license_row = db.execute(select(License).where(License.license_key == normalized)).scalar_one_or_none()
    else:
        device_id = (device_install_id or "").strip()
        license_row = db.execute(
            select(License)
            .where(License.activated_device_install_id == device_id)
            .order_by(License.activated_at.desc().nullslast(), License.created_at.desc())
            .limit(1)
        ).scalar_one_or_none()

    # When using Bearer auth, verify the license belongs to the authenticated user
    if bearer_sub and license_row and license_row.activated_by_user_id:
        if license_row.activated_by_user_id != bearer_sub:
            raise HTTPException(status_code=403, detail="license does not belong to this user")

    if not license_row:
        return LicenseStatusResponse(
            ok=True,
            plan="none",
            plan_label="Keine Lizenz",
            state="none",
        )

    materialize_expiry(db, license_row)

    # Load plan for feature flags and grace period
    plan = db.execute(select(Plan).where(Plan.id == license_row.tier)).scalar_one_or_none()
    grace_period_days = plan.grace_period_days if plan else 7
    features: dict[str, bool] = {}
    if plan and plan.feature_flags:
        features = {k: bool(v) for k, v in plan.feature_flags.items()}
    else:
        features = DEFAULT_FEATURES.get(license_row.tier, {})

    max_devices: int | None = plan.max_devices if plan else None
    plan_label: str = plan.label if plan else license_row.tier.capitalize()

    now = utcnow()
    state = license_row.state
    grace_period_until: datetime | None = None
    days_remaining: int | None = None

    if state == "expired" and license_row.expires_at:
        grace_until = license_row.expires_at + timedelta(days=grace_period_days)
        if now <= grace_until:
            state = "grace"
            grace_period_until = grace_until

    if license_row.expires_at and state in ("activated", "grace"):
        delta = license_row.expires_at - now
        days_remaining = max(0, delta.days)

    return LicenseStatusResponse(
        ok=True,
        plan=license_row.tier,
        plan_label=plan_label,
        state=state,
        expires_at=license_row.expires_at,
        grace_period_until=grace_period_until,
        days_remaining=days_remaining,
        max_devices=max_devices,
        features=features,
    )
