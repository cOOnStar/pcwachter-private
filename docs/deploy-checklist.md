✅ Deploy-/Release-Checklist (Gate)
0) Voraussetzungen

 .env vorhanden und vollständig (Secrets gesetzt, keine Defaults in Prod)

 Docker Host hat genug Disk/RAM (mind. 10–20 GB frei, Swap ok)

 DNS / Reverse Proxy (NPM) zeigt auf den richtigen Host (API/Home/Console/Login)

1) Compose/Container-Status

 docker compose -f server/infra/compose/docker-compose.yml --env-file .env ps zeigt alle Services running/healthy

 Keine Crashloops in Logs:

 docker logs --tail=200 pcw-api

 docker logs --tail=200 pcw-keycloak

 (optional) docker logs --tail=200 pcw-zammad-nginx

2) DB / Migration Gate

 DB erreichbar (intern): pg_isready / psql ok

 Alembic ist auf Head:

 docker compose ... exec -T pcw-api alembic current

 docker compose ... exec -T pcw-api alembic heads

 Wichtige Tabellen vorhanden:

 webhook_events (unique stripe_event_id)

 device_tokens inkl. last_used_at, revoked_at, expires_at

 Keine „Ports:“ Exposition für Postgres (nur expose, kein ports: 5432:5432)

3) Keycloak Gate (P0 für Prod)

 Realm pcwaechter-prod existiert und OIDC Discovery funktioniert:

 GET /realms/pcwaechter-prod/.well-known/openid-configuration → 200

 JWKS Endpoint erreichbar:

 GET /realms/pcwaechter-prod/protocol/openid-connect/certs → 200

 Clients existieren: pcwaechter-desktop, pcwaechter-home, pcwaechter-console, pcwaechter-api

 Token enthält aud=pcwaechter-api (Audience Mapper aktiv)

 Rollen vorhanden: pcw_user, pcw_admin, pcw_console, pcw_support (optional pcw_agent)

4) API Gate (Health + Versioning)

 Health:

 GET /health → 200

 GET /api/v1/health → 200

 Canonical Routes laufen unter /api/v1/*

 Legacy Compat Layer ist aktiv (falls geplant), inkl. Deprecation Header

5) Smoke Tests Gate (MUSS gegen laufenden Stack laufen)

Nicht nur Syntax-Check. Smoke Tests müssen HTTP gegen die laufende Umgebung ausführen.

Linux:

 chmod +x scripts/smoke.sh

 API_BASE_URL="http://localhost:18080" ./scripts/smoke.sh → Exitcode 0

 (optional) Stripe Idempotenz:

 TEST_WEBHOOK_SECRET="whsec_***" API_BASE_URL="http://localhost:18080" ./scripts/smoke.sh

Windows:

 Set-ExecutionPolicy -Scope Process Bypass

 $env:API_BASE_URL="http://localhost:18080"; .\scripts\smoke.ps1 → Exitcode 0

 (optional) Stripe Idempotenz:

 $env:TEST_WEBHOOK_SECRET="whsec_***"; .\scripts\smoke.ps1

6) Security Gate (Kurzcheck)

 Pre-auth Rate Limit greift auch bei invalid Requests (401 x10 → 429)

 Body size limit: >1MB → 413 (vor Parsing)

 JWT verify: falsches aud/iss → 401

 Stripe Webhook: gleiche event_id 2× → nur einmal verarbeitet (idempotent)

 Device Token Rotate: alter Token 401, neuer Token 200

7) Rollback-Plan (Pflicht)

 Vorheriges API-Image/Tag bekannt

 Rollback-Schritte dokumentiert:

 Image zurücksetzen

 docker compose up -d (nur betroffene Services)

 DB-Migrations-Rollback bewertet (falls neue Migration live ging)

8) Abschluss

 Release Notes aktualisiert (kurz)

 Sunset/Deprecation Datum geprüft (z.B. Legacy Sunset 01 Sep 2026)

 Monitoring/Alerting check (wenn vorhanden)