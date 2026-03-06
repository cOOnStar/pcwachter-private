import uuid
from datetime import datetime

from sqlalchemy import Boolean, CheckConstraint, DateTime, Float, ForeignKey, Index, Integer, String, Text, UniqueConstraint, func
from sqlalchemy.dialects.postgresql import JSONB, UUID
from sqlalchemy.orm import Mapped, mapped_column, relationship

from .db import Base


class Plan(Base):
    __tablename__ = "plans"

    id: Mapped[str] = mapped_column(String(32), primary_key=True)  # "trial", "standard", etc.
    label: Mapped[str] = mapped_column(String(64), nullable=False)
    price_eur: Mapped[float | None] = mapped_column(Float, nullable=True)
    duration_days: Mapped[int | None] = mapped_column(Integer, nullable=True)  # None = lifetime
    max_devices: Mapped[int | None] = mapped_column(Integer, nullable=True)    # None = unlimited
    is_active: Mapped[bool] = mapped_column(Boolean, nullable=False, default=True)
    sort_order: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    feature_flags: Mapped[dict | None] = mapped_column(JSONB, nullable=True)   # {"auto_fix": true, ...}
    grace_period_days: Mapped[int] = mapped_column(Integer, nullable=False, default=7)
    stripe_price_id: Mapped[str | None] = mapped_column(String(128), nullable=True)
    stripe_product_id: Mapped[str | None] = mapped_column(String(128), nullable=True)
    amount_cents: Mapped[int | None] = mapped_column(Integer, nullable=True)
    currency: Mapped[str] = mapped_column(String(8), nullable=False, default="eur", server_default="eur")
    price_version: Mapped[int] = mapped_column(Integer, nullable=False, default=1, server_default="1")

    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), nullable=False)
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True),
        server_default=func.now(),
        onupdate=func.now(),
        nullable=False,
    )


class Device(Base):
    __tablename__ = "devices"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    device_install_id: Mapped[str] = mapped_column(String(128), nullable=False, unique=True)
    host_name: Mapped[str | None] = mapped_column(String(255), nullable=True, index=True)

    os_name: Mapped[str | None] = mapped_column(String, nullable=True)
    os_version: Mapped[str | None] = mapped_column(String, nullable=True)
    os_build: Mapped[str | None] = mapped_column(String, nullable=True)

    agent_version: Mapped[str | None] = mapped_column(String, nullable=True)
    agent_channel: Mapped[str | None] = mapped_column(String, nullable=True)

    desktop_version: Mapped[str | None] = mapped_column(String(64), nullable=True)
    updater_version: Mapped[str | None] = mapped_column(String(64), nullable=True)
    update_channel: Mapped[str | None] = mapped_column(String(32), nullable=True)

    primary_ip: Mapped[str | None] = mapped_column(String, nullable=True)
    macs: Mapped[dict | None] = mapped_column(JSONB, nullable=True)

    last_seen_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True, index=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), nullable=False)
    blocked: Mapped[bool] = mapped_column(Boolean, nullable=False, default=False, server_default="false")

    inventories: Mapped[list["DeviceInventory"]] = relationship(back_populates="device", cascade="all, delete-orphan")
    telemetry_snapshots: Mapped[list["TelemetrySnapshot"]] = relationship(
        back_populates="device", cascade="all, delete-orphan"
    )


class DeviceInventory(Base):
    __tablename__ = "device_inventory"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    device_id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), ForeignKey("devices.id"), nullable=False, index=True)

    collected_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False)
    payload: Mapped[dict] = mapped_column(JSONB, nullable=False)

    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), nullable=False)

    device: Mapped["Device"] = relationship(back_populates="inventories")

    __table_args__ = (UniqueConstraint("device_id", "collected_at", name="uq_device_inventory_device_collected"),)


