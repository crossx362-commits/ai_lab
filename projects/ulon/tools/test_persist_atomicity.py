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
        self.assertEqual(persist.get_character("player")["Gold"], 15)
        self.assertEqual(persist.get_character("player"), saved)
        stable = persist.put_stable("player", {"PetId": "pet", "ControlSlots": 2})
        self.assertEqual(stable["PetId"], "pet")
        self.assertEqual(persist.get_stable("player"), stable)

    def test_saved_at_round_trip_and_update(self):
        first = persist.put_character({"AccountId": "stamp", "Gold": 10, "SavedAt": "2020-01-01T00:00:00Z"})
        self.assertEqual(first["SavedAt"], "2020-01-01T00:00:00Z")
        self.assertEqual(persist.get_character("stamp")["SavedAt"], "2020-01-01T00:00:00Z")
        later = persist.put_character({"AccountId": "stamp", "Gold": 99, "SavedAt": "2026-09-12T00:00:00Z",
                                       "Inventory": [item()], "Skills": [{"Id": 0, "Value": 40}]})
        self.assertEqual(later["Gold"], 99)
        self.assertEqual(later["SavedAt"], "2026-09-12T00:00:00Z")
        loaded = persist.get_character("stamp")
        self.assertEqual(loaded["Gold"], 99)
        self.assertEqual(loaded["SavedAt"], "2026-09-12T00:00:00Z")
        self.assertEqual(loaded["Inventory"], [item()])
        stamped = persist.put_character({"AccountId": "autostamp", "Gold": 3})
        self.assertTrue(stamped["SavedAt"])

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

    def _seed_world(self, gold=15):
        persist.put_character({
            "AccountId": "bak", "CharacterId": "bak", "Gold": gold,
            "Inventory": [item()], "Skills": [{"Id": 0, "Value": 12, "Lock": 0}],
            "Bank": [item(1)], "Spells": [1],
        })
        persist.put_house("plot-bak", {
            "OwnerCharacterId": "bak", "AccountId": "bak",
            "Items": [item()],
        })
        persist.put_stable("bak", {"PetId": "wolf", "ControlSlots": 2, "DisplayName": "늑대"})

    def test_backup_restore_round_trip_includes_house_and_stable(self):
        self._seed_world(15)
        snap = persist.write_backup()
        self.assertTrue(Path(snap["path"]).is_file())
        self.assertGreaterEqual(snap["counts"]["characters"], 1)
        self.assertGreaterEqual(snap["counts"]["houses"], 1)
        self.assertGreaterEqual(snap["counts"]["stables"], 1)
        persist.put_character({"AccountId": "bak", "CharacterId": "bak", "Gold": 99})
        persist.put_house("plot-bak", {"OwnerCharacterId": "other", "Items": []})
        persist.put_stable("bak", {"PetId": "gone", "ControlSlots": 1})
        persist.restore_snapshot(snap)
        loaded = persist.get_character("bak")
        self.assertEqual(loaded["Gold"], 15)
        self.assertEqual(loaded["Inventory"], [item()])
        house = persist.get_house("plot-bak")
        self.assertEqual(house["OwnerCharacterId"], "bak")
        self.assertEqual(house["Items"], [item()])
        stable = persist.get_stable("bak")
        self.assertEqual(stable["PetId"], "wolf")
        self.assertEqual(stable["ControlSlots"], 2)

    def test_restore_missing_table_does_not_touch_live_rows(self):
        self._seed_world(15)
        snap = persist.export_snapshot()
        persist.put_character({"AccountId": "bak", "CharacterId": "bak", "Gold": 99})
        bad = copy.deepcopy(snap)
        del bad["tables"]["houses"]
        with self.assertRaises(ValueError):
            persist.restore_snapshot(bad)
        self.assertEqual(persist.get_character("bak")["Gold"], 99)
        self.assertEqual(persist.get_house("plot-bak")["OwnerCharacterId"], "bak")

    def test_restore_insert_failure_rolls_back(self):
        self._seed_world(15)
        snap = persist.export_snapshot()
        persist.put_character({"AccountId": "bak", "CharacterId": "bak", "Gold": 99})
        bad = copy.deepcopy(snap)
        row = copy.deepcopy(bad["tables"]["characters"][0])
        bad["tables"]["characters"].append(row)
        with self.assertRaises(Exception):
            persist.restore_snapshot(bad)
        self.assertEqual(persist.get_character("bak")["Gold"], 99)
        self.assertEqual(persist.get_house("plot-bak")["OwnerCharacterId"], "bak")
        self.assertEqual(persist.get_stable("bak")["PetId"], "wolf")

    def test_http_backup_and_restore(self):
        self._seed_world(15)
        server = ThreadingHTTPServer(("127.0.0.1", 0), persist.Handler)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            base = "http://127.0.0.1:" + str(server.server_port)
            req = urllib.request.Request(base + "/backup", data=b"{}", method="POST")
            req.add_header("Content-Type", "application/json")
            with urllib.request.urlopen(req, timeout=5) as resp:
                saved = json.load(resp)
            self.assertTrue(saved["ok"])
            self.assertTrue(Path(saved["path"]).is_file())
            persist.put_character({"AccountId": "bak", "CharacterId": "bak", "Gold": 1})
            body = json.dumps({"path": saved["path"]}).encode()
            req = urllib.request.Request(base + "/restore", data=body, method="POST")
            req.add_header("Content-Type", "application/json")
            with urllib.request.urlopen(req, timeout=5) as resp:
                restored = json.load(resp)
            self.assertTrue(restored["ok"])
            self.assertEqual(persist.get_character("bak")["Gold"], 15)
            self.assertEqual(persist.get_stable("bak")["PetId"], "wolf")
            missing = urllib.request.Request(
                base + "/restore",
                data=json.dumps({"path": "/no/such/backup.json"}).encode(),
                method="POST",
            )
            missing.add_header("Content-Type", "application/json")
            with self.assertRaises(urllib.error.HTTPError) as ctx:
                urllib.request.urlopen(missing, timeout=5)
            self.assertEqual(ctx.exception.code, 404)
        finally:
            server.shutdown()
            server.server_close()
            thread.join(timeout=3)

    def test_nc_skip_restore_refuses(self):
        self._seed_world(15)
        snap = persist.export_snapshot()
        persist.NC_SKIP_RESTORE = True
        try:
            with self.assertRaises(RuntimeError):
                persist.restore_snapshot(snap)
            self.assertEqual(persist.get_character("bak")["Gold"], 15)
        finally:
            persist.NC_SKIP_RESTORE = False


