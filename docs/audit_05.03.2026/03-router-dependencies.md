# Audit C) Router Dependencies Matrix

## Router-Ebene

| Router-Datei | Prefix | Tags | Router-Dependencies | Endpoint Count | Endpoint-Dependency-Set |
|---|---|---|---|---:|---|
| server/api/app/routers/admin.py | /admin | admin | require_api_key | 3 | get_db |
| server/api/app/routers/agent.py | /agent | agent |  | 4 | get_db, require_agent_auth, require_api_key |
| server/api/app/routers/console.py | /console | console |  | 35 | get_db, require_api_key, require_console_owner, require_console_user |
| server/api/app/routers/features.py | /console | features |  | 3 | get_db, require_console_owner, require_console_user |
| server/api/app/routers/license.py | /license | license |  | 3 | get_db, require_api_key, require_verified_token |
| server/api/app/routers/payments.py | /payments | payments |  | 3 | get_db, require_home_user |
| server/api/app/routers/telemetry.py | /telemetry | telemetry | require_api_key | 2 | get_db |
| server/api/app/main.py |  | health |  | 2 |  |

## Endpoint-Ebene (Depends pro Endpoint)

| Canonical Path | Method | Handler | Router Dependencies | Endpoint Dependencies | Effektiv (Union) |
|---|---|---|---|---|---|
| /api/v1/admin/devices/{device_install_id} | DELETE | admin.delete_device | require_api_key | get_db | require_api_keyget_db |
| /api/v1/admin/devices/{device_install_id}/snapshots/{snapshot_id} | DELETE | admin.delete_snapshot | require_api_key | get_db | require_api_keyget_db |
| /api/v1/admin/devices/overview | GET | admin.devices_overview | require_api_key | get_db | require_api_keyget_db |
| /api/v1/agent/heartbeat | POST | agent.heartbeat | (none) | get_db, require_agent_auth | get_db, require_agent_auth |
| /api/v1/agent/inventory | POST | agent.inventory | (none) | get_db, require_agent_auth | get_db, require_agent_auth |
| /api/v1/agent/register | POST | agent.register | (none) | get_db, require_agent_auth, require_api_key | get_db, require_agent_auth, require_api_key |
| /api/v1/agent/token/rotate | POST | agent.rotate_token | (none) | get_db, require_agent_auth | get_db, require_agent_auth |
| /api/v1/console/devices | GET | console.list_devices | (none) | get_db, require_api_key | get_db, require_api_key |
| /api/v1/console/devices/{device_id} | GET | console.device_detail | (none) | get_db, require_api_key | get_db, require_api_key |
| /api/v1/console/devices/{device_id}/inventory/latest | GET | console.latest_inventory | (none) | get_db, require_api_key, require_console_user | get_db, require_api_key, require_console_user |
| /api/v1/console/public/plans | GET | console.public_list_plans | (none) | get_db | get_db |
| /api/v1/console/ui/accounts | GET | console.ui_accounts | (none) | require_console_user | require_console_user |
| /api/v1/console/ui/accounts/{account_id}/role | PATCH | console.ui_update_account_role | (none) | require_console_owner | require_console_owner |
| /api/v1/console/ui/activity-feed | GET | console.ui_activity_feed | (none) | get_db, require_console_user | get_db, require_console_user |
| /api/v1/console/ui/audit-log | GET | console.ui_audit_log | (none) | get_db, require_console_user | get_db, require_console_user |
| /api/v1/console/ui/dashboard | GET | console.ui_dashboard | (none) | get_db, require_console_user | get_db, require_console_user |
| /api/v1/console/ui/database/hosts | GET | console.ui_database_hosts | (none) | get_db, require_console_user | get_db, require_console_user |
| /api/v1/console/ui/database/payloads | GET | console.ui_database_payloads | (none) | get_db, require_console_user | get_db, require_console_user |
| /api/v1/console/ui/devices | GET | console.ui_list_devices | (none) | get_db, require_console_user | get_db, require_console_user |
| /api/v1/console/ui/devices/{device_id}/block | POST | console.ui_block_device | (none) | get_db, require_console_owner | get_db, require_console_owner |
| /api/v1/console/ui/devices/{device_id}/detail | GET | console.ui_device_detail | (none) | get_db, require_console_user | get_db, require_console_user |
| /api/v1/console/ui/devices/{device_id}/unblock | POST | console.ui_unblock_device | (none) | get_db, require_console_owner | get_db, require_console_owner |
| /api/v1/console/ui/knowledge-base | GET | console.ui_knowledge_base | (none) | require_console_user | require_console_user |
| /api/v1/console/ui/licenses | GET | console.ui_list_licenses | (none) | get_db, require_console_user | get_db, require_console_user |
| /api/v1/console/ui/licenses | POST | console.ui_create_licenses | (none) | get_db, require_console_owner | get_db, require_console_owner |
| /api/v1/console/ui/licenses/{license_key} | PATCH | console.ui_patch_license | (none) | get_db, require_console_owner | get_db, require_console_owner |
| /api/v1/console/ui/licenses/{license_key}/block | POST | console.ui_block_license | (none) | get_db, require_console_owner | get_db, require_console_owner |
| /api/v1/console/ui/licenses/{license_key}/revoke | PATCH | console.ui_revoke_license | (none) | get_db, require_console_owner | get_db, require_console_owner |
| /api/v1/console/ui/licenses/{license_key}/revoke | POST | console.ui_revoke_license_post | (none) | get_db, require_console_owner | get_db, require_console_owner |
| /api/v1/console/ui/licenses/{license_key}/unblock | POST | console.ui_unblock_license | (none) | get_db, require_console_owner | get_db, require_console_owner |
| /api/v1/console/ui/licenses/generate | POST | console.ui_generate_licenses | (none) | get_db, require_console_owner | get_db, require_console_owner |
| /api/v1/console/ui/notifications | GET | console.ui_notifications | (none) | get_db, require_console_user | get_db, require_console_user |
| /api/v1/console/ui/notifications/{notification_id}/read | POST | console.ui_notification_mark_read | (none) | require_console_user | require_console_user |
| /api/v1/console/ui/plans | GET | console.ui_list_plans | (none) | get_db, require_console_user | get_db, require_console_user |
| /api/v1/console/ui/plans/{plan_id} | PUT | console.ui_upsert_plan | (none) | get_db, require_console_owner | get_db, require_console_owner |
| /api/v1/console/ui/preview | GET | console.ui_preview | (none) | get_db | get_db |
| /api/v1/console/ui/search | GET | console.ui_search | (none) | get_db, require_console_user | get_db, require_console_user |
| /api/v1/console/ui/server/containers | GET | console.ui_server_containers | (none) | require_console_user | require_console_user |
| /api/v1/console/ui/server/host | GET | console.ui_server_host | (none) | require_console_user | require_console_user |
| /api/v1/console/ui/server/services | GET | console.ui_server_services | (none) | require_console_user | require_console_user |
| /api/v1/console/ui/telemetry | GET | console.ui_list_telemetry | (none) | get_db, require_console_user | get_db, require_console_user |
| /api/v1/console/ui/telemetry/chart | GET | console.ui_telemetry_chart | (none) | get_db, require_console_user | get_db, require_console_user |
| /api/v1/console/ui/features/{feature_key}/disable | POST | features.disable_feature | (none) | get_db, require_console_owner | get_db, require_console_owner |
| /api/v1/console/ui/features/overrides | GET | features.list_feature_overrides | (none) | get_db, require_console_user | get_db, require_console_user |
| /api/v1/console/ui/features/overrides | POST | features.upsert_feature_override | (none) | get_db, require_console_owner | get_db, require_console_owner |
| /api/v1/license/activate | POST | license.activate_license | (none) | get_db, require_api_key | get_db, require_api_key |
| /api/v1/license/me | GET | license.license_me | (none) | get_db, require_verified_token | get_db, require_verified_token |
| /api/v1/license/status | GET | license.license_status | (none) | get_db | get_db |
| /api/v1/payments/create-checkout | POST | payments.create_checkout | (none) | get_db, require_home_user | get_db, require_home_user |
| /api/v1/payments/portal | POST | payments.customer_portal | (none) | get_db, require_home_user | get_db, require_home_user |
| /api/v1/payments/webhook | POST | payments.stripe_webhook | (none) | get_db | get_db |
| /api/v1/telemetry/snapshot | POST | telemetry.ingest_snapshot | require_api_key | get_db | require_api_keyget_db |
| /api/v1/telemetry/update | POST | telemetry.ingest_update | require_api_key | get_db | require_api_keyget_db |
| /api/v1/health | GET | main.health | (none) | (none) | (none) |
| /health | GET | main.health | (none) | (none) | (none) |

## Kurzfazit
- Router-Level `dependencies=[Depends(...)]` wird nur in `admin.py` und `telemetry.py` genutzt (API-Key global am Router).
- Console-/Home-JWT-Schutz wird überwiegend endpoint-spezifisch via `Depends(require_console_user|owner|home_user|verified_token)` umgesetzt.
- `payments.webhook` hat bewusst keine Auth-Dependency; Schutz erfolgt über Stripe-Signaturprüfung im Handler.
