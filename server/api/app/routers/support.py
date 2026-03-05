from __future__ import annotations

import base64
import hashlib
import json
import logging
from datetime import datetime, timezone
from typing import Any, Dict, List, Optional

import httpx
from fastapi import APIRouter, Depends, File, Header, HTTPException, Query, Request, UploadFile
from pydantic import BaseModel, ConfigDict, Field
from slowapi import Limiter
from slowapi.util import get_remote_address
from sqlalchemy import select
from sqlalchemy.orm import Session

# require_home_user : pcw_user | pcw_admin | pcw_console | pcw_support
# require_console_owner: pcw_admin | owner | admin  (write/admin access)
from ..db import get_db
from ..models import WebhookEventV2
from ..security_jwt import require_console_owner, require_home_user
from ..settings import settings

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/support", tags=["support"])
limiter = Limiter(key_func=get_remote_address)

# ---------------------------------------------------------------------------
# Elevated roles – admin/support tier (not just pcw_user)
# ---------------------------------------------------------------------------
_ELEVATED_ROLES: frozenset[str] = frozenset(
    {"pcw_admin", "pcw_console", "pcw_support", "owner", "admin"}
)


def _is_elevated(user: dict) -> bool:
    """True when user carries any admin/support role (not just pcw_user)."""
    roles = {r.lower() for r in (user.get("roles") or [])}
    return bool(roles & _ELEVATED_ROLES)


def _utcnow() -> datetime:
    return datetime.now(timezone.utc)


# ---------------------------------------------------------------------------
# Zammad helpers
# ---------------------------------------------------------------------------

def _zammad_headers() -> dict:
    if not settings.ZAMMAD_API_TOKEN:
        raise HTTPException(status_code=503, detail="support_not_configured")
    return {
        "Authorization": f"Token token={settings.ZAMMAD_API_TOKEN}",
        "Content-Type": "application/json",
    }


def _require_zammad_configured() -> None:
    if not settings.ZAMMAD_BASE_URL:
        raise HTTPException(status_code=503, detail="support_not_configured")


def _raise_zammad_upstream_unreachable(operation: str, exc: httpx.RequestError) -> None:
    logger.warning("Zammad request error during %s: %s", operation, exc)
    raise HTTPException(
        status_code=502,
        detail=f"zammad_unreachable during {operation}",
    ) from exc


def _extract_email(user: dict) -> str:
    """Extract email from Keycloak userinfo dict. Raises 400 if missing."""
    email = (user.get("email") or "").strip()
    if not email:
        raise HTTPException(status_code=400, detail="user_email_missing")
    return email


async def _find_zammad_user(email: str, client: httpx.AsyncClient) -> int | None:
    """
    Search Zammad for a user by email.
    Returns Zammad user.id or None.
    NEVER creates a user — read-only lookup only.

    Equivalent curl:
      curl -s "$ZAMMAD_BASE_URL/api/v1/users/search?query=<email>" \\
           -H "Authorization: Token token=$ZAMMAD_API_TOKEN"
    """
    try:
        r = await client.get(
            f"{settings.ZAMMAD_BASE_URL}/api/v1/users/search",
            params={"query": email},
            headers=_zammad_headers(),
        )
    except httpx.RequestError as exc:
        _raise_zammad_upstream_unreachable("users/search", exc)
    if r.status_code >= 400:
        raise HTTPException(
            status_code=502,
            detail=f"zammad_user_search_failed: {r.status_code}",
        )
    results = r.json()
    if isinstance(results, list) and results:
        return int(results[0]["id"])
    return None


# Zammad search query field varies by version/config.
# (a) customer_id:<id>  — most common
# (b) customer.id:<id>  — some Elasticsearch-backed installs
_SEARCH_QUERY_FIELDS = ("customer_id", "customer.id")


