"""rename rollout_pct to rollout_percent

Revision ID: 20260305_0009
Revises: 20260305_0008
Create Date: 2026-03-05
"""
from alembic import op

revision = "20260305_0009"
down_revision = "20260305_0008"
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.alter_column("feature_overrides", "rollout_pct", new_column_name="rollout_percent")


def downgrade() -> None:
    op.alter_column("feature_overrides", "rollout_percent", new_column_name="rollout_pct")
