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
        charts = board.progress_charts(status, design, game, {
            "x": {"title": "V2 사람 판정", "choice": "pass"},
        })
        self.assertEqual(charts["queue"]["done"], 1)
        self.assertEqual(charts["queue"]["blocked"], 1)
        self.assertEqual(next(g for g in charts["gates"] if g["id"] == "V2")["pct"], 100)
        self.assertEqual(next(g for g in charts["gates"] if g["id"] == "V3")["pct"], 100)
        self.assertEqual(next(g for g in charts["gates"] if g["id"] == "V4b")["pct"], 0)
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
            old = (board.STATUS, board.INBOX, board.DESIGN, board.DECISIONS_PATH)
            board.STATUS, board.INBOX, board.DECISIONS_PATH = status_p, inbox_p, dec_p
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
            finally:
                board.STATUS, board.INBOX, board.DESIGN, board.DECISIONS_PATH = old

    def test_retry_keeps_queue(self):
        with tempfile.TemporaryDirectory() as tmp:
            tmp = Path(tmp)
            status_p = tmp / "STATUS.md"
            inbox_p = tmp / "INBOX.md"
            status_p.write_text(STATUS, encoding="utf-8")
            inbox_p.write_text(INBOX, encoding="utf-8")
            old = (board.STATUS, board.INBOX, board.DESIGN, board.DECISIONS_PATH)
            board.STATUS, board.INBOX = status_p, inbox_p
            board.DESIGN = tmp / "DESIGN.md"
            board.DESIGN.write_text(DESIGN, encoding="utf-8")
            board.DECISIONS_PATH = tmp / "dec.json"
            try:
                v2 = next(x for x in board.parse_queue(STATUS) if x["title"] == "V2 사람 판정")
                board.apply_decision(v2["id"], "retry", "")
                self.assertEqual(
                    [x["title"] for x in board.parse_queue(status_p.read_text(encoding="utf-8"))],
                    ["V2 사람 판정", "V4 삭제 루프 준비"],
                )
            finally:
                board.STATUS, board.INBOX, board.DESIGN, board.DECISIONS_PATH = old


class NowWorkTests(unittest.TestCase):
    def test_infer_prefers_last_action_line(self):
        log = "읽기만 함\nV4 삭제 루프의 코드 갭부터 확인합니다.\nRED 17건을 확인했습니다. 생산 경계를 구현합니다.\n"
        title = board.infer_now_title(log, [{"title": "큐항목"}], [])
        self.assertIn("구현", title)

    def test_infer_falls_back_to_queue(self):
        self.assertEqual(
            board.infer_now_title("ok\n", [{"title": "V2 사람 판정"}], []),
            "큐 · V2 사람 판정",
        )

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
            old = board.HERE
            board.HERE = here
            try:
                now = board.current_work(True, False, False, "iter_20260816_164158.log", "")
                self.assertEqual(now["phase"], "작업 중")
                self.assertEqual(now["number"], "2")
                self.assertIn("SelfCheck", "\n".join(now["activity"]))
                (here / "loop_main.log").write_text(
                    "▶ 이터레이션 #2  16:41:58  → /x/loop/logs/iter_20260816_164158.log\n✅ #2 완료\n",
                    encoding="utf-8",
                )
                done = board.current_work(True, False, False, "iter_20260816_164158.log", "")
                self.assertEqual(done["phase"], "대기")
            finally:
                board.HERE = old


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
