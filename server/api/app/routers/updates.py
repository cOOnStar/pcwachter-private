from __future__ import annotations

import re
from datetime import datetime, timezone
from enum import Enum

from fastapi import APIRouter, Depends, HTTPException, Query
from pydantic import BaseModel, Field, field_validator
from sqlalchemy import select
from sqlalchemy.orm import Session

from ..db import get_db
from ..models import UpdateManifest
from ..security_jwt import require_console_owner, require_console_user

router = APIRouter(tags=["updates"])

_SEMVER_RE = re.compile(r"^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$")


class UpdateComponent(str, Enum):
    desktop = "desktop"
    agent = "agent"
    updater = "updater"


class UpdateChannel(str, Enum):
    stable = "stable"
    beta = "beta"
    internal = "internal"


class UpdateManifestEnvelope(BaseModel):
    component: UpdateComponent
    channel: UpdateChannel
    latest_version: str = Field(min_length=1, max_length=64)
    min_supported_version: str = Field(min_length=1, max_length=64)
    mandatory: bool
    download_url: str = Field(min_length=1, max_length=4096)
    sha256: str = Field(min_length=1, max_length=128)
    changelog: str | None = None
    released_at: datetime

    @field_validator("latest_version", "min_supported_version")
    @classmethod
    def _validate_semver(cls, value: str) -> str:
        raw = value.strip()
        if not _SEMVER_RE.match(raw):
            raise ValueError("invalid semver format")
        return raw


class UpdateManifestUpsertRequest(UpdateManifestEnvelope):
    pass


class UpdateManifestListResponse(BaseModel):
    items: list[UpdateManifestEnvelope]
    total: int


def _to_envelope(row: UpdateManifest) -> UpdateManifestEnvelope:
    return UpdateManifestEnvelope(
        component=UpdateComponent(row.component),
        channel=UpdateChannel(row.channel),
        latest_version=row.latest_version,
        min_supported_version=row.min_supported_version,
        mandatory=bool(row.mandatory),
        download_url=row.download_url,
        sha256=row.sha256,
        changelog=row.changelog,
        released_at=row.released_at,
    )


@router.get("/updates/latest", response_model=UpdateManifestEnvelope)
def get_latest_update_manifest(
    channel: UpdateChannel = Query(default=UpdateChannel.stable),
    component: UpdateComponent = Query(default=UpdateComponent.desktop),
    db: Session = Depends(get_db),
):
    row = db.execute(
        select(UpdateManifest).where(
            UpdateManifest.component == component.value,
            UpdateManifest.channel == channel.value,
        )
    ).scalar_one_or_none()
    if not row:
        raise HTTPException(status_code=404, detail="update manifest not found")

    return _to_envelope(row)


@router.get("/console/ui/updates/manifests", response_model=UpdateManifestListResponse)
def list_update_manifests(
    component: UpdateComponent | None = Query(default=None),
    channel: UpdateChannel | None = Query(default=None),
    db: Session = Depends(get_db),
    _user: dict = Depends(require_console_user),
):
    stmt = select(UpdateManifest)
    if component is not None:
        stmt = stmt.where(UpdateManifest.component == component.value)
    if channel is not None:
        stmt = stmt.where(UpdateManifest.channel == channel.value)

    rows = db.execute(
        stmt.order_by(UpdateManifest.component.asc(), UpdateManifest.channel.asc())
    ).scalars().all()

    items = [_to_envelope(row) for row in rows]
    return UpdateManifestListResponse(items=items, total=len(items))


@router.post("/console/ui/updates/manifests", response_model=UpdateManifestEnvelope)
def upsert_update_manifest(
    payload: UpdateManifestUpsertRequest,
    db: Session = Depends(get_db),
    _owner: dict = Depends(require_console_owner),
):
    row = db.execute(
        select(UpdateManifest).where(
            UpdateManifest.component == payload.component.value,
            UpdateManifest.channel == payload.channel.value,
        )
    ).scalar_one_or_none()

    if row is None:
        row = UpdateManifest(
            component=payload.component.value,
            channel=payload.channel.value,
            latest_version=payload.latest_version,
            min_supported_version=payload.min_supported_version,
            mandatory=payload.mandatory,
            download_url=payload.download_url,
            sha256=payload.sha256,
            changelog=payload.changelog,
            released_at=payload.released_at,
        )
        db.add(row)
    else:
        row.latest_version = payload.latest_version
        row.min_supported_version = payload.min_supported_version
        row.mandatory = payload.mandatory
        row.download_url = payload.download_url
        row.sha256 = payload.sha256
        row.changelog = payload.changelog
        row.released_at = payload.released_at
        row.updated_at = datetime.now(timezone.utc)

    db.commit()
    db.refresh(row)
    return _to_envelope(row)
