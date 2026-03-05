"""add update manifests and webhook events v2

Revision ID: 20260305_0015
Revises: 20260305_0014
Create Date: 2026-03-05
"""

from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects.postgresql import JSONB, UUID


revision = "20260305_0015"
down_revision = "20260305_0014"
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.create_table(
        "update_manifests",
        sa.Column("id", UUID(as_uuid=True), primary_key=True, nullable=False),
        sa.Column("component", sa.String(length=32), nullable=False),
        sa.Column("channel", sa.String(length=32), nullable=False),
        sa.Column("latest_version", sa.String(length=64), nullable=False),
        sa.Column("min_supported_version", sa.String(length=64), nullable=False),
        sa.Column("mandatory", sa.Boolean(), nullable=False, server_default=sa.text("false")),
        sa.Column("download_url", sa.Text(), nullable=False),
        sa.Column("sha256", sa.String(length=128), nullable=False),
        sa.Column("changelog", sa.Text(), nullable=True),
        sa.Column("released_at", sa.DateTime(timezone=True), nullable=False),
        sa.Column("created_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
        sa.Column("updated_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
        sa.UniqueConstraint("component", "channel", name="uq_update_manifests_component_channel"),
        sa.CheckConstraint("component IN ('desktop', 'agent', 'updater')", name="ck_update_manifests_component"),
        sa.CheckConstraint("channel IN ('stable', 'beta', 'internal')", name="ck_update_manifests_channel"),
    )

    op.create_table(
        "webhook_events_v2",
        sa.Column("id", UUID(as_uuid=True), primary_key=True, nullable=False),
        sa.Column("source", sa.String(length=32), nullable=False),
        sa.Column("event_id", sa.String(length=255), nullable=False),
        sa.Column("event_type", sa.String(length=128), nullable=False),
        sa.Column("payload", JSONB, nullable=True),
        sa.Column("received_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
        sa.Column("processed_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("status", sa.String(length=16), nullable=False, server_default=sa.text("'ok'")),
        sa.Column("error", sa.Text(), nullable=True),
        sa.UniqueConstraint("source", "event_id", name="uq_webhook_events_v2_source_event_id"),
        sa.CheckConstraint("source IN ('stripe', 'zammad')", name="ck_webhook_events_v2_source"),
        sa.CheckConstraint("status IN ('ok', 'failed')", name="ck_webhook_events_v2_status"),
    )
    op.create_index(
        "ix_webhook_events_v2_source_type_received",
        "webhook_events_v2",
        ["source", "event_type", "received_at"],
    )


def downgrade() -> None:
    op.drop_index("ix_webhook_events_v2_source_type_received", table_name="webhook_events_v2")
    op.drop_table("webhook_events_v2")
    op.drop_table("update_manifests")
