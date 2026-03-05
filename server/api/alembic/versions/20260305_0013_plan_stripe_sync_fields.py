"""add stripe sync fields to plans

Revision ID: 20260305_0013
Revises: 20260305_0012
Create Date: 2026-03-05
"""

from alembic import op
import sqlalchemy as sa


revision = "20260305_0013"
down_revision = "20260305_0012"
branch_labels = None
depends_on = None


def upgrade() -> None:
    bind = op.get_bind()

    def _col_exists(table: str, col: str) -> bool:
        result = bind.execute(
            sa.text(
                "SELECT 1 FROM information_schema.columns "
                "WHERE table_schema='public' AND table_name=:t AND column_name=:c"
            ),
            {"t": table, "c": col},
        ).fetchone()
        return result is not None

    if not _col_exists("plans", "stripe_product_id"):
        op.add_column("plans", sa.Column("stripe_product_id", sa.String(128), nullable=True))

    if not _col_exists("plans", "amount_cents"):
        op.add_column("plans", sa.Column("amount_cents", sa.Integer(), nullable=True))

    if not _col_exists("plans", "currency"):
        op.add_column(
            "plans",
            sa.Column("currency", sa.String(8), nullable=False, server_default="eur"),
        )

    if not _col_exists("plans", "price_version"):
        op.add_column(
            "plans",
            sa.Column("price_version", sa.Integer(), nullable=False, server_default="1"),
        )


def downgrade() -> None:
    bind = op.get_bind()

    def _col_exists(table: str, col: str) -> bool:
        result = bind.execute(
            sa.text(
                "SELECT 1 FROM information_schema.columns "
                "WHERE table_schema='public' AND table_name=:t AND column_name=:c"
            ),
            {"t": table, "c": col},
        ).fetchone()
        return result is not None

    for col in ("price_version", "currency", "amount_cents", "stripe_product_id"):
        if _col_exists("plans", col):
            op.drop_column("plans", col)
