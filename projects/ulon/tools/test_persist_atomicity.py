#!/usr/bin/env python3
"""실제 SQLite 및 선택적 PostgreSQL로 저장 실패의 원자성을 검증한다.
ULON_TEST_PG_DSN 지정 시 임의 스키마만 생성/삭제하며 기존 테이블은 건드리지 않는다.
"""
import copy
import importlib.util
import os
from pathlib import Path
import sys
import tempfile
import unittest
import json
import threading
import urllib.request
import urllib.error
from http.server import ThreadingHTTPServer
import uuid
from unittest.mock import patch

sys.stdout.reconfigure(encoding="utf-8")
SOURCE = Path(__file__).resolve().parents[1] / "server" / "persist.py"
with patch.dict(os.environ, {"DATABASE_URL": "sqlite"}):
    spec = importlib.util.spec_from_file_location("ulon_persist_test", SOURCE)
    persist = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(persist)


def item(slot=0):
    return {"Slot": slot, "TemplateId": "iron_sword", "Amount": 1,
            "Uses": 12, "MakerId": "smith", "Exceptional": True}


class AtomicSaveTests:
    def check_http(self, route):
        server = ThreadingHTTPServer(("127.0.0.1", 0), persist.Handler)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            try:
                response = urllib.request.urlopen(
                    "http://127.0.0.1:" + str(server.server_port) + route, timeout=3)
            except urllib.error.HTTPError as error:
                response = error
            with response:
                return response.status, json.load(response)
        finally:
            server.shutdown()
            server.server_close()
            thread.join(timeout=3)

    def test_readiness_checks_database(self):
        status, body = self.check_http("/ready")
        self.assertEqual(status, 200)
        self.assertTrue(body["ok"])
        with patch.object(persist, "connect", side_effect=ConnectionError("DB unavailable")):
            status, body = self.check_http("/ready")
            self.assertEqual(status, 503)
            self.assertFalse(body["ok"])
            self.assertEqual(self.check_http("/health")[0], 200)

    def test_round_trip(self):
        snap = {"AccountId": "player", "Gold": 15, "Inventory": [item()],
                "Skills": [{"Id": 0, "Value": 12, "Lock": 1}],
                "Bank": [item()], "Spells": [1], "CorpseId": "corpse",
                "Corpse": [item()]}
        saved = persist.put_character(snap)
        self.assertEqual(saved["Gold"], 15)
        for key in ("Inventory", "Bank", "Corpse"):
            self.assertEqual(saved[key], [item()])
        self.assertEqual(persist.get_character("player"), saved)
        stable = persist.put_stable("player", {"PetId": "pet", "ControlSlots": 2})
        self.assertEqual(stable["PetId"], "pet")
        self.assertEqual(persist.get_stable("player"), stable)

    def test_failed_character_save_preserves_whole_snapshot(self):
        for field in ("Skills", "Inventory", "Bank", "Spells", "Corpse"):
            with self.subTest(field=field):
                snap = {"AccountId": field, "Gold": 15, "Inventory": [item()],
                        "Bank": [item()], "Skills": [{"Id": 0, "Value": 12}],
                        "Spells": [1], "CorpseId": field + "-corpse", "Corpse": [item()]}
                before = persist.put_character(snap)
                bad = copy.deepcopy(snap)
                bad["Gold"] = 999
                bad[field] = bad[field] * 2  # 중복 기본키: real DB constraint failure
                with self.assertRaises(Exception):
                    persist.put_character(bad)
                self.assertEqual(persist.get_character(field), before)

    def test_failed_new_character_leaves_no_partial_account(self):
        with self.assertRaises(Exception):
            persist.put_character({"AccountId": "new", "Inventory": [item(), item()]})
        self.assertIsNone(persist.get_character("new"))
        conn = persist.connect()
        try:
            cur = conn.cursor()
            cur.execute("SELECT COUNT(*) FROM accounts WHERE account_id = 'new'")
            row = cur.fetchone()
            self.assertEqual(next(iter(row.values())) if isinstance(row, dict) else row[0], 0)
        finally:
            conn.close()

    def test_failed_house_save_preserves_owner_and_items(self):
        before = persist.put_house("plot", {"OwnerCharacterId": "owner", "Items": [item()]})
        with self.assertRaises(Exception):
            persist.put_house("plot", {"OwnerCharacterId": "other", "Items": [item(), item()]})
        self.assertEqual(persist.get_house("plot"), before)


class SQLiteTests(AtomicSaveTests, unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.patch = patch.multiple(persist, POSTGRES=False, DB_PATH=Path(self.temp.name) / "db.sqlite")
        self.patch.start()
        self.addCleanup(self.patch.stop)
        persist.init()


@unittest.skipUnless(os.environ.get("ULON_TEST_PG_DSN"), "ULON_TEST_PG_DSN 미지정: PostgreSQL 미검증")
class PostgreSQLTests(AtomicSaveTests, unittest.TestCase):
    def setUp(self):
        import psycopg2
        from psycopg2.extensions import make_dsn
        self.schema = "ulon_test_" + uuid.uuid4().hex
        self.admin = psycopg2.connect(os.environ["ULON_TEST_PG_DSN"])
        self.admin.autocommit = True
        self.addCleanup(self.admin.close)
        with self.admin.cursor() as cur:
            cur.execute('CREATE SCHEMA "' + self.schema + '"')
        self.addCleanup(self.drop_schema)
        self.patch = patch.multiple(persist, POSTGRES=True,
            DATABASE_URL=make_dsn(os.environ["ULON_TEST_PG_DSN"], options="-c search_path=" + self.schema))
        self.patch.start()
        self.addCleanup(self.patch.stop)
        persist.init()

    def drop_schema(self):
        with self.admin.cursor() as cur:
            cur.execute('DROP SCHEMA "' + self.schema + '" CASCADE')


if __name__ == "__main__":
    unittest.main(verbosity=2)
