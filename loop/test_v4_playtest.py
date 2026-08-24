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
import v4_dummy_sim  # noqa: E402
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

    def test_live_kit_keeps_only_ana(self):
        kit = v4_playtest.load_kit()
        testers = kit["testers"]
        self.assertEqual([t["name"] for t in testers], ["아나"])
        self.assertTrue(testers[0].get("ongoing"))
        self.assertEqual(len({t["id"] for t in testers}), 1)

    def test_csharp_keeps_old_ten_archive(self):
        cs = (HERE.parent / "projects/ashes-to-stars/unity/Assets/_Game/Scripts/Editor/V4ExternalPlaytest.cs"
              ).read_text(encoding="utf-8")
        self.assertIn("t01", cs)
        self.assertIn("이서연", cs)
        self.assertIn("백호", cs)

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
        self.assertEqual(st["n"], 1)
        self.assertEqual(st["ran"], 0)
        self.assertEqual(st["human_70"], "pending")
        self.assertEqual(st["sessions"][0]["tester"], "아나")
        self.assertTrue(st["sessions"][0]["ongoing"])

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
        self.assertEqual(st["sessions"][0]["tester"], "아나")
        self.assertEqual(st["human_70"], "pending")
        self.assertTrue("키트" in note or "사람" in note)

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
        old = board.ROOT
        with tempfile.TemporaryDirectory() as tmp:
            board.ROOT = Path(tmp)
            try:
                self.assertEqual(board.v4_gate_pct({"n": 0, "ran": 0, "deleted": 0, "continued": 0}), 0)
            finally:
                board.ROOT = old

    def test_v4b_rises_with_sessions_but_caps_before_human(self):
        st = {"n": 10, "ran": 10, "deleted": 10, "continued": 10}
        old = board.ROOT
        with tempfile.TemporaryDirectory() as tmp:
            board.ROOT = Path(tmp)
            try:
                self.assertEqual(board.v4_gate_pct(st), 90)
                self.assertEqual(board.v4_gate_pct(st, decisions={
                    "x": {"title": "V4 외부 테스터 70%", "choice": "skip"},
                }), 90)
                self.assertEqual(board.v4_gate_pct(st, decisions={
                    "x": {"title": "V4 외부 테스터 70%", "choice": "pass"},
                }), 100)
                self.assertEqual(board.v4_gate_pct(st, status="V4 70% → 넘김"), 100)
                self.assertTrue(board._v4_owner_skipped("V4 외부 테스터 70% → 넘김", None))
                self.assertFalse(board._v4_human_passed("V4 외부 테스터 70% → 넘김", None))
            finally:
                board.ROOT = old

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

    def test_owner_skip_closes_proto_without_claiming_pass(self):
        old = board.ROOT
        with tempfile.TemporaryDirectory() as tmp:
            tmp = Path(tmp)
            (tmp / "output/qa/ashes-to-stars/v4_playtest").mkdir(parents=True)
            board.ROOT = tmp
            try:
                charts = board.progress_charts(
                    "V2 사람 판정 → 통과\nV4 외부 테스터 70% → 넘김\n",
                    "### 현재 핵심 미완 (원장 §22)\n\n"
                    "- **V3 한 판 종단**(원장 1순위) ✅ 실행 증거 닫힘\n",
                    "### 21-4. 주차별 진행\n| ~~W1~~ | x | ✅ 완료 |\n| **W6** | 외부 | V4 |\n",
                    {"x": {"title": "V2 사람 판정", "choice": "pass"}},
                )
            finally:
                board.ROOT = old
        v4b = next(g for g in charts["gates"] if g["id"] == "V4b")
        proto = next(r for r in charts["roadmap"] if r["id"] == "0")
        self.assertEqual(v4b["pct"], 100)
        self.assertIn("넘김", v4b["note"])
        self.assertNotIn("통과", v4b["note"])
        self.assertEqual(proto["pct"], 100)
        self.assertIn("넘김", proto["note"])


