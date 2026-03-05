# Audit F) Feature-Abdeckung vs Gesamtübersicht (v6.2)

## Referenzbasis
- Produkt-/System-Referenz: `docs/00-overview.md` (v6.2), `docs/07-frontends.md`, `docs/05-api.md`, `docs/06-db.md`.
- Live-/Code-Inventar: `docs/audit/_generated/*`, Router-Dateien unter `server/api/app/routers/`, Frontend-Routen in `server/console/src/App.tsx` und `server/home/src/app`.

## Coverage Matrix

| Bereich / Feature Key | DB | API | UI | Status | Notizen / Abweichungen | Konkrete TODOs |
|---|---|---|---|---|---|---|
| Licensing | `licenses`, `plans`, `subscriptions` vorhanden | `/api/v1/license/*`, `/api/v1/console/ui/licenses*`, `/api/v1/console/ui/plans*` | Console `/licenses`, `/plans`; Home `/account`, `/account/billing` | ✅ vollständig | JWT-geschütztes `license/me` vorhanden (`license.py`) | Keine P0-Lücke |
| Agent Lifecycle | `devices`, `device_tokens`, `device_inventory` vorhanden | `/api/v1/agent/register|heartbeat|inventory|token/rotate` | Kein dediziertes Agent-UI, aber Device-Ansichten in Console | 🟨 teilweise | Agent-Funktionalität ist primär API-/Client-getrieben | Optional: Console-Action für Token-Rotate explizit anbinden |
| Telemetry | `telemetry_snapshots` vorhanden | `/api/v1/telemetry/*`, `/api/v1/console/ui/telemetry*` | Console `/telemetry` + Device-Detail | ✅ vollständig | Composite Index für Telemetry vorhanden | Optional: mehr Chart/Range-Endpoints |
| Updater / Version-Management | Kein `desktop_version`/`updater_version` Feld im Modell; nur `agent_version` | keine dedizierten `/updater/*` Endpoints | UI zeigt Agent-Version, keine dedizierte Updater-Verwaltung | ❌ fehlt | v6-Anforderung „desktop/agent/updater version visibility“ nur teilweise erfüllt | `models.py` + Migration für `desktop_version`, `updater_version`; Console-UI erweitern |
| Feature Flags / Rollouts | `feature_overrides`, `plans.feature_flags` vorhanden | `/api/v1/console/ui/features/overrides`, `/disable` | Console `/features`, `/plans` | ✅ vollständig | Scope/Target-Rollout vorhanden (`scope`, `target_id`, `rollout_percent`) | Optional: erweiterte Segmentierung (z. B. org/group) |
| Notifications | keine persistente Notification-Tabelle | `/api/v1/console/ui/notifications` + `/read` | Console `/notifications` | 🟨 teilweise | Notifications werden serverseitig aus vorhandenen Daten generiert; `mark_read` ist NOOP | Persistente Tabelle + echtes Read-State-Tracking ergänzen |
| Zammad / Support | Compose/Env vorhanden, aber keine dedizierte PCW-DB-Struktur | Kein `/api/v1/support/*` Router nachweisbar | Home-Supportseiten aus Doku in Code nicht nachweisbar | ❌ fehlt | Dokumentation nennt Support-Flow, Code-Inventar zeigt keine API-Integration | Support-Router unter `/api/v1/support/*` implementieren oder Doku auf IST anpassen |
| Keycloak Integration | n/a (externes IAM, eigene Tabellen im selben Postgres) | JWT Verify (JWKS + `aud` + `iss`) in `security_jwt.py` | Console/Home via OIDC | ✅ vollständig | Realm/Clients/Rollen/Mappers im laufenden Keycloak nachgewiesen | Optional: Realm-Export JSON versionieren |
| Admin Console | n/a | 34 `/api/v1/console/ui/*` Endpoints | 11 aktive Routen (`server/console/src/App.tsx`) | ✅ vollständig | Einige Reserve-Endpoints ohne UI-Verwendung vorhanden | Optional: Dead endpoints reduzieren oder UI anbinden |
| Home Portal | n/a | nutzt `/license/status`, `/console/public/plans` plus lokale Next-API-Proxys | 5 Routen (`/`, `/account`, `/account/billing`, `/download`, `/pricing`) | 🟨 teilweise | Kein nachweisbarer `/support`-Bereich trotz Doku | Home-Routen/Doku synchronisieren, fehlende Bereiche ergänzen |

## Abweichungen mit Risiko

| Thema | Risiko | Priorität |
|---|---|---|
| Fehlendes Updater-Datenmodell | Versions-/Rollout-Transparenz unvollständig | P1 |
| Support/Zammad-API nicht im Backend verdrahtet | Doku vs Code drift, unklare Betriebsfähigkeit Support-Flow | P1 |
| Notifications ohne Persistenz | kein auditierbares Read-State, UX inkonsistent | P2 |
| Alembic-Baseline-Lücke (`devices`, `device_inventory`) | Greenfield-Reproduzierbarkeit eingeschränkt | P1 |
