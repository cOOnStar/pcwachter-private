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
    with op.batch_alter_table("devices") as batch:
        batch.add_column(sa.Column("desktop_version", sa.String(length=64), nullable=True))
        batch.add_column(sa.Column("updater_version", sa.String(length=64), nullable=True))
        batch.add_column(sa.Column("update_channel", sa.String(length=32), nullable=True))


def downgrade() -> None:
    with op.batch_alter_table("devices") as batch:
        batch.drop_column("update_channel")
        batch.drop_column("updater_version")
        batch.drop_column("desktop_version")