class TelemetrySnapshot(Base):
    __tablename__ = "telemetry_snapshots"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    device_id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), ForeignKey("devices.id"), nullable=False, index=True)
    received_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), nullable=False, index=True)
    category: Mapped[str] = mapped_column(String(64), nullable=False, index=True)
    payload: Mapped[dict] = mapped_column(JSONB, nullable=False)
    summary: Mapped[str | None] = mapped_column(String(512), nullable=True)
    source: Mapped[str] = mapped_column(String(32), nullable=False)

    device: Mapped["Device"] = relationship(back_populates="telemetry_snapshots")

    __table_args__ = (
        Index(
            "ix_telemetry_snapshots_device_category_received_desc",
            "device_id",
            "category",
            received_at.desc(),
        ),
    )


class License(Base):
    __tablename__ = "licenses"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    license_key: Mapped[str] = mapped_column(String(128), nullable=False, unique=True, index=True)
    tier: Mapped[str] = mapped_column(String(32), nullable=False)
    duration_days: Mapped[int | None] = mapped_column(Integer, nullable=True)
    state: Mapped[str] = mapped_column(String(24), nullable=False, index=True, default="issued")

    issued_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), nullable=False)
    activated_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    expires_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True, index=True)

    activated_device_install_id: Mapped[str | None] = mapped_column(String(128), nullable=True, index=True)
    activated_by_user_id: Mapped[str | None] = mapped_column(String(128), nullable=True)

    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), nullable=False)
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True),
        server_default=func.now(),
        onupdate=func.now(),
        nullable=False,
    )


class Subscription(Base):
    __tablename__ = "subscriptions"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    keycloak_user_id: Mapped[str] = mapped_column(String(128), nullable=False, index=True)
    license_id: Mapped[uuid.UUID | None] = mapped_column(UUID(as_uuid=True), ForeignKey("licenses.id"), nullable=True)
    plan_id: Mapped[str | None] = mapped_column(String(32), ForeignKey("plans.id"), nullable=True)
    status: Mapped[str] = mapped_column(String(24), nullable=False, default="active", index=True)
    # active | expired | grace | cancelled

    stripe_customer_id: Mapped[str | None] = mapped_column(String(128), nullable=True, index=True)
    stripe_subscription_id: Mapped[str | None] = mapped_column(String(128), nullable=True, index=True)
    stripe_price_id: Mapped[str | None] = mapped_column(String(128), nullable=True)

    current_period_start: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    current_period_end: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    grace_until: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    trial_used: Mapped[bool] = mapped_column(Boolean, nullable=False, default=False)
    allow_self_cancel: Mapped[bool] = mapped_column(Boolean, nullable=False, default=False, server_default="false")

    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), nullable=False)
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True),
        server_default=func.now(),
        onupdate=func.now(),
        nullable=False,
    )

    license: Mapped["License | None"] = relationship("License", foreign_keys=[license_id])


class DeviceToken(Base):
    __tablename__ = "device_tokens"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    device_install_id: Mapped[str] = mapped_column(
        String(128),
        ForeignKey("devices.device_install_id", ondelete="CASCADE"),
        nullable=False,
        index=True,
    )
    token_hash: Mapped[str] = mapped_column(String(128), nullable=False, unique=True)
    expires_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), nullable=False)
    last_used_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    revoked_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)


class WebhookEvent(Base):
    __tablename__ = "webhook_events"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    event_type: Mapped[str] = mapped_column(String(128), nullable=False)
    stripe_event_id: Mapped[str] = mapped_column(String(128), nullable=False, unique=True)
    payload: Mapped[dict | None] = mapped_column(JSONB, nullable=True)
    processed_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), nullable=False)


class WebhookEventV2(Base):
    __tablename__ = "webhook_events_v2"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    source: Mapped[str] = mapped_column(String(32), nullable=False)
    event_id: Mapped[str] = mapped_column(String(255), nullable=False)
    event_type: Mapped[str] = mapped_column(String(128), nullable=False)
    payload: Mapped[dict | None] = mapped_column(JSONB, nullable=True)
    received_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), nullable=False)
    processed_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    status: Mapped[str] = mapped_column(String(16), nullable=False, default="ok", server_default="ok")
    error: Mapped[str | None] = mapped_column(Text, nullable=True)

    __table_args__ = (
        UniqueConstraint("source", "event_id", name="uq_webhook_events_v2_source_event_id"),
        CheckConstraint("source IN ('stripe', 'zammad')", name="ck_webhook_events_v2_source"),
        CheckConstraint("status IN ('ok', 'failed')", name="ck_webhook_events_v2_status"),
        Index("ix_webhook_events_v2_source_type_received", "source", "event_type", "received_at"),
    )


