from __future__ import annotations

import hashlib
import html
import json
import logging
import re
import uuid
from typing import Any, Dict, List, Optional

import httpx
from fastapi import APIRouter, Depends, File, Header, HTTPException, Query, Request, UploadFile
from pydantic import BaseModel, ConfigDict, Field
from slowapi import Limiter
from slowapi.util import get_remote_address
from sqlalchemy import select
from sqlalchemy.orm import Session

from ..db import get_db
from ..models import WebhookEventV2
from ..security_jwt import require_console_owner, require_console_user, require_home_user
from ..settings import settings
from ..services.support_service import (
    build_support_profile,
    get_effective_support_settings,
    get_or_create_support_settings,
    load_zammad_catalog,
    load_zammad_customer_details,
    mark_attachments_consumed,
    materialize_stored_attachments,
    normalize_name,
    process_zammad_webhook_event,
    raise_zammad_unreachable,
    require_zammad_configured,
    resolve_or_create_zammad_customer_id,
    save_support_attachment,
    should_block_customer_support_action,
    support_attachment_payload,
    verify_zammad_webhook_hmac,
    support_settings_payload,
    utcnow,
    zammad_base_url,
    zammad_error_message,
    zammad_headers,
)

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/support", tags=["support"])
limiter = Limiter(key_func=get_remote_address)

_ELEVATED_ROLES: frozenset[str] = frozenset(
    {"pcw_admin", "pcw_console", "pcw_support", "owner", "admin"}
)
_SEARCH_QUERY_FIELDS = ("customer_id", "customer.id")
_HTML_TAG_RE = re.compile(r"<[^>]+>")


def _is_elevated(user: dict) -> bool:
    roles = {r.lower() for r in (user.get("roles") or [])}
    return bool(roles & _ELEVATED_ROLES)


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


def _clean_article_body(body: str, content_type: str | None) -> str:
    raw = body or ""
    if content_type and "html" in content_type:
        normalized = raw.replace("<br>", "\n").replace("<br/>", "\n").replace("<br />", "\n")
        normalized = normalized.replace("</p>", "\n\n").replace("</li>", "\n")
        normalized = _HTML_TAG_RE.sub("", normalized)
        normalized = html.unescape(normalized)
        return normalized.strip()
    return raw.strip()


async def _search_own_tickets(
    customer_id: int,
    client: httpx.AsyncClient,
    page: int,
    per_page: int,
) -> Any:
    headers = zammad_headers()
    last_status: int = 0
    search_endpoint_missing = False

    for field in _SEARCH_QUERY_FIELDS:
        try:
            response = await client.get(
                f"{zammad_base_url()}/api/v1/tickets/search",
                params={
                    "query": f"{field}:{customer_id}",
                    "page": page,
                    "per_page": per_page,
                },
                headers=headers,
            )
        except httpx.RequestError as exc:
            raise_zammad_unreachable("tickets/search", exc)
        if response.status_code in (404, 405):
            search_endpoint_missing = True
            break
        if response.status_code in (400, 422):
            last_status = response.status_code
            logger.debug(
                "Zammad search field '%s:%s' rejected (%s), trying next",
                field,
                customer_id,
                response.status_code,
            )
            continue
        if response.status_code >= 400:
            raise HTTPException(
                status_code=502,
                detail=(
                    f"zammad_search_failed: {response.status_code} – "
                    "check GET /api/v1/support/admin/diag/zammad-roles for Zammad connectivity"
                ),
            )
        return response.json()

    if search_endpoint_missing:
        batch_size = 100
        target_count = page * per_page
        matched_rows: list[dict[str, Any]] = []
        fallback_page = 1

        while len(matched_rows) < target_count:
            try:
                fallback = await client.get(
                    f"{zammad_base_url()}/api/v1/tickets",
                    params={"page": fallback_page, "per_page": batch_size},
                    headers=headers,
                )
            except httpx.RequestError as exc:
                raise_zammad_unreachable("tickets/index(fallback)", exc)
            if fallback.status_code >= 400:
                raise HTTPException(
                    status_code=502,
                    detail=(
                        "zammad_tickets_search_unavailable_and_fallback_failed: "
                        f"{fallback.status_code}"
                    ),
                )
            payload = fallback.json()
            if not isinstance(payload, list):
                raise HTTPException(status_code=502, detail="zammad_tickets_fallback_unexpected_response")
            if not payload:
                break

            for row in payload:
                if not isinstance(row, dict):
                    continue
                try:
                    row_customer_id = int(row.get("customer_id"))
                except (TypeError, ValueError):
                    continue
                if row_customer_id == customer_id:
                    matched_rows.append(row)

            if len(payload) < batch_size:
                break
            fallback_page += 1

        start = max(0, (page - 1) * per_page)
        end = start + per_page
        return matched_rows[start:end]

    raise HTTPException(
        status_code=502,
        detail=(
            f"zammad_search_query_unsupported (last_status={last_status}) – "
            "both 'customer_id:<id>' and 'customer.id:<id>' were rejected; "
            "verify Zammad version/config via GET /api/v1/support/admin/diag/zammad-roles"
        ),
    )


