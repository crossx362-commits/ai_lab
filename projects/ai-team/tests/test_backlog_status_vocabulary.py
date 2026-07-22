#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""백로그 정규 상태 어휘 가드 — 어휘 밖 상태(정지 유령) 감지 회귀 테스트.

배경(2026-07-22 하네스 검수): backlog.json에서 '완료(부분)' 같은 비어휘 상태로
정지한 항목을 발견. 코드는 그런 값을 만들지 않지만 수동 편집으로 새면 어떤 소비자도
안 읽어 영구 방치된다("'진행' 상태는 무덤" 교훈). noncanonical_items()가 이를 감지한다.
"""
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from _shared.backlog import CANONICAL_STATUSES, noncanonical_items


class NoncanonicalItemsTests(unittest.TestCase):
    def test_canonical_statuses_are_the_four(self):
        self.assertEqual(set(CANONICAL_STATUSES), {"대기", "보류", "PR대기", "완료"})

    def test_detects_offvocabulary_status(self):
        items = [
            {"id": "a", "status": "대기"},
            {"id": "b", "status": "완료(부분)"},   # 유령
            {"id": "c", "status": "PR대기"},
            {"id": "d", "status": "진행"},          # 유령
        ]
        ghosts = {g["id"] for g in noncanonical_items(items)}
        self.assertEqual(ghosts, {"b", "d"})

    def test_all_canonical_yields_no_ghost(self):
        items = [{"id": s, "status": s} for s in CANONICAL_STATUSES]
        self.assertEqual(noncanonical_items(items), [])

    def test_robust_to_empty_and_nondict(self):
        self.assertEqual(noncanonical_items([]), [])
        self.assertEqual(noncanonical_items(None), [])
        self.assertEqual(noncanonical_items(["not-a-dict", 3]), [])


if __name__ == "__main__":
    unittest.main()
