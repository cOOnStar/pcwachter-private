import logging

from fastapi import APIRouter, FastAPI, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
from limits import parse as rl_parse
from limits import storage as rl_storage
from limits import strategies as rl_strategies
from slowapi import Limiter, _rate_limit_exceeded_handler
from slowapi.errors import RateLimitExceeded
from slowapi.middleware import SlowAPIMiddleware
from slowapi.util import get_remote_address

from .db import Base, engine
from .routers.admin import router as admin_router
from .routers.agent import router as agent_router
from .routers.client import router as client_router
from .routers.console import router as console_router
from .routers.features import router as features_router
from .routers.license import router as license_router
from .routers.payments import router as payments_router
from .routers.support import router as support_router
from .routers.telemetry import router as telemetry_router
from .settings import settings

logger = logging.getLogger("pcw.api")

def _build_rate_limit_storage() -> tuple[object, str]:
    redis_url = settings.RATELIMIT_REDIS_URL.strip()
    if redis_url:
        try:
            storage = rl_storage.storage_from_string(redis_url)
            logger.info("rate-limit storage: redis")
            return storage, redis_url
        except Exception as exc:
            logger.warning("rate-limit redis unavailable, fallback to memory: %s", exc)
    return rl_storage.MemoryStorage(), "memory://"


_rl_storage, _rl_storage_uri = _build_rate_limit_storage()
limiter = Limiter(
    key_func=get_remote_address,
    default_limits=["300/minute"],
    storage_uri=_rl_storage_uri,
)

# ---------------------------------------------------------------------------
# Pre-auth rate limiter (counts every request, including 401/422)
# Runs before routing so auth failures also consume quota.
# ---------------------------------------------------------------------------
_rl_mw = rl_strategies.MovingWindowRateLimiter(_rl_storage)
# path → (limit_obj, window_seconds for Retry-After)
_PRE_AUTH_RULES: dict[str, tuple] = {
    "/agent/register":           (rl_parse("10/minute"), 60),
    "/api/v1/agent/register":    (rl_parse("10/minute"), 60),
    "/payments/webhook":         (rl_parse("120/minute"), 60),
    "/api/v1/payments/webhook":  (rl_parse("120/minute"), 60),
    "/license/status":           (rl_parse("30/minute"), 60),
    "/api/v1/license/status":    (rl_parse("30/minute"), 60),
}

# ---------------------------------------------------------------------------
# Body size limit (raw bytes, before Pydantic parsing)
# ---------------------------------------------------------------------------
_MAX_BODY_BYTES = 1 * 1024 * 1024  # 1 MB

app = FastAPI(title="PCWaechter API", version="1.0.0")
app.state.limiter = limiter
app.add_exception_handler(RateLimitExceeded, _rate_limit_exceeded_handler)
app.add_middleware(SlowAPIMiddleware)


@app.middleware("http")
async def body_size_limit(request: Request, call_next):
    content_length = request.headers.get("content-length")
    if content_length and int(content_length) > _MAX_BODY_BYTES:
        return JSONResponse(
            {"detail": "payload too large"},
            status_code=413,
            headers={"Content-Type": "application/json"},
        )
    # For streaming bodies without Content-Length, read and check
    if request.method in ("POST", "PUT", "PATCH") and not content_length:
        body = await request.body()
        if len(body) > _MAX_BODY_BYTES:
            return JSONResponse({"detail": "payload too large"}, status_code=413)
    return await call_next(request)


@app.middleware("http")
async def pre_auth_rate_limit(request: Request, call_next):
    rule_entry = _PRE_AUTH_RULES.get(request.url.path)
    if rule_entry:
        limit_obj, retry_after = rule_entry
        ip = request.client.host if request.client else "unknown"
        if not _rl_mw.hit(limit_obj, ip, request.url.path):
            return JSONResponse(
                {"detail": "rate limit exceeded"},
                status_code=429,
                headers={"Retry-After": str(retry_after)},
            )
    return await call_next(request)


