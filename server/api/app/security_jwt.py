"""Keycloak JWT verification via JWKS (local signature check) + userinfo for roles."""

import base64
import json
import time
from threading import Lock
from typing import Any

import httpx
from fastapi import Header, HTTPException
from jose import JWTError, jwt

from .settings import settings

# ---------------------------------------------------------------------------
# Legacy helper: decode JWT payload without signature check (claims only)
# Used by license.py to extract `sub` before doing API-key fallback auth.
# ---------------------------------------------------------------------------

def _decode_jwt_payload(token: str) -> dict:
    import base64, json
    parts = token.split(".")
    if len(parts) < 2:
        return {}
    payload = parts[1]
    payload += "=" * (-len(payload) % 4)
    try:
        decoded = base64.urlsafe_b64decode(payload.encode("utf-8"))
        data = json.loads(decoded.decode("utf-8"))
        return data if isinstance(data, dict) else {}
    except Exception:
        return {}


# ---------------------------------------------------------------------------
# JWKS cache — refreshed at most once every 5 minutes
# ---------------------------------------------------------------------------
_jwks_cache: dict[str, Any] = {}
_jwks_fetched_at: float = 0.0
_oidc_cache: dict[str, Any] = {}
_oidc_fetched_at: float = 0.0
_jwks_lock = Lock()
_JWKS_TTL = 300  # seconds


def _get_jwks() -> dict:
    global _jwks_cache, _jwks_fetched_at
    now = time.monotonic()
    with _jwks_lock:
        if now - _jwks_fetched_at > _JWKS_TTL or not _jwks_cache:
            url = f"{settings.KEYCLOAK_URL}/realms/{settings.KEYCLOAK_REALM}/protocol/openid-connect/certs"
            try:
                r = httpx.get(url, timeout=10)
                r.raise_for_status()
                _jwks_cache = r.json()
                _jwks_fetched_at = now
            except Exception:
                if not _jwks_cache:
                    raise HTTPException(status_code=503, detail="keycloak JWKS unreachable")
        return _jwks_cache


def _get_oidc_config() -> dict[str, Any]:
    global _oidc_cache, _oidc_fetched_at
    now = time.monotonic()
    with _jwks_lock:
        if now - _oidc_fetched_at > _JWKS_TTL or not _oidc_cache:
            url = f"{settings.KEYCLOAK_URL}/realms/{settings.KEYCLOAK_REALM}/.well-known/openid-configuration"
            try:
                r = httpx.get(url, timeout=10)
                r.raise_for_status()
                data = r.json()
                if isinstance(data, dict):
                    _oidc_cache = data
                    _oidc_fetched_at = now
            except Exception:
                if not _oidc_cache:
                    raise HTTPException(status_code=503, detail="keycloak OIDC discovery unreachable")
        return _oidc_cache


def _expected_issuer() -> str:
    explicit = settings.KEYCLOAK_ISSUER.strip()
    if explicit:
        return explicit

    discovered = _get_oidc_config().get("issuer")
    if isinstance(discovered, str) and discovered.strip():
        return discovered.strip()

    return f"{settings.KEYCLOAK_URL.rstrip('/')}/realms/{settings.KEYCLOAK_REALM}"


def _extract_bearer_token(authorization: str | None) -> str:
    if not authorization or not authorization.startswith("Bearer "):
        raise HTTPException(status_code=401, detail="missing bearer token")
    token = authorization.split(" ", 1)[1].strip()
    if not token:
        raise HTTPException(status_code=401, detail="missing bearer token")
    return token


# ---------------------------------------------------------------------------
# Role extraction helpers
# ---------------------------------------------------------------------------

def _allowed_roles() -> set[str]:
    return {r.strip().lower() for r in settings.CONSOLE_ALLOWED_ROLES.split(",") if r.strip()}


def _allowed_audiences() -> set[str]:
    return {aud.strip() for aud in settings.KEYCLOAK_AUDIENCE.split(",") if aud.strip()}


def _claim_roles(value: object) -> set[str]:
    if isinstance(value, list):
        return {str(item).strip().lower() for item in value if str(item).strip()}
    return set()


def _extract_roles(claims: dict) -> set[str]:
    roles: set[str] = set()
    roles.update(_claim_roles(claims.get("roles")))

    realm_access = claims.get("realm_access")
    if isinstance(realm_access, dict):
        roles.update(_claim_roles(realm_access.get("roles")))

    resource_access = claims.get("resource_access")
    if isinstance(resource_access, dict):
        for client_data in resource_access.values():
            if isinstance(client_data, dict):
                roles.update(_claim_roles(client_data.get("roles")))

    return roles


