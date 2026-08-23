#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""개발 보드 파서·INBOX 기록 회귀."""
from __future__ import annotations

import json
import os
import sys
import tempfile
import unittest
from unittest import mock
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

    def test_queue_allows_parenthetical_after_title(self):
        text = STATUS.replace(
            "1. **V2 사람 판정** — ",
            "1. **UI 호버/눌림 육안** (오너 UI 지시) — ",
        )
        q = board.parse_queue(text)
        self.assertEqual(q[0]["title"], "UI 호버/눌림 육안")

    def test_simple_wiring_is_not_owner_gate(self):
        self.assertFalse(board.needs_human("UI 호버/눌림 육안", "필드·탑 제목 아이콘"))
        self.assertFalse(board.needs_human("1차 전직 선택지", "역할별 2/4/2/3"))
        self.assertFalse(board.needs_human("대장간 둘째 슬라이스", "강화 +15, 나머지 5부위"))
        self.assertFalse(board.needs_human("긴급 탈출 6초 캐스팅", "피격 시 취소"))
        self.assertFalse(board.needs_human("V4 외부 판정 후 확장", "70% 이상이 계속 플레이할 때만"))
        self.assertFalse(board.needs_human("V4 외부 테스터 70%", "오너 보류"))
        self.assertTrue(board.needs_human("V2 사람 판정", "자동검사로 통과 선언 금지"))
        self.assertTrue(board.needs_human("전직 분기", "오너 선택 A/B/C"))

    def test_pending_choices_only_human_gates(self):
        q = board.parse_queue(STATUS)
        extra = [
            {"id": "a", "title": "대장간 둘째", "detail": "강화", "human": False},
            {"id": "b", "title": "V4 외부 테스터 70%", "detail": "오너 보류",
             "human": board.needs_human("V4 외부 테스터 70%", "오너 보류")},
        ]
        miles = [{"id": "c", "title": "V2 사람 판정", "detail": "", "human": True, "done": False}]
        pending = board.pending_choices(q, miles, {}, extra)
        titles = [p["title"] for p in pending]
        self.assertIn("V2 사람 판정", titles)
        self.assertNotIn("대장간 둘째", titles)
        self.assertNotIn("V4 외부 테스터 70%", titles)
        self.assertTrue(all(p["human"] for p in pending))

    def test_queue_table_skips_done_rows(self):
        table = """
## 다음 할 일 큐 (맨 위부터 하나씩)

| # | 항목 | 통과 기준 | 네거티브 |
|---|---|---|---|
| 1 | **몹 AI 재생성** 남은 확인 | 매트릭스 | 옛 mob01 |
| ~~2~~ | ~~보스~~ ✅ | 끝 | 끝 |
| 4 | **영지 건물 3종** | 소비 시스템이 없어 잠금 | 거짓말 UI |
"""
        rows = board.parse_queue_table(table)
        self.assertEqual([r["title"] for r in rows], ["몹 AI 재생성", "영지 건물 3종"])

    def test_queue_table_all_counts_done_and_blocked(self):
        table = """
## 다음 할 일 큐 (맨 위부터 하나씩)

| # | 항목 | 통과 기준 | 네거티브 |
|---|---|---|---|
| 1 | **몹 AI 재생성** 남은 확인 | 매트릭스 | 옛 mob01 |
| ~~2~~ | ~~보스~~ ✅ | 끝 | 끝 |
| 4 | **영지 건물 3종** | 소비 시스템이 없어 잠금 | 거짓말 UI |
"""
        rows = board.parse_queue_table_all(table)
        self.assertEqual(len(rows), 3)
        self.assertTrue(next(r for r in rows if r["title"] == "보스")["done"])
        self.assertTrue(next(r for r in rows if "영지" in r["title"])["blocked"])
        self.assertFalse(next(r for r in rows if "몹" in r["title"])["done"])

    def test_progress_charts_uses_documents_not_guesses(self):
        status = STATUS + """
## 다음 할 일 큐 (맨 위부터 하나씩)

| # | 항목 | 통과 기준 | 네거티브 |
|---|---|---|---|
| ~~2~~ | ~~보스~~ ✅ | 끝 | 끝 |
| 4 | **영지 건물 3종** | 소비 시스템이 없어 | x |
"""
        design = DESIGN
        game = """
### 21-4. 주차별 진행

| 주차 | 목표 | 관문 |
|---|------|------|
| ~~W1~~ | ~~유닛~~ | ✅ **완료 (2026-08-13) — V1 통과** |
| **W6** | 외부 플레이테스트 | **V4 판정** |
"""
        old = board.ROOT
        with tempfile.TemporaryDirectory() as tmp:
            tmp = Path(tmp)
            (tmp / "output/qa/ashes-to-stars/v4_playtest").mkdir(parents=True)
            board.ROOT = tmp
            try:
                charts = board.progress_charts(status, design, game, {
                    "x": {"title": "V2 사람 판정", "choice": "pass"},
                })
            finally:
                board.ROOT = old
        self.assertEqual(charts["queue"]["total"], 2)
        self.assertEqual(charts["queue"]["open"], 2)
        self.assertEqual(next(g for g in charts["gates"] if g["id"] == "V2")["pct"], 100)
        self.assertEqual(next(g for g in charts["gates"] if g["id"] == "V3")["pct"], 100)
        self.assertEqual(next(g for g in charts["gates"] if g["id"] == "V4b")["pct"], 0)
        self.assertLess(next(g for g in charts["gates"] if g["id"] == "V4b")["pct"], 100)
        w1 = next(w for w in charts["weeks"] if w["id"] == "W1")
        self.assertEqual(w1["pct"], 100)
        w6 = next(w for w in charts["weeks"] if w["id"] == "W6")
        self.assertEqual(w6["pct"], 0)

    def test_now_list_from_design(self):
        doc = """
### 지금 당장 할 일 (우선순위 순)

1. **V3 보스 전투를 끝까지 연결** — 한 판에서 성립
2. **V2 사람 판정** — 창에서 피했는지

## 23. 다음
"""
        rows = board.parse_now_list(doc)
        self.assertEqual([r["title"] for r in rows],
                         ["V3 보스 전투를 끝까지 연결", "V2 사람 판정"])

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

    def test_stuck_items_blocked_not_done(self):
        table = """
## 다음 할 일 큐 (맨 위부터 하나씩)

| # | 항목 | 통과 기준 | 네거티브 |
|---|---|---|---|
| 1 | **몹 AI 재생성** 남은 확인 | 매트릭스 | 옛 mob01 |
| ~~2~~ | ~~보스~~ ✅ | 끝 | 끝 |
| 4 | **영지 건물 3종** | 소비 시스템이 없어 잠금 | 거짓말 UI |
"""
        stuck = board.stuck_items(table, {}, log_text="")
        titles = [s["title"] for s in stuck]
        self.assertIn("영지 건물 3종", titles)
        self.assertNotIn("보스", titles)
        self.assertNotIn("몹 AI 재생성", titles)
        self.assertTrue(all(s["kind"] == "blocked" for s in stuck))

    def test_stuck_items_hold_and_latest_fail_only(self):
        status = """
## 다음 할 일 (원장 §22 — 위에서부터 하나만)

1. **거래서버 경매** — 유보. 완료로 내리지 말고.
2. **V4 외부 테스터 70%** — 오너 보류.
"""
        stuck = board.stuck_items(
            status, {"blocked": ["HOLD 파일"]},
            log_text="❌ #7 실패\n✅ #8 완료\n❌ #9 타임아웃",
        )
        titles = [s["title"] for s in stuck]
        kinds = [s["kind"] for s in stuck]
        self.assertEqual(kinds[0], "loop")
        self.assertEqual(stuck[0]["title"], "HOLD 파일")
        self.assertIn("이터 #9 실패", titles)
        self.assertNotIn("이터 #7 실패", titles)
        self.assertIn("거래서버 경매", titles)
        self.assertNotIn("V4 외부 테스터 70%", titles)

    def test_stuck_items_success_clears_fail(self):
        stuck = board.stuck_items("", {}, log_text="❌ #7 실패\n✅ #8 완료")
        self.assertFalse(any(s["kind"] == "fail" for s in stuck))

    def test_board_html_stuck_after_request(self):
        html = (HERE / "board.html").read_text(encoding="utf-8")
        self.assertLess(html.find('class="request-top"'), html.find('id="stuck-box"'))
        self.assertIn("renderStuck", html)

    def test_completed_posts_description_and_shot(self):
        status = STATUS + """
> **이번 이터 결과(코드/실행): 대장간 둘째 슬라이스 — 강화 +15.**
> - 6부위 강화. 경매 거래서버는 안 열었다.
> - **화면**: `smith_enhance_shots/ok.png` 318037B — 제목 영지·대장간.
> - **코드** `783af30e`.

## 다음 할 일 큐 (맨 위부터 하나씩)

| # | 항목 | 통과 기준 | 네거티브 |
|---|---|---|---|
| ~~2~~ | ~~**보스 쫄 소환**~~ ✅ | 소환피해 24 · `shots/summon.png` | 소환 0 |
| 4 | **영지 건물 3종** | 소비 시스템이 없어 | x |
"""
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "smith_enhance_shots").mkdir()
            (root / "shots").mkdir()
            (root / "smith_enhance_shots" / "ok.png").write_bytes(b"\x89PNG")
            (root / "shots" / "summon.png").write_bytes(b"\x89PNG")
            old = board.QA_ROOT
            board.QA_ROOT = root
            try:
                posts = board.completed_posts(status)
            finally:
                board.QA_ROOT = old
        titles = [p["title"] for p in posts]
        self.assertIn("대장간 둘째 슬라이스 — 강화 +15", titles)
        smith = next(p for p in posts if "대장간" in p["title"])
        self.assertIn("6부위", smith["detail"])
        self.assertEqual(smith["commit"], "783af30e")
        self.assertEqual(smith["shots"][0]["path"], "smith_enhance_shots/ok.png")
        self.assertTrue(smith["shots"][0]["url"].startswith("/shots/"))
        self.assertIn("보스 쫄 소환", titles)
        self.assertNotIn("영지 건물 3종", titles)

    def test_black_screenshot_is_rejected(self):
        try:
            from PIL import Image
        except ImportError:
            self.skipTest("PIL 없음")
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            black = root / "black.png"
            gold = root / "gold.png"
            Image.new("RGB", (128, 72), (0, 0, 0)).save(black)
            Image.new("RGB", (128, 72), (212, 162, 76)).save(gold)
            self.assertTrue(board.shot_is_black(black))
            self.assertFalse(board.shot_is_black(gold))
            (root / "done").mkdir()
            black2 = root / "done" / "black.png"
            gold2 = root / "done" / "ok.png"
            Image.new("RGB", (128, 72), (2, 2, 2)).save(black2)
            Image.new("RGB", (128, 72), (40, 80, 120)).save(gold2)
            old = board.QA_ROOT
            board.QA_ROOT = root
            try:
                status = (
                    STATUS
                    + "\n> **이번 이터 결과(코드/실행): 검은 화면 금지.**\n"
                    + "> - **화면**: `done/black.png` 그리고 `done/ok.png`.\n"
                )
                posts = board.completed_posts(status)
            finally:
                board.QA_ROOT = old
        item = next(p for p in posts if "검은 화면" in p["title"])
        paths = [s["path"] for s in item["shots"]]
        self.assertEqual(paths, ["done/ok.png"])
        self.assertNotIn("done/black.png", paths)

    def test_hinted_shots_ignore_body_non_work(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "smith_enhance_shots").mkdir()
            (root / "v4_wipe_shots").mkdir()
            (root / "smith_enhance_shots" / "qa_go_Estate_smith.png").write_bytes(b"\x89PNG")
            (root / "v4_wipe_shots" / "qa_go:Result.png").write_bytes(b"\x89PNG")
            old = board.QA_ROOT
            board.QA_ROOT = root
            try:
                shots = board.hinted_shots(
                    "긴급 탈출 6초 캐스트·피격 취소",
                    "루프의 대장간 둘째는 안 넣었다. V4 70%는 안 열었다.",
                )
            finally:
                board.QA_ROOT = old
        self.assertEqual(shots, [])

    def test_mentioned_shots_skips_old_compare(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "party_chrome_shots").mkdir()
            (root / "shots").mkdir()
            (root / "party_chrome_shots" / "qa_go:Party.png").write_bytes(b"\x89PNG")
            (root / "shots" / "qa_go:Party.png").write_bytes(b"\x89PNG")
            old = board.QA_ROOT
            board.QA_ROOT = root
            try:
                shots = board.mentioned_shots(
                    "화면 `party_chrome_shots/qa_go:Party.png` 하트 3칸.\n"
                    "옛 `shots/qa_go:Party.png`과 갈림."
                )
            finally:
                board.QA_ROOT = old
        self.assertEqual(shots, ["party_chrome_shots/qa_go:Party.png"])

    def test_read_shot_stays_inside_qa_root(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "ok.png").write_bytes(b"\x89PNG\r\n")
            secret = Path(tmp).parent / "secret.png"
            old = board.QA_ROOT
            board.QA_ROOT = root
            try:
                data, ctype, err = board.read_shot("ok.png")
                self.assertEqual(ctype, "image/png")
                self.assertTrue(data.startswith(b"\x89PNG"))
                self.assertEqual(err, "")
                missing, _, merr = board.read_shot("../" + secret.name)
                self.assertIsNone(missing)
                self.assertTrue(merr)
            finally:
                board.QA_ROOT = old

    def test_board_html_completed_gallery(self):
        html = (HERE / "board.html").read_text(encoding="utf-8")
        self.assertLess(html.find('id="charts"'), html.find('id="done-gallery"'))
        self.assertIn("renderCompleted", html)
        self.assertIn('id="slice"', html)

    def test_board_html_queue_above_charts_and_commands_log(self):
        html = (HERE / "board.html").read_text(encoding="utf-8")
        self.assertLess(html.find('id="commands"'), html.find('id="queue"'))
        self.assertLess(html.find('id="queue"'), html.find('id="charts"'))
        self.assertLess(html.find('id="queue"'), html.find('id="choices"'))
        self.assertIn("renderCommands", html)
        self.assertIn("내가 시킨 일", html)
        self.assertIn("proto_done", html)
        self.assertIn("지금 ·", html)
        self.assertIn("테스트 하는 사람", html)
        self.assertEqual(html.count('id="queue"'), 1)
        self.assertIn('id="tests"', html)
        self.assertIn("검증 결과", html)
        self.assertIn("renderTests", html)
        self.assertLess(html.find('id="commands"'), html.find('id="tests"'))
        self.assertLess(html.find('id="tests"'), html.find('id="queue"'))


