# Settings Notes

This file documents maintenance-focused settings additions.

## `RATELIMIT_REDIS_URL` (optional)

Rate-limit storage can run in two modes:

1. `RATELIMIT_REDIS_URL` set:
   - API tries to initialize rate-limit storage from the Redis URL.
   - Example: `redis://redis:6379/0`
2. `RATELIMIT_REDIS_URL` empty (default):
   - API uses in-memory storage (`memory://`) as before.

Implementation details:

- Code path: `server/api/app/main.py` (`_build_rate_limit_storage`)
- Setting source: `server/api/app/settings.py`
- Env sample: `.env.example`

Fallback behavior:

- If Redis URL is configured but unavailable/invalid, API logs a warning and falls back to in-memory storage.
- This keeps startup behavior robust and backward compatible.

Operational note:

- In-memory rate limits are per-process and reset on restart.
- Redis-backed limits are shared and stable across restarts/replicas.

