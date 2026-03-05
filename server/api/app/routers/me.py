"""Identity and self-service profile endpoints for the current home user."""
from __future__ import annotations

import logging

import httpx
from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel, Field

from ..security_jwt import require_home_user
from ..settings import settings

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


def _normalize_email(value: object) -> str | None:
    if value is None:
        return None
    text = str(value).strip().lower()
    return text or None


def _normalize_name(value: object) -> str | None:
    if value is None:
        return None
    text = str(value).strip()
    return text or None


def _require_user_sub(user: dict) -> str:
    user_id = str(user.get("sub") or "").strip()
    if not user_id:
        raise HTTPException(status_code=401, detail="user_sub_missing")
    return user_id


def _keycloak_admin_configured() -> bool:
    return bool(settings.KEYCLOAK_ADMIN_USER and settings.KEYCLOAK_ADMIN_PASSWORD)


def _keycloak_admin_context() -> tuple[str, dict[str, str]]:
    if not _keycloak_admin_configured():
        raise HTTPException(status_code=503, detail="profile_update_unavailable")

    token_url = f"{settings.KEYCLOAK_URL}/realms/master/protocol/openid-connect/token"
    data = {
        "grant_type": "password",
        "client_id": settings.KEYCLOAK_ADMIN_CLIENT_ID,
        "username": settings.KEYCLOAK_ADMIN_USER,
        "password": settings.KEYCLOAK_ADMIN_PASSWORD,
    }
    if settings.KEYCLOAK_ADMIN_CLIENT_SECRET:
        data["client_secret"] = settings.KEYCLOAK_ADMIN_CLIENT_SECRET

    try:
        response = httpx.post(token_url, data=data, timeout=15)
    except httpx.RequestError as exc:
        raise HTTPException(status_code=503, detail="keycloak_unreachable") from exc

    if not response.is_success:
        raise HTTPException(status_code=503, detail="keycloak_admin_auth_failed")

    token = str(response.json().get("access_token") or "").strip()
    if not token:
        raise HTTPException(status_code=503, detail="keycloak_admin_auth_failed")

    base = f"{settings.KEYCLOAK_URL}/admin/realms/{settings.KEYCLOAK_REALM}"
    return base, {"Authorization": f"Bearer {token}"}


def _fetch_keycloak_user(user_id: str) -> dict:
    base, headers = _keycloak_admin_context()
    try:
        response = httpx.get(f"{base}/users/{user_id}", headers=headers, timeout=15)
    except httpx.RequestError as exc:
        raise HTTPException(status_code=503, detail="keycloak_unreachable") from exc

    if response.status_code == 404:
        raise HTTPException(status_code=404, detail="profile_not_found")
    if not response.is_success:
        raise HTTPException(status_code=502, detail="keycloak_profile_lookup_failed")

    payload = response.json()
    if not isinstance(payload, dict):
        raise HTTPException(status_code=502, detail="keycloak_profile_lookup_failed")
    return payload


def _profile_from_claims(user: dict, warnings: list[str] | None = None) -> ProfileResponse:
    first_name = _normalize_name(user.get("given_name") or user.get("firstName"))
    last_name = _normalize_name(user.get("family_name") or user.get("lastName"))
    display_name = _normalize_name(user.get("name"))
    if not display_name:
        display_name = " ".join(part for part in [first_name, last_name] if part) or _normalize_email(user.get("email"))

    return ProfileResponse(
        sub=str(user.get("sub") or ""),
        email=_normalize_email(user.get("email")),
        first_name=first_name,
        last_name=last_name,
        name=display_name or None,
        email_verified=bool(user.get("email_verified")) if user.get("email_verified") is not None else None,
        warnings=warnings or [],
    )


def _profile_from_keycloak_record(record: dict, warnings: list[str] | None = None) -> ProfileResponse:
    first_name = _normalize_name(record.get("firstName"))
    last_name = _normalize_name(record.get("lastName"))
    display_name = " ".join(part for part in [first_name, last_name] if part)
    if not display_name:
        display_name = _normalize_name(record.get("email")) or _normalize_name(record.get("username"))

    return ProfileResponse(
        sub=str(record.get("id") or ""),
        email=_normalize_email(record.get("email")),
        first_name=first_name,
        last_name=last_name,
        name=display_name or None,
        email_verified=bool(record.get("emailVerified")) if record.get("emailVerified") is not None else None,
        warnings=warnings or [],
    )


