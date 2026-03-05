"""subscription allow_self_cancel

Revision ID: 20260305_0014
Revises: 20260305_0013
Create Date: 2026-03-05
"""
from alembic import op
import sqlalchemy as sa

revision = "20260305_0014"
down_revision = "20260305_0013"
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.add_column(
        "subscriptions",
        sa.Column(
            "allow_self_cancel",
            sa.Boolean(),
            nullable=False,
            server_default="false",
        ),
    )


def downgrade() -> None:
    op.drop_column("subscriptions", "allow_self_cancel")
