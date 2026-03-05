# Compose & ENV Inventory

## Quellen
- Haupt-Compose: `server/infra/compose/docker-compose.yml`.
- Zammad standalone (optional): `server/zammad/docker-compose.yml`.
- Caddy-Compose (separat): `server/infra/caddy/docker-compose.yml`.
- ENV-Vorlagen: `.env.example`, `server/api/.env.example`.
- API-Settings: `server/api/app/settings.py`.
- Keycloak Provisioning: `server/keycloak/provision-realm.sh`.

## Compose: Services / Ports / Networks / Volumes

### Netzwerke
| Netzwerk | Definition | Nachweis |
|---|---|---|
| `pcw-internal` | Hauptnetz für `postgres`, `keycloak`, `api`, `console`, `home` | `server/infra/compose/docker-compose.yml:5-7` |
| `zammad-internal` | Optionales Zammad-Profil-Netz | `server/infra/compose/docker-compose.yml:8-9` |

### Volumes
| Volume | Zweck (aus Mounts ableitbar) | Nachweis |
|---|---|---|
| `pg_data` | PostgreSQL Daten | `server/infra/compose/docker-compose.yml:12`, `33-35` |
| `exports_data` | API Export-Verzeichnis | `server/infra/compose/docker-compose.yml:13`, `145-146` |
| `uploads_data` | API Upload-Verzeichnis | `server/infra/compose/docker-compose.yml:14`, `145-146` |
| `zammad-*` Volumes | Zammad Daten/Storage/DB/Redis/Elasticsearch | `server/infra/compose/docker-compose.yml:15-19`, `221-223`, `242-244`, `261-263`, `288-290` |

### Services (Haupt-Compose)
| Service | Ports/Expose | Networks | Volumes | Nachweis |
|---|---|---|---|---|
| `postgres` | expose `5432` | `pcw-internal` | `pg_data` | `server/infra/compose/docker-compose.yml:25-44` |
| `keycloak` | host `18083:8080` | `pcw-internal` | Theme-Mount `../../keycloak/keycloak-theme/pcwaechter-v1` | `server/infra/compose/docker-compose.yml:48-89`, `75-77`, `82-83` |
| `db-init` (profile `db-init`) | none (one-shot) | `pcw-internal` | none | `server/infra/compose/docker-compose.yml:93-107` |
| `api` | host `18080:8000` | `pcw-internal` | `exports_data`, `uploads_data` | `server/infra/compose/docker-compose.yml:111-159`, `145-149`, `153-154` |
| `console` | host `13000:80` | `pcw-internal` | none | `server/infra/compose/docker-compose.yml:164-188`, `181-182` |
| `home` | host `13001:3000` | `pcw-internal` | none | `server/infra/compose/docker-compose.yml:192-223`, `217-218` |
| `zammad-*` (profile `zammad`) | `zammad-nginx` host `3001:3001` | `zammad-internal` (+ `pcw-internal` bei nginx) | mehrere `zammad-*` Volumes | `server/infra/compose/docker-compose.yml:230-426` |

### Zammad optional (Standalone-Datei)
- Separate Compose-Datei mit denselben Kernservices (`zammad-elasticsearch`, `zammad-redis`, `zammad-db`, `zammad-init`, `zammad-railsserver`, `zammad-scheduler`, `zammad-websocket`, `zammad-nginx`).
- Nachweis: `server/zammad/docker-compose.yml:23-246`.

### Caddy (separates Infra-Compose)
- Exponiert `80`, `443`, `443/udp` und nutzt externes Netzwerk `${DOCKER_NETWORK_NAME:-pcwaechter-infra}`.
- Nachweis: `server/infra/caddy/docker-compose.yml:1-27`.

## ENV-Variablen (erforderlich laut Repo)

### Infrastruktur / Plattform
| Variable | Quelle(n) |
|---|---|
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | `.env.example:19-22`, `server/infra/compose/docker-compose.yml:30-33` |
| `PCW_KEYCLOAK_IMAGE`, `PCW_API_IMAGE`, `PCW_CONSOLE_IMAGE`, `PCW_HOME_IMAGE` | `.env.example:11-14`, `server/infra/compose/docker-compose.yml:49`, `95`, `165`, `193` |
| `KC_ADMIN_USER`, `KC_ADMIN_PASSWORD`, `KC_LOG_LEVEL` | `.env.example:26-28`, `server/infra/compose/docker-compose.yml:61-63`, `69` |

### API / Security / Keycloak
| Variable | Quelle(n) |
|---|---|
| `DATABASE_URL` | `server/api/app/settings.py:5`, `server/infra/compose/docker-compose.yml:103`, `118` |
| `API_KEYS`, `AGENT_API_KEYS`, `RATELIMIT_REDIS_URL` | `server/api/app/settings.py:10-12`, `.env.example:45-47` |
| `AGENT_BOOTSTRAP_KEY`, `ALLOW_LEGACY_API_KEY_REGISTER` | `server/api/app/settings.py:16-19`, `.env.example:48-50`, `server/infra/compose/docker-compose.yml:122-123` |
| `KEYCLOAK_URL`, `KEYCLOAK_REALM`, `KEYCLOAK_AUDIENCE`, `KEYCLOAK_ISSUER` | `server/api/app/settings.py:21-24`, `.env.example:35-40`, `server/infra/compose/docker-compose.yml:124-132` |
| `KEYCLOAK_ADMIN_CLIENT_ID`, `KEYCLOAK_ADMIN_CLIENT_SECRET`, `KEYCLOAK_ADMIN_USER`, `KEYCLOAK_ADMIN_PASSWORD` | `server/api/app/settings.py:25-29`, `.env.example:37-38`, `server/infra/compose/docker-compose.yml:126-129` |
| `CONSOLE_ALLOWED_ROLES`, `CORS_ORIGINS` | `server/api/app/settings.py:31-35`, `.env.example:52-53`, `server/infra/compose/docker-compose.yml:130-132` |

