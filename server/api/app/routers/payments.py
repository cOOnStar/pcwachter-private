"""
Stripe payment endpoints.
- POST /payments/create-checkout  → create Stripe Checkout Session
- POST /payments/webhook          → handle Stripe webhook events
- POST /payments/portal           → create Stripe Customer Portal session
"""
import secrets
import string
from datetime import datetime, timedelta, timezone
from typing import Any
from urllib.parse import parse_qsl, urlencode, urlparse, urlunparse

import stripe
from fastapi import APIRouter, Depends, HTTPException, Request
from slowapi import Limiter
from slowapi.util import get_remote_address
from sqlalchemy import select
from sqlalchemy.orm import Session

limiter = Limiter(key_func=get_remote_address)

from ..db import get_db
from ..models import License, Plan, Subscription, WebhookEventV2
from ..schemas import (
    OkResponse,
    StripeCheckoutRequest,
    StripeCheckoutResponse,
    StripePortalRequest,
    StripePortalResponse,
)
from ..security_jwt import require_home_user
from ..settings import settings

router = APIRouter(prefix="/payments", tags=["payments"])


def _ensure_stripe_configured() -> None:
    if not settings.STRIPE_SECRET_KEY:
        raise HTTPException(status_code=503, detail="Stripe not configured")
    stripe.api_key = settings.STRIPE_SECRET_KEY


def utcnow() -> datetime:
    return datetime.now(timezone.utc)


def _generate_license_key() -> str:
    alphabet = string.ascii_uppercase + string.digits
    rng = secrets.SystemRandom()
    return "-".join("".join(rng.choices(alphabet, k=4)) for _ in range(3))


def _get_or_create_stripe_customer(email: str, name: str | None = None) -> str:
    """Find existing Stripe customer by email or create a new one."""
    existing = stripe.Customer.list(email=email, limit=1)
    if existing.data:
        return existing.data[0].id
    customer = stripe.Customer.create(email=email, name=name or email)
    return customer.id


def _append_query_param(url: str, key: str, value: str) -> str:
    parsed = urlparse(url)
    query = parse_qsl(parsed.query, keep_blank_values=True)
    query.append((key, value))
    return urlunparse(parsed._replace(query=urlencode(query)))


@router.post("/create-checkout", response_model=StripeCheckoutResponse)
async def create_checkout(
    payload: StripeCheckoutRequest,
    db: Session = Depends(get_db),
    user: dict = Depends(require_home_user),
):
    """
    Create a Stripe Checkout Session for a given plan.
    Returns the checkout URL the frontend should redirect to.
    """
    _ensure_stripe_configured()

    plan = db.execute(select(Plan).where(Plan.id == payload.plan_id, Plan.is_active == True)).scalar_one_or_none()
    if not plan:
        raise HTTPException(status_code=404, detail="plan not found")
    if not plan.stripe_price_id:
        raise HTTPException(status_code=400, detail="plan has no Stripe price configured")

    email: str = user.get("email", "")
    name: str = user.get("name", "")
    keycloak_user_id: str = user.get("sub", "")

    customer_id = _get_or_create_stripe_customer(email, name)

    session = stripe.checkout.Session.create(
        customer=customer_id,
        mode="subscription" if plan.duration_days and plan.duration_days <= 365 else "payment",
        line_items=[{"price": plan.stripe_price_id, "quantity": 1}],
        success_url=_append_query_param(payload.success_url, "session_id", "{CHECKOUT_SESSION_ID}"),
        cancel_url=payload.cancel_url,
        metadata={
            "keycloak_user_id": keycloak_user_id,
            "plan_id": plan.id,
        },
        allow_promotion_codes=True,
    )

    return StripeCheckoutResponse(ok=True, checkout_url=session.url, session_id=session.id)


@router.post("/portal", response_model=StripePortalResponse)
async def customer_portal(
    payload: StripePortalRequest,
    db: Session = Depends(get_db),
    user: dict = Depends(require_home_user),
):
    """Create a Stripe Customer Portal session for billing management."""
    _ensure_stripe_configured()

    keycloak_user_id: str = user.get("sub", "")
    sub = db.execute(
        select(Subscription)
        .where(Subscription.keycloak_user_id == keycloak_user_id)
        .order_by(Subscription.created_at.desc())
        .limit(1)
    ).scalar_one_or_none()

    if not sub or not sub.stripe_customer_id:
        raise HTTPException(status_code=404, detail="no stripe customer found")

    portal_kwargs: dict = {
        "customer": sub.stripe_customer_id,
        "return_url": payload.return_url,
    }
    if sub.allow_self_cancel and settings.STRIPE_PORTAL_CONFIG_WITH_CANCEL:
        portal_kwargs["configuration"] = settings.STRIPE_PORTAL_CONFIG_WITH_CANCEL
    elif not sub.allow_self_cancel and settings.STRIPE_PORTAL_CONFIG_NO_CANCEL:
        portal_kwargs["configuration"] = settings.STRIPE_PORTAL_CONFIG_NO_CANCEL

    session = stripe.billing_portal.Session.create(**portal_kwargs)
    return StripePortalResponse(ok=True, portal_url=session.url)


