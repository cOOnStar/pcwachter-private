"""Rules Engine – catalog management and findings.

Admin endpoints:
  GET  /console/ui/rules                  – list all rules
  POST /console/ui/rules                  – create/upsert rule
  PATCH /console/ui/rules/{rule_id}       – update (enable/disable/thresholds)
  DELETE /console/ui/rules/{rule_id}      – delete rule
  GET  /console/ui/rules/findings         – list findings (all devices)
  PATCH /console/ui/rules/findings/{fid}  – update finding state

Rule evaluation is triggered on telemetry ingest via evaluate_rules_for_device().
Conditions format (JSONB list):
  [{"metric": "cpu_percent", "operator": ">", "threshold": 90, "category": "cpu"}]

Supported operators: >, <, >=, <=, ==, !=
Metric is a dot-path into the telemetry payload (e.g. "cpu_percent", "used_gb").
"""
from __future__ import annotations

import uuid
from datetime import datetime, timezone
from typing import Any

from fastapi import APIRouter, Depends, HTTPException, Query
from pydantic import BaseModel, Field
from sqlalchemy import func, select
from sqlalchemy.orm import Session

from ..db import get_db
from ..models import Device, RuleFinding, RulesCatalog
from ..security_jwt import require_console_owner, require_console_user

router = APIRouter(tags=["rules"])


# ---------------------------------------------------------------------------
# Schemas
# ---------------------------------------------------------------------------

class RuleCondition(BaseModel):
    metric: str
    operator: str  # >, <, >=, <=, ==, !=
    threshold: float
    category: str = ""  # which telemetry category to check


class RuleRecommendations(BaseModel):
    text: str
    actions: list[str] = []


class RuleItem(BaseModel):
    id: str
    name: str
    category: str
    severity: str
    enabled: bool
    conditions: list[dict[str, Any]]
    recommendations: dict[str, Any]
    rollout_percent: int
    min_client_version: str | None
    max_client_version: str | None
    platform: str
    notes: str | None
    updated_at: str
    updated_by_admin_id: str | None


class RuleUpsertRequest(BaseModel):
    id: str = Field(min_length=1, max_length=64, pattern=r"^[a-z0-9_\-]+$")
    name: str = Field(min_length=1, max_length=128)
    category: str = Field(min_length=1, max_length=32)
    severity: str = "warning"
    enabled: bool = True
    conditions: list[dict[str, Any]] = []
    recommendations: dict[str, Any] = {}
    rollout_percent: int = Field(default=100, ge=0, le=100)
    min_client_version: str | None = None
    max_client_version: str | None = None
    platform: str = "all"
    notes: str | None = None


class RulePatchRequest(BaseModel):
    name: str | None = None
    severity: str | None = None
    enabled: bool | None = None
    conditions: list[dict[str, Any]] | None = None
    recommendations: dict[str, Any] | None = None
    rollout_percent: int | None = Field(default=None, ge=0, le=100)
    notes: str | None = None


class RuleListResponse(BaseModel):
    items: list[RuleItem]
    total: int


class FindingItem(BaseModel):
    id: str
    device_id: str
    device_name: str | None
    rule_id: str
    rule_name: str | None
    severity: str | None
    state: str
    details: dict[str, Any]
    created_at: str
    resolved_at: str | None
    updated_at: str


class FindingListResponse(BaseModel):
    items: list[FindingItem]
    total: int


class FindingPatchRequest(BaseModel):
    state: str  # "open" | "resolved" | "ignored"


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _to_rule_item(r: RulesCatalog) -> RuleItem:
    return RuleItem(
        id=r.id,
        name=r.name,
        category=r.category,
        severity=r.severity,
        enabled=r.enabled,
        conditions=r.conditions if isinstance(r.conditions, list) else [],
        recommendations=r.recommendations if isinstance(r.recommendations, dict) else {},
        rollout_percent=r.rollout_percent,
        min_client_version=r.min_client_version,
        max_client_version=r.max_client_version,
        platform=r.platform,
        notes=r.notes,
        updated_at=r.updated_at.isoformat(),
        updated_by_admin_id=r.updated_by_admin_id,
    )


# ---------------------------------------------------------------------------
# Rule catalog endpoints
# ---------------------------------------------------------------------------

