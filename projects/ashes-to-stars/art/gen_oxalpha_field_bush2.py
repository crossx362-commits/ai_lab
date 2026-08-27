#!/usr/bin/env python3
"""ox-alpha 필드 덤불(field_bush_2) 코드합성 — 256×256 작은 한 덩이 덤불.

배경: field_bush_0/1 은 ox-alpha 256인데 field_bush_2 는 아직 나노바나나
1794×1575·4.1MB 그레이 가시덤불이라 톤이 따로 논다. 같은 웜톤·같은 256,
둥근 _0·쌍봉 _1과 구별되는 작은 한 덩이 덤불로 교체한다. field_bush_0/1 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, field_bush_2=0.65)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 작은 올리브 한 덩이 + 위로 솟은 잎 몇 가닥. §6-A: 바닥 큰 원/고리/글로우
금지, 그림자는 밑 얇은 띠뿐.

사용: python3 gen_oxalpha_field_bush2.py
출력: art/out_oxalpha_field_bush2.png (256×256 RGBA)
"""
from pathlib import Path
import random

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

OUTLINE = (40, 24, 20, 255)
LEAF = (110, 132, 72, 255)
LEAF_DK = (70, 92, 48, 255)
LEAF_LT = (148, 168, 90, 255)
WOOD_DK = (96, 64, 34, 255)


def _blob(d, cx, cy, w, h, fill, outline=True):
    if outline:
        d.ellipse([cx - w - 3, cy - h - 3, cx + w + 3, cy + h + 3], fill=OUTLINE)
    d.ellipse([cx - w, cy - h, cx + w, cy + h], fill=fill)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    rng = random.Random(20260827)
    cx = 128

    d.rectangle([88, 198, 168, 206], fill=(30, 24, 20, 90))

    # 짧은 밑가지
    d.line([(cx - 6, 196), (cx - 14, 176)], fill=OUTLINE, width=4)
    d.line([(cx - 6, 196), (cx - 14, 176)], fill=WOOD_DK, width=2)
    d.line([(cx + 6, 196), (cx + 16, 176)], fill=OUTLINE, width=4)
    d.line([(cx + 6, 196), (cx + 16, 176)], fill=WOOD_DK, width=2)

    # 한 덩이(작음)
    _blob(d, cx, 160, 36, 28, LEAF)
    _blob(d, cx - 8, 150, 20, 16, LEAF_LT, outline=False)
    _blob(d, cx + 10, 164, 16, 12, LEAF_DK, outline=False)

    # 위로 솟은 잎 가닥(_0/_1과 실루엣 분리)
    for x0, y0, x1, y1 in (
        (cx - 10, 148, cx - 18, 118),
        (cx + 2, 146, cx + 4, 112),
        (cx + 12, 150, cx + 22, 122),
    ):
        d.line([(x0, y0), (x1, y1)], fill=OUTLINE, width=4)
        d.line([(x0, y0), (x1, y1)], fill=LEAF_LT, width=2)

    for _ in range(12):
        x = rng.randint(96, 160)
        y = rng.randint(136, 184)
        if ((x - cx) / 36) ** 2 + ((y - 160) / 28) ** 2 > 0.9:
            continue
        d.ellipse([x, y, x + 4, y + 3], fill=rng.choice([LEAF_LT, LEAF_DK, LEAF]))

    out = HERE / "out_oxalpha_field_bush2.png"
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
