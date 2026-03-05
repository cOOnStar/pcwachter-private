# Support API - Smoke Tests

> Endpoints: `POST /api/v1/support/tickets`, `GET /api/v1/support/tickets`,
> `GET /api/v1/support/tickets/{id}`, `GET /api/v1/support/admin/diag/zammad-roles`,
> `GET /api/v1/support/admin/diag/zammad-user`
>
> Realm: `pcwaechter-prod` - Keycloak URL: `https://login.xn--pcwchter-2za.de`

---

## Token Beschaffung

**WICHTIG:** Direct Access Grants (Password Grant) nur in Dev aktivieren.
In Prod: Token aus Browser DevTools (Network -> POST .../token nach Login) oder PKCE-Flow.

```bash
KC_URL="https://login.xn--pcwchter-2za.de"
REALM="pcwaechter-prod"
CLIENT="pcwaechter-api"
API="http://localhost:8000"

# DEV ONLY
USER_TOKEN=$(curl -sf -X POST \
  "$KC_URL/realms/$REALM/protocol/openid-connect/token" \
  -d "grant_type=password&client_id=$CLIENT&username=testuser&password=testpass" \
  | jq -r .access_token)

ADMIN_TOKEN=$(curl -sf -X POST \
  "$KC_URL/realms/$REALM/protocol/openid-connect/token" \
  -d "grant_type=password&client_id=$CLIENT&username=admin&password=adminpass" \
  | jq -r .access_token)
```

---

## 1) User creates ticket

```bash
curl -s -X POST "$API/api/v1/support/tickets" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"title":"Agent startet nicht","body":"Nach dem Update startet der Agent nicht mehr."}' \
  | jq '{id: .id, title: .title, customer: .customer}'
# Erwartet: HTTP 200/201, .id vorhanden
```

---

## 2) User lists only own tickets

```bash
curl -s "$API/api/v1/support/tickets" \
  -H "Authorization: Bearer $USER_TOKEN" | jq .
# Erwartet: nur eigene Tickets
# Hinweis: Response kann `array` oder `object` (Search-Result) sein.
curl -s "$API/api/v1/support/tickets" \
  -H "Authorization: Bearer $USER_TOKEN" | jq 'type, (if type=="array" then length else (.tickets|length? // "n/a") end)'

curl -s "$API/api/v1/support/tickets?page=2&per_page=10" \
  -H "Authorization: Bearer $USER_TOKEN" | jq .
# Erwartet: Pagination wird durchgereicht
```

---

## 3) User all=true -> 403

```bash
curl -s -o /dev/null -w "%{http_code}" \
  "$API/api/v1/support/tickets?all=true" \
  -H "Authorization: Bearer $USER_TOKEN"
# Erwartet: 403

curl -s "$API/api/v1/support/tickets?all=true" \
  -H "Authorization: Bearer $USER_TOKEN" | jq .detail
# Erwartet: "forbidden: all=true requires admin role"
```

---

## 4) Admin all=true -> ok

```bash
curl -s "$API/api/v1/support/tickets?all=true&page=1&per_page=50" \
  -H "Authorization: Bearer $ADMIN_TOKEN" | jq 'type, (if type=="array" then length else (.tickets|length? // "n/a") end)'
# Erwartet: Anzahl aller Tickets
# Hinweis: Response kann `array` oder `object` (Search-Result) sein.
```

---

## 5) User reads foreign ticket -> 404

```bash
FOREIGN_TICKET_ID=999

curl -s -o /dev/null -w "%{http_code}" \
  "$API/api/v1/support/tickets/$FOREIGN_TICKET_ID" \
  -H "Authorization: Bearer $USER_TOKEN"
# Erwartet: 404
```

---

## 6) Token ohne email -> 400 user_email_missing

```bash
curl -s -X POST "$API/api/v1/support/tickets" \
  -H "Authorization: Bearer $NO_EMAIL_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"title":"T","body":"B"}' | jq .
# Erwartet: HTTP 400 {"detail":"user_email_missing"}
```

---

## 7) Zammad down -> 502 zammad_unreachable

```bash
# Beispiel: ZAMMAD_BASE_URL auf nicht erreichbare URL setzen
# ZAMMAD_BASE_URL="http://127.0.0.1:19999"

curl -s "$API/api/v1/support/tickets" \
  -H "Authorization: Bearer $USER_TOKEN" | jq .
# Erwartet: HTTP 502 und "detail" beginnt mit "zammad_unreachable during "
# Oder 503 support_not_configured wenn ZAMMAD_BASE_URL leer ist
```

---

## 8) Admin diag roles -> list roles, Customer role sichtbar

```bash
curl -s "$API/api/v1/support/admin/diag/zammad-roles" \
  -H "Authorization: Bearer $ADMIN_TOKEN" | jq .
# Erwartet: [{"id":...,"name":"...","active":...}, ...]

curl -s "$API/api/v1/support/admin/diag/zammad-roles" \
  -H "Authorization: Bearer $ADMIN_TOKEN" | jq '.[] | select(.name=="Customer")'
# Erwartet: Customer Rolle sichtbar (id instanzabhaengig)
```

---

## 9) Pagination invalid page/per_page -> 400

```bash
curl -s "$API/api/v1/support/tickets?per_page=201" \
  -H "Authorization: Bearer $USER_TOKEN" | jq .
# Erwartet: HTTP 400 {"detail":"per_page_invalid"}

curl -s "$API/api/v1/support/tickets?page=0" \
  -H "Authorization: Bearer $USER_TOKEN" | jq .
# Erwartet: HTTP 400 {"detail":"page_invalid"}

curl -s "$API/api/v1/support/tickets?page=abc&per_page=xyz" \
  -H "Authorization: Bearer $USER_TOKEN" | jq .
# Erwartet: HTTP 400 {"detail":"page_invalid"} oder {"detail":"per_page_invalid"}
```

---

## Optional: Diag user search

```bash
curl -s "$API/api/v1/support/admin/diag/zammad-user?email=testuser@example.com" \
  -H "Authorization: Bearer $ADMIN_TOKEN" | jq .
# Erwartet: found=true/false + minimale Userdaten bei Treffer
```

---

## Unknowns und Verifikation

- unknown: Ob `GET /api/v1/tickets/search` auf der konkreten Zammad-Version verfuegbar ist.
  Verifikation:
```bash
curl -s "$ZAMMAD_BASE_URL/api/v1/tickets/search?query=customer_id:1&per_page=1" \
  -H "Authorization: Token token=$ZAMMAD_API_TOKEN"

curl -s "$ZAMMAD_BASE_URL/api/v1/tickets/search?query=customer.id:1&per_page=1" \
  -H "Authorization: Token token=$ZAMMAD_API_TOKEN"
```

- unknown: Ob Fallback `GET /api/v1/users/{id}/tickets` auf der Instanz verfuegbar ist.
  Verifikation:
```bash
curl -s "$ZAMMAD_BASE_URL/api/v1/users/1/tickets?page=1&per_page=1" \
  -H "Authorization: Token token=$ZAMMAD_API_TOKEN"
```
