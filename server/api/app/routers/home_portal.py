from __future__ import annotations

from datetime import timedelta
import uuid

import httpx
from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel, Field
from sqlalchemy import select
from sqlalchemy.orm import Session
from starlette.concurrency import run_in_threadpool

from ..db import get_db
from ..keycloak_admin import fetch_keycloak_user, keycloak_admin_configured, keycloak_admin_context
from ..models import (
    Device,
    DeviceHistoryEntry,
    KbArticle,
    License,
    LicenseAuditLog,
    Notification,
    Plan,
    SupportTicketRating,
    SupportTicketSyncState,
    TelemetrySnapshot,
    UpdateManifest,
)
from ..security_jwt import require_home_user
from ..services.home_portal_service import (
    active_assignment_rows_for_user,
    active_assignment_for_device,
    active_license_owner_id,
    assign_license_to_device,
    derive_license_status,
    detect_device_type,
    ensure_home_profile,
    fetch_latest_release,
    format_date_de,
    get_plan_map,
    license_display_name,
    owned_license_rows,
    record_device_history,
    record_license_audit,
    release_assignment,
    summarize_license_capacity,
    tier_label,
    utcnow,
)
from ..services.support_service import (
    _article_sender_is_agent,
    build_support_profile,
    get_effective_support_settings,
    load_zammad_catalog,
    load_zammad_customer_details,
    normalize_email,
    normalize_name,
    resolve_or_create_zammad_customer_id,
    support_settings_payload,
    sync_zammad_profile_for_identity,
)
from .support import _clean_article_body, _load_ticket, _load_ticket_articles, _search_own_tickets

router = APIRouter(prefix="/home", tags=["home-portal"])


class HomeProfileUpdateIn(BaseModel):
    email: str = Field(..., min_length=3, max_length=254)
    username: str | None = Field(default=None, max_length=255)
    first_name: str | None = Field(default=None, max_length=255)
    last_name: str | None = Field(default=None, max_length=255)
    phone: str | None = Field(default=None, max_length=64)
    preferred_language: str = Field(default="de", min_length=2, max_length=16)
    preferred_timezone: str = Field(default="Europe/Berlin", min_length=3, max_length=64)
    email_notifications_enabled: bool = True
    license_reminders_enabled: bool = True
    support_updates_enabled: bool = True


class HomeDeleteRequestIn(BaseModel):
    confirmation: str = Field(..., min_length=1, max_length=32)


class DeviceLicenseAssignIn(BaseModel):
    license_id: uuid.UUID


class DeviceRenameIn(BaseModel):
    name: str = Field(..., min_length=1, max_length=255)


DEFAULT_DOCUMENTATION = [
    {
        "id": 1,
        "name": "Administratorhandbuch",
        "version": "7.2",
        "size": "12 MB",
        "format": "PDF",
        "language": "Deutsch",
        "type": "manual",
    },
    {
        "id": 2,
        "name": "Schnellstart-Anleitung",
        "version": "7.2",
        "size": "2 MB",
        "format": "PDF",
        "language": "Deutsch",
        "type": "guide",
    },
    {
        "id": 3,
        "name": "API-Dokumentation",
        "version": "7.2",
        "size": "5 MB",
        "format": "PDF",
        "language": "Englisch",
        "type": "technical",
    },
]

DEFAULT_DOCUMENTATION_CATEGORIES = [
    {
        "title": "Erste Schritte",
        "icon": "BookOpen",
        "color": "text-blue-600",
        "bgColor": "bg-blue-50",
        "articles": [
            {"title": "Installation und Einrichtung", "views": "1.2k"},
            {"title": "Grundlegende Konfiguration", "views": "980"},
            {"title": "Erste Schritte mit der Management Console", "views": "850"},
        ],
    },
    {
        "title": "Video-Tutorials",
        "icon": "Video",
        "color": "text-purple-600",
        "bgColor": "bg-purple-50",
        "articles": [
            {"title": "PC-Waechter Pro Ueberblick (15:30)", "views": "2.1k"},
            {"title": "Benutzer und Rechte verwalten (8:45)", "views": "1.5k"},
            {"title": "Remote-Zugriff einrichten (12:20)", "views": "1.3k"},
        ],
    },
    {
        "title": "FAQ",
        "icon": "FileQuestion",
        "color": "text-green-600",
        "bgColor": "bg-green-50",
        "articles": [
            {"title": "Haeufig gestellte Fragen zur Lizenzierung", "views": "3.2k"},
            {"title": "Problembehandlung bei der Installation", "views": "2.8k"},
            {"title": "Kompatibilitaet mit anderen Softwareloesungen", "views": "1.9k"},
        ],
    },
]