@router.post("/webhook", response_model=OkResponse)
@limiter.limit("120/minute")
async def stripe_webhook(request: Request, db: Session = Depends(get_db)):
    """
    Handle Stripe webhook events.
    Validates signature, processes checkout.session.completed and subscription events.
    """
    if not settings.STRIPE_WEBHOOK_SECRET:
        raise HTTPException(status_code=503, detail="Stripe webhook not configured")

    payload_bytes = await request.body()
    sig_header = request.headers.get("stripe-signature", "")

    try:
        event: dict[str, Any] = stripe.Webhook.construct_event(payload_bytes, sig_header, settings.STRIPE_WEBHOOK_SECRET)
    except Exception:
        raise HTTPException(status_code=400, detail="invalid webhook signature")

    event_id: str = event.get("id", "")
    event_type: str = event["type"]
    if not event_id:
        raise HTTPException(status_code=400, detail="missing webhook event id")

    # Idempotency: skip already-processed events
    existing = db.execute(
        select(WebhookEventV2).where(
            WebhookEventV2.source == "stripe",
            WebhookEventV2.event_id == event_id,
        )
    ).scalar_one_or_none()
    if existing:
        return OkResponse()

    now_utc = datetime.now(timezone.utc)
    event_row = WebhookEventV2(
        source="stripe",
        event_id=event_id,
        event_type=event_type,
        payload=event.get("data"),
        received_at=now_utc,
        processed_at=now_utc,
        status="ok",
        error=None,
    )
    db.add(event_row)
    db.flush()

    try:
        if event_type == "checkout.session.completed":
            session = event["data"]["object"]
            _handle_checkout_completed(db, session)

        elif event_type in ("invoice.paid",):
            invoice = event["data"]["object"]
            _handle_invoice_paid(db, invoice)

        elif event_type in ("invoice.payment_failed",):
            invoice = event["data"]["object"]
            _handle_invoice_payment_failed(db, invoice)

        elif event_type in ("customer.subscription.updated",):
            subscription = event["data"]["object"]
            _handle_subscription_updated(db, subscription)

        elif event_type in ("customer.subscription.deleted",):
            subscription = event["data"]["object"]
            _handle_subscription_deleted(db, subscription)

        # Persist webhook idempotency marker even for unhandled event types.
        event_row.status = "ok"
        event_row.error = None
        event_row.processed_at = datetime.now(timezone.utc)
        db.commit()
    except Exception as exc:
        try:
            event_row.status = "failed"
            event_row.error = str(exc)[:4000]
            event_row.processed_at = datetime.now(timezone.utc)
            db.commit()
        except Exception:
            db.rollback()
        raise

    return OkResponse()


# ---------------------------------------------------------------------------
# Webhook helpers
# ---------------------------------------------------------------------------

def _handle_checkout_completed(db: Session, session: dict) -> None:
    keycloak_user_id: str = (session.get("metadata") or {}).get("keycloak_user_id", "")
    plan_id: str = (session.get("metadata") or {}).get("plan_id", "")
    stripe_customer_id: str = session.get("customer", "")
    stripe_subscription_id: str = session.get("subscription", "") or ""

    if not keycloak_user_id or not plan_id:
        return

    plan = db.execute(select(Plan).where(Plan.id == plan_id)).scalar_one_or_none()
    if not plan:
        return

    # Generate a new license key
    for _ in range(10):
        key = _generate_license_key()
        if not db.execute(select(License).where(License.license_key == key)).scalar_one_or_none():
            break

    now = utcnow()
    expires_at = (now + timedelta(days=plan.duration_days)) if plan.duration_days else None

    license_row = License(
        license_key=key,
        tier=plan_id,
        duration_days=plan.duration_days,
        state="activated",
        activated_at=now,
        expires_at=expires_at,
        activated_by_user_id=keycloak_user_id,
    )
    db.add(license_row)
    db.flush()

    sub = db.execute(
        select(Subscription)
        .where(Subscription.keycloak_user_id == keycloak_user_id)
        .order_by(Subscription.created_at.desc())
        .limit(1)
    ).scalar_one_or_none()

    period_end = expires_at or (now + timedelta(days=36500))  # lifetime

    if sub:
        sub.license_id = license_row.id
        sub.plan_id = plan_id
        sub.status = "active"
        sub.stripe_customer_id = stripe_customer_id or sub.stripe_customer_id
        sub.stripe_subscription_id = stripe_subscription_id or sub.stripe_subscription_id
        sub.current_period_start = now
        sub.current_period_end = period_end
    else:
        sub = Subscription(
            keycloak_user_id=keycloak_user_id,
            license_id=license_row.id,
            plan_id=plan_id,
            status="active",
            stripe_customer_id=stripe_customer_id,
            stripe_subscription_id=stripe_subscription_id,
            current_period_start=now,
            current_period_end=period_end,
            trial_used=(plan_id == "trial"),
        )
        db.add(sub)

    db.commit()


