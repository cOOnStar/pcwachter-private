# Deploy Guide (Skeleton) – PCWächter

## Voraussetzungen
- Docker + Docker Compose
- DNS Subdomains: `api`, `home`, `console`, `login` (optional: `support`, `cdn`)
- Reverse Proxy: Nginx Proxy Manager (empfohlen)

## 1) Dateien
- `server/infra/compose/docker-compose.yml`
- `.env` (aus `.env.example` erstellen)

## 2) Start (lokal/Server)
```bash
cd server/infra/compose
cp ../../../.env.example .env   # oder pfad anpassen
docker compose --env-file .env up -d
```

## 3) Nginx Proxy Manager – Proxy Hosts
| Public Hostname | Forward Host | Forward Port | Host Port |
|---|---|---|---|
| `login.xn--pcwchter-2za.de` | `localhost` | 18083 | (Keycloak) |
| `api.xn--pcwchter-2za.de` | `localhost` | 18080 | (FastAPI) |
| `home.xn--pcwchter-2za.de` | `localhost` | 13001 | (Next.js) |
| `console.xn--pcwchter-2za.de` | `localhost` | 13000 | (React SPA) |
| `support.xn--pcwchter-2za.de` (optional) | `localhost` | 3001 | (Zammad) |

### Wichtige Proxy Einstellungen
- Websockets: ON
- Forwarded Headers: ON
- Optional: Upload size limit erhöhen (Attachments)

## 4) Keycloak: Realm/Clients anlegen
Siehe `docs/keycloak-setup.md`.

## 5) Backend DB Migrations
Im API-Container (abhängig vom Image):
```bash
docker exec -it pcw-api alembic upgrade head
```

## 6) Smoke Tests
- Keycloak health: `http://<server>:18083/health/ready`
- API health: `http://<server>:18080/health`
- Home: `http://<server>:13001`
- Console: `http://<server>:13000`

## 7) Webhooks
- Stripe → `https://api.xn--pcwchter-2za.de/v1/payments/webhook`
- Zammad → `https://api.xn--pcwchter-2za.de/v1/support/webhook`

## 8) Production Notes
- Postgres extern nicht veröffentlichen
- Secrets in `.env` nicht committen
- Backups einrichten (Postgres + Keycloak export + Zammad volumes)