DEFAULT_TICKET_TEMPLATES = [
    {
        "id": "template-installation",
        "name": "Installationsproblem",
        "category": "Installation",
        "description": "Hilfe bei der Installation von PC-Waechter",
        "fields": [
            {
                "label": "Betriebssystem",
                "type": "select",
                "options": ["Windows 10", "Windows 11", "Windows Server 2019", "Windows Server 2022"],
                "required": True,
            },
            {
                "label": "Fehlermeldung",
                "type": "textarea",
                "placeholder": "Bitte beschreiben Sie die genaue Fehlermeldung...",
                "required": True,
            },
        ],
    },
    {
        "id": "template-license",
        "name": "Lizenzierung",
        "category": "Lizenzierung",
        "description": "Fragen zur Lizenzaktivierung oder -verwaltung",
        "fields": [
            {
                "label": "Lizenzschluessel",
                "type": "text",
                "placeholder": "XXXX-XXXX-XXXX",
                "required": True,
            },
            {
                "label": "Problem-Beschreibung",
                "type": "textarea",
                "placeholder": "Beschreiben Sie Ihr Lizenzproblem...",
                "required": True,
            },
        ],
    },
]


def _require_user_sub(user: dict) -> str:
    user_id = str(user.get("sub") or "").strip()
    if not user_id:
        raise HTTPException(status_code=401, detail="user_sub_missing")
    return user_id


def _home_user_payload(claims: dict, record: dict | None) -> dict:
    source = record or {}
    return {
        "id": str(source.get("id") or claims.get("sub") or ""),
        "username": normalize_name(source.get("username") or claims.get("preferred_username") or claims.get("username")),
        "email": normalize_email(source.get("email") or claims.get("email")),
        "firstName": normalize_name(source.get("firstName") or claims.get("given_name") or claims.get("firstName")),
        "lastName": normalize_name(source.get("lastName") or claims.get("family_name") or claims.get("lastName")),
        "emailVerified": bool(source.get("emailVerified")) if source.get("emailVerified") is not None else claims.get("email_verified"),
    }


async def _load_keycloak_record(user_id: str) -> dict | None:
    if not keycloak_admin_configured():
        return None
    try:
        return await run_in_threadpool(fetch_keycloak_user, user_id)
    except HTTPException as exc:
        if exc.status_code in {404, 502, 503}:
            return None
        raise


def _notification_payload(row: Notification) -> dict:
    meta = row.meta if isinstance(row.meta, dict) else {}
    severity = meta.get("severity") if meta.get("severity") in {"info", "warning", "success", "error"} else None
    return {
        "id": str(row.id),
        "title": row.title,
        "message": row.body,
        "type": severity or row.type if row.type in {"info", "warning", "success", "error"} else "info",
        "timestamp": row.created_at.isoformat() if row.created_at else None,
        "read": row.read_at is not None,
        "meta": meta,
    }


def _ticket_status_from_state(state_name: str | None) -> str:
    normalized = (state_name or "").strip().lower()
    if normalized in {"closed", "merged"}:
        return "Geschlossen"
    if normalized in {"new", "open", "offen", "neu"}:
        return "Offen"
    if normalized in {"pending reminder", "pending close", "wartend"}:
        return "Warten auf Antwort"
    return "In Bearbeitung"


def _license_sort_key(payload: dict) -> tuple[int, str]:
    status_weight = {"Läuft bald ab": 0, "Aktiv": 1, "Abgelaufen": 2}
    return (status_weight.get(payload["status"], 9), payload["name"])


def _telemetry_severity(payload: TelemetrySnapshot | None) -> tuple[str, str]:
    if payload is None:
        return "Geschuetzt", "Alles in Ordnung"

    haystack = " ".join(
        [
            (payload.category or "").lower(),
            (payload.summary or "").lower(),
            str(payload.payload or {}).lower(),
        ]
    )
    if any(token in haystack for token in ("critical", "krit", "failed", "error", "deaktiv", "outdated", "suspicious")):
        return "Kritisch", payload.summary or "Kritischer Zustand erkannt"
    if any(token in haystack for token in ("warning", "warn", "pending", "low", "alt", "expir")):
        return "Warnung", payload.summary or "Es liegen Warnhinweise vor"
    return "Geschuetzt", payload.summary or "Alles in Ordnung"


