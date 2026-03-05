import json
import uuid
from datetime import datetime
from typing import Literal

from pydantic import BaseModel, Field, model_validator

_MAX_PAYLOAD_BYTES = 512 * 1024  # 512 KB


def _check_dict_size(value: dict, label: str = "payload") -> dict:
    if len(json.dumps(value).encode()) > _MAX_PAYLOAD_BYTES:
        raise ValueError(f"{label} exceeds 512 KB limit")
    return value


class OSInfo(BaseModel):
    name: str | None = None
    version: str | None = None
    build: str | None = None


class AgentInfo(BaseModel):
    version: str | None = None
    channel: str | None = None


class NetworkInfo(BaseModel):
    primary_ip: str | None = None
    macs: list[str] = Field(default_factory=list)


class AgentRegisterRequest(BaseModel):
    device_install_id: str
    hostname: str | None = None
    os: OSInfo | None = None
    agent: AgentInfo | None = None
    network: NetworkInfo | None = None


class AgentRegisterResponse(BaseModel):
    device_id: uuid.UUID
    poll_interval_seconds: int = 30
    server_time: datetime
    device_token: str | None = None  # issued on registration, store securely


class AgentTokenRotateResponse(BaseModel):
    device_token: str
    server_time: datetime


class AgentHeartbeatRequest(BaseModel):
    device_install_id: str
    at: datetime
    status: dict = Field(default_factory=dict)


class AgentHeartbeatResponse(BaseModel):
    ok: bool = True
    server_time: datetime


class AgentInventoryRequest(BaseModel):
    device_install_id: str
    collected_at: datetime
    inventory: dict

    @model_validator(mode="after")
    def _limit_inventory_size(self) -> "AgentInventoryRequest":
        _check_dict_size(self.inventory, "inventory")
        return self


class AgentInventoryResponse(BaseModel):
    ok: bool = True
    inventory_id: uuid.UUID


class DeviceListItem(BaseModel):
    device_id: uuid.UUID
    host_name: str | None = None
    os_name: str | None = None
    os_version: str | None = None
    last_seen_at: datetime | None = None
    online: bool
    primary_ip: str | None = None
    agent_version: str | None = None


class DeviceListResponse(BaseModel):
    items: list[DeviceListItem]
    total: int


class DeviceDetailResponse(BaseModel):
    device_id: uuid.UUID
    device_install_id: str
    host_name: str | None = None
    os: OSInfo | None = None
    agent: AgentInfo | None = None
    network: NetworkInfo | None = None
    last_seen_at: datetime | None = None
    online: bool


class LatestInventoryResponse(BaseModel):
    inventory_id: uuid.UUID
    collected_at: datetime
    inventory: dict


class TelemetryUpdateRequest(BaseModel):
    device_install_id: str
    host_name: str
    old_version: str | None = None
    new_version: str | None = None
    result: Literal["success", "failed", "rolled_back", "deferred", "silent_not_supported"]
    exit_code: int
    details: str | None = None
    diagnostics_bundle_id: str | None = None
    source: Literal["updater"] = "updater"


class TelemetrySnapshotIngestRequest(BaseModel):
    device_install_id: str
    host_name: str
    category: Literal["memory", "ssd", "antivirus"]
    payload: dict
    summary: str | None = None
    source: Literal["agent"] = "agent"

    @model_validator(mode="after")
    def _limit_payload_size(self) -> "TelemetrySnapshotIngestRequest":
        _check_dict_size(self.payload)
        return self


class TelemetrySnapshotEnvelope(BaseModel):
    id: uuid.UUID
    received_at: datetime
    summary: str | None = None
    payload: dict
    source: str


class DeviceOverviewLatest(BaseModel):
    memory: TelemetrySnapshotEnvelope | None = None
    ssd: TelemetrySnapshotEnvelope | None = None
    antivirus: TelemetrySnapshotEnvelope | None = None
    update: TelemetrySnapshotEnvelope | None = None


class DeviceOverviewItem(BaseModel):
    device_install_id: str
    host_name: str | None = None
    last_seen_at: datetime | None = None
    latest: DeviceOverviewLatest


class DeviceOverviewResponse(BaseModel):
    items: list[DeviceOverviewItem]
    total: int


class OkResponse(BaseModel):
    ok: bool = True


class LicenseActivateRequest(BaseModel):
    license_key: str
    device_install_id: str
    keycloak_user_id: str | None = None


class LicenseInfo(BaseModel):
    license_key: str
    tier: Literal["trial", "standard", "professional", "unlimited", "custom"]
    state: Literal["issued", "activated", "expired", "revoked"]
    duration_days: int | None = None
    issued_at: datetime
    activated_at: datetime | None = None
    expires_at: datetime | None = None
    activated_device_install_id: str | None = None
    activated_by_user_id: str | None = None


class LicenseActivateResponse(BaseModel):
    ok: bool = True
    activated_now: bool = False
    license: LicenseInfo


class LicenseMeResponse(BaseModel):
    ok: bool = True
    license: LicenseInfo


# ---------------------------------------------------------------------------
# Plans
# ---------------------------------------------------------------------------

class PlanItem(BaseModel):
    id: str
    label: str
    price_eur: float | None = None
    duration_days: int | None = None
    max_devices: int | None = None
    is_active: bool
    sort_order: int


class PlanListResponse(BaseModel):
    items: list[PlanItem]
    total: int


class PlanUpsertRequest(BaseModel):
    label: str = Field(min_length=1, max_length=64)
    price_eur: float | None = None
    duration_days: int | None = None
    max_devices: int | None = None
    is_active: bool = True
    sort_order: int = 0


# ---------------------------------------------------------------------------
# License admin
# ---------------------------------------------------------------------------

class LicenseCreateRequest(BaseModel):
    tier: Literal["trial", "standard", "professional", "unlimited", "custom"]
    quantity: int = Field(default=1, ge=1, le=100)


class LicenseCreateResponse(BaseModel):
    ok: bool = True
    licenses: list[LicenseInfo]


# ---------------------------------------------------------------------------
# License status (for desktop client / agent)
# ---------------------------------------------------------------------------

class LicenseStatusResponse(BaseModel):
    ok: bool = True
    plan: str
    plan_label: str
    state: str  # active | grace | expired | revoked | none
    expires_at: datetime | None = None
    grace_period_until: datetime | None = None
    days_remaining: int | None = None
    max_devices: int | None = None
    features: dict[str, bool] = {}


# ---------------------------------------------------------------------------
# Plans (extended)
# ---------------------------------------------------------------------------

class PlanItemExtended(PlanItem):
    feature_flags: dict | None = None
    grace_period_days: int = 7
    stripe_price_id: str | None = None


class PlanListResponseExtended(BaseModel):
    items: list[PlanItemExtended]
    total: int


class PlanUpsertRequestExtended(PlanUpsertRequest):
    feature_flags: dict | None = None
    grace_period_days: int = 7
    stripe_price_id: str | None = None


# ---------------------------------------------------------------------------
# Stripe / Payments
# ---------------------------------------------------------------------------

class StripeCheckoutRequest(BaseModel):
    plan_id: str
    success_url: str
    cancel_url: str


class StripeCheckoutResponse(BaseModel):
    ok: bool = True
    checkout_url: str
    session_id: str


class StripePortalRequest(BaseModel):
    return_url: str


class StripePortalResponse(BaseModel):
    ok: bool = True
    portal_url: str
