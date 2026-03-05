"""feature_override scope+target_id + device.blocked

Revision ID: 20260305_0008
Revises: 20260304_0007
Create Date: 2026-03-05

Changes:
- feature_overrides: add scope (NOT NULL DEFAULT 'global'), target_id (NULL)
- feature_overrides: drop old unique constraint on feature_key alone
- feature_overrides: add composite unique index (feature_key, scope, COALESCE(target_id,''))
- feature_overrides: add index (scope, target_id)
- feature_overrides: add check constraint scope/target_id consistency
- devices: add blocked BOOLEAN NOT NULL DEFAULT false
"""

from alembic import op
import sqlalchemy as sa


revision = "20260305_0008"
down_revision = "20260304_0007"
branch_labels = None
depends_on = None


def upgrade() -> None:
    # ------------------------------------------------------------------
    # feature_overrides: add scope + target_id columns
    # ------------------------------------------------------------------
    op.add_column(
        "feature_overrides",
        sa.Column("scope", sa.String(32), nullable=False, server_default=sa.text("'global'")),
    )
    op.add_column(
        "feature_overrides",
        sa.Column("target_id", sa.String(128), nullable=True),
    )

    # Drop the old simple unique constraint on feature_key alone.
    # The constraint was named feature_overrides_feature_key_key (auto-named by PG).
    op.drop_constraint("feature_overrides_feature_key_key", "feature_overrides", type_="unique")

    # Composite functional unique index: normalise NULL target_id to '' for uniqueness.
    op.execute(
        "CREATE UNIQUE INDEX uq_fo_key_scope_target "
        "ON feature_overrides (feature_key, scope, COALESCE(target_id, ''))"
    )

    # Index for scope/target lookups.
    op.create_index("ix_fo_scope_target", "feature_overrides", ["scope", "target_id"])

    # Check constraint: global → target_id NULL; non-global → target_id NOT NULL.
    op.create_check_constraint(
        "ck_feature_override_scope_target",
        "feature_overrides",
        "(scope = 'global' AND target_id IS NULL) OR (scope != 'global' AND target_id IS NOT NULL)",
    )

    # ------------------------------------------------------------------
    # devices: add blocked flag
    # ------------------------------------------------------------------
    op.add_column(
        "devices",
        sa.Column("blocked", sa.Boolean(), nullable=False, server_default=sa.text("false")),
    )


def downgrade() -> None:
    # devices
    op.drop_column("devices", "blocked")

    # feature_overrides – reverse all changes
    op.drop_constraint("ck_feature_override_scope_target", "feature_overrides", type_="check")
    op.drop_index("ix_fo_scope_target", table_name="feature_overrides")
    op.execute("DROP INDEX IF EXISTS uq_fo_key_scope_target")
    op.drop_column("feature_overrides", "target_id")
    op.drop_column("feature_overrides", "scope")

    # Restore the original simple unique constraint.
    op.create_unique_constraint("feature_overrides_feature_key_key", "feature_overrides", ["feature_key"])