@router.get("/console/ui/rules", response_model=RuleListResponse)
def list_rules(
    category: str | None = Query(default=None),
    enabled: bool | None = Query(default=None),
    db: Session = Depends(get_db),
    _user: dict = Depends(require_console_user),
) -> RuleListResponse:
    stmt = select(RulesCatalog)
    if category is not None:
        stmt = stmt.where(RulesCatalog.category == category)
    if enabled is not None:
        stmt = stmt.where(RulesCatalog.enabled == enabled)
    rows = db.execute(stmt.order_by(RulesCatalog.category, RulesCatalog.id)).scalars().all()
    return RuleListResponse(items=[_to_rule_item(r) for r in rows], total=len(rows))


@router.post("/console/ui/rules", response_model=RuleItem)
def upsert_rule(
    payload: RuleUpsertRequest,
    db: Session = Depends(get_db),
    owner: dict = Depends(require_console_owner),
) -> RuleItem:
    admin_id = str(owner.get("sub", ""))
    existing = db.get(RulesCatalog, payload.id)
    if existing is None:
        existing = RulesCatalog(id=payload.id)
        db.add(existing)

    existing.name = payload.name
    existing.category = payload.category
    existing.severity = payload.severity
    existing.enabled = payload.enabled
    existing.conditions = payload.conditions
    existing.recommendations = payload.recommendations
    existing.rollout_percent = payload.rollout_percent
    existing.min_client_version = payload.min_client_version
    existing.max_client_version = payload.max_client_version
    existing.platform = payload.platform
    existing.notes = payload.notes
    existing.updated_at = datetime.now(timezone.utc)
    existing.updated_by_admin_id = admin_id

    db.commit()
    db.refresh(existing)
    return _to_rule_item(existing)


@router.patch("/console/ui/rules/{rule_id}", response_model=RuleItem)
def patch_rule(
    rule_id: str,
    payload: RulePatchRequest,
    db: Session = Depends(get_db),
    owner: dict = Depends(require_console_owner),
) -> RuleItem:
    rule = db.get(RulesCatalog, rule_id)
    if not rule:
        raise HTTPException(status_code=404, detail="rule not found")

    admin_id = str(owner.get("sub", ""))
    if payload.name is not None:
        rule.name = payload.name
    if payload.severity is not None:
        rule.severity = payload.severity
    if payload.enabled is not None:
        rule.enabled = payload.enabled
    if payload.conditions is not None:
        rule.conditions = payload.conditions
    if payload.recommendations is not None:
        rule.recommendations = payload.recommendations
    if payload.rollout_percent is not None:
        rule.rollout_percent = payload.rollout_percent
    if payload.notes is not None:
        rule.notes = payload.notes
    rule.updated_at = datetime.now(timezone.utc)
    rule.updated_by_admin_id = admin_id

    db.commit()
    db.refresh(rule)
    return _to_rule_item(rule)


@router.delete("/console/ui/rules/{rule_id}")
def delete_rule(
    rule_id: str,
    db: Session = Depends(get_db),
    _owner: dict = Depends(require_console_owner),
) -> dict:
    rule = db.get(RulesCatalog, rule_id)
    if not rule:
        raise HTTPException(status_code=404, detail="rule not found")
    db.delete(rule)
    db.commit()
    return {"ok": True}


# ---------------------------------------------------------------------------
# Findings endpoints
# ---------------------------------------------------------------------------

@router.get("/console/ui/rules/findings", response_model=FindingListResponse)
def list_findings(
    device_id: str | None = Query(default=None),
    rule_id: str | None = Query(default=None),
    state: str | None = Query(default=None),
    limit: int = Query(default=100, ge=1, le=500),
    offset: int = Query(default=0, ge=0),
    db: Session = Depends(get_db),
    _user: dict = Depends(require_console_user),
) -> FindingListResponse:
    stmt = (
        select(RuleFinding, Device.host_name, RulesCatalog.name, RulesCatalog.severity)
        .join(Device, RuleFinding.device_id == Device.id)
        .join(RulesCatalog, RuleFinding.rule_id == RulesCatalog.id)
    )
    if device_id is not None:
        stmt = stmt.where(RuleFinding.device_id == device_id)
    if rule_id is not None:
        stmt = stmt.where(RuleFinding.rule_id == rule_id)
    if state is not None:
        stmt = stmt.where(RuleFinding.state == state)

    total = db.execute(
        select(func.count()).select_from(stmt.subquery())
    ).scalar_one()

    rows = db.execute(
        stmt.order_by(RuleFinding.updated_at.desc()).limit(limit).offset(offset)
    ).all()

    items = [
        FindingItem(
            id=str(f.id),
            device_id=str(f.device_id),
            device_name=host_name,
            rule_id=f.rule_id,
            rule_name=rule_name,
            severity=severity,
            state=f.state,
            details=f.details if isinstance(f.details, dict) else {},
            created_at=f.created_at.isoformat(),
            resolved_at=f.resolved_at.isoformat() if f.resolved_at else None,
            updated_at=f.updated_at.isoformat(),
        )
        for f, host_name, rule_name, severity in rows
    ]
    return FindingListResponse(items=items, total=total)


