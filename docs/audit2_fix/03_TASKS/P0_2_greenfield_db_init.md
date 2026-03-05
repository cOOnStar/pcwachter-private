# Task P0-2 — Greenfield DB Init standardisieren (bootstrap + alembic)

## Problem (audit2)
Greenfield ist nicht „Alembic-only“ reproduzierbar. Es existiert ein `bootstrap/0000_create_devices_and_inventory.sql`,
aber der Ablauf ist nicht standardisiert/automatisiert.

## Ziel
- *Idempotent* init: bootstrap.sql + `alembic upgrade head` in einem reproduzierbaren Schritt.
- Kein Schaden bei bestehenden DBs (nur `IF NOT EXISTS` / guards).

## Umsetzungsvorschlag (Codex soll entscheiden, welche Variante im Repo am besten passt)

### Variante A (empfohlen): `db-init` Script + Compose Opt-In
1. Neues Script:
   - `server/api/scripts/db_init.py` oder `server/api/scripts/db_init.sh`
   - Ablauf:
     1) Prüfe Verbindung zu Postgres (`DATABASE_URL`)
     2) Führe `server/api/alembic/bootstrap/0000_create_devices_and_inventory.sql` aus
     3) Führe `alembic upgrade head` aus
2. Compose:
   - optionaler Command/Entry: `python server/api/scripts/db_init.py && uvicorn ...`
   - Oder: eigenes one-shot Service `db-init` (depends_on postgres healthy)

### Variante B: API Start hook (nur DEV/STAGE)
- Beim Start check: existiert `alembic_version` oder `devices` table?
- Wenn nicht: bootstrap + upgrade
- In PROD deaktivierbar per ENV `DB_AUTO_INIT=false`

## Akzeptanz / Smoke
- Neue leere DB:
  - `db-init` läuft ohne Fehler
  - danach `docker compose up api` startet
- Bestehende DB:
  - `db-init` ist no-op außer alembic upgrade (idempotent)

## Rückmeldung
- Welche Variante umgesetzt wurde
- Commands + Logs
