# Endpoint Inventory (FastAPI)

## Scope
- Vollständige FastAPI-Endpoints mit kanonischem Prefix `/api/v1`.
- Legacy-Aliase ohne Prefix sind als Zusatzspalte dokumentiert (Compat-Layer).
- Router werden sowohl unter `/api/v1` als auch legacy ohne Prefix eingebunden.
- Nachweis Router-Mount: `server/api/app/main.py:202-227`.

## Auth Guard Mapping (Nachweis)
| Guard/Auth-Typ | Nachweis |
|---|---|
| API key (`X-API-Key`/`X-Agent-Api-Key`) | `server/api/app/security.py:20-33` |
| Agent bootstrap key (legacy API key optional) | `server/api/app/security.py:67-100` |
| Agent device token oder API key | `server/api/app/security.py:35-65` |
| JWT verified bearer | `server/api/app/security_jwt.py:201-205` |
| JWT console read roles | `server/api/app/security_jwt.py:219-227` |
| JWT console owner roles | `server/api/app/security_jwt.py:230-236` |
| JWT home user roles | `server/api/app/security_jwt.py:239-245` |
| Stripe webhook signature | `server/api/app/routers/payments.py:135-145` |
| Zammad webhook secret | `server/api/app/routers/support.py:546-549` |
| Figma preview key | `server/api/app/routers/console.py:1579-1584` |
| License status (Bearer oder API key, manuell) | `server/api/app/routers/license.py:184-195` |
| Kein Auth-Guard (`/api/v1/health`) | `server/api/app/main.py:192-195` |