def _device_history_payload(rows: list[DeviceHistoryEntry]) -> list[dict]:
    ordered = sorted(rows, key=lambda row: row.created_at or utcnow(), reverse=True)
    return [
        {
            "id": str(row.id),
            "type": row.event_type,
            "message": row.message,
            "timestamp": row.created_at.isoformat() if row.created_at else None,
        }
        for row in ordered[:10]
    ]


async def _support_ticket_payloads(db: Session, user: dict) -> list[dict]:
    try:
        profile = await build_support_profile(user)
    except HTTPException:
        return []

    async with httpx.AsyncClient(timeout=20) as client:
        try:
            customer_id = await resolve_or_create_zammad_customer_id(
                db=db,
                profile=profile,
                client=client,
                create_if_missing=False,
            )
        except HTTPException:
            return []
        if customer_id is None:
            return []

        raw_tickets = await _search_own_tickets(customer_id, client, 1, 20)
        raw_rows = [row for row in raw_tickets if isinstance(row, dict)]
        if not raw_rows:
            return []

        catalog = await load_zammad_catalog(client)
        state_map = {
            int(row["id"]): normalize_name(row.get("name"))
            for row in catalog.get("states", [])
            if row.get("id") is not None
        }
        group_map = {
            int(row["id"]): normalize_name(row.get("name"))
            for row in catalog.get("groups", [])
            if row.get("id") is not None
        }
        ticket_ids = [int(row["id"]) for row in raw_rows if row.get("id") is not None]
        rating_rows = db.execute(
            select(SupportTicketRating).where(
                SupportTicketRating.keycloak_user_id == profile.user_id,
                SupportTicketRating.zammad_ticket_id.in_(ticket_ids),
            )
        ).scalars().all()
        rating_by_ticket = {int(row.zammad_ticket_id): row for row in rating_rows}
        sync_rows = db.execute(
            select(SupportTicketSyncState).where(
                SupportTicketSyncState.keycloak_user_id == profile.user_id,
                SupportTicketSyncState.zammad_ticket_id.in_(ticket_ids),
            )
        ).scalars().all()
        sync_by_ticket = {int(row.zammad_ticket_id): row for row in sync_rows}

        tickets: list[dict] = []
        for raw in raw_rows:
            ticket_id = int(raw.get("id"))
            ticket = await _load_ticket(ticket_id, client)
            articles = await _load_ticket_articles(ticket_id, client)
            customer = await load_zammad_customer_details(ticket.get("customer_id"), client)
            sorted_articles = sorted(articles, key=lambda row: str(row.get("created_at") or ""))

            first_name = normalize_name((customer or {}).get("firstname"))
            last_name = normalize_name((customer or {}).get("lastname"))
            customer_display = " ".join(part for part in [first_name, last_name] if part)
            if not customer_display:
                customer_display = normalize_name((customer or {}).get("email")) or "Sie"

            messages = []
            attachments = []
            for article in sorted_articles:
                if article.get("internal"):
                    continue
                article_id = int(article.get("id"))
                is_support = _article_sender_is_agent(article)
                body = _clean_article_body(str(article.get("body") or ""), normalize_name(article.get("content_type")))
                messages.append(
                    {
                        "id": article_id,
                        "sender": "Support Team" if is_support else customer_display,
                        "message": body,
                        "timestamp": article.get("created_at"),
                        "isSupport": is_support,
                    }
                )
                for index, attachment in enumerate(article.get("attachments") or []):
                    if not isinstance(attachment, dict):
                        continue
                    attachments.append(
                        {
                            "id": f"{article_id}-{index}",
                            "name": normalize_name(attachment.get("filename") or attachment.get("name")) or f"attachment-{index + 1}",
                            "size": int(attachment.get("size") or 0),
                            "type": normalize_name(attachment.get("mime-type") or attachment.get("type")) or "application/octet-stream",
                            "uploadedAt": article.get("created_at"),
                        }
                    )

            state_name = state_map.get(int(ticket.get("state_id") or 0)) or normalize_name(ticket.get("state"))
            sync_state = sync_by_ticket.get(ticket_id)
            rating = rating_by_ticket.get(ticket_id)
            rating_payload = None
            if rating is not None:
                rating_payload = {
                    "rating": int(rating.rating),
                    "comment": rating.comment,
                    "ratedAt": rating.created_at.isoformat() if rating.created_at else None,
                }

            tickets.append(
                {
                    "id": ticket_id,
                    "title": normalize_name(ticket.get("title")) or "Ohne Betreff",
                    "description": messages[0]["message"] if messages else "",
                    "status": _ticket_status_from_state(state_name),
                    "category": normalize_name((sync_state or {}).portal_category) or group_map.get(int(ticket.get("group_id") or 0)) or "Technischer Support",
                    "createdAt": ticket.get("created_at"),
                    "lastUpdate": ticket.get("updated_at"),
                    "messages": messages,
                    "attachments": attachments,
                    "rating": rating_payload,
                }
            )
        return tickets


