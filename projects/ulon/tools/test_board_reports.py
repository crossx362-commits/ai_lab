#!/usr/bin/env python3
"""보드 보고서: STATUS에 적힌 샷만, 중복·바퀴번호 없는 파일은 제외."""
from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "loop"))
import board_server as board  # noqa: E402


STATUS = """# 울온 현황

## 시스템 상태

판정 문장.

| 시스템 | 상태 | 이번 근거 |
|---|---|---|
| 카메라 고정 3/4 쿼터뷰 | 동작함 | loop#7: 샷 `unity/Captures/loop7_play_minimap.png` — 고정 3/4 |
| VFX | 동작함 | loop#8 타격 불티 + loop#9 등불. 샷 `unity/Captures/loop9_fountain_spray.png` |
| UI 팩 | 부분 | loop#14: 샷 `unity/Captures/loop14_context_trainer.png`·`loop14_context_house.png`·`unity/Captures/loop14_missing.png` |
"""


class ReportCollectTests(unittest.TestCase):
    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp())
        for name in (
            "loop7_play_minimap.png",
            "loop8_hit_vfx.png",
            "loop8_hit_close.png",
            "loop9_fountain_spray.png",
            "loop9_fountain_spray_1.png",
            "loop9_lantern_flame.png",
            "loop12_paperdoll.png",
            "loop14_context_trainer.png",
            "loop14_context_house.png",
            "loop14_context_pet-1.png",
            "ulon_anim_idle.png",
        ):
            (self.tmp / name).write_bytes(b"png")

    def test_status_ticks_and_loop_files(self):
        cards = [
            {"id": "map-land-scale", "status": "완료", "completed_loop": 7, "title": "섬 확대", "rationale": ""},
            {"id": "ui-paperdoll", "status": "완료", "completed_loop": 12, "title": "Paperdoll", "rationale": "칸 그림"},
            {"id": "ui-context-menu", "status": "완료", "completed_loop": 14, "title": "우클릭", "rationale": ""},
        ]
        r = board.collect_reports(STATUS, cards, self.tmp)
        by = {g["loop"]: g for g in r["groups"]}
        names7 = [s["name"] for s in by[7]["shots"]]
        names8 = [s["name"] for s in by[8]["shots"]]
        names9 = [s["name"] for s in by[9]["shots"]]
        names12 = [s["name"] for s in by[12]["shots"]]
        names14 = [s["name"] for s in by[14]["shots"]]
        all_names = [s["name"] for g in r["groups"] for s in g["shots"]]

        self.assertIn("loop7_play_minimap.png", names7)
        self.assertEqual(names9, ["loop9_fountain_spray.png"])
        self.assertIn("loop8_hit_vfx.png", names8)
        self.assertIn("loop8_hit_close.png", names8)
        self.assertIn("loop12_paperdoll.png", names12)
        self.assertEqual(
            names14,
            ["loop14_context_house.png", "loop14_context_trainer.png", "loop14_missing.png"],
        )
        missing = [s for s in by[14]["shots"] if s["missing"]]
        self.assertEqual([s["name"] for s in missing], ["loop14_missing.png"])
        self.assertNotIn(10, by)
        self.assertNotIn(0, by)
        self.assertNotIn("ulon_anim_idle.png", all_names)
        self.assertNotIn("loop9_fountain_spray_1.png", all_names)
        self.assertNotIn("loop14_context_pet-1.png", all_names)
        self.assertNotIn("loop9_lantern_flame.png", names9)

    def test_nc_empty_status_does_not_dump_captures(self):
        r = board.collect_reports("# no table\n", [], self.tmp)
        self.assertEqual(r["groups"], [])
        self.assertEqual(r["shot_count"], 0)

    def test_captures_dir_not_assets(self):
        p = (ROOT / "unity" / "Captures" / "loop7_play_minimap.png").resolve()
        secret = (ROOT / "unity" / "Assets").resolve() / "x.png"
        h = board.Handler
        inst = h.__new__(h)
        self.assertTrue(inst._allowed_file(p))
        self.assertFalse(inst._allowed_file(secret))


if __name__ == "__main__":
    unittest.main()
