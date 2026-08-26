#!/usr/bin/env python3
"""ox-alpha 필드 나무(field_tree_2) 코드합성 — 256×256 갈라진 쌍수관.

배경: field_tree_0/1 은 ox-alpha 256인데 field_tree_2 는 아직 나노바나나
1113×1891·2.5MB 그레이 마른 나무+눈광이라 톤이 따로 논다. 같은 웜톤·같은 256,
곧은 _0·기운 _1과 구별되는 Y자 갈라진 들판 나무로 교체한다. field_tree_0/1/3 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, field_tree_2=4.40)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 짧은 줄기에서 좌·우로 갈라진 가지 + 양쪽 올리브 수관. §6-A: 바닥 큰
원/고리/글로우 금지, 옛 눈광 없음, 그림자는 밑 얇은 띠뿐.

사용: python3 gen_oxalpha_field_tree2.py
출력: art/out_oxalpha_field_tree2.png (256×256 RGBA)
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

    d.rectangle([88, 226, 168, 234], fill=(30, 24, 20, 90))

    # 뿌리
    d.polygon([(cx - 24, 228), (cx - 8, 204), (cx + 8, 204), (cx + 26, 228)], fill=OUTLINE)
    d.polygon([(cx - 20, 226), (cx - 6, 208), (cx + 6, 208), (cx + 22, 226)], fill=WOOD_DK)

    # 짧은 줄기
    d.rectangle([cx - 12, 148, cx + 12, 214], fill=OUTLINE)
    d.rectangle([cx - 10, 150, cx + 10, 212], fill=WOOD)
    d.line([(cx - 4, 154), (cx - 4, 208)], fill=WOOD_LT, width=2)
    d.line([(cx + 4, 154), (cx + 4, 208)], fill=WOOD_DK, width=2)

    # Y자 가지
    d.line([(cx - 4, 160), (cx - 52, 92)], fill=OUTLINE, width=10)
    d.line([(cx - 4, 160), (cx - 52, 92)], fill=WOOD, width=6)
    d.line([(cx + 4, 160), (cx + 56, 88)], fill=OUTLINE, width=10)
    d.line([(cx + 4, 160), (cx + 56, 88)], fill=WOOD, width=6)
    d.line([(cx - 48, 100), (cx - 4, 156)], fill=WOOD_LT, width=2)
    d.line([(cx + 8, 156), (cx + 52, 96)], fill=WOOD_DK, width=2)

    # 좌 수관
    _blob(d, 76, 78, 40, 34, LEAF_DK)
    _blob(d, 64, 58, 28, 24, LEAF)
    _blob(d, 88, 52, 22, 18, LEAF_LT, outline=False)
    # 우 수관
    _blob(d, 184, 74, 42, 36, LEAF)
    _blob(d, 198, 54, 30, 24, LEAF_LT)
    _blob(d, 170, 50, 20, 16, LEAF_DK, outline=False)

    for _ in range(24):
        x = rng.randint(40, 220)
        y = rng.randint(28, 110)
        left = ((x - 76) / 48) ** 2 + ((y - 68) / 38) ** 2 <= 1
        right = ((x - 184) / 50) ** 2 + ((y - 66) / 40) ** 2 <= 1
        if not (left or right):
            continue
        d.ellipse([x, y, x + 5, y + 4], fill=rng.choice([LEAF_LT, LEAF_DK, LEAF]))

    out = HERE / "out_oxalpha_field_tree2.png"
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
