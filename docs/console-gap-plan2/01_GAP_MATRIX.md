# GAP-Matrix (Console Backend)

| Item | Ziel | IST | Delta | Priorität |
|---|---|---|---|---|
| P1-5 Activity Feed API | Console UI lädt Activity Feed ohne Fehler | UI existiert, API-Call geht auf `/console/ui/activity-feed`, Endpoint fehlt | Endpoint implementieren + DB-Query (telemetry_snapshots + devices) + Pagination | P1 |
| P1-6 Knowledge Base API | Console UI lädt KB Artikel ohne Fehler | UI existiert, API-Call geht auf `/console/ui/knowledge-base`, Endpoint fehlt | Endpoint implementieren + neue Tabelle `kb_articles` + Migration + Search/Pagination | P1 |

## Nachweis (IST)
- Console ruft `GET /console/ui/activity-feed` und `GET /console/ui/knowledge-base` auf: `server/console/src/app/services/api-service.ts` (strings in Datei).  
- Telemetry Snapshots existieren: Migration `server/api/alembic/versions/20260227_0001_update_telemetry.py` (create_table `telemetry_snapshots`).  
- Devices hat `created_at`, `host_name`: Bootstrap `server/api/alembic/bootstrap/0000_create_devices_and_inventory.sql`.

## Unknowns
- Ob eine bestehende Knowledge-Base-Implementierung außerhalb des Monorepos existiert: unknown (keine Quelle im Repo).