async def _search_own_tickets(
    customer_id: int,
    client: httpx.AsyncClient,
    page: int,
    per_page: int,
) -> Any:
    """
    Search Zammad tickets for customer_id via GET /api/v1/tickets/search.

    Tries two query-field variants for cross-version compatibility:
      (a) customer_id:<id>
      (b) customer.id:<id>
    Returns the raw Zammad response on success (list or search-result dict).
    Raises HTTP 502 with actionable message if both query variants are rejected.

    Unknown: exact Zammad search response schema varies — may be a list of ticket
    objects, or {"tickets": [id,...], "assets": {"Ticket": {id: {...}}}} depending
    on Zammad version. Verify with: GET /api/v1/support/admin/diag/zammad-roles
    to confirm Zammad connectivity, then run a manual search curl (see smoke-tests.md).
    """
    headers = _zammad_headers()
    last_status: int = 0
    search_endpoint_missing = False

    for field in _SEARCH_QUERY_FIELDS:
        try:
            r = await client.get(
                f"{settings.ZAMMAD_BASE_URL}/api/v1/tickets/search",
                params={
                    "query": f"{field}:{customer_id}",
                    "page": page,
                    "per_page": per_page,
                },
                headers=headers,
            )
        except httpx.RequestError as exc:
            _raise_zammad_upstream_unreachable("tickets/search", exc)
        if r.status_code in (404, 405):
            search_endpoint_missing = True
            break
        if r.status_code in (400, 422):
            last_status = r.status_code
            logger.debug(
                "Zammad search field '%s:%s' rejected (%s), trying next",
                field, customer_id, r.status_code,
            )
            continue
        if r.status_code >= 400:
            raise HTTPException(
                status_code=502,
                detail=(
                    f"zammad_search_failed: {r.status_code} – "
                    "check GET /api/v1/support/admin/diag/zammad-roles for Zammad connectivity"
                ),
            )
        return r.json()

    if search_endpoint_missing:
        # Fallback for installations where /tickets/search is unavailable.
        # Unknown: /users/{id}/tickets is version-dependent and may not support pagination.
        try:
            fallback = await client.get(
                f"{settings.ZAMMAD_BASE_URL}/api/v1/users/{customer_id}/tickets",
                params={"page": page, "per_page": per_page},
                headers=headers,
            )
        except httpx.RequestError as exc:
            _raise_zammad_upstream_unreachable("users/{id}/tickets(fallback)", exc)
        if fallback.status_code >= 400:
            raise HTTPException(
                status_code=502,
                detail=(
                    "zammad_tickets_search_unavailable_and_fallback_failed: "
                    f"{fallback.status_code}"
                ),
            )
        return fallback.json()

    # Both query fields rejected
    raise HTTPException(
        status_code=502,
        detail=(
            f"zammad_search_query_unsupported (last_status={last_status}) – "
            "both 'customer_id:<id>' and 'customer.id:<id>' were rejected; "
            "verify Zammad version/config via GET /api/v1/support/admin/diag/zammad-roles"
        ),
    )


# ---------------------------------------------------------------------------
# Ticket endpoints
# ---------------------------------------------------------------------------

class TicketCreateIn(BaseModel):
    title: str = Field(..., max_length=200)
    body: str = Field(..., max_length=5000)


class TicketAttachmentIn(BaseModel):
    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    filename: str = Field(..., min_length=1, max_length=255)
    data: str = Field(..., min_length=1)
    mime_type: str = Field(default="application/octet-stream", alias="mime-type", max_length=255)


class TicketReplyIn(BaseModel):
    body: str = Field(..., min_length=1, max_length=10000)
    content_type: str = Field(default="text/plain", pattern="^(text/plain|text/html)$")
    internal: bool = False
    attachments: list[TicketAttachmentIn] = Field(default_factory=list)


def _parse_int_query_param(name: str, value: str, minimum: int, maximum: int | None = None) -> int:
    try:
        parsed = int(value)
    except (TypeError, ValueError) as exc:
        raise HTTPException(status_code=400, detail=f"{name}_invalid") from exc

    if parsed < minimum:
        raise HTTPException(status_code=400, detail=f"{name}_invalid")
    if maximum is not None and parsed > maximum:
        raise HTTPException(status_code=400, detail=f"{name}_invalid")
    return parsed