class UpdateManifest(Base):
    __tablename__ = "update_manifests"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    component: Mapped[str] = mapped_column(String(32), nullable=False)
    channel: Mapped[str] = mapped_column(String(32), nullable=False)
    latest_version: Mapped[str] = mapped_column(String(64), nullable=False)
    min_supported_version: Mapped[str] = mapped_column(String(64), nullable=False)
    mandatory: Mapped[bool] = mapped_column(Boolean, nullable=False, default=False, server_default="false")
    download_url: Mapped[str] = mapped_column(Text, nullable=False)
    sha256: Mapped[str] = mapped_column(String(128), nullable=False)
    changelog: Mapped[str | None] = mapped_column(Text, nullable=True)
    released_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), nullable=False)
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True),
        server_default=func.now(),
        onupdate=func.now(),
        nullable=False,
    )

    __table_args__ = (
        UniqueConstraint("component", "channel", name="uq_update_manifests_component_channel"),
        CheckConstraint("component IN ('desktop', 'agent', 'updater')", name="ck_update_manifests_component"),
        CheckConstraint("channel IN ('stable', 'beta', 'internal')", name="ck_update_manifests_channel"),
    )


class FeatureOverride(Base):
    """Per-feature kill-switch and rollout control.

    Scope values: 'global' | 'plan' | 'user' | 'device'
    For scope='global', target_id must be NULL.
    For other scopes, target_id must be non-NULL (plan-id / user-id / device-id).
    Uniqueness is enforced via uq_fo_key_scope_target on (feature_key, scope, COALESCE(target_id, '')).
    """

    __tablename__ = "feature_overrides"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    feature_key: Mapped[str] = mapped_column(String(64), nullable=False)
    enabled: Mapped[bool] = mapped_column(Boolean, nullable=False, default=True, server_default="true")
    rollout_percent: Mapped[int] = mapped_column(Integer, nullable=False, default=100, server_default="100")
    scope: Mapped[str] = mapped_column(String(32), nullable=False, default="global", server_default="global")
    target_id: Mapped[str | None] = mapped_column(String(128), nullable=True)
    version_min: Mapped[str | None] = mapped_column(String(32), nullable=True)
    platform: Mapped[str] = mapped_column(String(32), nullable=False, default="all", server_default="all")
    notes: Mapped[str | None] = mapped_column(Text, nullable=True)
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True),
        server_default=func.now(),
        onupdate=func.now(),
        nullable=False,
    )

    __table_args__ = (
        CheckConstraint(
            "(scope = 'global' AND target_id IS NULL) OR (scope != 'global' AND target_id IS NOT NULL)",
            name="ck_feature_override_scope_target",
        ),
        Index("ix_fo_scope_target", "scope", "target_id"),
    )


class KbArticle(Base):
    __tablename__ = "kb_articles"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, server_default=func.gen_random_uuid())
    title: Mapped[str] = mapped_column(String(200), nullable=False, index=True)
    category: Mapped[str] = mapped_column(String(64), nullable=False, default="general", server_default="general")
    tags: Mapped[list] = mapped_column(JSONB, nullable=False, default=list, server_default="[]")
    body_md: Mapped[str] = mapped_column(Text, nullable=False, default="", server_default="")
    published: Mapped[bool] = mapped_column(Boolean, nullable=False, default=True, server_default="true", index=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), nullable=False)
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True),
        server_default=func.now(),
        onupdate=func.now(),
        nullable=False,
        index=True,
    )


class Notification(Base):
    __tablename__ = "notifications"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    user_id: Mapped[str] = mapped_column(String(128), nullable=False, index=True)
    type: Mapped[str] = mapped_column(String(64), nullable=False)
    title: Mapped[str] = mapped_column(String(255), nullable=False)
    body: Mapped[str] = mapped_column(Text, nullable=False)
    meta: Mapped[dict | None] = mapped_column(JSONB, nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), nullable=False, index=True)
    read_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True, index=True)


