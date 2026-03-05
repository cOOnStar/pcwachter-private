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
