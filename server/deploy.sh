#!/usr/bin/env bash
# =============================================================================
# PCWächter – Server Deployment Script
# =============================================================================
# Nutzung:
#   bash server/deploy.sh              # alle Services
#   bash server/deploy.sh api          # nur API
#   bash server/deploy.sh console      # nur Console
#   bash server/deploy.sh home         # nur Home Portal
#   bash server/deploy.sh keycloak     # nur Keycloak
# =============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"
COMPOSE_FILE="$SCRIPT_DIR/infra/compose/docker-compose.yml"
ENV_FILE="$ROOT_DIR/.env"
TARGET="${1:-all}"

log()  { echo "[$(date '+%H:%M:%S')] $*"; }
ok()   { echo "[$(date '+%H:%M:%S')] ✓ $*"; }
fail() { echo "[$(date '+%H:%M:%S')] ✗ $*" >&2; exit 1; }

COMPOSE="docker compose -f $COMPOSE_FILE --env-file $ENV_FILE"

# ── Vorprüfungen ──────────────────────────────────────────────────────────────
command -v docker >/dev/null 2>&1 || fail "docker nicht gefunden"
[ -f "$COMPOSE_FILE" ] || fail "Compose-Datei nicht gefunden: $COMPOSE_FILE"
[ -f "$ENV_FILE" ] || fail ".env fehlt in $ROOT_DIR – bitte .env.example kopieren und befüllen"

# ── Deploy-Funktionen ─────────────────────────────────────────────────────────
deploy_keycloak() {
  log "Keycloak bauen und starten..."
  $COMPOSE build keycloak
  $COMPOSE up -d keycloak
  ok "Keycloak gestartet"
}

deploy_api() {
  log "API deployen..."
  $COMPOSE build api
  $COMPOSE up -d api

  log "Datenbankmigrationen ausführen..."
  docker exec pcw-api alembic upgrade head && ok "Migrationen erfolgreich" || log "Migration fehlgeschlagen (ggf. bereits aktuell)"
  ok "API gestartet"
}

deploy_console() {
  log "Admin Console deployen..."
  $COMPOSE build console
  $COMPOSE up -d console
  ok "Console gestartet"
}

deploy_home() {
  log "Home Portal deployen..."
  $COMPOSE build home
  $COMPOSE up -d home
  ok "Home Portal gestartet"
}

cleanup() {
  log "Aufräumen..."
  docker image prune -f >/dev/null
  ok "Ungenutzte Images entfernt"
}

show_status() {
  echo ""
  echo "═══════════════════════════════════════"
  echo " Service Status"
  echo "═══════════════════════════════════════"
  $COMPOSE ps --format "table {{.Name}}\t{{.Status}}\t{{.Ports}}" 2>/dev/null || true
  echo ""
}

# ── Hauptlogik ────────────────────────────────────────────────────────────────
echo ""
echo "╔══════════════════════════════════════╗"
echo "║   PCWächter Deployment               ║"
echo "║   Target: $TARGET"
echo "╚══════════════════════════════════════╝"
echo ""

case "$TARGET" in
  all)
    deploy_keycloak
    deploy_api
    deploy_console
    deploy_home
    ;;
  keycloak)
    deploy_keycloak
    ;;
  api)
    deploy_api
    ;;
  console)
    deploy_console
    ;;
  home)
    deploy_home
    ;;
  *)
    echo "Unbekannter Target: $TARGET"
    echo "Gültige Optionen: all | keycloak | api | console | home"
    exit 1
    ;;
esac

cleanup
show_status

echo "✓ Deployment abgeschlossen"