class SupportPortalSettings(Base):
    __tablename__ = "support_portal_settings"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    allow_customer_group_selection: Mapped[bool] = mapped_column(
        Boolean, nullable=False, default=False, server_default="false"
    )
    customer_visible_group_ids: Mapped[list] = mapped_column(JSONB, nullable=False, default=list, server_default="[]")
    default_group_id: Mapped[int | None] = mapped_column(Integer, nullable=True)
    default_priority_id: Mapped[int | None] = mapped_column(Integer, nullable=True)
    uploads_enabled: Mapped[bool] = mapped_column(Boolean, nullable=False, default=True, server_default="true")
    uploads_max_bytes: Mapped[int] = mapped_column(Integer, nullable=False, default=5 * 1024 * 1024, server_default="5242880")
    maintenance_mode: Mapped[bool] = mapped_column(Boolean, nullable=False, default=False, server_default="false")
    maintenance_message: Mapped[str | None] = mapped_column(Text, nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), nullable=False)
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True),
        server_default=func.now(),
        onupdate=func.now(),
        nullable=False,
    )


class SupportIdentityLink(Base):
    __tablename__ = "support_identity_links"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    keycloak_user_id: Mapped[str] = mapped_column(String(128), nullable=False, unique=True, index=True)
    zammad_user_id: Mapped[int] = mapped_column(Integer, nullable=False, unique=True, index=True)
    last_synced_email: Mapped[str | None] = mapped_column(String(254), nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), nullable=False)
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True),
        server_default=func.now(),
        onupdate=func.now(),
        nullable=False,
    )


class SupportAttachment(Base):
    __tablename__ = "support_attachments"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    keycloak_user_id: Mapped[str] = mapped_column(String(128), nullable=False, index=True)
    filename: Mapped[str] = mapped_column(String(255), nullable=False)
    mime_type: Mapped[str] = mapped_column(String(255), nullable=False, default="application/octet-stream")
    size_bytes: Mapped[int] = mapped_column(Integer, nullable=False)
    sha256: Mapped[str] = mapped_column(String(64), nullable=False)
    storage_path: Mapped[str] = mapped_column(Text, nullable=False)
    zammad_ticket_id: Mapped[int | None] = mapped_column(Integer, nullable=True, index=True)
    zammad_article_id: Mapped[int | None] = mapped_column(Integer, nullable=True, index=True)
    consumed_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True, index=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), nullable=False)


class SupportTicketSyncState(Base):
    __tablename__ = "support_ticket_sync_states"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    keycloak_user_id: Mapped[str] = mapped_column(String(128), nullable=False, index=True)
    zammad_ticket_id: Mapped[int] = mapped_column(Integer, nullable=False, index=True)
    ticket_number: Mapped[str | None] = mapped_column(String(64), nullable=True)
    ticket_title: Mapped[str | None] = mapped_column(String(255), nullable=True)
    last_state: Mapped[str | None] = mapped_column(String(64), nullable=True)
    last_ticket_updated_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    last_public_agent_article_id: Mapped[int | None] = mapped_column(Integer, nullable=True)
    last_public_agent_article_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    last_contact_agent_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    last_contact_customer_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), nullable=False)
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True),
        server_default=func.now(),
        onupdate=func.now(),
        nullable=False,
    )

    __table_args__ = (
        UniqueConstraint("keycloak_user_id", "zammad_ticket_id", name="uq_support_ticket_sync_user_ticket"),
    )


