"""add telemetry snapshots and device host_name/install_id updates

Revision ID: 20260227_0001
Revises: None
Create Date: 2026-02-27 00:00:01
"""

from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects import postgresql


revision = "20260227_0001"
down_revision = None
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.execute("CREATE EXTENSION IF NOT EXISTS pgcrypto")

    with op.batch_alter_table("devices", schema=None) as batch_op:
        batch_op.alter_column(
            "device_install_id",
            existing_type=postgresql.UUID(as_uuid=False),
            type_=sa.String(length=128),
            postgresql_using="device_install_id::text",
            existing_nullable=False,
        )

        if not _column_exists("devices", "host_name"):
            batch_op.add_column(sa.Column("host_name", sa.String(length=255), nullable=True))

    if _column_exists("devices", "hostname"):
        op.execute(sa.text('UPDATE devices SET host_name = COALESCE(host_name, hostname)'))
        with op.batch_alter_table("devices", schema=None) as batch_op:
            batch_op.drop_column("hostname")

    if not _index_exists("ix_devices_host_name"):
        op.create_index("ix_devices_host_name", "devices", ["host_name"], unique=False)
    if not _index_exists("ix_devices_last_seen_at"):
        op.create_index("ix_devices_last_seen_at", "devices", ["last_seen_at"], unique=False)

    op.create_table(
        "telemetry_snapshots",
        sa.Column("id", postgresql.UUID(as_uuid=True), nullable=False, server_default=sa.text("gen_random_uuid()")),
        sa.Column("device_id", postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column("received_at", sa.DateTime(timezone=True), server_default=sa.text("now()"), nullable=False),
        sa.Column("category", sa.String(length=64), nullable=False),
        sa.Column("payload", postgresql.JSONB(astext_type=sa.Text()), nullable=False),
        sa.Column("summary", sa.String(length=512), nullable=True),
        sa.Column("source", sa.String(length=32), nullable=False),
        sa.ForeignKeyConstraint(["device_id"], ["devices.id"], ondelete="CASCADE"),
        sa.PrimaryKeyConstraint("id"),
    )
    if not _index_exists("ix_telemetry_snapshots_device_id"):
        op.create_index("ix_telemetry_snapshots_device_id", "telemetry_snapshots", ["device_id"], unique=False)
    if not _index_exists("ix_telemetry_snapshots_received_at"):
        op.create_index("ix_telemetry_snapshots_received_at", "telemetry_snapshots", ["received_at"], unique=False)
    if not _index_exists("ix_telemetry_snapshots_category"):
        op.create_index("ix_telemetry_snapshots_category", "telemetry_snapshots", ["category"], unique=False)
    if not _index_exists("ix_telemetry_snapshots_device_category_received_desc"):
        op.create_index(
            "ix_telemetry_snapshots_device_category_received_desc",
            "telemetry_snapshots",
            ["device_id", "category", "received_at"],
            unique=False,
        )


def downgrade() -> None:
    op.drop_index("ix_telemetry_snapshots_device_category_received_desc", table_name="telemetry_snapshots")
    op.drop_index("ix_telemetry_snapshots_category", table_name="telemetry_snapshots")
    op.drop_index("ix_telemetry_snapshots_received_at", table_name="telemetry_snapshots")
    op.drop_index("ix_telemetry_snapshots_device_id", table_name="telemetry_snapshots")
    op.drop_table("telemetry_snapshots")

    op.drop_index("ix_devices_last_seen_at", table_name="devices")
    op.drop_index("ix_devices_host_name", table_name="devices")
    with op.batch_alter_table("devices", schema=None) as batch_op:
        if _column_exists("devices", "host_name"):
            batch_op.add_column(sa.Column("hostname", sa.String(), nullable=True))
    if _column_exists("devices", "host_name"):
        op.execute(sa.text('UPDATE devices SET hostname = COALESCE(hostname, host_name)'))
        with op.batch_alter_table("devices", schema=None) as batch_op:
            batch_op.drop_column("host_name")

    with op.batch_alter_table("devices", schema=None) as batch_op:
        batch_op.alter_column(
            "device_install_id",
            existing_type=sa.String(length=128),
            type_=postgresql.UUID(as_uuid=False),
            postgresql_using="device_install_id::uuid",
            existing_nullable=False,
        )


def _column_exists(table_name: str, column_name: str) -> bool:
    bind = op.get_bind()
    result = bind.execute(
        sa.text(
            """
            SELECT 1
            FROM information_schema.columns
            WHERE table_name = :table_name AND column_name = :column_name
            """
        ),
        {"table_name": table_name, "column_name": column_name},
    )
    return result.first() is not None


def _index_exists(index_name: str) -> bool:
    bind = op.get_bind()
    result = bind.execute(
        sa.text("SELECT 1 FROM pg_indexes WHERE indexname = :index_name"),
        {"index_name": index_name},
    )
    return result.first() is not None