# ---------------------------------------------------------------------------
# Legacy-path deprecation tracking (raw ASGI middleware — header-injection safe)
# Logs a warning every 100 hits per path; injects Deprecation/Sunset/Link
# headers into the HTTP response.start ASGI message before headers are sent.
# ---------------------------------------------------------------------------
import collections
_legacy_hit_counts: dict[str, int] = collections.defaultdict(int)
_LEGACY_LOG_EVERY = 100
_SUNSET_DATE = "Sun, 01 Sep 2026 00:00:00 GMT"

# Paths served by the legacy compat layer (no /api/v1 prefix)
_LEGACY_PREFIXES = (
    "/agent/", "/console/", "/telemetry/", "/admin/", "/license/", "/payments/",
    "/agent", "/console", "/telemetry", "/admin", "/license", "/payments",
)


class _LegacyDeprecationMiddleware:
    """Pure-ASGI middleware: injects Deprecation/Sunset/Link headers on legacy paths."""

    def __init__(self, app):
        self._app = app

    async def __call__(self, scope, receive, send):
        if scope["type"] != "http":
            await self._app(scope, receive, send)
            return

        path = scope.get("path", "")
        is_legacy = (
            any(path.startswith(p) for p in _LEGACY_PREFIXES)
            and not path.startswith("/api/")
        )

        if not is_legacy:
            await self._app(scope, receive, send)
            return

        _legacy_hit_counts[path] += 1
        count = _legacy_hit_counts[path]
        if count % _LEGACY_LOG_EVERY == 1:
            logger.warning(
                "legacy path hit #%d: %s %s",
                count,
                scope.get("method", ""),
                path,
            )

        canonical = f"/api/v1{path}"

        async def _send_with_deprecation(message):
            if message["type"] == "http.response.start":
                extra = [
                    (b"deprecation", b"true"),
                    (b"sunset", _SUNSET_DATE.encode()),
                    (b"link", f'<{canonical}>; rel="successor-version"'.encode()),
                ]
                message = {**message, "headers": list(message.get("headers", [])) + extra}
            await send(message)

        await self._app(scope, receive, _send_with_deprecation)


# NOTE: add_middleware wraps the app stack; last added = outermost.
# LegacyDeprecationMiddleware added here (before CORS) so it runs outside all others.
_cors_origins = [o.strip() for o in settings.CORS_ORIGINS.split(",") if o.strip()]
# Lokale Dev-Origins immer erlauben
_cors_origins += [
    "http://localhost:5173",
    "http://localhost:3000",
    "http://localhost:13000",
    "http://localhost:13001",
]

app.add_middleware(
    CORSMiddleware,
    allow_origins=_cors_origins,
    allow_credentials=True,
    allow_methods=["GET", "POST", "PUT", "DELETE", "OPTIONS"],
    allow_headers=["*"],
)
app.add_middleware(_LegacyDeprecationMiddleware)

# Fuer lokale Entwicklungsumgebungen ohne Migrationslauf.
Base.metadata.create_all(bind=engine)


@app.get("/health")
@app.get("/api/v1/health")
def health():
    return {"ok": True}


# ---------------------------------------------------------------------------
# v1 versioned routes (kanonisch)
# ---------------------------------------------------------------------------
_v1 = APIRouter(prefix="/api/v1")
_v1.include_router(agent_router)
_v1.include_router(client_router)
_v1.include_router(console_router)
_v1.include_router(features_router)
_v1.include_router(telemetry_router)
_v1.include_router(admin_router)
_v1.include_router(license_router)
_v1.include_router(payments_router)
_v1.include_router(support_router)
app.include_router(_v1)

# ---------------------------------------------------------------------------
# Legacy routes ohne Prefix (Compat Layer – bestehende Agents/Clients)
# ---------------------------------------------------------------------------
app.include_router(agent_router)
app.include_router(client_router)
app.include_router(console_router)
app.include_router(features_router)
app.include_router(telemetry_router)
app.include_router(admin_router)
app.include_router(license_router)
app.include_router(payments_router)
app.include_router(support_router)
