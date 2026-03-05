# 03 — Commands / Runbook

> Variablen anpassen: `API`, `KC_URL`, `REALM`, `TOKEN`, `ZAMMAD_BASE_URL`

## A) Zammad erreichbar (direkt)
```bash
curl -s -o /dev/null -w "%{http_code}\n" "$ZAMMAD_BASE_URL/"
curl -s "$ZAMMAD_BASE_URL/api/v1/roles" -H "Authorization: Token token=$ZAMMAD_API_TOKEN" | jq '.[0:5]'
```

## B) API Support Diag (admin token nötig)
```bash
API="http://localhost:18080/api"
TOKEN="<admin bearer>"

curl -s "$API/v1/support/admin/diag/zammad-roles" -H "Authorization: Bearer $TOKEN" | jq .
curl -s "$API/v1/support/admin/diag/zammad-user?email=test@example.com" -H "Authorization: Bearer $TOKEN" | jq .
```

## C) Support Self-Service (normal user token)
```bash
USER_TOKEN="<user bearer>"

# create
curl -s -X POST "$API/v1/support/tickets"   -H "Authorization: Bearer $USER_TOKEN"   -H "Content-Type: application/json"   -d '{"title":"Test","body":"Hallo Support"}' | jq .

# list
curl -s "$API/v1/support/tickets?page=1&per_page=20"   -H "Authorization: Bearer $USER_TOKEN" | jq 'type, (if type=="array" then length else (.tickets|length? // "n/a") end)'
```

## D) Console build
```bash
cd server/console
npm run build
```

## E) Compose / ENV (Beispiel)
```bash
# .env ergänzen (API Container liest sie)
# ZAMMAD_BASE_URL=https://support.pcwächter.de
# ZAMMAD_API_TOKEN=...
# ZAMMAD_DEFAULT_GROUP_ID=1
# ZAMMAD_DEFAULT_ORG_ID=0
# optional:
# ZAMMAD_WEBHOOK_SECRET=...

docker compose -f server/infra/compose/docker-compose.yml --env-file .env up -d api
```

## F) Zammad down Simulation
```bash
# setze ZAMMAD_BASE_URL auf nicht erreichbare Adresse
# Erwartet: API gibt 502 detail "zammad_unreachable during <operation>"
```