async def _load_ticket(ticket_id: int, client: httpx.AsyncClient) -> dict[str, Any]:
    try:
        response = await client.get(
            f"{zammad_base_url()}/api/v1/tickets/{ticket_id}",
            headers=zammad_headers(),
        )
    except httpx.RequestError as exc:
        raise_zammad_unreachable("tickets/detail", exc)

    if response.status_code == 404:
        raise HTTPException(status_code=404, detail="ticket_not_found")
    if response.status_code >= 400:
        raise HTTPException(status_code=502, detail=f"zammad_error: {response.status_code}")

    payload = response.json()
    if not isinstance(payload, dict):
        raise HTTPException(status_code=502, detail="zammad_ticket_unexpected_response")
    return payload


async def _load_ticket_articles(ticket_id: int, client: httpx.AsyncClient) -> list[dict[str, Any]]:
    try:
        response = await client.get(
            f"{zammad_base_url()}/api/v1/ticket_articles",
            params={"ticket_id": ticket_id},
            headers=zammad_headers(),
        )
    except httpx.RequestError as exc:
        raise_zammad_unreachable("ticket_articles/list", exc)

    if response.status_code >= 400:
        raise HTTPException(status_code=502, detail=f"zammad_error: {response.status_code}")

    payload = response.json()
    if not isinstance(payload, list):
        raise HTTPException(status_code=502, detail="zammad_ticket_articles_unexpected_response")
    return [row for row in payload if isinstance(row, dict)]


def _catalog_name_map(rows: list[dict[str, Any]], *, fallback_key: str = "name") -> dict[int, str]:
    mapping: dict[int, str] = {}
    for row in rows:
        item_id = row.get("id")
        if item_id is None:
            continue
        try:
            normalized_id = int(item_id)
        except (TypeError, ValueError):
            continue
        label = normalize_name(row.get(fallback_key) or row.get("name")) or str(normalized_id)
        mapping[normalized_id] = label
    return mapping


def _format_customer_name(customer: dict[str, Any] | None) -> tuple[str | None, str | None]:
    if not customer:
        return None, None
    first_name = normalize_name(customer.get("firstname"))
    last_name = normalize_name(customer.get("lastname"))
    email = normalize_name(customer.get("email"))
    if first_name or last_name:
        return " ".join(part for part in [first_name, last_name] if part), email
    return email, email


