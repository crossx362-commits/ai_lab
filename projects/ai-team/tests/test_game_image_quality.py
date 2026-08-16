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
        self.assertIn("딥서치", text)
        self.assertIn("수정", text)

    def test_purple_body_is_not_chroma(self):
        import numpy as np
        from PIL import Image
        import tempfile
        from pathlib import Path
        mod = _load()
        tmp = Path(tempfile.mkdtemp())
        (tmp / "sprites").mkdir()
        body = np.zeros((48, 48, 4), np.uint8)
        body[8:40, 8:40] = (180, 60, 160, 255)
        Image.fromarray(body, "RGBA").save(tmp / "sprites" / "purple.png")
        flags = mod._inspect_rel(tmp / "sprites" / "purple.png", "sprites/purple.png")
        self.assertFalse(any("크로마" in f for f in flags), flags)

    def test_can_fix_skips_ui_and_fx(self):
        mod = _load()
        self.assertFalse(mod.can_fix("ui/atlas.png", ["크로마 잔상 1.0%"]))
        self.assertFalse(mod.can_fix("FX/fx.png", ["반투명 가장자리 20.0%"]))
        self.assertTrue(mod.can_fix("sprites/a.png", ["크로마 잔상 1.0%"]))


if __name__ == "__main__":
    unittest.main()