def _documentation_payload(db: Session) -> tuple[list[dict], list[dict], list[dict]]:
    docs = list(DEFAULT_DOCUMENTATION)
    categories = list(DEFAULT_DOCUMENTATION_CATEGORIES)
    kb_rows = db.execute(
        select(KbArticle)
        .where(KbArticle.published.is_(True))
        .order_by(KbArticle.updated_at.desc())
        .limit(4)
    ).scalars().all()
    popular = [
        {
            "title": row.title,
            "category": row.category.capitalize(),
            "views": "Neu",
            "rating": 4.8,
        }
        for row in kb_rows
    ]
    if not popular:
        popular = [
            {"title": "Wie aktiviere ich meine Lizenz?", "category": "Lizenzierung", "views": "5.4k", "rating": 4.8},
            {"title": "PC-Waechter auf mehreren Rechnern installieren", "category": "Installation", "views": "4.2k", "rating": 4.6},
            {"title": "Sicherheitseinstellungen optimal konfigurieren", "category": "Konfiguration", "views": "3.8k", "rating": 4.9},
        ]
    return docs, categories, popular


async def _support_config_payload(db: Session) -> dict:
    config = get_effective_support_settings(db)
    payload = support_settings_payload(config)
    payload.pop("storage_root", None)
    payload["groups"] = []
    payload["support_available"] = False
    payload["zammad_reachable"] = False

    try:
        async with httpx.AsyncClient(timeout=20) as client:
            catalog = await load_zammad_catalog(client)
    except Exception:
        return payload

    groups_by_id = {
        int(row["id"]): normalize_name(row.get("name")) or f"Gruppe {row['id']}"
        for row in catalog.get("groups", [])
        if row.get("id") is not None
    }
    payload["groups"] = [
        {"id": group_id, "name": groups_by_id[group_id]}
        for group_id in payload["customer_visible_group_ids"]
        if group_id in groups_by_id
    ]
    payload["support_available"] = True
    payload["zammad_reachable"] = True
    return payload


