#!/usr/bin/env bash
# =============================================================
# PCWAECHTER - Zammad Post-Install Setup
# Erstellt Admin-User + API-Token und gibt den Token aus.
#
# Verwendung:
#   chmod +x server/zammad/setup.sh
#   ./server/zammad/setup.sh
#
# Voraussetzung: Zammad laeuft bereits und ist erreichbar.
# =============================================================

set -euo pipefail

# ---------------------
# Konfiguration
# ---------------------
ZAMMAD_URL="${ZAMMAD_BASE_URL:-http://localhost:3001}"
ZAMMAD_PUBLIC_URL="${ZAMMAD_PUBLIC_URL:-http://localhost:3001}"
ADMIN_EMAIL="${ZAMMAD_ADMIN_EMAIL:-admin@pcwaechter.de}"
ADMIN_PASSWORD="${ZAMMAD_ADMIN_PASSWORD:-CHANGE_ME}"
ADMIN_FIRSTNAME="${ZAMMAD_ADMIN_FIRSTNAME:-PCW}"
ADMIN_LASTNAME="${ZAMMAD_ADMIN_LASTNAME:-Admin}"
TOKEN_NAME="pcwaechter-api"
DEFAULT_GROUP_ID="${ZAMMAD_DEFAULT_GROUP_ID:-1}"
EXISTING_API_TOKEN="${ZAMMAD_API_TOKEN:-}"
KEYCLOAK_ISSUER="${ZAMMAD_KEYCLOAK_ISSUER:-https://login.xn--pcwchter-2za.de/realms/pcwaechter-prod}"
KEYCLOAK_CLIENT_ID="${ZAMMAD_KEYCLOAK_CLIENT_ID:-zammad}"
KEYCLOAK_DISPLAY_NAME="${ZAMMAD_KEYCLOAK_DISPLAY_NAME:-PCW Login}"
KEYCLOAK_SCOPE="${ZAMMAD_KEYCLOAK_SCOPE:-openid email profile}"
KEYCLOAK_UID_FIELD="${ZAMMAD_KEYCLOAK_UID_FIELD:-sub}"
KEYCLOAK_PKCE="${ZAMMAD_KEYCLOAK_PKCE:-true}"
KEYCLOAK_AUTO_LINK="${ZAMMAD_KEYCLOAK_AUTO_LINK:-true}"
KEYCLOAK_NO_CREATE_USER="${ZAMMAD_KEYCLOAK_NO_CREATE_USER:-false}"
WEBHOOK_SECRET="${ZAMMAD_WEBHOOK_SECRET:-}"
WEBHOOK_ENDPOINT="${ZAMMAD_WEBHOOK_ENDPOINT:-http://api:8000/api/v1/support/webhook}"
WEBHOOK_NAME="${ZAMMAD_WEBHOOK_NAME:-PCW Home Sync}"
WEBHOOK_TRIGGER_CREATE_NAME="${ZAMMAD_WEBHOOK_TRIGGER_CREATE_NAME:-PCW Home Sync (ticket create)}"
WEBHOOK_TRIGGER_UPDATE_NAME="${ZAMMAD_WEBHOOK_TRIGGER_UPDATE_NAME:-PCW Home Sync (ticket update)}"
WEBHOOK_TRIGGER_STATE_NAME="${ZAMMAD_WEBHOOK_TRIGGER_STATE_NAME:-PCW Home Sync (ticket state change)}"

API_TOKEN=""
PUBLIC_SCHEME="${ZAMMAD_PUBLIC_URL%%://*}"
PUBLIC_HOST="${ZAMMAD_PUBLIC_URL#*://}"
PUBLIC_HOST="${PUBLIC_HOST%%/*}"
PUBLIC_URL_NO_SLASH="${ZAMMAD_PUBLIC_URL%/}"
OIDC_CALLBACK_URL="${PUBLIC_URL_NO_SLASH}/auth/openid_connect/callback"
OIDC_BACKCHANNEL_LOGOUT_URL="${PUBLIC_URL_NO_SLASH}/auth/openid_connect/backchannel_logout"
PYTHON_BIN="${PYTHON_BIN:-python3}"

if ! command -v "${PYTHON_BIN}" >/dev/null 2>&1; then
  PYTHON_BIN="python"
