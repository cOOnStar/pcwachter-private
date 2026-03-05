#!/usr/bin/env bash
# =============================================================================
# PCWächter – Keycloak Realm Provisioning (pcw_* role model)
# =============================================================================
# Erstellt/aktualisiert Realm pcwaechter-prod mit allen PCW-Clients, Rollen,
# Gruppen und Protocol Mappern (inkl. Audience-Mapper für pcwaechter-api).
#
# Nutzung (innerhalb des Containers oder via docker exec):
#   KEYCLOAK_URL=http://localhost:8080 \
#   KC_ADMIN_USER=admin \
#   KC_ADMIN_PASSWORD=<secret> \
#   HOME_CLIENT_SECRET=$(openssl rand -hex 32) \
#   bash provision-realm.sh
#
# Oder via docker exec (aus server/keycloak/):
#   docker cp provision-realm.sh pcw-keycloak:/tmp/
#   docker exec pcw-keycloak bash /tmp/provision-realm.sh
# =============================================================================
set -euo pipefail

KEYCLOAK_URL="${KEYCLOAK_URL:-http://localhost:8080}"
REALM="${REALM:-pcwaechter-prod}"
ADMIN_USER="${KC_ADMIN_USER:-admin}"
ADMIN_PASS="${KC_ADMIN_PASSWORD:?KC_ADMIN_PASSWORD must be set}"
HOME_SECRET="${HOME_CLIENT_SECRET:-$(cat /proc/sys/kernel/random/uuid | tr -d '-')}"

KCADM="//opt//keycloak//bin//kcadm.sh"
# If running INSIDE the container, use the direct path:
if [ -f /opt/keycloak/bin/kcadm.sh ]; then
  KCADM="/opt/keycloak/bin/kcadm.sh"
fi

log() { echo "[$(date -u +%H:%M:%S)] $*"; }

# ── 1. Authenticate ───────────────────────────────────────────────────────────
log "Authenticating with Keycloak at ${KEYCLOAK_URL} ..."
"$KCADM" config credentials \
  --server "${KEYCLOAK_URL}" \
  --realm master \
  --user "${ADMIN_USER}" \
  --password "${ADMIN_PASS}"
log "✓ Authenticated"

# ── 2. Create / update Realm ─────────────────────────────────────────────────
log "Creating/updating realm ${REALM} ..."
if "$KCADM" get "realms/${REALM}" --fields realm > /dev/null 2>&1; then
  "$KCADM" update "realms/${REALM}" \
    -s enabled=true \
    -s displayName="PCWächter" \
    -s sslRequired=EXTERNAL \
    -s registrationAllowed=false \
    -s loginWithEmailAllowed=true \
    -s duplicateEmailsAllowed=false \
    -s resetPasswordAllowed=true \
    -s rememberMe=true \
    -s bruteForceProtected=true \
    -s failureFactor=5 \
    -s waitIncrementSeconds=60 \
    -s maxFailureWaitSeconds=900 \
    -s maxDeltaTimeSeconds=43200 \
    -s passwordPolicy="length(10) and digits(1) and upperCase(1) and specialChars(1) and notUsername" \
    -s loginTheme=pcwaechter-v1
  log "✓ Realm updated"
else
  "$KCADM" create realms \
    -s "realm=${REALM}" \
    -s enabled=true \
    -s displayName="PCWächter" \
    -s sslRequired=EXTERNAL \
    -s registrationAllowed=false \
    -s loginWithEmailAllowed=true \
    -s duplicateEmailsAllowed=false \
    -s resetPasswordAllowed=true \
    -s rememberMe=true \
    -s bruteForceProtected=true \
    -s failureFactor=5 \
    -s waitIncrementSeconds=60 \
    -s maxFailureWaitSeconds=900 \
    -s maxDeltaTimeSeconds=43200 \
    -s passwordPolicy="length(10) and digits(1) and upperCase(1) and specialChars(1) and notUsername" \
    -s loginTheme=pcwaechter-v1
  log "✓ Realm created"