@router.get("/bootstrap")
async def home_bootstrap(
    db: Session = Depends(get_db),
    user: dict = Depends(require_home_user),
):
    user_id = _require_user_sub(user)
    home_profile = ensure_home_profile(db, user_id)
    keycloak_record = await _load_keycloak_record(user_id)
    user_payload = _home_user_payload(user, keycloak_record)
    plan_map = get_plan_map(db)
    owned_licenses = owned_license_rows(db, user_id)

    assignment_rows = active_assignment_rows_for_user(db, user_id)
    assignments_by_license: dict[uuid.UUID, list[str]] = {}
    for row in assignment_rows:
        assignments_by_license.setdefault(row.license_id, []).append(row.device_install_id)

    licenses = []
    for row in owned_licenses:
        plan = plan_map.get(row.tier)
        active_devices = list(dict.fromkeys(assignments_by_license.get(row.id, [])))
        devices_current, max_devices = summarize_license_capacity(row, plan, len(active_devices))
        licenses.append(
            {
                "id": str(row.id),
                "name": license_display_name(row, plan),
                "key": row.license_key,
                "type": tier_label(row.tier),
                "status": derive_license_status(row),
                "validUntil": format_date_de(row.expires_at),
                "devices": devices_current,
                "maxDevices": max_devices,
            }
        )
    licenses.sort(key=_license_sort_key)

    install_ids = sorted(
        {
            device_id
            for row in assignment_rows
            for device_id in [row.device_install_id]
            if device_id
        }
        | {
            row.activated_device_install_id
            for row in owned_licenses
            if row.activated_device_install_id
        }
    )

    latest_release = None
    try:
        latest_release = await fetch_latest_release()
    except HTTPException:
        latest_release = None

    latest_version = normalize_name((latest_release or {}).get("tag_name"))
    stable_manifest = db.execute(
        select(UpdateManifest)
        .where(UpdateManifest.component == "desktop", UpdateManifest.channel == "stable")
        .limit(1)
    ).scalar_one_or_none()
    if stable_manifest and stable_manifest.latest_version:
        latest_version = stable_manifest.latest_version

    devices = []
    device_by_install_id = {}
    if install_ids:
        for row in db.execute(select(Device).where(Device.device_install_id.in_(install_ids))).scalars().all():
            device_by_install_id[row.device_install_id] = row

    online_threshold = utcnow() - timedelta(seconds=90)
    license_by_install_id: dict[str, License] = {}
    for row in owned_licenses:
        for install_id in assignments_by_license.get(row.id, []):
            license_by_install_id[install_id] = row
        if row.activated_device_install_id and row.activated_device_install_id not in license_by_install_id:
            license_by_install_id[row.activated_device_install_id] = row

    for install_id in install_ids:
        device = device_by_install_id.get(install_id)
        if device is None:
            continue
        latest_telemetry = db.execute(
            select(TelemetrySnapshot)
            .where(TelemetrySnapshot.device_id == device.id)
            .order_by(TelemetrySnapshot.received_at.desc())
            .limit(1)
        ).scalar_one_or_none()
        history_rows = db.execute(
            select(DeviceHistoryEntry)
            .where(DeviceHistoryEntry.device_install_id == install_id)
            .order_by(DeviceHistoryEntry.created_at.desc())
            .limit(20)
        ).scalars().all()
        security_status, status_message = _telemetry_severity(latest_telemetry)
        license_row = license_by_install_id.get(install_id)
        license_plan = plan_map.get(license_row.tier) if license_row else None
        device_status = "Online" if device.last_seen_at and device.last_seen_at >= online_threshold else "Offline"
        if device.blocked:
            device_status = "Wartung"
        devices.append(
            {
                "id": install_id,
                "name": normalize_name(device.host_name) or install_id,
                "type": detect_device_type(device),
                "os": " ".join(part for part in [device.os_name or "", device.os_version or ""] if part).strip() or "Unbekannt",
                "lastSeen": device.last_seen_at.isoformat() if device.last_seen_at else device.created_at.isoformat() if device.created_at else None,
                "status": device_status,
                "ipAddress": device.primary_ip or "",
                "licenseId": str(license_row.id) if license_row else "",
                "licenseName": license_display_name(license_row, license_plan) if license_row else "",
                "licenseStatus": derive_license_status(license_row) if license_row else "Nicht zugewiesen",
                "licenseValidUntil": format_date_de(license_row.expires_at) if license_row else None,
                "licenseType": tier_label(license_row.tier) if license_row else None,
                "pcWaechterVersion": normalize_name(device.desktop_version or device.agent_version or device.updater_version) or "0.0.0",
                "registeredAt": device.created_at.isoformat() if device.created_at else None,
                "securityStatus": security_status,
                "statusMessage": status_message,
                "lastScan": latest_telemetry.received_at.isoformat() if latest_telemetry and latest_telemetry.received_at else None,
                "lastMaintenance": history_rows[0].created_at.isoformat() if history_rows else None,
                "updateAvailable": bool(
                    latest_version
                    and normalize_name(device.desktop_version or device.agent_version or device.updater_version)
                    and normalize_name(device.desktop_version or device.agent_version or device.updater_version) != latest_version
                ),
                "latestVersion": latest_version,
                "history": _device_history_payload(history_rows),
            }
        )

    license_ids = [row.id for row in owned_licenses]
    audit_rows = db.execute(
        select(LicenseAuditLog)
        .where(LicenseAuditLog.license_id.in_(license_ids if license_ids else [uuid.uuid4()]))
        .order_by(LicenseAuditLog.created_at.desc())
        .limit(100)
    ).scalars().all() if license_ids else []
    license_audit_log = [
        {
            "id": str(row.id),
            "licenseId": str(row.license_id),
            "action": row.action,
            "description": row.description,
            "user": row.actor_name,
            "timestamp": row.created_at.isoformat() if row.created_at else None,
            "details": row.details,
        }
        for row in audit_rows
    ]

    support_tickets = await _support_ticket_payloads(db, user)
    notifications = [
        _notification_payload(row)
        for row in db.execute(
            select(Notification)
            .where(Notification.user_id == user_id)
            .order_by(Notification.created_at.desc())
            .limit(50)
        ).scalars().all()
    ]

    documentation, documentation_categories, popular_articles = _documentation_payload(db)
    support_config = await _support_config_payload(db)
    recent_activity = [
        {
            "type": "license",
            "title": row["description"],
            "description": row["user"],
            "timestamp": row["timestamp"],
        }
        for row in license_audit_log[:3]
    ]
    recent_activity.extend(
        {
            "type": notification["type"],
            "title": notification["title"],
            "description": notification["message"],
            "timestamp": notification["timestamp"],
        }
        for notification in notifications[:3]
    )
    recent_activity = sorted(recent_activity, key=lambda row: row["timestamp"] or "", reverse=True)[:6]

    plans = [
        {
            "id": row.id,
            "label": row.label,
            "price_eur": row.price_eur,
            "duration_days": row.duration_days,
            "max_devices": row.max_devices,
            "is_active": row.is_active,
            "sort_order": row.sort_order,
            "feature_flags": row.feature_flags,
            "grace_period_days": row.grace_period_days,
            "stripe_price_id": row.stripe_price_id,
            "amount_cents": row.amount_cents,
            "currency": row.currency,
        }
        for row in db.execute(
            select(Plan)
            .where(Plan.is_active.is_(True))
            .order_by(Plan.sort_order, Plan.id)
        ).scalars().all()
    ]

    return {
        "user": user_payload,
        "licenses": licenses,
        "devices": devices,
        "supportTickets": support_tickets,
        "notifications": notifications,
        "stats": {
            "totalLicenses": len(licenses),
            "activeLicenses": sum(1 for row in licenses if row["status"] == "Aktiv"),
            "expiringLicenses": sum(1 for row in licenses if row["status"] == "Läuft bald ab"),
            "openTickets": sum(1 for row in support_tickets if row["status"] != "Geschlossen"),
        },
        "systemStatus": [
            {"name": "API-Server", "status": "operational", "description": "Alle Systeme funktionieren normal"},
            {"name": "Lizenz-Server", "status": "operational", "description": "Lizenz- und Abo-Daten sind verfuegbar"},
            {
                "name": "Support-System",
                "status": "operational" if support_config["zammad_reachable"] else "degraded",
                "description": "Support ist erreichbar" if support_config["zammad_reachable"] else "Support teilweise eingeschraenkt",
            },
        ],
        "documentation": documentation,
        "documentationCategories": documentation_categories,
        "popularArticles": popular_articles,
        "licenseAuditLog": license_audit_log,
        "ticketTemplates": DEFAULT_TICKET_TEMPLATES,
        "recentActivity": recent_activity,
        "profileSettings": {
            "phone": home_profile.phone,
            "preferredLanguage": home_profile.preferred_language,
            "preferredTimezone": home_profile.preferred_timezone,
            "emailNotificationsEnabled": home_profile.email_notifications_enabled,
            "licenseRemindersEnabled": home_profile.license_reminders_enabled,
            "supportUpdatesEnabled": home_profile.support_updates_enabled,
            "deletionRequestedAt": home_profile.deletion_requested_at.isoformat() if home_profile.deletion_requested_at else None,
            "deletionScheduledFor": home_profile.deletion_scheduled_for.isoformat() if home_profile.deletion_scheduled_for else None,
        },
        "supportConfig": support_config,
        "plans": plans,
    }