class SQLiteTests(AtomicSaveTests, unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.patch = patch.multiple(
            persist,
            POSTGRES=False,
            DB_PATH=Path(self.temp.name) / "db.sqlite",
            BACKUP_DIR=Path(self.temp.name) / "backups",
            NC_SKIP_RESTORE=False,
        )
        self.patch.start()
        self.addCleanup(self.patch.stop)
        persist.init()


@unittest.skipUnless(os.environ.get("ULON_TEST_PG_DSN"), "ULON_TEST_PG_DSN 미지정: PostgreSQL 미검증")
class PostgreSQLTests(AtomicSaveTests, unittest.TestCase):
    def setUp(self):
        import psycopg2
        from psycopg2.extensions import make_dsn
        self.schema = "ulon_test_" + uuid.uuid4().hex
        self.bakdir = tempfile.TemporaryDirectory()
        self.addCleanup(self.bakdir.cleanup)
        self.admin = psycopg2.connect(os.environ["ULON_TEST_PG_DSN"])
        self.admin.autocommit = True
        self.addCleanup(self.admin.close)
        with self.admin.cursor() as cur:
            cur.execute('CREATE SCHEMA "' + self.schema + '"')
        self.addCleanup(self.drop_schema)
        self.patch = patch.multiple(
            persist,
            POSTGRES=True,
            DATABASE_URL=make_dsn(os.environ["ULON_TEST_PG_DSN"], options="-c search_path=" + self.schema),
            BACKUP_DIR=Path(self.bakdir.name),
            NC_SKIP_RESTORE=False,
        )
        self.patch.start()
        self.addCleanup(self.patch.stop)
        persist.init()

    def drop_schema(self):
        with self.admin.cursor() as cur:
            cur.execute('DROP SCHEMA "' + self.schema + '" CASCADE')


if __name__ == "__main__":
    unittest.main(verbosity=2)
