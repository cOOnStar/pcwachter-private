COMPOSE = docker compose -f server/infra/compose/docker-compose.yml --env-file .env

.PHONY: help up down build deploy logs ps migrate db-init keycloak-setup

# ── Hilfe ─────────────────────────────────────────────────────────────────────
help:
	@echo ""
	@echo "PCWächter – Makefile Übersicht"
	@echo "────────────────────────────────────────"
	@echo "  make up             alle Services starten (Postgres, Keycloak, API, Home, Console)"
	@echo "  make down           alle Services stoppen"
	@echo "  make build          alle Images lokal bauen"
	@echo "  make build-api      nur API bauen"
	@echo "  make build-console  nur Console bauen"
	@echo "  make build-home     nur Home Portal bauen"
	@echo "  make deploy         Server-Deploy ausführen"
	@echo "  make deploy-home    nur Home Portal deployen"
	@echo "  make logs           alle Logs (follow)"
	@echo "  make logs-api       API Logs"
	@echo "  make logs-home      Home Portal Logs"
	@echo "  make ps             Container-Status"
	@echo "  make migrate        Alembic-Migrationen ausführen"
	@echo "  make db-init        Idempotent Greenfield-Init (bootstrap + alembic)"
	@echo "  make keycloak-setup Keycloak Clients einrichten"
	@echo ""

# ── Starten / Stoppen ─────────────────────────────────────────────────────────
up:
	$(COMPOSE) up -d
	@echo "✓ Alle Services gestartet"

down:
	$(COMPOSE) down

# ── Bauen ─────────────────────────────────────────────────────────────────────
build: build-api build-console build-home

build-api:
	$(COMPOSE) build api

build-console:
	$(COMPOSE) build console

build-home:
	$(COMPOSE) build home

# ── Deployen ──────────────────────────────────────────────────────────────────
deploy:
	bash server/deploy.sh all

deploy-api:
	bash server/deploy.sh api

deploy-console:
	bash server/deploy.sh console

deploy-home:
	bash server/deploy.sh home

# ── Logs ──────────────────────────────────────────────────────────────────────
logs:
	$(COMPOSE) logs -f

logs-api:
	$(COMPOSE) logs -f api

logs-console:
	$(COMPOSE) logs -f console

logs-home:
	$(COMPOSE) logs -f home

logs-keycloak:
	$(COMPOSE) logs -f keycloak

# ── Status ────────────────────────────────────────────────────────────────────
ps:
	$(COMPOSE) ps

# ── Migrationen ───────────────────────────────────────────────────────────────
migrate:
	docker exec pcw-api alembic upgrade head

db-init:
	$(COMPOSE) --profile db-init run --rm db-init

migrate-history:
	docker exec pcw-api alembic history

# ── Keycloak Setup ────────────────────────────────────────────────────────────
keycloak-setup:
	@test -n "$(KEYCLOAK_ADMIN_PASSWORD)" || (echo "KEYCLOAK_ADMIN_PASSWORD not set. Nutzung: make keycloak-setup KEYCLOAK_ADMIN_PASSWORD=..." && exit 1)
	KEYCLOAK_ADMIN_PASSWORD=$(KEYCLOAK_ADMIN_PASSWORD) bash server/keycloak/setup-clients.sh

# ── Aufräumen ─────────────────────────────────────────────────────────────────
clean:
	docker image prune -f
	docker volume prune -f

clean-all: down
	docker system prune -af --volumes
