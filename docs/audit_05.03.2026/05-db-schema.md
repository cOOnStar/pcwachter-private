# Audit E1) Datenbank IST (Schema)

## Methode / Nachweis
- ORM-Quelle: `server/api/app/models.py` (`Plan` bis `FeatureOverride`, Zeilen 11–220).
- Runtime-Schema (live) per `psql` aus `pcw-postgres`.
- Inventar-Dateien:
  - `docs/audit/_generated/db_tables.csv`
  - `docs/audit/_generated/db_columns.csv`
  - `docs/audit/_generated/db_relationships.csv`

## Scope
- Die DB `pcwaechter` enthält **PCW-Tabellen + Keycloak-Tabellen** (runtime: 100 Tabellen insgesamt).
- Dieses Kapitel bewertet das **PCW-Applikationsschema** (9 Tabellen aus `models.py`).
- Keycloak-Interntabellen sind für dieses Kapitel out-of-scope.

## Tabellen-Inventory (PCW-Applikation)

| Tabelle | Spalten (name:type) | PK/FK/UNIQUE/CHECK | Indizes (Auszug) |
|---|---|---|---|
| `plans` | `id:string`, `label:string`, `price_eur:float`, `duration_days:int`, `max_devices:int`, `is_active:bool`, `sort_order:int`, `feature_flags:jsonb`, `grace_period_days:int`, `stripe_price_id:string`, `created_at:timestamptz`, `updated_at:timestamptz` | PK:`plans_pkey(id)` | `plans_pkey` |
| `devices` | `id:uuid`, `device_install_id:string`, `host_name:string`, `os_name:string`, `os_version:string`, `os_build:string`, `agent_version:string`, `agent_channel:string`, `primary_ip:string`, `macs:jsonb`, `last_seen_at:timestamptz`, `created_at:timestamptz`, `blocked:bool` | PK:`devices_pkey(id)`, UNIQUE:`devices_device_install_id_key(device_install_id)` | `ix_devices_host_name`, `ix_devices_last_seen_at` |
| `device_inventory` | `id:uuid`, `device_id:uuid`, `collected_at:timestamptz`, `payload:jsonb`, `created_at:timestamptz` | PK:`device_inventory_pkey(id)`, FK:`device_inventory_device_id_fkey(device_id->devices.id)`, UNIQUE:`uq_device_inventory_device_collected(device_id,collected_at)` | `ix_device_inventory_device_id` |
| `telemetry_snapshots` | `id:uuid`, `device_id:uuid`, `received_at:timestamptz`, `category:string`, `payload:jsonb`, `summary:string`, `source:string` | PK:`telemetry_snapshots_pkey(id)`, FK:`telemetry_snapshots_device_id_fkey(device_id->devices.id)` | `ix_telemetry_snapshots_device_id`, `ix_telemetry_snapshots_received_at`, `ix_telemetry_snapshots_category`, `ix_telemetry_snapshots_device_category_received_desc` |
| `licenses` | `id:uuid`, `license_key:string`, `tier:string`, `duration_days:int`, `state:string`, `issued_at:timestamptz`, `activated_at:timestamptz`, `expires_at:timestamptz`, `activated_device_install_id:string`, `activated_by_user_id:string`, `created_at:timestamptz`, `updated_at:timestamptz` | PK:`licenses_pkey(id)` | `ix_licenses_license_key`, `ix_licenses_state`, `ix_licenses_expires_at`, `ix_licenses_activated_device_install_id` |
| `subscriptions` | `id:uuid`, `keycloak_user_id:string`, `license_id:uuid`, `plan_id:string`, `status:string`, `stripe_customer_id:string`, `stripe_subscription_id:string`, `stripe_price_id:string`, `current_period_start:timestamptz`, `current_period_end:timestamptz`, `grace_until:timestamptz`, `trial_used:bool`, `created_at:timestamptz`, `updated_at:timestamptz` | PK:`subscriptions_pkey(id)`, FK:`subscriptions_license_id_fkey(license_id->licenses.id)`, FK:`subscriptions_plan_id_fkey(plan_id->plans.id)` | `ix_subscriptions_keycloak_user_id`, `ix_subscriptions_status`, `ix_subscriptions_stripe_customer_id`, `ix_subscriptions_stripe_subscription_id` |
| `device_tokens` | `id:uuid`, `device_install_id:string`, `token_hash:string`, `expires_at:timestamptz`, `created_at:timestamptz`, `last_used_at:timestamptz`, `revoked_at:timestamptz` | PK:`device_tokens_pkey(id)`, FK:`device_tokens_device_install_id_fkey(device_install_id->devices.device_install_id)`, UNIQUE:`device_tokens_token_hash_key(token_hash)` | `ix_device_tokens_device_install_id`, `ix_device_tokens_token_hash` |
| `webhook_events` | `id:uuid`, `event_type:string`, `stripe_event_id:string`, `payload:jsonb`, `processed_at:timestamptz`, `created_at:timestamptz` | PK:`webhook_events_pkey(id)`, UNIQUE:`webhook_events_stripe_event_id_key(stripe_event_id)` | `ix_webhook_events_event_type` |
| `feature_overrides` | `id:uuid`, `feature_key:string`, `enabled:bool`, `rollout_percent:int`, `scope:string`, `target_id:string`, `version_min:string`, `platform:string`, `notes:text`, `updated_at:timestamptz` | PK:`feature_overrides_pkey(id)`, CHECK:`ck_feature_override_scope_target` | `ix_fo_scope_target`, UNIQUE:`uq_fo_key_scope_target(feature_key,scope,coalesce(target_id,''))` |

## ERD (Textform)
- `devices (1) -> (n) telemetry_snapshots` via `telemetry_snapshots.device_id`.
- `devices (1) -> (n) device_inventory` via `device_inventory.device_id`.
- `devices.device_install_id (1) -> (n) device_tokens.device_install_id`.
- `plans (1) -> (n) subscriptions` via `subscriptions.plan_id`.
- `licenses (1) -> (n) subscriptions` via `subscriptions.license_id`.

## Konsistenzcheck: Models vs Runtime-Schema

| Check | Ergebnis | Nachweis |
|---|---|---|
| 9 ORM-Tabellen vorhanden | OK | `models.py` Klassen + runtime `\dt`/`information_schema` |
| `device_tokens.last_used_at` vorhanden | OK | `information_schema.columns` Query (runtime) |
| `webhook_events.stripe_event_id` UNIQUE | OK | `webhook_events_stripe_event_id_key` |
| `feature_overrides.feature_key` als single-column UNIQUE | **Nicht vorhanden** (stattdessen Composite UNIQUE) | `uq_fo_key_scope_target(feature_key,scope,coalesce(target_id,''))` |
| Redundante Unique-Indizes (`ix_feature_overrides_feature_key`, `ix_webhook_events_stripe_event_id`) entfernt | OK | `pg_indexes` (nur aktuelle Indexliste sichtbar) |

## Roh-Nachweis (Runtime-Kommandos)

```bash
docker exec pcw-postgres psql -U pcwaechter -d pcwaechter -c \
  "SELECT table_name, column_name, data_type, is_nullable
   FROM information_schema.columns
   WHERE table_schema='public'
     AND table_name IN ('device_tokens','webhook_events','feature_overrides')
   ORDER BY table_name, ordinal_position;"
```

```bash
docker exec pcw-postgres psql -U pcwaechter -d pcwaechter -c \
  "SELECT tablename, indexname
   FROM pg_indexes
   WHERE schemaname='public'
     AND tablename IN ('device_tokens','webhook_events','feature_overrides')
   ORDER BY tablename, indexname;"
```