@router.get("/tickets")
async def list_tickets(
    page: str = Query("1", description="Page number (1-based, min 1)"),
    per_page: str = Query("50", description="Results per page (1..200)"),
    all: bool = Query(False, description="Admin only: return all tickets, not scoped to caller"),
    _user: dict = Depends(require_home_user),
) -> Any:
    """
    List tickets.

    - Any authenticated user (incl. pcw_user): returns own tickets only.
      Uses GET /api/v1/tickets/search?query=customer_id:<id> with fallback to
      customer.id:<id> for cross-version compat.
      If caller has no Zammad account yet: returns [] (no user creation side-effect).
    - Elevated (pcw_admin/pcw_support/pcw_console/owner/admin) + ?all=true:
      returns all tickets via GET /api/v1/tickets (no scope filter).
    - pcw_user + ?all=true: 403.
    """
    _require_zammad_configured()
    page_num = _parse_int_query_param("page", page, minimum=1)
    per_page_num = _parse_int_query_param("per_page", per_page, minimum=1, maximum=200)
    elevated = _is_elevated(_user)

    if all and not elevated:
        raise HTTPException(status_code=403, detail="forbidden: all=true requires admin role")

    async with httpx.AsyncClient(timeout=20) as client:
        if all and elevated:
            # Admin unscoped path — full Zammad ticket list
            try:
                r = await client.get(
                    f"{settings.ZAMMAD_BASE_URL}/api/v1/tickets",
                    params={"page": page_num, "per_page": per_page_num},
                    headers=_zammad_headers(),
                )
            except httpx.RequestError as exc:
                _raise_zammad_upstream_unreachable("tickets(list all)", exc)
            if r.status_code >= 400:
                raise HTTPException(status_code=502, detail=f"zammad_error: {r.status_code}")
            return r.json()

        # Scoped path: find caller's Zammad user — NO user creation
        email = _extract_email(_user)
        customer_id = await _find_zammad_user(email, client)
        if customer_id is None:
            # User has no Zammad account yet; no tickets possible
            return []

        return await _search_own_tickets(customer_id, client, page_num, per_page_num)


@router.post("/tickets")
async def create_ticket(
    payload: TicketCreateIn,
    _user: dict = Depends(require_home_user),
) -> Any:
    """
    Create a support ticket.

    Uses Zammad's "customer": "<email>" field directly — Zammad resolves or
    provisions the customer internally. No manual user-create, no roles lookup.
    article.internal is explicitly false so the customer can read the article.
    """
    _require_zammad_configured()
    email = _extract_email(_user)

    async with httpx.AsyncClient(timeout=20) as client:
        try:
            r = await client.post(
                f"{settings.ZAMMAD_BASE_URL}/api/v1/tickets",
                headers=_zammad_headers(),
                json={
                    "title": payload.title,
                    "group_id": settings.ZAMMAD_DEFAULT_GROUP_ID,
                    "customer": email,
                    "article": {
                        "subject": payload.title,
                        "body": payload.body,
                        "type": "note",
                        "internal": False,
                    },
                },
            )
        except httpx.RequestError as exc:
            _raise_zammad_upstream_unreachable("tickets(create)", exc)
    if r.status_code >= 400:
        raise HTTPException(status_code=502, detail=f"zammad_error: {r.status_code}")
    return r.json()


@router.get("/tickets/{ticket_id}")
async def get_ticket(
    ticket_id: int,
    _user: dict = Depends(require_home_user),
) -> Any:
    """
    Get a single ticket.

    - pcw_user: 404 if ticket.customer_id != own Zammad user ID (no info leak).
      If caller has no Zammad account: 404 (no user creation).
    - Elevated roles: no ownership check.
    """
    _require_zammad_configured()
    elevated = _is_elevated(_user)

    async with httpx.AsyncClient(timeout=20) as client:
        try:
            r = await client.get(
                f"{settings.ZAMMAD_BASE_URL}/api/v1/tickets/{ticket_id}",
                headers=_zammad_headers(),
            )
        except httpx.RequestError as exc:
            _raise_zammad_upstream_unreachable("tickets(detail)", exc)
        if r.status_code == 404:
            raise HTTPException(status_code=404, detail="ticket_not_found")
        if r.status_code >= 400:
            raise HTTPException(status_code=502, detail=f"zammad_error: {r.status_code}")

        ticket = r.json()

        if not elevated:
            # Ownership check — find Zammad user (no create)
            email = _extract_email(_user)
            customer_id = await _find_zammad_user(email, client)
            if customer_id is None:
                # No Zammad account → can't own any ticket
                raise HTTPException(status_code=404, detail="ticket_not_found")
            ticket_customer_id = ticket.get("customer_id")
            if ticket_customer_id is None or int(ticket_customer_id) != customer_id:
                # 404, not 403, to avoid leaking ticket existence
                raise HTTPException(status_code=404, detail="ticket_not_found")

    return ticket


