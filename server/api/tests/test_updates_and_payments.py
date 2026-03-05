import unittest
from datetime import datetime, timezone
from types import SimpleNamespace
from urllib.parse import parse_qs, urlparse

from fastapi import FastAPI
from fastapi.testclient import TestClient

from app.db import get_db
from app.routers.payments import _append_query_param
from app.routers.updates import router as updates_router


class _DummyResult:
    def __init__(self, row):
        self._row = row

    def scalar_one_or_none(self):
        return self._row


class _DummySession:
    def __init__(self, row):
        self._row = row

    def execute(self, _stmt):
        return _DummyResult(self._row)


def _build_manifest_row(**overrides):
    data = {
        "component": "desktop",
        "channel": "stable",
        "latest_version": "1.2.3",
        "min_supported_version": "1.0.0",
        "mandatory": False,
        "download_url": "https://downloads.example.com/pcw.exe",
        "sha256": "abc123",
        "changelog": "Bugfixes",
        "released_at": datetime(2026, 3, 5, 12, 0, tzinfo=timezone.utc),
    }
    data.update(overrides)
    return SimpleNamespace(**data)


def _build_test_client(row):
    app = FastAPI()
    app.include_router(updates_router)

    def _override_get_db():
        return _DummySession(row)

    app.dependency_overrides[get_db] = _override_get_db
    return TestClient(app)


class UpdatesLatestEndpointTests(unittest.TestCase):
    def test_latest_manifest_success(self):
        client = _build_test_client(_build_manifest_row())

        res = client.get("/updates/latest?channel=stable&component=desktop")

        self.assertEqual(res.status_code, 200)
        payload = res.json()
        self.assertEqual(payload["component"], "desktop")
        self.assertEqual(payload["channel"], "stable")
        self.assertEqual(payload["latest_version"], "1.2.3")
        self.assertEqual(payload["min_supported_version"], "1.0.0")
        self.assertIn("released_at", payload)

    def test_latest_manifest_not_found(self):
        client = _build_test_client(None)

        res = client.get("/updates/latest?channel=stable&component=desktop")

        self.assertEqual(res.status_code, 404)
        self.assertEqual(res.json()["detail"], "update manifest not found")


class StripeSuccessUrlAppendTests(unittest.TestCase):
    def test_append_to_url_without_existing_query(self):
        url = _append_query_param("https://example.com/account/billing", "session_id", "sess_123")
        parsed = urlparse(url)
        qs = parse_qs(parsed.query)
        self.assertEqual(parsed.path, "/account/billing")
        self.assertEqual(qs.get("session_id"), ["sess_123"])

    def test_append_to_url_with_existing_query(self):
        url = _append_query_param(
            "https://example.com/account/billing?checkout=success",
            "session_id",
            "sess_123",
        )
        parsed = urlparse(url)
        qs = parse_qs(parsed.query)
        self.assertEqual(qs.get("checkout"), ["success"])
        self.assertEqual(qs.get("session_id"), ["sess_123"])


if __name__ == "__main__":
    unittest.main()