fi

auth_args() {
  if [ -n "${API_TOKEN}" ]; then
    printf '%s\n' "-H" "Authorization: Token token=${API_TOKEN}"
    return
  fi

  printf '%s\n' "-u" "${ADMIN_EMAIL}:${ADMIN_PASSWORD}"
}

api_call() {
  local method="$1"
  local endpoint="$2"
  local payload="${3:-}"
  local args=("${ZAMMAD_URL}${endpoint}")
  local auth
  mapfile -t auth < <(auth_args)

  if [ -n "${payload}" ]; then
    curl -sf -X "${method}" "${args[0]}" \
      "${auth[@]}" \
      -H "Content-Type: application/json" \
      -d "${payload}"
  else
    curl -sf -X "${method}" "${args[0]}" \
      "${auth[@]}" \
      -H "Content-Type: application/json"
  fi
}

lookup_setting_id() {
  local name="$1"
  local payload

  payload="$(api_call GET "/api/v1/settings")"

  printf '%s' "${payload}" | "${PYTHON_BIN}" -c '
import json
import sys

target = sys.argv[1]
for item in json.load(sys.stdin):
    if item.get("name") == target:
        print(item["id"])
        break
' "$name"
}

set_setting() {
  local name="$1"
  local state_json="$2"
  local setting_id

  setting_id="$(lookup_setting_id "${name}")"
  if [ -z "${setting_id}" ]; then
    echo "      Warnung: Setting '${name}' wurde nicht gefunden." >&2
    return 1
  fi

  api_call PUT "/api/v1/settings/${setting_id}" "{\"state_current\":{\"value\":${state_json}}}" >/dev/null
}

json_escape_stdin() {
  "${PYTHON_BIN}" -c 'import json, sys; print(json.dumps(sys.stdin.read()))'
}

extract_json_id() {
  "${PYTHON_BIN}" -c '
import json
import sys

payload = json.load(sys.stdin)
if isinstance(payload, dict) and payload.get("id") is not None:
    print(payload["id"])
'
}

find_resource_id_by_name() {
  local resource="$1"
  local target_name="$2"

  api_call GET "/api/v1/${resource}?per_page=200" | "${PYTHON_BIN}" -c '
import json
import sys

target = sys.argv[1].strip().lower()
for item in json.load(sys.stdin):
    if str(item.get("name") or "").strip().lower() == target:
        print(item.get("id"))
        break
' "${target_name}"
}

upsert_resource_by_name() {
  local resource="$1"
  local name="$2"
  local payload="$3"
  local resource_id

  resource_id="$(find_resource_id_by_name "${resource}" "${name}")"
  if [ -n "${resource_id}" ]; then
    api_call PUT "/api/v1/${resource}/${resource_id}" "${payload}" | extract_json_id
    return
  fi

  api_call POST "/api/v1/${resource}" "${payload}" | extract_json_id
}

