"""
Stripe service layer – DB is the single source of truth for pricing.

Provides three public functions:
  ensure_product_for_plan   – creates Stripe Product if missing, persists product_id
  create_new_price_for_plan – creates a new Stripe Price, deactivates old one,
                              bumps price_version and updates stripe_price_id in DB
  migrate_subscriptions_to_price – updates all active Stripe Subscriptions
                                    to the new Price (proration_behavior="none")
"""
from __future__ import annotations

import logging
import time
from dataclasses import dataclass, field
from typing import TYPE_CHECKING

import stripe
from sqlalchemy import select
from sqlalchemy.orm import Session

from ..models import Plan, Subscription
from ..settings import settings

if TYPE_CHECKING:
    pass

logger = logging.getLogger("pcw.stripe_service")

_ACTIVE_STATUSES = {"active", "trialing", "past_due"}
_BATCH_SIZE = 25  # Stripe subs to migrate per batch


def _init_stripe() -> None:
    if not settings.STRIPE_SECRET_KEY:
        raise RuntimeError("STRIPE_SECRET_KEY not configured")
    stripe.api_key = settings.STRIPE_SECRET_KEY


def _interval_for_plan(plan: Plan) -> str:
    """Derive Stripe billing interval from plan.duration_days."""
    if plan.duration_days == 30:
        return "month"
    if plan.duration_days == 365:
        return "year"
    raise ValueError(
        f"Plan '{plan.id}' has duration_days={plan.duration_days!r}; "
        "only 30 (month) and 365 (year) are supported for recurring Stripe prices."
    )


def ensure_product_for_plan(plan: Plan, db: Session) -> str:
    """Return the Stripe Product ID for the plan, creating it if necessary."""
    _init_stripe()

    if plan.stripe_product_id:
        return plan.stripe_product_id

    product = stripe.Product.create(
        name=plan.label,
        metadata={"plan_id": plan.id},
    )
    plan.stripe_product_id = product.id
    db.commit()
    logger.info("Created Stripe Product %s for plan %s", product.id, plan.id)
    return product.id


def create_new_price_for_plan(plan: Plan, db: Session) -> str:
    """
    Create a new Stripe Price for the plan using plan.amount_cents.
    Deactivates the previous price (if any).
    Updates plan.stripe_price_id and increments plan.price_version in DB.
    Returns the new stripe price_id.
    """
    _init_stripe()

    if plan.amount_cents is None or plan.amount_cents <= 0:
        raise ValueError(
            f"Plan '{plan.id}' has no valid amount_cents ({plan.amount_cents!r}). "
            "Set amount_cents before publishing a price."
        )

    product_id = ensure_product_for_plan(plan, db)
    interval = _interval_for_plan(plan)
    new_version = (plan.price_version or 1) + 1
    idempotency_key = f"plan:{plan.id}:price_version:{new_version}"

    new_price = stripe.Price.create(
        product=product_id,
        unit_amount=plan.amount_cents,
        currency=plan.currency or "eur",
        recurring={"interval": interval},
        metadata={"plan_id": plan.id, "plan_version": str(new_version)},
        idempotency_key=idempotency_key,
    )

    old_price_id = plan.stripe_price_id
    if old_price_id and old_price_id != new_price.id:
        try:
            stripe.Price.modify(old_price_id, active=False)
            logger.info("Deactivated old Stripe Price %s for plan %s", old_price_id, plan.id)
        except stripe.StripeError as exc:
            logger.warning("Could not deactivate old price %s: %s", old_price_id, exc)

    plan.stripe_price_id = new_price.id
    plan.price_version = new_version
    db.commit()
    logger.info(
        "Created new Stripe Price %s (v%d) for plan %s",
        new_price.id, new_version, plan.id,
    )
    return new_price.id


@dataclass
class MigrationSummary:
    migrated_count: int = 0
    failed_count: int = 0
    failed_subscription_ids: list[str] = field(default_factory=list)
    took_ms: int = 0


def migrate_subscriptions_to_price(
    plan: Plan,
    new_price_id: str,
    old_price_id: str | None,
    db: Session,
) -> MigrationSummary:
    """
    For every active Stripe Subscription on this plan, update the subscription
    item to new_price_id with proration_behavior="none" (Mode A).

    Identifies the correct subscription item by:
      1. price.id == old_price_id (preferred)
      2. price.product == plan.stripe_product_id (fallback)
    """
    _init_stripe()

    summary = MigrationSummary()
    t0 = time.monotonic()

    subs = (
        db.execute(
            select(Subscription).where(
                Subscription.plan_id == plan.id,
                Subscription.stripe_subscription_id.isnot(None),
                Subscription.status.in_(_ACTIVE_STATUSES),
            )
        )
        .scalars()
        .all()
    )

    for i in range(0, len(subs), _BATCH_SIZE):
        batch = subs[i : i + _BATCH_SIZE]
        for sub in batch:
            stripe_sub_id: str = sub.stripe_subscription_id  # type: ignore[assignment]
            try:
                stripe_sub = stripe.Subscription.retrieve(
                    stripe_sub_id,
                    expand=["items.data.price"],
                )
                item_id = _find_subscription_item(
                    stripe_sub, old_price_id, plan.stripe_product_id
                )
                if not item_id:
                    logger.warning(
                        "No matching subscription item in %s for plan %s – skipping",
                        stripe_sub_id, plan.id,
                    )
                    summary.failed_count += 1
                    if len(summary.failed_subscription_ids) < 50:
                        summary.failed_subscription_ids.append(stripe_sub_id)
                    continue

                idem_key = f"sub:{stripe_sub_id}:plan:{plan.id}:to_price:{new_price_id}"
                stripe.Subscription.modify(
                    stripe_sub_id,
                    items=[{"id": item_id, "price": new_price_id}],
                    proration_behavior="none",
                    idempotency_key=idem_key,
                )
                summary.migrated_count += 1
                logger.info("Migrated subscription %s to price %s", stripe_sub_id, new_price_id)

            except stripe.StripeError as exc:
                logger.error("Failed to migrate subscription %s: %s", stripe_sub_id, exc)
                summary.failed_count += 1
                if len(summary.failed_subscription_ids) < 50:
                    summary.failed_subscription_ids.append(stripe_sub_id)

    summary.took_ms = int((time.monotonic() - t0) * 1000)
    return summary


def _find_subscription_item(
    stripe_sub: stripe.Subscription,
    old_price_id: str | None,
    product_id: str | None,
) -> str | None:
    """Return the Stripe subscription item ID that matches old_price_id or product_id."""
    items: list[dict] = getattr(stripe_sub, "items", {}).get("data", [])

    # Primary: exact price match
    if old_price_id:
        for item in items:
            if item.get("price", {}).get("id") == old_price_id:
                return item["id"]

    # Fallback: product match
    if product_id:
        matches = [
            item for item in items
            if item.get("price", {}).get("product") == product_id
        ]
        if len(matches) == 1:
            return matches[0]["id"]
        if len(matches) > 1:
            logger.warning(
                "Multiple items match product %s in subscription %s; using first",
                product_id, stripe_sub.id,
            )
            return matches[0]["id"]

    return None