def _normalize_ticket_detail(
    ticket: dict[str, Any],
    articles: list[dict[str, Any]],
    catalog: dict[str, list[dict[str, Any]]],
    customer: dict[str, Any] | None,
) -> dict[str, Any]:
    articles_sorted = sorted(articles, key=lambda row: str(row.get("created_at") or ""))
    first_article = articles_sorted[0] if articles_sorted else None
    description = ""
    if first_article is not None:
        description = _clean_article_body(
            str(first_article.get("body") or ""),
            normalize_name(first_article.get("content_type")),
        )

    replies = []
    for article in articles_sorted[1:]:
        replies.append(
            {
                "id": str(article.get("id") or ""),
                "body": _clean_article_body(
                    str(article.get("body") or ""),
                    normalize_name(article.get("content_type")),
                ),
                "author": normalize_name(article.get("created_by") or article.get("from")) or "Unbekannt",
                "created_at": article.get("created_at"),
                "internal": bool(article.get("internal")),
            }
        )

    attachments = []
    for article in articles_sorted:
        for index, attachment in enumerate(article.get("attachments") or []):
            if not isinstance(attachment, dict):
                continue
            filename = normalize_name(attachment.get("filename") or attachment.get("name")) or f"attachment-{index + 1}"
            size = attachment.get("size")
            try:
                normalized_size = int(size) if size is not None else 0
            except (TypeError, ValueError):
                normalized_size = 0
            attachments.append(
                {
                    "id": f"{article.get('id')}-{index}",
                    "filename": filename,
                    "size": normalized_size,
                    "created_at": article.get("created_at"),
                }
            )

    state_name_by_id = _catalog_name_map(catalog.get("states", []))
    priority_name_by_id = _catalog_name_map(catalog.get("priorities", []))
    customer_name, customer_email = _format_customer_name(customer)

    return {
        "id": str(ticket.get("id") or ""),
        "subject": normalize_name(ticket.get("title")) or "Ohne Betreff",
        "state": state_name_by_id.get(int(ticket.get("state_id") or 0), str(ticket.get("state_id") or "")),
        "priority": priority_name_by_id.get(int(ticket.get("priority_id") or 0), str(ticket.get("priority_id") or "")),
        "created_at": ticket.get("created_at"),
        "updated_at": ticket.get("updated_at"),
        "customer_name": customer_name,
        "customer_email": customer_email,
        "description": description,
        "replies": replies,
        "attachments": attachments,
    }


class TicketAttachmentIn(BaseModel):
    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    filename: str = Field(..., min_length=1, max_length=255)
    data: str = Field(..., min_length=1)
    mime_type: str = Field(default="application/octet-stream", alias="mime-type", max_length=255)


class TicketCreateIn(BaseModel):
    title: str = Field(..., max_length=200)
    body: str = Field(..., max_length=5000)
    group_id: int | None = None
    attachment_ids: list[uuid.UUID] = Field(default_factory=list)
    attachments: list[TicketAttachmentIn] = Field(default_factory=list)


class TicketReplyIn(BaseModel):
    body: str = Field(..., min_length=1, max_length=10000)
    content_type: str = Field(default="text/plain", pattern="^(text/plain|text/html)$")
    internal: bool = False
    attachment_ids: list[uuid.UUID] = Field(default_factory=list)
    attachments: list[TicketAttachmentIn] = Field(default_factory=list)


class SupportSettingsUpdateIn(BaseModel):
    allow_customer_group_selection: bool = False
    customer_visible_group_ids: list[int] = Field(default_factory=list)
    default_group_id: int | None = None
    default_priority_id: int | None = None
    uploads_enabled: bool = True
    uploads_max_bytes: int = Field(..., ge=1)
    maintenance_mode: bool = False
    maintenance_message: str | None = Field(default=None, max_length=1000)


def _materialize_inline_attachments(attachments: list[TicketAttachmentIn]) -> list[dict[str, Any]]:
    return [attachment.model_dump(by_alias=True) for attachment in attachments]


def _resolve_ticket_group_id(
    *,
    requested_group_id: int | None,
    config: dict[str, Any],
    elevated: bool,
) -> int:
    default_group_id = config["default_group_id"]
    visible_group_ids = config["customer_visible_group_ids"]

    if elevated and requested_group_id is not None:
        return requested_group_id

    if requested_group_id is not None and config["allow_customer_group_selection"]:
        if requested_group_id in visible_group_ids:
            return requested_group_id

    if default_group_id is None:
        raise HTTPException(status_code=503, detail="support_not_configured")
    return default_group_id


@router.get("/tickets")
async def list_tickets(
    page: str = Query("1", description="Page number (1-based, min 1)"),
    per_page: str = Query("50", description="Results per page (1..200)"),
    all: bool = Query(False, description="Admin only: return all tickets, not scoped to caller"),
    db: Session = Depends(get_db),
    _user: dict = Depends(require_home_user),
) -> Any:
    require_zammad_configured()
    page_num = _parse_int_query_param("page", page, minimum=1)
    per_page_num = _parse_int_query_param("per_page", per_page, minimum=1, maximum=200)
    elevated = _is_elevated(_user)

    if all and not elevated:
        raise HTTPException(status_code=403, detail="forbidden: all=true requires admin role")

    async with httpx.AsyncClient(timeout=20) as client:
        if all and elevated:
            try:
                response = await client.get(
                    f"{zammad_base_url()}/api/v1/tickets",
                    params={"page": page_num, "per_page": per_page_num},
                    headers=zammad_headers(),
                )
            except httpx.RequestError as exc:
                raise_zammad_unreachable("tickets(list all)", exc)
            if response.status_code >= 400:
                raise HTTPException(status_code=502, detail=f"zammad_error: {response.status_code}")
            return response.json()

        profile = await build_support_profile(_user)
        customer_id = await resolve_or_create_zammad_customer_id(
            db=db,
            profile=profile,
            client=client,
            create_if_missing=False,
        )
        if customer_id is None:
            return []

        return await _search_own_tickets(customer_id, client, page_num, per_page_num)