configure_home_sync_webhook() {
  if [ -z "${WEBHOOK_SECRET}" ]; then
    echo "      Hinweis: ZAMMAD_WEBHOOK_SECRET fehlt - Home-Sync wird uebersprungen."
    return 0
  fi

  local custom_payload
  local custom_payload_json
  local webhook_payload
  local webhook_id
  local create_trigger_payload
  local update_trigger_payload
  local state_trigger_payload

  custom_payload="$(cat <<'EOF'
{
  "ticket": {
    "id": "#{ticket.id}",
    "number": "#{ticket.number}",
    "title": "#{ticket.title}",
    "customer_id": "#{ticket.customer.id}",
    "customer_email": "#{ticket.customer.email}",
    "state_id": "#{ticket.state_id}",
    "state": "#{ticket.state.name}",
    "article_count": "#{ticket.article_count}",
    "updated_at": "#{ticket.updated_at}",
    "last_contact_agent_at": "#{ticket.last_contact_agent_at}",
    "last_contact_customer_at": "#{ticket.last_contact_customer_at}"
  },
  "article": {
    "id": "#{article.id}",
    "sender": "#{article.sender.name}",
    "sender_id": "#{article.sender_id}",
    "internal": "#{article.internal}",
    "type": "#{article.type.name}",
    "created_at": "#{article.created_at}",
    "subject": "#{article.subject}",
    "body": "#{article.body}"
  },
  "notification": {
    "message": "#{notification.message}",
    "changes": "#{notification.changes}"
  }
}
EOF
)"
  custom_payload_json="$(printf '%s' "${custom_payload}" | json_escape_stdin)"

  webhook_payload="$(cat <<EOF
{
  "name": "${WEBHOOK_NAME}",
  "endpoint": "${WEBHOOK_ENDPOINT}",
  "http_method": "post",
  "ssl_verify": false,
  "bearer_token": "${WEBHOOK_SECRET}",
  "customized_payload": true,
  "custom_payload": ${custom_payload_json},
  "active": true,
  "note": "PCW Home ticket sync for portal notifications and badge updates."
}
EOF
)"
  webhook_id="$(upsert_resource_by_name "webhooks" "${WEBHOOK_NAME}" "${webhook_payload}")"
  if [ -z "${webhook_id}" ]; then
    echo "      Warnung: Webhook '${WEBHOOK_NAME}' konnte nicht erstellt werden." >&2
    return 1
  fi

  create_trigger_payload="$(cat <<EOF
{
  "name": "${WEBHOOK_TRIGGER_CREATE_NAME}",
  "active": true,
  "activator": "action",
  "execution_condition_mode": "selective",
  "condition": {
    "ticket.action": {
      "operator": "is",
      "value": "create"
    }
  },
  "perform": {
    "notification.webhook": {
      "webhook_id": ${webhook_id}
    }
  }
}
EOF
)"
  upsert_resource_by_name "triggers" "${WEBHOOK_TRIGGER_CREATE_NAME}" "${create_trigger_payload}" >/dev/null

  update_trigger_payload="$(cat <<EOF
{
  "name": "${WEBHOOK_TRIGGER_UPDATE_NAME}",
  "active": true,
  "activator": "action",
  "execution_condition_mode": "selective",
  "condition": {
    "ticket.action": {
      "operator": "is",
      "value": "update"
    }
  },
  "perform": {
    "notification.webhook": {
      "webhook_id": ${webhook_id}
    }
  }
}
EOF
)"
  upsert_resource_by_name "triggers" "${WEBHOOK_TRIGGER_UPDATE_NAME}" "${update_trigger_payload}" >/dev/null

  state_trigger_payload="$(cat <<EOF
{
  "name": "${WEBHOOK_TRIGGER_STATE_NAME}",
  "active": true,
  "activator": "action",
  "execution_condition_mode": "selective",
  "condition": {
    "ticket.state_id": {
      "operator": "has changed"
    }
  },
  "perform": {
    "notification.webhook": {
      "webhook_id": ${webhook_id}
    }
  }
}
EOF
)"
  upsert_resource_by_name "triggers" "${WEBHOOK_TRIGGER_STATE_NAME}" "${state_trigger_payload}" >/dev/null

  echo "      Home-Sync-Webhook aktiviert: ${WEBHOOK_ENDPOINT}"
}

echo "=================================================="
echo " PCWAECHTER - Zammad Setup"
echo " URL: ${ZAMMAD_URL}"
echo " Public URL: ${ZAMMAD_PUBLIC_URL}"
echo " Admin: ${ADMIN_EMAIL}"
echo "=================================================="

# ---------------------
# Warten bis Zammad bereit ist
# ---------------------
echo ""
echo "[1/7] Warte auf Zammad..."
for i in $(seq 1 60); do
  STATUS=$(curl -sf -o /dev/null -w "%{http_code}" "${ZAMMAD_URL}/api/v1/signshow" 2>/dev/null || echo "000")
  if [ "$STATUS" = "200" ] || [ "$STATUS" = "401" ]; then
    echo "      Zammad erreichbar (HTTP ${STATUS})"
    break
  fi
  echo "      Versuch ${i}/60 - warte 5s (HTTP ${STATUS})..."
  sleep 5
done

# ---------------------
# Admin-User anlegen (nur beim ersten Start)
# ---------------------
echo ""
echo "[2/7] Erstelle Admin-User (falls noch nicht vorhanden)..."

if [ -n "${EXISTING_API_TOKEN}" ]; then
  API_TOKEN="${EXISTING_API_TOKEN}"
  echo "      Vorhandenes API-Token gefunden - Bootstrap wird uebersprungen."
