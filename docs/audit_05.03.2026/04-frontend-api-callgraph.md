# Audit D) Frontend ↔ API Call Graph

## 1) Service-Funktionen → API URLs

| App | Service-Funktion | Method | API URL (raw) | Canonical Backend Path (abgeleitet) | Service Datei |
|---|---|---|---|---|---|
| console | blockDevice | POST | /console/ui/devices/${encodeURIComponent(deviceId)}/block | /api/v1/console/ui/devices/${encodeURIComponent(deviceId)}/block | server/console/src/app/services/api-service.ts |
| console | blockLicense | POST | /console/ui/licenses/${encodeURIComponent(key)}/block | /api/v1/console/ui/licenses/${encodeURIComponent(key)}/block | server/console/src/app/services/api-service.ts |
| console | disableFeature | POST | /console/ui/features/${encodeURIComponent(featureKey)}/disable | /api/v1/console/ui/features/${encodeURIComponent(featureKey)}/disable | server/console/src/app/services/api-service.ts |
| console | generateLicenses | POST | /console/ui/licenses/generate | /api/v1/console/ui/licenses/generate | server/console/src/app/services/api-service.ts |
| console | getAccounts | GET | /console/ui/accounts?${q} | /api/v1/console/ui/accounts?${q} | server/console/src/app/services/api-service.ts |
| console | getAuditLog | GET | /console/ui/audit-log?${q} | /api/v1/console/ui/audit-log?${q} | server/console/src/app/services/api-service.ts |
| console | getContainers | GET | /console/ui/server/containers | /api/v1/console/ui/server/containers | server/console/src/app/services/api-service.ts |
| console | getDashboard | GET | /console/ui/dashboard | /api/v1/console/ui/dashboard | server/console/src/app/services/api-service.ts |
| console | getDeviceDetail | GET | /console/ui/devices/${encodeURIComponent(deviceId)}/detail | /api/v1/console/ui/devices/${encodeURIComponent(deviceId)}/detail | server/console/src/app/services/api-service.ts |
| console | getDevices | GET | /console/ui/devices?${q} | /api/v1/console/ui/devices?${q} | server/console/src/app/services/api-service.ts |
| console | getFeatureOverrides | GET | /console/ui/features/overrides | /api/v1/console/ui/features/overrides | server/console/src/app/services/api-service.ts |
| console | getHostInfo | GET | /console/ui/server/host | /api/v1/console/ui/server/host | server/console/src/app/services/api-service.ts |
| console | getLicenses | GET | /console/ui/licenses?${q} | /api/v1/console/ui/licenses?${q} | server/console/src/app/services/api-service.ts |
| console | getNotifications | GET | /console/ui/notifications | /api/v1/console/ui/notifications | server/console/src/app/services/api-service.ts |
| console | getPlans | GET | /console/ui/plans | /api/v1/console/ui/plans | server/console/src/app/services/api-service.ts |
| console | getTelemetry | GET | /console/ui/telemetry?${q} | /api/v1/console/ui/telemetry?${q} | server/console/src/app/services/api-service.ts |
| console | getTelemetryChart | GET | /console/ui/telemetry/chart?category=${category}&hours=${hours} | /api/v1/console/ui/telemetry/chart?category=${category}&hours=${hours} | server/console/src/app/services/api-service.ts |
| console | markNotificationRead | POST | /console/ui/notifications/${encodeURIComponent(id)}/read | /api/v1/console/ui/notifications/${encodeURIComponent(id)}/read | server/console/src/app/services/api-service.ts |
| console | patchLicense | PATCH | /console/ui/licenses/${encodeURIComponent(key)} | /api/v1/console/ui/licenses/${encodeURIComponent(key)} | server/console/src/app/services/api-service.ts |
| console | revokeLicense | POST | /console/ui/licenses/${encodeURIComponent(key)}/revoke | /api/v1/console/ui/licenses/${encodeURIComponent(key)}/revoke | server/console/src/app/services/api-service.ts |
| console | search | GET | /console/ui/search?q=${encodeURIComponent(q)} | /api/v1/console/ui/search?q=${encodeURIComponent(q)} | server/console/src/app/services/api-service.ts |
| console | unblockDevice | POST | /console/ui/devices/${encodeURIComponent(deviceId)}/unblock | /api/v1/console/ui/devices/${encodeURIComponent(deviceId)}/unblock | server/console/src/app/services/api-service.ts |
| console | unblockLicense | POST | /console/ui/licenses/${encodeURIComponent(key)}/unblock | /api/v1/console/ui/licenses/${encodeURIComponent(key)}/unblock | server/console/src/app/services/api-service.ts |
| console | updateAccountRole | PATCH | /console/ui/accounts/${accountId}/role | /api/v1/console/ui/accounts/${accountId}/role | server/console/src/app/services/api-service.ts |
| console | upsertFeatureOverride | POST | /console/ui/features/overrides | /api/v1/console/ui/features/overrides | server/console/src/app/services/api-service.ts |
| console | upsertPlan | PUT | /console/ui/plans/${planId} | /api/v1/console/ui/plans/${planId} | server/console/src/app/services/api-service.ts |
| home | getLicenseStatus | GET | /license/status | /api/v1/license/status | server/home/src/lib/api.ts |
| home | getPlans | GET | /console/public/plans | /api/v1/console/public/plans | server/home/src/lib/api.ts |

