# Codex Prompt – Console/Home “klickfertig”, Stripe konsistent (DB = Quelle)

Du bist Codex. Arbeite im Repo `pcwachter-private` und implementiere die folgenden Punkte in **einem PR**.

## Ziel
- **Einheitlicher Stripe-Flow**: Stripe Secret nur im Backend (`server/api`) – Home ist nur UI + API-Proxies.
- Home nutzt **ausschließlich** API-Endpunkte:
  - `POST /api/v1/payments/create-checkout`
  - `POST /api/v1/payments/portal`
- Home zeigt Preise aus den **neuen Feldern** (`amount_cents`, `currency`, `price_version`) und verlangt published `stripe_price_id` für Kauf.
- Option “monatliche Kündigung” ist standardmäßig **aus** und kann pro Kunde/Subscription aktiviert werden (ohne Custom Domain).

## Tasks (Implementiere alle)
### A) Home: Stripe direkt entfernen → nur API verwenden
1. `server/home/src/app/api/checkout/route.ts`
   - Entferne Stripe SDK Nutzung und `STRIPE_SECRET_KEY` Abhängigkeit.
   - Implementiere stattdessen: Proxy auf API `POST /payments/create-checkout`.
   - Übergib: `plan_id`, `success_url`, `cancel_url`.
   - Leite Fehler sauber durch (status + message).

2. `server/home/src/app/api/portal/route.ts`
   - Entferne “Customer by email” Lookup.
   - Proxy auf API `POST /payments/portal` mit `return_url`.
   - Behandle 404 “no stripe customer found” als UI-freundlichen Zustand.

3. `.env.example` im Home:
   - Entferne `STRIPE_SECRET_KEY` (falls vorhanden).
   - Nur noch `API_BASE_URL`, `NEXT_PUBLIC_*` falls nötig.

### B) Home UI: Preise und Planfelder aktualisieren
1. `server/home/src/lib/api.ts` (oder passende Stelle)
   - Plan-Typ aktualisieren:
     - `amount_cents?: number`
     - `currency: string`
     - `price_version: number`
     - `stripe_product_id?: string`
     - `stripe_price_id?: string`
   - Preisformatierung: aus `amount_cents/currency`, fallback nur wenn wirklich nötig.

2. `server/home/src/app/account/billing/page.tsx`
   - Zeige Preis aus `amount_cents` (z.B. 499 → “4,99 €”).
   - Falls `stripe_price_id` fehlt: “Noch nicht verfügbar (nicht veröffentlicht)” und Kaufbutton disabled + Tooltip.
   - CTA:
     - “Jetzt kaufen” → ruft `/api/checkout` und redirect auf returned URL (wie bisher).
     - “Abo verwalten” → `/api/portal` und redirect.

### C) Kündigung standardmäßig AUS, optional pro Subscription AN
1. Backend DB:
   - Füge in `Subscription` Feld hinzu: `allow_self_cancel: bool` default false.
   - Alembic migration idempotent.

2. Backend API:
   - Erweitere `POST /payments/portal`:
     - Wenn `allow_self_cancel == false` → nutze Portal-Konfiguration ohne Kündigung (optional via ENV `STRIPE_PORTAL_CONFIG_NO_CANCEL`).
     - Wenn true → Konfiguration mit Kündigung (ENV `STRIPE_PORTAL_CONFIG_WITH_CANCEL`).
     - Falls ENV nicht gesetzt: nutze Default-Konfiguration (kompatibel).
   - Erweitere Console UI:
     - Toggle pro Subscription/User für `allow_self_cancel`.
     - API-Endpunkt: `PATCH /console/ui/subscriptions/{id}` (owner-only) oder ähnliches.

3. Home UI:
   - Kündigungs-Text neutral halten (“Verwaltung über das Kundenportal”) oder abhängig von Flag.

### D) Qualität/DoD
- TypeScript build muss laufen (Home/Console).
- API startet (migrations ok).
- Dokumentiere neue ENV Variablen in README / .env.example:
  - `STRIPE_PORTAL_CONFIG_NO_CANCEL`
  - `STRIPE_PORTAL_CONFIG_WITH_CANCEL`
- Update Smoke-Test Abschnitt.

## Abgabe
- Erstelle PR mit Titel: `feat(stripe): unify home billing via api + optional cancel control`
- In PR Beschreibung: “Was/Warum/Tests/ENV”
