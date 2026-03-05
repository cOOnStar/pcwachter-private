# One‑Shot Umsetzungsplan (API)

## Ziel: Endpoints

1) **Activity Feed**
- `GET /api/v1/console/ui/activity-feed?limit=50&offset=0`
- Auth: `require_console_user`
- Response:
```json
{ "items": [ { "id": "...", "timestamp": "...", "type": "device", "action": "...", "target": "...", "user": null, "description": "..." } ], "total": 123 }
```

**Datenquelle (sicher, ohne neue Tabellen):**
- `telemetry_snapshots` JOIN `devices`
- `devices` als zusätzliche Event-Quelle (Registrierung): `created_at`

**Empfohlene SQL-Strategie:**
- UNION ALL in Postgres (telemetry + device_created) -> ORDER BY timestamp DESC
- `total` via `SELECT count(*) FROM (<union>) AS q`

2) **Knowledge Base**
- `GET /api/v1/console/ui/knowledge-base?search=<optional>&limit=50&offset=0`
- Auth: `require_console_user`
- Response:
```json
{ "items": [ { "id": "...", "title": "...", "category": "...", "tags": ["..."], "updated_at": "..." } ], "total": 12 }
```

**Datenquelle (neu):**
- Neue Tabelle `kb_articles` (read-only in Console)

### Migration (Alembic)
Neue Revision z.B. `20260305_0012_add_kb_articles.py` (nächste freie Nummer):
- `kb_articles`:
  - `id` uuid PK default gen_random_uuid()
  - `title` varchar(200) NOT NULL
  - `category` varchar(64) NOT NULL default 'general'
  - `tags` jsonb NOT NULL default '[]'
  - `body_md` text NOT NULL default ''
  - `published` bool NOT NULL default true
  - `created_at` timestamptz NOT NULL default now()
  - `updated_at` timestamptz NOT NULL default now()
- Index: `ix_kb_articles_updated_at`, optional `ix_kb_articles_title`

### API Router Code
**Ort:**
- Best-case: `server/api/app/routers/console.py` (dort sind bereits `/console/ui/*` Endpoints)
- Falls `console.py` unhandlich ist: neue Datei `server/api/app/routers/console_extra.py` und in `main.py` unter `/api/v1/console` mit-mounten.

### Validierung
- `python -m py_compile` für geänderte Python Dateien
- `docker compose build api && docker compose up -d api`
- `curl` Smoke:
```bash
curl -s "$API/api/v1/console/ui/activity-feed?limit=1&offset=0" -H "Authorization: Bearer $ADMIN_TOKEN" | jq .
curl -s "$API/api/v1/console/ui/knowledge-base?limit=1&offset=0" -H "Authorization: Bearer $ADMIN_TOKEN" | jq .
```

## Rollback
- Code: git revert / checkout
- Migration: `alembic downgrade -1` (nur kb_articles)