@router.get("/downloads/latest-release")
async def latest_release_endpoint(
    _user: dict = Depends(require_home_user),
):
    return await fetch_latest_release()


@router.patch("/profile")
async def update_home_profile(
    payload: HomeProfileUpdateIn,
    db: Session = Depends(get_db),
    user: dict = Depends(require_home_user),
):
    user_id = _require_user_sub(user)
    row = ensure_home_profile(db, user_id)
    record = await _load_keycloak_record(user_id)
    if record is None:
        raise HTTPException(status_code=503, detail="profile_identity_update_unavailable")

    email = normalize_email(payload.email)
    username = normalize_name(payload.username) or normalize_name(record.get("username")) or email
    first_name = normalize_name(payload.first_name) or ""
    last_name = normalize_name(payload.last_name) or ""
    previous_email = normalize_email(record.get("email")) or normalize_email(user.get("email"))

    update_payload: dict[str, object] = {
        "email": email,
        "username": username,
        "firstName": first_name,
        "lastName": last_name,
        "enabled": bool(record.get("enabled", True)),
        "emailVerified": bool(record.get("emailVerified")) if previous_email == email else False,
    }
    attributes = record.get("attributes")
    if isinstance(attributes, dict):
        update_payload["attributes"] = attributes
    required_actions = record.get("requiredActions")
    if isinstance(required_actions, list):
        update_payload["requiredActions"] = required_actions

    base, headers = keycloak_admin_context()
    response = httpx.put(f"{base}/users/{user_id}", headers=headers, json=update_payload, timeout=15)
    if response.status_code == 409:
        raise HTTPException(status_code=409, detail="email_or_username_already_exists")
    if not response.is_success:
        raise HTTPException(status_code=502, detail="profile_identity_update_failed")

    row.phone = normalize_name(payload.phone)
    row.preferred_language = normalize_name(payload.preferred_language) or "de"
    row.preferred_timezone = normalize_name(payload.preferred_timezone) or "Europe/Berlin"
    row.email_notifications_enabled = bool(payload.email_notifications_enabled)
    row.license_reminders_enabled = bool(payload.license_reminders_enabled)
    row.support_updates_enabled = bool(payload.support_updates_enabled)

    await sync_zammad_profile_for_identity(
        db=db,
        keycloak_user_id=user_id,
        email=email or "",
        first_name=first_name,
        last_name=last_name,
    )
    db.commit()
    return await home_bootstrap(db=db, user=user)


