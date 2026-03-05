# One‑Shot Umsetzungsplan (Console) — Reihenfolge + Dependencies

## Phase 0 — Repo Hygiene (einmalig)
1) New Branch:
- `git checkout -b feat/console-gap-plan1`
2) Ensure builds:
- `cd server/console && npm ci && npm run build`

## Phase 1 (P1) — Pages + API wrapper (ohne DB-Migrations)
### 1.1 Activity Feed
- Add `ActivityFeedPage.tsx` unter `server/console/src/app/pages/`
- Add api-service call:
  - `getActivityFeed(): Promise<ActivityItem[]>` → `GET /api/v1/console/ui/activity-feed`
- Add route + nav item:
  - Sidebar: add `/activity`
  - Routes: add `<Route path="/activity" element={<ActivityFeedPage/>} />`

### 1.2 Knowledge Base (read-only)
- Add `KnowledgeBasePage.tsx`
- Add api-service call:
  - `getKnowledgeBase(): Promise<KbArticle[]>` → `GET /api/v1/console/ui/knowledge-base`
- Add route + nav item:
  - Sidebar: add `/knowledge-base`

### 1.3 Support Overview (Self-service + Admin)
- Add `SupportPage.tsx` (list)
- Add `SupportTicketDetailPage.tsx` (detail + reply + attachments)
- api-service additions:
  - `listSupportTickets({all, page, perPage})` → `GET /api/v1/support/tickets` (+ `all=true` für admin)
  - `getSupportTicket(id)` → `GET /api/v1/support/tickets/{id}`
  - `replySupportTicket(id, body)` → `POST /api/v1/support/tickets/{id}/reply`
  - `uploadSupportAttachment(file)` → `POST /api/v1/support/attachments` (multipart/form-data)
  - `diagZammadRoles()` (admin only) → `GET /api/v1/support/admin/diag/zammad-roles`
- Add route + nav item:
  - Sidebar: add `/support`

### 1.4 Device Update Channel Override (Admin)
**Backend erforderlich** (klein, additiv):
- Add endpoint in `server/api/app/routers/console.py`:
  - `PATCH /api/v1/console/ui/devices/{device_id}/update-channel`
  - Body: `{ "update_channel": "stable|beta|internal" }`
  - Auth: `Depends(require_console_owner)` (owner/admin write)
  - Update `devices.update_channel` + audit log entry (falls vorhanden)
- Console UI:
  - In `DeviceDetailPage.tsx`: Dropdown + Save action
  - api-service: `setDeviceUpdateChannel(deviceId, channel)`

## Phase 2 (P2) — Optional, abhängig von Backend
### 2.1 Downloads/Updates Page (minimal, ohne DB)
- Add `UpdatesPage.tsx`:
  - Fetch GitHub `latest/download/installer-manifest.json` via `NEXT_PUBLIC_RELEASE_BASE_URL` (same as Home)
  - Show: latest_version, sha256, urls, released_at
  - Purpose: Admin visibility & troubleshooting, no persistence required.

### 2.2 Client Config (Remote config) — requires DB/API first
- DB: new table `client_config` (key/value, scope, updated_at, updated_by)
- API: CRUD endpoints for console: `/api/v1/console/ui/client-config`
- Console: `ClientConfigPage.tsx` (view/edit)

### 2.3 Knowledge Base CRUD — requires DB/API first
- DB: `knowledge_base` + `knowledge_base_revisions` (optional)
- API: CRUD endpoints: `/api/v1/console/ui/knowledge-base/*`
- Console: edit/create page

### 2.4 Rules Management — blocked on spec
- Add `docs/rules/spec.md` first (Inputs/Outputs/DB/API)
- Then implement DB + API + Console page.

## Deliverables / Output
- Neue Pages + updated Sidebar + updated routes
- api-service erweitert
- Optional: minimal UpdatesPage
- Backend: 1 additiver Endpoint für Update-Channel override