@router.post("/tickets")
async def create_ticket(
    payload: TicketCreateIn,
    db: Session = Depends(get_db),
    _user: dict = Depends(require_home_user),
) -> Any:
    require_zammad_configured()
    elevated = _is_elevated(_user)
    effective_config = get_effective_support_settings(db)
    config = support_settings_payload(effective_config)
    if should_block_customer_support_action(effective_config, elevated=elevated):
        raise HTTPException(status_code=503, detail=config["maintenance_message"])

    profile = await build_support_profile(_user)
    group_id = _resolve_ticket_group_id(
        requested_group_id=payload.group_id,
        config=config,
        elevated=elevated,
    )

    if payload.attachment_ids and not config["uploads_enabled"]:
        raise HTTPException(status_code=403, detail="support_uploads_disabled")

    async with httpx.AsyncClient(timeout=20) as client:
        customer_id = await resolve_or_create_zammad_customer_id(
            db=db,
            profile=profile,
            client=client,
            create_if_missing=True,
        )
        if customer_id is None:
            raise HTTPException(status_code=502, detail="support_customer_create_failed")

        stored_attachment_payloads, stored_rows = materialize_stored_attachments(
            db=db,
            attachment_ids=payload.attachment_ids,
            requester_user_id=profile.user_id,
            elevated=elevated,
        )
        attachments = [*_materialize_inline_attachments(payload.attachments), *stored_attachment_payloads]

        ticket_payload: dict[str, Any] = {
            "title": payload.title,
            "group_id": group_id,
            "customer_id": customer_id,
            "article": {
                "subject": payload.title,
                "body": payload.body,
                "type": "note",
                "internal": False,
            },
        }
        if config["default_priority_id"] is not None:
            ticket_payload["priority_id"] = config["default_priority_id"]
        if attachments:
            ticket_payload["article"]["attachments"] = attachments

        try:
            response = await client.post(
                f"{zammad_base_url()}/api/v1/tickets",
                headers=zammad_headers(),
                json=ticket_payload,
            )
        except httpx.RequestError as exc:
            raise_zammad_unreachable("tickets/create", exc)

    if response.status_code >= 400:
        detail = zammad_error_message(response)
        if response.status_code == 403:
            logger.warning("zammad ticket create forbidden for %s: %s", profile.email, detail)
            raise HTTPException(status_code=503, detail="support_not_configured")
        raise HTTPException(status_code=502, detail=f"support_ticket_create_failed: {detail}")

    data = response.json()
    if stored_rows:
        ticket_id = data.get("id") if isinstance(data, dict) else None
        mark_attachments_consumed(db, stored_rows, zammad_ticket_id=int(ticket_id) if ticket_id else None)
    return data


@router.get("/tickets/{ticket_id}")
async def get_ticket(
    ticket_id: int,
    db: Session = Depends(get_db),
    _user: dict = Depends(require_home_user),
) -> Any:
    require_zammad_configured()
    elevated = _is_elevated(_user)

    async with httpx.AsyncClient(timeout=20) as client:
        ticket = await _load_ticket(ticket_id, client)
        profile = await build_support_profile(_user)
        if not elevated:
            customer_id = await resolve_or_create_zammad_customer_id(
                db=db,
                profile=profile,
                client=client,
                create_if_missing=False,
            )
            if customer_id is None:
                raise HTTPException(status_code=404, detail="ticket_not_found")
            ticket_customer_id = ticket.get("customer_id")
            if ticket_customer_id is None or int(ticket_customer_id) != customer_id:
                raise HTTPException(status_code=404, detail="ticket_not_found")

        articles = await _load_ticket_articles(ticket_id, client)
        catalog = await load_zammad_catalog(client)
        customer = await load_zammad_customer_details(ticket.get("customer_id"), client)

    return _normalize_ticket_detail(ticket, articles, catalog, customer)


