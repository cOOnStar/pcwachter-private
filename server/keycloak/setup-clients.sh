#!/usr/bin/env bash
# =============================================================================
# PCWächter – Keycloak Client Setup
# =============================================================================
# Erstellt/aktualisiert alle benötigten Keycloak-Clients im Realm pcwaechter-prod.
#
# Voraussetzungen:
#   - curl, jq installiert
#   - Keycloak ist erreichbar unter KEYCLOAK_URL
#   - Admin-Credentials sind bekannt
#
# Nutzung:
#   export KEYCLOAK_URL=https://login.xn--pcwchter-2za.de
#   export KEYCLOAK_REALM=pcwaechter-prod
#   export KEYCLOAK_ADMIN_USER=admin
#   export KEYCLOAK_ADMIN_PASSWORD=...
#   bash setup-clients.sh
#
# Optional (generiert Secret für den Legacy-Client "home" automatisch):
#   export HOME_CLIENT_SECRET=$(openssl rand -hex 32)
# =============================================================================

set -euo pipefail

KEYCLOAK_URL="${KEYCLOAK_URL:-https://login.xn--pcwchter-2za.de}"
REALM="${KEYCLOAK_REALM:-pcwaechter-prod}"
ADMIN_USER="${KEYCLOAK_ADMIN_USER:-admin}"
ADMIN_PASS="${KEYCLOAK_ADMIN_PASSWORD:?KEYCLOAK_ADMIN_PASSWORD must be set}"
HOME_SECRET="${HOME_CLIENT_SECRET:-$(openssl rand -hex 32)}"
ZAMMAD_PUBLIC_URL="${ZAMMAD_PUBLIC_URL:-https://support.xn--pcwchter-2za.de}"
ZAMMAD_PUBLIC_URL="${ZAMMAD_PUBLIC_URL%/}"

ADMIN_API="${KEYCLOAK_URL}/admin/realms/${REALM}"

echo ""
echo "=== PCWächter Keycloak Client Setup ==="
echo "Realm:    ${REALM}"
echo "API Base: ${ADMIN_API}"
echo ""

# ── Helper: Admin-Token holen ────────────────────────────────────────────────
get_token() {
  curl -sf -X POST \
    "${KEYCLOAK_URL}/realms/master/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -d "grant_type=password" \
    -d "client_id=admin-cli" \
    -d "username=${ADMIN_USER}" \
    -d "password=${ADMIN_PASS}" \
    | jq -r '.access_token'
}

# ── Helper: Client erstellen oder updaten ────────────────────────────────────
upsert_client() {
  local token="$1"
  local client_json="$2"
  local client_id
  client_id=$(echo "$client_json" | jq -r '.clientId')

  echo "→ Verarbeite Client: ${client_id}"

  # Vorhandene Client-UUID suchen
  local existing_uuid
  existing_uuid=$(curl -sf \
    "${ADMIN_API}/clients?clientId=${client_id}" \
    -H "Authorization: Bearer ${token}" \
    | jq -r '.[0].id // empty')

  if [ -n "$existing_uuid" ]; then
    echo "  Update bestehender Client (${existing_uuid})"
    curl -sf -X PUT \
      "${ADMIN_API}/clients/${existing_uuid}" \
      -H "Authorization: Bearer ${token}" \
      -H "Content-Type: application/json" \
      -d "$client_json" && echo "  ✓ Aktualisiert"
  else
    echo "  Erstelle neuen Client"
    curl -sf -X POST \
      "${ADMIN_API}/clients" \
      -H "Authorization: Bearer ${token}" \
      -H "Content-Type: application/json" \
      -d "$client_json" && echo "  ✓ Erstellt"
  fi
}

get_client_uuid() {
  local token="$1"
  local client_id="$2"
  curl -sf \
    "${ADMIN_API}/clients?clientId=${client_id}" \
    -H "Authorization: Bearer ${token}" \
    | jq -r '.[0].id // empty'
}

