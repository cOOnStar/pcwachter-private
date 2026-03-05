# Keycloak (`server/keycloak`)

Dieser Ordner enthaelt den kompletten Login-Stack mit Keycloak und Gateway.

## Start

```bash
cd server/keycloak
cp .env.example .env
docker compose up -d
```

## Enthalten

- `keycloak` (OIDC/Realm/Auth)
- `keycloak-gateway` (Domain-Proxy auf Port 18083)
- `keycloak-theme/` (Custom Theme `pcwaechter`)

## Voraussetzungen

- Netzwerk `pcwaechter-infra` existiert
- Postgres-Container `pcwaechter-postgres` laeuft
- DB/Rolle `keycloak` ist angelegt (Script: `scripts/setup-keycloak-db-remote.sh`)