def _zammad_headers() -> dict[str, str] | None:
    if not settings.ZAMMAD_BASE_URL or not settings.ZAMMAD_API_TOKEN:
        return None
    return {
        "Authorization": f"Token token={settings.ZAMMAD_API_TOKEN}",
        "Content-Type": "application/json",
    }


def _sync_zammad_profile(
    *,
    previous_email: str | None,
    email: str,
    first_name: str,
    last_name: str,
) -> list[str]:
    headers = _zammad_headers()
    if headers is None:
        return []

    lookup_email = previous_email or email
    if not lookup_email:
        return []

    base = settings.ZAMMAD_BASE_URL.rstrip("/")
    try:
        search_resp = httpx.get(
            f"{base}/api/v1/users/search",
            params={"query": lookup_email},
            headers=headers,
            timeout=15,
        )
    except httpx.RequestError as exc:
        logger.warning("zammad profile search failed for %s: %s", lookup_email, exc)
        return ["support_sync_unreachable"]

    if not search_resp.is_success:
        logger.warning(
            "zammad profile search returned %s for %s",
            search_resp.status_code,
            lookup_email,
        )
        return ["support_sync_failed"]

    search_payload = search_resp.json()
    if not isinstance(search_payload, list):
        logger.warning("zammad profile search returned unexpected payload for %s", lookup_email)
        return ["support_sync_failed"]

    match = next((row for row in search_payload if isinstance(row, dict)), None)
    if match is None:
        return []

    zammad_user_id = match.get("id")
    if zammad_user_id is None:
        return ["support_sync_failed"]

    login = str(match.get("login") or "").strip()
    update_payload: dict[str, object] = {
        "email": email,
        "firstname": first_name,
        "lastname": last_name,
    }
    if not login or (previous_email and login.lower() == previous_email.lower()):
        update_payload["login"] = email

    try:
        update_resp = httpx.put(
            f"{base}/api/v1/users/{zammad_user_id}",
            headers=headers,
            json=update_payload,
            timeout=15,
        )
    except httpx.RequestError as exc:
        logger.warning("zammad profile update failed for %s: %s", lookup_email, exc)
        return ["support_sync_unreachable"]

    if not update_resp.is_success:
        logger.warning(
            "zammad profile update returned %s for %s",
            update_resp.status_code,
            lookup_email,
        )
        return ["support_sync_failed"]

    return []


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
    if not _keycloak_admin_configured():
        return _profile_from_claims(user)

    try:
        record = _fetch_keycloak_user(user_id)
    except HTTPException as exc:
        if exc.status_code in {503, 502}:
            logger.warning("profile fallback to userinfo for %s: %s", user_id, exc.detail)
            return _profile_from_claims(user, warnings=["profile_fallback"])
        raise

    return _profile_from_keycloak_record(record)


@router.patch("/me/profile", response_model=ProfileResponse)
def update_profile(
    payload: ProfileUpdateIn,
    user: dict = Depends(require_home_user),
) -> ProfileResponse:
    """Update the current user's profile in Keycloak and, if configured, in Zammad."""
    user_id = _require_user_sub(user)
    record = _fetch_keycloak_user(user_id)

    email = _normalize_email(payload.email)
    if not email:
        raise HTTPException(status_code=400, detail="email_required")

    first_name = _normalize_name(payload.first_name) or ""
    last_name = _normalize_name(payload.last_name) or ""
    previous_email = _normalize_email(record.get("email")) or _normalize_email(user.get("email"))
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

    base, headers = _keycloak_admin_context()
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

    refreshed = _fetch_keycloak_user(user_id)
    warnings = _sync_zammad_profile(
        previous_email=previous_email,
        email=email,
        first_name=first_name,
        last_name=last_name,
    )
    return _profile_from_keycloak_record(refreshed, warnings=warnings)