def _handle_invoice_paid(db: Session, invoice: dict) -> None:
    stripe_subscription_id: str = invoice.get("subscription", "") or ""
    if not stripe_subscription_id:
        return

    sub = db.execute(
        select(Subscription).where(Subscription.stripe_subscription_id == stripe_subscription_id)
    ).scalar_one_or_none()
    if not sub:
        return

    period_end_ts = invoice.get("lines", {}).get("data", [{}])[0].get("period", {}).get("end")
    if period_end_ts:
        sub.current_period_end = datetime.fromtimestamp(period_end_ts, tz=timezone.utc)

    sub.status = "active"
    sub.grace_until = None

    # Extend the linked license
    if sub.license_id:
        license_row = db.execute(select(License).where(License.id == sub.license_id)).scalar_one_or_none()
        if license_row and sub.current_period_end:
            license_row.expires_at = sub.current_period_end
            license_row.state = "activated"

    db.commit()


def _handle_invoice_payment_failed(db: Session, invoice: dict) -> None:
    stripe_subscription_id: str = invoice.get("subscription", "") or ""
    if not stripe_subscription_id:
        return

    sub = db.execute(
        select(Subscription).where(Subscription.stripe_subscription_id == stripe_subscription_id)
    ).scalar_one_or_none()
    if not sub:
        return

    grace_days = 7
    if sub.plan_id:
        plan = db.execute(select(Plan).where(Plan.id == sub.plan_id)).scalar_one_or_none()
        if plan:
            grace_days = plan.grace_period_days

    sub.status = "grace"
    sub.grace_until = utcnow() + timedelta(days=grace_days)
    db.commit()


def _handle_subscription_updated(db: Session, stripe_sub: dict) -> None:
    """
    Keep status and current_period_end in sync when Stripe emits
    customer.subscription.updated (e.g. after a price migration).
    Does NOT deactivate licenses for active/trialing subscriptions.
    """
    stripe_subscription_id: str = stripe_sub.get("id", "") or ""
    if not stripe_subscription_id:
        return

    sub = db.execute(
        select(Subscription).where(Subscription.stripe_subscription_id == stripe_subscription_id)
    ).scalar_one_or_none()
    if not sub:
        return

    stripe_status: str = stripe_sub.get("status", "") or ""
    period_end_ts = stripe_sub.get("current_period_end")

    # Map Stripe status → internal status (never deactivate active/trialing)
    status_map = {
        "active": "active",
        "trialing": "active",
        "past_due": "grace",
        "canceled": "cancelled",
        "unpaid": "grace",
        "paused": "grace",
    }
    new_status = status_map.get(stripe_status)
    if new_status and new_status not in ("cancelled",):
        sub.status = new_status
    elif new_status == "cancelled":
        # Only update status – deletion event handles license deactivation
        sub.status = "cancelled"

    if period_end_ts:
        sub.current_period_end = datetime.fromtimestamp(period_end_ts, tz=timezone.utc)

    db.commit()


def _handle_subscription_deleted(db: Session, stripe_sub: dict) -> None:
    stripe_subscription_id: str = stripe_sub.get("id", "") or ""
    if not stripe_subscription_id:
        return

    sub = db.execute(
        select(Subscription).where(Subscription.stripe_subscription_id == stripe_subscription_id)
    ).scalar_one_or_none()
    if not sub:
        return

    sub.status = "cancelled"

    if sub.license_id:
        license_row = db.execute(select(License).where(License.id == sub.license_id)).scalar_one_or_none()
        if license_row and license_row.state == "activated":
            license_row.state = "expired"

    db.commit()
