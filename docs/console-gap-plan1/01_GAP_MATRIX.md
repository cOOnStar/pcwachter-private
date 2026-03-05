# GAP-Matrix — Admin Console (v6.3)

Quelle Zielbild (v6.3 Gesamtübersicht): Admin Console Seiten/Funktionen:
- Dashboard
- Accounts
- Devices (inkl. Versions, last_seen, channel)
- Plans & Licenses
- Telemetry
- Notifications
- Audit Log
- Activity Feed
- Server/System Health
- Downloads/Updates
- Knowledge Base
- Feature Management (Kill switch/Rollout)
- Client Config (Remote config)
- Rules Management (rule-based intelligence)
- Support Overview

## Ist-Stand (aus Audit2 + Repo-Quellen)
Bereits vorhanden:
- Dashboard, Devices, Licenses, Telemetry, Accounts, Plans, Feature Rollouts, Audit Log, Notifications, Server
  - Nachweis: `server/console/src/app/components/layout/Sidebar.tsx` (NAV_ITEMS)
- Feature Overrides API vorhanden
  - Nachweis: `docs/audit2/ENDPOINT_INVENTORY.md` → `/api/v1/console/ui/features/overrides`
- Knowledge Base **API** vorhanden (read-only, in-memory), aber **keine Console-Seite**
  - Nachweis: `docs/audit2/ENDPOINT_INVENTORY.md` → `/api/v1/console/ui/knowledge-base`
- Activity Feed **API** vorhanden, aber **keine Console-Seite**
  - Nachweis: `docs/audit2/ENDPOINT_INVENTORY.md` → `/api/v1/console/ui/activity-feed`
- Support Self-Service API vorhanden (inkl. reply + attachments), aber **keine Console-Seite**
  - Nachweis: `docs/audit2/IST_Matrix.md` (Support erfüllt) + `server/api/app/routers/support.py`

Fehlt / Delta (Console relevant):
P1 (v6.3 Kern, sofort sinnvoll)
1) **Activity Feed Page** (UI) + api-service wrapper
2) **Knowledge Base Page** (UI) + api-service wrapper (read-only zunächst)
3) **Support Overview Page** (UI) + api-service wrapper (Tickets list/detail/reply/upload)
4) **Device Update Channel edit** (Admin override) — Endpoint + UI
   - IST: `devices.update_channel` existiert, aber kein Admin-Override Endpoint in Endpoint Inventory.

P2 (v6.3 Nice-to-have / abhängig von Backend)
5) **Downloads/Updates Admin Page** (UI) — mindestens Status/Manifest Viewer (GitHub latest/download)
   - Optional: später CRUD, wenn DB-Model `downloads` existiert (aktuell nicht).
6) **Client Config (Remote config) UI** — erfordert `client_config` DB+API (aktuell nicht).
7) **Knowledge Base CRUD UI** — erfordert Persistenzmodell `knowledge_base` DB+API (aktuell nicht).
8) **Rules Management UI** — unknown, da keine verbindliche Rules-Spezifikation im Repo.

## Verifikations-Commands (für unknowns)
- Existiert ein Admin-Endpoint zum Setzen des Update Channels?
  - `rg -n "update[-_]?channel|setUpdateChannel" server/api/app/routers server/api/app`
- Client Config vorhanden?
  - `rg -n "client_config|client-config" server/api/app server/api/alembic/versions server/api/app/models.py`
- Downloads/KB Persistenz vorhanden?
  - `rg -n '__tablename__\s*=\s*"(downloads|knowledge_base)"' server/api/app/models.py`
  - `rg -n 'create_table\(\s*"(downloads|knowledge_base)"' server/api/alembic/versions`