@router.patch("/console/ui/rules/findings/{finding_id}", response_model=FindingItem)
def patch_finding(
    finding_id: str,
    payload: FindingPatchRequest,
    db: Session = Depends(get_db),
    _owner: dict = Depends(require_console_owner),
) -> FindingItem:
    if payload.state not in ("open", "resolved", "ignored"):
        raise HTTPException(status_code=422, detail="invalid state")

    finding = db.get(RuleFinding, finding_id)
    if not finding:
        raise HTTPException(status_code=404, detail="finding not found")

    finding.state = payload.state
    finding.updated_at = datetime.now(timezone.utc)
    if payload.state == "resolved" and not finding.resolved_at:
        finding.resolved_at = datetime.now(timezone.utc)

    db.commit()
    db.refresh(finding)

    row = db.execute(
        select(RuleFinding, Device.host_name, RulesCatalog.name, RulesCatalog.severity)
        .join(Device, RuleFinding.device_id == Device.id)
        .join(RulesCatalog, RuleFinding.rule_id == RulesCatalog.id)
        .where(RuleFinding.id == finding.id)
    ).one()

    f, host_name, rule_name, severity = row
    return FindingItem(
        id=str(f.id),
        device_id=str(f.device_id),
        device_name=host_name,
        rule_id=f.rule_id,
        rule_name=rule_name,
        severity=severity,
        state=f.state,
        details=f.details if isinstance(f.details, dict) else {},
        created_at=f.created_at.isoformat(),
        resolved_at=f.resolved_at.isoformat() if f.resolved_at else None,
        updated_at=f.updated_at.isoformat(),
    )


# ---------------------------------------------------------------------------
# Rule evaluation engine (called from telemetry ingest)
# ---------------------------------------------------------------------------

def _get_value(payload: dict, metric: str) -> float | None:
    """Extract a numeric value from a flat or nested dict by dot-path."""
    parts = metric.split(".")
    current: Any = payload
    for part in parts:
        if not isinstance(current, dict):
            return None
        current = current.get(part)
    try:
        return float(current)
    except (TypeError, ValueError):
        return None


def _check_operator(value: float, operator: str, threshold: float) -> bool:
    ops = {
        ">": value > threshold,
        "<": value < threshold,
        ">=": value >= threshold,
        "<=": value <= threshold,
        "==": value == threshold,
        "!=": value != threshold,
    }
    return ops.get(operator, False)


def evaluate_rules_for_device(db: Session, device_id: uuid.UUID, category: str, payload: dict) -> None:
    """Evaluate all enabled rules for the given telemetry category/payload.

    Called from telemetry.py on every snapshot ingest.
    Creates or updates RuleFinding rows accordingly.
    """
    now = datetime.now(timezone.utc)
    rules = db.execute(
        select(RulesCatalog).where(RulesCatalog.enabled == True)
    ).scalars().all()

    for rule in rules:
        conditions: list[dict] = rule.conditions if isinstance(rule.conditions, list) else []
        if not conditions:
            continue

        # Only evaluate conditions that reference this telemetry category
        relevant = [c for c in conditions if c.get("category", "") in ("", category)]
        if not relevant:
            continue

        triggered = True
        details: dict = {}
        for cond in relevant:
            metric = cond.get("metric", "")
            operator = cond.get("operator", ">")
            threshold = float(cond.get("threshold", 0))
            value = _get_value(payload, metric)
            details[metric] = {"value": value, "threshold": threshold, "operator": operator}
            if value is None or not _check_operator(value, operator, threshold):
                triggered = False

        existing = db.execute(
            select(RuleFinding).where(
                RuleFinding.device_id == device_id,
                RuleFinding.rule_id == rule.id,
            )
        ).scalar_one_or_none()

        if triggered:
            if existing is None:
                db.add(RuleFinding(
                    device_id=device_id,
                    rule_id=rule.id,
                    state="open",
                    details=details,
                ))
            elif existing.state == "resolved":
                # Re-open resolved findings if condition triggers again
                existing.state = "open"
                existing.resolved_at = None
                existing.details = details
                existing.updated_at = now
        else:
            if existing is not None and existing.state == "open":
                existing.state = "resolved"
                existing.resolved_at = now
                existing.updated_at = now
