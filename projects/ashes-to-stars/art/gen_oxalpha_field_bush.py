#!/usr/bin/env python3
"""ox-alpha 필드 덤불(field_bush_0) 코드합성 — 256×256 둥근 올리브 덤불.

배경: 나무·집은 ox-alpha 256인데 field_bush_0 은 아직 나노바나나
1461×1676·2.5MB 그레이 가시덤불이라 톤이 따로 논다. 같은 웜톤·같은 256의
살아 있는 들판 덤불로 교체한다. field_bush_1/2 는 다음 칸.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, field_bush_0=0.75)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 줄기 없이 겹친 올리브 잎 덩어리(낮고 넓음, 나무와 구별). §6-A: 바닥 큰
원/고리/글로우 금지, 그림자는 밑 얇은 띠뿐.

사용: python3 gen_oxalpha_field_bush.py
출력: art/out_oxalpha_field_bush.png (256×256 RGBA)
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

    d.rectangle([64, 198, 192, 206], fill=(30, 24, 20, 90))

    # 밑동 짧은 가지(덤불 시그니처, 나무는 아님)
    d.line([(cx - 8, 196), (cx - 22, 168)], fill=OUTLINE, width=5)
    d.line([(cx - 8, 196), (cx - 22, 168)], fill=WOOD_DK, width=3)
    d.line([(cx + 8, 196), (cx + 24, 170)], fill=OUTLINE, width=5)
    d.line([(cx + 8, 196), (cx + 24, 170)], fill=WOOD_DK, width=3)

    # 낮고 넓은 수관
    _blob(d, cx - 36, 158, 38, 28, LEAF_DK)
    _blob(d, cx + 38, 160, 36, 26, LEAF_DK)
    _blob(d, cx, 148, 48, 34, LEAF)
    _blob(d, cx - 14, 136, 30, 22, LEAF_LT)
    _blob(d, cx + 18, 140, 26, 20, LEAF, outline=False)
    _blob(d, cx + 4, 128, 22, 16, LEAF_LT, outline=False)
    for _ in range(26):
        x = rng.randint(70, 186)
        y = rng.randint(112, 184)
        if ((x - cx) / 72) ** 2 + ((y - 150) / 38) ** 2 > 1:
            continue
        d.ellipse([x, y, x + 5, y + 4], fill=rng.choice([LEAF_LT, LEAF_DK, LEAF]))

    out = HERE / "out_oxalpha_field_bush.png"
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
