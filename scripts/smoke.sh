#!/usr/bin/env bash
set -u

API_BASE_URL="${API_BASE_URL:-http://localhost:18080}"
COMPOSE_FILE="${COMPOSE_FILE:-server/infra/compose/docker-compose.yml}"
ENV_FILE="${ENV_FILE:-.env}"
SMOKE_TIMEOUT="${SMOKE_TIMEOUT:-15}"
TEST_WEBHOOK_SECRET="${TEST_WEBHOOK_SECRET:-}"

PASS_COUNT=0
FAIL_COUNT=0
SKIP_COUNT=0
TMP_FILES=()

register_temp_file() {
  TMP_FILES+=("$1")
}

make_temp_file() {
  local f
  f="$(mktemp)"
  register_temp_file "$f"
  printf '%s' "$f"
}

cleanup_temp_files() {
  local f
  for f in "${TMP_FILES[@]}"; do
    if [ -n "$f" ] && [ -e "$f" ]; then
      rm -f "$f"
    fi
  done
}

trap cleanup_temp_files EXIT INT TERM

log_pass() {
  PASS_COUNT=$((PASS_COUNT + 1))
  printf '[PASS] %s\n' "$1"
}

log_fail() {
  FAIL_COUNT=$((FAIL_COUNT + 1))
  printf '[FAIL] %s\n' "$1"
}

log_skip() {
  SKIP_COUNT=$((SKIP_COUNT + 1))
  printf '[SKIP] %s\n' "$1"
}

mask_secret() {
  local value="$1"
  local len="${#value}"
  if [ "$len" -le 4 ]; then
    printf '***'
    return
  fi
  printf '%s***%s' "${value:0:2}" "${value:len-2:2}"
}

get_env_file_value() {
  local key="$1"
  if [ ! -f "$ENV_FILE" ]; then
    return 1
  fi
  local line
  line="$(grep -E "^${key}=" "$ENV_FILE" | tail -n 1 || true)"
  if [ -z "$line" ]; then
    return 1
  fi
  printf '%s' "${line#*=}"
}

first_csv_item() {
  local raw="$1"
  local item
  IFS=',' read -r -a parts <<< "$raw"
  for item in "${parts[@]}"; do
    item="$(printf '%s' "$item" | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')"
    if [ -n "$item" ]; then
      printf '%s' "$item"
      return
    fi
  done
}

json_get_string() {
  local body="$1"
  local key="$2"
  printf '%s' "$body" \
    | tr -d '\n\r' \
    | sed -n "s/.*\"${key}\"[[:space:]]*:[[:space:]]*\"\\([^\"]*\\)\".*/\\1/p"
}

curl_status() {
  local method="$1"
  local url="$2"
  local data_file="$3"
  shift 3
  local headers=("$@")

  local args=(-sS -o /dev/null -w "%{http_code}" --max-time "$SMOKE_TIMEOUT" -X "$method" "$url")
  local h
  for h in "${headers[@]}"; do
    args+=(-H "$h")
  done
  if [ -n "$data_file" ]; then
    args+=(--data-binary "@$data_file")
  fi
  curl "${args[@]}"
}

curl_body_and_status() {
  local method="$1"
  local url="$2"
  local data_file="$3"
  shift 3
  local headers=("$@")
  local body_file
  body_file="$(make_temp_file)"

  local args=(-sS -o "$body_file" -w "%{http_code}" --max-time "$SMOKE_TIMEOUT" -X "$method" "$url")
  local h
  for h in "${headers[@]}"; do
    args+=(-H "$h")
  done
  if [ -n "$data_file" ]; then
    args+=(--data-binary "@$data_file")
  fi

  local status
  status="$(curl "${args[@]}")"
  local body
  body="$(cat "$body_file")"
  printf '%s\n%s' "$status" "$body"
}

