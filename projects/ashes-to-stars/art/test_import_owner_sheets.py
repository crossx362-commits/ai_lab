#!/usr/bin/env python3
"""서포터 시트 — 공격 행 슬래시 조각을 캐릭터 칸으로 쓰지 않는다.

오너 2026-08-18 22:03 「서포터 스프라이트 잘못됨」.
시트는 몸이 있는데 반입이 attack_04/05를 반쪽·궤적으로 잘랐다.
"""
from __future__ import annotations

import unittest
from pathlib import Path

import numpy as np
from PIL import Image

import import_owner_sheets as ios

HERE = Path(__file__).resolve().parent
SHEET = HERE / "out_hf_inbox" / "owner_character_sprites" / (
    "b871e406-1908-4747-a9f5-a077d476634a.jpg"
)
RES = HERE.parent / "unity" / "Assets" / "Resources" / "sprites" / "buffer"
# 온전한 몸. 옛 반쪽은 idle 대비 0.70 아래였다.
MIN_AREA = 0.80


def _buffer_attack_row() -> np.ndarray:
    im = Image.open(SHEET).convert("RGB")
    a = np.asarray(im).astype(np.float32)
    bands = ios.label_bands(a)
    top, bot = bands[ios.ROW_ATTACK]
    return a[top:bot]


def _area(im: Image.Image) -> int:
    a = np.asarray(im.convert("RGBA"))
    return int((a[..., 3] > 40).sum())


class BufferAttackSplitTests(unittest.TestCase):
    def test_raw_attack_row_has_slash_scraps(self):
        row = _buffer_attack_row()
        raw = ios.split_row(row, tidy=False)
        widths = [c.shape[1] for c in raw]
        scraps = [w for w in widths if w < 130]
        self.assertGreaterEqual(len(raw), 9, f"조각 없이 10칸이 안 나오면 네거티브 무효 {widths}")
        self.assertGreaterEqual(len(scraps), 2, f"좁은 조각이 있어야 한다 {widths}")

    def test_tidy_drops_slash_scraps(self):
        row = _buffer_attack_row()
        cells = ios.split_row(row)
        widths = [c.shape[1] for c in cells]
        self.assertGreaterEqual(len(cells), 6, f"공격 칸 {len(cells)}")
        self.assertLessEqual(len(cells), 8, f"조각이 남았다 {widths}")
        self.assertTrue(all(w >= 170 for w in widths), f"좁은 칸 {widths}")

    def test_imported_attack_frames_have_a_body(self):
        idle = Image.open(RES / "buffer_idle_00.png")
        idle_a = _area(idle)
        self.assertGreater(idle_a, 4000, "idle 몸이 없다")
        for name in (
            "buffer_attack_00.png",
            "buffer_attack_03.png",
            "buffer_attack_04.png",
            "buffer_attack_05.png",
            "buffer_special_04.png",
            "buffer_special_05.png",
        ):
            p = RES / name
            self.assertTrue(p.is_file(), f"{p} 없음")
            ratio = _area(Image.open(p)) / idle_a
            self.assertGreaterEqual(
                ratio, MIN_AREA,
                f"{name} 면적 {ratio:.3f} < {MIN_AREA} — 반쪽·궤적",
            )


if __name__ == "__main__":
    unittest.main()
