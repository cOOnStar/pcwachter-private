from __future__ import annotations

from typing import Any, Dict, Optional

import httpx
from fastapi import APIRouter, Depends, Header, HTTPException
from pydantic import BaseModel, Field

# NOTE: Adjust imports and settings to match your repo.
from app.settings import settings  # unknown: verify settings object


router = APIRouter(prefix="/support", tags=["support"])


class TicketCreateIn(BaseModel):
    title: str = Field(..., max_length=200)
    body: str = Field(..., max_length=5000)
    # Optional: attachments handled via separate endpoint


@router.get("/tickets")
async def list_tickets() -> Any:
    # Implement auth: user JWT required (Keycloak)
    # current_user = Depends(require_user)
    async with httpx.AsyncClient(timeout=20) as client:
        r = await client.get(
            f"{settings.ZAMMAD_BASE_URL}/api/v1/tickets",
            headers={"Authorization": f"Token token={settings.ZAMMAD_API_TOKEN}"},
        )
    if r.status_code >= 400:
        raise HTTPException(status_code=502, detail=f"zammad_error: {r.status_code}")
    return r.json()


@router.post("/tickets")
async def create_ticket(payload: TicketCreateIn) -> Any:
    async with httpx.AsyncClient(timeout=20) as client:
        r = await client.post(
            f"{settings.ZAMMAD_BASE_URL}/api/v1/tickets",
            headers={"Authorization": f"Token token={settings.ZAMMAD_API_TOKEN}"},
            json={
                "title": payload.title,
                "group": "Users",  # TODO: map group properly
                "customer_id": 1,  # TODO: map keycloak user -> zammad user
                "article": {"subject": payload.title, "body": payload.body, "type": "note"},
            },
        )
    if r.status_code >= 400:
        raise HTTPException(status_code=502, detail=f"zammad_error: {r.status_code}")
    return r.json()


@router.post("/webhook")
async def zammad_webhook(x_zammad_secret: Optional[str] = Header(None)) -> Dict[str, bool]:
    # Verify shared secret
    if not settings.ZAMMAD_WEBHOOK_SECRET:
        raise HTTPException(status_code=500, detail="server_not_configured")
    if x_zammad_secret != settings.ZAMMAD_WEBHOOK_SECRET:
        raise HTTPException(status_code=401, detail="invalid_webhook_secret")
    return {"ok": True}
