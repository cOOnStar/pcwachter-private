#!/usr/bin/env bash
# =============================================================
# PCWächter – Zammad Post-Install Setup
# Erstellt Admin-User + API-Token und gibt den Token aus.
#
# Verwendung:
#   chmod +x server/zammad/setup.sh
#   ./server/zammad/setup.sh
#
# Voraussetzung: Zammad läuft bereits und ist erreichbar.
# =============================================================

set -euo pipefail

# ---------------------
# Konfiguration
# ---------------------
ZAMMAD_URL="${ZAMMAD_BASE_URL:-http://localhost:3001}"
ADMIN_EMAIL="${ZAMMAD_ADMIN_EMAIL:-admin@pcwaechter.de}"
ADMIN_PASSWORD="${ZAMMAD_ADMIN_PASSWORD:-CHANGE_ME}"
ADMIN_FIRSTNAME="${ZAMMAD_ADMIN_FIRSTNAME:-PCW}"
ADMIN_LASTNAME="${ZAMMAD_ADMIN_LASTNAME:-Admin}"
TOKEN_NAME="pcwaechter-api"

echo "=================================================="
echo " PCWächter – Zammad Setup"
echo " URL: ${ZAMMAD_URL}"
echo " Admin: ${ADMIN_EMAIL}"
echo "=================================================="

# ---------------------
# Warten bis Zammad bereit ist
# ---------------------
echo ""
echo "[1/4] Warte auf Zammad..."
for i in $(seq 1 60); do
  STATUS=$(curl -sf -o /dev/null -w "%{http_code}" "${ZAMMAD_URL}/api/v1/signshow" 2>/dev/null || echo "000")
  if [ "$STATUS" = "200" ] || [ "$STATUS" = "401" ]; then
    echo "      Zammad erreichbar (HTTP ${STATUS})"
    break
  fi
  echo "      Versuch ${i}/60 – warte 5s (HTTP ${STATUS})..."
  sleep 5
done

# ---------------------
# Admin-User anlegen (nur beim ersten Start)
# ---------------------
echo ""
echo "[2/4] Erstelle Admin-User (falls noch nicht vorhanden)..."

SETUP_RESPONSE=$(curl -sf -X POST "${ZAMMAD_URL}/api/v1/account_setup" \
  -H "Content-Type: application/json" \
  -d "{
    \"email\": \"${ADMIN_EMAIL}\",
    \"password\": \"${ADMIN_PASSWORD}\",
    \"firstname\": \"${ADMIN_FIRSTNAME}\",
    \"lastname\": \"${ADMIN_LASTNAME}\",
    \"organization\": \"PCWächter\"
  }" 2>&1 || true)

if echo "$SETUP_RESPONSE" | grep -q '"error"'; then
  echo "      Hinweis: Setup möglicherweise bereits durchgeführt."
  echo "      Antwort: $(echo "$SETUP_RESPONSE" | head -c 200)"
else
  echo "      Admin-User erstellt."
fi

# ---------------------
# API-Token erstellen
# ---------------------
echo ""
echo "[3/4] Erstelle API-Token '${TOKEN_NAME}'..."

TOKEN_RESPONSE=$(curl -sf -X POST "${ZAMMAD_URL}/api/v1/user_access_token" \
  -u "${ADMIN_EMAIL}:${ADMIN_PASSWORD}" \
  -H "Content-Type: application/json" \
  -d "{
    \"name\": \"${TOKEN_NAME}\",
    \"permission\": [\"ticket.agent\", \"ticket.customer\", \"user_preferences\"]
  }" 2>&1)

if echo "$TOKEN_RESPONSE" | grep -q '"token"'; then
  API_TOKEN=$(echo "$TOKEN_RESPONSE" | sed 's/.*"token":"\([^"]*\)".*/\1/')
  echo ""
  echo "=================================================="
  echo " API-Token erstellt!"
  echo " Token: ${API_TOKEN}"
  echo ""
  echo " In .env eintragen:"
  echo "   ZAMMAD_BASE_URL=${ZAMMAD_URL}"
  echo "   ZAMMAD_API_TOKEN=${API_TOKEN}"
  echo "=================================================="
else
  echo "      Fehler beim Token-Erstellen:"
  echo "      ${TOKEN_RESPONSE}"
  echo ""
  echo "      Bitte manuell unter ${ZAMMAD_URL} → Admin → API-Token erstellen."
fi

# ---------------------
# Basis-Konfiguration
# ---------------------
echo ""
echo "[4/4] Basis-Konfiguration..."

# FQDN setzen
curl -sf -X PUT "${ZAMMAD_URL}/api/v1/settings" \
  -u "${ADMIN_EMAIL}:${ADMIN_PASSWORD}" \
  -H "Content-Type: application/json" \
  -d "{\"name\": \"fqdn\", \"state\": \"${ZAMMAD_HOSTNAME:-support.pcwaechter.local}\"}" \
  >/dev/null 2>&1 || true

# E-Mail-Absender setzen
curl -sf -X PUT "${ZAMMAD_URL}/api/v1/settings" \
  -u "${ADMIN_EMAIL}:${ADMIN_PASSWORD}" \
  -H "Content-Type: application/json" \
  -d "{\"name\": \"ticket_hook\", \"state\": \"Ticket#\"}" \
  >/dev/null 2>&1 || true

echo "      Basis-Konfiguration gesetzt."
echo ""
echo "Fertig! Zammad ist unter ${ZAMMAD_URL} erreichbar."