fi

# ── 3. Roles ─────────────────────────────────────────────────────────────────
log "Creating realm roles ..."
for ROLE in pcw_admin pcw_console pcw_user pcw_support pcw_agent; do
  if ! "$KCADM" get "roles/${ROLE}" -r "${REALM}" --fields name > /dev/null 2>&1; then
    "$KCADM" create roles -r "${REALM}" -s "name=${ROLE}"
    log "  ✓ Role ${ROLE} created"
  else
    log "  ~ Role ${ROLE} already exists"
  fi
done

# ── 4. Groups + Role Mappings ────────────────────────────────────────────────
log "Creating groups ..."
create_group_with_role() {
  local GROUP_NAME="$1"
  local ROLE_NAME="$2"
  local GID
  GID=$("$KCADM" get groups -r "${REALM}" -q "search=${GROUP_NAME}" --fields id,name \
    | grep -B1 "\"name\" : \"${GROUP_NAME}\"" | grep id | sed 's/.*"\(.*\)".*/\1/' | head -1 || true)

  if [ -z "$GID" ]; then
    "$KCADM" create groups -r "${REALM}" -s "name=${GROUP_NAME}" >/dev/null 2>&1 || true
    GID=$("$KCADM" get groups -r "${REALM}" -q "search=${GROUP_NAME}" --fields id,name \
      | grep -B1 "\"name\" : \"${GROUP_NAME}\"" | grep id | sed 's/.*"\(.*\)".*/\1/' | head -1 || true)
    if [ -z "$GID" ]; then
      log "  ! Group ${GROUP_NAME} lookup failed"
      return
    fi
    log "  ✓ Group ${GROUP_NAME} created (${GID})"
  else
    log "  ~ Group ${GROUP_NAME} already exists (${GID})"
  fi
  "$KCADM" add-roles -r "${REALM}" --gid "${GID}" --rolename "${ROLE_NAME}" 2>/dev/null || true
}

create_group_with_role "pcw-admins"   "pcw_admin"
create_group_with_role "pcw-console"  "pcw_console"
create_group_with_role "pcw-users"    "pcw_user"
create_group_with_role "pcw-support"  "pcw_support"

# ── 5. Helper: upsert client ─────────────────────────────────────────────────
upsert_client() {
  local CLIENT_ID="$1"
  local JSON="$2"
  local EXISTING_UUID
  EXISTING_UUID=$("$KCADM" get clients -r "${REALM}" -q "clientId=${CLIENT_ID}" --fields id \
    | grep '"id"' | sed 's/.*"\(.*\)".*/\1/' | head -1 || true)

  if [ -z "$EXISTING_UUID" ]; then
    "$KCADM" create clients -r "${REALM}" -f - <<< "${JSON}"
    log "  ✓ Client ${CLIENT_ID} created"
  else
    "$KCADM" update "clients/${EXISTING_UUID}" -r "${REALM}" -f - <<< "${JSON}"
    log "  ~ Client ${CLIENT_ID} updated (${EXISTING_UUID})"
  fi
}

# ── Helper: get client internal UUID ─────────────────────────────────────────
get_client_uuid() {
  local CLIENT_ID="$1"
  "$KCADM" get clients -r "${REALM}" -q "clientId=${CLIENT_ID}" --fields id \
    | grep '"id"' | sed 's/.*"\(.*\)".*/\1/' | head -1
}

# ── Helper: add protocol mapper to client ────────────────────────────────────
add_audience_mapper() {
  local CLIENT_UUID="$1"
  local MAPPER_NAME="pcwaechter-api-audience"
  # Skip if already present
  if "$KCADM" get "clients/${CLIENT_UUID}/protocol-mappers/models" -r "${REALM}" \
     | grep -q "\"${MAPPER_NAME}\"" 2>/dev/null; then
    log "    ~ Audience mapper already present"
    return
  fi
  "$KCADM" create "clients/${CLIENT_UUID}/protocol-mappers/models" -r "${REALM}" -f - << 'EOF'
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
  log "    ✓ Audience mapper added"
}

