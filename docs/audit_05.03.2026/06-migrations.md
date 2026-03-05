# Audit E2) Datenbank IST (Migrations)

## Methode / Nachweis
- Alembic-Dateien: `server/api/alembic/versions/*.py`
- Parser-Output: `docs/audit/_generated/migrations_inventory.csv`
- Runtime-Status:
  - `docker exec pcw-api alembic current`
  - `docker exec pcw-api alembic heads`
  - `docker exec pcw-postgres psql -U pcwaechter -d pcwaechter -c "select version_num from alembic_version;"`

## Alembic-Kette (IST)

| Reihenfolge | Revision | Down Revision | Datei | Kurzbeschreibung |
|---|---|---|---|---|
| 1 | `20260227_0001` | `None` | `20260227_0001_update_telemetry.py` | Telemetry-Tabelle neu, `devices`-Anpassungen (`device_install_id`, `host_name`, Indizes) |
| 2 | `20260302_0002` | `20260227_0001` | `20260302_0002_add_licenses.py` | `licenses` |
| 3 | `20260304_0003` | `20260302_0002` | `20260304_0003_add_plans.py` | `plans` |
| 4 | `20260304_0004` | `20260304_0003` | `20260304_0004_add_subscriptions_and_features.py` | `subscriptions`, neue Plan-Felder |
| 5 | `20260304_0005` | `20260304_0004` | `20260304_0005_add_device_tokens_and_webhooks.py` | `device_tokens`, `webhook_events`, `feature_overrides` |
| 6 | `20260304_0006` | `20260304_0005` | `20260304_0006_device_token_last_used_at.py` | `device_tokens.last_used_at` |
| 7 | `20260304_0007` | `20260304_0006` | `20260304_0007_drop_redundant_unique_indexes.py` | redundante Unique-Indizes entfernen |
| 8 | `20260305_0008` | `20260304_0007` | `20260305_0008_feature_override_scope_device_blocked.py` | `feature_overrides.scope/target_id`, `devices.blocked` |
| 9 | `20260305_0009` | `20260305_0008` | `20260305_0009_rename_rollout_pct_to_rollout_percent.py` | Rename `rollout_pct -> rollout_percent` |

## Runtime-Migrationsstatus

| Check | Ergebnis |
|---|---|
| `alembic current` | `20260305_0009 (head)` |
| `alembic heads` | `20260305_0009 (head)` |
| `alembic_version.version_num` | `20260305_0009` |

## Konsistenz / Drift

| Prüffrage | Ergebnis | Nachweis |
|---|---|---|
| Migration-Head entspricht DB-Version | OK | `alembic current`, `alembic heads`, `alembic_version` |
| Alle ORM-Tabellen durch Alembic erzeugbar | **Teilweise** | In der Migrationskette werden `devices` und `device_inventory` nicht neu erstellt |
| Erklärung für `devices` in Migration `0001` | Baseline-vorausgesetzt | `20260227_0001` nutzt `batch_alter_table("devices")` statt `create_table("devices")` |
| Risiko | Reproduzierbarkeit auf leerer DB ohne zusätzliche Bootstrap-Schritte ist nicht vollständig nachweisbar | kein dedizierter Initial-Migrationsschritt für `devices`/`device_inventory` gefunden |

## Notiz zur Reproduzierbarkeit
- Für vollständig reproduzierbare Greenfield-Deployments sollte entweder:
  - eine saubere Initial-Migration (`create_table devices/device_inventory`) ergänzt werden, oder
  - der initiale Bootstrap-Prozess explizit dokumentiert werden (wenn bewusst außerhalb Alembic).
