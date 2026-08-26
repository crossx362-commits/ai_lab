#!/usr/bin/env python3
"""ox-alpha 마을 가로등(village_lamp_0) 코드합성 — 256×256 석조·목조 랜턴.

배경: 영지 8동·집·헛간·우물·수레는 ox-alpha 256으로 통일됐으나
가로등(village_lamp_0)은 아직 옛 나노바나나(669×1841·1.3MB, 그레이)라 톤·해상도가
따로 논다. 같은 웜톤 팔레트·같은 256 캔버스의 석조 받침+목주+랜턴으로 교체한다.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, village_lamp_0=2.60)이라 해상도 스왑이 스케일-세이프.
.cs 변경 0. 파일명 동일 → .meta/GUID 무변경, FieldDecor 이름 참조 무변경.

디자인(가로등으로 확실히 읽히게): 하단 석조 받침(벽돌) + 목재 기둥 + 가로 팔 +
매달린 사각 랜턴(철틀·웜톤 유리, 꼭대기 꼭지). §6-A: 바닥 큰 원/고리/글로우 금지 —
그림자는 밑 얇은 띠뿐, 유리 하이라이트는 작은 획만(FX PNG 아님). 실루엣 우선·
두꺼운 아웃라인의 ox-alpha 결.

사용: python3 gen_oxalpha_village_lamp.py
출력: art/out_oxalpha_village_lamp.png (256×256 RGBA)
"""
from pathlib import Path
import random

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

# ox-alpha 팔레트 — village_barn/house/well/cart와 동일
STONE = (150, 142, 126, 255)
STONE_DK = (108, 100, 86, 255)
STONE_LT = (188, 180, 162, 255)
MORTAR = (86, 80, 68, 255)
WOOD = (150, 104, 58, 255)
WOOD_DK = (96, 64, 34, 255)
WOOD_LT = (196, 150, 92, 255)
OUTLINE = (40, 24, 20, 255)
GOLD = (216, 172, 96, 255)
IRON = (86, 80, 68, 255)
GLASS = (216, 172, 96, 255)
GLASS_DK = (170, 128, 62, 255)
GLASS_LT = (236, 210, 140, 255)


def _stone_base(d, rng, x0, y0, x1, y1):
    d.rectangle([x0 - 3, y0 - 3, x1 + 3, y1 + 3], fill=OUTLINE)
    d.rectangle([x0, y0, x1, y1], fill=STONE)
    d.rectangle([x0, y0, x0 + 8, y1], fill=STONE_DK)
    d.rectangle([x1 - 8, y0, x1, y1], fill=STONE_LT)
    row_h = 11
    y = y0 + 2
    ri = 0
    while y < y1 - 2:
        off = (ri % 2) * 12
        d.line([(x0 + 1, y), (x1 - 1, y)], fill=MORTAR, width=1)
        bx = x0 + 6 + off
        while bx < x1 - 4:
            d.line([(bx, y + 1), (bx, min(y1 - 2, y + row_h - 1))], fill=MORTAR, width=1)
            bx += 22
        for _ in range(3):
            sx = rng.randint(x0 + 4, x1 - 10)
            sy = rng.randint(y + 1, min(y1 - 4, y + row_h - 3))
            d.rectangle([sx, sy, sx + 5, sy + 3],
                        fill=STONE_DK if rng.random() < 0.5 else STONE_LT)
        y += row_h
        ri += 1


