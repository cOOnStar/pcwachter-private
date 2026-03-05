# Services & Settings Reference

## FastAPI API
- Base URL: `https://api.pcwächter.de/v1`
- Health: `/health`

### Required ENV (minimum)
- `DATABASE_URL`
- `KEYCLOAK_ISSUER`
- `KEYCLOAK_JWKS_URL`
- `KEYCLOAK_AUDIENCE`
- `CORS_ORIGINS`

### Optional ENV
- Stripe: `STRIPE_SECRET_KEY`, `STRIPE_WEBHOOK_SECRET`
- Zammad: `ZAMMAD_BASE_URL`, `ZAMMAD_API_TOKEN`, `ZAMMAD_WEBHOOK_SECRET`
- Storage: `EXPORT_DIR`, `UPLOAD_DIR`
- Ops: `LOG_LEVEL`, `RATE_LIMIT_ENABLED`, `CDN_BASE_URL`

## Home Portal (Next.js)
- `NEXT_PUBLIC_API_BASE`
- `NEXT_PUBLIC_KEYCLOAK_URL`
- `NEXT_PUBLIC_KEYCLOAK_REALM`
- `NEXT_PUBLIC_KEYCLOAK_CLIENT_ID`
- optional (server OIDC): `KEYCLOAK_CLIENT_SECRET`, `SESSION_SECRET`

## Admin Console (React)
- `VITE_API_BASE`
- `VITE_KEYCLOAK_URL`
- `VITE_KEYCLOAK_REALM`
- `VITE_KEYCLOAK_CLIENT_ID`

## Webhooks
- Stripe -> API `/payments/webhook`
- Zammad -> API `/support/webhook`
