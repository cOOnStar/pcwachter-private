# Backend Settings

Konfiguration via `pydantic-settings` in `server/api/app/settings.py`.
Alle Werte werden aus Umgebungsvariablen gelesen (Docker Compose → `.env`).

## Vollständige Settings-Übersicht

| Variable | Typ | Default | Beschreibung |
|---|---|---|---|
| `DATABASE_URL` | str | `postgresql+psycopg://...` | PostgreSQL Connection String |
| `ONLINE_THRESHOLD_SECONDS` | int | `90` | Sekunden bis Gerät als offline gilt |
| `API_KEYS` | str | `""` | Kommagetrennte API-Keys (Admin/externe Tools) |
| `AGENT_API_KEYS` | str | `""` | Kommagetrennte API-Keys für Windows-Agenten |
| `RATELIMIT_REDIS_URL` | str | `""` | Optionaler Redis-Storage für Rate Limits (Fallback: Memory) |
| `KEYCLOAK_URL` | str | `https://login.xn--pcwchter-2za.de` | Keycloak-URL (intern: http://keycloak:8080) |
| `KEYCLOAK_REALM` | str | `pcwaechter-prod` | Realm-Name |
| `KEYCLOAK_AUDIENCE` | str | `pcwaechter-api` | Erwartete JWT `aud` Claim |
| `KEYCLOAK_ADMIN_USER` | str | `""` | Keycloak Admin-Username |
| `KEYCLOAK_ADMIN_PASSWORD` | str | `""` | Keycloak Admin-Passwort |
| `KEYCLOAK_ADMIN_CLIENT_ID` | str | `admin-cli` | Admin Client ID |
| `KEYCLOAK_ADMIN_CLIENT_SECRET` | str | `""` | Admin Client Secret |
| `CONSOLE_ALLOWED_ROLES` | str | `pcw_admin,pcw_console` | Rollen mit Console-Zugriff |
| `CORS_ORIGINS` | str | prod URLs | Erlaubte CORS-Origins (kommagetrennt) |
| `ZAMMAD_BASE_URL` | str | `""` | Zammad API URL |
| `ZAMMAD_API_TOKEN` | str | `""` | Zammad API Token |
| `EXPORT_DIR` | str | `/data/exports` | Pfad für Export-Dateien |
| `UPLOAD_DIR` | str | `/data/uploads` | Pfad für Upload-Dateien |
| `LOG_LEVEL` | str | `INFO` | Log-Level (DEBUG/INFO/WARNING/ERROR) |
| `FIGMA_PREVIEW_KEY` | str | `""` | API-Key für Figma Preview |
| `STRIPE_SECRET_KEY` | str | `""` | Stripe Secret Key |
| `STRIPE_WEBHOOK_SECRET` | str | `""` | Stripe Webhook Signing Secret |
| `STRIPE_PUBLISHABLE_KEY` | str | `""` | Stripe Publishable Key |

## CORS-Konfiguration

CORS-Origins werden aus `settings.CORS_ORIGINS` gelesen (kommagetrennt).
Zusätzlich sind lokale Dev-Origins (localhost) immer erlaubt:

| Origin | Zweck |
|---|---|
| `https://console.xn--pcwchter-2za.de` | Produktion Console |
| `https://home.xn--pcwchter-2za.de` | Produktion Home Portal |
| `http://localhost:5173` | Vite Dev-Server |
| `http://localhost:3000` | Next.js Dev-Server |
| `http://localhost:13000` | Docker Console |
| `http://localhost:13001` | Docker Home |
| `https://make.figma.com` | Figma Make Preview |
| `https://www.figma.com` | Figma |

## Dateipfad

`server/api/app/settings.py`