@router.post("/account/delete-request")
async def request_account_delete(
    payload: HomeDeleteRequestIn,
    db: Session = Depends(get_db),
    user: dict = Depends(require_home_user),
):
    if payload.confirmation.strip() != "LÖSCHEN":
        raise HTTPException(status_code=400, detail="invalid_confirmation")
    user_id = _require_user_sub(user)
    row = ensure_home_profile(db, user_id)
    now = utcnow()
    row.deletion_requested_at = now
    row.deletion_scheduled_for = now + timedelta(days=30)
    db.commit()
    return {
        "ok": True,
        "deletion_requested_at": row.deletion_requested_at.isoformat() if row.deletion_requested_at else None,
        "deletion_scheduled_for": row.deletion_scheduled_for.isoformat() if row.deletion_scheduled_for else None,
    }


@router.put("/devices/{device_install_id}/license")
async def assign_device_license(
    device_install_id: str,
    payload: DeviceLicenseAssignIn,
    db: Session = Depends(get_db),
    user: dict = Depends(require_home_user),
):
    user_id = _require_user_sub(user)
    device = db.execute(select(Device).where(Device.device_install_id == device_install_id)).scalar_one_or_none()
    if device is None:
        raise HTTPException(status_code=404, detail="device_not_found")

    license_row = db.get(License, payload.license_id)
    if license_row is None:
        raise HTTPException(status_code=404, detail="license_not_found")
    owner_id = active_license_owner_id(license_row)
    if owner_id and owner_id != user_id:
        raise HTTPException(status_code=404, detail="license_not_found")

    actor_name = " ".join(
        part for part in [normalize_name(user.get("given_name")), normalize_name(user.get("family_name"))] if part
    ) or normalize_name(user.get("name")) or normalize_name(user.get("email")) or "Kunde"
    assign_license_to_device(
        db,
        license_row=license_row,
        device_install_id=device_install_id,
        keycloak_user_id=user_id,
        actor_name=actor_name,
    )
    db.commit()
    return {"ok": True, "device_install_id": device_install_id, "license_id": str(license_row.id)}


