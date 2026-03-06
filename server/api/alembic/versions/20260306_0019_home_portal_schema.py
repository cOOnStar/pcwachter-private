"""add home portal profile, audit, assignment and rating tables

Revision ID: 20260306_0019
Revises: 20260306_0018
Create Date: 2026-03-06
"""
from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects.postgresql import JSONB, UUID

revision = "20260306_0019"
down_revision = "20260306_0018"
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.add_column("licenses", sa.Column("display_name", sa.String(length=255), nullable=True))
    op.add_column("licenses", sa.Column("max_devices", sa.Integer(), nullable=True))
    op.add_column("licenses", sa.Column("renewal_requested_at", sa.DateTime(timezone=True), nullable=True))
    op.add_column("licenses", sa.Column("owner_user_id", sa.String(length=128), nullable=True))
    op.create_index("ix_licenses_owner_user_id", "licenses", ["owner_user_id"])

    op.add_column("support_ticket_sync_states", sa.Column("portal_category", sa.String(length=128), nullable=True))

    op.create_table(
        "home_user_profiles",
        sa.Column("keycloak_user_id", sa.String(length=128), primary_key=True, nullable=False),
        sa.Column("phone", sa.String(length=64), nullable=True),
        sa.Column("preferred_language", sa.String(length=16), nullable=False, server_default="de"),
        sa.Column(
            "preferred_timezone",
            sa.String(length=64),
            nullable=False,
            server_default="Europe/Berlin",
        ),
        sa.Column(
            "email_notifications_enabled",
            sa.Boolean(),
            nullable=False,
            server_default=sa.text("true"),
        ),
        sa.Column(
            "license_reminders_enabled",
            sa.Boolean(),
            nullable=False,
            server_default=sa.text("true"),
        ),
        sa.Column(
            "support_updates_enabled",
            sa.Boolean(),
            nullable=False,
            server_default=sa.text("true"),
        ),
        sa.Column("deletion_requested_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("deletion_scheduled_for", sa.DateTime(timezone=True), nullable=True),
        sa.Column("created_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
        sa.Column("updated_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
    )

    op.create_table(
        "license_device_assignments",
        sa.Column("id", UUID(as_uuid=True), primary_key=True, nullable=False),
        sa.Column("license_id", UUID(as_uuid=True), sa.ForeignKey("licenses.id", ondelete="CASCADE"), nullable=False),
        sa.Column("device_install_id", sa.String(length=128), nullable=False),
        sa.Column("keycloak_user_id", sa.String(length=128), nullable=False),
        sa.Column("assigned_by_user_name", sa.String(length=255), nullable=True),
        sa.Column("assigned_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
        sa.Column("released_at", sa.DateTime(timezone=True), nullable=True),
    )
    op.create_index(
        "ix_license_device_assignments_license_id",
        "license_device_assignments",
        ["license_id"],
    )
    op.create_index(
        "ix_license_device_assignments_device_install_id",
        "license_device_assignments",
        ["device_install_id"],
    )
    op.create_index(
        "ix_license_device_assignments_keycloak_user_id",
        "license_device_assignments",
        ["keycloak_user_id"],
    )
    op.create_index(
        "ix_license_device_assignments_released_at",
        "license_device_assignments",
        ["released_at"],
    )
    op.create_index(
        "ix_license_device_assignments_active_license",
        "license_device_assignments",
        ["license_id", "released_at"],
    )
    op.create_index(
        "ix_license_device_assignments_active_device",
        "license_device_assignments",
        ["device_install_id", "released_at"],
    )

    op.create_table(
        "license_audit_logs",
        sa.Column("id", UUID(as_uuid=True), primary_key=True, nullable=False),
        sa.Column("license_id", UUID(as_uuid=True), sa.ForeignKey("licenses.id", ondelete="CASCADE"), nullable=False),
        sa.Column("action", sa.String(length=32), nullable=False),
        sa.Column("description", sa.Text(), nullable=False),
        sa.Column("actor_name", sa.String(length=255), nullable=False),
        sa.Column("details", JSONB(), nullable=True),
        sa.Column("created_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
    )
    op.create_index("ix_license_audit_logs_license_id", "license_audit_logs", ["license_id"])
    op.create_index("ix_license_audit_logs_action", "license_audit_logs", ["action"])
    op.create_index("ix_license_audit_logs_created_at", "license_audit_logs", ["created_at"])

    op.create_table(
        "device_history_entries",
        sa.Column("id", UUID(as_uuid=True), primary_key=True, nullable=False),
        sa.Column("device_install_id", sa.String(length=128), nullable=False),
        sa.Column("keycloak_user_id", sa.String(length=128), nullable=True),
        sa.Column("event_type", sa.String(length=32), nullable=False),
        sa.Column("message", sa.Text(), nullable=False),
        sa.Column("meta", JSONB(), nullable=True),
        sa.Column("created_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
    )
    op.create_index("ix_device_history_entries_device_install_id", "device_history_entries", ["device_install_id"])
    op.create_index("ix_device_history_entries_keycloak_user_id", "device_history_entries", ["keycloak_user_id"])
    op.create_index("ix_device_history_entries_event_type", "device_history_entries", ["event_type"])
    op.create_index("ix_device_history_entries_created_at", "device_history_entries", ["created_at"])

    op.create_table(
        "support_ticket_ratings",
        sa.Column("id", UUID(as_uuid=True), primary_key=True, nullable=False),
        sa.Column("keycloak_user_id", sa.String(length=128), nullable=False),
        sa.Column("zammad_ticket_id", sa.Integer(), nullable=False),
        sa.Column("rating", sa.Integer(), nullable=False),
        sa.Column("comment", sa.Text(), nullable=True),
        sa.Column("created_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
        sa.CheckConstraint("rating >= 1 AND rating <= 5", name="ck_support_ticket_ratings_value"),
        sa.UniqueConstraint("keycloak_user_id", "zammad_ticket_id", name="uq_support_ticket_ratings_user_ticket"),
    )
    op.create_index("ix_support_ticket_ratings_keycloak_user_id", "support_ticket_ratings", ["keycloak_user_id"])
    op.create_index("ix_support_ticket_ratings_zammad_ticket_id", "support_ticket_ratings", ["zammad_ticket_id"])

    op.execute(
        """
        UPDATE licenses AS lic
        SET owner_user_id = COALESCE(lic.owner_user_id, lic.activated_by_user_id),
            max_devices = COALESCE(
                lic.max_devices,
                (SELECT p.max_devices FROM plans AS p WHERE p.id = lic.tier),
                1
            ),
            display_name = COALESCE(
                lic.display_name,
                (SELECT p.label FROM plans AS p WHERE p.id = lic.tier),
                CASE
                    WHEN lic.tier = 'professional' THEN 'PC-Waechter Professional'
                    WHEN lic.tier = 'standard' THEN 'PC-Waechter Standard'
                    WHEN lic.tier = 'trial' THEN 'PC-Waechter Trial'
                    ELSE 'PC-Waechter Lizenz'
                END
            )
        """
    )

    op.execute(
        """
        INSERT INTO license_device_assignments (
            id,
            license_id,
            device_install_id,
            keycloak_user_id,
            assigned_by_user_name,
            assigned_at,
            released_at
        )
        SELECT
            gen_random_uuid(),
            id,
            activated_device_install_id,
            COALESCE(owner_user_id, activated_by_user_id, ''),
            'System',
            COALESCE(activated_at, created_at),
            NULL
        FROM licenses
        WHERE activated_device_install_id IS NOT NULL
        """
    )

    op.execute(
        """
        INSERT INTO license_audit_logs (id, license_id, action, description, actor_name, details, created_at)
        SELECT
            gen_random_uuid(),
            id,
            'created',
            'Lizenz im Kundenportal uebernommen',
            'System',
            jsonb_build_object('tier', tier, 'state', state),
            COALESCE(created_at, now())
        FROM licenses
        """
    )


def downgrade() -> None:
    op.drop_index("ix_support_ticket_ratings_zammad_ticket_id", table_name="support_ticket_ratings")
    op.drop_index("ix_support_ticket_ratings_keycloak_user_id", table_name="support_ticket_ratings")
    op.drop_table("support_ticket_ratings")

    op.drop_index("ix_device_history_entries_created_at", table_name="device_history_entries")
    op.drop_index("ix_device_history_entries_event_type", table_name="device_history_entries")
    op.drop_index("ix_device_history_entries_keycloak_user_id", table_name="device_history_entries")
    op.drop_index("ix_device_history_entries_device_install_id", table_name="device_history_entries")
    op.drop_table("device_history_entries")

    op.drop_index("ix_license_audit_logs_created_at", table_name="license_audit_logs")
    op.drop_index("ix_license_audit_logs_action", table_name="license_audit_logs")
    op.drop_index("ix_license_audit_logs_license_id", table_name="license_audit_logs")
    op.drop_table("license_audit_logs")

    op.drop_index("ix_license_device_assignments_active_device", table_name="license_device_assignments")
    op.drop_index("ix_license_device_assignments_active_license", table_name="license_device_assignments")
    op.drop_index("ix_license_device_assignments_released_at", table_name="license_device_assignments")
    op.drop_index("ix_license_device_assignments_keycloak_user_id", table_name="license_device_assignments")
    op.drop_index("ix_license_device_assignments_device_install_id", table_name="license_device_assignments")
    op.drop_index("ix_license_device_assignments_license_id", table_name="license_device_assignments")
    op.drop_table("license_device_assignments")

    op.drop_table("home_user_profiles")

    op.drop_column("support_ticket_sync_states", "portal_category")

    op.drop_index("ix_licenses_owner_user_id", table_name="licenses")
    op.drop_column("licenses", "owner_user_id")
    op.drop_column("licenses", "renewal_requested_at")
    op.drop_column("licenses", "max_devices")
    op.drop_column("licenses", "display_name")