resolve_api_key() {
  API_KEY=""
  API_KEY_HEADER=""

  if [ -n "${SMOKE_API_KEY:-}" ]; then
    API_KEY="$SMOKE_API_KEY"
    API_KEY_HEADER="${SMOKE_API_KEY_HEADER:-X-API-Key}"
    return
  fi

  local api_keys="${API_KEYS:-}"
  if [ -z "$api_keys" ]; then
    api_keys="$(get_env_file_value API_KEYS || true)"
  fi
  local first
  first="$(first_csv_item "$api_keys")"
  if [ -n "$first" ]; then
    API_KEY="$first"
    API_KEY_HEADER="X-API-Key"
    return
  fi

  local agent_keys="${AGENT_API_KEYS:-}"
  if [ -z "$agent_keys" ]; then
    agent_keys="$(get_env_file_value AGENT_API_KEYS || true)"
  fi
  first="$(first_csv_item "$agent_keys")"
  if [ -n "$first" ]; then
    API_KEY="$first"
    API_KEY_HEADER="X-Agent-Api-Key"
  fi
}

has_configured_api_keys() {
  local api_keys="${API_KEYS:-}"
  if [ -z "$api_keys" ]; then
    api_keys="$(get_env_file_value API_KEYS || true)"
  fi
  if [ -n "$(first_csv_item "$api_keys")" ]; then
    return 0
  fi

  local agent_keys="${AGENT_API_KEYS:-}"
  if [ -z "$agent_keys" ]; then
    agent_keys="$(get_env_file_value AGENT_API_KEYS || true)"
  fi
  if [ -n "$(first_csv_item "$agent_keys")" ]; then
    return 0
  fi

  return 1
}

test_health() {
  local s1 s2
  s1="$(curl -sS -o /dev/null -w "%{http_code}" --max-time "$SMOKE_TIMEOUT" "$API_BASE_URL/health")"
  s2="$(curl -sS -o /dev/null -w "%{http_code}" --max-time "$SMOKE_TIMEOUT" "$API_BASE_URL/api/v1/health")"

  if [ "$s1" = "200" ] && [ "$s2" = "200" ]; then
    log_pass "health endpoints (/health, /api/v1/health) -> 200"
  else
    log_fail "health endpoints expected 200/200, got $s1/$s2"
  fi
}

test_pre_auth_rate_limit_path() {
  local path="$1"
  local payload_file="$2"
  local statuses=()
  local i status
  for i in $(seq 1 11); do
    status="$(curl_status "POST" "$API_BASE_URL$path" "$payload_file" "Content-Type: application/json" "X-API-Key: invalid-smoke-key")"
    statuses+=("$status")
  done

  local ok=true
  for i in $(seq 0 9); do
    if [ "${statuses[$i]}" != "401" ]; then
      ok=false
    fi
  done
  if [ "${statuses[10]}" != "429" ]; then
    ok=false
  fi

  if [ "$ok" = true ]; then
    log_pass "pre-auth rate limit ($path): 401 x10 -> 429"
  else
    log_fail "pre-auth rate limit $path expected 401 x10 -> 429, got: ${statuses[*]}"
  fi
}

test_pre_auth_rate_limit() {
  if ! has_configured_api_keys; then
    log_skip "pre-auth rate limit: API_KEYS/AGENT_API_KEYS not configured, cannot assert 401 baseline"
    return
  fi

  local payload_file
  payload_file="$(make_temp_file)"
  cat > "$payload_file" << 'EOF'
{"device_install_id":"smoke-rl-device","hostname":"smoke-rl","os":{"name":"Windows","version":"11","build":"26000"},"agent":{"version":"1.0.0","channel":"stable"},"network":{"primary_ip":"127.0.0.1","macs":[]}}
EOF

  test_pre_auth_rate_limit_path "/agent/register" "$payload_file"
  test_pre_auth_rate_limit_path "/api/v1/agent/register" "$payload_file"
}

