# Codex Prompt – Home GAP Plan 1 (One-shot Umsetzung)

Du arbeitest im Repo **pcwachter-private**. Ziel: Das Kundenportal **server/home** in einem PR auf „Home v1 MVP“ bringen.

## Rahmen
- Ändere primär **server/home**. Backend nur minimal erweitern, falls die genannten Endpunkte fehlen.
- Nutze bestehenden Style (Buttons/Classes/CSS Vars) – **keine großen UI Libraries hinzufügen**.
- Baue robuste Fehlerzustände (loading/empty/error) auf allen neuen Seiten.
- Schreibe nur so viel wie nötig, aber Ende-zu-Ende nutzbar.

## Ist-Stand (wichtig)
- `/account` zeigt Profil + `GET /license/status` (via `server/home/src/lib/api.ts`).
- `/account/billing` kann aktuell nur `POST /api/portal` → Stripe Portal (Customer by email). Wenn kein Customer existiert, ist es ein Dead-End.
- `/download` ist vorhanden.
- `getPlans()` existiert und ruft `GET /console/public/plans`.

## Implementiere (P0 MVP)

### 1) Account Layout + Navigation
- Erstelle `server/home/src/app/account/layout.tsx` (falls nicht vorhanden) oder erweitere es:
  - Sidebar/Tab-Navigation mit Links:
    - Dashboard (`/account`)
    - Devices (`/account/devices`)
    - Billing (`/account/billing`)
    - Support (`/account/support`)
    - Download (`/download`)
  - Active state sichtbar.
- Zentraler Auth-Guard:
  - Wenn keine Session → redirect zum Sign-In (NextAuth).
  - Schutz gilt für `/account/*`.

### 2) Billing: Portal + Checkout-Fallback
- Erweitere `server/home/src/app/account/billing/page.tsx`:
  - Wenn Stripe Portal verfügbar (Customer existiert) → Button „Abo verwalten“ wie bisher.
  - Wenn Portal `404 No billing account found` oder explizit „kein Kunde“ → zeige Plan-Auswahl.
- Plan-Auswahl:
  - Hole Pläne über `getPlans()` und zeige nur `is_active=true`, sortiert nach `sort_order`.
  - Pro Plan: Label, Preis (EUR), Laufzeit, max_devices.
  - CTA „Jetzt kaufen“ → `POST /api/checkout` mit `{ plan_id }` (oder price id), danach redirect auf `checkout_url`.
- Checkout Route:
  - Prüfe/verbessere `server/home/src/app/api/checkout/route.ts`:
    - Validierung (Stripe configured, Input ok)
    - Mapping plan_id → stripe_price_id (über `getPlans()` oder direkt vom Backend)
    - `customer_email = session.user.email`
    - `metadata: { user_id, plan_id }`
    - `success_url` → `/account/billing?checkout=success`
    - `cancel_url` → `/account/billing?checkout=cancel`
  - Response: `{ checkout_url }`

- Nach Checkout (UI):
  - Bei `?checkout=success` zeige Hinweis + Poll `GET /api/license-status` alle 5s bis ok oder 60s.
  - Danach Button „Weiter zum Dashboard“.

### 3) Devices Seite
- Neue Route `server/home/src/app/account/devices/page.tsx`
- UI:
  - Tabelle/Karten: Name, OS, last_seen, Status badge (wenn vorhanden)
  - Empty-State mit Link zu `/download`
- API Client in `server/home/src/lib/api.ts`:
  - `getDevices(accessToken)`
  - `renameDevice(accessToken, id, name)`
  - `revokeDevice(accessToken, id)`
- Falls Backend Endpunkte fehlen: implementiere minimal im Backend:
  - `GET /devices`
  - `PATCH /devices/{id}`
  - `DELETE /devices/{id}` (oder revoke)
  - Auth: aktueller User (Bearer JWT)

### 4) Support Seite (Ticket Create)
- Neue Route `server/home/src/app/account/support/page.tsx`
- Formular: Subject, Category (select), Message
- Submit → API Client:
  - `createSupportTicket(accessToken, payload)`
- Falls Backend fehlt:
  - `POST /support/tickets` ruft Zammad API (über vorhandene ZAMMAD envs), Ticket dem User zuordnen (email).

### 5) UX Helpers
- Kleine Komponenten in `server/home/src/components/`:
  - `StateCard` (loading/error/empty wrapper)
  - `Spinner`
  - `EmptyState`
- Einheitliche Texte.

### 6) Download Seite abrunden
- Ergänze auf `/download` (ohne neues Design):
  - Silent install args
  - Kurz „SHA verifizieren“ Text

## Liefere am Ende
- Alle neuen/angepassten Dateien committed.
- Keine Typescript Errors (`npm run lint`/`npm run build` sollen durchlaufen).
- Kurze README Notiz: welche ENV vars benötigt werden (Stripe + API URLs + Zammad).

Wichtig: Arbeite zielorientiert. Wenn Backend-Endpunkte fehlen, implementiere minimal und dokumentiere sie.
