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
| Public Hostname | Forward Host | Port |
|---|---|---|
| `login.pcwächter.de` | `pcw-keycloak` | 8080 |
| `api.pcwächter.de` | `pcw-api` | 8000 |
| `home.pcwächter.de` | `pcw-home` | 3000 |
| `console.pcwächter.de` | `pcw-console` | 80 |
| `support.pcwächter.de` (optional) | `pcw-zammad` | 3001 |

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
- Keycloak health: `http://<server>:8080/health/ready`
- API health: `http://<server>:8000/health`
- Home: `http://<server>:3000`
- Console: `http://<server>:5173` (container exposed as 80)

## 7) Webhooks
- Stripe → `https://api.pcwächter.de/v1/payments/webhook`
- Zammad → `https://api.pcwächter.de/v1/support/webhook`

## 8) Production Notes
- Postgres extern nicht veröffentlichen
- Secrets in `.env` nicht committen
- Backups einrichten (Postgres + Keycloak export + Zammad volumes)
