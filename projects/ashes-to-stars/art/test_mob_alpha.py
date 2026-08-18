#!/usr/bin/env python3
"""필드 몹 5계열 — 알파 구멍. 시트가 채워져 있는데 반입만 뚫리면 FAIL.

오너 2026-08-18 22:03 「몬스터들 스프라이트 알파 잘못 빠져서 구멍나있음」.
p22 시트는 몸이 있는데 Resources 프레임만 외곽선이던 사고.
"""
from __future__ import annotations

import unittest
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
RES = HERE.parent / "unity" / "Assets" / "Resources" / "sprites"
MOBS = ("mob01", "mob_chaser", "mob_charger", "mob_ranged", "mob_swarmer")
# 닫힌 투명. 다리 사이처럼 가장자리와 이어진 빈 칸은 구멍이 아니다.
MAX_HOLE = 0.010


def interior_holes(alpha: np.ndarray, thresh: int = 16) -> tuple[float, int, float]:
    h, w = alpha.shape
    opaque = alpha > thresh
    vis = np.zeros_like(opaque, dtype=bool)
    q: deque[tuple[int, int]] = deque()
    for x in range(w):
        if not opaque[0, x]:
            vis[0, x] = True
            q.append((0, x))
        if not opaque[h - 1, x]:
            vis[h - 1, x] = True
            q.append((h - 1, x))
    for y in range(h):
        if not opaque[y, 0]:
            vis[y, 0] = True
            q.append((y, 0))
        if not opaque[y, w - 1]:
            vis[y, w - 1] = True
            q.append((y, w - 1))
    while q:
        y, x = q.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and not vis[ny, nx] and not opaque[ny, nx]:
                vis[ny, nx] = True
                q.append((ny, nx))
    holes = (~opaque) & (~vis)
    return float(opaque.mean()), int(holes.sum()), float(holes.mean())


class MobAlphaTests(unittest.TestCase):
    def test_idle_has_no_interior_holes(self):
        worst = []
        for name in MOBS:
            p = RES / name / f"{name}_idle_00.png"
            self.assertTrue(p.is_file(), f"{p} 없음")
            im = Image.open(p).convert("RGBA")
            op, n, hf = interior_holes(np.asarray(im)[..., 3])
            worst.append((hf, n, op, name, im.size))
            self.assertGreater(op, 0.15, f"{name} 불투명 {op:.3f} — 몸이 거의 없다")
            self.assertLess(
                hf, MAX_HOLE,
                f"{name} 내부 구멍 {hf:.4f} n={n} (상한 {MAX_HOLE})",
            )
        worst.sort(reverse=True)
        print("idle holes", [(m, f"{hf:.4f}", n) for hf, n, _, m, _ in worst])

    def test_p22_sheets_exist(self):
        for name in MOBS:
            a = HERE / "out_p22_bw" / f"sheet_{name}_A.png"
            b = HERE / "out_p22_bw" / f"sheet_{name}_B.png"
            self.assertTrue(a.is_file(), f"{a.name} 없음")
            self.assertTrue(b.is_file(), f"{b.name} 없음")


if __name__ == "__main__":
    unittest.main()