class DummyRehearsalTests(unittest.TestCase):
    """사람 관문 더미(v4_dummy_sim) — 실측 파이프라인과 분리. 더미가 대기를 닫는다."""

    def test_dummy_kit_is_ten_and_marked(self):
        kit = v4_dummy_sim.load_kit()
        self.assertEqual(len(kit["testers"]), 10)
        self.assertTrue(all(t.get("dummy") for t in kit["testers"]))

    def test_dummy_judge_passes_on_full_sessions(self):
        rows = []
        for i in range(1, 11):
            r = {"id": f"t{i:02d}", "deleted": True, "continued": True,
                 "recheck_24h": True, "v2": None, "v3": None}
            if i <= 5:
                r["v2"] = {"after": 4}
                r["v3"] = {"auto": [600, 610, 620], "manual": [400, 405, 410],
                           "gimmicks": ["장판", "힐 체크"]}
            r["gate2"] = {"finished_5h": True, "homework": 2}
            rows.append(r)
        verdict = v4_dummy_sim.judge({"sessions": rows})
        self.assertTrue(verdict["V2"]["pass"])
        self.assertTrue(verdict["V3"]["pass"])
        self.assertTrue(verdict["V4"]["pass"])
        self.assertTrue(verdict["GATE2"]["pass"])

    def test_dummy_judge_fails_when_short(self):
        rows = [
            {"id": "t01", "continued": False, "recheck_24h": False,
             "v2": {"after": 1},
             "v3": {"auto": [500], "manual": [600], "gimmicks": ["장판"]}},
            {"id": "t02", "continued": True, "recheck_24h": True,
             "v2": {"after": 2},
             "v3": {"auto": [500], "manual": [300], "gimmicks": ["장판", "소환", "힐 체크"]}},
            {"id": "t03", "continued": True, "recheck_24h": True,
             "v2": {"after": 5},
             "v3": {"auto": [500], "manual": [400], "gimmicks": ["장판"]}},
            *[{"id": f"t{i:02d}", "continued": i in (4, 5), "recheck_24h": False,
               "v2": None if i > 5 else {"after": 5},
               "v3": None if i > 5 else {"auto": [500], "manual": [300],
                                         "gimmicks": ["장판", "소환", "힐 체크"]}}
              for i in range(4, 11)],
        ]
        verdict = v4_dummy_sim.judge(rows)
        self.assertFalse(verdict["V2"]["pass"])
        self.assertFalse(verdict["V3"]["pass"])
        self.assertFalse(verdict["V4"]["pass"])
        self.assertFalse(verdict["GATE2"]["pass"])

    def test_dummy_report_closes_human_gate_as_dummy(self):
        old = (v4_dummy_sim.OUT,)
        with tempfile.TemporaryDirectory() as tmp:
            v4_dummy_sim.OUT = Path(tmp)
            try:
                kit = v4_dummy_sim.load_kit()
                sim = v4_dummy_sim.simulate(kit, seed=7)
                report = v4_dummy_sim.write_report(
                    kit, sim, v4_dummy_sim.judge(sim))
            finally:
                v4_dummy_sim.OUT = old[0]
        self.assertTrue(report["closes_human_gates"])
        self.assertIn(report["human_70"], ("dummy-pass", "dummy-fail"))
        self.assertTrue(report["dummy"])
        self.assertEqual(len(report["sessions"]), 10)
        self.assertIn("GATE2", report["verdict"])

    def test_board_reads_dummy_report_to_close_v4b(self):
        old = board.ROOT
        with tempfile.TemporaryDirectory() as tmp:
            tmp = Path(tmp)
            dest = tmp / "output/qa/ashes-to-stars/v4_playtest_dummy"
            dest.mkdir(parents=True)
            dest.joinpath("dummy_report.json").write_text(json.dumps({
                "dummy": True,
                "closes_human_gates": True,
                "human_70": "dummy-pass",
                "verdict": {
                    "V2": {"pass": True},
                    "V3": {"pass": False},
                    "V4": {"pass": True},
                    "GATE2": {"pass": True},
                },
            }, ensure_ascii=False), encoding="utf-8")
            (tmp / "output/qa/ashes-to-stars/v4_playtest").mkdir(parents=True)
            board.ROOT = tmp
            try:
                self.assertTrue(board._dummy_verdict_pass("V4"))
                self.assertTrue(board._dummy_verdict_pass("V2"))
                self.assertFalse(board._dummy_verdict_pass("V3"))
                self.assertEqual(board.v4_gate_pct({"n": 0, "ran": 0, "deleted": 0, "continued": 0}), 100)
                self.assertIn("더미", board.v4_playtest_note())
                st = board.playtest_state()
                self.assertEqual(st["human_70"], "dummy-pass")
            finally:
                board.ROOT = old

    def test_dummy_output_stays_out_of_live_paths(self):
        self.assertNotEqual(
            v4_dummy_sim.SESSIONS.parent.name, v4_playtest.OUT.name)


if __name__ == "__main__":
    unittest.main()
