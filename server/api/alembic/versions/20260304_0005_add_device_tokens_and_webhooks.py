"""add device_tokens, webhook_events, feature_overrides

Revision ID: 20260304_0005
Revises: 20260304_0004
Create Date: 2026-03-04 00:05:00
"""

from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects.postgresql import JSONB, UUID


revision = "20260304_0005"
down_revision = "20260304_0004"
branch_labels = None
depends_on = None


def upgrade() -> None:
    # -------------------------------------------------------
    # device_tokens – kurzlebige/langlebige Agent-Auth-Token
    # -------------------------------------------------------
    op.create_table(
        "device_tokens",
        sa.Column("id", UUID(as_uuid=True), primary_key=True, nullable=False),
        sa.Column(
            "device_install_id",
            sa.String(128),
            sa.ForeignKey("devices.device_install_id", ondelete="CASCADE"),
            nullable=False,
        ),
        sa.Column("token_hash", sa.String(128), nullable=False, unique=True),
        sa.Column("expires_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column(
            "created_at",
            sa.DateTime(timezone=True),
            nullable=False,
            server_default=sa.text("now()"),
        ),
        sa.Column("revoked_at", sa.DateTime(timezone=True), nullable=True),
        sa.PrimaryKeyConstraint("id"),
    )
    op.create_index(
        "ix_device_tokens_device_install_id",
        "device_tokens",
        ["device_install_id"],
    )
    op.create_index(
        "ix_device_tokens_token_hash",
        "device_tokens",
        ["token_hash"],
    )

    # -------------------------------------------------------
    # webhook_events – Stripe-Webhook-Idempotenz
    # -------------------------------------------------------
    op.create_table(
        "webhook_events",
        sa.Column("id", UUID(as_uuid=True), primary_key=True, nullable=False),
        sa.Column("event_type", sa.String(128), nullable=False),
        sa.Column("stripe_event_id", sa.String(128), nullable=False, unique=True),
        sa.Column("payload", JSONB, nullable=True),
        sa.Column("processed_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column(
            "created_at",
            sa.DateTime(timezone=True),
            nullable=False,
            server_default=sa.text("now()"),
        ),
        sa.PrimaryKeyConstraint("id"),
    )
    op.create_index(
        "ix_webhook_events_stripe_event_id",
        "webhook_events",
        ["stripe_event_id"],
        unique=True,
    )
    op.create_index(
        "ix_webhook_events_event_type",
        "webhook_events",
        ["event_type"],
    )

    # -------------------------------------------------------
    # feature_overrides – Kill-Switch / stufenweiser Rollout
    # -------------------------------------------------------
    op.create_table(
        "feature_overrides",
        sa.Column("id", UUID(as_uuid=True), primary_key=True, nullable=False),
        sa.Column("feature_key", sa.String(64), nullable=False, unique=True),
        sa.Column(
            "enabled",
            sa.Boolean(),
            nullable=False,
            server_default=sa.text("true"),
        ),
        sa.Column(
            "rollout_pct",
            sa.Integer(),
            nullable=False,
            server_default=sa.text("100"),
        ),
        sa.Column("version_min", sa.String(32), nullable=True),
        sa.Column(
            "platform",
            sa.String(32),
            nullable=False,
            server_default=sa.text("'all'"),
        ),
        sa.Column("notes", sa.Text(), nullable=True),
        sa.Column(
            "updated_at",
            sa.DateTime(timezone=True),
            nullable=False,
            server_default=sa.text("now()"),
        ),
        sa.PrimaryKeyConstraint("id"),
    )
    op.create_index(
        "ix_feature_overrides_feature_key",
        "feature_overrides",
        ["feature_key"],
        unique=True,
    )


def downgrade() -> None:
    op.drop_table("feature_overrides")
    op.drop_table("webhook_events")
    op.drop_table("device_tokens")
