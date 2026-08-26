#!/usr/bin/env python3
"""ox-alpha 마을 나무(village_tree_0) 코드합성 — 256×256 목조 줄기+잎 덩어리.

배경: 집·헛간·우물·수레·가로등·울타리·건초는 ox-alpha 256으로 통일됐으나
나무(village_tree_0)는 아직 옛 나노바나나(1753×1871·3.4MB, 그레이 마른 나무+눈광)라
톤이 따로 논다. 같은 웜톤 팔레트·같은 256의 살아 있는 마을 나무로 교체한다.
field_tree_* 는 손대지 않는다.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, village_tree_0=3.40)이라 해상도 스왑이 스케일-세이프.
.cs 변경 0. 파일명 동일 → .meta/GUID 무변경.

디자인(나무로 확실히 읽히게): 굵은 목재 줄기 + 작은 뿌리 + 3덩이 올리브 수관
(앞면). §6-A: 바닥 큰 원/고리/글로우 금지, 옛 눈광 없음, 그림자는 밑 얇은 띠뿐.

사용: python3 gen_oxalpha_village_tree.py
출력: art/out_oxalpha_village_tree.png (256×256 RGBA)
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

    # 밑 얇은 그림자 띠(원 아님)
    d.rectangle([92, 226, 164, 234], fill=(30, 24, 20, 90))

    # 뿌리(얇은 발)
    d.polygon([(cx - 28, 228), (cx - 8, 200), (cx + 8, 200), (cx + 30, 228)], fill=OUTLINE)
    d.polygon([(cx - 24, 226), (cx - 6, 204), (cx + 6, 204), (cx + 26, 226)], fill=WOOD_DK)
    d.rectangle([cx - 22, 218, cx - 8, 228], fill=WOOD)
    d.rectangle([cx + 8, 218, cx + 22, 228], fill=WOOD)

    # 줄기
    d.rectangle([cx - 14, 118, cx + 14, 214], fill=OUTLINE)
    d.rectangle([cx - 12, 120, cx + 12, 212], fill=WOOD)
    d.line([(cx - 6, 124), (cx - 6, 208)], fill=WOOD_LT, width=2)
    d.line([(cx + 6, 124), (cx + 6, 208)], fill=WOOD_DK, width=2)
    # 나무결
    for y in range(130, 200, 18):
        d.line([(cx - 10, y), (cx + 10, y)], fill=WOOD_DK, width=1)

    # 가지
    d.line([(cx - 8, 140), (cx - 40, 118)], fill=OUTLINE, width=8)
    d.line([(cx - 8, 140), (cx - 40, 118)], fill=WOOD, width=5)
    d.line([(cx + 8, 148), (cx + 42, 126)], fill=OUTLINE, width=8)
    d.line([(cx + 8, 148), (cx + 42, 126)], fill=WOOD, width=5)

    # 수관 3덩이 (뒤→앞)
    _blob(d, cx - 36, 96, 42, 36, LEAF_DK)
    _blob(d, cx + 38, 100, 40, 34, LEAF_DK)
    _blob(d, cx, 72, 52, 44, LEAF)
    _blob(d, cx - 8, 64, 28, 22, LEAF_LT, outline=False)
    _blob(d, cx + 18, 80, 22, 16, LEAF_DK, outline=False)
    # 잎 점
    for _ in range(28):
        x = rng.randint(70, 186)
        y = rng.randint(40, 130)
        if ((x - cx) / 70) ** 2 + ((y - 86) / 50) ** 2 > 1:
            continue
        col = rng.choice([LEAF_LT, LEAF_DK, LEAF])
        d.ellipse([x, y, x + 5, y + 4], fill=col)

    out = HERE / "out_oxalpha_village_tree.png"
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