add_roles_mapper() {
  local CLIENT_UUID="$1"
  local MAPPER_NAME="realm-roles"
  if "$KCADM" get "clients/${CLIENT_UUID}/protocol-mappers/models" -r "${REALM}" \
     | grep -q "\"${MAPPER_NAME}\"" 2>/dev/null; then
    log "    ~ Roles mapper already present"
    return
  fi
  "$KCADM" create "clients/${CLIENT_UUID}/protocol-mappers/models" -r "${REALM}" -f - << 'EOF'
{
  "name": "realm-roles",
  "protocol": "openid-connect",
  "protocolMapper": "oidc-usermodel-realm-role-mapper",
  "consentRequired": false,
  "config": {
    "claim.name": "roles",
    "jsonType.label": "String",
    "multivalued": "true",
    "userinfo.token.claim": "true",
    "access.token.claim": "true",
    "id.token.claim": "false"
  }
}
EOF
  log "    ✓ Roles mapper added"
}

# ── 6. Client: console (public, PKCE) ────────────────────────────────────────
log "Creating client: console ..."
upsert_client "console" '{
  "clientId": "console",
  "name": "PCWächter Admin Console",
  "enabled": true,
  "publicClient": true,
  "standardFlowEnabled": true,
  "implicitFlowEnabled": false,
  "directAccessGrantsEnabled": false,
  "serviceAccountsEnabled": false,
  "protocol": "openid-connect",
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
    "post.logout.redirect.uris": "https://console.xn--pcwchter-2za.de/*##http://localhost:13000/*##http://localhost:13001/*##http://localhost:5173/*"
  }
}'
CONSOLE_UUID=$(get_client_uuid "console")
add_audience_mapper "${CONSOLE_UUID}"
add_roles_mapper    "${CONSOLE_UUID}"

# ── 7. Client: home (confidential, NextAuth) ─────────────────────────────────
log "Creating client: home ..."
upsert_client "home" "{
  \"clientId\": \"home\",
  \"name\": \"PCWächter Home Portal\",
  \"enabled\": true,
  \"publicClient\": false,
  \"standardFlowEnabled\": true,
  \"implicitFlowEnabled\": false,
  \"directAccessGrantsEnabled\": false,
  \"serviceAccountsEnabled\": false,
  \"protocol\": \"openid-connect\",
  \"secret\": \"${HOME_SECRET}\",
  \"redirectUris\": [
    \"https://home.xn--pcwchter-2za.de/api/auth/callback/keycloak\",
    \"http://localhost:3000/api/auth/callback/keycloak\",
    \"http://localhost:13001/api/auth/callback/keycloak\"
  ],
  \"webOrigins\": [
    \"https://home.xn--pcwchter-2za.de\",
    \"http://localhost:3000\",
    \"http://localhost:13001\"
  ],
  \"attributes\": {
    \"post.logout.redirect.uris\": \"https://home.xn--pcwchter-2za.de/*##http://localhost:3000/*##http://localhost:13001/*\"
  }
}"
HOME_UUID=$(get_client_uuid "home")
add_audience_mapper "${HOME_UUID}"
add_roles_mapper    "${HOME_UUID}"

# ── 7b. Client: pcwaechter-console (canonical) ───────────────────────────────
log "Creating client: pcwaechter-console ..."
upsert_client "pcwaechter-console" '{
  "clientId": "pcwaechter-console",
  "name": "PCWächter Admin Console",
  "enabled": true,
  "publicClient": true,
  "standardFlowEnabled": true,
  "implicitFlowEnabled": false,
  "directAccessGrantsEnabled": false,
  "serviceAccountsEnabled": false,
  "protocol": "openid-connect",
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
    "post.logout.redirect.uris": "https://console.xn--pcwchter-2za.de/*##http://localhost:13000/*##http://localhost:13001/*##http://localhost:5173/*"
  }
}'
PCW_CONSOLE_UUID=$(get_client_uuid "pcwaechter-console")
add_audience_mapper "${PCW_CONSOLE_UUID}"
add_roles_mapper    "${PCW_CONSOLE_UUID}"