def _claim_audiences(claims: dict) -> set[str]:
    aud = claims.get("aud")
    if isinstance(aud, str):
        return {aud.strip()} if aud.strip() else set()
    if isinstance(aud, list):
        return {str(item).strip() for item in aud if str(item).strip()}
    return set()


# ---------------------------------------------------------------------------
# Core verification
# ---------------------------------------------------------------------------

def _verify_token(token: str) -> dict:
    """Verify signature via JWKS, check aud+iss, return decoded claims."""
    jwks = _get_jwks()
    try:
        claims = jwt.decode(
            token,
            jwks,
            algorithms=["RS256"],
            issuer=_expected_issuer(),
            options={
                "verify_exp": True,
                "verify_aud": False,
                "verify_iss": True,
            },
        )
        allowed_audiences = _allowed_audiences()
        if allowed_audiences and not _claim_audiences(claims).intersection(allowed_audiences):
            raise HTTPException(status_code=401, detail="invalid token: audience mismatch")
        return claims
    except JWTError as exc:
        raise HTTPException(status_code=401, detail=f"invalid token: {exc}") from exc


def _fetch_userinfo(token: str) -> dict:
    """Fetch fresh userinfo from Keycloak (authoritative for roles/claims)."""
    userinfo_url = (
        f"{settings.KEYCLOAK_URL}/realms/{settings.KEYCLOAK_REALM}"
        "/protocol/openid-connect/userinfo"
    )
    try:
        r = httpx.get(userinfo_url, headers={"Authorization": f"Bearer {token}"}, timeout=10)
    except httpx.RequestError:
        raise HTTPException(status_code=503, detail="keycloak unreachable")

    if r.status_code == 401:
        raise HTTPException(status_code=401, detail="invalid or expired token")
    if not r.is_success:
        raise HTTPException(status_code=503, detail="keycloak returned an error")

    return r.json()


def _require_console_roles(
    *,
    authorization: str | None,
    required_roles: set[str],
    error_message: str,
) -> dict:
    token = _extract_bearer_token(authorization)

    # 1. Verify signature + aud locally (fast, no network round-trip on cache hit)
    claims = _verify_token(token)

    # 2. Fetch userinfo for authoritative role info
    userinfo = _fetch_userinfo(token)

    # 3. Check roles. Userinfo is useful for fresh profile data, but it often
    # does not include realm/client roles unless dedicated mappers are enabled.
    # Fall back to the already verified access-token claims so authenticated
    # home/console sessions are not rejected incorrectly.
    roles = _extract_roles(claims) | _extract_roles(userinfo)
    if not roles.intersection(required_roles):
        raise HTTPException(status_code=403, detail=error_message)

    userinfo["roles"] = sorted(roles)
    return userinfo


def require_verified_token(authorization: str | None = Header(default=None)) -> dict:
    """Require a Bearer token and verify it against JWKS, aud and iss."""
    token = _extract_bearer_token(authorization)
    return _verify_token(token)


# ---------------------------------------------------------------------------
# Public guards
# ---------------------------------------------------------------------------

# Read access: new roles (pcw_admin, pcw_console, pcw_support) UNION legacy roles (owner, admin).
# CONSOLE_ALLOWED_ROLES env var adds canonical roles; we extend with legacy set.
_CONSOLE_USER_ROLES: frozenset[str] = frozenset({"pcw_admin", "pcw_console", "pcw_support", "owner", "admin"})

# Write/action access: admin-tier roles only (pcw_console is READ-ONLY).
_CONSOLE_OWNER_ROLES: frozenset[str] = frozenset({"pcw_admin", "owner", "admin"})


def require_console_user(authorization: str | None = Header(default=None)) -> dict:
    """Read access: pcw_admin | pcw_console | pcw_support | owner | admin."""
    # Merge env-configured roles with hardcoded legacy roles.
    allowed = _allowed_roles() | _CONSOLE_USER_ROLES
    return _require_console_roles(
        authorization=authorization,
        required_roles=allowed,
        error_message="insufficient role (require pcw_admin, pcw_console, pcw_support, owner, or admin)",
    )


def require_console_owner(authorization: str | None = Header(default=None)) -> dict:
    """Write/action access: pcw_admin | owner | admin. pcw_console is read-only."""
    return _require_console_roles(
        authorization=authorization,
        required_roles=_CONSOLE_OWNER_ROLES,
        error_message="insufficient role (require pcw_admin, owner, or admin)",
    )


def require_home_user(authorization: str | None = Header(default=None)) -> dict:
    """Accepts any authenticated Keycloak user (pcw_user, pcw_admin, etc.)."""
    return _require_console_roles(
        authorization=authorization,
        required_roles={"pcw_user", "pcw_admin", "pcw_console", "pcw_support"},
        error_message="authenticated user required",
    )