## 2) Service-Funktionen → aufrufende Seiten

| App | Page Route | Page File | Service-Funktion | Method | Canonical Path | Quelle |
|---|---|---|---|---|---|---|
| console | /accounts | server/console/src/app/pages/AccountsPage.tsx | getAccounts | GET | /api/v1/console/ui/accounts?${q} | service |
| console | /accounts | server/console/src/app/pages/AccountsPage.tsx | updateAccountRole | PATCH | /api/v1/console/ui/accounts/${accountId}/role | service |
| console | /audit | server/console/src/app/pages/AuditLogPage.tsx | getAuditLog | GET | /api/v1/console/ui/audit-log?${q} | service |
| console | * | / | server/console/src/app/pages/DashboardPage.tsx | getDashboard | GET | /api/v1/console/ui/dashboard | service |
| console | /devices/:deviceId | server/console/src/app/pages/DeviceDetailPage.tsx | blockDevice | POST | /api/v1/console/ui/devices/${encodeURIComponent(deviceId)}/block | service |
| console | /devices/:deviceId | server/console/src/app/pages/DeviceDetailPage.tsx | getDeviceDetail | GET | /api/v1/console/ui/devices/${encodeURIComponent(deviceId)}/detail | service |
| console | /devices/:deviceId | server/console/src/app/pages/DeviceDetailPage.tsx | unblockDevice | POST | /api/v1/console/ui/devices/${encodeURIComponent(deviceId)}/unblock | service |
| console | /devices | server/console/src/app/pages/DevicesPage.tsx | getDevices | GET | /api/v1/console/ui/devices?${q} | service |
| console | /features | server/console/src/app/pages/FeatureRolloutsPage.tsx | disableFeature | POST | /api/v1/console/ui/features/${encodeURIComponent(featureKey)}/disable | service |
| console | /features | server/console/src/app/pages/FeatureRolloutsPage.tsx | getFeatureOverrides | GET | /api/v1/console/ui/features/overrides | service |
| console | /features | server/console/src/app/pages/FeatureRolloutsPage.tsx | upsertFeatureOverride | POST | /api/v1/console/ui/features/overrides | service |
| console | /licenses | server/console/src/app/pages/LicensesPage.tsx | blockLicense | POST | /api/v1/console/ui/licenses/${encodeURIComponent(key)}/block | service |
| console | /licenses | server/console/src/app/pages/LicensesPage.tsx | generateLicenses | POST | /api/v1/console/ui/licenses/generate | service |
| console | /licenses | server/console/src/app/pages/LicensesPage.tsx | getLicenses | GET | /api/v1/console/ui/licenses?${q} | service |
| console | /licenses | server/console/src/app/pages/LicensesPage.tsx | revokeLicense | POST | /api/v1/console/ui/licenses/${encodeURIComponent(key)}/revoke | service |
| console | /licenses | server/console/src/app/pages/LicensesPage.tsx | unblockLicense | POST | /api/v1/console/ui/licenses/${encodeURIComponent(key)}/unblock | service |
| console | /notifications | server/console/src/app/pages/NotificationsPage.tsx | getNotifications | GET | /api/v1/console/ui/notifications | service |
| console | /notifications | server/console/src/app/pages/NotificationsPage.tsx | markNotificationRead | POST | /api/v1/console/ui/notifications/${encodeURIComponent(id)}/read | service |
| console | /plans | server/console/src/app/pages/PlansPage.tsx | getPlans | GET | /api/v1/console/ui/plans | service |
| console | /plans | server/console/src/app/pages/PlansPage.tsx | upsertPlan | PUT | /api/v1/console/ui/plans/${planId} | service |
| console | /server | server/console/src/app/pages/ServerPage.tsx | getContainers | GET | /api/v1/console/ui/server/containers | service |
| console | /server | server/console/src/app/pages/ServerPage.tsx | getHostInfo | GET | /api/v1/console/ui/server/host | service |
| console | /telemetry | server/console/src/app/pages/TelemetryPage.tsx | getTelemetry | GET | /api/v1/console/ui/telemetry?${q} | service |
| console | /telemetry | server/console/src/app/pages/TelemetryPage.tsx | getTelemetryChart | GET | /api/v1/console/ui/telemetry/chart?category=${category}&hours=${hours} | service |
| home | /account/billing | server/home/src/app/account/billing/page.tsx | LOCAL:/api/portal | n/a | n/a | local-or-unknown |
| home | /account | server/home/src/app/account/page.tsx | getLicenseStatus | GET | /api/v1/license/status | service |
| home | unknown | server/home/src/app/api/checkout/route.ts | getPlans | GET | /api/v1/console/public/plans | service |
| home | unknown | server/home/src/app/api/license-status/route.ts | getLicenseStatus | GET | /api/v1/license/status | service |
| home | unknown | server/home/src/components/PricingTable.tsx | LOCAL:/api/checkout | n/a | n/a | local-or-unknown |
| home | unknown | server/home/src/lib/api.ts | getLicenseStatus | GET | /api/v1/license/status | service |
| home | unknown | server/home/src/lib/api.ts | getPlans | GET | /api/v1/console/public/plans | service |

