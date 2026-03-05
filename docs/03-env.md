# ENV Variablen – PCWächter v6.2

Alle Variablen werden in `.env` (Root) gesetzt. Vorlage: `.env.example`.

## Datenbank

| Variable | Beispiel | Beschreibung |
|---|---|---|
| `POSTGRES_DB` | `pcwaechter` | Datenbankname |
| `POSTGRES_USER` | `pcwaechter` | DB-Benutzer |
| `POSTGRES_PASSWORD` | `CHANGE_ME` | DB-Passwort |

## Keycloak Admin

| Variable | Beispiel | Beschreibung |
|---|---|---|
| `KC_ADMIN_USER` | `admin` | Keycloak Admin-User |
| `KC_ADMIN_PASSWORD` | `CHANGE_ME` | Keycloak Admin-Passwort |
| `KC_LOG_LEVEL` | `INFO` | Log-Level |

## API – Keycloak Integration

| Variable | Beispiel | Beschreibung |
|---|---|---|
| `KEYCLOAK_URL` | `http://keycloak:8080` | Interne Keycloak-URL (Container→Container) |
| `KEYCLOAK_REALM` | `pcwaechter-prod` | Realm-Name |
| `KEYCLOAK_AUDIENCE` | `pcwaechter-api` | Erwartete JWT `aud` |
| `KEYCLOAK_ADMIN_CLIENT_ID` | `admin-cli` | Admin Client ID |
| `KEYCLOAK_ADMIN_CLIENT_SECRET` | – | Admin Client Secret (optional) |

## API – Zugriffskontrolle

| Variable | Beispiel | Beschreibung |
|---|---|---|
| `API_KEYS` | `key1,key2` | API-Keys für externe Tools |
| `AGENT_API_KEYS` | `agent_key` | API-Keys für Windows-Agenten |
| `CONSOLE_ALLOWED_ROLES` | `pcw_admin,pcw_console` | Keycloak-Rollen mit Console-Zugriff |
| `CORS_ORIGINS` | `https://console.pcwächter.de,https://home.pcwächter.de` | Erlaubte CORS-Origins (kommagetrennt) |

## API – Zammad Integration (optional)

| Variable | Beispiel | Beschreibung |
|---|---|---|
| `ZAMMAD_BASE_URL` | `https://support.xn--pcwchter-2za.de` | Zammad API URL |
| `ZAMMAD_API_TOKEN` | `CHANGE_ME` | Zammad API Token |

## API – Storage

| Variable | Beispiel | Beschreibung |
|---|---|---|
| `EXPORT_DIR` | `/data/exports` | Pfad für Exporte |
| `UPLOAD_DIR` | `/data/uploads` | Pfad für Uploads |

## API – Sonstiges

| Variable | Beispiel | Beschreibung |
|---|---|---|
| `LOG_LEVEL` | `INFO` | API Log-Level (DEBUG/INFO/WARNING/ERROR) |
| `ONLINE_THRESHOLD_SECONDS` | `90` | Sekunden bis Gerät als offline gilt |

## Console (Vite Build-Args – öffentlich)

| Variable | Beispiel | Beschreibung |
|---|---|---|
| `VITE_API_URL` | `https://api.pcwächter.de` | API-Base-URL für Browser |
| `VITE_KEYCLOAK_URL` | `https://login.pcwächter.de` | Keycloak-URL für Browser |
| `VITE_KEYCLOAK_REALM` | `pcwaechter-prod` | Realm |
| `VITE_KEYCLOAK_CLIENT_ID` | `console` | Keycloak Client ID |

## Home Portal (Build-Args + Runtime)

| Variable | Beispiel | Beschreibung |
|---|---|---|
| `NEXT_PUBLIC_API_URL` | `https://api.pcwächter.de` | API-Base-URL für Browser |
| `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` | `pk_live_...` | Stripe Public Key |
| `AUTH_KEYCLOAK_ID` | `home` | NextAuth Keycloak Client ID |
| `AUTH_KEYCLOAK_SECRET` | `CHANGE_ME` | NextAuth Keycloak Client Secret |
| `AUTH_KEYCLOAK_ISSUER` | `https://login.pcwächter.de/realms/pcwaechter-prod` | OIDC Issuer URL |
| `AUTH_SECRET` | (32 Zeichen random) | NextAuth Session Secret |
| `API_INTERNAL_URL` | `http://api:8000` | Interne API-URL (Server→Server) |

## Stripe (optional)

| Variable | Beschreibung |
|---|---|
| `STRIPE_SECRET_KEY` | Stripe Secret Key (`sk_live_...`) |
| `STRIPE_WEBHOOK_SECRET` | Webhook Signing Secret (`whsec_...`) |
| `STRIPE_PUBLISHABLE_KEY` | Publishable Key (`pk_live_...`) |

## Zammad (optional – Docker-Profil `zammad`)

| Variable | Beispiel |
|---|---|
| `ZAMMAD_POSTGRES_DB` | `zammad` |
| `ZAMMAD_POSTGRES_USER` | `zammad` |
| `ZAMMAD_POSTGRES_PASSWORD` | `CHANGE_ME` |
| `ZAMMAD_HOSTNAME` | `support.xn--pcwchter-2za.de` |

## Docker Images (Produktion)

| Variable | Beschreibung |
|---|---|
| `PCW_KEYCLOAK_IMAGE` | Keycloak Image (leer = lokaler Build) |
| `PCW_API_IMAGE` | API Image (leer = lokaler Build) |
| `PCW_CONSOLE_IMAGE` | Console Image (leer = lokaler Build) |
| `PCW_HOME_IMAGE` | Home Image (leer = lokaler Build) |
