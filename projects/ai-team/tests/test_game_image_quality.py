#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import importlib.util
import sys
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
TOOL = HERE.parent / "skills" / "별이_이미지품질" / "tools" / "game_image_quality.py"


def _load():
    spec = importlib.util.spec_from_file_location("game_image_quality", TOOL)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


class ImageQualityTests(unittest.TestCase):
    def test_self_test_detects_tiny(self):
        mod = _load()
        self.assertEqual(mod.self_test(), 0)

    def test_skill_exists(self):
        skill = HERE.parent / "skills" / "별이_이미지품질" / "SKILL.md"
        self.assertTrue(skill.is_file())
        text = skill.read_text(encoding="utf-8")
        self.assertIn("힉스필드 재생성", text)


if __name__ == "__main__":
    unittest.main()
