# audit2 – IST-Stand (v6.3 Nachweis)

## Kurzbeschreibung
- Scope: serverseitiger IST-Stand für `api`, `postgres`, `keycloak/login`, `console`, `home`, optional `zammad`, plus Release/Update „Variante A“.
- Repo-Revision: `0cebb53babe702bcf5bf8e19242ed0d9371eebab`.
- Audit-Datum: `2026-03-05`.
- Basisregel: Aussagen wurden nur aus Repository-Inhalten abgeleitet; fehlende/unklare Punkte sind als `unknown` markiert.
- Commit-Nachweis: Command `git rev-parse HEAD`.

## Reproduzierbarkeit (Commands)

### 1) Revisions- und Grundzustand
```powershell
git rev-parse HEAD
git status --short
```

### 2) Endpoint-Inventar (FastAPI)
```powershell
rg -n "^@router\.(get|post|put|delete|patch)\(" server/api/app/routers
rg -n "@app.get\(\"/health\"|@app.get\(\"/api/v1/health\"" server/api/app/main.py
```

### 3) DB-Inventar (ORM + Alembic + Bootstrap)
```powershell
rg -n "__tablename__" server/api/app/models.py
rg -n "revision =|op.create_table|op.add_column|op.drop_table|op.drop_column|op.alter_column" server/api/alembic/versions
rg -n "CREATE TABLE|ALTER TABLE|CREATE INDEX" server/api/alembic/bootstrap/0000_create_devices_and_inventory.sql
```

### 4) Compose/ENV/Keycloak
```powershell
rg -n "^networks:|^volumes:|^services:|^  [a-z0-9-]+:|ports:|environment:" server/infra/compose/docker-compose.yml
rg -n "KEYCLOAK_|API_KEYS|AGENT_API_KEYS|CONSOLE_ALLOWED_ROLES|ZAMMAD_|STRIPE_|FIGMA_PREVIEW_KEY" .env.example server/api/app/settings.py
rg -n "pcw_admin|pcw_console|pcw_user|pcw_support|audience|roles mapper|protocol-mappers|clientId" server/keycloak/provision-realm.sh
```

### 5) Release/Update Variante A
```powershell
rg -n "public-release|OFFLINE_NAME|LIVE_NAME|installer-manifest|sha256|releases/latest/download" \
  .github/workflows/publish_release.yml scripts/publish_release.ps1 \
  release/installer-manifest.json client/installer/manifests/installer-manifest.json \
  server/home/src/app/download/page.tsx client/installer/bootstrapper/Program.cs
```

### 6) Zeilenbelege wie im Audit
```powershell
$i=1; Get-Content server/api/app/main.py | ForEach-Object { '{0,4}: {1}' -f $i, $_; $i++ }
```

## Hauptquellen
- API Routing/Security: `server/api/app/main.py`, `server/api/app/security.py`, `server/api/app/security_jwt.py`, `server/api/app/routers/*.py`
- DB: `server/api/app/models.py`, `server/api/alembic/versions/*.py`, `server/api/alembic/bootstrap/0000_create_devices_and_inventory.sql`
- Infra/Env: `server/infra/compose/docker-compose.yml`, `.env.example`, `server/api/app/settings.py`, `server/keycloak/provision-realm.sh`
- Release/Update: `.github/workflows/publish_release.yml`, `scripts/publish_release.ps1`, `release/installer-manifest.json`, `client/installer/*`, `server/home/src/app/download/page.tsx`
- Zielbild-v6.3 Referenz im Repo: `docs/audit_fix_05.03.2026/01_GAP_MATRIX.md`, `docs/audit_fix_05.03.2026/05_DOD_CHECKLIST.md`, `docs/audit_fix_05.03.2026/templates/frontend/home_download_page_update.md`
