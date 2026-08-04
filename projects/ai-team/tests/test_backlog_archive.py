#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""완료 항목 아카이브 분리 회귀 테스트 (2026-08-04 오너 지시로 도입).

핵심 위험은 "완료를 옮기면 중복 적재 방지가 깨진다"였다 — 미오·나무·회의가
`existing = {완료 포함 모든 제목}`으로 같은 제안을 막고 있었기 때문에, 그냥 옮기면
이미 만든 기능을 매주 다시 제안한다(2026-07-25 'AI_AGENT_FRIENDS' 사고와 같은 계열:
이름 목록을 옮기면서 그걸 읽는 소비자를 확인 안 함). 네거티브 컨트롤로 실제 검증한다.
"""
import json
import sys
import tempfile
import unittest
from pathlib import Path

_here = Path(__file__).resolve().parent
sys.path.insert(0, str(_here.parent))

from _shared.backlog import (  # noqa: E402
    archive_completed, archived_titles, all_known_titles, archive_path,
)


def _mk(tmp: Path, items: list) -> Path:
    p = tmp / "backlog.json"
    p.write_text(json.dumps({"items": items}, ensure_ascii=False), encoding="utf-8")
    return p


def _done(i: int, updated: str = "") -> dict:
    d = {"id": f"done_{i}", "title": f"완료기능_{i}", "status": "완료"}
    if updated:
        d["updated"] = updated
    else:
        d["created"] = f"2026-01-{(i % 28) + 1:02d}T00:00:00"
    return d


class ArchiveCompletedTests(unittest.TestCase):
    def test_moves_only_old_completed(self):
        with tempfile.TemporaryDirectory() as td:
            tmp = Path(td)
            items = [_done(i, f"2026-0{(i % 9) + 1}-01T00:00:00") for i in range(50)]
            items += [{"id": "w1", "title": "대기중", "status": "대기"},
                      {"id": "h1", "title": "보류중", "status": "보류"},
                      {"id": "p1", "title": "PR대기중", "status": "PR대기"}]
            p = _mk(tmp, items)
            moved = archive_completed(p, keep_recent=10)
            self.assertEqual(moved, 40)
            active = json.loads(p.read_text(encoding="utf-8"))["items"]
            # 완료 10건만 남고, 진행 중 3건은 그대로
            self.assertEqual(sum(1 for i in active if i["status"] == "완료"), 10)
            for st in ("대기", "보류", "PR대기"):
                self.assertEqual(sum(1 for i in active if i["status"] == st), 1,
                                 f"{st}가 아카이브에 휩쓸렸다")

    def test_never_touches_unfinished(self):
        """보류·대기·PR대기는 몇 건이든 절대 안 옮긴다."""
        with tempfile.TemporaryDirectory() as td:
            tmp = Path(td)
            items = [{"id": f"h{i}", "title": f"보류_{i}", "status": "보류"} for i in range(50)]
            p = _mk(tmp, items)
            self.assertEqual(archive_completed(p, keep_recent=0), 0)
            self.assertEqual(len(json.loads(p.read_text(encoding="utf-8"))["items"]), 50)

    def test_noop_when_below_threshold(self):
        with tempfile.TemporaryDirectory() as td:
            p = _mk(Path(td), [_done(i) for i in range(5)])
            self.assertEqual(archive_completed(p, keep_recent=40), 0)
            self.assertFalse(archive_path(p).exists(), "불필요한 아카이브 파일 생성")

    def test_archive_is_append_only_and_idempotent(self):
        """두 번 돌려도 아카이브가 중복 누적되지 않는다."""
        with tempfile.TemporaryDirectory() as td:
            tmp = Path(td)
            p = _mk(tmp, [_done(i) for i in range(50)])
            archive_completed(p, keep_recent=10)
            first = len(json.loads(archive_path(p).read_text(encoding="utf-8"))["items"])
            archive_completed(p, keep_recent=10)  # 다시 — 이제 완료 10건뿐이라 무동작
            second = len(json.loads(archive_path(p).read_text(encoding="utf-8"))["items"])
            self.assertEqual(first, second, "아카이브가 중복 누적됐다")

    def test_undated_completed_survive_sort(self):
        """updated가 없는 완료건(실측 108개)이 정렬에서 터지지 않는다."""
        with tempfile.TemporaryDirectory() as td:
            items = [_done(i) for i in range(30)]  # created만 있음
            items += [{"id": "bare", "title": "날짜없음", "status": "완료"}]  # 둘 다 없음
            p = _mk(Path(td), items)
            self.assertEqual(archive_completed(p, keep_recent=5), 26)


class DedupPreservationTests(unittest.TestCase):
    """핵심 — 아카이브한 제목이 중복 판정에서 계속 살아 있는가."""

    def test_archived_titles_still_block_reproposal(self):
        with tempfile.TemporaryDirectory() as td:
            tmp = Path(td)
            p = _mk(tmp, [_done(i) for i in range(50)])
            archive_completed(p, keep_recent=10)
            active = json.loads(p.read_text(encoding="utf-8"))
            known = all_known_titles(p, active)
            # 옮겨진 것도 포함해 원래 50건 제목이 전부 대조 대상이어야 한다
            for i in range(50):
                self.assertIn(f"완료기능_{i}", known,
                              f"아카이브된 완료기능_{i}이 중복 대조에서 사라졌다 — 재제안 발생")

    def test_negative_control_active_only_would_break(self):
        """네거티브 컨트롤: 활성 파일만 봤다면 실제로 깨진다는 것을 증명."""
        with tempfile.TemporaryDirectory() as td:
            tmp = Path(td)
            p = _mk(tmp, [_done(i) for i in range(50)])
            archive_completed(p, keep_recent=10)
            active = json.loads(p.read_text(encoding="utf-8"))
            active_only = {i.get("title") for i in active["items"]}
            missing = [f"완료기능_{i}" for i in range(50) if f"완료기능_{i}" not in active_only]
            self.assertTrue(missing, "네거티브 컨트롤 실패 — 이 테스트가 위험을 재현 못 함")
            # all_known_titles는 바로 그 빠진 것들을 되살려야 한다
            known = all_known_titles(p, active)
            for t in missing:
                self.assertIn(t, known)

    def test_missing_archive_file_is_safe(self):
        with tempfile.TemporaryDirectory() as td:
            p = _mk(Path(td), [{"id": "a", "title": "T", "status": "대기"}])
            self.assertEqual(archived_titles(p), set())
            self.assertEqual(all_known_titles(p, json.loads(p.read_text(encoding="utf-8"))), {"T"})


if __name__ == "__main__":
    unittest.main(verbosity=2)
