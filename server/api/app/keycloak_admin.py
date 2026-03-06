from __future__ import annotations

import httpx
from fastapi import HTTPException

from .settings import settings


def keycloak_admin_configured() -> bool:
    return bool(settings.KEYCLOAK_ADMIN_USER and settings.KEYCLOAK_ADMIN_PASSWORD)


def keycloak_admin_context() -> tuple[str, dict[str, str]]:
    if not keycloak_admin_configured():
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


def fetch_keycloak_user(user_id: str) -> dict:
    base, headers = keycloak_admin_context()
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