## 3) Dead Calls / Dead Endpoints

### Dead Calls (Frontend call existiert, Endpoint fehlt)

- Keine Dead Calls gefunden (0).

### Dead Endpoints (Backend Endpoint existiert, aber kein Frontend-Aufruf)

- Anzahl: 27

| Method | Canonical Path | Handler | Hinweis |
|---|---|---|---|
| DELETE | /api/v1/admin/devices/{device_install_id} | admin.delete_device | API-Key Admin-Schnittstelle |
| DELETE | /api/v1/admin/devices/{device_install_id}/snapshots/{snapshot_id} | admin.delete_snapshot | API-Key Admin-Schnittstelle |
| GET | /api/v1/admin/devices/overview | admin.devices_overview | API-Key Admin-Schnittstelle |
| POST | /api/v1/agent/heartbeat | agent.heartbeat | Agent/Service ingest (nicht Web-Frontend) |
| POST | /api/v1/agent/inventory | agent.inventory | Agent/Service ingest (nicht Web-Frontend) |
| POST | /api/v1/agent/register | agent.register | Agent/Service ingest (nicht Web-Frontend) |
| POST | /api/v1/agent/token/rotate | agent.rotate_token | Agent/Service ingest (nicht Web-Frontend) |
| GET | /api/v1/console/devices | console.list_devices | unbekannt |
| GET | /api/v1/console/devices/{device_id} | console.device_detail | unbekannt |
| GET | /api/v1/console/devices/{device_id}/inventory/latest | console.latest_inventory | unbekannt |
| GET | /api/v1/console/ui/activity-feed | console.ui_activity_feed | UI-Reserve/noch nicht verdrahtet |
| GET | /api/v1/console/ui/database/hosts | console.ui_database_hosts | UI-Reserve/noch nicht verdrahtet |
| GET | /api/v1/console/ui/database/payloads | console.ui_database_payloads | UI-Reserve/noch nicht verdrahtet |
| GET | /api/v1/console/ui/knowledge-base | console.ui_knowledge_base | UI-Reserve/noch nicht verdrahtet |
| PATCH | /api/v1/console/ui/licenses/{license_key} | console.ui_patch_license | UI-Reserve/noch nicht verdrahtet |
| GET | /api/v1/console/ui/preview | console.ui_preview | UI-Reserve/noch nicht verdrahtet |
| GET | /api/v1/console/ui/search | console.ui_search | UI-Reserve/noch nicht verdrahtet |
| GET | /api/v1/console/ui/server/services | console.ui_server_services | UI-Reserve/noch nicht verdrahtet |
| GET | /api/v1/health | main.health | Infra/Monitoring |
| POST | /api/v1/license/activate | license.activate_license | unbekannt |
| GET | /api/v1/license/me | license.license_me | unbekannt |
| POST | /api/v1/payments/create-checkout | payments.create_checkout | Stripe/Server-to-server oder Home-intern |
| POST | /api/v1/payments/portal | payments.customer_portal | Stripe/Server-to-server oder Home-intern |
| POST | /api/v1/payments/webhook | payments.stripe_webhook | Stripe/Server-to-server oder Home-intern |
| POST | /api/v1/telemetry/snapshot | telemetry.ingest_snapshot | Agent/Service ingest (nicht Web-Frontend) |
| POST | /api/v1/telemetry/update | telemetry.ingest_update | Agent/Service ingest (nicht Web-Frontend) |
| GET | /health | main.health | Infra/Monitoring |

## 4) Interpretation
- Der Abgleich zeigt konsistente Frontend-Calls zu bestehenden Endpoints (0 Dead Calls).
- Mehrere Endpoints sind aktuell nicht von Web-Frontends verdrahtet (z. B. Agent/Admin/Monitoring/Reserve-UI), was als bewusstes API-Superset interpretiert werden kann.
- Home nutzt teils lokale Next.js-Routen (`/api/checkout`, `/api/portal`) als Server-Fassade; dadurch sind manche Backend-Endpoints nicht direkt im Browser-Callgraph sichtbar.
