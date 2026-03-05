"""add subscriptions table and plan feature flags

Revision ID: 20260304_0004
Revises: 20260304_0003
Create Date: 2026-03-04 00:04:00
"""

from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects.postgresql import JSONB, UUID


revision = "20260304_0004"
down_revision = "20260304_0003"
branch_labels = None
depends_on = None


def upgrade() -> None:
    # Add new columns to plans table
    op.add_column("plans", sa.Column("feature_flags", JSONB, nullable=True))
    op.add_column("plans", sa.Column("grace_period_days", sa.Integer(), nullable=False, server_default=sa.text("7")))
    op.add_column("plans", sa.Column("stripe_price_id", sa.String(128), nullable=True))

    # Seed default feature flags for existing plans
    op.execute("""
        UPDATE plans SET feature_flags = '{"auto_fix": false, "reports": false, "priority_support": false}'::jsonb WHERE id = 'trial';
        UPDATE plans SET feature_flags = '{"auto_fix": true, "reports": false, "priority_support": false}'::jsonb WHERE id = 'standard';
        UPDATE plans SET feature_flags = '{"auto_fix": true, "reports": true, "priority_support": true}'::jsonb WHERE id = 'professional';
        UPDATE plans SET feature_flags = '{"auto_fix": true, "reports": true, "priority_support": true}'::jsonb WHERE id = 'unlimited';
        UPDATE plans SET feature_flags = '{"auto_fix": true, "reports": true, "priority_support": false}'::jsonb WHERE id = 'custom';
    """)

    # Set trial grace period to 0 (no grace for free trial)
    op.execute("UPDATE plans SET grace_period_days = 0 WHERE id = 'trial';")

    # Create subscriptions table
    op.create_table(
        "subscriptions",
        sa.Column("id", UUID(as_uuid=True), primary_key=True, nullable=False),
        sa.Column("keycloak_user_id", sa.String(128), nullable=False),
        sa.Column("license_id", UUID(as_uuid=True), sa.ForeignKey("licenses.id"), nullable=True),
        sa.Column("plan_id", sa.String(32), sa.ForeignKey("plans.id"), nullable=True),
        sa.Column("status", sa.String(24), nullable=False, server_default=sa.text("'active'")),
        sa.Column("stripe_customer_id", sa.String(128), nullable=True),
        sa.Column("stripe_subscription_id", sa.String(128), nullable=True),
        sa.Column("stripe_price_id", sa.String(128), nullable=True),
        sa.Column("current_period_start", sa.DateTime(timezone=True), nullable=True),
        sa.Column("current_period_end", sa.DateTime(timezone=True), nullable=True),
        sa.Column("grace_until", sa.DateTime(timezone=True), nullable=True),
        sa.Column("trial_used", sa.Boolean(), nullable=False, server_default=sa.text("false")),
        sa.Column("created_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
        sa.Column("updated_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
        sa.PrimaryKeyConstraint("id"),
    )
    op.create_index("ix_subscriptions_keycloak_user_id", "subscriptions", ["keycloak_user_id"])
    op.create_index("ix_subscriptions_stripe_customer_id", "subscriptions", ["stripe_customer_id"])
    op.create_index("ix_subscriptions_stripe_subscription_id", "subscriptions", ["stripe_subscription_id"])
    op.create_index("ix_subscriptions_status", "subscriptions", ["status"])


def downgrade() -> None:
    op.drop_table("subscriptions")
    op.drop_column("plans", "stripe_price_id")
    op.drop_column("plans", "grace_period_days")
    op.drop_column("plans", "feature_flags")
