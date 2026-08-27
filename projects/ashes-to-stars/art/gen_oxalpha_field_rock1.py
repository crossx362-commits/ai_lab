#!/usr/bin/env python3
"""ox-alpha 필드 바위(field_rock_1) 코드합성 — 256×256 쌍석.

배경: field_rock_0 은 ox-alpha 256(c27a4fb9)인데 field_rock_1 은 아직 나노바나나
1694×1353·2.1MB 그레이 고리바위라 톤이 따로 놀았다. 같은 웜톤·같은 256,
단일 _0과 구별되는 솔리드 쌍석으로 교체한다. field_rock_0/2 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, field_rock_1=0.70)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 큰 돌 + 옆에 작은 돌(구멍/고리 없음). §6-A: 바닥 큰 원/고리/글로우 금지,
그림자는 밑 얇은 띠뿐.

사용: python3 gen_oxalpha_field_rock1.py
출력: art/out_oxalpha_field_rock1.png (256×256 RGBA)
"""
from pathlib import Path
import random

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

STONE = (150, 142, 126, 255)
STONE_DK = (108, 100, 86, 255)
STONE_LT = (188, 180, 162, 255)
MORTAR = (86, 80, 68, 255)
OUTLINE = (40, 24, 20, 255)


def _stone(d, rng, pts, fill, dk, lt):
    d.polygon([(p[0] - 2, p[1] - 2) for p in pts], fill=OUTLINE)
    d.polygon(pts, fill=fill)
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    for _ in range(5):
        x = rng.randint(min(xs) + 6, max(xs) - 8)
        y = rng.randint(min(ys) + 6, max(ys) - 8)
        d.rectangle([x, y, x + 5, y + 3], fill=dk if rng.random() < 0.5 else lt)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    rng = random.Random(20260827)

    d.rectangle([64, 196, 192, 204], fill=(30, 24, 20, 90))

    big = [(78, 176), (72, 148), (88, 124), (120, 118), (148, 136), (150, 168), (128, 188), (96, 186)]
    small = [(148, 180), (152, 154), (172, 142), (194, 154), (196, 178), (176, 190), (156, 188)]
    _stone(d, rng, big, STONE, STONE_DK, STONE_LT)
    d.polygon([(88, 132), (118, 122), (132, 140), (110, 150)], fill=STONE_LT)
    d.polygon([(128, 160), (146, 148), (146, 172), (124, 180)], fill=STONE_DK)
    d.line([(96, 150), (124, 168)], fill=MORTAR, width=1)
    _stone(d, rng, small, STONE_DK, MORTAR, STONE)
    d.polygon([(160, 150), (180, 146), (186, 162), (168, 166)], fill=STONE_LT)

    out = HERE / "out_oxalpha_field_rock1.png"
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
