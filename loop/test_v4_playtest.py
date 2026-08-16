#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""V4 테스터 10명 키트·보드 반영 회귀."""
from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import board  # noqa: E402
import v4_playtest  # noqa: E402


class KitTests(unittest.TestCase):
    def test_script_has_v2_v3_v4_and_ten_testers(self):
        script = json.loads((HERE / "v4_test_script.json").read_text(encoding="utf-8"))
        ids = [g["id"] for g in script["gates"]]
        self.assertEqual(ids, ["V2", "V3", "V4"])
        v4 = next(g for g in script["gates"] if g["id"] == "V4")
        self.assertEqual(len(v4["testers"]), 10)
        self.assertEqual(len(v4["steps"]), 5)
        self.assertEqual(script["human_70"], "pending")
        self.assertIn("손실 분노", script["fail_reasons"])
        loaded = board.load_playtest_script()
        self.assertEqual(len(loaded["gates"]), 3)
        self.assertTrue(loaded["gates"][0]["steps"][0]["do"])

    def test_playtest_doc_and_sheet_exist(self):
        root = HERE.parent
        doc = (root / "docs" / "V4_EXTERNAL_PLAYTEST.md").read_text(encoding="utf-8")
        sheet = (root / "docs" / "feedback" / "playtest_sheet.md").read_text(encoding="utf-8")
        self.assertIn("V2-1", doc)
        self.assertIn("V4-5", doc)
        self.assertIn("개발자·오너", doc)
        self.assertIn("t01 이서연", sheet)
        self.assertIn("t10 신유라", sheet)
        self.assertIn("즉시 계속", sheet)

    def test_ten_distinct_testers(self):
        kit = v4_playtest.load_kit()
        testers = kit["testers"]
        self.assertEqual(len(testers), 10)
        self.assertEqual(len({t["id"] for t in testers}), 10)
        self.assertEqual(len({t["favorite"] for t in testers}), 10)
        self.assertTrue(all(t["minutes"] >= 30 for t in testers))

    def test_csharp_lists_same_ten(self):
        kit = v4_playtest.load_kit()
        cs = (HERE.parent / "projects/ashes-to-stars/unity/Assets/_Game/Scripts/Editor/V4ExternalPlaytest.cs"
              ).read_text(encoding="utf-8")
        for t in kit["testers"]:
            self.assertIn(t["id"], cs)
            self.assertIn(t["favorite"], cs)
            self.assertIn(t["name"], cs)

    def test_playtest_state_from_kit_without_sessions(self):
        old = board.ROOT
        with tempfile.TemporaryDirectory() as tmp:
            tmp = Path(tmp)
            (tmp / "output/qa/ashes-to-stars/v4_playtest").mkdir(parents=True)
            board.ROOT = tmp
            try:
                st = board.playtest_state()
            finally:
                board.ROOT = old
        self.assertEqual(st["n"], 10)
        self.assertEqual(st["ran"], 0)
        self.assertEqual(st["human_70"], "pending")
        self.assertEqual(st["sessions"][0]["tester"], "이서연")

    def test_playtest_state_reads_sessions(self):
        old = board.ROOT
        with tempfile.TemporaryDirectory() as tmp:
            tmp = Path(tmp)
            dest = tmp / "output/qa/ashes-to-stars/v4_playtest"
            dest.mkdir(parents=True)
            dest.joinpath("sessions.json").write_text(json.dumps({
                "ran_at": "2026-08-16 19:00",
                "sessions": [{
                    "id": "t01", "tester": "이서연", "favorite": "백호",
                    "deleted": True, "continued": True, "living": 4, "level": 20,
                    "gear": True, "continue_path": "remaining_party",
                }],
            }, ensure_ascii=False), encoding="utf-8")
            board.ROOT = tmp
            try:
                st = board.playtest_state()
                note = board.v4_playtest_note()
            finally:
                board.ROOT = old
        self.assertEqual(st["ran"], 1)
        self.assertEqual(st["deleted"], 1)
        self.assertTrue(st["sessions"][0]["deleted"])
        self.assertEqual(st["human_70"], "pending")
        self.assertIn("키트", note)

    def test_report_does_not_close_human_gate(self):
        kit = v4_playtest.load_kit()
        old = (v4_playtest.OUT, v4_playtest.REPORT)
        with tempfile.TemporaryDirectory() as tmp:
            tmp = Path(tmp)
            v4_playtest.OUT = tmp
            v4_playtest.REPORT = tmp / "report.json"
            try:
                r = v4_playtest.write_report(kit, {
                    "ran_at": "now",
                    "sessions": [{"id": "t01", "deleted": True, "continued": True}] * 10,
                })
            finally:
                v4_playtest.OUT, v4_playtest.REPORT = old
        self.assertEqual(r["human_70"], "pending")
        self.assertEqual(r["deleted"], 10)

    def test_v4b_zero_without_kit_progress(self):
        self.assertEqual(board.v4_gate_pct({"n": 0, "ran": 0, "deleted": 0, "continued": 0}), 0)

    def test_v4b_rises_with_sessions_but_caps_before_human(self):
        st = {"n": 10, "ran": 10, "deleted": 10, "continued": 10}
        self.assertEqual(board.v4_gate_pct(st), 90)
        self.assertEqual(board.v4_gate_pct(st, decisions={
            "x": {"title": "V4 외부 테스터 70%", "choice": "skip"},
        }), 90)
        self.assertEqual(board.v4_gate_pct(st, decisions={
            "x": {"title": "V4 외부 테스터 70%", "choice": "pass"},
        }), 100)

    def test_proto_average_leaves_80_when_sessions_exist(self):
        old = board.ROOT
        with tempfile.TemporaryDirectory() as tmp:
            tmp = Path(tmp)
            dest = tmp / "output/qa/ashes-to-stars/v4_playtest"
            dest.mkdir(parents=True)
            dest.joinpath("sessions.json").write_text(json.dumps({
                "ran_at": "2026-08-16 18:55",
                "sessions": [{
                    "id": f"t{i:02d}", "deleted": True, "continued": True,
                } for i in range(1, 11)],
            }), encoding="utf-8")
            board.ROOT = tmp
            try:
                charts = board.progress_charts(
                    "V2 사람 판정 → 통과\n",
                    "### 현재 핵심 미완 (원장 §22)\n\n"
                    "- **V3 한 판 종단**(원장 1순위) ✅ 실행 증거 닫힘\n",
                    "### 21-4. 주차별 진행\n| ~~W1~~ | x | ✅ 완료 |\n",
                    {"x": {"title": "V2 사람 판정", "choice": "pass"}},
                )
            finally:
                board.ROOT = old
        v4b = next(g for g in charts["gates"] if g["id"] == "V4b")["pct"]
        proto = next(r for r in charts["roadmap"] if r["id"] == "0")["pct"]
        self.assertEqual(v4b, 90)
        self.assertGreater(proto, 80)
        self.assertLessEqual(proto, 90)
        self.assertLess(proto, 100)


if __name__ == "__main__":
    unittest.main()