class WriteTests(unittest.TestCase):
    def test_request_lands_under_waiting(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "INBOX.md"
            path.write_text(INBOX, encoding="utf-8")
            old = (board.INBOX, board.COMMANDS_PATH)
            try:
                board.INBOX = path
                board.COMMANDS_PATH = Path(tmp) / "cmds.json"
                stamp = board.write_request("스프라이트 확인", "버퍼 걷기부터.")
                logged = board.load_commands()
                self.assertEqual(logged[0]["title"], "스프라이트 확인")
                self.assertEqual(logged[0]["source"], "board")
            finally:
                board.INBOX, board.COMMANDS_PATH = old
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


class PlainCopyTests(unittest.TestCase):
    def test_inbox_stamp_and_code_leave_the_title(self):
        t = board.humanize_title(
            "INBOX 08:47 지금 문제점",
            "캐릭터·몹 움직임, 겹치지 않게. `W3Party`라 대화 세션.",
        )
        self.assertNotIn("INBOX", t)
        self.assertNotIn("08:47", t)
        self.assertNotIn("W3Party", t)
        self.assertTrue("움직임" in t or "겹침" in t)

    def test_detail_drops_code_and_keeps_one_line(self):
        d = board.humanize_detail(
            "캐릭터·몹 움직임, 겹치지 않게. 움직임은 `W3Party`라 대화 세션. 사람 육안."
        )
        self.assertNotIn("W3Party", d)
        self.assertNotIn("`", d)
        self.assertIn("움직임", d)
        self.assertLess(len(d), 90)

    def test_hint_reads_title_not_body(self):
        t = board.humanize_title(
            "긴급 탈출 보상 포기",
            "움직임·겹침은 W3Party라 대화 세션.",
        )
        self.assertIn("탈출", t)
        self.assertNotIn("움직임", t)

    def test_loop_meta_is_not_a_description(self):
        d = board.humanize_detail("큐 1번은 움직임·겹침이 W3Party/FieldDecor라 대기하지 않음.")
        self.assertNotIn("큐 1번", d)
        self.assertNotIn("FieldDecor", d)

    def test_leftover_sentence_beats_closed_list(self):
        d = board.humanize_detail(
            "하단 도크·격자 8×8는 닫음. 캐릭터창 3열 명부·장비 둘레 라벨 잘림은 남음. "
            "`UiPages` 근거."
        )
        self.assertIn("남음", d)
        self.assertNotIn("UiPages", d)


class CommandLogTests(unittest.TestCase):
    def test_record_command_newest_first_and_cap(self):
        with tempfile.TemporaryDirectory() as tmp:
            old = board.COMMANDS_PATH
            board.COMMANDS_PATH = Path(tmp) / "cmds.json"
            try:
                board.record_command("첫번째", "a", source="chat", status="done")
                board.record_command("두번째", "b", source="chat", status="done")
                rows = board.load_commands()
                self.assertEqual([r["title"] for r in rows], ["두번째", "첫번째"])
                self.assertEqual(rows[0]["source"], "chat")
                self.assertEqual(rows[0]["status"], "done")
                old_max = board.COMMANDS_MAX
                board.COMMANDS_MAX = 2
                try:
                    board.record_command("세번째", "", source="chat", status="done")
                    self.assertEqual([r["title"] for r in board.load_commands()],
                                     ["세번째", "두번째"])
                finally:
                    board.COMMANDS_MAX = old_max
            finally:
                board.COMMANDS_PATH = old

    def test_cli_done_does_not_touch_inbox(self):
        with tempfile.TemporaryDirectory() as tmp:
            inbox = Path(tmp) / "INBOX.md"
            inbox.write_text(INBOX, encoding="utf-8")
            old = (board.INBOX, board.COMMANDS_PATH)
            try:
                board.INBOX = inbox
                board.COMMANDS_PATH = Path(tmp) / "cmds.json"
                rc = board.cli_command(
                    ["board.py", "command", "글씨 위치", "금테 안",
                     "--source", "chat", "--status", "done"])
                self.assertEqual(rc, 0)
                self.assertNotIn("글씨 위치", inbox.read_text(encoding="utf-8"))
                self.assertEqual(board.load_commands()[0]["title"], "글씨 위치")
            finally:
                board.INBOX, board.COMMANDS_PATH = old


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


class TestReportTests(unittest.TestCase):
    def test_missing_report_is_empty(self):
        with tempfile.TemporaryDirectory() as tmp:
            old = board.TEST_REPORT_PATH
            try:
                board.TEST_REPORT_PATH = Path(tmp) / "none.json"
                self.assertEqual(board.load_test_report(), {})
            finally:
                board.TEST_REPORT_PATH = old

    def test_reads_pass_and_fail_rows(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "last_test_report.json"
            path.write_text(
                json.dumps({
                    "at": "2026-08-17 11:00",
                    "ok": False,
                    "summary": "1개 실패 / 2",
                    "items": [
                        {"name": "사냥 강화 3택", "ok": True, "note": "통과"},
                        {"name": "캐릭터 겹침", "ok": False, "note": "같은 점"},
                    ],
                }, ensure_ascii=False),
                encoding="utf-8",
            )
            old = board.TEST_REPORT_PATH
            try:
                board.TEST_REPORT_PATH = path
                data = board.load_test_report()
            finally:
                board.TEST_REPORT_PATH = old
            self.assertFalse(data["ok"])
            self.assertEqual(data["summary"], "1개 실패 / 2")
            self.assertEqual(data["items"][0]["name"], "사냥 강화 3택")
            self.assertTrue(data["items"][0]["ok"])
            self.assertFalse(data["items"][1]["ok"])


    def test_bom_report_still_parses(self):
        """2026-08-18: 다른 세션이 BOM을 붙여 저장해 검증 칸이 통째로 비었다."""
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "last_test_report.json"
            path.write_bytes(
                b"\xef\xbb\xbf" + json.dumps({
                    "at": "2026-08-18 01:57",
                    "ok": True,
                    "summary": "25개 전부 통과",
                    "items": [{"name": "전투 HUD", "ok": True, "note": "통과"}],
                }, ensure_ascii=False).encode("utf-8")
            )
            old = board.TEST_REPORT_PATH
            try:
                board.TEST_REPORT_PATH = path
                data = board.load_test_report()
            finally:
                board.TEST_REPORT_PATH = old
            self.assertEqual(data["summary"], "25개 전부 통과")
            self.assertEqual(len(data["items"]), 1)


def _git_repo(tmp):
    """Git 기능 테스트용 실제 worktree + bare origin."""
    import subprocess
    root = Path(tmp) / "work"
    bare = Path(tmp) / "origin.git"
    subprocess.run(["git", "init", "-q", "--bare", str(bare)], check=True)
    subprocess.run(["git", "init", "-q", "-b", "master", str(root)], check=True)
    env = {"GIT_AUTHOR_NAME": "t", "GIT_AUTHOR_EMAIL": "t@t",
           "GIT_COMMITTER_NAME": "t", "GIT_COMMITTER_EMAIL": "t@t", **os.environ}
    (root / "docs").mkdir()
    (root / "a.txt").write_text("1", encoding="utf-8")
    (root / "docs" / "sync.txt").write_text("1", encoding="utf-8")
    subprocess.run(["git", "-C", str(root), "add", "a.txt", "docs/sync.txt"], check=True)
    subprocess.run(["git", "-C", str(root), "commit", "-qm", "one"], check=True, env=env)
    subprocess.run(["git", "-C", str(root), "config", "user.name", "t"], check=True)
    subprocess.run(["git", "-C", str(root), "config", "user.email", "t@t"], check=True)
    subprocess.run(["git", "-C", str(root), "remote", "add", "origin", str(bare)], check=True)
    subprocess.run(["git", "-C", str(root), "push", "-q", "-u", "origin", "master"], check=True)
    return root, bare, env


class PushTests(unittest.TestCase):
    """보드 푸시 버튼(오너 지시 2026-08-18) — 진짜 git 저장소로 확인한다.

    모킹하면 '올릴 게 없을 때'와 '거절될 때'를 못 잡는다(2026-07-12 교훈: Popen 성공이
    스크립트 성공이 아니었다). 임시 bare 원격을 만들어 실제로 밀어본다."""

    def _repo(self, tmp):
        root, _bare, env = _git_repo(tmp)
        return root, env

    def test_push_sends_only_when_ahead(self):
        import subprocess
        with tempfile.TemporaryDirectory() as tmp:
            root, env = self._repo(tmp)
            old = board.ROOT
            try:
                board.ROOT = root
                self.assertEqual(board.push_state()["ahead"], 0)
                with self.assertRaises(ValueError):
                    board.push_work()              # 올릴 게 없으면 밀지 않는다

                (root / "a.txt").write_text("2", encoding="utf-8")
                subprocess.run(["git", "-C", str(root), "commit", "-qam", "two"],
                               check=True, env=env)
                self.assertEqual(board.push_state()["ahead"], 1)
                result = board.push_work()
                self.assertEqual(result["pushed"], 1)
                self.assertEqual(result["ahead"], 0)
                self.assertEqual(board.push_state()["ahead"], 0)
            finally:
                board.ROOT = old

    def test_push_never_forces(self):
        """강제 푸시 금지 — 남의 커밋을 지운다. 소스에 --force가 없어야 한다."""
        src = (HERE / "board.py").read_text(encoding="utf-8")
        body = src[src.index("def _push_work"):src.index("def recent_commits")]
        self.assertIn('"git", "push", "origin", branch', body)
        for bad in ('"--force"', '"-f"', '"--force-with-lease"', '"+HEAD"'):
            self.assertNotIn(bad, body)


class GitDetailTests(unittest.TestCase):
    def test_reports_branch_divergence_and_file_breakdown(self):
        """종류 집계가 틀리면 보드의 상세 수치가 실제 worktree와 어긋난다."""
        with tempfile.TemporaryDirectory() as tmp:
            root, _bare, _env = _git_repo(tmp)
            (root / "docs" / "sync.txt").write_text("2", encoding="utf-8")
            (root / "docs" / "new.txt").write_text("new", encoding="utf-8")
            (root / ".env").write_text("SECRET=x", encoding="utf-8")
            old = board.ROOT
            try:
                board.ROOT = root
                detail = board.git_detail()
            finally:
                board.ROOT = old

        self.assertEqual(detail["branch"], "master")
        self.assertEqual(detail["upstream"], "origin/master")
        self.assertEqual(detail["ahead"], 0)
        self.assertEqual(detail["behind"], 0)
        self.assertEqual(detail["changed"], 3)
        self.assertEqual(detail["allowed"], 2)
        self.assertEqual(detail["blocked"], 1)
        self.assertEqual(detail["counts"]["modified"], 1)
        self.assertEqual(detail["counts"]["untracked"], 2)

    def test_detail_exposes_commit_facts_and_stage_counts(self):
        """상세 칸의 해시·시각·stage 수가 실제 Git 값과 다르면 안 된다."""
        import subprocess
        with tempfile.TemporaryDirectory() as tmp:
            root, _bare, _env = _git_repo(tmp)
            (root / "docs" / "sync.txt").write_text("2", encoding="utf-8")
            subprocess.run(
                ["git", "-C", str(root), "add", "docs/sync.txt"], check=True)
            (root / "docs" / "new.txt").write_text("new", encoding="utf-8")
            expected_hash = subprocess.check_output(
                ["git", "-C", str(root), "rev-parse", "--short", "HEAD"],
                text=True, encoding="utf-8",
            ).strip()
            old = board.ROOT
            try:
                board.ROOT = root
                detail = board.git_detail()
            finally:
                board.ROOT = old

        self.assertEqual(detail["status"], "origin과 같음")
        self.assertEqual(detail["staged"], 1)
        self.assertEqual(detail["unstaged"], 1)
        self.assertEqual(detail["local"]["hash"], expected_hash)
        self.assertEqual(detail["remote"]["hash"], expected_hash)
        self.assertTrue(detail["local"]["when"])
        self.assertEqual(detail["local"]["subject"], "one")

    def test_git_summary_omits_heavy_file_array(self):
        """8초 상태 응답에 전체 파일을 넣으면 숨긴 화면도 계속 대량 렌더된다."""
        with tempfile.TemporaryDirectory() as tmp:
            root, _bare, _env = _git_repo(tmp)
            (root / "docs" / "new.txt").write_text("new", encoding="utf-8")
            old = board.ROOT
            try:
                board.ROOT = root
                summary = board.git_summary()
            finally:
                board.ROOT = old

        self.assertNotIn("files", summary)
        self.assertEqual(summary["changed"], 1)
        self.assertEqual(summary["allowed"], 1)
        self.assertEqual(summary["counts"]["untracked"], 1)
        self.assertTrue(summary["change_id"])

    def test_detail_and_sync_require_configured_upstream(self):
        """origin ref가 있어도 실제 upstream 설정이 없으면 커밋·푸시하지 않는다."""
        import subprocess
        with tempfile.TemporaryDirectory() as tmp:
            root, _bare, _env = _git_repo(tmp)
            subprocess.run(
                ["git", "-C", str(root), "branch", "--unset-upstream"], check=True)
            (root / "docs" / "sync.txt").write_text("2", encoding="utf-8")
            before = subprocess.check_output(
                ["git", "-C", str(root), "rev-parse", "HEAD"],
                text=True, encoding="utf-8",
            ).strip()
            old = board.ROOT
            try:
                board.ROOT = root
                detail = board.git_detail()
                with self.assertRaisesRegex(ValueError, "원격 추적이 없다"):
                    board.sync_work("test: no upstream")
            finally:
                board.ROOT = old
            after = subprocess.check_output(
                ["git", "-C", str(root), "rev-parse", "HEAD"],
                text=True, encoding="utf-8",
            ).strip()

        self.assertEqual(detail["upstream"], "")
        self.assertEqual(detail["status"], "원격 추적 없음")
        self.assertEqual(detail["remote"]["hash"], "")
        self.assertEqual(after, before)

    def test_summary_change_id_changes_when_only_path_changes(self):
        """집계가 같아도 변경 파일이 바뀌면 열린 상세 목록을 다시 받아야 한다."""
        import subprocess
        with tempfile.TemporaryDirectory() as tmp:
            root, _bare, env = _git_repo(tmp)
            for name in ("a.txt", "b.txt"):
                (root / "docs" / name).write_text("base", encoding="utf-8")
            subprocess.run(["git", "-C", str(root), "add", "docs"], check=True)
            subprocess.run(
                ["git", "-C", str(root), "commit", "-qm", "tracked paths"],
                check=True, env=env,
            )
            (root / "docs" / "a.txt").write_text("changed", encoding="utf-8")
            old = board.ROOT
            try:
                board.ROOT = root
                first = board.git_summary()
                subprocess.run(
                    ["git", "-C", str(root), "checkout", "--", "docs/a.txt"],
                    check=True,
                )
                (root / "docs" / "b.txt").write_text("changed", encoding="utf-8")
                second = board.git_summary()
            finally:
                board.ROOT = old

        self.assertEqual(first["changed"], second["changed"])
        self.assertEqual(first["counts"], second["counts"])
        self.assertNotEqual(first["change_id"], second["change_id"])

    def test_git_status_failure_is_explicit_and_sync_fails_closed(self):
        """git status 실패를 변경 없음으로 표시하거나 동기화하면 안 된다."""
        import subprocess
        from unittest import mock
        with tempfile.TemporaryDirectory() as tmp:
            root, _bare, _env = _git_repo(tmp)
            real_check_output = board.subprocess.check_output

            def fail_status(cmd, *args, **kwargs):
                if cmd[:3] == ["git", "status", "--porcelain=v1"]:
                    raise subprocess.CalledProcessError(1, cmd)
                return real_check_output(cmd, *args, **kwargs)

            old = board.ROOT
            try:
                board.ROOT = root
                with mock.patch.object(board.subprocess, "check_output", side_effect=fail_status):
                    detail = board.git_detail()
                    with self.assertRaisesRegex(ValueError, "작업 트리 상태 확인 실패"):
                        board.sync_work()
            finally:
                board.ROOT = old

        self.assertFalse(detail["ok"])
        self.assertIn("작업 트리 상태 확인 실패", detail["status"])

    def test_dirty_files_preserves_korean_path(self):
        """Git의 C식 인용 문자열을 경로로 쓰면 화면과 git add가 모두 깨진다."""
        with tempfile.TemporaryDirectory() as tmp:
            root, _bare, _env = _git_repo(tmp)
            path = root / "docs" / "한글 파일.txt"
            path.write_text("내용", encoding="utf-8")
            old = board.ROOT
            try:
                board.ROOT = root
                files = board.dirty_files()
            finally:
                board.ROOT = old

        item = next(row for row in files if row["kind"] == "untracked")
        self.assertEqual(item["path"], "docs/한글 파일.txt")
        self.assertTrue(item["allowed"])

    def test_sync_commits_allowed_files_and_pushes_real_origin(self):
        """동기화가 제외 파일까지 커밋하거나 origin에 안 올리면 실패한다."""
        import subprocess
        with tempfile.TemporaryDirectory() as tmp:
            root, bare, _env = _git_repo(tmp)
            (root / "docs" / "sync.txt").write_text("2", encoding="utf-8")
            (root / ".env").write_text("SECRET=x", encoding="utf-8")
            old = board.ROOT
            try:
                board.ROOT = root
                result = board.sync_work("test: board sync")
                detail = board.git_detail()
            finally:
                board.ROOT = old

            remote_body = subprocess.check_output(
                ["git", "--git-dir", str(bare), "show", "master:docs/sync.txt"],
                text=True, encoding="utf-8",
            ).strip()
            remote_files = subprocess.check_output(
                ["git", "--git-dir", str(bare), "ls-tree", "-r", "--name-only", "master"],
                text=True, encoding="utf-8",
            ).splitlines()

        self.assertEqual(result["action"], "synced")
        self.assertEqual(result["pushed"], 1)
        self.assertTrue(result["commit"]["hash"])
        self.assertEqual(remote_body, "2")
        self.assertNotIn(".env", remote_files)
        self.assertEqual(detail["ahead"], 0)
        self.assertEqual(detail["behind"], 0)
        self.assertEqual(detail["blocked"], 1)

    def test_sync_clean_repo_is_noop(self):
        """변경이 없는데 커밋·푸시를 시도하면 버튼이 거짓 실패를 보여 준다."""
        with tempfile.TemporaryDirectory() as tmp:
            root, _bare, _env = _git_repo(tmp)
            old = board.ROOT
            try:
                board.ROOT = root
                result = board.sync_work()
            finally:
                board.ROOT = old

        self.assertEqual(result["action"], "noop")
        self.assertIsNone(result["commit"])
        self.assertEqual(result["pushed"], 0)
        self.assertEqual(result["git"]["ahead"], 0)
        self.assertEqual(result["git"]["behind"], 0)

    def test_sync_status_records_latest_success(self):
        """주기 갱신 뒤에도 완료 시각·결과를 서버 사실로 다시 그릴 수 있어야 한다."""
        with tempfile.TemporaryDirectory() as tmp:
            root, _bare, _env = _git_repo(tmp)
            old = board.ROOT
            try:
                board.ROOT = root
                result = board.sync_work()
                status = board.git_sync_status()
            finally:
                board.ROOT = old

        self.assertEqual(result["action"], "noop")
        self.assertFalse(status["busy"])
        self.assertTrue(status["ok"])
        self.assertEqual(status["action"], "noop")
        self.assertTrue(status["at"])
        self.assertIn("최신", status["message"])

    def test_sync_refuses_pre_staged_blocked_file(self):
        """다른 세션이 stage한 제외 파일을 commit_work가 함께 삼키면 안 된다."""
        import subprocess
        with tempfile.TemporaryDirectory() as tmp:
            root, bare, _env = _git_repo(tmp)
            (root / "docs" / "sync.txt").write_text("2", encoding="utf-8")
            (root / ".env").write_text("SECRET=x", encoding="utf-8")
            subprocess.run(["git", "-C", str(root), "add", ".env"], check=True)
            before = subprocess.check_output(
                ["git", "-C", str(root), "rev-parse", "HEAD"], text=True,
                encoding="utf-8",
            ).strip()
            old = board.ROOT
            try:
                board.ROOT = root
                with self.assertRaisesRegex(ValueError, "제외 파일.*스테이징"):
                    board.sync_work("test: must refuse")
            finally:
                board.ROOT = old

            after = subprocess.check_output(
                ["git", "-C", str(root), "rev-parse", "HEAD"], text=True,
                encoding="utf-8",
            ).strip()
            remote_files = subprocess.check_output(
                ["git", "--git-dir", str(bare), "ls-tree", "-r", "--name-only", "master"],
                text=True, encoding="utf-8",
            ).splitlines()

        self.assertEqual(after, before)
        self.assertNotIn(".env", remote_files)

    def test_sync_fetches_then_stops_when_origin_is_ahead(self):
        """원격이 앞선 상태에서 자동 commit/pull/merge를 하면 동시 작업을 덮는다."""
        import subprocess
        with tempfile.TemporaryDirectory() as tmp:
            root, bare, env = _git_repo(tmp)
            peer = Path(tmp) / "peer"
            subprocess.run(
                ["git", "clone", "-q", "-b", "master", str(bare), str(peer)], check=True)
            (peer / "a.txt").write_text("remote", encoding="utf-8")
            subprocess.run(["git", "-C", str(peer), "add", "a.txt"], check=True)
            subprocess.run(
                ["git", "-C", str(peer), "commit", "-qm", "remote"], check=True, env=env)
            subprocess.run(["git", "-C", str(peer), "push", "-q"], check=True)
            (root / "docs" / "sync.txt").write_text("local", encoding="utf-8")
            before = subprocess.check_output(
                ["git", "-C", str(root), "rev-parse", "HEAD"],
                text=True, encoding="utf-8",
            ).strip()
            old = board.ROOT
            try:
                board.ROOT = root
                with self.assertRaisesRegex(ValueError, "원격이 1개 앞서"):
                    board.sync_work("test: must stop")
                detail = board.git_detail()
            finally:
                board.ROOT = old
            after = subprocess.check_output(
                ["git", "-C", str(root), "rev-parse", "HEAD"],
                text=True, encoding="utf-8",
            ).strip()
            merge_started = (root / ".git" / "MERGE_HEAD").exists()

        self.assertEqual(after, before)
        self.assertEqual(detail["behind"], 1)
        self.assertEqual(detail["ahead"], 0)
        self.assertFalse(merge_started)

    def test_sync_refuses_non_origin_upstream(self):
        """다른 upstream을 비교하고 origin에 push하면 성공 수치가 거짓이 된다."""
        import subprocess
        with tempfile.TemporaryDirectory() as tmp:
            root, bare, _env = _git_repo(tmp)
            backup = Path(tmp) / "backup.git"
            subprocess.run(["git", "clone", "-q", "--bare", str(bare), str(backup)], check=True)
            subprocess.run(
                ["git", "-C", str(root), "remote", "add", "backup", str(backup)], check=True)
            subprocess.run(["git", "-C", str(root), "fetch", "-q", "backup"], check=True)
            subprocess.run(
                ["git", "-C", str(root), "branch", "--set-upstream-to", "backup/master"],
                check=True, stdout=subprocess.DEVNULL,
            )
            (root / "docs" / "sync.txt").write_text("2", encoding="utf-8")
            before = subprocess.check_output(
                ["git", "-C", str(root), "rev-parse", "HEAD"],
                text=True, encoding="utf-8",
            ).strip()
            old = board.ROOT
            try:
                board.ROOT = root
                with self.assertRaisesRegex(ValueError, "origin이 아닌 원격"):
                    board.sync_work("test: wrong remote")
            finally:
                board.ROOT = old
            after = subprocess.check_output(
                ["git", "-C", str(root), "rev-parse", "HEAD"],
                text=True, encoding="utf-8",
            ).strip()

        self.assertEqual(after, before)

    def test_sync_refuses_different_origin_upstream_branch(self):
        """master가 origin/other를 추적할 때 origin/master로 잘못 push하면 안 된다."""
        import subprocess
        with tempfile.TemporaryDirectory() as tmp:
            root, _bare, _env = _git_repo(tmp)
            subprocess.run(["git", "-C", str(root), "branch", "other"], check=True)
            subprocess.run(
                ["git", "-C", str(root), "push", "-q", "origin", "other"], check=True)
            subprocess.run(
                ["git", "-C", str(root), "branch", "--set-upstream-to", "origin/other"],
                check=True, stdout=subprocess.DEVNULL,
            )
            (root / "docs" / "sync.txt").write_text("2", encoding="utf-8")
            before = subprocess.check_output(
                ["git", "-C", str(root), "rev-parse", "HEAD"],
                text=True, encoding="utf-8",
            ).strip()
            old = board.ROOT
            try:
                board.ROOT = root
                with self.assertRaisesRegex(ValueError, "다른 원격 브랜치"):
                    board.sync_work("test: mismatched upstream")
            finally:
                board.ROOT = old
            after = subprocess.check_output(
                ["git", "-C", str(root), "rev-parse", "HEAD"],
                text=True, encoding="utf-8",
            ).strip()

        self.assertEqual(after, before)

    def test_concurrent_sync_returns_busy_without_second_git_run(self):
        """두 탭의 연속 클릭이 fetch·commit을 두 번 실행하면 안 된다."""
        import threading
        with tempfile.TemporaryDirectory() as tmp:
            root, _bare, _env = _git_repo(tmp)
            entered = threading.Event()
            release = threading.Event()
            calls = []
            errors = []
            old = (board.ROOT, board._fetch_origin)

            def slow_fetch(branch):
                calls.append(branch)
                if len(calls) == 1:
                    entered.set()
                    release.wait(3)

            def first_sync():
                try:
                    board.sync_work()
                except Exception as exc:  # pragma: no cover - 실패 내용을 주 스레드에서 확인
                    errors.append(exc)

            board.ROOT = root
            board._fetch_origin = slow_fetch
            worker = threading.Thread(target=first_sync)
            worker.start()
            try:
                self.assertTrue(entered.wait(2), "첫 동기화가 fetch에 진입하지 않음")
                with self.assertRaisesRegex(ValueError, "이미 진행 중"):
                    board.sync_work()
            finally:
                release.set()
                worker.join(4)
                board.ROOT, board._fetch_origin = old

        self.assertEqual(calls, ["master"])
        self.assertEqual(errors, [])

    def test_sync_lock_blocks_manual_commit(self):
        """sync의 fetch 중 수동 commit이 index와 HEAD를 바꾸면 안 된다."""
        import threading
        with tempfile.TemporaryDirectory() as tmp:
            root, _bare, _env = _git_repo(tmp)
            (root / "docs" / "sync.txt").write_text("2", encoding="utf-8")
            entered = threading.Event()
            release = threading.Event()
            old = (board.ROOT, board._fetch_origin)

            def slow_fetch(_branch):
                entered.set()
                release.wait(3)

            board.ROOT = root
            board._fetch_origin = slow_fetch
            worker = threading.Thread(target=board.sync_work)
            worker.start()
            try:
                self.assertTrue(entered.wait(2))
                with self.assertRaisesRegex(ValueError, "깃 작업이 이미 진행 중"):
                    board.commit_work("test: must wait")
            finally:
                release.set()
                worker.join(4)
                board.ROOT, board._fetch_origin = old

    def test_sync_lock_blocks_manual_push(self):
        """sync의 fetch 중 수동 push가 원격을 먼저 바꾸면 안 된다."""
        import subprocess
        import threading
        with tempfile.TemporaryDirectory() as tmp:
            root, _bare, env = _git_repo(tmp)
            (root / "a.txt").write_text("ahead", encoding="utf-8")
            subprocess.run(
                ["git", "-C", str(root), "commit", "-qam", "ahead"],
                check=True, env=env,
            )
            entered = threading.Event()
            release = threading.Event()
            old = (board.ROOT, board._fetch_origin)

            def slow_fetch(_branch):
                entered.set()
                release.wait(3)

            board.ROOT = root
            board._fetch_origin = slow_fetch
            worker = threading.Thread(target=board.sync_work)
            worker.start()
            try:
                self.assertTrue(entered.wait(2))
                with self.assertRaisesRegex(ValueError, "깃 작업이 이미 진행 중"):
                    board.push_work()
            finally:
                release.set()
                worker.join(4)
                board.ROOT, board._fetch_origin = old


class GitApiTests(unittest.TestCase):
    def _server(self, root):
        import threading
        old = board.ROOT
        board.ROOT = root
        server = board.ThreadingHTTPServer(("127.0.0.1", 0), board.Handler)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        return old, server, thread

    def test_get_git_returns_lazy_file_details(self):
        """상세를 열 때만 실제 파일 목록을 받고 한국어 경로도 그대로 보여야 한다."""
        import urllib.request
        with tempfile.TemporaryDirectory() as tmp:
            root, _bare, _env = _git_repo(tmp)
            (root / "docs" / "한글 파일.txt").write_text("내용", encoding="utf-8")
            old, server, thread = self._server(root)
            try:
                url = f"http://127.0.0.1:{server.server_port}/api/git"
                with urllib.request.urlopen(url, timeout=3) as response:
                    data = json.loads(response.read().decode("utf-8"))
                    cache = response.headers.get("Cache-Control")
            finally:
                server.shutdown()
                server.server_close()
                thread.join(3)
                board.ROOT = old

        self.assertTrue(data["ok"])
        self.assertEqual(cache, "no-store")
        self.assertEqual(data["git"]["branch"], "master")
        self.assertEqual(data["git"]["changed"], 1)
        self.assertEqual(data["git"]["files"][0]["path"], "docs/한글 파일.txt")
        self.assertIn("busy", data["sync"])

    def test_post_sync_commits_and_pushes_through_http(self):
        """화면 버튼의 실제 HTTP 경로가 함수만 존재하고 끊겨 있으면 안 된다."""
        import subprocess
        import urllib.request
        with tempfile.TemporaryDirectory() as tmp:
            root, bare, _env = _git_repo(tmp)
            (root / "docs" / "sync.txt").write_text("api", encoding="utf-8")
            old, server, thread = self._server(root)
            try:
                url = f"http://127.0.0.1:{server.server_port}/api/sync"
                request = urllib.request.Request(
                    url,
                    data=json.dumps({"message": "test: api sync"}).encode("utf-8"),
                    headers={"Content-Type": "application/json"},
                    method="POST",
                )
                with urllib.request.urlopen(request, timeout=5) as response:
                    data = json.loads(response.read().decode("utf-8"))
            finally:
                server.shutdown()
                server.server_close()
                thread.join(3)
                board.ROOT = old
            remote_body = subprocess.check_output(
                ["git", "--git-dir", str(bare), "show", "master:docs/sync.txt"],
                text=True, encoding="utf-8",
            ).strip()

        self.assertTrue(data["ok"])
        self.assertEqual(data["action"], "synced")
        self.assertEqual(data["pushed"], 1)
        self.assertEqual(remote_body, "api")

    def test_cross_origin_sync_is_rejected_and_default_is_loopback(self):
        """다른 웹사이트가 로컬 보드에 요청을 보내 commit하면 안 된다."""
        import subprocess
        import urllib.error
        import urllib.request
        with tempfile.TemporaryDirectory() as tmp:
            root, _bare, _env = _git_repo(tmp)
            (root / "docs" / "sync.txt").write_text("csrf", encoding="utf-8")
            before = subprocess.check_output(
                ["git", "-C", str(root), "rev-parse", "HEAD"],
                text=True, encoding="utf-8",
            ).strip()
            old, server, thread = self._server(root)
            try:
                url = f"http://127.0.0.1:{server.server_port}/api/sync"
                request = urllib.request.Request(
                    url,
                    data=b"{}",
                    headers={
                        "Content-Type": "application/json",
                        "Origin": "https://evil.example",
                    },
                    method="POST",
                )
                with self.assertRaises(urllib.error.HTTPError) as caught:
                    urllib.request.urlopen(request, timeout=3)
                code = caught.exception.code
                caught.exception.close()
            finally:
                server.shutdown()
                server.server_close()
                thread.join(3)
                board.ROOT = old
            after = subprocess.check_output(
                ["git", "-C", str(root), "rev-parse", "HEAD"],
                text=True, encoding="utf-8",
            ).strip()

        source = (HERE / "board.py").read_text(encoding="utf-8")
        self.assertEqual(code, 403)
        self.assertEqual(after, before)
        self.assertIn('os.getenv("BOARD_HOST", "127.0.0.1")', source)

    def test_page_exposes_unique_accessible_git_controls(self):
        """화면에 고유 앵커와 실시간 상태 영역이 없으면 육안·E2E 검증이 불가능하다."""
        from html.parser import HTMLParser
        import urllib.request

        class IdParser(HTMLParser):
            def __init__(self):
                super().__init__()
                self.nodes = {}
                self.duplicates = set()
                self.stack = []

            _void = {"area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "source", "track", "wbr"}

            def handle_starttag(self, tag, attrs):
                attrs = dict(attrs)
                key = attrs.get("id")
                if key:
                    if key in self.nodes:
                        self.duplicates.add(key)
                    self.nodes[key] = {
                        "tag": tag, "ancestors": tuple(x for x in self.stack if x), **attrs,
                    }
                if tag not in self._void:
                    self.stack.append(key)

            def handle_endtag(self, tag):
                if self.stack:
                    self.stack.pop()

        with tempfile.TemporaryDirectory() as tmp:
            root, _bare, _env = _git_repo(tmp)
            old, server, thread = self._server(root)
            try:
                url = f"http://127.0.0.1:{server.server_port}/"
                with urllib.request.urlopen(url, timeout=3) as response:
                    html = response.read().decode("utf-8")
            finally:
                server.shutdown()
                server.server_close()
                thread.join(3)
                board.ROOT = old

        parser = IdParser()
        parser.feed(html)
        self.assertEqual(parser.duplicates, set())
        self.assertEqual(parser.nodes["btn-git-sync"]["tag"], "button")
        self.assertEqual(parser.nodes["btn-git-detail"]["aria-controls"], "git-detail-dialog")
        self.assertEqual(parser.nodes["git-detail-dialog"]["tag"], "dialog")
        self.assertEqual(parser.nodes["git-sync-msg"]["role"], "status")
        self.assertEqual(parser.nodes["git-sync-msg"]["aria-live"], "polite")
        self.assertIn("git-head-actions", parser.nodes["btn-git-sync"]["ancestors"])
        self.assertIn("git-head-actions", parser.nodes["btn-git-detail"]["ancestors"])

    def test_live_event_stream_drives_page_refresh(self):
        """외부 Git 변경은 브라우저 타이머가 멈춰도 서버 신호로 반영돼야 한다."""
        import urllib.request
        with tempfile.TemporaryDirectory() as tmp:
            root, _bare, _env = _git_repo(tmp)
            old, server, thread = self._server(root)
            try:
                url = f"http://127.0.0.1:{server.server_port}/api/events?once=1"
                with urllib.request.urlopen(url, timeout=3) as response:
                    body = response.read().decode("utf-8")
                    content_type = response.headers.get("Content-Type")
                    cache = response.headers.get("Cache-Control")
                page_url = f"http://127.0.0.1:{server.server_port}/"
                with urllib.request.urlopen(page_url, timeout=3) as response:
                    html = response.read().decode("utf-8")
            finally:
                server.shutdown()
                server.server_close()
                thread.join(3)
                board.ROOT = old

        self.assertEqual(content_type, "text/event-stream; charset=utf-8")
        self.assertEqual(cache, "no-cache")
        self.assertIn("event: refresh\n", body)
        self.assertIn('new EventSource("/api/events")', html)
        self.assertIn('addEventListener("refresh"', html)


class LiveLogTests(unittest.TestCase):
    """2026-08-18 사고: 파이프로 띄운 루프의 출력이 loop_main.log에 안 들어가
    보드의 '지금'이 하루 전에 멈췄다. 메인 로그가 낡으면 이터 로그를 본다."""

    def _flags(self, tmp: Path) -> dict:
        old_here, old_root = board.HERE, board.ROOT
        try:
            board.HERE = tmp
            board.ROOT = tmp.parent
            return board.loop_flags()
        finally:
            board.HERE, board.ROOT = old_here, old_root

    def test_launchd_loop_path_with_spaces_counts_as_running(self):
        process_table = (
            "66332 /bin/bash /Users/junholee/Library/Application Support/"
            "AI Lab Autonomous Loop/loop.sh /Users/junholee/ai_lab\n"
        )
        with mock.patch.object(board.subprocess, "check_output", return_value=process_table):
            self.assertEqual(board.find_loop_pids(), [66332])

    def test_current_loop_reads_root_dated_logs(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            here = root / "loop"
            dated = root / "logs" / "2026-08-23"
            here.mkdir()
            dated.mkdir(parents=True)
            (root / "logs" / "loop_main.log").write_text("현재 메인\n", encoding="utf-8")
            lap = dated / "lap-20260823-134128-1.log"
            lap.write_text("현재 작업 중\n", encoding="utf-8")
            with mock.patch.object(board, "find_loop_pids", return_value=[66332]):
                flags = self._flags(here)
        self.assertTrue(flags["running"])
        self.assertEqual(flags["latest_iter"], "lap-20260823-134128-1.log")
        self.assertIn(flags["log_from"], {"loop_main.log", "lap-20260823-134128-1.log"})

    def test_stale_main_log_falls_back_to_iter_log(self):
        with tempfile.TemporaryDirectory() as tmp:
            here = Path(tmp) / "loop"
            (here / "logs").mkdir(parents=True)
            main = here / "loop_main.log"
            main.write_text("▶ 이터레이션 #94  12:52:14  → 어제것\n", encoding="utf-8")
            os.utime(main, (1_700_000_000, 1_700_000_000))
            it = here / "logs" / "iter_20260818_202138.log"
            it.write_text("지금 돌고 있는 줄\n", encoding="utf-8")
            flags = self._flags(here)
            self.assertEqual(flags["log_from"], "iter_20260818_202138.log")
            self.assertIn("지금 돌고 있는 줄", flags["log_tail"])
            self.assertEqual(flags["now"]["started"], "20:21:38")

    def test_fresh_main_log_wins(self):
        with tempfile.TemporaryDirectory() as tmp:
            here = Path(tmp) / "loop"
            (here / "logs").mkdir(parents=True)
            it = here / "logs" / "iter_20260818_202138.log"
            it.write_text("옛 이터\n", encoding="utf-8")
            os.utime(it, (1_700_000_000, 1_700_000_000))
            (here / "loop_main.log").write_text("메인이 최신\n", encoding="utf-8")
            flags = self._flags(here)
            self.assertEqual(flags["log_from"], "loop_main.log")
            self.assertIn("메인이 최신", flags["log_tail"])

    def test_glance_shows_live_freshness(self):
        """한 화면(한눈에)이 '지금'을 말하려면 로그 신선도가 화면에 나와야 한다."""
        html = (HERE / "board.html").read_text(encoding="utf-8")
        for hook in ('id="glance"', "log_age_sec", "hide-compact", "body.compact",
                     'id="g-stuck"'):
            self.assertIn(hook, html)
        self.assertIn("stuck-box hide-compact", html)
        doc = (HERE / "board.py").read_text(encoding="utf-8")
        self.assertIn("한 화면이 철칙", doc)
        self.assertIn('"log_age_sec"', doc)

    def test_loop_standing_polish_uses_existing_art(self):
        """오너 2026-08-18: 상시 폴리싱. 중복 재생성이 금지이지 5직업·몹 금지가 아니다. 할로우 강제는 취소."""
        sh = (HERE / "loop.sh").read_text(encoding="utf-8")
        self.assertIn("상시 폴리싱", sh)
        self.assertIn("중복 리소스 재생성 금지", sh)
        self.assertIn("할로우 나이트 화풍 강제는 취소", sh)
        self.assertIn("영지 화면·EstateYard", sh)
        self.assertNotIn("기본 5직업·몹 실루엣은 다시 뽑지 않는다", sh)

    def test_loop_sh_writes_main_log_itself(self):
        """띄우는 쪽에 로그를 맡기지 마라 — loop.sh가 직접 tee 한다."""
        sh = (HERE / "loop.sh").read_text(encoding="utf-8")
        self.assertIn('tee -a "$MAIN_LOG"', sh)
        doc = (HERE / "board.py").read_text(encoding="utf-8")
        start = doc[doc.index("def start_loop"):doc.index("def resume_work")]
        self.assertIn("stdout=subprocess.DEVNULL", start)


class BoardManageTests(unittest.TestCase):
    """오너 2026-08-17: 보드 규칙을 되돌리면 이 테스트가 먼저 깨진다."""

    def test_standing_rules_are_still_in_force(self):
        doc = (HERE / "board.py").read_text(encoding="utf-8")
        html = (HERE / "board.html").read_text(encoding="utf-8")
        self.assertIn("board.py command", doc)
        self.assertIn("한 화면이 철칙", doc)
        self.assertIn("아나", doc + (HERE / "v4_testers.json").read_text(encoding="utf-8"))
        self.assertIn("검은 화면", doc)
        self.assertLess(html.find('id="queue"'), html.find('id="charts"'))
        self.assertIn("할 일 적기", html)
        self.assertIn("내가 시킨 일", html)
        self.assertIn("테스트 하는 사람", html)
        self.assertIn("검증 결과", html)
        self.assertTrue(callable(board.load_test_report))
        self.assertTrue(callable(board.record_command))
        self.assertTrue(callable(board.humanize_title))
        self.assertTrue(callable(board.shot_is_black))
        self.assertTrue(callable(board.pick_current_stage))
        kit = json.loads((HERE / "v4_testers.json").read_text(encoding="utf-8"))
        self.assertEqual([t["name"] for t in kit["testers"]], ["아나"])


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

    def test_exact_allow_entries_do_not_accept_suffixes(self):
        """정확한 파일 허용값을 접두사로 읽으면 백업·시크릿 사본이 섞인다."""
        self.assertFalse(board.commit_allowed(".gitignore.backup"))
        self.assertFalse(board.commit_allowed("loop/board.py.bak"))
        self.assertFalse(board.commit_allowed("projects/ashes-to-stars/CLAUDE.md.secret"))

    def test_commit_refuses_pre_staged_blocked_file(self):
        """수동 커밋도 기존 stage의 제외 파일을 함께 삼키면 안 된다."""
        import subprocess
        with tempfile.TemporaryDirectory() as tmp:
            root, _bare, _env = _git_repo(tmp)
            (root / "docs" / "sync.txt").write_text("2", encoding="utf-8")
            (root / ".env").write_text("SECRET=x", encoding="utf-8")
            subprocess.run(["git", "-C", str(root), "add", ".env"], check=True)
            old = board.ROOT
            try:
                board.ROOT = root
                with self.assertRaisesRegex(ValueError, "제외 파일.*스테이징"):
                    board.commit_work("test: must refuse")
            finally:
                board.ROOT = old

            committed = subprocess.check_output(
                ["git", "--git-dir", str(_bare), "ls-tree", "-r", "--name-only", "master"],
                text=True, encoding="utf-8",
            ).splitlines()
        self.assertNotIn(".env", committed)


class DecisionTests(unittest.TestCase):
    def test_rewrite_queue_removes_and_renumbers(self):
        q = board.parse_queue(STATUS)
        remain = [x for x in q if x["title"] != "V2 사람 판정"]
        out = board.rewrite_queue(STATUS, remain, "> **오너 선택: V2 → 통과.**")
        nq = board.parse_queue(out)
        self.assertEqual([x["title"] for x in nq], ["V4 삭제 루프 준비"])
        self.assertEqual(nq[0]["n"], 1)
        self.assertIn("오너 선택: V2 → 통과", out)

    def test_apply_pass_writes_inbox_and_drops_queue(self):
        with tempfile.TemporaryDirectory() as tmp:
            tmp = Path(tmp)
            status_p = tmp / "STATUS.md"
            inbox_p = tmp / "INBOX.md"
            dec_p = tmp / "dec.json"
            status_p.write_text(STATUS, encoding="utf-8")
            inbox_p.write_text(INBOX, encoding="utf-8")
            old = (board.STATUS, board.INBOX, board.DESIGN, board.DECISIONS_PATH,
                   board.COMMANDS_PATH)
            board.STATUS, board.INBOX, board.DECISIONS_PATH = status_p, inbox_p, dec_p
            board.COMMANDS_PATH = tmp / "cmds.json"
            board.DESIGN = tmp / "DESIGN.md"
            board.DESIGN.write_text(DESIGN, encoding="utf-8")
            try:
                q = board.parse_queue(STATUS)
                v2 = next(x for x in q if x["title"] == "V2 사람 판정")
                r = board.apply_decision(v2["id"], "pass", "창에서 피한 느낌 있음")
                self.assertEqual(r["choice"], "pass")
                self.assertEqual([x["title"] for x in board.parse_queue(status_p.read_text(encoding="utf-8"))],
                                 ["V4 삭제 루프 준비"])
                inbox = inbox_p.read_text(encoding="utf-8")
                wait = inbox.split("## 대기 중", 1)[1]
                self.assertIn("오너 판정 — V2 사람 판정", wait)
                self.assertIn("통과", wait)
                self.assertIn("창에서 피한 느낌 있음", wait)
                pending = board.pending_choices(
                    board.parse_queue(status_p.read_text(encoding="utf-8")),
                    board.parse_milestones(DESIGN),
                    board.load_decisions(),
                )
                self.assertFalse(any(p["title"] == "V2 사람 판정" for p in pending))
                # 큐에 같은 제목이 다시 있으면 예전 통과로 숨기지 않는다
                again = board.pending_choices(
                    [{"id": v2["id"], "title": "V2 사람 판정", "detail": "사람", "human": True}],
                    [],
                    board.load_decisions(),
                )
                self.assertTrue(any(p["title"] == "V2 사람 판정" for p in again))
                self.assertEqual(board.load_commands()[0]["source"], "decide")
            finally:
                board.STATUS, board.INBOX, board.DESIGN, board.DECISIONS_PATH, board.COMMANDS_PATH = old

    def test_retry_keeps_queue(self):
        with tempfile.TemporaryDirectory() as tmp:
            tmp = Path(tmp)
            status_p = tmp / "STATUS.md"
            inbox_p = tmp / "INBOX.md"
            status_p.write_text(STATUS, encoding="utf-8")
            inbox_p.write_text(INBOX, encoding="utf-8")
            old = (board.STATUS, board.INBOX, board.DESIGN, board.DECISIONS_PATH,
                   board.COMMANDS_PATH)
            board.STATUS, board.INBOX = status_p, inbox_p
            board.DESIGN = tmp / "DESIGN.md"
            board.DESIGN.write_text(DESIGN, encoding="utf-8")
            board.DECISIONS_PATH = tmp / "dec.json"
            board.COMMANDS_PATH = tmp / "cmds.json"
            try:
                v2 = next(x for x in board.parse_queue(STATUS) if x["title"] == "V2 사람 판정")
                board.apply_decision(v2["id"], "retry", "")
                self.assertEqual(
                    [x["title"] for x in board.parse_queue(status_p.read_text(encoding="utf-8"))],
                    ["V2 사람 판정", "V4 삭제 루프 준비"],
                )
            finally:
                board.STATUS, board.INBOX, board.DESIGN, board.DECISIONS_PATH, board.COMMANDS_PATH = old


class NowWorkTests(unittest.TestCase):
    def test_infer_prefers_last_action_line(self):
        log = "읽기만 함\nV4 삭제 루프의 코드 갭부터 확인합니다.\nRED 17건을 확인했습니다. 생산 경계를 구현합니다.\n"
        title = board.infer_now_title(log, [{"title": "큐항목"}], [])
        self.assertIn("구현", title)
        self.assertLess(len(title), 80)

    def test_infer_falls_back_to_queue(self):
        self.assertEqual(
            board.infer_now_title("ok\n", [{"title": "V2 사람 판정"}], []),
            "V2 사람 판정 하는 중",
        )

    def test_infer_reading_is_not_current_work(self):
        title = board.infer_now_title(
            "이터레이션을 시작합니다.INBOX 최신 지시는 80% 정체 분석입니다.",
            [{"title": "UI 퀄리티"}],
            [{"title": "80%에서 멈춤"}],
        )
        self.assertEqual(title, "80%에서 멈춤 하는 중")

    def test_infer_skips_reading_blob(self):
        log = (
            "이터레이션을 시작합니다.지시대로 인박스·상태·설계부터 읽습니다."
            "인박스 캐릭터창은 목록만 있고 오른쪽이 비어 있습니다."
            "목록은 왼쪽, 대형 모습·장비는 오른쪽에 한 화면으로 붙이겠습니다."
            "사본에 동기화한 뒤 SelfCheck를 돌리겠습니다."
        )
        title = board.infer_now_title(log, [{"title": "UI 퀄리티"}], [])
        self.assertNotIn("이터레이션을 시작", title)
        self.assertNotIn("지시대로", title)
        self.assertTrue("SelfCheck" in title or "붙이" in title or "동기화" in title)
        self.assertLess(len(title), 60)

    def test_infer_skips_wrapup_report(self):
        log = (
            "목록은 왼쪽, 모습·장비는 오른쪽에 붙이겠습니다."
            "**한 일** - 명부 탭: 왼쪽 줄 목록 + 오른쪽 전신 idle · 전체 화면 그대로."
            "다음 세션은 80%에서 멈춤부터 잡으면 됩니다."
        )
        title = board.infer_now_title(log, [], [])
        self.assertNotIn("한 일", title)
        self.assertNotIn("다음 세션", title)
        self.assertIn("붙이", title)

    def test_current_work_active_until_done_marker(self):
        with tempfile.TemporaryDirectory() as tmp:
            here = Path(tmp)
            logs = here / "logs"
            logs.mkdir()
            (logs / "iter_20260816_164158.log").write_text(
                "V4 경계를 구현합니다.\n사본에서 SelfCheck를 실행합니다.\n",
                encoding="utf-8",
            )
            (here / "loop_main.log").write_text(
                "▶ 이터레이션 #2  16:41:58  → /x/loop/logs/iter_20260816_164158.log\n",
                encoding="utf-8",
            )
            old = (board.HERE, board.ROOT)
            board.HERE, board.ROOT = here, here.parent
            try:
                now = board.current_work(True, False, False, "iter_20260816_164158.log", "")
                self.assertEqual(now["phase"], "작업 중")
                self.assertEqual(now["number"], "2")
                self.assertTrue("SelfCheck" in now["title"] or "구현" in now["title"])
                self.assertEqual(now["activity"], [])
                (here / "loop_main.log").write_text(
                    "▶ 이터레이션 #2  16:41:58  → /x/loop/logs/iter_20260816_164158.log\n✅ #2 완료\n",
                    encoding="utf-8",
                )
                done = board.current_work(True, False, False, "iter_20260816_164158.log", "")
                self.assertEqual(done["phase"], "대기")
            finally:
                board.HERE, board.ROOT = old


SAMPLE_BILLING = {
    "config": {
        "currentPeriod": {
            "type": "USAGE_PERIOD_TYPE_WEEKLY",
            "start": "2026-08-16T06:52:40+00:00",
            "end": "2026-08-23T06:52:40+00:00",
        },
        "creditUsagePercent": 44.0,
        "productUsage": [
            {"product": "GrokBuild", "usagePercent": 36.0},
            {"product": "GrokImagine", "usagePercent": 4.0},
        ],
    }
}


class GrokUsageTests(unittest.TestCase):
    def setUp(self):
        board._usage_mem = None
        board._usage_at = 0.0
        self._tmp = tempfile.TemporaryDirectory()
        self._old_cache = board.GROK_USAGE_CACHE
        board.GROK_USAGE_CACHE = Path(self._tmp.name) / "grok_usage.cache.json"

    def tearDown(self):
        board.GROK_USAGE_CACHE = self._old_cache
        board._usage_mem = None
        board._usage_at = 0.0
        self._tmp.cleanup()

    def test_summarize_remaining(self):
        out = board.summarize_grok_billing(SAMPLE_BILLING, fetched_at="21:00")
        self.assertTrue(out["ok"])
        self.assertEqual(out["used_pct"], 44.0)
        self.assertEqual(out["remain_pct"], 56.0)
        self.assertEqual(out["period"], "이번 주")
        self.assertEqual(out["period_end"], "8/23")
        labels = [p["label"] for p in out["products"]]
        self.assertEqual(labels, ["빌드", "이미지"])

    def test_cache_skips_second_fetch(self):
        n = {"c": 0}

        def fetch(_token):
            n["c"] += 1
            return SAMPLE_BILLING

        old_tok = board._grok_token
        board._grok_token = lambda: "tok"
        try:
            a = board.grok_usage(now=1000, fetch=fetch)
            b = board.grok_usage(now=1100, fetch=fetch)
        finally:
            board._grok_token = old_tok
        self.assertEqual(n["c"], 1)
        self.assertEqual(a["remain_pct"], 56.0)
        self.assertEqual(b["remain_pct"], 56.0)

    def test_empty_billing_keeps_last_number(self):
        n = {"c": 0}

        def fetch(_token):
            n["c"] += 1
            if n["c"] == 1:
                return SAMPLE_BILLING
            return {"config": {
                "currentPeriod": SAMPLE_BILLING["config"]["currentPeriod"],
            }}

        old_tok = board._grok_token
        board._grok_token = lambda: "tok"
        try:
            a = board.grok_usage(now=1000, fetch=fetch)
            b = board.grok_usage(now=2000, fetch=fetch)
        finally:
            board._grok_token = old_tok
        self.assertEqual(a["remain_pct"], 56.0)
        self.assertEqual(b["remain_pct"], 56.0)
        self.assertTrue(b["stale"])
        self.assertFalse(b["ok"])

    def test_no_token_says_so(self):
        old_tok = board._grok_token
        old_disk = board._load_usage_disk
        board._grok_token = lambda: ""
        board._load_usage_disk = lambda: None
        try:
            out = board.grok_usage(now=1, fetch=lambda t: SAMPLE_BILLING)
        finally:
            board._grok_token = old_tok
            board._load_usage_disk = old_disk
        self.assertFalse(out["ok"])
        self.assertIn("로그인", out["error"])

    def test_html_has_usage_box(self):
        html = (HERE / "board.html").read_text(encoding="utf-8")
        self.assertIn('id="usage-box"', html)
        self.assertIn("renderGrokUsage", html)
        self.assertIn("usageChip", html)
        self.assertIn('usageChip(state.claude, "클로드")', html)
        self.assertIn('usageChip(state.codex, "코덱스")', html)
        self.assertNotIn("usage-row", html)
        self.assertLess(html.find('id="usage-box"'), html.find('class="request-top"'))


SAMPLE_CLAUDE = {
    "five_hour": {"utilization": 12.5, "resets_at": None},
    "seven_day": {"utilization": 40.0, "resets_at": "2026-08-17T14:00:00+00:00"},
}

SAMPLE_CODEX = {
    "plan_type": "plus",
    "rate_limit": {
        "primary_window": {
            "used_percent": 70.0,
            "limit_window_seconds": 604800,
            "reset_at": 1787444587,
        },
    },
}


class CliUsageTests(unittest.TestCase):
    def setUp(self):
        board._claude_mem = None
        board._claude_at = 0.0
        board._codex_mem = None
        board._codex_at = 0.0
        self._tmp = tempfile.TemporaryDirectory()
        self._old_c = board.CLAUDE_USAGE_CACHE
        self._old_x = board.CODEX_USAGE_CACHE
        board.CLAUDE_USAGE_CACHE = Path(self._tmp.name) / "claude.json"
        board.CODEX_USAGE_CACHE = Path(self._tmp.name) / "codex.json"

    def tearDown(self):
        board.CLAUDE_USAGE_CACHE = self._old_c
        board.CODEX_USAGE_CACHE = self._old_x
        board._claude_mem = None
        board._codex_mem = None
        self._tmp.cleanup()

    def test_summarize_claude_windows(self):
        out = board.summarize_claude_usage(SAMPLE_CLAUDE, fetched_at="22:00")
        self.assertTrue(out["ok"])
        self.assertEqual(out["used_pct"], 40.0)
        self.assertEqual(out["remain_pct"], 60.0)
        self.assertEqual(out["period_end"], "8/17")
        self.assertEqual([p["label"] for p in out["products"]], ["5시간", "주간"])

    def test_summarize_codex_weekly(self):
        out = board.summarize_codex_usage(SAMPLE_CODEX, fetched_at="22:00")
        self.assertTrue(out["ok"])
        self.assertEqual(out["used_pct"], 70.0)
        self.assertEqual(out["remain_pct"], 30.0)
        self.assertEqual(out["period"], "이번 주")
        self.assertEqual(out["plan"], "Plus")
        self.assertTrue(out["period_end"])

    def test_claude_no_token(self):
        old = board._claude_token
        board._claude_token = lambda: ""
        try:
            out = board.claude_usage(now=1, fetch=lambda t: SAMPLE_CLAUDE)
        finally:
            board._claude_token = old
        self.assertFalse(out["ok"])
        self.assertIn("로그인", out["error"])

    def test_codex_no_token(self):
        old = board._codex_auth
        board._codex_auth = lambda: ("", "")
        try:
            out = board.codex_usage(now=1, fetch=lambda t, a="": SAMPLE_CODEX)
        finally:
            board._codex_auth = old
        self.assertFalse(out["ok"])
        self.assertIn("로그인", out["error"])

    def test_claude_cache_skips_second_fetch(self):
        n = {"c": 0}

        def fetch(_token):
            n["c"] += 1
            return SAMPLE_CLAUDE

        old = board._claude_token
        board._claude_token = lambda: "tok"
        try:
            a = board.claude_usage(now=1000, fetch=fetch)
            b = board.claude_usage(now=1100, fetch=fetch)
        finally:
            board._claude_token = old
        self.assertEqual(n["c"], 1)
        self.assertEqual(a["remain_pct"], 60.0)
        self.assertEqual(b["remain_pct"], 60.0)


class SliceBoardTests(unittest.TestCase):
    def test_roadmap_from_spec(self):
        rows = board.parse_roadmap_table("""
### 개발 로드맵 — 프로토타입 이후

| 단계 | 기간 | 범위 | 관문 |
|---|---|---|---|
| **0. 프로토타입** | 4주 | §21 | **V4** |
| **1. 수직 슬라이스** | 8주 | 영지 7종 | 5시간 |
""")
        self.assertEqual([r["id"] for r in rows], ["0", "1"])
        self.assertIn("수직", rows[1]["label"])

    def test_slice_checks_need_evidence(self):
        design = """
- **수직 슬라이스** — 본성 레벨·광산 적립·창고 용량·방어 건물 4종은 닫힘. 격자·단축 50%는 다음.
"""
        status = """
> 영묘✅ · 대장간 첫 슬라이스✅ · 수비 배치✅
> ~~**§3 전직 시스템**~~ ✅ **완료**
"""
        rows = {r["title"]: r["done"] for r in board.slice_checks(status, design)}
        self.assertTrue(rows["본성 레벨"])
        self.assertTrue(rows["광산 생산"])
        self.assertTrue(rows["방어 건물 4종"])
        self.assertTrue(rows["영묘"])
        self.assertTrue(rows["전직 1차"])
        self.assertFalse(rows["격자 8×8"])
        self.assertFalse(rows["단축 50%"])
        self.assertFalse(rows["탑 30층"])

    def test_queue_title_is_not_closed(self):
        status = (
            "1. **수직 슬라이스 — 격자 8×8** — 본성·단축 50%는 닫음.\n"
            "> **이번 이터 결과: 건설 단축 50%.**\n"
        )
        rows = {r["title"]: r["done"] for r in board.slice_checks(status, "")}
        self.assertFalse(rows["격자 8×8"])
        self.assertTrue(rows["단축 50%"])
        self.assertFalse(rows["탑 30층"])

    def test_slice_pct_after_v4_skip(self):
        status = STATUS + "\nV2 사람 판정 → 통과\nV4 외부 테스터 70% → 넘김\n"
        design = DESIGN + "\n- **수직 슬라이스** — 본성 레벨은 닫힘. 격자·단축 50%는 다음.\n"
        game = """
개발 로드맵 — 프로토타입 이후

| 단계 | 기간 | 범위 | 관문 |
|---|---|---|---|
| **0. 프로토타입** | 4주 | §21 | V4 |
| **1. 수직 슬라이스** | 8주 | 영지 | 5시간 |
"""
        charts = board.progress_charts(status, design, game, {})
        stage = next(r for r in charts["roadmap"] if r["id"] == "1")
        self.assertGreater(stage["pct"], 0)
        self.assertLess(stage["pct"], 100)
        self.assertIn("원장 범위", stage["note"])
        self.assertEqual(charts["current"]["id"], "1")
        self.assertTrue(charts["current"]["proto_done"])
        self.assertEqual(charts["current"]["label"], "마을·탑·장비")
        self.assertFalse(any(f["id"] == "V4b" for f in charts["focus"]))
        self.assertTrue(any("격자" in (f.get("label") or "") or f["id"] == "slice-done"
                            for f in charts["focus"]))

    def test_current_stays_proto_until_v4_done(self):
        charts = board.progress_charts(STATUS, DESIGN, "", {})
        self.assertEqual(charts["current"]["id"], "0")
        self.assertFalse(charts["current"]["proto_done"])
        self.assertTrue(any(f["id"] == "V4b" for f in charts["focus"]))

    def test_now_list_closed_after_gates(self):
        now = board.parse_now_list("""
### 지금 당장 할 일 (우선순위 순)

1. **V3 보스 전투를 끝까지 연결** — 한 판
2. **V2 사람 판정** — 창
3. **V4 외부 판정 후 확장** — 영지

## 23. 다음
""")
        design = """### 현재 핵심 미완

- **V3 한 판 종단** ✅ 닫힘
- **V2 사람 판정** ✅ 통과
"""
        board.mark_now_closed(now, design, "V4 70% → 넘김", {})
        self.assertTrue(all(it.get("done") for it in now))


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
