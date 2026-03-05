# PCWächter Home – GAP Plan (home-gap-plan1)

Datum: 2026-03-05  
Scope: **server/home** (Kundenportal „Home“) + minimale API-Erweiterungen (nur wenn nötig)

---

## 1) Ist-Stand (aus Repo beobachtet)

**Tech/Stack**
- Next.js (App Router) unter `server/home/src/app/*`.
- Auth via **NextAuth + Keycloak** (`server/home/src/auth.ts`, API Route `src/app/api/auth/[...nextauth]`).
- Interne API-Nutzung:
  - `GET /license/status` (Backend) via `getLicenseStatus()` in `server/home/src/lib/api.ts`.
  - `GET /console/public/plans` (Backend) via `getPlans()` in `server/home/src/lib/api.ts`.
- Billing:
  - `POST /api/portal` erzeugt eine **Stripe Billing Portal Session**, sucht Stripe Customer anhand `session.user.email`. Wenn kein Customer existiert → 404 „No billing account found“.  
- Download:
  - `GET /download` lädt Installer von `pcwaechter-public-release` (GitHub Releases) + Manifest `installer-manifest.json`.

**Pages, die es aktuell gibt**
- `/` → Redirect nach `/account`
- `/account` → Profil + Lizenzstatus (Plan, State, Ablauf, Features)
- `/account/billing` → Button „Stripe Portal öffnen“ (Portal Session)
- `/pricing` → Redirect nach `/account/billing`
- `/download` → Offline-/Live-Installer + SHA256

---

## 2) Zielbild (Home v1) – „Kundenportal fertig nutzbar“

> Ein Endnutzer kann sich anmelden, seine Lizenz/Subscription verwalten, Geräte sehen/verwalten, die App herunterladen und Support anfragen – ohne Admin-Konsole.

### Zielbild – Navigation/IA (Information Architecture)
**Top-Level (eingeloggt):**
1. **Dashboard** (`/account`)  
   - Lizenz-Widget (Status, Ablauf, Geräte-Limit)
   - Quick Actions: „Download“, „Geräte“, „Support“, „Abo verwalten“
2. **Geräte** (`/account/devices`)  
   - Liste registrierter Geräte (Name, OS, last_seen, Status)
   - Aktionen: umbenennen, deaktivieren/entkoppeln, „Installations-Key“/Pairing anzeigen (falls Konzept existiert)
3. **Abrechnung** (`/account/billing`)  
   - Wenn Stripe-Kunde existiert: Portal öffnen (Upgrade/Downgrade, Rechnungen)
   - Wenn *kein* Stripe-Kunde: Plan-Auswahl + Checkout (Stripe Checkout)
4. **Download** (`/download`)  
   - Installer + Checksums + Silent-Args + Systemanforderungen
5. **Support** (`/account/support`)  
   - Ticket erstellen (Zammad) + Ticket-Liste (optional v1)  
   - „Support Paket exportieren“ (ZIP Upload/Download optional v1)

### Definition „fertig“ (DoD)
- Kein Dead-End: Ein neuer User ohne Stripe-Konto kann **von Home aus** kaufen/aktivieren.
- Alle Seiten sind geschützt (Auth required), saubere Redirects.
- Fehlerzustände sind sauber (Empty States, Retry, klare Messages).
- Home zeigt nur Kundensicht, keine Admin-Funktionen.

---

## 3) GAP Matrix (Was fehlt bis Zielbild)

Legende: **P0 = muss für v1**, **P1 = sehr sinnvoll**, **P2 = nice-to-have**

### A) Navigation & Layout
- **GAP-A1 (P0):** „Account“-Bereich braucht ein konsistentes Layout (Sidebar/Tab-Navigation)  
  **Fix:** `server/home/src/app/account/layout.tsx` anlegen (oder erweitern) mit Nav: Dashboard, Devices, Billing, Support, Download.  
  **Akzeptanz:** Auf allen `/account/*` Seiten identisches Layout + aktive Route Hervorhebung.

- **GAP-A2 (P0):** Route-Schutz zentralisieren (nicht nur pro Page)  
  **Fix:** Next.js `middleware.ts` (oder `account/layout.tsx` server-side guard) → redirect to sign-in.  
  **Akzeptanz:** Direkter Aufruf `/account/devices` ohne Session → Redirect zu Login.

### B) Billing / Plan Purchase
- **GAP-B1 (P0):** Billing ist aktuell „Portal only“ → **Neue User hängen fest**, weil `POST /api/portal` 404 liefert wenn kein Stripe Customer existiert.  
  **Fix:** `/account/billing` so erweitern:
  - `getPlans()` anzeigen (nur aktive, sortiert).
  - CTA „Jetzt kaufen“ → `POST /api/checkout` (Stripe Checkout Session) pro Plan.
  - Bestehende Kunden: weiterhin Portal.
  **Akzeptanz:** User ohne Stripe Customer kann einen Plan kaufen (Checkout), danach zurück und Lizenzstatus ist aktiv.

