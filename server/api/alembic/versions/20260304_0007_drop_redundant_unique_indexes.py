"""drop redundant unique indexes

Two tables had a SQLAlchemy-generated UNIQUE constraint (via unique=True on the column)
AND an explicit Index(... unique=True) created by a previous migration — resulting in
two indexes on the same column. Drop the redundant explicit ones; the constraint index
created by the UNIQUE column constraint remains.

  webhook_events.stripe_event_id:
    keep  → webhook_events_stripe_event_id_key  (from unique=True)
    drop  → ix_webhook_events_stripe_event_id   (redundant)

  feature_overrides.feature_key:
    keep  → feature_overrides_feature_key_key   (from unique=True)
    drop  → ix_feature_overrides_feature_key    (redundant)

Revision ID: 20260304_0007
Revises: 20260304_0006
Create Date: 2026-03-04
"""

from alembic import op

revision = "20260304_0007"
down_revision = "20260304_0006"
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.drop_index("ix_webhook_events_stripe_event_id", table_name="webhook_events")
    op.drop_index("ix_feature_overrides_feature_key", table_name="feature_overrides")


def downgrade() -> None:
    op.create_index(
        "ix_feature_overrides_feature_key",
        "feature_overrides",
        ["feature_key"],
        unique=True,
    )
    op.create_index(
        "ix_webhook_events_stripe_event_id",
        "webhook_events",
        ["stripe_event_id"],
        unique=True,
    )
