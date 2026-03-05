# Master-Prompt für Codex/Claude (komplette Umsetzung v6.3 „Variante A“)

**Rolle:** Du bist Senior Staff Engineer (Backend/DevOps/Frontend).  
**Regeln:**  
- Arbeite nur im aktuellen Repo-Inhalt.  
- Nichts erfinden: wenn ein File/Struktur abweicht, stoppe und melde „unknown“ + Fundstelle.  
- Änderungen idempotent und additive bevorzugen.  
- Nach jeder Phase: build + minimal smoke.

## Ziel
PCWächter auf **Zielbild v6.3 (Updates Variante A via GitHub Releases)** bringen, inklusive:
- Greenfield-DB Bootstrap schließen (devices/device_inventory).
- Device Versions: desktop_version + updater_version + update_channel.
- Endpoint `POST /api/v1/client/status`.
- Agent Register Flow ohne statischen API-Key (Bootstrap-Key oder user-binding, Legacy optional via Toggle).
- Home Download-Seite: stabile GitHub Links.
- Console Device UI: Versionsfelder anzeigen.
- (Optional P1) Support Router `/api/v1/support/*` (Zammad Proxy).

## Arbeitsplan (ausführen in Reihenfolge)

### Phase 1: DB & Alembic
1) Prüfe Alembic Chain:
- `server/api/alembic/versions/*.py`
- `docker exec pcw-api alembic current && docker exec pcw-api alembic heads`

2) Implementiere Greenfield-Bootstrap:
- Lege `server/api/alembic/bootstrap/0000_create_devices_and_inventory.sql` neu an:
  - `CREATE TABLE IF NOT EXISTS devices (...)`
  - `CREATE TABLE IF NOT EXISTS device_inventory (...)`
  - Indizes + Unique Constraints laut `server/api/app/models.py` (Device + DeviceInventory).
- Ergänze Doku in `docs/06-db.md` (oder passende Deploy-Doku): „Für leere DB zuerst bootstrap.sql, dann alembic upgrade“.

3) Device Version Migration:
- Ergänze in `server/api/app/models.py` im `Device` Model die Felder:
  - `desktop_version`, `updater_version`, `update_channel`
- Neue Migration `server/api/alembic/versions/20260305_0010_add_desktop_updater_versions.py` (Revision folgt auf `20260305_0009`).

### Phase 2: Backend APIs
4) Implementiere Router `server/api/app/routers/client.py`:
- `POST /api/v1/client/status` akzeptiert JSON:
  - `device_install_id` (required)
  - `desktop_version` (optional)
  - `updater_version` (optional)
  - `update_channel` (optional)
- Schreibt in `devices` Tabelle (Upsert/Update) + `last_seen_at`.
- Auth: wähle eine sichere Strategie:
  - Option A: nur Device Token (preferred)
  - Option B: JWT user (wenn Desktop direkt meldet)
  - Dokumentiere Entscheidung kurz im Code.

5) Registriere Router in `server/api/app/main.py` (oder dort, wo Router inkludiert werden).

6) Entferne statischen API-Key aus `/api/v1/agent/register`:
- Analysiere `require_agent_auth` / `require_api_key`.
- Implementiere `PCW_AGENT_BOOTSTRAP_KEY` (env) + `X-Install-Key` header **oder** user-binding.
- Optional Legacy-Pfad per Env Toggle (default OFF).

### Phase 3: Frontends
7) Console:
- Update DTOs in `server/console/src/app/services/api-service.ts` (getDevices/getDeviceDetail).
- Update UI in:
  - `server/console/src/app/pages/DevicesPage.tsx`
  - `server/console/src/app/pages/DeviceDetailPage.tsx`
  um die neuen Felder anzuzeigen.

8) Home Download Page:
- Edit `server/home/src/app/download/page.tsx`
- Setze Links auf GitHub latest/download Assets:
  - Offline Setup
  - Live Installer
  - installer-manifest.json
- Optional: fetch manifest client-side und zeige Version/Datum/sha256.

### Phase 4: Optional Support (P1)
9) Implementiere `server/api/app/routers/support.py` (Zammad Proxy):
- `GET/POST /api/v1/support/tickets`
- `GET /api/v1/support/tickets/{id}`
- `POST /api/v1/support/tickets/{id}/reply`
- `POST /api/v1/support/attachments`
- `POST /api/v1/support/webhook` (shared secret)
- Nutze existierende Env Keys (falls vorhanden) oder füge sie in `.env.example` hinzu.

### Phase 5: Tests / Smoke
10) Build:
- `server/console`: `npm ci && npm run build`
- `server/home`: `npm ci && npm run build`
- API container: `docker compose build api && docker compose up -d api`

11) Smoke:
- `bash scripts/smoke.sh` (oder `pwsh scripts/smoke.ps1`, je OS)
- `curl` gegen Health Endpoints (falls vorhanden)
- Quick checks:
  - `\d devices` zeigt neue Spalten
  - `POST /api/v1/client/status` funktioniert (200)

## Output
- Erstelle einen PR/Commit mit:
  - neuen Dateien (bootstrap.sql, client router, migrations)
  - geänderten Modellen/DTOs/UI
  - kurze Doku-Updates (Release/Bootstrap)

