"""device_token last_used_at

Revision ID: 20260304_0006
Revises: 20260304_0005
Create Date: 2026-03-04
"""

from alembic import op
import sqlalchemy as sa

revision = "20260304_0006"
down_revision = "20260304_0005"
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.add_column(
        "device_tokens",
        sa.Column("last_used_at", sa.DateTime(timezone=True), nullable=True),
    )


def downgrade() -> None:
    op.drop_column("device_tokens", "last_used_at")
