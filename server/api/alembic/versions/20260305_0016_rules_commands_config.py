"""rules_catalog, rule_findings, device_commands, client_config

Revision ID: 20260305_0016
Revises: 20260305_0015
Create Date: 2026-03-05
"""
from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects.postgresql import JSONB, UUID

revision = "20260305_0016"
down_revision = "20260305_0015"
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.create_table(
        "rules_catalog",
        sa.Column("id", sa.String(64), primary_key=True, nullable=False),
        sa.Column("name", sa.String(128), nullable=False),
        sa.Column("category", sa.String(32), nullable=False),
        sa.Column("severity", sa.String(16), nullable=False, server_default="warning"),
        sa.Column("enabled", sa.Boolean(), nullable=False, server_default=sa.text("true")),
        sa.Column("conditions", JSONB, nullable=False, server_default="[]"),
        sa.Column("recommendations", JSONB, nullable=False, server_default="{}"),
        sa.Column("rollout_percent", sa.Integer(), nullable=False, server_default="100"),
        sa.Column("min_client_version", sa.String(32), nullable=True),
        sa.Column("max_client_version", sa.String(32), nullable=True),
        sa.Column("platform", sa.String(32), nullable=False, server_default="all"),
        sa.Column("notes", sa.Text(), nullable=True),
        sa.Column("updated_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
        sa.Column("updated_by_admin_id", sa.String(128), nullable=True),
        sa.CheckConstraint("severity IN ('critical', 'warning', 'info')", name="ck_rules_catalog_severity"),
        sa.CheckConstraint("rollout_percent BETWEEN 0 AND 100", name="ck_rules_catalog_rollout"),
    )
    op.create_index("ix_rules_catalog_category", "rules_catalog", ["category"])
    op.create_index("ix_rules_catalog_enabled", "rules_catalog", ["enabled"])

    op.create_table(
        "rule_findings",
        sa.Column("id", UUID(as_uuid=True), primary_key=True, nullable=False),
        sa.Column("device_id", UUID(as_uuid=True), sa.ForeignKey("devices.id"), nullable=False),
        sa.Column("rule_id", sa.String(64), sa.ForeignKey("rules_catalog.id"), nullable=False),
        sa.Column("state", sa.String(16), nullable=False, server_default="open"),
        sa.Column("details", JSONB, nullable=False, server_default="{}"),
        sa.Column("created_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
        sa.Column("resolved_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("updated_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
        sa.UniqueConstraint("device_id", "rule_id", name="uq_rule_findings_device_rule"),
        sa.CheckConstraint("state IN ('open', 'resolved', 'ignored')", name="ck_rule_findings_state"),
    )
    op.create_index("ix_rule_findings_device_id", "rule_findings", ["device_id"])
    op.create_index("ix_rule_findings_rule_id", "rule_findings", ["rule_id"])
    op.create_index("ix_rule_findings_state", "rule_findings", ["state"])

    op.create_table(
        "device_commands",
        sa.Column("id", UUID(as_uuid=True), primary_key=True, nullable=False),
        sa.Column("device_id", UUID(as_uuid=True), sa.ForeignKey("devices.id"), nullable=False),
        sa.Column("command", sa.String(64), nullable=False),
        sa.Column("payload", JSONB, nullable=True),
        sa.Column("status", sa.String(16), nullable=False, server_default="pending"),
        sa.Column("issued_by_admin_id", sa.String(128), nullable=True),
        sa.Column("issued_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
        sa.Column("sent_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("done_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("result", JSONB, nullable=True),
        sa.CheckConstraint(
            "status IN ('pending', 'sent', 'done', 'failed', 'cancelled')",
            name="ck_device_commands_status",
        ),
    )
    op.create_index("ix_device_commands_device_id", "device_commands", ["device_id"])
    op.create_index("ix_device_commands_status", "device_commands", ["status"])

    op.create_table(
        "client_config",
        sa.Column("id", UUID(as_uuid=True), primary_key=True, nullable=False),
        sa.Column("scope", sa.String(32), nullable=False, server_default="global"),
        sa.Column("scope_id", sa.String(128), nullable=True),
        sa.Column("config", JSONB, nullable=False, server_default="{}"),
        sa.Column("updated_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.text("now()")),
        sa.Column("updated_by_admin_id", sa.String(128), nullable=True),
        sa.UniqueConstraint("scope", "scope_id", name="uq_client_config_scope_scope_id"),
        sa.CheckConstraint(
            "(scope = 'global' AND scope_id IS NULL) OR (scope != 'global' AND scope_id IS NOT NULL)",
            name="ck_client_config_scope_scope_id",
        ),
        sa.CheckConstraint("scope IN ('global', 'device', 'user')", name="ck_client_config_scope"),
    )


def downgrade() -> None:
    op.drop_table("client_config")
    op.drop_index("ix_device_commands_status", table_name="device_commands")
    op.drop_index("ix_device_commands_device_id", table_name="device_commands")
    op.drop_table("device_commands")
    op.drop_index("ix_rule_findings_state", table_name="rule_findings")
    op.drop_index("ix_rule_findings_rule_id", table_name="rule_findings")
    op.drop_index("ix_rule_findings_device_id", table_name="rule_findings")
    op.drop_table("rule_findings")
    op.drop_index("ix_rules_catalog_enabled", table_name="rules_catalog")
    op.drop_index("ix_rules_catalog_category", table_name="rules_catalog")
    op.drop_table("rules_catalog")
