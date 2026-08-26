#!/usr/bin/env python3
"""ox-alpha 필드 나무(field_tree_0) 코드합성 — 256×256 키 큰 수관.

배경: village_tree_0 은 ox-alpha 256(ea1bf16c)인데 field_tree_0 은 아직 나노바나나
1006×1764·1.7MB 그레이 마른 나무라 톤이 따로 논다. 같은 웜톤·같은 256,
마을 나무보다 키 큰 들판 나무로 교체한다. field_tree_1~3 은 다음 칸.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, field_tree_0=5.20)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 긴 줄기 + 넓은 올리브 수관(5덩이, 마을 나무 3덩이와 구별). §6-A:
바닥 큰 원/고리/글로우 금지, 그림자는 밑 얇은 띠뿐.

사용: python3 gen_oxalpha_field_tree.py
출력: art/out_oxalpha_field_tree.png (256×256 RGBA)
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

    d.rectangle([96, 228, 160, 236], fill=(30, 24, 20, 90))

    # 뿌리
    d.polygon([(cx - 26, 230), (cx - 8, 204), (cx + 8, 204), (cx + 28, 230)], fill=OUTLINE)
    d.polygon([(cx - 22, 228), (cx - 6, 208), (cx + 6, 208), (cx + 24, 228)], fill=WOOD_DK)

    # 긴 줄기(마을 나무보다 가늘고 김)
    d.rectangle([cx - 11, 96, cx + 11, 216], fill=OUTLINE)
    d.rectangle([cx - 9, 98, cx + 9, 214], fill=WOOD)
    d.line([(cx - 4, 102), (cx - 4, 210)], fill=WOOD_LT, width=2)
    d.line([(cx + 5, 102), (cx + 5, 210)], fill=WOOD_DK, width=2)
    for y in range(110, 200, 16):
        d.line([(cx - 8, y), (cx + 8, y)], fill=WOOD_DK, width=1)

    # 가지
    d.line([(cx - 6, 118), (cx - 48, 88)], fill=OUTLINE, width=7)
    d.line([(cx - 6, 118), (cx - 48, 88)], fill=WOOD, width=4)
    d.line([(cx + 6, 124), (cx + 52, 92)], fill=OUTLINE, width=7)
    d.line([(cx + 6, 124), (cx + 52, 92)], fill=WOOD, width=4)

    # 수관 5덩이 — 마을(3)보다 넓고 높음
    _blob(d, cx - 48, 78, 36, 30, LEAF_DK)
    _blob(d, cx + 50, 82, 34, 28, LEAF_DK)
    _blob(d, cx - 18, 52, 40, 34, LEAF)
    _blob(d, cx + 22, 48, 38, 32, LEAF)
    _blob(d, cx + 2, 36, 44, 36, LEAF_LT)
    _blob(d, cx - 10, 40, 22, 16, LEAF, outline=False)
    _blob(d, cx + 20, 58, 18, 14, LEAF_DK, outline=False)
    for _ in range(32):
        x = rng.randint(56, 200)
        y = rng.randint(18, 110)
        if ((x - cx) / 82) ** 2 + ((y - 62) / 48) ** 2 > 1:
            continue
        d.ellipse([x, y, x + 5, y + 4], fill=rng.choice([LEAF_LT, LEAF_DK, LEAF]))

    out = HERE / "out_oxalpha_field_tree.png"
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
