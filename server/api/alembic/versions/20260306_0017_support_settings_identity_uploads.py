"""support settings, support identity links, and filesystem attachment metadata

Revision ID: 20260306_0017
Revises: 20260305_0016
Create Date: 2026-03-06
"""
from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects.postgresql import JSONB, UUID

revision = "20260306_0017"
down_revision = "20260305_0016"
branch_labels = None
depends_on = None


def upgrade() -> None:
    bind = op.get_bind()
    inspector = sa.inspect(bind)
    existing_tables = set(inspector.get_table_names())

    if "support_portal_settings" not in existing_tables:
        op.create_table(
            "support_portal_settings",
            sa.Column("id", sa.Integer(), primary_key=True, nullable=False),
            sa.Column(
                "allow_customer_group_selection",
                sa.Boolean(),
                nullable=False,
                server_default=sa.text("false"),
            ),
            sa.Column("customer_visible_group_ids", JSONB, nullable=False, server_default="[]"),
            sa.Column("default_group_id", sa.Integer(), nullable=True),
            sa.Column("default_priority_id", sa.Integer(), nullable=True),
            sa.Column("uploads_enabled", sa.Boolean(), nullable=False, server_default=sa.text("true")),
            sa.Column("uploads_max_bytes", sa.Integer(), nullable=False, server_default="5242880"),
            sa.Column("maintenance_mode", sa.Boolean(), nullable=False, server_default=sa.text("false")),
            sa.Column("maintenance_message", sa.Text(), nullable=True),
            sa.Column("created_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
            sa.Column("updated_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
        )

    if "support_identity_links" not in existing_tables:
        op.create_table(
            "support_identity_links",
            sa.Column("id", UUID(as_uuid=True), primary_key=True, nullable=False),
            sa.Column("keycloak_user_id", sa.String(128), nullable=False),
            sa.Column("zammad_user_id", sa.Integer(), nullable=False),
            sa.Column("last_synced_email", sa.String(254), nullable=True),
            sa.Column("created_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
            sa.Column("updated_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
            sa.UniqueConstraint("keycloak_user_id", name="uq_support_identity_links_keycloak_user_id"),
            sa.UniqueConstraint("zammad_user_id", name="uq_support_identity_links_zammad_user_id"),
        )
    support_identity_indexes = {
        index["name"] for index in inspector.get_indexes("support_identity_links")
    } if "support_identity_links" in set(sa.inspect(bind).get_table_names()) else set()
    if "ix_support_identity_links_keycloak_user_id" not in support_identity_indexes:
        op.create_index("ix_support_identity_links_keycloak_user_id", "support_identity_links", ["keycloak_user_id"])
    if "ix_support_identity_links_zammad_user_id" not in support_identity_indexes:
        op.create_index("ix_support_identity_links_zammad_user_id", "support_identity_links", ["zammad_user_id"])

    if "support_attachments" not in existing_tables:
        op.create_table(
            "support_attachments",
            sa.Column("id", UUID(as_uuid=True), primary_key=True, nullable=False),
            sa.Column("keycloak_user_id", sa.String(128), nullable=False),
            sa.Column("filename", sa.String(255), nullable=False),
            sa.Column("mime_type", sa.String(255), nullable=False),
            sa.Column("size_bytes", sa.Integer(), nullable=False),
            sa.Column("sha256", sa.String(64), nullable=False),
            sa.Column("storage_path", sa.Text(), nullable=False),
            sa.Column("zammad_ticket_id", sa.Integer(), nullable=True),
            sa.Column("zammad_article_id", sa.Integer(), nullable=True),
            sa.Column("consumed_at", sa.DateTime(timezone=True), nullable=True),
            sa.Column("created_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
        )
    support_attachment_indexes = {
        index["name"] for index in inspector.get_indexes("support_attachments")
    } if "support_attachments" in set(sa.inspect(bind).get_table_names()) else set()
    if "ix_support_attachments_keycloak_user_id" not in support_attachment_indexes:
        op.create_index("ix_support_attachments_keycloak_user_id", "support_attachments", ["keycloak_user_id"])
    if "ix_support_attachments_zammad_ticket_id" not in support_attachment_indexes:
        op.create_index("ix_support_attachments_zammad_ticket_id", "support_attachments", ["zammad_ticket_id"])
    if "ix_support_attachments_zammad_article_id" not in support_attachment_indexes:
        op.create_index("ix_support_attachments_zammad_article_id", "support_attachments", ["zammad_article_id"])
    if "ix_support_attachments_consumed_at" not in support_attachment_indexes:
        op.create_index("ix_support_attachments_consumed_at", "support_attachments", ["consumed_at"])

    op.execute(
        sa.text(
            """
            INSERT INTO support_portal_settings (
                id,
                allow_customer_group_selection,
                customer_visible_group_ids,
                default_group_id,
                default_priority_id,
                uploads_enabled,
                uploads_max_bytes,
                maintenance_mode,
                maintenance_message
            )
            VALUES (
                1,
                false,
                '[]'::jsonb,
                NULL,
                2,
                true,
                5242880,
                false,
                NULL
            )
            ON CONFLICT (id) DO NOTHING
            """
        )
    )


def downgrade() -> None:
    op.drop_index("ix_support_attachments_consumed_at", table_name="support_attachments")
    op.drop_index("ix_support_attachments_zammad_article_id", table_name="support_attachments")
    op.drop_index("ix_support_attachments_zammad_ticket_id", table_name="support_attachments")
    op.drop_index("ix_support_attachments_keycloak_user_id", table_name="support_attachments")
    op.drop_table("support_attachments")
    op.drop_index("ix_support_identity_links_zammad_user_id", table_name="support_identity_links")
    op.drop_index("ix_support_identity_links_keycloak_user_id", table_name="support_identity_links")
    op.drop_table("support_identity_links")
    op.drop_table("support_portal_settings")