### Home / Console
| Variable | Quelle(n) |
|---|---|
| `VITE_API_BASE_URL`, `VITE_KEYCLOAK_URL`, `VITE_KEYCLOAK_REALM`, `VITE_KEYCLOAK_CLIENT_ID` | `.env.example:59-62`, `server/infra/compose/docker-compose.yml:170-173`, `server/console/Dockerfile:8-15` |
| `NEXT_PUBLIC_API_URL`, `AUTH_KEYCLOAK_ID`, `AUTH_KEYCLOAK_SECRET`, `AUTH_KEYCLOAK_ISSUER`, `AUTH_SECRET`, `API_INTERNAL_URL` | `.env.example:67-74`, `server/infra/compose/docker-compose.yml:203-208`, `server/home/src/auth.ts:7-10` |

### Optional Integrationen
| Variable | Quelle(n) |
|---|---|
| `STRIPE_SECRET_KEY`, `STRIPE_WEBHOOK_SECRET`, `STRIPE_PUBLISHABLE_KEY` | `.env.example:79-81`, `server/api/app/settings.py:55-57`, `server/infra/compose/docker-compose.yml:136-138` |
| `ZAMMAD_BASE_URL`, `ZAMMAD_API_TOKEN`, `SUPPORT_ATTACHMENT_MAX_BYTES` | `.env.example:85-87`, `server/api/app/settings.py:39-40`, `46`, `server/infra/compose/docker-compose.yml:133-135` |
| `ZAMMAD_WEBHOOK_SECRET`, `ZAMMAD_DEFAULT_GROUP_ID`, `ZAMMAD_DEFAULT_ORG_ID`, `ZAMMAD_CUSTOMER_ROLE_ID` | `server/api/app/settings.py:41-45` (Compose-Wiring in `server/infra/compose/docker-compose.yml` nicht vorhanden) |
| `ZAMMAD_IMAGE`, `ZAMMAD_POSTGRES_DB`, `ZAMMAD_POSTGRES_USER`, `ZAMMAD_POSTGRES_PASSWORD`, `ZAMMAD_HOSTNAME` | `.env.example:94-98`, `server/infra/compose/docker-compose.yml:291`, `276-278`, `407-408` |
| `FIGMA_PREVIEW_KEY` | `.env.example:90`, `server/api/app/settings.py:52`, `server/infra/compose/docker-compose.yml:139` |

## Keycloak / Issuer / Audience / Role-Mapping

### Was ist vorhanden (Code/Config)
- JWT-Prüfung nutzt JWKS, validiert `aud` und `iss` strikt.
- Nachweis: `server/api/app/security_jwt.py:138-152`.
- Erwarteter Issuer wird aus `KEYCLOAK_ISSUER` oder OIDC Discovery oder Fallback `${KEYCLOAK_URL}/realms/${KEYCLOAK_REALM}` bestimmt.
- Nachweis: `server/api/app/security_jwt.py:82-91`, `.env.example:40`.
- Audience ist als `KEYCLOAK_AUDIENCE` konfiguriert (`pcwaechter-api` in Vorlage).
- Nachweis: `server/api/app/settings.py:23`, `.env.example:39`.
- Realm-Provisioning legt `pcw_*` Rollen, Gruppenzuordnung, Audience-Mapper und Roles-Mapper an.
- Nachweis: `server/keycloak/provision-realm.sh:87-123`, `150-173`, `175-199`, `202-371`.
- Console/Home Frontends lesen Keycloak-Claims/Rollen und nutzen OIDC Clients.
- Nachweis: `server/console/src/app/context/auth-context.tsx:39-43`, `82-95`; `server/home/src/auth.ts:7-10`.

### Was ist unknown
- Ob die laufende Produktiv-Instanz exakt mit `provision-realm.sh` übereinstimmt (Clients, Mapper, Gruppen): `unknown`.
- Fehlende Quelle: versionierter Realm-Export (`*.json`) im Repo.
- Verifikations-Command (extern, nicht im Repo ausführbar ohne Zielsystem):
  - `curl "$KEYCLOAK_URL/realms/$REALM/.well-known/openid-configuration"`
  - `kcadm.sh get clients -r "$REALM"`

- Ob `.env` in Zielumgebung `ALLOW_LEGACY_API_KEY_REGISTER=false` gesetzt ist: `unknown`.
- Fehlende Quelle: deploytes `.env`/Secret-Store ist nicht im Repo versioniert.
- Ob `ZAMMAD_WEBHOOK_SECRET` / `ZAMMAD_DEFAULT_GROUP_ID` / `ZAMMAD_DEFAULT_ORG_ID` / `ZAMMAD_CUSTOMER_ROLE_ID` in Zielumgebungen aktiv gesetzt werden: `unknown`.
- Fehlende Quelle: produktive Compose/Secret-Store Werte sind nicht im Repo.