@router.post("/tickets/{ticket_id}/reply")
async def reply_ticket(
    ticket_id: int,
    payload: TicketReplyIn,
    db: Session = Depends(get_db),
    _user: dict = Depends(require_home_user),
) -> Any:
    require_zammad_configured()
    elevated = _is_elevated(_user)
    effective_config = get_effective_support_settings(db)
    if should_block_customer_support_action(effective_config, elevated=elevated):
        raise HTTPException(status_code=503, detail=effective_config.maintenance_message)
    if payload.attachment_ids and not effective_config.uploads_enabled:
        raise HTTPException(status_code=403, detail="support_uploads_disabled")

    profile = await build_support_profile(_user)
    async with httpx.AsyncClient(timeout=20) as client:
        ticket = await _load_ticket(ticket_id, client)
        if not elevated:
            customer_id = await resolve_or_create_zammad_customer_id(
                db=db,
                profile=profile,
                client=client,
                create_if_missing=False,
            )
            if customer_id is None:
                raise HTTPException(status_code=404, detail="ticket_not_found")

            ticket_customer_id = ticket.get("customer_id")
            if ticket_customer_id is None or int(ticket_customer_id) != customer_id:
                raise HTTPException(status_code=404, detail="ticket_not_found")

        stored_attachment_payloads, stored_rows = materialize_stored_attachments(
            db=db,
            attachment_ids=payload.attachment_ids,
            requester_user_id=profile.user_id,
            elevated=elevated,
        )
        attachments = [*_materialize_inline_attachments(payload.attachments), *stored_attachment_payloads]

        article_payload: dict[str, Any] = {
            "ticket_id": ticket_id,
            "body": payload.body,
            "content_type": payload.content_type,
            "type": "note",
            "internal": bool(payload.internal),
        }
        if attachments:
            article_payload["attachments"] = attachments

        try:
            response = await client.post(
                f"{zammad_base_url()}/api/v1/ticket_articles",
                headers=zammad_headers(),
                json=article_payload,
            )
        except httpx.RequestError as exc:
            raise_zammad_unreachable("ticket_articles(reply)", exc)

    if response.status_code >= 400:
        raise HTTPException(status_code=502, detail=f"zammad_error: {response.status_code}")

    data = response.json()
    if stored_rows:
        article_id = data.get("id") if isinstance(data, dict) else None
        mark_attachments_consumed(
            db,
            stored_rows,
            zammad_ticket_id=ticket_id,
            zammad_article_id=int(article_id) if article_id else None,
        )
    return data


@router.post("/attachments")
async def upload_attachment(
    file: UploadFile = File(...),
    db: Session = Depends(get_db),
    _user: dict = Depends(require_home_user),
) -> Dict[str, Any]:
    effective_config = get_effective_support_settings(db)
    elevated = _is_elevated(_user)
    if not effective_config.uploads_enabled:
        raise HTTPException(status_code=403, detail="support_uploads_disabled")
    if should_block_customer_support_action(effective_config, elevated=elevated):
        raise HTTPException(status_code=503, detail=effective_config.maintenance_message)

    profile = await build_support_profile(_user)
    row = await save_support_attachment(
        db=db,
        user_id=profile.user_id,
        file=file,
        max_size=effective_config.uploads_max_bytes,
    )
    return support_attachment_payload(row)


@router.get("/config")
async def get_support_config(
    db: Session = Depends(get_db),
    _user: dict = Depends(require_home_user),
) -> dict[str, Any]:
    config = get_effective_support_settings(db)
    payload = support_settings_payload(config)
    payload.pop("storage_root", None)
    payload.update(
        {
            "support_available": bool(settings.ZAMMAD_BASE_URL.strip() and settings.ZAMMAD_API_TOKEN.strip()),
            "groups": [],
            "zammad_reachable": False,
        }
    )

    if not payload["support_available"]:
        return payload

    try:
        async with httpx.AsyncClient(timeout=20) as client:
            catalog = await load_zammad_catalog(client)
    except HTTPException as exc:
        payload["zammad_error"] = str(exc.detail)
        return payload

    group_by_id = {
        int(group["id"]): group
        for group in catalog.get("groups", [])
        if group.get("id") is not None
    }
    payload["groups"] = [
        {
            "id": group_id,
            "name": normalize_name(group_by_id[group_id].get("name")) or f"Gruppe {group_id}",
        }
        for group_id in payload["customer_visible_group_ids"]
        if group_id in group_by_id
    ]
    payload["zammad_reachable"] = True
    return payload


