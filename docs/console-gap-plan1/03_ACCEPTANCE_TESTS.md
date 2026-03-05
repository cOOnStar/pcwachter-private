# Acceptance / Smoke Tests — Console Gap Plan 1

## Build Checks
```bash
cd server/console
npm ci
npm run lint
npm run build
```

## API quick checks (requires running stack + valid admin token)
> Token: Admin via Keycloak (prod: PKCE in browser; dev-only: password grant).

### Activity Feed
```bash
curl -s "$API/api/v1/console/ui/activity-feed" -H "Authorization: Bearer $ADMIN_TOKEN" | jq 'type'
```

### Knowledge Base
```bash
curl -s "$API/api/v1/console/ui/knowledge-base" -H "Authorization: Bearer $ADMIN_TOKEN" | jq 'type'
```

### Support tickets (admin)
```bash
curl -s "$API/api/v1/support/tickets?all=true&page=1&per_page=10" -H "Authorization: Bearer $ADMIN_TOKEN" | jq 'type'
```

### Update Channel override (new endpoint)
```bash
DEVICE_ID="<from console ui list>"
curl -s -X PATCH "$API/api/v1/console/ui/devices/$DEVICE_ID/update-channel"   -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json"   -d '{"update_channel":"beta"}' | jq .
```

## UI checks (manual)
- Sidebar shows: Activity, Knowledge Base, Support, (optional Updates)
- Pages load without console errors
- Support page:
  - Non-admin user sees only own tickets
  - Admin sees all=true toggle
  - Ticket detail: reply works
  - Attachment upload works (returns upload info)
- Device detail:
  - update_channel dropdown sets new value
  - Device list shows updated channel after refresh