test_body_limit_path() {
  local path="$1"
  local payload_file="$2"
  local status
  status="$(curl_status "POST" "$API_BASE_URL$path" "$payload_file" "Content-Type: application/json")"
  if [ "$status" = "413" ]; then
    log_pass "body limit $path >1MB -> 413"
  else
    log_fail "body limit $path expected 413, got $status"
  fi
}

test_body_limit() {
  local payload_file
  payload_file="$(make_temp_file)"
  dd if=/dev/zero bs=1 count=$((1024 * 1024 + 1)) 2>/dev/null | tr '\0' 'x' > "$payload_file"

  test_body_limit_path "/api/v1/payments/webhook" "$payload_file"
  test_body_limit_path "/payments/webhook" "$payload_file"
}

test_legacy_headers() {
  local header_file
  header_file="$(make_temp_file)"
  local status
  status="$(curl -sS -o /dev/null -D "$header_file" -w "%{http_code}" --max-time "$SMOKE_TIMEOUT" "$API_BASE_URL/license/status?device_install_id=smoke-legacy")"

  local has_deprecation has_sunset has_link
  has_deprecation="$(grep -i '^deprecation:' "$header_file" || true)"
  has_sunset="$(grep -i '^sunset:' "$header_file" || true)"
  has_link="$(grep -i '^link:' "$header_file" || true)"

  if [ "$status" = "401" ] && [ -n "$has_deprecation" ] && [ -n "$has_sunset" ] && [ -n "$has_link" ]; then
    log_pass "legacy headers on /license/status (401 includes Deprecation/Sunset/Link)"
  else
    log_fail "legacy headers expected status 401 with Deprecation/Sunset/Link, got status=$status"
  fi
}

test_device_token_rotate() {
  resolve_api_key
  if [ -z "$API_KEY" ] || [ -z "$API_KEY_HEADER" ]; then
    log_skip "device token rotate: API key missing (SMOKE_API_KEY or .env API_KEYS/AGENT_API_KEYS)"
    return
  fi

  local masked_key
  masked_key="$(mask_secret "$API_KEY")"
  local device_id
  device_id="smoke-device-$(date +%s)"

  local register_payload
  register_payload="$(make_temp_file)"
  cat > "$register_payload" << EOF
{"device_install_id":"$device_id","hostname":"smoke-host","os":{"name":"Windows","version":"11","build":"26000"},"agent":{"version":"1.0.0","channel":"stable"},"network":{"primary_ip":"10.0.0.1","macs":["00:11:22:33:44:55"]}}
EOF

  local reg_raw reg_status reg_body old_token
  reg_raw="$(curl_body_and_status "POST" "$API_BASE_URL/api/v1/agent/register" "$register_payload" "Content-Type: application/json" "$API_KEY_HEADER: $API_KEY")"
  reg_status="$(printf '%s' "$reg_raw" | head -n 1)"
  reg_body="$(printf '%s' "$reg_raw" | tail -n +2)"
  old_token="$(json_get_string "$reg_body" "device_token")"

  if [ "$reg_status" != "200" ] || [ -z "$old_token" ]; then
    log_fail "device token rotate: register failed (status=$reg_status, key=${API_KEY_HEADER}:${masked_key})"
    return
  fi

  local rotate_raw rotate_status rotate_body new_token
  rotate_raw="$(curl_body_and_status "POST" "$API_BASE_URL/api/v1/agent/token/rotate" "" "X-Device-Token: $old_token")"
  rotate_status="$(printf '%s' "$rotate_raw" | head -n 1)"
  rotate_body="$(printf '%s' "$rotate_raw" | tail -n +2)"
  new_token="$(json_get_string "$rotate_body" "device_token")"

  if [ "$rotate_status" != "200" ] || [ -z "$new_token" ]; then
    log_fail "device token rotate: rotate failed (status=$rotate_status)"
    return
  fi

  local heartbeat_payload
  heartbeat_payload="$(make_temp_file)"
  cat > "$heartbeat_payload" << EOF
{"device_install_id":"$device_id","at":"$(date -u +"%Y-%m-%dT%H:%M:%SZ")","status":{}}
EOF

  local old_status new_status
  old_status="$(curl_status "POST" "$API_BASE_URL/api/v1/agent/heartbeat" "$heartbeat_payload" "Content-Type: application/json" "X-Device-Token: $old_token")"
  new_status="$(curl_status "POST" "$API_BASE_URL/api/v1/agent/heartbeat" "$heartbeat_payload" "Content-Type: application/json" "X-Device-Token: $new_token")"

  if [ "$old_status" = "401" ] && [ "$new_status" = "200" ]; then
    log_pass "device token rotate: old token revoked (401), new token valid (200)"
  else
    log_fail "device token rotate expected old=401/new=200, got old=$old_status new=$new_status"
  fi
}