# ── 7c. Client: pcwaechter-home (canonical) ──────────────────────────────────
log "Creating client: pcwaechter-home ..."
upsert_client "pcwaechter-home" "{
  \"clientId\": \"pcwaechter-home\",
  \"name\": \"PCWächter Home Portal\",
  \"enabled\": true,
  \"publicClient\": false,
  \"standardFlowEnabled\": true,
  \"implicitFlowEnabled\": false,
  \"directAccessGrantsEnabled\": false,
  \"serviceAccountsEnabled\": false,
  \"protocol\": \"openid-connect\",
  \"secret\": \"${HOME_SECRET}\",
  \"redirectUris\": [
    \"https://home.xn--pcwchter-2za.de/api/auth/callback/keycloak\",
    \"http://localhost:3000/api/auth/callback/keycloak\",
    \"http://localhost:13001/api/auth/callback/keycloak\"
  ],
  \"webOrigins\": [
    \"https://home.xn--pcwchter-2za.de\",
    \"http://localhost:3000\",
    \"http://localhost:13001\"
  ],
  \"attributes\": {
    \"post.logout.redirect.uris\": \"https://home.xn--pcwchter-2za.de/*##http://localhost:3000/*##http://localhost:13001/*\"
  }
}"
PCW_HOME_UUID=$(get_client_uuid "pcwaechter-home")
add_audience_mapper "${PCW_HOME_UUID}"
add_roles_mapper    "${PCW_HOME_UUID}"

# ── 8. Client: pcwaechter-desktop (public, PKCE) ─────────────────────────────
log "Creating client: pcwaechter-desktop ..."
upsert_client "pcwaechter-desktop" '{
  "clientId": "pcwaechter-desktop",
  "name": "PCWächter Desktop",
  "enabled": true,
  "publicClient": true,
  "standardFlowEnabled": true,
  "implicitFlowEnabled": false,
  "directAccessGrantsEnabled": false,
  "serviceAccountsEnabled": false,
  "protocol": "openid-connect",
  "redirectUris": [
    "http://127.0.0.1:8765/callback",
    "http://localhost:8765/callback"
  ],
  "webOrigins": [],
  "attributes": {
    "pkce.code.challenge.method": "S256",
    "post.logout.redirect.uris": "http://127.0.0.1:8765/logout##http://localhost:8765/logout"
  }
}'
DESKTOP_UUID=$(get_client_uuid "pcwaechter-desktop")
add_audience_mapper "${DESKTOP_UUID}"
add_roles_mapper    "${DESKTOP_UUID}"

# ── 9. Client: pcwaechter-api (confidential, audience target) ────────────────
log "Creating client: pcwaechter-api ..."
upsert_client "pcwaechter-api" '{
  "clientId": "pcwaechter-api",
  "name": "PCWächter API",
  "enabled": true,
  "publicClient": false,
  "standardFlowEnabled": false,
  "implicitFlowEnabled": false,
  "directAccessGrantsEnabled": false,
  "serviceAccountsEnabled": true,
  "bearerOnly": false,
  "protocol": "openid-connect"
}'
PCW_API_UUID=$(get_client_uuid "pcwaechter-api")
add_audience_mapper "${PCW_API_UUID}"

# ── 10. Summary ───────────────────────────────────────────────────────────────
log ""
log "=== Realm provisioning complete ==="
log "Realm:             ${REALM}"
MASKED_HOME_SECRET="${HOME_SECRET:0:4}***${HOME_SECRET: -4}"
log "Home client secret: ${MASKED_HOME_SECRET}"
log ""
log "Add to .env:"
log "  AUTH_KEYCLOAK_SECRET=<masked>"
log ""
log "Verify OIDC discovery:"
log "  curl ${KEYCLOAK_URL}/realms/${REALM}/.well-known/openid-configuration"
