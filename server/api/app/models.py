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
