"""add support ticket sync state snapshots

Revision ID: 20260306_0018
Revises: 20260306_0017
Create Date: 2026-03-06
"""
from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects.postgresql import UUID

revision = "20260306_0018"
down_revision = "20260306_0017"
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.create_table(
        "support_ticket_sync_states",
        sa.Column("id", UUID(as_uuid=True), primary_key=True, nullable=False),
        sa.Column("keycloak_user_id", sa.String(128), nullable=False),
        sa.Column("zammad_ticket_id", sa.Integer(), nullable=False),
        sa.Column("ticket_number", sa.String(64), nullable=True),
        sa.Column("ticket_title", sa.String(255), nullable=True),
        sa.Column("last_state", sa.String(64), nullable=True),
        sa.Column("last_ticket_updated_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("last_public_agent_article_id", sa.Integer(), nullable=True),
        sa.Column("last_public_agent_article_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("last_contact_agent_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("last_contact_customer_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("created_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
        sa.Column("updated_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
        sa.UniqueConstraint("keycloak_user_id", "zammad_ticket_id", name="uq_support_ticket_sync_user_ticket"),
    )
    op.create_index(
        "ix_support_ticket_sync_states_keycloak_user_id",
        "support_ticket_sync_states",
        ["keycloak_user_id"],
    )
    op.create_index(
        "ix_support_ticket_sync_states_zammad_ticket_id",
        "support_ticket_sync_states",
        ["zammad_ticket_id"],
    )


def downgrade() -> None:
    op.drop_index("ix_support_ticket_sync_states_zammad_ticket_id", table_name="support_ticket_sync_states")
    op.drop_index("ix_support_ticket_sync_states_keycloak_user_id", table_name="support_ticket_sync_states")
    op.drop_table("support_ticket_sync_states")
