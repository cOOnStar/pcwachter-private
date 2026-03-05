"""add desktop/updater versions and update_channel to devices

Revision ID: 20260305_0010
Revises: 20260305_0009
Create Date: 2026-03-05

"""
from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision = "20260305_0010"
down_revision = "20260305_0009"
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.add_column("devices", sa.Column("desktop_version", sa.String(length=64), nullable=True))
    op.add_column("devices", sa.Column("updater_version", sa.String(length=64), nullable=True))
    op.add_column("devices", sa.Column("update_channel", sa.String(length=32), nullable=True))


def downgrade() -> None:
    op.drop_column("devices", "update_channel")
    op.drop_column("devices", "updater_version")
    op.drop_column("devices", "desktop_version")