test_stripe_idempotency() {
  if [ -z "$TEST_WEBHOOK_SECRET" ]; then
    log_skip "stripe idempotency: TEST_WEBHOOK_SECRET not set"
    return
  fi

  if ! command -v openssl >/dev/null 2>&1; then
    log_skip "stripe idempotency: openssl not available"
    return
  fi
  if ! command -v xxd >/dev/null 2>&1; then
    log_skip "stripe idempotency: xxd not available"
    return
  fi

  local event_id payload ts sig sig_header
  event_id="evt_smoke_idem_$(date +%s)"
  payload="{\"id\":\"$event_id\",\"type\":\"smoke.event\",\"data\":{\"object\":{}}}"
  ts="$(date +%s)"
  sig="$(printf '%s.%s' "$ts" "$payload" | openssl dgst -sha256 -hmac "$TEST_WEBHOOK_SECRET" -binary | xxd -p -c 256)"
  sig_header="t=$ts,v1=$sig"

  local payload_file
  payload_file="$(make_temp_file)"
  printf '%s' "$payload" > "$payload_file"

  local status1 status2
  status1="$(curl_status "POST" "$API_BASE_URL/api/v1/payments/webhook" "$payload_file" "Content-Type: application/json" "Stripe-Signature: $sig_header")"
  status2="$(curl_status "POST" "$API_BASE_URL/api/v1/payments/webhook" "$payload_file" "Content-Type: application/json" "Stripe-Signature: $sig_header")"

  if [ "$status1" != "200" ] || [ "$status2" != "200" ]; then
    log_fail "stripe idempotency webhook calls expected 200/200, got $status1/$status2"
    return
  fi

  local count
  count="$(docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" exec -T postgres \
    psql -U pcwaechter -d pcwaechter -t -A \
    -c "SELECT count(*) FROM webhook_events WHERE stripe_event_id='${event_id}';" 2>/dev/null | tr -d '[:space:]' || true)"

  if [ -z "$count" ]; then
    log_skip "stripe idempotency: webhook delivered (200/200), DB count check unavailable"
    return
  fi

  docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" exec -T postgres \
    psql -U pcwaechter -d pcwaechter \
    -c "DELETE FROM webhook_events WHERE stripe_event_id='${event_id}';" >/dev/null 2>&1 || true

  if [ "$count" = "1" ]; then
    log_pass "stripe idempotency: same event_id processed once (DB count=1)"
  else
    log_fail "stripe idempotency expected DB count=1, got $count"
  fi
}

printf 'Smoke tests against %s\n' "$API_BASE_URL"
test_health
test_pre_auth_rate_limit
test_body_limit
test_legacy_headers
test_device_token_rotate
test_stripe_idempotency

printf '\nSummary: PASS=%d FAIL=%d SKIP=%d\n' "$PASS_COUNT" "$FAIL_COUNT" "$SKIP_COUNT"
if [ "$FAIL_COUNT" -gt 0 ]; then
  exit 1
fi