else
  SETUP_RESPONSE=$(curl -sf -X POST "${ZAMMAD_URL}/api/v1/account_setup" \
    -H "Content-Type: application/json" \
    -d "{
      \"email\": \"${ADMIN_EMAIL}\",
      \"password\": \"${ADMIN_PASSWORD}\",
      \"firstname\": \"${ADMIN_FIRSTNAME}\",
      \"lastname\": \"${ADMIN_LASTNAME}\",
      \"organization\": \"PCWAECHTER\"
    }" 2>&1 || true)

  if echo "$SETUP_RESPONSE" | grep -q '"error"'; then
    echo "      Hinweis: Setup moeglicherweise bereits durchgefuehrt."
    echo "      Antwort: $(echo "$SETUP_RESPONSE" | head -c 200)"
  else
    echo "      Admin-User erstellt."
  fi
fi

# ---------------------
# Admin-User der Default-Gruppe zuordnen
# ---------------------
echo ""
echo "[3/7] Weise Admin-User der Ticket-Gruppe ${DEFAULT_GROUP_ID} zu..."

ADMIN_USER_ID=$(api_call GET "/api/v1/users/search?query=${ADMIN_EMAIL}" \
  | sed -n 's/.*"id":\([0-9][0-9]*\).*/\1/p' | head -n1)

if [ -n "${ADMIN_USER_ID}" ]; then
  api_call PUT "/api/v1/users/${ADMIN_USER_ID}" "{\"group_ids\":{\"${DEFAULT_GROUP_ID}\":[\"full\"]}}" \
    >/dev/null 2>&1 || true
  echo "      Gruppenzuordnung gesetzt (User ${ADMIN_USER_ID} -> Group ${DEFAULT_GROUP_ID})."
else
  echo "      Hinweis: Admin-User konnte fuer die Gruppenzuordnung nicht aufgeloest werden."
fi

# ---------------------
# API-Token erstellen
# ---------------------
echo ""
echo "[4/7] Erstelle API-Token '${TOKEN_NAME}'..."

if [ -n "${EXISTING_API_TOKEN}" ]; then
  API_TOKEN="${EXISTING_API_TOKEN}"
  echo "      Vorhandenes API-Token wird weiterverwendet."
else
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
    echo "      Bitte manuell unter ${ZAMMAD_URL} -> Admin -> API-Token erstellen."
  fi
fi

# ---------------------
# Basis-Konfiguration
# ---------------------
echo ""
echo "[5/7] Basis-Konfiguration..."

set_setting "http_type" "\"${PUBLIC_SCHEME}\"" || true
set_setting "fqdn" "\"${PUBLIC_HOST}\"" || true
set_setting "ticket_hook" "\"Ticket#\"" || true

echo "      Basis-Konfiguration gesetzt."

# ---------------------
# Keycloak / OpenID Connect
# ---------------------
echo ""
echo "[6/7] Konfiguriere Keycloak OpenID Connect..."

set_setting "auth_openid_connect_credentials" "$(cat <<EOF
{
  "display_name": "${KEYCLOAK_DISPLAY_NAME}",
  "identifier": "${KEYCLOAK_CLIENT_ID}",
  "issuer": "${KEYCLOAK_ISSUER}",
  "uid_field": "${KEYCLOAK_UID_FIELD}",
  "scope": "${KEYCLOAK_SCOPE}",
  "pkce": ${KEYCLOAK_PKCE}
}
EOF
)" || true
set_setting "auth_third_party_auto_link_at_inital_login" "${KEYCLOAK_AUTO_LINK}" || true
set_setting "auth_third_party_no_create_user" "${KEYCLOAK_NO_CREATE_USER}" || true
set_setting "auth_openid_connect" "true" || true

echo "      OpenID Connect aktiviert."
echo "      Callback URL: ${OIDC_CALLBACK_URL}"
echo "      Backchannel Logout: ${OIDC_BACKCHANNEL_LOGOUT_URL}"
echo ""
echo "[7/7] Konfiguriere PCW Home Webhook-Sync..."
configure_home_sync_webhook
echo ""
echo "Fertig! Zammad ist unter ${ZAMMAD_URL} erreichbar."
