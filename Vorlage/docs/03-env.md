# ENV Variablen (erkannt)

| Variable | Fundstellen (Auszug) |
|---|---|
| `ADMIN_API` | keycloak/keycloak/setup-clients.sh |
| `ADMIN_PASS` | keycloak/keycloak/setup-clients.sh |
| `ADMIN_USER` | keycloak/keycloak/setup-clients.sh |
| `AGENT_API_KEYS` | api/api/infra/compose/docker-compose.yml |
| `API_IMAGE` | api/api/infra/compose/docker-compose.yml |
| `API_KEYS` | api/api/infra/compose/docker-compose.yml |
| `CONSOLE_ALLOWED_ROLES` | api/api/infra/compose/docker-compose.yml |
| `CONSOLE_IMAGE` | api/api/infra/compose/docker-compose.yml |
| `CONSOLE_KEYCLOAK_CLIENT_ID` | api/api/infra/compose/docker-compose.yml |
| `DATABASE_URL` | api/api/alembic/env.py, api/api/app/db.py, api/api/app/settings.py, api/api/infra/compose/docker-compose.yml |
| `DOCKER_NETWORK_NAME` | api/api/infra/compose/docker-compose.yml, db/datenbank/docker-compose.yml, infra/infra/caddy/docker-compose.yml, keycloak/keycloak/docker-compose.yml |
| `HOME_API_INTERNAL_URL` | api/api/infra/compose/docker-compose.yml |
| `HOME_AUTH_SECRET` | api/api/infra/compose/docker-compose.yml |
| `HOME_CLIENT_SECRET` | keycloak/keycloak/setup-clients.sh |
| `HOME_IMAGE` | api/api/infra/compose/docker-compose.yml |
| `HOME_KEYCLOAK_CLIENT_ID` | api/api/infra/compose/docker-compose.yml |
| `HOME_KEYCLOAK_CLIENT_SECRET` | api/api/infra/compose/docker-compose.yml |
| `HOME_SECRET` | keycloak/keycloak/setup-clients.sh |
| `KC_BOOTSTRAP_ADMIN_PASSWORD` | keycloak/keycloak/docker-compose.yml |
| `KC_BOOTSTRAP_ADMIN_USERNAME` | keycloak/keycloak/docker-compose.yml |
| `KC_DB` | keycloak/keycloak/docker-compose.yml |
| `KC_DB_PASSWORD` | keycloak/keycloak/docker-compose.yml |
| `KC_DB_URL` | keycloak/keycloak/docker-compose.yml |
| `KC_DB_USERNAME` | keycloak/keycloak/docker-compose.yml |
| `KC_HEALTH_ENABLED` | keycloak/keycloak/docker-compose.yml |
| `KC_HOSTNAME` | keycloak/keycloak/docker-compose.yml |
| `KC_HTTP_ENABLED` | keycloak/keycloak/docker-compose.yml |
| `KC_METRICS_ENABLED` | keycloak/keycloak/docker-compose.yml |
| `KC_PROXY_HEADERS` | keycloak/keycloak/docker-compose.yml |
| `KC_SPI_THEME_WELCOME_THEME` | keycloak/keycloak/docker-compose.yml |
| `KEYCLOAK_ADMIN_CLIENT_ID` | api/api/README.md, api/api/app/routers/console.py, api/api/app/settings.py, api/api/infra/compose/docker-compose.yml |
| `KEYCLOAK_ADMIN_CLIENT_SECRET` | api/api/README.md, api/api/app/routers/console.py, api/api/app/settings.py, api/api/infra/compose/docker-compose.yml |
| `KEYCLOAK_ADMIN_PASSWORD` | api/api/README.md, api/api/app/routers/console.py, api/api/app/settings.py, api/api/infra/compose/docker-compose.yml, keycloak/keycloak/docker-compose.yml, keycloak/keycloak/setup-clients.sh |
| `KEYCLOAK_ADMIN_USER` | api/api/README.md, api/api/app/routers/console.py, api/api/app/settings.py, api/api/infra/compose/docker-compose.yml, keycloak/keycloak/docker-compose.yml, keycloak/keycloak/setup-clients.sh |
| `KEYCLOAK_DB_HOST` | keycloak/keycloak/docker-compose.yml |
| `KEYCLOAK_DB_NAME` | keycloak/keycloak/docker-compose.yml |
| `KEYCLOAK_DB_PASSWORD` | keycloak/keycloak/docker-compose.yml |
| `KEYCLOAK_DB_PORT` | keycloak/keycloak/docker-compose.yml |
| `KEYCLOAK_DB_USER` | keycloak/keycloak/docker-compose.yml |
| `KEYCLOAK_IMAGE` | keycloak/keycloak/docker-compose.yml |
| `KEYCLOAK_PUBLIC_URL` | keycloak/keycloak/docker-compose.yml |
| `KEYCLOAK_REALM` | api/api/app/routers/console.py, api/api/app/security_jwt.py, api/api/app/settings.py, api/api/infra/compose/docker-compose.yml, keycloak/keycloak/setup-clients.sh |
| `KEYCLOAK_URL` | api/api/app/routers/console.py, api/api/app/security_jwt.py, api/api/app/settings.py, api/api/infra/compose/docker-compose.yml, keycloak/keycloak/setup-clients.sh |
| `LOGIN_GATEWAY_PORT` | keycloak/keycloak/docker-compose.yml |
| `PCW_LICENSE_KEYS` | keycloak/keycloak/docker-compose.yml |
| `POSTGRES_DB` | api/api/infra/compose/docker-compose.yml, db/datenbank/docker-compose.yml |
| `POSTGRES_EXPOSE_PORT` | db/datenbank/docker-compose.yml |
| `POSTGRES_HOST` | api/api/infra/compose/docker-compose.yml |
| `POSTGRES_IMAGE_TAG` | db/datenbank/docker-compose.yml |
| `POSTGRES_PASSWORD` | api/api/infra/compose/docker-compose.yml, db/datenbank/README.md, db/datenbank/docker-compose.yml |
| `POSTGRES_PORT` | api/api/infra/compose/docker-compose.yml |
| `POSTGRES_USER` | api/api/infra/compose/docker-compose.yml, db/datenbank/README.md, db/datenbank/docker-compose.yml |
| `POSTGRES_VOLUME_NAME` | db/datenbank/docker-compose.yml |
| `REALM` | keycloak/keycloak/setup-clients.sh |
| `STRIPE_PUBLISHABLE_KEY` | api/api/app/settings.py, api/api/infra/compose/docker-compose.yml |
| `STRIPE_SECRET_KEY` | api/api/app/routers/payments.py, api/api/app/settings.py, api/api/infra/compose/docker-compose.yml |
| `STRIPE_WEBHOOK_SECRET` | api/api/app/routers/payments.py, api/api/app/settings.py, api/api/infra/compose/docker-compose.yml |
| `UPDATES_IMAGE` | api/api/infra/compose/docker-compose.public-services.template.yml |
| `VITE_API_URL` | api/api/infra/compose/docker-compose.yml |
