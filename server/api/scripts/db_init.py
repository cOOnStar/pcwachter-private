from __future__ import annotations

import os
import subprocess
import sys
import time
from pathlib import Path

import psycopg


ROOT = Path(__file__).resolve().parents[1]
BOOTSTRAP_SQL = ROOT / "alembic" / "bootstrap" / "0000_create_devices_and_inventory.sql"


def _normalize_dsn(database_url: str) -> str:
    url = database_url.strip()
    if url.startswith("postgresql+psycopg://"):
        return "postgresql://" + url[len("postgresql+psycopg://"):]
    if url.startswith("postgresql://"):
        return url
    raise ValueError("DATABASE_URL must start with postgresql+psycopg:// or postgresql://")


def _wait_for_db(dsn: str, retries: int = 30, delay_seconds: int = 2) -> None:
    last_exc: Exception | None = None
    for attempt in range(1, retries + 1):
        try:
            with psycopg.connect(dsn, connect_timeout=5) as conn:
                with conn.cursor() as cur:
                    cur.execute("SELECT 1")
                    cur.fetchone()
            print(f"[db-init] database reachable (attempt {attempt}/{retries})")
            return
        except Exception as exc:  # pragma: no cover (connection errors are environment-dependent)
            last_exc = exc
            print(f"[db-init] waiting for database ({attempt}/{retries}): {exc}")
            time.sleep(delay_seconds)
    raise RuntimeError(f"database not reachable after {retries} attempts: {last_exc}")


def _run_bootstrap_sql(dsn: str) -> None:
    if not BOOTSTRAP_SQL.exists():
        raise FileNotFoundError(f"bootstrap sql not found: {BOOTSTRAP_SQL}")
    sql = BOOTSTRAP_SQL.read_text(encoding="utf-8")
    with psycopg.connect(dsn, autocommit=True) as conn:
        with conn.cursor() as cur:
            cur.execute(sql)
    print(f"[db-init] bootstrap applied: {BOOTSTRAP_SQL}")


def _run_alembic_upgrade() -> None:
    print("[db-init] running alembic upgrade head ...")
    subprocess.run(["alembic", "upgrade", "head"], cwd=ROOT, check=True)
    print("[db-init] alembic upgrade completed")


def main() -> int:
    database_url = os.getenv("DATABASE_URL", "").strip()
    if not database_url:
        print("[db-init] DATABASE_URL is missing", file=sys.stderr)
        return 2

    try:
        dsn = _normalize_dsn(database_url)
        _wait_for_db(dsn)
        _run_bootstrap_sql(dsn)
        _run_alembic_upgrade()
        print("[db-init] done")
        return 0
    except Exception as exc:
        print(f"[db-init] failed: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
