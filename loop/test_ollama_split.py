#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""올라마 분담기가 클라우드로 안 새고, 테스터/경매를 skip으로 막는지."""
from __future__ import annotations

import ast
import unittest
from pathlib import Path
from unittest import mock

HERE = Path(__file__).resolve().parent
import sys
sys.path.insert(0, str(HERE))
import ollama_split  # noqa: E402


class OllamaSplitTests(unittest.TestCase):
    def test_script_calls_ollama_not_cloud_text(self):
        src = (HERE / "ollama_split.py").read_text(encoding="utf-8")
        tree = ast.parse(src)
        imports = []
        for n in ast.walk(tree):
            if isinstance(n, ast.ImportFrom) and n.module and "llm" in n.module:
                imports.extend(a.name for a in n.names)
        self.assertIn("ollama", imports)
        self.assertNotIn("text", imports)
        self.assertIn("ALLOW_OLLAMA_CLOUD", src)
        self.assertIn("false", src)

    def test_classify_forces_skip_on_tester_or_auction(self):
        fake = '{"next":"V4 외부 테스터 70%","owner":"grok","why":"해야 함"}'
        with mock.patch.object(ollama_split, "ollama", return_value=fake):
            with mock.patch.object(ollama_split, "_read", return_value=""):
                data = ollama_split.classify()
        self.assertEqual(data["owner"], "skip")

    def test_copy_returns_lines(self):
        fake = '{"lines":["피격 시 취소","두루마리는 끝난 뒤"]}'
        with mock.patch.object(ollama_split, "ollama", return_value=fake):
            data = ollama_split.copy("escape")
        self.assertEqual(data["lines"][0], "피격 시 취소")


if __name__ == "__main__":
    unittest.main()
