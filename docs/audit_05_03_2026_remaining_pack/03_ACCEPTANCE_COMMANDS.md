\
# Abnahme / Smoke Checks – Commands

## A) DB + API
```bash
docker compose up -d postgres
docker compose up -d api
docker exec -it pcw-api alembic current
docker exec -it pcw-api alembic heads
docker exec -it pcw-api alembic upgrade head
```

### Schema prüfen (Postgres)
```bash
docker exec -it pcw-postgres psql -U pcwaechter -d pcwaechter -c "\d+ devices"
```
Erwartet: Spalten `desktop_version`, `updater_version`, `update_channel`

## B) OpenAPI prüfen (lokal)
```bash
curl -s https://api.pcwächter.de/v1/openapi.json | jq '.paths | keys[]' | rg "client/status|support"
```

## C) /client/status Test (JWT required)
```bash
# Example payload
curl -X POST https://api.pcwächter.de/v1/client/status \
  -H "Authorization: Bearer <USER_JWT>" \
  -H "Content-Type: application/json" \
  -d '{"device_install_id":"<GUID>","desktop_version":"0.0.76","updater_version":"0.0.12","update_channel":"stable"}'
```

## D) GitHub Release (Variante A)
- Öffne latest release:
  - `.../releases/latest`
- Prüfe Assets:
  - `installer-manifest.json`
  - `PCWaechter_offline_installer.exe`
  - `PCWaechter_live_installer.exe`

## E) Download URLs smoke
```bash
curl -I https://github.com/cOOnStar/pcwaechter-public-release/releases/latest/download/installer-manifest.json
curl -I https://github.com/cOOnStar/pcwaechter-public-release/releases/latest/download/PCWaechter_offline_installer.exe
curl -I https://github.com/cOOnStar/pcwaechter-public-release/releases/latest/download/PCWaechter_live_installer.exe
```
