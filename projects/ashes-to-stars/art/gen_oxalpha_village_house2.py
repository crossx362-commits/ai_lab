#!/usr/bin/env python3
"""ox-alpha 마을 작은 집(village_house_2) 코드합성 — 256×256 단칸 오두막.

배경: house_0/1 은 ox-alpha 256인데 house_2 는 아직 나노바나나
1841×1687·2.7MB 그레이 폐가라 톤이 따로 논다. 같은 웜톤·같은 256,
중형 _0·2층 _1과 구별되는 작은 단칸 오두막으로 교체한다. house_0/1 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, village_house_2=2.80)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 낮은 석조 기초 + 세로 널 목벽 + 급경사 작은 박공 지붕 + 문 하나 + 창 하나.
굴뚝 없음(_0과 구별). §6-A: 바닥 큰 원/고리/글로우 금지, 그림자는 밑 얇은 띠뿐.

사용: python3 gen_oxalpha_village_house2.py
출력: art/out_oxalpha_village_house2.png (256×256 RGBA)
"""
from pathlib import Path
import random

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

STONE = (150, 142, 126, 255)
STONE_DK = (108, 100, 86, 255)
STONE_LT = (188, 180, 162, 255)
MORTAR = (86, 80, 68, 255)
WOOD = (150, 104, 58, 255)
WOOD_DK = (96, 64, 34, 255)
WOOD_LT = (196, 150, 92, 255)
ROOF = (128, 88, 52, 255)
ROOF_DK = (92, 60, 34, 255)
ROOF_LT = (170, 128, 78, 255)
OUTLINE = (40, 24, 20, 255)
GOLD = (216, 172, 96, 255)


def _stone_base(d, rng, x0, y0, x1, y1):
    d.rectangle([x0 - 2, y0 - 2, x1 + 2, y1 + 2], fill=OUTLINE)
    d.rectangle([x0, y0, x1, y1], fill=STONE)
    y = y0
    ri = 0
    row_h = 11
    while y < y1:
        off = (ri % 2) * 12
        bx = x0 + 4 + off
        while bx < x1 - 3:
            d.line([(bx, y + 1), (bx, min(y1 - 1, y + row_h - 1))], fill=MORTAR, width=1)
            bx += 24
        d.line([(x0 + 1, min(y1 - 1, y + row_h)), (x1 - 1, min(y1 - 1, y + row_h))], fill=MORTAR, width=1)
        lo, hi = y + 2, min(y1 - 3, y + row_h - 3)
        if lo <= hi:
            for _ in range(max(1, (x1 - x0) // 28)):
                sx = rng.randint(x0 + 3, x1 - 10)
                sy = rng.randint(lo, hi)
                d.rectangle([sx, sy, sx + 5, sy + 3],
                            fill=STONE_DK if rng.random() < 0.5 else STONE_LT)
        y += row_h
        ri += 1


def _plank_wall(d, x0, y0, x1, y1):
    d.rectangle([x0 - 2, y0 - 2, x1 + 2, y1 + 2], fill=OUTLINE)
    d.rectangle([x0, y0, x1, y1], fill=WOOD)
    px = x0 + 7
    i = 0
    while px < x1 - 2:
        col = WOOD_DK if i % 2 == 0 else WOOD_LT
        d.line([(px, y0 + 1), (px, y1 - 1)], fill=col, width=1)
        px += 10
        i += 1
    d.rectangle([x0, y0, x1, y0 + 4], fill=WOOD_DK)
    d.rectangle([x0, y1 - 4, x1, y1], fill=WOOD_DK)


def _roof(d, cx, apex_y, base_y, half_w):
    lx, rx = cx - half_w, cx + half_w
    d.polygon([(cx, apex_y - 3), (lx - 4, base_y + 3), (rx + 4, base_y + 3)], fill=OUTLINE)
    d.polygon([(cx, apex_y), (lx, base_y), (rx, base_y)], fill=ROOF)
    rows = 7
    for i in range(1, rows):
        t = i / rows
        yy = int(apex_y + (base_y - apex_y) * t)
        hw = int(half_w * t)
        col = ROOF_DK if i % 2 == 0 else ROOF_LT
        d.line([(cx - hw, yy), (cx + hw, yy)], fill=col, width=1)
    d.line([(cx, apex_y), (lx, base_y)], fill=ROOF_LT, width=2)
    d.line([(cx, apex_y), (rx, base_y)], fill=ROOF_DK, width=2)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    rng = random.Random(20260827)
    cx = 128

    d.rectangle([78, 218, 178, 226], fill=(30, 24, 20, 90))

    # 좁은 몸체
    _stone_base(d, rng, 82, 192, 174, 220)
    _plank_wall(d, 86, 132, 170, 194)
    _roof(d, cx, 72, 132, half_w=56)

    # 문(왼쪽) + 창(오른쪽) — 단칸
    d.rectangle([96, 156, 122, 216], fill=OUTLINE)
    d.rectangle([98, 158, 120, 214], fill=WOOD_LT)
    d.pieslice([98, 146, 120, 170], 180, 360, fill=WOOD_LT, outline=OUTLINE)
    d.line([(109, 152), (109, 212)], fill=WOOD_DK, width=1)
    d.ellipse([114, 184, 118, 188], fill=GOLD, outline=OUTLINE)

    d.rectangle([140, 160, 160, 180], fill=OUTLINE)
    d.rectangle([142, 162, 158, 178], fill=(120, 128, 120, 255))
    d.line([(150, 162), (150, 178)], fill=OUTLINE, width=1)
    d.line([(142, 170), (158, 170)], fill=OUTLINE, width=1)
    d.rectangle([134, 161, 140, 179], fill=WOOD, outline=OUTLINE)
    d.rectangle([160, 161, 166, 179], fill=WOOD, outline=OUTLINE)

    out = HERE / "out_oxalpha_village_house2.png"
    im.save(out)
    px = im.load()
    clear = solid = 0
    for y in range(256):
        for x in range(256):
            a = px[x, y][3]
            if a < 13:
                clear += 1
            elif a > 242:
                solid += 1
    print(f"→ {out.name}  256×256  투명 {clear} · 불투명 {solid} / 65536")


if __name__ == "__main__":
    make()
