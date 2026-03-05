# Zammad Mapping (Keycloak User -> Zammad User)

Problem:
- Support Router braucht "customer_id/group" Zuordnung.
- Hardcode `customer_id: 1` ist nicht sauber.

Empfohlenes Mapping (Phase 1 pragmatisch):
1) Nutze User Claim `email` aus JWT
2) Zammad API:
   - Search user by email
   - Wenn nicht gefunden: create user (role: customer)
3) Nutze returned user.id als customer_id

Optional:
- Org/Tenant mapping: nach domain oder Keycloak group/tenant_id
- Group mapping: z.B. "Support" group id via config

Benötigte ENV:
- ZAMMAD_BASE_URL
- ZAMMAD_API_TOKEN
- ZAMMAD_DEFAULT_GROUP_ID (optional)
- ZAMMAD_DEFAULT_ORG_ID (optional)