- **GAP-B2 (P0):** `POST /api/checkout` muss stabil sein (idempotent, validiert plan/price, success/cancel URLs).  
  **Fix:** Checkout Route prüfen/ergänzen:  
  - Input: `{ plan_id }` oder `{ stripe_price_id }`  
  - Mapping über `/console/public/plans` (backend) oder config
  - `customer_email` setzen, `client_reference_id` (user_id), `metadata` (user_id, plan_id)
  **Akzeptanz:** Checkout Session wird erstellt, Fehlerzustände klar (503 wenn Stripe nicht konfiguriert, 400 bei invalid plan).

- **GAP-B3 (P1):** Nach Checkout: State Sync  
  **Fix:** success_url führt zu `/account/billing?checkout=success` und zeigt Hinweis „Abo wird aktiviert…“ + poll `license-status`.  
  **Akzeptanz:** Nach Kauf sieht User ohne Refresh eine Status-Aktualisierung (spätestens nach 30–60s).

### C) Devices (Kernfeature Kundensicht)
- **GAP-C1 (P0):** Geräte-Seite fehlt komplett  
  **Fix:** Neue Route `/account/devices`:
  - Liste + Empty-State („Noch kein Gerät verbunden – Download/Install“)
  - Aktionen (UI + API calls): Rename, Deactivate/Unlink
  **Benötigte API-Endpunkte (Backend):**
  - `GET /devices` (für aktuellen User)
  - `PATCH /devices/{id}` (rename)
  - `DELETE /devices/{id}` oder `POST /devices/{id}/revoke`
  **Akzeptanz:** Geräte werden angezeigt; ein Gerät kann umbenannt und entkoppelt werden.

- **GAP-C2 (P1):** Status „Online/Offline“ + last_seen  
  **Fix:** Device DTO erweitert (last_seen_at, online boolean).  
  **Akzeptanz:** Portal zeigt Status-Badge.

### D) Support (Zammad)
- **GAP-D1 (P0):** Kundensupport fehlt (Ticket erstellen)  
  **Fix:** `/account/support`:
  - Formular: Betreff, Kategorie, Beschreibung, optional E-Mail vorbefüllt.
  - Submit → Backend `POST /support/tickets` (Zammad create ticket).
  **Akzeptanz:** Ticket wird erstellt, User sieht Ticket-ID + success message.

- **GAP-D2 (P1):** Ticket-Liste (letzte 10)  
  **Fix:** `GET /support/tickets`  
  **Akzeptanz:** User sieht seine Tickets + Status.

### E) Download Quality
- **GAP-E1 (P0):** Download Seite ist gut, aber fehlt „Silent Install“/„Systemanforderungen“ & „Troubleshooting“ kompakt.  
  **Fix:** Ergänzen um:
  - Silent args (aus Manifest oder constant)
  - „Verify SHA-256“ Kurz-Anleitung
  **Akzeptanz:** Alles ohne Scroll-Wüste, mobil gut.

- **GAP-E2 (P1):** Download gated by license?  
  **Fix:** Optional: Wenn `license.status.ok=false` → Hinweis + Kauf-Link.  
  **Akzeptanz:** Trial/Free weiterhin Download möglich je nach Business Rule.

### F) UX/Robustness
- **GAP-F1 (P0):** Einheitliche Loading/Empty/Error States  
  **Fix:** Kleine UI helpers: `<StateCard/>`, `<EmptyState/>`, `<Spinner/>`  
  **Akzeptanz:** Keine blank screens, jede Page hat definierte States.

- **GAP-F2 (P1):** Observability (Sentry/Console logs)  
  **Fix:** Minimal: server-side `console.error` + request id in responses.  
  **Akzeptanz:** Debuggability steigt.

---

## 4) Konkrete ToDo-Liste (Implementierungsreihenfolge)

### Phase 1 (P0 – „Home v1 MVP“)
1. Account Layout + Nav (GAP-A1/A2)
2. Billing: Plan-Auswahl + Checkout-Fallback (GAP-B1/B2/B3 minimal)
3. Devices: List + Unlink + Rename (GAP-C1)
4. Support: Ticket create (GAP-D1)
5. UX States + kleine Komponenten (GAP-F1)
6. Download Seite abrunden (GAP-E1)

### Phase 2 (P1 – „Polish“)
- Ticket-Liste, Device online status, Download gating

---

## 5) Dateien/Orte (wo Codex arbeiten soll)

**Home**
- `server/home/src/app/account/layout.tsx` (neu/erweitern)
- `server/home/src/app/account/devices/page.tsx` (neu)
- `server/home/src/app/account/support/page.tsx` (neu)
- `server/home/src/app/account/billing/page.tsx` (erweitern: plans + checkout)
- `server/home/src/app/api/checkout/route.ts` (prüfen/robust machen)
- `server/home/src/lib/api.ts` (Device/Support API Clients ergänzen)
- `server/home/src/components/*` (kleine UI Bausteine)

**Backend (nur falls Endpunkte fehlen)**
- `GET/PATCH/DELETE /devices*`
- `POST /support/tickets` (+ optional GET tickets)

---

## 6) Akzeptanzkriterien (Kurz)

- [ ] User ohne Stripe Customer kann auf `/account/billing` einen Plan kaufen.
- [ ] Nach Kauf → Lizenzstatus wird in `/account` als aktiv angezeigt.
- [ ] `/account/devices` listet Geräte und erlaubt Rename/Unlink.
- [ ] `/account/support` kann ein Ticket erstellen.
- [ ] Alles ist auth-protected, keine toten Links, klare Errors.

