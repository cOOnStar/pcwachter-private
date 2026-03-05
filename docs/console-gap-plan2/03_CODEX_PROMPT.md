# Master‑Prompt (Codex / Claude) – Console GAP Plan 2

Du bist Senior Staff Engineer (Backend/DevOps/Frontend) und arbeitest **NUR** mit den Dateien im Repo.

## Ziel
Implementiere die fehlenden Console‑Backend Endpoints:
- `GET /api/v1/console/ui/activity-feed`
- `GET /api/v1/console/ui/knowledge-base`

## Regeln
- Nichts erfinden: Wenn etwas unklar ist → `unknown` + Quelle nennen.
- Immer Nachweis: Datei + Zeile oder konkreter Command.
- Additiv, migrations-sicher.
- Keine Authenticode / Code Signing Themen.

## Kontext (IST-Nachweise)
- Console ruft die Endpoints auf: `server/console/src/app/services/api-service.ts` (enthält die Pfade `/console/ui/activity-feed` und `/console/ui/knowledge-base`).
- Telemetry Snapshots existieren: `server/api/alembic/versions/20260227_0001_update_telemetry.py` (Tabelle `telemetry_snapshots`).
- Devices Basis: `server/api/alembic/bootstrap/0000_create_devices_and_inventory.sql` (Spalten `created_at`, `host_name`).

## Umsetzung (verbindlich)
### A) Activity Feed
1. Implementiere in der API einen Endpoint:
   - Path: `/api/v1/console/ui/activity-feed`
   - Auth: `require_console_user`
   - Query params: `limit` (default 50, 1..200), `offset` (default 0, >=0)
2. Datenmodell (Response):
   - `{ "items": ActivityItem[], "total": int }`
   - `ActivityItem`: `id`, `timestamp`, `type`, `action`, `target`, `user`, `description`
3. Query-Logik:
   - UNION aus
     - `telemetry_snapshots` JOIN `devices` (timestamp = received_at)
     - `devices` (timestamp = created_at; action = "registered")
   - Sort DESC by timestamp
   - Pagination über limit/offset auf der UNION
   - `total` = count(*) der UNION
4. Rückgabeformate müssen exakt zu Console TS passen: snake_case Felder wie `timestamp` (ISO str).

### B) Knowledge Base
1. Lege eine neue Alembic Migration an (nächste freie Revision-ID), die `kb_articles` erzeugt (siehe One‑Shot Plan).
2. Ergänze SQLAlchemy Model `KbArticle` in `server/api/app/models.py` (oder wo eure Models liegen).
3. Implementiere Endpoint:
   - Path: `/api/v1/console/ui/knowledge-base`
   - Auth: `require_console_user`
   - Query params: `search` optional, `limit`, `offset` wie oben
   - Filter: `published = true`
   - Search: ILIKE auf `title` und optional `body_md`
   - Response: `{ "items": [...], "total": int }` mit Feldern `id,title,category,tags,updated_at`
4. Wenn keine Artikel vorhanden: `items=[]`, `total=0` (kein Fehler).

### C) Mounting
- Stelle sicher, dass diese Endpoints tatsächlich unter `/api/v1/console/...` erreichbar sind.
- Wenn du neue Router-Dateien anlegst: `main.py` entsprechend erweitern.
- Halte bestehende Endpoints unverändert.

## Output (verbindlich)
1) Liste aller geänderten/neu angelegten Dateien (mit kurzer Begründung).
2) Unified diff Patch für alle Änderungen.
3) Commands:
   - alembic upgrade head
   - docker compose build api; docker compose up -d api
   - 2 curl smoke tests (activity-feed + knowledge-base)
4) Rückmelde-Template (1:1) ausgeben, damit ich das hier posten kann.

## Rückmelde-Template (bitte exakt ausgeben)
Nutze dieses Schema:

RÜCKMELDUNG (console-gap-plan2)
Repo: pcwachter-private
Branch: <name>
Commit: <full sha>

Activity Feed API
Status (PASS/FAIL):
Curl (HTTP + short json):
Notes:

Knowledge Base API
Status (PASS/FAIL):
Migration created (yes/no + filename):
Alembic upgrade head (PASS/FAIL):
Curl (HTTP + short json):
Notes:

git diff --stat:
<paste>

Fehler (falls FAIL):
<command + full error>