ensure_client_mapper() {
  local token="$1"
  local client_uuid="$2"
  local mapper_name="$3"
  local mapper_json="$4"

  if [ -z "$client_uuid" ]; then
    return
  fi

  local existing_uuid
  existing_uuid=$(curl -sf \
    "${ADMIN_API}/clients/${client_uuid}/protocol-mappers/models" \
    -H "Authorization: Bearer ${token}" \
    | jq -r --arg name "${mapper_name}" '.[] | select(.name == $name) | .id' \
    | head -1)

  if [ -n "${existing_uuid}" ]; then
    echo "  ~ Mapper ${mapper_name} bereits vorhanden"
    return
  fi

  curl -sf -X POST \
    "${ADMIN_API}/clients/${client_uuid}/protocol-mappers/models" \
    -H "Authorization: Bearer ${token}" \
    -H "Content-Type: application/json" \
    -d "${mapper_json}" >/dev/null
  echo "  ✓ Mapper ${mapper_name} hinzugefügt"
}

# ── Token holen ───────────────────────────────────────────────────────────────
echo "Hole Admin-Token..."
TOKEN=$(get_token)
if [ -z "$TOKEN" ] || [ "$TOKEN" = "null" ]; then
  echo "FEHLER: Admin-Token konnte nicht abgerufen werden. Credentials prüfen."
  exit 1
fi
echo "✓ Token erhalten"
echo ""

# =============================================================================
# Client 1: console (public, PKCE, für React Admin Console)
# =============================================================================
upsert_client "$TOKEN" "$(cat <<EOF
{
  "clientId": "console",
  "name": "PCWächter Admin Console",
  "description": "React-basierte Admin-Oberfläche",
  "enabled": true,
  "publicClient": true,
  "standardFlowEnabled": true,
  "implicitFlowEnabled": false,
  "directAccessGrantsEnabled": false,
  "serviceAccountsEnabled": false,
  "redirectUris": [
    "https://console.xn--pcwchter-2za.de/*",
    "http://localhost:13000/*",
    "http://localhost:13001/*",
    "http://localhost:5173/*"
  ],
  "webOrigins": [
    "https://console.xn--pcwchter-2za.de",
    "http://localhost:13000",
    "http://localhost:13001",
    "http://localhost:5173"
  ],
  "attributes": {
    "pkce.code.challenge.method": "S256",
    "post.logout.redirect.uris": "https://console.xn--pcwchter-2za.de/*##http://localhost:13000/*##http://localhost:5173/*"
  },
  "protocol": "openid-connect"
}
EOF
)"
CONSOLE_UUID=$(get_client_uuid "$TOKEN" "console")
ensure_client_mapper "$TOKEN" "$CONSOLE_UUID" "pcwaechter-api-audience" "$(cat <<EOF
{
  "name": "pcwaechter-api-audience",
  "protocol": "openid-connect",
  "protocolMapper": "oidc-audience-mapper",
  "consentRequired": false,
  "config": {
    "included.custom.audience": "pcwaechter-api",
    "access.token.claim": "true",
    "id.token.claim": "false"
  }
}
EOF
)"

echo ""

# =============================================================================
# Client 2: home (confidential, Legacy-Client für altes Next.js Home Portal)
# =============================================================================
upsert_client "$TOKEN" "$(cat <<EOF
{
  "clientId": "home",
  "name": "PCWächter Home Portal",
  "description": "Legacy Home Portal (confidential, NextAuth-kompatibel)",
  "enabled": true,
  "publicClient": false,
  "standardFlowEnabled": true,
  "implicitFlowEnabled": false,
  "directAccessGrantsEnabled": false,
  "serviceAccountsEnabled": false,
  "secret": "${HOME_SECRET}",
  "redirectUris": [
    "https://home.xn--pcwchter-2za.de/api/auth/callback/keycloak",
    "http://localhost:3000/api/auth/callback/keycloak",
    "http://localhost:13001/api/auth/callback/keycloak",
    "http://localhost:13002/api/auth/callback/keycloak"
  ],
  "webOrigins": [
    "https://home.xn--pcwchter-2za.de",
    "http://localhost:3000",
    "http://localhost:13001",
    "http://localhost:13002"
  ],
  "attributes": {
    "post.logout.redirect.uris": "https://home.xn--pcwchter-2za.de/*##http://localhost:3000/*"
  },
  "protocol": "openid-connect"
}
EOF
)"

echo ""

