#!/usr/bin/env python3
"""ox-alpha 필드 나무(field_tree_3) 코드합성 — 256×256 작은 둥근 묘목.

배경: field_tree_0~2 는 ox-alpha 256인데 field_tree_3 는 아직 나노바나나
1726×1620·3.0MB 그레이 마른 나무+눈광이라 톤이 따로 논다. 같은 웜톤·같은 256,
곧은 _0·기운 _1·Y자 _2과 구별되는 작은 둥근 들판 묘목으로 교체한다.
field_tree_0/1/2 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, field_tree_3=2.40)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 짧은 줄기 + 한 덩이 둥근 올리브 수관(묘목). §6-A: 바닥 큰 원/고리/글로우
금지, 옛 눈광 없음, 그림자는 밑 얇은 띠뿐.

사용: python3 gen_oxalpha_field_tree3.py
출력: art/out_oxalpha_field_tree3.png (256×256 RGBA)
"""
from pathlib import Path
import random

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

WOOD = (150, 104, 58, 255)
WOOD_DK = (96, 64, 34, 255)
WOOD_LT = (196, 150, 92, 255)
OUTLINE = (40, 24, 20, 255)
LEAF = (110, 132, 72, 255)
LEAF_DK = (70, 92, 48, 255)
LEAF_LT = (148, 168, 90, 255)


def _blob(d, cx, cy, w, h, fill, outline=True):
    if outline:
        d.ellipse([cx - w - 3, cy - h - 3, cx + w + 3, cy + h + 3], fill=OUTLINE)
    d.ellipse([cx - w, cy - h, cx + w, cy + h], fill=fill)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    rng = random.Random(20260827)
    cx = 128

    d.rectangle([100, 220, 156, 228], fill=(30, 24, 20, 90))

    # 짧은 뿌리
    d.polygon([(cx - 18, 222), (cx - 6, 198), (cx + 6, 198), (cx + 18, 222)], fill=OUTLINE)
    d.polygon([(cx - 14, 220), (cx - 4, 202), (cx + 4, 202), (cx + 14, 220)], fill=WOOD_DK)

    # 짧은 줄기
    d.rectangle([cx - 8, 150, cx + 8, 206], fill=OUTLINE)
    d.rectangle([cx - 6, 152, cx + 6, 204], fill=WOOD)
    d.line([(cx - 2, 156), (cx - 2, 200)], fill=WOOD_LT, width=1)
    d.line([(cx + 3, 156), (cx + 3, 200)], fill=WOOD_DK, width=1)

    # 작은 가지
    d.line([(cx - 4, 164), (cx - 28, 140)], fill=OUTLINE, width=5)
    d.line([(cx - 4, 164), (cx - 28, 140)], fill=WOOD, width=3)
    d.line([(cx + 4, 168), (cx + 30, 144)], fill=OUTLINE, width=5)
    d.line([(cx + 4, 168), (cx + 30, 144)], fill=WOOD, width=3)

    # 한 덩이 둥근 수관 (묘목 — _0의 5덩이·_2의 쌍수관과 구별)
    _blob(d, cx, 118, 52, 46, LEAF)
    _blob(d, cx - 10, 108, 28, 24, LEAF_LT, outline=False)
    _blob(d, cx + 16, 124, 22, 18, LEAF_DK, outline=False)
    _blob(d, cx + 4, 96, 16, 12, LEAF_LT, outline=False)
    for _ in range(18):
        x = rng.randint(78, 178)
        y = rng.randint(76, 160)
        if ((x - cx) / 52) ** 2 + ((y - 118) / 46) ** 2 > 0.92:
            continue
        d.ellipse([x, y, x + 4, y + 3], fill=rng.choice([LEAF_LT, LEAF_DK, LEAF]))

    out = HERE / "out_oxalpha_field_tree3.png"
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