@router.post("/tickets/{ticket_id}/reply")
async def reply_ticket(
    ticket_id: int,
    payload: TicketReplyIn,
    _user: dict = Depends(require_home_user),
) -> Any:
    """Reply to a ticket via Zammad ticket_articles (supports optional attachments)."""
    _require_zammad_configured()
    elevated = _is_elevated(_user)

    async with httpx.AsyncClient(timeout=20) as client:
        try:
            detail = await client.get(
                f"{settings.ZAMMAD_BASE_URL}/api/v1/tickets/{ticket_id}",
                headers=_zammad_headers(),
            )
        except httpx.RequestError as exc:
            _raise_zammad_upstream_unreachable("tickets(detail for reply)", exc)
        if detail.status_code == 404:
            raise HTTPException(status_code=404, detail="ticket_not_found")
        if detail.status_code >= 400:
            raise HTTPException(status_code=502, detail=f"zammad_error: {detail.status_code}")

        ticket = detail.json()
        if not elevated:
            email = _extract_email(_user)
            customer_id = await _find_zammad_user(email, client)
            if customer_id is None:
                raise HTTPException(status_code=404, detail="ticket_not_found")

            ticket_customer_id = ticket.get("customer_id")
            if ticket_customer_id is None or int(ticket_customer_id) != customer_id:
                raise HTTPException(status_code=404, detail="ticket_not_found")

        article_payload: dict[str, Any] = {
            "ticket_id": ticket_id,
            "body": payload.body,
            "content_type": payload.content_type,
            "type": "note",
            "internal": bool(payload.internal),
        }
        if payload.attachments:
            article_payload["attachments"] = [attachment.model_dump(by_alias=True) for attachment in payload.attachments]

        try:
            response = await client.post(
                f"{settings.ZAMMAD_BASE_URL}/api/v1/ticket_articles",
                headers=_zammad_headers(),
                json=article_payload,
            )
        except httpx.RequestError as exc:
            _raise_zammad_upstream_unreachable("ticket_articles(reply)", exc)

    if response.status_code >= 400:
        raise HTTPException(status_code=502, detail=f"zammad_error: {response.status_code}")
    return response.json()


@router.post("/attachments")
@limiter.limit("120/minute")
async def upload_attachment(
    request: Request,
    file: UploadFile = File(...),
    _user: dict = Depends(require_home_user),
) -> Dict[str, Any]:
    """Upload helper: returns a base64 attachment object compatible with ticket_articles."""
    max_size = max(1, int(settings.SUPPORT_ATTACHMENT_MAX_BYTES))
    raw = await file.read(max_size + 1)
    if len(raw) > max_size:
        raise HTTPException(status_code=413, detail="attachment_too_large")
    if not raw:
        raise HTTPException(status_code=400, detail="empty_attachment")

    filename = (file.filename or "attachment.bin").strip() or "attachment.bin"
    mime_type = (file.content_type or "application/octet-stream").strip() or "application/octet-stream"

    return {
        "filename": filename,
        "data": base64.b64encode(raw).decode("ascii"),
        "mime-type": mime_type,
        "size": len(raw),
    }


# ---------------------------------------------------------------------------
# Admin diag endpoints (require_console_owner: pcw_admin | owner | admin)
# ---------------------------------------------------------------------------

@router.get("/admin/diag/zammad-roles")
async def diag_zammad_roles(
    _user: dict = Depends(require_console_owner),
) -> List[dict]:
    """
    Admin-only: list all Zammad roles (id, name, active).

    Use this to verify whether role ID 3 is "Customer" on this Zammad instance.
    ZAMMAD_CUSTOMER_ROLE_ID is informational only and no longer used in the
    self-service flow; this endpoint exists for operational verification.

    Equivalent curl:
      curl -s "$ZAMMAD_BASE_URL/api/v1/roles" \\
           -H "Authorization: Token token=$ZAMMAD_API_TOKEN" \\
           | jq '[.[] | {id, name, active}]'

    Unknown: Zammad GET /api/v1/roles requires admin-level API token.
    If this returns 403, your ZAMMAD_API_TOKEN lacks admin scope.
    """
    _require_zammad_configured()
    async with httpx.AsyncClient(timeout=20) as client:
        try:
            r = await client.get(
                f"{settings.ZAMMAD_BASE_URL}/api/v1/roles",
                headers=_zammad_headers(),
            )
        except httpx.RequestError as exc:
            _raise_zammad_upstream_unreachable("roles(diag)", exc)
    if r.status_code >= 400:
        raise HTTPException(
            status_code=502,
            detail=f"zammad_roles_lookup_failed: {r.status_code}",
        )
    roles = r.json()
    if not isinstance(roles, list):
        raise HTTPException(status_code=502, detail="zammad_roles_unexpected_response")
    return [
        {"id": role.get("id"), "name": role.get("name"), "active": role.get("active")}
        for role in roles
        if isinstance(role, dict)
    ]