@router.get("/admin/settings")
async def get_admin_settings(
    db: Session = Depends(get_db),
    _user: dict = Depends(require_console_user),
) -> dict[str, Any]:
    config = get_effective_support_settings(db)
    payload = support_settings_payload(config)
    payload.update(
        {
            "zammad_configured": bool(settings.ZAMMAD_BASE_URL.strip() and settings.ZAMMAD_API_TOKEN.strip()),
            "zammad_reachable": False,
            "zammad_error": None,
            "groups": [],
            "priorities": [],
            "states": [],
            "identity_sync_mode": "keycloak_sub_link",
            "zammad_oidc_recommended": True,
        }
    )

    if not payload["zammad_configured"]:
        return payload

    try:
        async with httpx.AsyncClient(timeout=20) as client:
            catalog = await load_zammad_catalog(client)
    except HTTPException as exc:
        payload["zammad_error"] = str(exc.detail)
        return payload

    payload["groups"] = [
        {
            "id": int(group.get("id")),
            "name": normalize_name(group.get("name")) or f"Gruppe {group.get('id')}",
            "active": bool(group.get("active", True)),
        }
        for group in catalog.get("groups", [])
        if group.get("id") is not None
    ]
    payload["priorities"] = [
        {
            "id": int(priority.get("id")),
            "name": normalize_name(priority.get("name")) or f"Prioritaet {priority.get('id')}",
            "active": bool(priority.get("active", True)),
        }
        for priority in catalog.get("priorities", [])
        if priority.get("id") is not None
    ]
    payload["states"] = [
        {
            "id": int(state.get("id")),
            "name": normalize_name(state.get("name")) or f"Status {state.get('id')}",
            "active": bool(state.get("active", True)),
        }
        for state in catalog.get("states", [])
        if state.get("id") is not None
    ]
    payload["zammad_reachable"] = True
    return payload


@router.put("/admin/settings")
async def update_admin_settings(
    payload: SupportSettingsUpdateIn,
    db: Session = Depends(get_db),
    _user: dict = Depends(require_console_owner),
) -> dict[str, Any]:
    row = get_or_create_support_settings(db)
    effective_config = get_effective_support_settings(db)

    customer_visible_group_ids = sorted({int(group_id) for group_id in payload.customer_visible_group_ids if group_id > 0})
    default_group_id = payload.default_group_id if payload.default_group_id and payload.default_group_id > 0 else None
    default_priority_id = (
        payload.default_priority_id if payload.default_priority_id and payload.default_priority_id > 0 else None
    )

    if default_group_id is not None and default_group_id not in customer_visible_group_ids:
        customer_visible_group_ids.append(default_group_id)
        customer_visible_group_ids.sort()

    if settings.ZAMMAD_BASE_URL.strip() and settings.ZAMMAD_API_TOKEN.strip():
        async with httpx.AsyncClient(timeout=20) as client:
            catalog = await load_zammad_catalog(client)
        valid_group_ids = {
            int(group["id"])
            for group in catalog.get("groups", [])
            if group.get("id") is not None
        }
        valid_priority_ids = {
            int(priority["id"])
            for priority in catalog.get("priorities", [])
            if priority.get("id") is not None
        }
        invalid_groups = [group_id for group_id in customer_visible_group_ids if group_id not in valid_group_ids]
        if invalid_groups:
            raise HTTPException(status_code=422, detail="invalid_support_group_ids")
        if default_group_id is not None and default_group_id not in valid_group_ids:
            raise HTTPException(status_code=422, detail="invalid_default_support_group_id")
        if default_priority_id is not None and default_priority_id not in valid_priority_ids:
            raise HTTPException(status_code=422, detail="invalid_default_support_priority_id")

    row.allow_customer_group_selection = payload.allow_customer_group_selection
    row.customer_visible_group_ids = customer_visible_group_ids
    row.default_group_id = default_group_id
    row.default_priority_id = default_priority_id
    row.uploads_enabled = payload.uploads_enabled
    row.uploads_max_bytes = min(
        max(1, int(payload.uploads_max_bytes)),
        effective_config.uploads_max_bytes_ceiling,
    )
    row.maintenance_mode = payload.maintenance_mode
    row.maintenance_message = normalize_name(payload.maintenance_message) or effective_config.maintenance_message
    db.commit()

    return await get_admin_settings(db=db, _user=_user)


