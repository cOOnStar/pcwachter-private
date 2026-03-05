# Audit G) Keycloak IST (Realm / Clients / Roles / Redirects)

## Methode / Nachweis
- Konfigurationsquellen:
  - `server/keycloak/provision-realm.sh`
  - `.env` / `.env.example` (nur Key-Namen, keine Secret-Ausgabe)
  - `server/api/app/security_jwt.py`
- Laufzeitprüfung (Docker):
  - `curl http://localhost:18083/realms/pcwaechter-prod/.well-known/openid-configuration`
  - `curl http://localhost:18083/realms/pcwaechter-prod/protocol/openid-connect/certs`
  - `kcadm.sh get realms/pcwaechter-prod`, `get clients`, `get roles`, `get groups`

## Realm IST

| Feld | Wert | Nachweis |
|---|---|---|
| Realm Name | `pcwaechter-prod` | `.env(.example)`, `provision-realm.sh` (`REALM`), Runtime `kcadm get realms/pcwaechter-prod` |
| Realm enabled | `true` (runtime) | `kcadm get realms/pcwaechter-prod --fields realm,enabled,displayName` |
| OIDC Discovery | erreichbar (HTTP 200) | `curl .../.well-known/openid-configuration` |
| JWKS | erreichbar (HTTP 200) | `curl .../protocol/openid-connect/certs` |

## Clients IST

### Pflicht-Clients (Soll)
- `pcwaechter-api`
- `pcwaechter-console`
- `pcwaechter-home`
- `pcwaechter-desktop`

### Laufzeitfund (Ist)
- Pflicht-Clients vorhanden.
- Zusätzlich vorhanden (Legacy/Kompatibilität): `console`, `home`.

| Client | Typ/Flow (runtime) | Redirect URIs / Origins (runtime) | Audience Mapper `pcwaechter-api` |
|---|---|---|---|
| `pcwaechter-api` | confidential/service-account (`publicClient=false`, `standardFlow=false`, `serviceAccounts=true`) | n/a | vorhanden |
| `pcwaechter-console` | public + PKCE (`publicClient=true`, `standardFlow=true`) | `https://console.../*`, `http://localhost:13000/*`, `http://localhost:13001/*`, `http://localhost:5173/*` | vorhanden |
| `pcwaechter-home` | confidential (`publicClient=false`, `standardFlow=true`) | `.../api/auth/callback/keycloak` (prod + localhost) | vorhanden |
| `pcwaechter-desktop` | public + PKCE (`publicClient=true`, `standardFlow=true`) | `http://localhost:8765/callback`, `http://127.0.0.1:8765/callback` | vorhanden |

Hinweis:
- `provision-realm.sh` setzt Audience-Mapper explizit via `included.custom.audience = pcwaechter-api` (`add_audience_mapper`).
- PKCE (`pkce.code.challenge.method = S256`) ist in Console/Desktop-Clientdefinitionen gesetzt.

## Rollen / Gruppen / Mappers

| Kategorie | IST | Nachweis |
|---|---|---|
| Realm-Rollen | `pcw_admin`, `pcw_console`, `pcw_user`, `pcw_support`, `pcw_agent` (+ default KC roles) | Runtime `kcadm get roles -r pcwaechter-prod` |
| Gruppen | `pcw-admins`, `pcw-console`, `pcw-support`, `pcw-users` | Runtime `kcadm get groups -r pcwaechter-prod` |
| Gruppen->Rollen | `pcw-admins->pcw_admin`, `pcw-console->pcw_console`, `pcw-support->pcw_support`, `pcw-users->pcw_user` | Runtime `kcadm get groups/<id>/role-mappings/realm/composite` |
| Audience Mapper | `pcwaechter-api-audience` | `provision-realm.sh` + Runtime Mapper-Auswertung |
| Roles Mapper | `realm-roles` (bei UI-Clients) | `provision-realm.sh` + Runtime Mapper-Auswertung |

## API-Validierung gegen Keycloak

| Check | IST | Nachweis |
|---|---|---|
| JWKS Signature Verify | aktiv | `security_jwt.py:_verify_token` |
| `aud`-Prüfung | aktiv (`settings.KEYCLOAK_AUDIENCE`) | `security_jwt.py` Zeile mit `audience=` |
| `iss`-Prüfung | aktiv (`_expected_issuer`) | `security_jwt.py` Zeile mit `issuer=` |
| Token-Guard für Console/Home | aktiv (`require_console_user/owner`, `require_home_user`) | `security_jwt.py` |

## App ↔ Client Zuordnung (IST)

| App | Keycloak Client | Quelle |
|---|---|---|
| Console (Vite) | aktuell `console` (Env-Default), parallel existiert `pcwaechter-console` | `.env`, compose build args, Runtime Clients |
| Home (NextAuth) | aktuell `home` (Env-Default), parallel existiert `pcwaechter-home` | `.env`, compose env, Runtime Clients |
| API (resource/audience) | `pcwaechter-api` | `.env(.example)`, `security_jwt.py` |
| Desktop Client | `pcwaechter-desktop` | `provision-realm.sh`, Runtime Clients |

## Bewertung
- Realm und Pflicht-Clients sind vorhanden und funktionsfähig.
- Audience-Mapper für `aud=pcwaechter-api` ist nachweisbar.
- Es existiert ein Dual-Set aus canonical (`pcwaechter-*`) und legacy (`console`,`home`) Clients; das ist funktional, aber erhöht Betriebs-Komplexität.

## Unknown / Nicht nachweisbar
- Kein versionierter Realm-Export (`*.json`) im Repo gefunden, der den kompletten Live-Realm 1:1 abbildet. Quelle fehlt: exportierte Realm-JSON-Datei unter `server/keycloak/`.
