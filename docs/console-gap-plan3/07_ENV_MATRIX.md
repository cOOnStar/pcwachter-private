# 07 — ENV Matrix (Support)

## API (server/api)
| Variable | required | Beispiel | Zweck |
|---|---:|---|---|
| ZAMMAD_BASE_URL | yes | https://support.pcwächter.de | Zammad API Base |
| ZAMMAD_API_TOKEN | yes | <token> | Auth gegen Zammad |
| ZAMMAD_DEFAULT_GROUP_ID | yes | 1 | Tickets default group |
| ZAMMAD_DEFAULT_ORG_ID | no | 0 | optional org |
| ZAMMAD_WEBHOOK_SECRET | no | <secret> | optional inbound webhook |

## Proxy (NPM)
- upload limit (Attachments)
- forwarded headers (Host/Proto)

## Keycloak
- Token muss `email` claim enthalten.
