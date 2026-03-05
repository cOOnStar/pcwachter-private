"""add licenses table

Revision ID: 20260302_0002
Revises: 20260227_0001
Create Date: 2026-03-02 00:02:00
"""

from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects import postgresql


revision = "20260302_0002"
down_revision = "20260227_0001"
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.execute("CREATE EXTENSION IF NOT EXISTS pgcrypto")

    op.create_table(
        "licenses",
        sa.Column("id", postgresql.UUID(as_uuid=True), nullable=False, server_default=sa.text("gen_random_uuid()")),
        sa.Column("license_key", sa.String(length=128), nullable=False),
        sa.Column("tier", sa.String(length=32), nullable=False),
        sa.Column("duration_days", sa.Integer(), nullable=True),
        sa.Column("state", sa.String(length=24), nullable=False, server_default=sa.text("'issued'")),
        sa.Column("issued_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
        sa.Column("activated_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("expires_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("activated_device_install_id", sa.String(length=128), nullable=True),
        sa.Column("activated_by_user_id", sa.String(length=128), nullable=True),
        sa.Column("created_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
        sa.Column("updated_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
        sa.PrimaryKeyConstraint("id"),
        sa.UniqueConstraint("license_key", name="uq_licenses_license_key"),
    )

    op.create_index("ix_licenses_license_key", "licenses", ["license_key"], unique=True)
    op.create_index("ix_licenses_state", "licenses", ["state"], unique=False)
    op.create_index("ix_licenses_expires_at", "licenses", ["expires_at"], unique=False)
    op.create_index(
        "ix_licenses_activated_device_install_id",
        "licenses",
        ["activated_device_install_id"],
        unique=False,
    )


def downgrade() -> None:
    op.drop_index("ix_licenses_activated_device_install_id", table_name="licenses")
    op.drop_index("ix_licenses_expires_at", table_name="licenses")
    op.drop_index("ix_licenses_state", table_name="licenses")
    op.drop_index("ix_licenses_license_key", table_name="licenses")
    op.drop_table("licenses")