class RulesCatalog(Base):
    """Rule-based intelligence: defines conditions + recommendations per category."""

    __tablename__ = "rules_catalog"

    id: Mapped[str] = mapped_column(String(64), primary_key=True)  # slug, e.g. "cpu_high_sustained"
    name: Mapped[str] = mapped_column(String(128), nullable=False)
    category: Mapped[str] = mapped_column(String(32), nullable=False, index=True)
    # "performance" | "security" | "health" | "update" | "storage"
    severity: Mapped[str] = mapped_column(String(16), nullable=False, default="warning", server_default="warning")
    # "critical" | "warning" | "info"
    enabled: Mapped[bool] = mapped_column(Boolean, nullable=False, default=True, server_default="true")
    # conditions: list of threshold checks against telemetry payload values
    # e.g. [{"metric": "cpu_percent", "operator": ">", "threshold": 90}]
    conditions: Mapped[dict] = mapped_column(JSONB, nullable=False, default=list, server_default="[]")
    # recommendations: {text: str, actions: [str]}
    recommendations: Mapped[dict] = mapped_column(JSONB, nullable=False, default=dict, server_default="{}")
    rollout_percent: Mapped[int] = mapped_column(Integer, nullable=False, default=100, server_default="100")
    min_client_version: Mapped[str | None] = mapped_column(String(32), nullable=True)
    max_client_version: Mapped[str | None] = mapped_column(String(32), nullable=True)
    platform: Mapped[str] = mapped_column(String(32), nullable=False, default="all", server_default="all")
    notes: Mapped[str | None] = mapped_column(Text, nullable=True)
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), server_default=func.now(), onupdate=func.now(), nullable=False
    )
    updated_by_admin_id: Mapped[str | None] = mapped_column(String(128), nullable=True)

    findings: Mapped[list["RuleFinding"]] = relationship(back_populates="rule", cascade="all, delete-orphan")


class RuleFinding(Base):
    """Active finding: a rule was triggered for a specific device."""

    __tablename__ = "rule_findings"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    device_id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), ForeignKey("devices.id"), nullable=False, index=True)
    rule_id: Mapped[str] = mapped_column(String(64), ForeignKey("rules_catalog.id"), nullable=False, index=True)
    state: Mapped[str] = mapped_column(String(16), nullable=False, default="open", index=True)
    # "open" | "resolved" | "ignored"
    details: Mapped[dict] = mapped_column(JSONB, nullable=False, default=dict, server_default="{}")
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), nullable=False)
    resolved_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), server_default=func.now(), onupdate=func.now(), nullable=False
    )

    rule: Mapped["RulesCatalog"] = relationship(back_populates="findings")

    __table_args__ = (
        UniqueConstraint("device_id", "rule_id", name="uq_rule_findings_device_rule"),
    )


class DeviceCommand(Base):
    """Remote command issued to a device; agent polls and executes."""

    __tablename__ = "device_commands"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    device_id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), ForeignKey("devices.id"), nullable=False, index=True)
    command: Mapped[str] = mapped_column(String(64), nullable=False)
    # e.g. "restart_agent", "run_scan", "update_agent", "clear_findings"
    payload: Mapped[dict | None] = mapped_column(JSONB, nullable=True)
    status: Mapped[str] = mapped_column(String(16), nullable=False, default="pending", index=True)
    # "pending" | "sent" | "done" | "failed" | "cancelled"
    issued_by_admin_id: Mapped[str | None] = mapped_column(String(128), nullable=True)
    issued_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), nullable=False)
    sent_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    done_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    result: Mapped[dict | None] = mapped_column(JSONB, nullable=True)


class ClientConfig(Base):
    """Remote configuration delivered to agents/clients on startup/poll."""

    __tablename__ = "client_config"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    scope: Mapped[str] = mapped_column(String(32), nullable=False, default="global", server_default="global")
    # "global" | "device" | "user"
    scope_id: Mapped[str | None] = mapped_column(String(128), nullable=True)
    # NULL for global; device_install_id for device scope; keycloak_user_id for user scope
    config: Mapped[dict] = mapped_column(JSONB, nullable=False, default=dict, server_default="{}")
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), server_default=func.now(), onupdate=func.now(), nullable=False
    )
    updated_by_admin_id: Mapped[str | None] = mapped_column(String(128), nullable=True)

    __table_args__ = (
        UniqueConstraint("scope", "scope_id", name="uq_client_config_scope_scope_id"),
        CheckConstraint(
            "(scope = 'global' AND scope_id IS NULL) OR (scope != 'global' AND scope_id IS NOT NULL)",
            name="ck_client_config_scope_scope_id",
        ),
    )
