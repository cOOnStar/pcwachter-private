# DB Inventory (SQLAlchemy + Alembic + Bootstrap)

## Quellen
- SQLAlchemy-Modelle: `server/api/app/models.py`.
- Alembic-Chain: `server/api/alembic/versions/*.py`.
- Greenfield-Bootstrap: `server/api/alembic/bootstrap/0000_create_devices_and_inventory.sql`.
- Tabellennamen-Command: `rg -n "__tablename__" server/api/app/models.py`.

## Tabellen/Modelle (IST)

| Tabelle | SQLAlchemy Model | Erzeugung / Schema-Herkunft | Nachweis |
|---|---|---|---|
| `plans` | `Plan` | `op.create_table("plans")`, später Spalten-Erweiterung (`feature_flags`, `grace_period_days`, `stripe_price_id`) | `server/api/app/models.py:11-33`, `server/api/alembic/versions/20260304_0003_add_plans.py:19-31`, `server/api/alembic/versions/20260304_0004_add_subscriptions_and_features.py:21-24` |
| `devices` | `Device` | In Alembic-Chain kein `create_table`; wird per Bootstrap-SQL angelegt, danach per Migration erweitert (`blocked`, `desktop_version`, `updater_version`, `update_channel`) | `server/api/app/models.py:34-63`, `server/api/alembic/bootstrap/0000_create_devices_and_inventory.sql:13-27`, `server/api/alembic/versions/20260305_0008_feature_override_scope_device_blocked.py:62-65`, `server/api/alembic/versions/20260305_0010_add_desktop_updater_versions.py:20-22` |
| `device_inventory` | `DeviceInventory` | In Alembic-Chain kein `create_table`; wird per Bootstrap-SQL angelegt | `server/api/app/models.py:65-79`, `server/api/alembic/bootstrap/0000_create_devices_and_inventory.sql:45-66` |
| `telemetry_snapshots` | `TelemetrySnapshot` | `op.create_table("telemetry_snapshots")` in Rev `20260227_0001` | `server/api/app/models.py:81-102`, `server/api/alembic/versions/20260227_0001_update_telemetry.py:44-69` |
| `licenses` | `License` | `op.create_table("licenses")` in Rev `20260302_0002` | `server/api/app/models.py:104-127`, `server/api/alembic/versions/20260302_0002_add_licenses.py:22-43` |
| `subscriptions` | `Subscription` | `op.create_table("subscriptions")` in Rev `20260304_0004` | `server/api/app/models.py:129-157`, `server/api/alembic/versions/20260304_0004_add_subscriptions_and_features.py:38-60` |
| `device_tokens` | `DeviceToken` | `op.create_table("device_tokens")` in Rev `20260304_0005`; `last_used_at` in Rev `20260304_0006` | `server/api/app/models.py:159-174`, `server/api/alembic/versions/20260304_0005_add_device_tokens_and_webhooks.py:23-53`, `server/api/alembic/versions/20260304_0006_device_token_last_used_at.py:18-21` |
| `webhook_events` | `WebhookEvent` | `op.create_table("webhook_events")` in Rev `20260304_0005`; redundanter Unique-Index entfernt in `20260304_0007` | `server/api/app/models.py:176-185`, `server/api/alembic/versions/20260304_0005_add_device_tokens_and_webhooks.py:57-83`, `server/api/alembic/versions/20260304_0007_drop_redundant_unique_indexes.py:30-31` |
| `feature_overrides` | `FeatureOverride` | `op.create_table("feature_overrides")` in Rev `20260304_0005`; Scope/Target-Refactor in `20260305_0008`; Rename `rollout_pct`→`rollout_percent` in `20260305_0009` | `server/api/app/models.py:187-220`, `server/api/alembic/versions/20260304_0005_add_device_tokens_and_webhooks.py:87-125`, `server/api/alembic/versions/20260305_0008_feature_override_scope_device_blocked.py:30-57`, `server/api/alembic/versions/20260305_0009_rename_rollout_pct_to_rollout_percent.py:15-17` |

## Alembic-Chain (IST)

