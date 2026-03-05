"""add plans table

Revision ID: 20260304_0003
Revises: 20260302_0002
Create Date: 2026-03-04 00:03:00
"""

from alembic import op
import sqlalchemy as sa


revision = "20260304_0003"
down_revision = "20260302_0002"
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.create_table(
        "plans",
        sa.Column("id", sa.String(length=32), primary_key=True, nullable=False),
        sa.Column("label", sa.String(length=64), nullable=False),
        sa.Column("price_eur", sa.Float(), nullable=True),
        sa.Column("duration_days", sa.Integer(), nullable=True),
        sa.Column("max_devices", sa.Integer(), nullable=True),
        sa.Column("is_active", sa.Boolean(), nullable=False, server_default=sa.text("true")),
        sa.Column("sort_order", sa.Integer(), nullable=False, server_default=sa.text("0")),
        sa.Column("created_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
        sa.Column("updated_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
        sa.PrimaryKeyConstraint("id"),
    )

    op.bulk_insert(
        sa.table(
            "plans",
            sa.column("id", sa.String),
            sa.column("label", sa.String),
            sa.column("price_eur", sa.Float),
            sa.column("duration_days", sa.Integer),
            sa.column("max_devices", sa.Integer),
            sa.column("is_active", sa.Boolean),
            sa.column("sort_order", sa.Integer),
        ),
        [
            {"id": "trial",        "label": "Testversion",  "price_eur": 0.0,  "duration_days": 7,   "max_devices": 1,    "is_active": True, "sort_order": 0},
            {"id": "standard",     "label": "Standard",     "price_eur": 4.99, "duration_days": 30,  "max_devices": 3,    "is_active": True, "sort_order": 1},
            {"id": "professional", "label": "Professional", "price_eur": 49.99,"duration_days": 365, "max_devices": None, "is_active": True, "sort_order": 2},
            {"id": "unlimited",    "label": "Unbegrenzt",   "price_eur": None, "duration_days": None,"max_devices": None, "is_active": True, "sort_order": 3},
            {"id": "custom",       "label": "Custom",       "price_eur": None, "duration_days": None,"max_devices": None, "is_active": True, "sort_order": 4},
        ],
    )


def downgrade() -> None:
    op.drop_table("plans")