# =============================================================================
# Client 3: home-web (public, PKCE, für React/Vite Home Portal)
# =============================================================================
upsert_client "$TOKEN" "$(cat <<EOF
{
  "clientId": "home-web",
  "name": "PCWächter Home Portal Web",
  "description": "React/Vite Home Portal (public PKCE SPA)",
  "enabled": true,
  "publicClient": true,
  "standardFlowEnabled": true,
  "implicitFlowEnabled": false,
  "directAccessGrantsEnabled": false,
  "serviceAccountsEnabled": false,
  "redirectUris": [
    "https://home.xn--pcwchter-2za.de/*",
    "http://localhost:13001/*",
    "http://localhost:3000/*",
    "http://localhost:5173/*"
  ],
  "webOrigins": [
    "https://home.xn--pcwchter-2za.de",
    "http://localhost:13001",
    "http://localhost:3000",
    "http://localhost:5173"
  ],
  "attributes": {
    "pkce.code.challenge.method": "S256",
    "post.logout.redirect.uris": "https://home.xn--pcwchter-2za.de/*##http://localhost:13001/*##http://localhost:3000/*##http://localhost:5173/*"
  },
  "protocol": "openid-connect"
}
EOF
)"
HOME_WEB_UUID=$(get_client_uuid "$TOKEN" "home-web")
ensure_client_mapper "$TOKEN" "$HOME_WEB_UUID" "pcwaechter-api-audience" "$(cat <<EOF
{
  "name": "pcwaechter-api-audience",
  "protocol": "openid-connect",
  "protocolMapper": "oidc-audience-mapper",
  "consentRequired": false,
  "config": {
    "included.custom.audience": "pcwaechter-api",
    "access.token.claim": "true",
    "id.token.claim": "false"
  }
}
EOF
)"

echo ""

# =============================================================================
# Client 4: zammad (public, PKCE, für native Zammad-Anmeldung via Keycloak)
# =============================================================================
upsert_client "$TOKEN" "$(cat <<EOF
{
  "clientId": "zammad",
  "name": "PCWächter Support",
  "description": "Zammad Login via Keycloak OpenID Connect",
  "enabled": true,
  "publicClient": true,
  "standardFlowEnabled": true,
  "implicitFlowEnabled": false,
  "directAccessGrantsEnabled": false,
  "serviceAccountsEnabled": false,
  "redirectUris": [
    "http://localhost:3001/auth/openid_connect/callback",
    "${ZAMMAD_PUBLIC_URL}/auth/openid_connect/callback"
  ],
  "webOrigins": [
    "http://localhost:3001",
    "${ZAMMAD_PUBLIC_URL}"
  ],
  "attributes": {
    "pkce.code.challenge.method": "S256",
    "post.logout.redirect.uris": "http://localhost:3001/*##${ZAMMAD_PUBLIC_URL}/*",
    "backchannel.logout.session.required": "true",
    "backchannel.logout.url": "${ZAMMAD_PUBLIC_URL}/auth/openid_connect/backchannel_logout"
  },
  "protocol": "openid-connect"
}
EOF
)"

echo ""

# =============================================================================
# Client 5: pcwaechter-desktop (public, PKCE, für Windows Desktop Client)
# =============================================================================
upsert_client "$TOKEN" "$(cat <<EOF
{
  "clientId": "pcwaechter-desktop",
  "name": "PCWächter Desktop Client",
  "description": "Windows WPF Desktop App (PKCE Authorization Code Flow)",
  "enabled": true,
  "publicClient": true,
  "standardFlowEnabled": true,
  "implicitFlowEnabled": false,
  "directAccessGrantsEnabled": false,
  "serviceAccountsEnabled": false,
  "redirectUris": [
    "http://127.0.0.1:8765/callback",
    "http://localhost:8765/callback"
  ],
  "webOrigins": [],
  "attributes": {
    "pkce.code.challenge.method": "S256",
    "post.logout.redirect.uris": "http://127.0.0.1:8765/logout"
  },
  "protocol": "openid-connect"
}
EOF
)"

echo ""
echo "=== Setup abgeschlossen ==="
echo ""
echo "WICHTIG: Secret für den 'home' Client:"
echo "  HOME_KEYCLOAK_CLIENT_SECRET=${HOME_SECRET}"
echo ""
echo "Dieses Secret in .env eintragen:"
echo "  AUTH_KEYCLOAK_SECRET=${HOME_SECRET}"