def _post(d, x0, y0, x1, y1):
    d.rectangle([x0 - 2, y0 - 2, x1 + 2, y1 + 2], fill=OUTLINE)
    d.rectangle([x0, y0, x1, y1], fill=WOOD)
    d.line([(x0 + 3, y0 + 2), (x0 + 3, y1 - 1)], fill=WOOD_LT, width=2)
    d.line([(x1 - 2, y0 + 2), (x1 - 2, y1 - 1)], fill=WOOD_DK, width=2)
    # 가로 띠(단)
    for yy in (y0 + 18, (y0 + y1) // 2, y1 - 14):
        d.rectangle([x0 - 1, yy - 2, x1 + 1, yy + 2], fill=WOOD_DK)
        d.line([(x0, yy), (x1, yy)], fill=WOOD_LT, width=1)


def _lantern(d, cx, top, w=28, h=42):
    """사각 랜턴 — 철틀 + 웜톤 유리. 글로우 없음."""
    x0, x1 = cx - w, cx + w
    y0, y1 = top, top + h
    # 지붕(작은 박공)
    d.polygon([(cx, y0 - 16), (x0 - 8, y0 + 4), (x1 + 8, y0 + 4)], fill=OUTLINE)
    d.polygon([(cx, y0 - 13), (x0 - 5, y0 + 2), (x1 + 5, y0 + 2)], fill=WOOD_DK)
    d.line([(cx, y0 - 13), (x0 - 5, y0 + 2)], fill=WOOD_LT, width=1)
    # 꼭지
    d.rectangle([cx - 3, y0 - 22, cx + 3, y0 - 13], fill=OUTLINE)
    d.rectangle([cx - 2, y0 - 21, cx + 2, y0 - 14], fill=IRON)
    d.ellipse([cx - 5, y0 - 28, cx + 5, y0 - 18], fill=OUTLINE)
    d.ellipse([cx - 4, y0 - 27, cx + 4, y0 - 19], fill=GOLD)
    # 본체
    d.rectangle([x0 - 3, y0, x1 + 3, y1 + 3], fill=OUTLINE)
    d.rectangle([x0, y0, x1, y1], fill=GLASS_DK)
    # 4칸 유리
    mid_x, mid_y = cx, (y0 + y1) // 2
    panes = [
        (x0 + 3, y0 + 4, mid_x - 3, mid_y - 2),
        (mid_x + 3, y0 + 4, x1 - 3, mid_y - 2),
        (x0 + 3, mid_y + 2, mid_x - 3, y1 - 4),
        (mid_x + 3, mid_y + 2, x1 - 3, y1 - 4),
    ]
    for a, b, c, e in panes:
        d.rectangle([a, b, c, e], fill=GLASS)
        d.line([(a + 2, b + 2), (a + 8, b + 2)], fill=GLASS_LT, width=1)
    # 철 십자
    d.line([(cx, y0 + 1), (cx, y1 - 1)], fill=IRON, width=3)
    d.line([(x0 + 1, mid_y), (x1 - 1, mid_y)], fill=IRON, width=3)
    d.rectangle([x0, y0, x1, y0 + 4], fill=WOOD)
    d.rectangle([x0, y1 - 5, x1, y1], fill=WOOD_DK)
    # 밑 꼭지
    d.polygon([(cx, y1 + 12), (cx - 6, y1), (cx + 6, y1)], fill=OUTLINE)
    d.polygon([(cx, y1 + 9), (cx - 4, y1 + 1), (cx + 4, y1 + 1)], fill=IRON)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    rng = random.Random(20260827)

    cx = 128
    # 밑 얇은 그림자 띠(원 아님 — §6-A)
    d.rectangle([88, 228, 168, 234], fill=(30, 24, 20, 90))

    # 석조 받침
    _stone_base(d, rng, 96, 196, 160, 228)

    # 목재 기둥(받침 위 → 팔 아래)
    _post(d, 116, 78, 140, 198)

    # 가로 팔(오른쪽)
    d.rectangle([138, 78, 196, 92], fill=OUTLINE)
    d.rectangle([140, 80, 194, 90], fill=WOOD)
    d.line([(140, 82), (194, 82)], fill=WOOD_LT, width=1)
    # 팔 끝 고리
    d.ellipse([186, 88, 202, 104], fill=OUTLINE)
    d.ellipse([189, 91, 199, 101], fill=IRON)
    d.line([(193, 100), (193, 112)], fill=OUTLINE, width=3)

    # 랜턴(팔 아래)
    _lantern(d, 193, 114, w=22, h=38)

    out = HERE / "out_oxalpha_village_lamp.png"
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
