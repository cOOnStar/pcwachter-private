# 02 — One-Shot Umsetzungsplan (Plan3)

## Reihenfolge (mit Abhängigkeiten)

### Phase 0 — Vorchecks (keine Code-Änderung)
1) **Zammad erreichbar?** (DNS/Proxy/TLS)
2) **API ENV gesetzt?** (ZAMMAD_*, Webhook secret optional)
3) **Keycloak email claim vorhanden?** (Support benötigt Email)
4) **Diag Endpoints** (admin-only) nutzen, um Zammad Roles/User zu prüfen

> Wenn Phase 0 nicht erfüllt ist, bringt Frontend-Polish nur kosmetisch etwas.

---

### Phase 1 — Infra: Zammad korrekt einrichten (P0)
A) Entscheide Betriebsart:
- **Variante 1 (recommended):** Zammad als Service im gleichen Compose-Stack (profile `support`)
- **Variante 2:** Externe Zammad Instanz (Base URL zeigt dahin)

B) Minimal-Konfig (ENV in API):
- `ZAMMAD_BASE_URL=https://support.pcwächter.de` (oder interne URL)
- `ZAMMAD_API_TOKEN=...`
- `ZAMMAD_DEFAULT_GROUP_ID=...` (z.B. “Users” Gruppe)
- `ZAMMAD_DEFAULT_ORG_ID=0` (optional)
- optional: `ZAMMAD_WEBHOOK_SECRET=...` falls du Zammad→API Webhooks nutzt

C) Nginx Proxy Manager:
- Proxy Host `support.pcwächter.de` → Zammad Container/Host
- TLS, Forwarded Headers
- Upload size ausreichend (Attachments)

D) Zammad Setup (einmalig):
- Admin account erstellen
- API Token erzeugen (Admin → Profile → Token)
- Gruppen/Queues prüfen: Default Group ID ermitteln
- Optional: Organization anlegen (z.B. PCWächter) → Org ID

E) Verifikation:
- `GET /api/v1/support/admin/diag/zammad-roles`
- `GET /api/v1/support/admin/diag/zammad-user?email=...`
- Ticket Create via API (mit normalem User Token) muss 200/201 liefern.

---

### Phase 2 — Console UI Polish + Diag (P1)
1) `SupportPage.tsx`:
- Wenn API error detail `support_not_configured`: zeige **Admin-Hintbox** mit fehlenden ENV Variablen.
- Wenn `zammad_unreachable during ...`: zeige “Zammad nicht erreichbar” + Link/CTA zur Diag.
- Für Nicht-Admin: neutrale Meldung ohne interne Details.

2) Diag UI:
- Button “Diag: Zammad Roles” (nur Admin)
- Ergebnis in Modal/Drawer anzeigen (`id`, `name`, `active`)
- Optional: Eingabefeld Email + Button “Diag: Zammad User”

3) Optional: SupportPage zeigt “Configured / OK” Badge, wenn request erfolgreich.

---

### Phase 3 — Home Portal Support (P1, falls noch nicht vorhanden)
**unknown**: Ob Home bereits eine Support-Seite hat.
- Wenn vorhanden: API calls an `/api/v1/support/*` wie Console.
- Wenn nicht vorhanden: neue Page `server/home/src/app/support/page.tsx` (oder entsprechendes Router-Pattern) implementieren:
  - Ticket list (scoped)
  - Ticket create
  - Ticket detail + reply + upload

> Falls Home in diesem Sprint nicht angefasst werden soll: dokumentiere das explizit als “out of scope”.

---

### Phase 4 — Optional: Webhook (P1)
Wenn du Ticket-Status-Updates/Replies aus Zammad “pushen” willst:
- Zammad Trigger/Webhook konfigurieren → API Endpoint `/api/v1/support/webhook`
- `ZAMMAD_WEBHOOK_SECRET` setzen
- idempotent handling (falls bereits implementiert)

---

### Phase 5 — P2 Backlog (nur dokumentieren + Specs erstellen)
- UpdatesPage (Manifest Viewer)
- ClientConfig UI
- KB CRUD
- Rules UI (blocked): `docs/rules/spec.md` erstellen