## Endpoints
| Methode | Path (`/api/v1`) | Legacy Alias | Router-Datei | Auth | Zweck | Nachweis Route | Nachweis Auth |
|---|---|---|---|---|---|---|---|
| GET | `/api/v1/admin/devices/overview` | `/admin/devices/overview` | `server/api/app/routers/admin.py` | API key | devices overview | `server/api/app/routers/admin.py:19-20` | `server/api/app/security.py:20-33` |
| DELETE | `/api/v1/admin/devices/{device_install_id}` | `/admin/devices/{device_install_id}` | `server/api/app/routers/admin.py` | API key | delete device | `server/api/app/routers/admin.py:84-85` | `server/api/app/security.py:20-33` |
| DELETE | `/api/v1/admin/devices/{device_install_id}/snapshots/{snapshot_id}` | `/admin/devices/{device_install_id}/snapshots/{snapshot_id}` | `server/api/app/routers/admin.py` | API key | delete snapshot | `server/api/app/routers/admin.py:96-97` | `server/api/app/security.py:20-33` |
| POST | `/api/v1/agent/heartbeat` | `/agent/heartbeat` | `server/api/app/routers/agent.py` | device token or API key | heartbeat | `server/api/app/routers/agent.py:128-129` | `server/api/app/security.py:35-65` |
| POST | `/api/v1/agent/inventory` | `/agent/inventory` | `server/api/app/routers/agent.py` | device token or API key | inventory | `server/api/app/routers/agent.py:143-144` | `server/api/app/security.py:35-65` |
| POST | `/api/v1/agent/register` | `/agent/register` | `server/api/app/routers/agent.py` | agent bootstrap key (legacy API key optional) | register | `server/api/app/routers/agent.py:93-95` | `server/api/app/security.py:67-100` |
| POST | `/api/v1/agent/token/rotate` | `/agent/token/rotate` | `server/api/app/routers/agent.py` | device token or API key | Revoke the current device token and issue a fresh one. | `server/api/app/routers/agent.py:162-163` | `server/api/app/security.py:35-65` |
| POST | `/api/v1/client/status` | `/client/status` | `server/api/app/routers/client.py` | device token or API key | Desktop/Updater melden ihre installierten Versionen und Update-Channel. | `server/api/app/routers/client.py:29-30` | `server/api/app/security.py:35-65` |
| GET | `/api/v1/console/devices` | `/console/devices` | `server/api/app/routers/console.py` | API key | list devices | `server/api/app/routers/console.py:355-356` | `server/api/app/security.py:20-33` |
| GET | `/api/v1/console/devices/{device_id}` | `/console/devices/{device_id}` | `server/api/app/routers/console.py` | API key | device detail | `server/api/app/routers/console.py:395-396` | `server/api/app/security.py:20-33` |
| GET | `/api/v1/console/devices/{device_id}/inventory/latest` | `/console/devices/{device_id}/inventory/latest` | `server/api/app/routers/console.py` | API key | latest inventory | `server/api/app/routers/console.py:413-414` | `server/api/app/security.py:20-33` |
| GET | `/api/v1/console/public/plans` | `/console/public/plans` | `server/api/app/routers/console.py` | none (public endpoint) | Public plan list for home portal pricing/checkout. | `server/api/app/routers/console.py:1080-1081` | `server/api/app/routers/console.py:1080-1081` |
| GET | `/api/v1/console/ui/accounts` | `/console/ui/accounts` | `server/api/app/routers/console.py` | JWT roles (console read) | ui accounts | `server/api/app/routers/console.py:554-555` | `server/api/app/security_jwt.py:219-227` |
| PATCH | `/api/v1/console/ui/accounts/{account_id}/role` | `/console/ui/accounts/{account_id}/role` | `server/api/app/routers/console.py` | JWT roles (owner/admin write) | ui update account role | `server/api/app/routers/console.py:602-603` | `server/api/app/security_jwt.py:230-236` |
| GET | `/api/v1/console/ui/activity-feed` | `/console/ui/activity-feed` | `server/api/app/routers/console.py` | JWT roles (console read) | ui activity feed | `server/api/app/routers/console.py:677-678` | `server/api/app/security_jwt.py:219-227` |
| GET | `/api/v1/console/ui/audit-log` | `/console/ui/audit-log` | `server/api/app/routers/console.py` | JWT roles (console read) | ui audit log | `server/api/app/routers/console.py:769-770` | `server/api/app/security_jwt.py:219-227` |
| GET | `/api/v1/console/ui/dashboard` | `/console/ui/dashboard` | `server/api/app/routers/console.py` | JWT roles (console read) | ui dashboard | `server/api/app/routers/console.py:626-627` | `server/api/app/security_jwt.py:219-227` |
| GET | `/api/v1/console/ui/database/hosts` | `/console/ui/database/hosts` | `server/api/app/routers/console.py` | JWT roles (console read) | ui database hosts | `server/api/app/routers/console.py:856-857` | `server/api/app/security_jwt.py:219-227` |
| GET | `/api/v1/console/ui/database/payloads` | `/console/ui/database/payloads` | `server/api/app/routers/console.py` | JWT roles (console read) | ui database payloads | `server/api/app/routers/console.py:892-893` | `server/api/app/security_jwt.py:219-227` |
| GET | `/api/v1/console/ui/devices` | `/console/ui/devices` | `server/api/app/routers/console.py` | JWT roles (console read) | ui list devices | `server/api/app/routers/console.py:436-437` | `server/api/app/security_jwt.py:219-227` |
| POST | `/api/v1/console/ui/devices/{device_id}/block` | `/console/ui/devices/{device_id}/block` | `server/api/app/routers/console.py` | JWT roles (owner/admin write) | ui block device | `server/api/app/routers/console.py:1726-1727` | `server/api/app/security_jwt.py:230-236` |
| GET | `/api/v1/console/ui/devices/{device_id}/detail` | `/console/ui/devices/{device_id}/detail` | `server/api/app/routers/console.py` | JWT roles (console read) | Full device detail including versions and online status (JWT auth). | `server/api/app/routers/console.py:1690-1691` | `server/api/app/security_jwt.py:219-227` |
| POST | `/api/v1/console/ui/devices/{device_id}/unblock` | `/console/ui/devices/{device_id}/unblock` | `server/api/app/routers/console.py` | JWT roles (owner/admin write) | ui unblock device | `server/api/app/routers/console.py:1747-1748` | `server/api/app/security_jwt.py:230-236` |
| GET | `/api/v1/console/ui/features/overrides` | `/console/ui/features/overrides` | `server/api/app/routers/features.py` | JWT roles (console read) | List all feature overrides (read: any console role). | `server/api/app/routers/features.py:108-109` | `server/api/app/security_jwt.py:219-227` |
| POST | `/api/v1/console/ui/features/overrides` | `/console/ui/features/overrides` | `server/api/app/routers/features.py` | JWT roles (owner/admin write) | Create or update a feature override (admin only). | `server/api/app/routers/features.py:120-121` | `server/api/app/security_jwt.py:230-236` |
| POST | `/api/v1/console/ui/features/{feature_key}/disable` | `/console/ui/features/{feature_key}/disable` | `server/api/app/routers/features.py` | JWT roles (owner/admin write) | Emergency kill-switch: upsert global override with enabled=False, rollout_percent=0 (admin only). | `server/api/app/routers/features.py:168-169` | `server/api/app/security_jwt.py:230-236` |
| GET | `/api/v1/console/ui/knowledge-base` | `/console/ui/knowledge-base` | `server/api/app/routers/console.py` | JWT roles (console read) | ui knowledge base | `server/api/app/routers/console.py:1549-1550` | `server/api/app/security_jwt.py:219-227` |
| GET | `/api/v1/console/ui/licenses` | `/console/ui/licenses` | `server/api/app/routers/console.py` | JWT roles (console read) | ui list licenses | `server/api/app/routers/console.py:519-520` | `server/api/app/security_jwt.py:219-227` |
| POST | `/api/v1/console/ui/licenses` | `/console/ui/licenses` | `server/api/app/routers/console.py` | JWT roles (owner/admin write) | ui create licenses | `server/api/app/routers/console.py:1147-1148` | `server/api/app/security_jwt.py:230-236` |
| POST | `/api/v1/console/ui/licenses/generate` | `/console/ui/licenses/generate` | `server/api/app/routers/console.py` | JWT roles (owner/admin write) | Generate license keys – canonical POST endpoint (replaces legacy POST /ui/licenses). | `server/api/app/routers/console.py:1778-1779` | `server/api/app/security_jwt.py:230-236` |
| PATCH | `/api/v1/console/ui/licenses/{license_key}` | `/console/ui/licenses/{license_key}` | `server/api/app/routers/console.py` | JWT roles (owner/admin write) | Patch expiry date (and optional notes) on a license. | `server/api/app/routers/console.py:1890-1891` | `server/api/app/security_jwt.py:230-236` |
| POST | `/api/v1/console/ui/licenses/{license_key}/block` | `/console/ui/licenses/{license_key}/block` | `server/api/app/routers/console.py` | JWT roles (owner/admin write) | ui block license | `server/api/app/routers/console.py:1843-1844` | `server/api/app/security_jwt.py:230-236` |
| PATCH | `/api/v1/console/ui/licenses/{license_key}/revoke` | `/console/ui/licenses/{license_key}/revoke` | `server/api/app/routers/console.py` | JWT roles (owner/admin write) | ui revoke license | `server/api/app/routers/console.py:1193-1194` | `server/api/app/security_jwt.py:230-236` |
| POST | `/api/v1/console/ui/licenses/{license_key}/revoke` | `/console/ui/licenses/{license_key}/revoke` | `server/api/app/routers/console.py` | JWT roles (owner/admin write) | Revoke a license (POST action variant; canonical endpoint). | `server/api/app/routers/console.py:1824-1825` | `server/api/app/security_jwt.py:230-236` |
| POST | `/api/v1/console/ui/licenses/{license_key}/unblock` | `/console/ui/licenses/{license_key}/unblock` | `server/api/app/routers/console.py` | JWT roles (owner/admin write) | Unblock a license using state-machine rule: | `server/api/app/routers/console.py:1863-1864` | `server/api/app/security_jwt.py:230-236` |
| GET | `/api/v1/console/ui/notifications` | `/console/ui/notifications` | `server/api/app/routers/console.py` | JWT roles (console read) | ui notifications (persisted per user in `notifications` table) | `server/api/app/routers/console.py:1215-1216` | `server/api/app/security_jwt.py:219-227` |
| POST | `/api/v1/console/ui/notifications/{notification_id}/read` | `/console/ui/notifications/{notification_id}/read` | `server/api/app/routers/console.py` | JWT roles (console read) | marks persisted notification as read (`read_at`) | `server/api/app/routers/console.py:1363-1364` | `server/api/app/security_jwt.py:219-227` |
| GET | `/api/v1/console/ui/plans` | `/console/ui/plans` | `server/api/app/routers/console.py` | JWT roles (console read) | ui list plans | `server/api/app/routers/console.py:1069-1070` | `server/api/app/security_jwt.py:219-227` |
| PUT | `/api/v1/console/ui/plans/{plan_id}` | `/console/ui/plans/{plan_id}` | `server/api/app/routers/console.py` | JWT roles (owner/admin write) | ui upsert plan | `server/api/app/routers/console.py:1095-1096` | `server/api/app/security_jwt.py:230-236` |
| GET | `/api/v1/console/ui/preview` | `/console/ui/preview` | `server/api/app/routers/console.py` | preview key header (X-Preview-Key) | Read-only data snapshot for Figma Make. Protected by X-Preview-Key header. | `server/api/app/routers/console.py:1577-1578` | `server/api/app/routers/console.py:1579-1584` |
| GET | `/api/v1/console/ui/search` | `/console/ui/search` | `server/api/app/routers/console.py` | JWT roles (console read) | ui search | `server/api/app/routers/console.py:1392-1393` | `server/api/app/security_jwt.py:219-227` |
| GET | `/api/v1/console/ui/server/containers` | `/console/ui/server/containers` | `server/api/app/routers/console.py` | JWT roles (console read) | Docker container status (requires /var/run/docker.sock mounted). | `server/api/app/routers/console.py:949-950` | `server/api/app/security_jwt.py:219-227` |
| GET | `/api/v1/console/ui/server/host` | `/console/ui/server/host` | `server/api/app/routers/console.py` | JWT roles (console read) | Host-System Metriken (CPU, RAM, Disk, Uptime). | `server/api/app/routers/console.py:1000-1001` | `server/api/app/security_jwt.py:219-227` |
| GET | `/api/v1/console/ui/server/services` | `/console/ui/server/services` | `server/api/app/routers/console.py` | JWT roles (console read) | ui server services | `server/api/app/routers/console.py:1507-1508` | `server/api/app/security_jwt.py:219-227` |
| GET | `/api/v1/console/ui/telemetry` | `/console/ui/telemetry` | `server/api/app/routers/console.py` | JWT roles (console read) | ui list telemetry | `server/api/app/routers/console.py:479-480` | `server/api/app/security_jwt.py:219-227` |
| GET | `/api/v1/console/ui/telemetry/chart` | `/console/ui/telemetry/chart` | `server/api/app/routers/console.py` | JWT roles (console read) | ui telemetry chart | `server/api/app/routers/console.py:1459-1460` | `server/api/app/security_jwt.py:219-227` |
| GET | `/api/v1/health` | `/health` | `server/api/app/main.py` | none | health | `server/api/app/main.py:193-194` | `server/api/app/main.py:192-195` |
| POST | `/api/v1/license/activate` | `/license/activate` | `server/api/app/routers/license.py` | API key | activate license | `server/api/app/routers/license.py:70-71` | `server/api/app/security.py:20-33` |
| GET | `/api/v1/license/me` | `/license/me` | `server/api/app/routers/license.py` | JWT (verified bearer) | license me | `server/api/app/routers/license.py:126-127` | `server/api/app/security_jwt.py:201-205` |
| GET | `/api/v1/license/status` | `/license/status` | `server/api/app/routers/license.py` | JWT bearer OR API key (manual branch) | Returns the current license status including plan details and feature flags. | `server/api/app/routers/license.py:163-164` | `server/api/app/routers/license.py:184-195` |
| POST | `/api/v1/payments/create-checkout` | `/payments/create-checkout` | `server/api/app/routers/payments.py` | JWT roles (authenticated home user) | Create a Stripe Checkout Session for a given plan. | `server/api/app/routers/payments.py:61-62` | `server/api/app/security_jwt.py:239-245` |
| POST | `/api/v1/payments/portal` | `/payments/portal` | `server/api/app/routers/payments.py` | JWT roles (authenticated home user) | Create a Stripe Customer Portal session for billing management. | `server/api/app/routers/payments.py:101-102` | `server/api/app/security_jwt.py:239-245` |
| POST | `/api/v1/payments/webhook` | `/payments/webhook` | `server/api/app/routers/payments.py` | Stripe signature header | Handle Stripe webhook events. | `server/api/app/routers/payments.py:128-130` | `server/api/app/routers/payments.py:135-145` |
| GET | `/api/v1/notifications` | `/notifications` | `server/api/app/routers/notifications.py` | JWT roles (authenticated home user) | list notifications | `server/api/app/routers/notifications.py:28-29` | `server/api/app/security_jwt.py:239-245` |
| POST | `/api/v1/notifications/{notification_id}/read` | `/notifications/{notification_id}/read` | `server/api/app/routers/notifications.py` | JWT roles (authenticated home user) | mark one notification as read | `server/api/app/routers/notifications.py:69-70` | `server/api/app/security_jwt.py:239-245` |
| POST | `/api/v1/notifications/read-all` | `/notifications/read-all` | `server/api/app/routers/notifications.py` | JWT roles (authenticated home user) | mark all notifications as read | `server/api/app/routers/notifications.py:98-99` | `server/api/app/security_jwt.py:239-245` |
| GET | `/api/v1/support/admin/diag/zammad-roles` | `/support/admin/diag/zammad-roles` | `server/api/app/routers/support.py` | JWT roles (owner/admin write) | Admin-only: list all Zammad roles (id, name, active). | `server/api/app/routers/support.py:450-451` | `server/api/app/security_jwt.py:230-236` |
| GET | `/api/v1/support/admin/diag/zammad-user` | `/support/admin/diag/zammad-user` | `server/api/app/routers/support.py` | JWT roles (owner/admin write) | Admin-only: search Zammad for a user by email. | `server/api/app/routers/support.py:493-494` | `server/api/app/security_jwt.py:230-236` |
| POST | `/api/v1/support/attachments` | `/support/attachments` | `server/api/app/routers/support.py` | JWT roles (authenticated home user) | Upload helper returns base64 attachment payload for Zammad ticket articles. | `server/api/app/routers/support.py:422-423` | `server/api/app/security_jwt.py:239-245` |
| GET | `/api/v1/support/tickets` | `/support/tickets` | `server/api/app/routers/support.py` | JWT roles (authenticated home user) | List tickets. | `server/api/app/routers/support.py:229-230` | `server/api/app/security_jwt.py:239-245` |
| POST | `/api/v1/support/tickets` | `/support/tickets` | `server/api/app/routers/support.py` | JWT roles (authenticated home user) | Create a support ticket. | `server/api/app/routers/support.py:280-281` | `server/api/app/security_jwt.py:239-245` |
| GET | `/api/v1/support/tickets/{ticket_id}` | `/support/tickets/{ticket_id}` | `server/api/app/routers/support.py` | JWT roles (authenticated home user) | Get a single ticket. | `server/api/app/routers/support.py:319-320` | `server/api/app/security_jwt.py:239-245` |
| POST | `/api/v1/support/tickets/{ticket_id}/reply` | `/support/tickets/{ticket_id}/reply` | `server/api/app/routers/support.py` | JWT roles (authenticated home user) | Reply to ticket via Zammad `ticket_articles` (supports attachments). | `server/api/app/routers/support.py:364-365` | `server/api/app/security_jwt.py:239-245` |
| POST | `/api/v1/support/webhook` | `/support/webhook` | `server/api/app/routers/support.py` | shared secret header (X-Zammad-Secret) | Inbound webhook from Zammad. Verified via shared secret. | `server/api/app/routers/support.py:541-542` | `server/api/app/routers/support.py:546-549` |
| POST | `/api/v1/telemetry/snapshot` | `/telemetry/snapshot` | `server/api/app/routers/telemetry.py` | API key | ingest snapshot | `server/api/app/routers/telemetry.py:61-62` | `server/api/app/security.py:20-33` |
| POST | `/api/v1/telemetry/update` | `/telemetry/update` | `server/api/app/routers/telemetry.py` | API key | ingest update | `server/api/app/routers/telemetry.py:32-33` | `server/api/app/security.py:20-33` |

## Hinweis Legacy-Pfade
- Alle Router (`agent`, `client`, `console`, `features`, `telemetry`, `admin`, `license`, `payments`, `support`, `notifications`) sind zusätzlich ohne `/api/v1` eingebunden (`server/api/app/main.py:218-227`).
- Legacy-Requests erhalten Deprecation/Sunset/Link Header (`server/api/app/main.py:115-166`).