@router.get("/admin/diag/zammad-user")
async def diag_zammad_user(
    email: str = Query(..., min_length=3, max_length=254),
    _user: dict = Depends(require_console_owner),
) -> dict:
    """
    Admin-only: search Zammad for a user by email.

    Shows whether users/search matches for a given email and what customer_id
    would be used for ticket scoping. Same lookup as GET /tickets (own scope).

    Equivalent curl:
      curl -s "$ZAMMAD_BASE_URL/api/v1/users/search?query=<email>" \\
           -H "Authorization: Token token=$ZAMMAD_API_TOKEN" \\
           | jq '.[0] | {id, email, login, role_ids}'
    """
    _require_zammad_configured()
    async with httpx.AsyncClient(timeout=20) as client:
        try:
            r = await client.get(
                f"{settings.ZAMMAD_BASE_URL}/api/v1/users/search",
                params={"query": email},
                headers=_zammad_headers(),
            )
        except httpx.RequestError as exc:
            _raise_zammad_upstream_unreachable("users/search(diag)", exc)
    if r.status_code >= 400:
        raise HTTPException(
            status_code=502,
            detail=f"zammad_user_search_failed: {r.status_code}",
        )
    results = r.json()
    if not isinstance(results, list) or not results:
        return {"found": False, "email": email}
    first = results[0]
    return {
        "found": True,
        "id": first.get("id"),
        "email": first.get("email"),
        "login": first.get("login"),
        "role_ids": first.get("role_ids"),
    }


# ---------------------------------------------------------------------------
# Zammad webhook (no auth — verified via shared secret)
# ---------------------------------------------------------------------------

@router.post("/webhook")
@limiter.limit("120/minute")
async def zammad_webhook(
    request: Request,
    x_zammad_secret: Optional[str] = Header(None),
    db: Session = Depends(get_db),
) -> Dict[str, bool]:
    """Inbound webhook from Zammad. Verified via shared secret."""
    if not settings.ZAMMAD_WEBHOOK_SECRET:
        raise HTTPException(status_code=500, detail="server_not_configured")
    if x_zammad_secret != settings.ZAMMAD_WEBHOOK_SECRET:
        raise HTTPException(status_code=401, detail="invalid_webhook_secret")

    payload_bytes = await request.body()
    event_id = (
        request.headers.get("x-zammad-event-id")
        or request.headers.get("x-request-id")
        or hashlib.sha256(payload_bytes).hexdigest()
    )
    event_type = (
        request.headers.get("x-zammad-event")
        or request.headers.get("x-zammad-event-type")
        or "zammad.webhook"
    )

    payload: dict[str, Any] | None
    if not payload_bytes:
        payload = {}
    else:
        try:
            parsed = json.loads(payload_bytes.decode("utf-8"))
            payload = parsed if isinstance(parsed, dict) else {"data": parsed}
        except Exception:
            payload = {"raw": payload_bytes.decode("utf-8", errors="replace")}

    existing = db.execute(
        select(WebhookEventV2).where(
            WebhookEventV2.source == "zammad",
            WebhookEventV2.event_id == event_id,
        )
    ).scalar_one_or_none()
    if existing:
        return {"ok": True}

    db.add(
        WebhookEventV2(
            source="zammad",
            event_id=event_id,
            event_type=event_type,
            payload=payload,
            received_at=_utcnow(),
            processed_at=_utcnow(),
            status="ok",
            error=None,
        )
    )
    db.commit()
    return {"ok": True}
