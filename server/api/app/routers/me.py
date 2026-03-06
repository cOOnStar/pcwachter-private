"""Identity and self-service profile endpoints for the current home user."""
from __future__ import annotations

import logging

import httpx
from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel, Field
from sqlalchemy.orm import Session

from ..db import get_db
from ..keycloak_admin import (
    fetch_keycloak_user,
    keycloak_admin_configured,
    keycloak_admin_context,
)
from ..security_jwt import require_home_user
from ..services.support_service import normalize_email, normalize_name, sync_zammad_profile_for_identity

router = APIRouter(tags=["me"])
logger = logging.getLogger(__name__)


class MeResponse(BaseModel):
    sub: str
    email: str | None
    name: str | None
    roles: list[str]


class ProfileResponse(BaseModel):
    sub: str
    email: str | None
    first_name: str | None
    last_name: str | None
    name: str | None
    email_verified: bool | None = None
    warnings: list[str] = Field(default_factory=list)


class ProfileUpdateIn(BaseModel):
    email: str = Field(..., min_length=3, max_length=254)
    first_name: str | None = Field(default=None, max_length=255)
    last_name: str | None = Field(default=None, max_length=255)


def _require_user_sub(user: dict) -> str:
    user_id = str(user.get("sub") or "").strip()
    if not user_id:
        raise HTTPException(status_code=401, detail="user_sub_missing")
    return user_id


def _profile_from_claims(user: dict, warnings: list[str] | None = None) -> ProfileResponse:
    first_name = normalize_name(user.get("given_name") or user.get("firstName"))
    last_name = normalize_name(user.get("family_name") or user.get("lastName"))
    display_name = normalize_name(user.get("name"))
    if not display_name:
        display_name = " ".join(part for part in [first_name, last_name] if part) or normalize_email(user.get("email"))

    return ProfileResponse(
        sub=str(user.get("sub") or ""),
        email=normalize_email(user.get("email")),
        first_name=first_name,
        last_name=last_name,
        name=display_name or None,
        email_verified=bool(user.get("email_verified")) if user.get("email_verified") is not None else None,
        warnings=warnings or [],
    )


def _profile_from_keycloak_record(record: dict, warnings: list[str] | None = None) -> ProfileResponse:
    first_name = normalize_name(record.get("firstName"))
    last_name = normalize_name(record.get("lastName"))
    display_name = " ".join(part for part in [first_name, last_name] if part)
    if not display_name:
        display_name = normalize_name(record.get("email")) or normalize_name(record.get("username"))

    return ProfileResponse(
        sub=str(record.get("id") or ""),
        email=normalize_email(record.get("email")),
        first_name=first_name,
        last_name=last_name,
        name=display_name or None,
        email_verified=bool(record.get("emailVerified")) if record.get("emailVerified") is not None else None,
        warnings=warnings or [],
    )


@router.get("/me", response_model=MeResponse)
def get_me(user: dict = Depends(require_home_user)) -> MeResponse:
    """Return identity claims from the current bearer token."""
    roles: list[str] = (user.get("realm_access") or {}).get("roles", [])
    return MeResponse(
        sub=user.get("sub", ""),
        email=user.get("email"),
        name=user.get("name"),
        roles=roles,
    )


@router.get("/me/profile", response_model=ProfileResponse)
def get_profile(user: dict = Depends(require_home_user)) -> ProfileResponse:
    """Return the current user's editable profile data."""
    user_id = _require_user_sub(user)
    if not keycloak_admin_configured():
        return _profile_from_claims(user)

    try:
        record = fetch_keycloak_user(user_id)
    except HTTPException as exc:
        if exc.status_code in {503, 502}:
            logger.warning("profile fallback to userinfo for %s: %s", user_id, exc.detail)
            return _profile_from_claims(user, warnings=["profile_fallback"])
        raise

    return _profile_from_keycloak_record(record)


@router.patch("/me/profile", response_model=ProfileResponse)
async def update_profile(
    payload: ProfileUpdateIn,
    user: dict = Depends(require_home_user),
    db: Session = Depends(get_db),
) -> ProfileResponse:
    """Update the current user's profile in Keycloak and, if configured, in Zammad."""
    user_id = _require_user_sub(user)
    record = fetch_keycloak_user(user_id)

    email = normalize_email(payload.email)
    if not email:
        raise HTTPException(status_code=400, detail="email_required")

    first_name = normalize_name(payload.first_name) or ""
    last_name = normalize_name(payload.last_name) or ""
    previous_email = normalize_email(record.get("email")) or normalize_email(user.get("email"))
    username = str(record.get("username") or "").strip()
    should_update_username = not username or (previous_email and username.lower() == previous_email.lower())

    update_payload: dict[str, object] = {
        "email": email,
        "firstName": first_name,
        "lastName": last_name,
        "enabled": bool(record.get("enabled", True)),
        "emailVerified": bool(record.get("emailVerified")) if previous_email == email else False,
    }
    if should_update_username:
        update_payload["username"] = email

    attributes = record.get("attributes")
    if isinstance(attributes, dict):
        update_payload["attributes"] = attributes

    required_actions = record.get("requiredActions")
    if isinstance(required_actions, list):
        update_payload["requiredActions"] = required_actions

    base, headers = keycloak_admin_context()
    try:
        response = httpx.put(
            f"{base}/users/{user_id}",
            headers=headers,
            json=update_payload,
            timeout=15,
        )
    except httpx.RequestError as exc:
        raise HTTPException(status_code=503, detail="keycloak_unreachable") from exc

    if response.status_code == 404:
        raise HTTPException(status_code=404, detail="profile_not_found")
    if response.status_code == 409:
        raise HTTPException(status_code=409, detail="email_already_exists")
    if not response.is_success:
        raise HTTPException(status_code=502, detail="keycloak_profile_update_failed")

    refreshed = fetch_keycloak_user(user_id)
    warnings = await sync_zammad_profile_for_identity(
        db=db,
        keycloak_user_id=user_id,
        email=email,
        first_name=first_name,
        last_name=last_name,
    )
    return _profile_from_keycloak_record(refreshed, warnings=warnings)
