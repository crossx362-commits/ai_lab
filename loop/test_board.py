#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""개발 보드 파서·INBOX 기록 회귀."""
from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import board  # noqa: E402


STATUS = """최종 갱신: 2026-08-16 · 루프

## 다음 할 일 (원장 §22 — 위에서부터 하나만)

1. **V2 사람 판정** — 실제 창에서 대시 무적 0.3초. 자동검사로 통과 선언 금지.
2. **V4 삭제 루프 준비** — 보스 패배→3회 사망→삭제.

> **이번 이터 결과(코드/실행): V3 한 판 종단.**
> - 코드 `ec927cbe`. 층 5→6.
"""

DESIGN = """### 현재 핵심 미완 (원장 §22 · 2026-08-16 재확인)

이미 코드에 있다.

- **V3 한 판 종단**(원장 1순위) ✅ 실행 증거 닫힘 — 다시 만들지 마라
- **V2 사람 판정**(원장 2순위) — 실제 창에서 대시 무적
- **V4 준비**(원장 3순위) — 보스 패배→삭제
"""

INBOX = """# 오너 지시함

## 처리됨 — 최신

### ✅ 확정 — 전직은 기본직업 4종에서 시작

옵션 B.

## 대기 중

### 📌 옛 기록

본문.
"""


class ParseTests(unittest.TestCase):
    def test_queue_order_and_human_flag(self):
        q = board.parse_queue(STATUS)
        self.assertEqual([x["title"] for x in q], ["V2 사람 판정", "V4 삭제 루프 준비"])
        self.assertEqual(q[0]["n"], 1)
        self.assertTrue(q[0]["human"])
        self.assertFalse(q[1]["human"])

    def test_results_take_commit(self):
        r = board.parse_results(STATUS)
        self.assertEqual(r[0]["commit"], "ec927cbe")
        self.assertIn("V3 한 판 종단", r[0]["title"])

    def test_milestones_done_flag(self):
        m = board.parse_milestones(DESIGN)
        self.assertEqual(len(m), 3)
        self.assertTrue(m[0]["done"])
        self.assertFalse(m[1]["done"])
        self.assertEqual(m[0]["title"], "V3 한 판 종단")

    def test_inbox_sections(self):
        box = board.parse_inbox(INBOX)
        self.assertTrue(box["waiting"][0]["title"].startswith("📌"))
        self.assertIn("확정", box["done"][0]["title"])


class WriteTests(unittest.TestCase):
    def test_request_lands_under_waiting(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "INBOX.md"
            path.write_text(INBOX, encoding="utf-8")
            old = board.INBOX
            try:
                board.INBOX = path
                stamp = board.write_request("스프라이트 확인", "버퍼 걷기부터.")
            finally:
                board.INBOX = old
            text = path.read_text(encoding="utf-8")
            wait = text.split("## 대기 중", 1)[1]
            done = text.split("## 처리됨", 1)[1].split("## 대기 중", 1)[0]
            self.assertIn("스프라이트 확인", wait)
            self.assertIn(stamp, wait)
            self.assertIn("버퍼 걷기부터.", wait)
            self.assertLess(wait.find("스프라이트 확인"), wait.find("옛 기록"))
            self.assertNotIn("스프라이트 확인", done)

    def test_empty_title_rejected(self):
        with self.assertRaises(ValueError):
            board.write_request("  ", "본문")


class CheckTests(unittest.TestCase):
    def test_check_roundtrip(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "checks.json"
            old = board.CHECKS_PATH
            try:
                board.CHECKS_PATH = path
                board.save_checks({"abc": {"at": "now"}})
                self.assertEqual(board.load_checks()["abc"]["at"], "now")
            finally:
                board.CHECKS_PATH = old


class CommitAllowTests(unittest.TestCase):
    def test_allow_game_and_docs(self):
        self.assertTrue(board.commit_allowed(".gitignore"))
        self.assertTrue(board.commit_allowed("docs/STATUS.md"))
        self.assertTrue(board.commit_allowed("loop/board.py"))
        self.assertTrue(board.commit_allowed("projects/ashes-to-stars/unity/Assets/Scripts/W3Party.cs"))

    def test_deny_secrets_and_cache(self):
        self.assertFalse(board.commit_allowed(".env"))
        self.assertFalse(board.commit_allowed("projects/foo/.env.encrypted"))
        self.assertFalse(board.commit_allowed("projects/ashes-to-stars/unity/Library/foo"))
        self.assertFalse(board.commit_allowed("loop/logs/iter.log"))
        self.assertFalse(board.commit_allowed("projects/ashes-to-stars/unity_meas/Assets/x.cs"))


class ResumeTests(unittest.TestCase):
    def test_resume_clears_hold_and_starts_if_down(self):
        with tempfile.TemporaryDirectory() as tmp:
            here = Path(tmp)
            (here / "HOLD").write_text("")
            (here / "STOP").write_text("")
            old = (board.HERE, board.find_loop_pids, board.start_loop)
            started = []
            board.HERE = here
            board.find_loop_pids = lambda: [99] if started else []
            board.start_loop = lambda: started.append(1) or 99
            try:
                r = board.resume_work()
            finally:
                board.HERE, board.find_loop_pids, board.start_loop = old
            self.assertFalse((here / "HOLD").exists())
            self.assertFalse((here / "STOP").exists())
            self.assertTrue(r["started"])
            self.assertEqual(r["pids"], [99])


if __name__ == "__main__":
    unittest.main()
