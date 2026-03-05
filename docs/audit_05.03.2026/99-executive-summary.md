# Audit H) Executive Summary

## Gesamturteil

**Prod-ready (aktueller Runtime-Stand): `JA, mit Vorbehalten`**  
**Prod-ready (vollständige v6.2-Feature-Abdeckung): `NEIN`**

Begründung:
- Kernpfade laufen stabil (API/Console/Home/Keycloak/Postgres healthy, OIDC+JWKS aktiv, Alembic auf Head).
- Für die in `docs/00-overview.md` implizierte Gesamtvision fehlen oder driften einzelne Bereiche (Support/Zammad-API, Updater-Featuretiefe, Migrations-Baseline-Vollständigkeit).

## Kompakte IST-Kennzahlen

| Kennzahl | Wert | Quelle |
|---|---:|---|
| Backend-Endpunkte gesamt | 55 | `docs/audit/_generated/api_endpoints_inventory.csv` |
| Console UI-Endpunkte (`/console/ui/*`) | 34 | API-Inventar |
| Frontend-Seiten (console/home/sonstige) | 11 / 5 / 1 | `docs/audit/_generated/page_matrix.csv` |
| Dead Calls (Frontend->fehlender Endpoint) | 0 | `docs/audit/_generated/dead_calls.csv` |
| Dead Endpoints (ohne Frontend-Nutzung) | 27 | `docs/audit/_generated/dead_endpoints.csv` |
| Alembic Revisionen | 9 | `docs/audit/_generated/migrations_inventory.csv` |
| Alembic Runtime Version | `20260305_0009` | live `alembic current/heads` |

## Top 10 Findings

1. **Migrations-Baseline unvollständig für Greenfield**  
   `devices`/`device_inventory` werden in der aktuellen Alembic-Kette nicht initial erstellt; `0001` setzt `devices` bereits voraus.
2. **Feature-Drift Updater/Versioning**  
   `desktop_version`/`updater_version` fehlen im DB/API-Modell, nur `agent_version` ist durchgängig.
3. **Support/Zammad in Code nicht End-to-End verdrahtet**  
   Env + Compose vorhanden, aber keine nachweisbaren `/api/v1/support/*` Router.
4. **Notifications sind nicht persistent**  
   Endpoint liefert generierte Daten; `mark_read` ist NOOP (kein dauerhaftes Read-Tracking).
5. **Duale Keycloak-Clientlandschaft**  
   `pcwaechter-*` und parallel `console/home` vorhanden; erhöht Konfig-Komplexität.
6. **Auth-Härtung ist gut umgesetzt**  
   JWKS + `aud` + `iss` + Rollen-Guards sind im Code aktiv (`security_jwt.py`).
7. **Pre-auth Rate-Limits und Body-Limit sind global aktiv**  
   Middleware in `main.py` deckt Legacy + `/api/v1` Pfade ab.
8. **DB enthält App- und Keycloak-Schema im selben Postgres**  
   Funktional möglich, aber Trennung/Backup-Strategie muss strikt dokumentiert sein.
9. **Reserve-Endpunkte ohne UI-Anbindung**  
   Teil der API ist aktuell ungenutzt (bewusstes Superset oder Backlog).
10. **Page-Matrix initial fehlerhaft generiert (behoben)**  
    Platzhalter in vorherigem Zwischenstand korrigiert; aktueller Report ist konsistent.

## Priorisierte TODO-Liste

### P0
1. **Alembic-Reproduzierbarkeit schließen**
   - Initialmigration für `devices` + `device_inventory` ergänzen oder dokumentierten Bootstrap verbindlich machen.
   - Dateien: `server/api/alembic/versions/*` (neu), `docs/deploy*.md`.

### P1
1. **Updater-Datenmodell vervollständigen**
   - DB: `desktop_version`, `updater_version` (Migration + Modell).
   - API/UI: Console Device-Detail und Listen erweitern.
2. **Support-/Zammad-Flow entweder implementieren oder Doku bereinigen**
   - Backend Router unter `/api/v1/support/*` oder klare Entfernung aus Feature-Doku.
3. **Keycloak-Clientkonsolidierung**
   - Entscheiden: canonical-only (`pcwaechter-*`) vs legacy aliases.

### P2
1. **Notification-Persistenz**
   - Tabelle + read/unread state + optional actor/audit.
2. **Dead-endpoint-Hygiene**
   - Unbenutzte Endpunkte dokumentieren/abschalten oder im UI verdrahten.
3. **Realm-Export versionieren**
   - Reproduzierbarer JSON-Export unter `server/keycloak/`.

## Verweise
- Projekt/Build-Map: `docs/audit/00-project-map.md`
- Frontend-Seiten: `docs/audit/01-page-matrix.md`
- API-Inventar: `docs/audit/02-api-endpoints.md`, `docs/audit/03-router-dependencies.md`, `docs/audit/04-frontend-api-callgraph.md`
- DB/Migrations: `docs/audit/05-db-schema.md`, `docs/audit/06-migrations.md`
- Feature-Coverage: `docs/audit/07-feature-coverage.md`
- Keycloak-IST: `docs/audit/08-keycloak-config.md`
