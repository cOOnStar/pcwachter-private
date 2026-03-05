# PCWächter – Sammel-Anleitung zur Ausführung (Commands, Smoke-Checks)

> Ziel: alles in **einem** koordinierten Durchlauf umsetzen, ohne unnötige Re-Runs.

## 1) Branch + Workspace

```bash
git status
git checkout -b feature/v6.3-updates-varA
```

## 2) DB: Migration vorbereiten & ausführen

### 2.1 Bestandsumgebung (prod/stage)
```bash
# API Container muss laufen:
docker ps --format "table {.Names}	{.Status}" | rg "pcw-api|pcw-postgres"

# Migration files hinzufügen/committen, dann:
docker exec -it pcw-api alembic current
docker exec -it pcw-api alembic heads
docker exec -it pcw-api alembic upgrade head

# Sanity: neue Spalten vorhanden?
docker exec -it pcw-postgres psql -U pcwaechter -d pcwaechter -c "\d devices"
```

### 2.2 Greenfield (neu)
```bash
# 1) Bootstrap (idempotent) – schließt Audit-Lücke:
docker exec -i pcw-postgres psql -U pcwaechter -d pcwaechter < server/api/alembic/bootstrap/0000_create_devices_and_inventory.sql

# 2) Dann Alembic normal:
docker exec -it pcw-api alembic upgrade head
```

> Nachweis, dass diese Lücke existiert: `audit_05.03.2026/06-migrations.md:L38-L40`

## 3) Backend Smoke

```bash
# OpenAPI/health (je nach Setup)
curl -fsS https://api.pcwächter.de/api/v1/health || true
curl -fsS https://api.pcwächter.de/api/v1/ready || true

# Endpoint-Inventar quick check: client/status muss danach auftauchen
rg -n "/api/v1/client/status" audit_05.03.2026/_generated/api_endpoints_inventory.csv || true
```

## 4) Console & Home Build

> Tools/Package Manager **unknown** im Audit – erwartbar: npm/pnpm.  
> Prüfe im Repo `package.json` und wähle passenden Command.

```bash
# console
cd server/console
npm ci
npm run build

# home
cd ../home
npm ci
npm run build
```

## 5) Release Repo (pcwaechter-public-release)

```bash
git clone https://github.com/cOOnStar/pcwaechter-public-release.git
cd pcwaechter-public-release

# (A) Assets hochladen via GitHub Actions Release Workflow
# (B) Manuell testweise:
gh release create vX.Y.Z --generate-notes \
  ./installer-manifest.json \
  ./PCWaechter-Offline-Setup.exe \
  ./PCWaechter-Live-Installer.exe
```

## 6) Full Smoke Test (Repo vorhanden)

Im Audit als vorhanden genannt:
- `scripts/smoke.sh`
- `scripts/smoke.ps1`

Nachweis: `audit_05.03.2026/00-project-map.md:L54-L58`

### Linux/macOS
```bash
bash scripts/smoke.sh
```

### Windows (PowerShell)
```powershell
pwsh scripts/smoke.ps1
```

