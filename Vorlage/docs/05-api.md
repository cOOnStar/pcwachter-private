# API Endpoints (FastAPI, best-effort Scan)

## include_router() Prefixe (gefunden)

(keine include_router(prefix=...) pattern gefunden)


## Endpoints (@router.<method>(...))

| Method | Path | Datei |
|---|---|---|
| DELETE | /devices/{device_install_id} | api/app/routers/admin.py |
| DELETE | /devices/{device_install_id}/snapshots/{snapshot_id} | api/app/routers/admin.py |
| GET | /devices | api/app/routers/console.py |
| GET | /devices/overview | api/app/routers/admin.py |
| GET | /devices/{device_id} | api/app/routers/console.py |
| GET | /devices/{device_id}/inventory/latest | api/app/routers/console.py |
| GET | /health | api/app/main.py |
| GET | /me | api/app/routers/license.py |
| GET | /public/plans | api/app/routers/console.py |
| GET | /status | api/app/routers/license.py |
| GET | /ui/accounts | api/app/routers/console.py |
| GET | /ui/activity-feed | api/app/routers/console.py |
| GET | /ui/audit-log | api/app/routers/console.py |
| GET | /ui/dashboard | api/app/routers/console.py |
| GET | /ui/database/hosts | api/app/routers/console.py |
| GET | /ui/database/payloads | api/app/routers/console.py |
| GET | /ui/devices | api/app/routers/console.py |
| GET | /ui/knowledge-base | api/app/routers/console.py |
| GET | /ui/licenses | api/app/routers/console.py |
| GET | /ui/notifications | api/app/routers/console.py |
| GET | /ui/plans | api/app/routers/console.py |
| GET | /ui/preview | api/app/routers/console.py |
| GET | /ui/search | api/app/routers/console.py |
| GET | /ui/server/containers | api/app/routers/console.py |
| GET | /ui/server/host | api/app/routers/console.py |
| GET | /ui/server/services | api/app/routers/console.py |
| GET | /ui/telemetry | api/app/routers/console.py |
| GET | /ui/telemetry/chart | api/app/routers/console.py |
| PATCH | /ui/accounts/{account_id}/role | api/app/routers/console.py |
| PATCH | /ui/licenses/{license_key}/revoke | api/app/routers/console.py |
| POST | /activate | api/app/routers/license.py |
| POST | /create-checkout | api/app/routers/payments.py |
| POST | /heartbeat | api/app/routers/agent.py |
| POST | /inventory | api/app/routers/agent.py |
| POST | /portal | api/app/routers/payments.py |
| POST | /register | api/app/routers/agent.py |
| POST | /snapshot | api/app/routers/telemetry.py |
| POST | /ui/licenses | api/app/routers/console.py |
| POST | /update | api/app/routers/telemetry.py |
| POST | /webhook | api/app/routers/payments.py |
| PUT | /ui/plans/{plan_id} | api/app/routers/console.py |