@router.patch("/devices/{device_install_id}/name")
async def rename_device(
    device_install_id: str,
    payload: DeviceRenameIn,
    db: Session = Depends(get_db),
    user: dict = Depends(require_home_user),
):
    user_id = _require_user_sub(user)
    device = db.execute(select(Device).where(Device.device_install_id == device_install_id)).scalar_one_or_none()
    if device is None:
        raise HTTPException(status_code=404, detail="device_not_found")

    assignment = active_assignment_for_device(db, device_install_id)
    if assignment is not None:
        if assignment.keycloak_user_id != user_id:
            license_row = db.get(License, assignment.license_id)
            if license_row is None or active_license_owner_id(license_row) != user_id:
                raise HTTPException(status_code=404, detail="device_not_found")
    else:
        license_row = db.execute(select(License).where(License.activated_device_install_id == device_install_id)).scalar_one_or_none()
        if license_row is None or active_license_owner_id(license_row) != user_id:
            raise HTTPException(status_code=404, detail="device_not_found")

    new_name = normalize_name(payload.name)
    if not new_name:
        raise HTTPException(status_code=400, detail="device_name_invalid")

    device.host_name = new_name
    record_device_history(
        db,
        device_install_id=device_install_id,
        event_type="action",
        message=f'Gerätename geändert zu "{new_name}"',
        keycloak_user_id=user_id,
    )
    db.commit()
    return {"ok": True, "device_install_id": device_install_id, "host_name": new_name}


@router.delete("/devices/{device_install_id}")
async def remove_device(
    device_install_id: str,
    db: Session = Depends(get_db),
    user: dict = Depends(require_home_user),
):
    user_id = _require_user_sub(user)
    actor_name = " ".join(
        part for part in [normalize_name(user.get("given_name")), normalize_name(user.get("family_name"))] if part
    ) or normalize_name(user.get("name")) or normalize_name(user.get("email")) or "Kunde"

    assignment = active_assignment_for_device(db, device_install_id)
    if assignment is not None:
        if assignment.keycloak_user_id != user_id:
            license_row = db.get(License, assignment.license_id)
            if license_row is None or active_license_owner_id(license_row) != user_id:
                raise HTTPException(status_code=404, detail="device_not_found")
        release_assignment(db, assignment, actor_name=actor_name)
        db.commit()
        return {"ok": True, "device_install_id": device_install_id}

    license_row = db.execute(select(License).where(License.activated_device_install_id == device_install_id)).scalar_one_or_none()
    if license_row is None or active_license_owner_id(license_row) != user_id:
        raise HTTPException(status_code=404, detail="device_not_found")

    license_row.activated_device_install_id = None
    record_license_audit(
        db,
        license_id=license_row.id,
        action="device_removed",
        description=f'Gerät "{device_install_id}" entkoppelt',
        actor_name=actor_name,
        details={"deviceInstallId": device_install_id},
    )
    record_device_history(
        db,
        device_install_id=device_install_id,
        event_type="action",
        message="Gerät aus dem Kundenkonto entkoppelt",
        keycloak_user_id=user_id,
        meta={"licenseId": str(license_row.id)},
    )
    db.commit()
    return {"ok": True, "device_install_id": device_install_id}


@router.post("/licenses/{license_id}/renew-request")
async def request_license_renewal(
    license_id: uuid.UUID,
    db: Session = Depends(get_db),
    user: dict = Depends(require_home_user),
):
    user_id = _require_user_sub(user)
    license_row = db.get(License, license_id)
    if license_row is None or active_license_owner_id(license_row) != user_id:
        raise HTTPException(status_code=404, detail="license_not_found")

    now = utcnow()
    actor_name = " ".join(
        part for part in [normalize_name(user.get("given_name")), normalize_name(user.get("family_name"))] if part
    ) or normalize_name(user.get("name")) or normalize_name(user.get("email")) or "Kunde"
    license_row.renewal_requested_at = now
    record_license_audit(
        db,
        license_id=license_row.id,
        action="updated",
        description="Verlängerung angefragt",
        actor_name=actor_name,
        details={"requestedAt": now.isoformat()},
    )
    db.add(
        Notification(
            user_id=user_id,
            type="license.renewal_requested",
            title="Verlängerung angefragt",
            body=f"Für {license_display_name(license_row)} wurde eine Verlängerung angefragt.",
            meta={
                "severity": "info",
                "href": "/licenses",
                "license_id": str(license_row.id),
                "requested_at": now.isoformat(),
            },
        )
    )
    db.commit()
    return {"ok": True, "license_id": str(license_row.id), "renewal_requested_at": now.isoformat()}