@router.get("/admin/diag/zammad-roles")
async def diag_zammad_roles(
    _user: dict = Depends(require_console_owner),
) -> List[dict]:
    require_zammad_configured()
    async with httpx.AsyncClient(timeout=20) as client:
        try:
            response = await client.get(
                f"{zammad_base_url()}/api/v1/roles",
                headers=zammad_headers(),
            )
        except httpx.RequestError as exc:
            raise_zammad_unreachable("roles(diag)", exc)
    if response.status_code >= 400:
        raise HTTPException(
            status_code=502,
            detail=f"zammad_roles_lookup_failed: {response.status_code}",
        )
    roles = response.json()
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
    require_zammad_configured()
    async with httpx.AsyncClient(timeout=20) as client:
        try:
            response = await client.get(
                f"{zammad_base_url()}/api/v1/users/search",
                params={"query": email},
                headers=zammad_headers(),
            )
        except httpx.RequestError as exc:
            raise_zammad_unreachable("users/search(diag)", exc)
    if response.status_code >= 400:
        raise HTTPException(
            status_code=502,
            detail=f"zammad_user_search_failed: {response.status_code}",
        )
    results = response.json()
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


@router.post("/webhook")
@limiter.limit("120/minute")
async def zammad_webhook(
    request: Request,
    x_zammad_secret: Optional[str] = Header(None),
    x_hub_signature: Optional[str] = Header(None),
    x_zammad_delivery: Optional[str] = Header(None),
    x_zammad_trigger: Optional[str] = Header(None),
    authorization: Optional[str] = Header(None),
    db: Session = Depends(get_db),
) -> Dict[str, Any]:
    secret = settings.ZAMMAD_WEBHOOK_SECRET.strip()
    if not secret:
        raise HTTPException(status_code=500, detail="server_not_configured")

    payload_bytes = await request.body()
    bearer_secret = ""
    if authorization:
        scheme, _, token = authorization.partition(" ")
        if scheme.lower() == "bearer":
            bearer_secret = token.strip()

    signature_ok = verify_zammad_webhook_hmac(secret, payload_bytes, x_hub_signature)
    secret_ok = x_zammad_secret == secret or bearer_secret == secret
    if not signature_ok and not secret_ok:
        raise HTTPException(status_code=401, detail="invalid_webhook_secret")

    event_id = (
        x_zammad_delivery
        or request.headers.get("x-zammad-event-id")
        or request.headers.get("x-request-id")
        or hashlib.sha256(payload_bytes).hexdigest()
    )
    event_type = (
        x_zammad_trigger
        or request.headers.get("x-zammad-event")
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
    if existing and existing.status == "ok":
        return {"ok": True, "notifications_created": 0}

    event_row = existing
    if event_row is None:
        event_row = WebhookEventV2(
            source="zammad",
            event_id=event_id,
            event_type=event_type,
            payload=payload,
            received_at=utcnow(),
        )
        db.add(event_row)
    else:
        event_row.event_type = event_type
        event_row.payload = payload

    try:
        processing = process_zammad_webhook_event(
            db=db,
            event_id=event_id,
            payload=payload,
        )
        event_row.processed_at = utcnow()
        event_row.status = "ok"
        event_row.error = None
        db.commit()
        return {
            "ok": True,
            "notifications_created": int(processing.get("notifications_created") or 0),
        }
    except HTTPException as exc:
        event_row.processed_at = utcnow()
        event_row.status = "failed"
        event_row.error = str(exc.detail)[:2000]
        db.commit()
        raise
    except Exception as exc:
        logger.exception("zammad webhook processing failed for event %s", event_id)
        event_row.processed_at = utcnow()
        event_row.status = "failed"
        event_row.error = str(exc)[:2000]
        db.commit()
        raise HTTPException(status_code=500, detail="zammad_webhook_processing_failed") from exc
