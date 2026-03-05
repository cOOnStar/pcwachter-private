# Smoke Tests

This project provides two smoke-test runners:

- `scripts/smoke.sh` for Linux/macOS (bash)
- `scripts/smoke.ps1` for Windows (PowerShell)

Both scripts run the same checks and print clear `[PASS]`, `[FAIL]`, and `[SKIP]` lines.

## Covered Checks

1. API health: `/health` and `/api/v1/health` -> `200`
2. Pre-auth rate limit: `/agent/register` -> `401` x10, then `429`
3. Body limit: payload `> 1MB` on `/api/v1/payments/webhook` -> `413`
4. Legacy deprecation headers on `/license/status` (also on `401`)
5. Device token rotate flow:
   - register device
   - rotate token
   - old token rejected (`401`)
   - new token accepted (`200`)
6. Stripe webhook idempotency:
   - same `event_id` sent twice
   - second processing is a NOOP (`200`)
   - DB count check (`webhook_events`) if DB access is available

## Prerequisites

- Running API service (default `http://localhost:18080`)
- `curl` available (`curl` on Linux/macOS, `curl.exe` on Windows)
- For DB-backed Stripe check: running Docker Compose stack with Postgres container
- For rotate test: at least one valid API key in env (`API_KEYS` or `AGENT_API_KEYS`)

## Environment Variables

| Variable | Required | Default | Purpose |
|---|---|---|---|
| `API_BASE_URL` | No | `http://localhost:18080` | Base URL for API smoke tests |
| `COMPOSE_FILE` | No | `server/infra/compose/docker-compose.yml` | Compose file for DB checks |
| `ENV_FILE` | No | `.env` | Env file passed to docker compose |
| `SMOKE_TIMEOUT` | No | `15` | Request timeout in seconds |
| `SMOKE_API_KEY` | No | - | Explicit API key for rotate test |
| `SMOKE_API_KEY_HEADER` | No | `X-API-Key` | Header name for `SMOKE_API_KEY` |
| `TEST_WEBHOOK_SECRET` | No | - | Enables Stripe idempotency test |

Notes:

- If `SMOKE_API_KEY` is not set, scripts try `API_KEYS` / `AGENT_API_KEYS` (env first, then `.env`).
- If required env/config is missing for a test, the test is marked as `[SKIP]`.
- Secrets are never printed in full (masked in logs).

## Usage

Linux/macOS:

```bash
chmod +x scripts/smoke.sh
./scripts/smoke.sh
```

Windows PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke.ps1
```

Examples with custom env:

```bash
API_BASE_URL=http://localhost:18080 TEST_WEBHOOK_SECRET=whsec_xxx ./scripts/smoke.sh
```

```powershell
$env:API_BASE_URL = "http://localhost:18080"
$env:TEST_WEBHOOK_SECRET = "whsec_xxx"
powershell -ExecutionPolicy Bypass -File .\scripts\smoke.ps1
```

## Exit Code

- `0`: all executed tests passed (SKIP allowed)
- `1`: at least one test failed

