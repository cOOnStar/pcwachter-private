# Docker / Infra

## Unified Compose

- `server/infra/compose/docker-compose.yml`

## Services

| Service | Image | Port (Host→Container) | Beschreibung |
|---|---|---|---|
| `postgres` | postgres:17 | 5432→5432 | PostgreSQL Datenbank |
| `keycloak` | pcw-keycloak:local | 18083→8080 | Keycloak Identity Provider |
| `api` | pcw-api:local | 18080→8000 | FastAPI Backend |
| `console` | pcw-console:local | 13000→80 | Admin Console (React/Vite) |
| `home` | pcw-home:local | 13001→3000 | Kundenportal (Next.js) |
| `zammad-nginx` (optional) | ghcr.io/zammad/zammad | 3001→3001 | Support-Ticket-System (Frontend) |

## Start

```bash
# Aus dem Root-Verzeichnis
make up

# Oder direkt
docker compose -f server/infra/compose/docker-compose.yml --env-file .env up -d

# Zammad optional aktivieren
docker compose -f server/infra/compose/docker-compose.yml --env-file .env --profile zammad up -d
```

## Volumes

| Volume | Inhalt |
|---|---|
| `pg_data` | PostgreSQL Datenbankdaten |
| `exports_data` | API Export-Dateien |
| `uploads_data` | API Upload-Dateien |

## Netzwerk

Alle Services laufen im internen Bridge-Netzwerk `pcw-internal`. Nur die o.g. Ports werden nach außen exponiert.

## Keycloak Konfiguration

| Variable | Wert | Beschreibung |
|---|---|---|
| `KC_DB` | postgres | Datenbank-Typ |
| `KC_HEALTH_ENABLED` | true | Health-Endpoint aktivieren |
| `KC_METRICS_ENABLED` | false | Metrics-Endpoint (deaktiviert) |
| `KC_PROXY_HEADERS` | xforwarded | Proxy-Header für NPM/TLS |
| `KC_HTTP_ENABLED` | true | HTTP intern erlaubt |
| `KC_HOSTNAME_STRICT` | false | Kein Hostname-Strict (intern flexibel) |
| `KC_HOSTNAME` | `https://login.xn--pcwchter-2za.de` | Öffentliche URL (Prod) |
| `KC_LOG_LEVEL` | `${KC_LOG_LEVEL:-INFO}` | Log-Level |

## Logging (Log-Limits)

Alle Services verwenden das `json-file`-Log-Driver mit Größenbeschränkung:

```yaml
logging:
  driver: json-file
  options:
    max-size: "10m"
    max-file: "3"
```

## Reverse Proxy (NPM)

| Subdomain | Externer Port (NPM) | Lokaler Port |
|---|---|---|
| `login.xn--pcwchter-2za.de` | 443 | 18083 |
| `api.xn--pcwchter-2za.de` | 443 | 18080 |
| `console.xn--pcwchter-2za.de` | 443 | 13000 |
| `home.xn--pcwchter-2za.de` | 443 | 13001 |
| `support.xn--pcwchter-2za.de` | 16232 | 3001 |

Keycloak benötigt diese Header-Konfiguration in NPM:
```
X-Forwarded-Proto: https
X-Forwarded-Host: login.xn--pcwchter-2za.de
```
