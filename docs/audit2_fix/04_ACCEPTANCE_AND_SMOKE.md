# Acceptance + Smoke (Kurz)

## DoD (global)
- `git status` clean (oder erwartete Änderungen committed)
- `python -m py_compile` für API (mind. Router + settings) ✅
- `docker compose up -d api` startet ✅
- `GET /api/v1/health` oder vergleichbarer Healthcheck: 200 ✅ (unknown falls endpoint fehlt)

## P0 Checks
- Agent Register: 503 wenn bootstrap missing + legacy false
- db-init: fresh DB init ok
- release workflow: assets + manifest

## P1 Checks
- Support reply + attachments: 201 from Zammad
- Notifications: list + read + read-all

## Minimal Command Set (Beispiel)
```bash
# repo root
python -m py_compile server/api/app/routers/support.py

# compose (falls vorhanden)
docker compose up -d postgres
docker compose up -d api
docker compose logs -f api
```
