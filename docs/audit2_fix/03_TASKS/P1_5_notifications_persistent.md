# Task P1-5 — Notifications persistent (Read-State + Speicherung)

## Zielbild (v6.3)
- Notifications werden serverseitig gespeichert und können als gelesen markiert werden.
- Endpoints:
  - `GET /api/v1/notifications`
  - `POST /api/v1/notifications/{id}/read`
  - `POST /api/v1/notifications/read-all`
- Idempotent: read/read-all mehrfach ausführen ist ok.

## Umsetzung (Codex)
> Achtung: Dieser Task hängt stark vom aktuellen IST ab (audit2 markiert „partial“).
> Codex soll zuerst prüfen, ob Modelle/Tabellen schon existieren, bevor er neue anlegt.

1. DB
- Wenn Tabelle fehlt: Alembic Migration anlegen.
- Minimales Schema (falls noch nicht vorhanden):
  - `notifications`:
    - id (uuid or int)
    - user_id (FK users)
    - type (str)
    - title (str)
    - body (text)
    - created_at
    - read_at nullable
    - meta jsonb nullable

2. API
- Router: `server/api/app/routers/notifications.py` (falls nicht existiert, neu)
- Guards: `require_home_user`
- Implement:
  - list (paginate optional)
  - read by id (404 wenn nicht owner)
  - read-all (bulk update)

3. Client impact
- console/home: nur wenn bereits Calls existieren; sonst out-of-scope.

## Akzeptanz / Smoke
- Erzeuge 1-2 Notifications (z.B. via admin seed oder direkte DB insert in dev) und:
  - GET list shows them
  - POST read sets read_at
  - POST read-all sets all unread

## Rückmeldung
- Migration file name
- Router path
- curl outputs