| Revision | Inhalt (kurz) | Nachweis |
|---|---|---|
| `20260227_0001` | `telemetry_snapshots` create; `devices` Spalten-/Index-Anpassung, aber **kein** `devices` create | `server/api/alembic/versions/20260227_0001_update_telemetry.py:22-44` |
| `20260302_0002` | `licenses` create | `server/api/alembic/versions/20260302_0002_add_licenses.py:22-43` |
| `20260304_0003` | `plans` create + Seed | `server/api/alembic/versions/20260304_0003_add_plans.py:19-51` |
| `20260304_0004` | `plans` add columns + `subscriptions` create | `server/api/alembic/versions/20260304_0004_add_subscriptions_and_features.py:21-60` |
| `20260304_0005` | `device_tokens`, `webhook_events`, `feature_overrides` create | `server/api/alembic/versions/20260304_0005_add_device_tokens_and_webhooks.py:23-125` |
| `20260304_0006` | `device_tokens.last_used_at` | `server/api/alembic/versions/20260304_0006_device_token_last_used_at.py:18-21` |
| `20260304_0007` | Redundante Unique-Indizes drop | `server/api/alembic/versions/20260304_0007_drop_redundant_unique_indexes.py:29-31` |
| `20260305_0008` | `feature_overrides` scope/target/check; `devices.blocked` | `server/api/alembic/versions/20260305_0008_feature_override_scope_device_blocked.py:30-65` |
| `20260305_0009` | Rename `rollout_pct`→`rollout_percent` | `server/api/alembic/versions/20260305_0009_rename_rollout_pct_to_rollout_percent.py:15-17` |
| `20260305_0010` | `devices.desktop_version`, `devices.updater_version`, `devices.update_channel` | `server/api/alembic/versions/20260305_0010_add_desktop_updater_versions.py:20-22` |

## Bootstrap SQL (IST)
- `0000_create_devices_and_inventory.sql` ist idempotent (`CREATE TABLE IF NOT EXISTS`) und erzeugt `devices` + `device_inventory` inkl. Constraints/Indizes.
- Nachweis: `server/api/alembic/bootstrap/0000_create_devices_and_inventory.sql:13-66`.
- Laut Kommentar ist das explizit als Greenfield-Lückenschluss gedacht.
- Nachweis: `server/api/alembic/bootstrap/0000_create_devices_and_inventory.sql:1-5`.

## Zielbild-Tabellen: fehlend / unknown

Zielbildquellen im Repo:
- `docs/audit_fix_05.03.2026/01_GAP_MATRIX.md:51-54`
- `docs/audit_fix_05.03.2026/05_DOD_CHECKLIST.md:24-26`

| Zielbild-Tabelle(n) | IST im Code | Delta | Nachweis |
|---|---|---|---|
| `notifications`, `notification_reads` (oder read_at Modell) | nicht gefunden | P2: Persistente Notification-Read-States ergänzen | Zielbild: `docs/audit_fix_05.03.2026/01_GAP_MATRIX.md:51`; Abwesenheit: Command `rg -n '__tablename__\s*=\s*"(notifications|notification_reads)"|create_table\(\s*"(notifications|notification_reads)"' server/api/app/models.py server/api/alembic/versions` |
| `client_config` | nicht gefunden | P2: Remote Client Config DB/API ergänzen | Zielbild: `docs/audit_fix_05.03.2026/01_GAP_MATRIX.md:52`; Abwesenheit: Command `rg -n '__tablename__\s*=\s*"client_config"|create_table\(\s*"client_config"' server/api/app/models.py server/api/alembic/versions` |
| `rules_catalog`, `rule_findings` | unknown (nur Zielbild-Hinweis, keine verbindliche technische Spezifikation im Repo) | Wenn Zielbild bestätigt: Tabellen + API + UI ergänzen | Zielbild-Hinweis: `docs/audit_fix_05.03.2026/01_GAP_MATRIX.md:53`; fehlende Spezifikationsquelle: keine konkrete Schema-Datei unter `server/api/` |
| `knowledge_base`, `downloads` (persistente KB/Downloads) | nicht gefunden | P2: Persistenz ergänzen oder Zielbild-Annahme streichen | Zielbild: `docs/audit_fix_05.03.2026/01_GAP_MATRIX.md:54`; Abwesenheit: Command `rg -n '__tablename__\s*=\s*"(knowledge_base|downloads)"|create_table\(\s*"(knowledge_base|downloads)"' server/api/app/models.py server/api/alembic/versions` |

## Unknowns
- Ob eine reale Produktions-DB zusätzliche, nicht versionierte Tabellen enthält: `unknown`.
- Fehlende Quelle: Live-DB-Dump oder schema-only Dump aus Produktion ist nicht im Repo enthalten.
