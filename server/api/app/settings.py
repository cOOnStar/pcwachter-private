from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    DATABASE_URL: str = "postgresql+psycopg://pcw:pcw@postgres:5432/pcw"
    ONLINE_THRESHOLD_SECONDS: int = 90
    LOG_LEVEL: str = "INFO"

    # API-Keys
    API_KEYS: str = ""
    AGENT_API_KEYS: str = ""
    RATELIMIT_REDIS_URL: str = ""

    # Agent Bootstrap-Key (replaces static API-key for /agent/register)
    # Set a long random secret here; agents supply it as X-Agent-Bootstrap-Key.
    AGENT_BOOTSTRAP_KEY: str = ""
    # Legacy API-key register path (temporary migration toggle).
    # Default is OFF; only set true while migrating old agents.
    ALLOW_LEGACY_API_KEY_REGISTER: bool = False

    # Keycloak
    KEYCLOAK_URL: str = "https://login.xn--pcwchter-2za.de"
    KEYCLOAK_REALM: str = "pcwaechter-prod"
    KEYCLOAK_AUDIENCE: str = "pcwaechter-api"
    KEYCLOAK_ISSUER: str = ""
    KEYCLOAK_ADMIN_USER: str = ""
    KEYCLOAK_ADMIN_PASSWORD: str = ""
    KEYCLOAK_ADMIN_CLIENT_ID: str = "admin-cli"
    KEYCLOAK_ADMIN_CLIENT_SECRET: str = ""

    # Zugriffskontrolle
    CONSOLE_ALLOWED_ROLES: str = "pcw_admin,pcw_console"
    CORS_ORIGINS: str = (
        "https://console.xn--pcwchter-2za.de,"
        "https://home.xn--pcwchter-2za.de"
    )

    # Zammad (optional)
    ZAMMAD_BASE_URL: str = ""
    ZAMMAD_API_TOKEN: str = ""
    ZAMMAD_WEBHOOK_SECRET: str = ""
    ZAMMAD_DEFAULT_GROUP_ID: int = 1
    ZAMMAD_DEFAULT_ORG_ID: int = 0
    # Set to the Zammad role ID for "Customer". 0 = resolve dynamically via GET /api/v1/roles.
    ZAMMAD_CUSTOMER_ROLE_ID: int = 0
    SUPPORT_ATTACHMENT_MAX_BYTES: int = 25 * 1024 * 1024

    # Storage
    EXPORT_DIR: str = "/data/exports"
    UPLOAD_DIR: str = "/data/uploads"

    # Figma
    FIGMA_PREVIEW_KEY: str = ""

    # Stripe
    STRIPE_SECRET_KEY: str = ""
    STRIPE_WEBHOOK_SECRET: str = ""
    STRIPE_PUBLISHABLE_KEY: str = ""
    STRIPE_ENABLED: bool = True
    STRIPE_CURRENCY_DEFAULT: str = "eur"
    # Customer Portal configurations (Stripe dashboard IDs, format: bpc_xxx)
    # Portal config without cancel option (default for all subscriptions)
    STRIPE_PORTAL_CONFIG_NO_CANCEL: str = ""
    # Portal config with cancel option (used when sub.allow_self_cancel=True)
    STRIPE_PORTAL_CONFIG_WITH_CANCEL: str = ""


settings = Settings()
