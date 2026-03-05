# PCWaechter FastAPI API

## Start lokal

```bash
python -m venv .venv
. .venv/Scripts/activate
pip install -r requirements.txt
uvicorn app.main:app --reload --host 0.0.0.0 --port 18080
```

## Migrationen (Alembic)

```bash
alembic upgrade head
```

## Demo-/Startdaten seeden

```bash
python scripts/seed_console_data.py
```

Oder im laufenden Container:

```bash
docker exec api python scripts/seed_console_data.py
```

## Console + Keycloak Rollout (owner/admin)

Von Repo-Root aus:

```bash
KEYCLOAK_ADMIN_PASSWORD=<secret> bash scripts/provision-keycloak-console-access-remote.sh
```

Damit werden im Realm `pcwaechter-prod` angelegt/aktualisiert:

- Client `console` (PKCE + Redirect auf `https://console.xn--pcwchter-2za.de/*`)
- Realm-Rollen `owner`, `admin`, `manager`, `user`
- Benutzer fuer Console-Zugriff (`owner` und `admin-console`)

Zusatz fuer Accounts-Liste aus Keycloak-Admin-API:

- `KEYCLOAK_ADMIN_USER`
- `KEYCLOAK_ADMIN_PASSWORD`
- optional `KEYCLOAK_ADMIN_CLIENT_ID`, `KEYCLOAK_ADMIN_CLIENT_SECRET`

Container-Neubau (API + Console):

```bash
docker compose -f server/api/infra/compose/docker-compose.yml up -d --build
```

## Wichtige Endpoints

- `GET /health`
- `POST /agent/register`
- `POST /agent/heartbeat`
- `POST /agent/inventory`
- `POST /telemetry/update`
- `POST /telemetry/snapshot`
- `GET /admin/devices/overview`
- `DELETE /admin/devices/{device_install_id}`
- `DELETE /admin/devices/{device_install_id}/snapshots/{snapshot_id}`
- `POST /license/activate`
- `GET /license/me`
- `GET /console/ui/dashboard`
- `GET /console/ui/devices`
- `GET /console/ui/telemetry`
- `GET /console/ui/licenses`
- `GET /console/ui/accounts`
- `GET /console/ui/activity-feed`
- `GET /console/ui/audit-log`
- `GET /console/ui/database/hosts`
- `GET /console/ui/database/payloads?device_id=...`
- `GET /console/ui/server/containers`

Alle Machine-to-Machine und Admin-Endpoints erwarten einen gültigen API-Key (`X-API-Key` oder `X-Agent-Api-Key`).
Konfiguration über `API_KEYS` (CSV) und optional `AGENT_API_KEYS`.

## Stripe Setup (Home Checkout + Billing Portal)

Webhook Endpoint (kanonisch):

- `POST /api/v1/payments/webhook`

Benötigte Stripe-Events:

- `checkout.session.completed`
- `invoice.paid`
- `invoice.payment_failed`
- `customer.subscription.updated`
- `customer.subscription.deleted`

Benötigte ENV im API-Service:

- `STRIPE_SECRET_KEY`
- `STRIPE_WEBHOOK_SECRET`
- optional `STRIPE_PORTAL_CONFIG_NO_CANCEL`
- optional `STRIPE_PORTAL_CONFIG_WITH_CANCEL`

### Smoke-Test Checkliste

1. Home-Login durchführen und in `/account/billing` einen kostenpflichtigen Plan auswählen (`Jetzt kaufen`).
2. Prüfen, dass `/api/checkout` im Home auf API `POST /api/v1/payments/create-checkout` geht und Stripe Checkout öffnet.
3. Nach erfolgreichem Checkout Rückkehr auf `/account/billing?checkout=success` prüfen.
4. Beobachten, dass der Banner in Billing nach Polling auf „Lizenz aktiv“ wechselt (oder Timeout-Hinweis nach 60s).
5. Stripe sendet Webhook an `/api/v1/payments/webhook`; danach Subscription/Lizenz in DB prüfen.
6. Billing Portal öffnen (`/api/portal` → `/api/v1/payments/portal`); bei fehlendem Billing-Account muss Home `no_billing_account` und CTA anzeigen.
