"""add persistent notifications table

Revision ID: 20260305_0011
Revises: 20260305_0010
Create Date: 2026-03-05
"""

from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects import postgresql


# revision identifiers, used by Alembic.
revision = "20260305_0011"
down_revision = "20260305_0010"
branch_labels = None
depends_on = None


def upgrade() -> None:
    if not _table_exists("notifications"):
        op.create_table(
            "notifications",
            sa.Column("id", postgresql.UUID(as_uuid=True), nullable=False),
            sa.Column("user_id", sa.String(length=128), nullable=False),
            sa.Column("type", sa.String(length=64), nullable=False),
            sa.Column("title", sa.String(length=255), nullable=False),
            sa.Column("body", sa.Text(), nullable=False),
            sa.Column("meta", postgresql.JSONB(astext_type=sa.Text()), nullable=True),
            sa.Column("created_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
            sa.Column("read_at", sa.DateTime(timezone=True), nullable=True),
            sa.PrimaryKeyConstraint("id"),
        )

    if not _index_exists("ix_notifications_user_id"):
        op.create_index("ix_notifications_user_id", "notifications", ["user_id"], unique=False)
    if not _index_exists("ix_notifications_created_at"):
        op.create_index("ix_notifications_created_at", "notifications", ["created_at"], unique=False)
    if not _index_exists("ix_notifications_read_at"):
        op.create_index("ix_notifications_read_at", "notifications", ["read_at"], unique=False)


def downgrade() -> None:
    if not _table_exists("notifications"):
        return

    if _index_exists("ix_notifications_read_at"):
        op.drop_index("ix_notifications_read_at", table_name="notifications")
    if _index_exists("ix_notifications_created_at"):
        op.drop_index("ix_notifications_created_at", table_name="notifications")
    if _index_exists("ix_notifications_user_id"):
        op.drop_index("ix_notifications_user_id", table_name="notifications")
    op.drop_table("notifications")


def _table_exists(table_name: str) -> bool:
    bind = op.get_bind()
    result = bind.execute(
        sa.text("SELECT to_regclass(:name)"),
        {"name": f"public.{table_name}"},
    ).scalar()
    return result is not None


def _index_exists(index_name: str) -> bool:
    bind = op.get_bind()
    result = bind.execute(
        sa.text("SELECT to_regclass(:name)"),
        {"name": f"public.{index_name}"},
    ).scalar()
    return result is not None
