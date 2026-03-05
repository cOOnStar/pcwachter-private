# PR Checklist – Home v1 MVP (server/home)

Diese Checkliste ist für einen PR gedacht, der die „Home v1 MVP“ Lücken schließt (Account-Navigation, Billing Checkout-Fallback, Devices, Support, Download polish).

---

## 0) PR Meta (vor dem Review)
- [ ] PR-Titel klar: `home: v1 mvp (billing checkout fallback + devices + support)`
- [ ] PR-Beschreibung enthält:
  - [ ] Welche Seiten/Flows betroffen sind
  - [ ] Neue/angepasste ENV Variablen
  - [ ] Neue Backend-Endpunkte (falls hinzugefügt) inkl. Pfade
  - [ ] Kurze manuelle Testanleitung (3–5 Schritte)
- [ ] Keine Secrets in Diff (Stripe keys, Tokens, URLs, `.env`)
- [ ] Nur notwendige Dateien geändert (kein zufälliger Reformat/Noise)

---

## 1) Code Quality / CI
### Home (Next.js)
- [ ] `cd server/home`
- [ ] `npm ci` (oder `pnpm i` je nach Repo-Standard)
- [ ] `npm run lint` (keine TS/ESLint Fehler)
- [ ] `npm run build` (Build muss durchlaufen)
- [ ] Keine ungenutzten Exporte/Imports, keine `any`-Orgien in neuen Files
- [ ] API Calls aus `src/lib/api.ts` konsistent (ein Error-Handling Pattern)

### API (falls betroffen)
- [ ] Alembic Migrations sauber (nur wenn DB-Schema erweitert wurde)
- [ ] Startet lokal in Compose (API + DB + ggf. Zammad) ohne Crash
- [ ] Endpunkte sind auth-geschützt (Bearer / Keycloak) und testen gegen echte Tokens

---

## 2) UX / Zielbild Abgleich (Pages & States)
- [ ] `/account` (Dashboard)
  - [ ] Logged-in required (redirect wenn keine Session)
  - [ ] Lizenzstatus wird angezeigt
  - [ ] Loading/Empty/Error States vorhanden
- [ ] `/account/devices`
  - [ ] Loading/Empty/Error States
  - [ ] Geräte werden angezeigt (Name/OS/last_seen/Status)
  - [ ] Rename/Revoke funktioniert und aktualisiert UI
- [ ] `/account/billing`
  - [ ] Wenn Stripe-Customer existiert → „Abo verwalten“ (Portal) funktioniert
  - [ ] Wenn kein Stripe-Customer existiert → Plan-Auswahl wird angezeigt (kein Dead-End)
  - [ ] Checkout Redirect funktioniert
  - [ ] `?checkout=success` → Hinweis + Polling auf Lizenzstatus (max ~60s)
  - [ ] `?checkout=cancel` → klare Meldung, kein Broken State
- [ ] `/account/support`
  - [ ] Formular validiert (subject/message required)
  - [ ] Ticket wird erstellt und Success UI erscheint
  - [ ] Fehlerzustände (Zammad down, 401, 500) werden sinnvoll angezeigt
- [ ] `/download`
  - [ ] Release/Manifest Download funktioniert weiterhin
  - [ ] Install-Argumente und SHA Hinweis vorhanden
- [ ] Navigation in `/account/*`
  - [ ] Active State sichtbar
  - [ ] Link-Ziele korrekt
  - [ ] Mobile/Small width nicht komplett kaputt (min. nutzbar)

---

## 3) Security / Auth
- [ ] Keine sensiblen Daten im Client gerendert (Stripe secret, internal tokens)
- [ ] Alle `/account/*` Pages geschützt (server-side redirect oder middleware)
- [ ] Home API Routes (`/api/*`) checken Session und nutzen server-side Tokens
- [ ] Backend Endpunkte (Devices/Support/etc.) prüfen „User kann nur eigene Daten“

---

## 4) Billing / Stripe Spezifisch
- [ ] Fehlerfall: Portal Route gibt 404 wenn kein Customer → UI zeigt Plan-Auswahl
- [ ] Checkout Route validiert Input (plan_id/price_id)
- [ ] `success_url` und `cancel_url` sind korrekt (keine offenen Redirects)
- [ ] `metadata` gesetzt (z.B. user_id, plan_id) für spätere Zuordnung
- [ ] Webhook/Sync (falls existiert) aktualisiert Lizenz zuverlässig

---

## 5) Backend Contract Checks (cURL / Smoke)
> Beispiele anpassen an eure Base-URL und Token.

### License Status
- [ ] `GET /license/status` → 200 + erwartete Felder

### Plans (Public)
- [ ] `GET /console/public/plans` → 200, aktive Pläne, `stripe_price_id` vorhanden (oder Mapping sauber)

### Devices
- [ ] `GET /devices` → 200 + Liste
- [ ] `PATCH /devices/{id}` → 200, Name geändert
- [ ] `DELETE /devices/{id}` (oder revoke) → 200/204, Gerät entfernt/disabled

### Support
- [ ] `POST /support/tickets` → 201/200, Ticket-Id zurück, in Zammad sichtbar

---

## 6) Regression Checks
- [ ] Login/Logout funktioniert noch
- [ ] `/` (Home landing) lädt ohne Errors
- [ ] Bestehende Nutzer (mit Stripe customer) bekommen keine neue Fehlermeldung
- [ ] Neue Nutzer (ohne Stripe customer) landen nicht in einem Dead-End
- [ ] Download Links weiterhin korrekt (keine kaputten GitHub Release/manifest Calls)

---

## 7) Dokumentation & ENV
- [ ] `server/home` README oder `.env.example` aktualisiert:
  - [ ] `NEXTAUTH_URL`
  - [ ] Keycloak (issuer/client)
  - [ ] `API_BASE_URL` / Console URL (wie im Projekt)
  - [ ] Stripe (publishable + secret im Server)
  - [ ] Zammad (falls Support Ticket create)
- [ ] Hinweis: Welche Services müssen in Compose laufen (api, db, zammad, …)

---

## 8) Definition of Done (DoD)
- [ ] Alle P0 Flows manuell getestet (siehe Abschnitt 2)
- [ ] Build/Lint grün
- [ ] Reviewer kann PR lokal starten und 2 Hauptflows nachklicken:
  1) Neuer User → Plan kaufen → Lizenz aktiv → Dashboard ok
  2) Bestehender User → Portal öffnen → Abo verwalten → zurück → ok

---

## Optional (Nice-to-have)
- [ ] Minimaler Playwright/Smoke Test (Login Mock + Navigation + Billing page renders)
- [ ] Sentry/Logging für Checkout/Portal Fehlerfälle
- [ ] Rate limit auf Support Ticket create (basic)
