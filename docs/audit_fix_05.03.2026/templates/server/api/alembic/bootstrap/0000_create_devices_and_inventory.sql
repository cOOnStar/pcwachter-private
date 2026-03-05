-- PCWächter Greenfield Bootstrap (idempotent)
-- Zweck: Schließt die Audit-Lücke, dass Alembic in IST `devices`/`device_inventory` nicht initial erzeugt.
-- Nachweis Lücke: audit_05.03.2026/06-migrations.md (devices/device_inventory nicht create_table)

BEGIN;

-- Optional: UUID generator (für DEFAULT gen_random_uuid()).
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- 1) devices
CREATE TABLE IF NOT EXISTS devices (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  device_install_id varchar(128) NOT NULL,
  host_name varchar(255),
  os_name varchar(255),
  os_version varchar(255),
  os_build varchar(255),
  agent_version varchar(64),
  agent_channel varchar(32),
  primary_ip varchar(64),
  macs jsonb,
  last_seen_at timestamptz,
  created_at timestamptz NOT NULL DEFAULT now(),
  blocked boolean NOT NULL DEFAULT false
);

-- Unique constraint (device_install_id)
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint
    WHERE conname = 'devices_device_install_id_key'
  ) THEN
    ALTER TABLE devices ADD CONSTRAINT devices_device_install_id_key UNIQUE (device_install_id);
  END IF;
END$$;

-- Indexes
CREATE INDEX IF NOT EXISTS ix_devices_host_name ON devices (host_name);
CREATE INDEX IF NOT EXISTS ix_devices_last_seen_at ON devices (last_seen_at);

-- 2) device_inventory
CREATE TABLE IF NOT EXISTS device_inventory (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  device_id uuid NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
  collected_at timestamptz NOT NULL,
  payload jsonb NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now()
);

-- Unique (device_id, collected_at)
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint
    WHERE conname = 'uq_device_inventory_device_collected'
  ) THEN
    ALTER TABLE device_inventory
      ADD CONSTRAINT uq_device_inventory_device_collected UNIQUE (device_id, collected_at);
  END IF;
END$$;

CREATE INDEX IF NOT EXISTS ix_device_inventory_device_id ON device_inventory (device_id);

COMMIT;
