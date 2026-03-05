# 06 — Zammad Setup Guide (konkret, idempotent)

## 1) Zammad Admin Login
- Öffne `https://support.pcwächter.de`
- Erstelle/verwende Admin Account.

## 2) API Token erzeugen
- Zammad UI → Profile (oder Admin/User Settings) → Token anlegen.
- Speichere Token sicher.

## 3) Default Group ID bestimmen
- In Zammad UI: Admin → Groups.
- Notiere die ID der Gruppe, die Tickets zugewiesen bekommen soll (z.B. "Users" / "Support").
- Setze `ZAMMAD_DEFAULT_GROUP_ID=<id>` in API ENV.

## 4) Optional: Organization ID
- Admin → Organizations → (optional) “PCWächter” anlegen.
- Notiere Org ID → `ZAMMAD_DEFAULT_ORG_ID=<id>` (oder 0 wenn nicht genutzt).

## 5) Webhook (optional)
Ziel: Events aus Zammad an API pushen (Ticket updates).
- API Endpoint: `/api/v1/support/webhook`
- Setze `ZAMMAD_WEBHOOK_SECRET=<shared secret>`
- In Zammad: Trigger/Webhook erstellen, der bei Ticket-Update/Article-Create POST auf API macht.
- In API: verify secret header.

## 6) Verifikation via API Diag
- `GET /api/v1/support/admin/diag/zammad-roles` muss Customer/Agent/Admin Rollen zeigen.
- `GET /api/v1/support/admin/diag/zammad-user?email=<user>` zeigt found=true/false.
