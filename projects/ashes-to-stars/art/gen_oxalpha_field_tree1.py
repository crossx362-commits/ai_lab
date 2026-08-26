#!/usr/bin/env python3
"""ox-alpha 필드 나무(field_tree_1) 코드합성 — 256×256 기울어진 수관.

배경: field_tree_0 은 ox-alpha 256(c71a22a6)인데 field_tree_1 은 아직 나노바나나
1426×1561·2.5MB 그레이 마른 나무라 톤이 따로 논다. 같은 웜톤·같은 256,
곧은 _0과 구별되는 기울어진 들판 나무로 교체한다. field_tree_0/2/3 은 손대지 않는다.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, field_tree_1=6.00)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 오른쪽으로 기운 줄기 + 상단 우측에 몰린 올리브 수관. §6-A: 바닥 큰
원/고리/글로우 금지, 그림자는 밑 얇은 띠뿐.

사용: python3 gen_oxalpha_field_tree1.py
출력: art/out_oxalpha_field_tree1.png (256×256 RGBA)
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

    d.rectangle([70, 228, 150, 236], fill=(30, 24, 20, 90))

    # 뿌리(왼쪽 무게)
    d.polygon([(70, 230), (104, 206), (124, 206), (148, 230)], fill=OUTLINE)
    d.polygon([(76, 228), (108, 210), (122, 210), (142, 228)], fill=WOOD_DK)

    # 기운 줄기: 밑(110) → 위(150)
    trunk = [(102, 214), (122, 214), (158, 100), (136, 92)]
    d.polygon([(p[0] - 2, p[1] - 2) for p in trunk], fill=OUTLINE)
    d.polygon(trunk, fill=WOOD)
    d.line([(110, 208), (142, 104)], fill=WOOD_LT, width=2)
    d.line([(118, 208), (152, 108)], fill=WOOD_DK, width=2)

    # 가지
    d.line([(148, 118), (96, 78)], fill=OUTLINE, width=7)
    d.line([(148, 118), (96, 78)], fill=WOOD, width=4)
    d.line([(152, 108), (196, 72)], fill=OUTLINE, width=7)
    d.line([(152, 108), (196, 72)], fill=WOOD, width=4)

    # 수관 — 우상단 클러스터 (곧은 _0과 실루엣 분리)
    _blob(d, 108, 70, 34, 28, LEAF_DK)
    _blob(d, 168, 54, 40, 32, LEAF)
    _blob(d, 198, 78, 32, 26, LEAF_DK)
    _blob(d, 148, 42, 36, 30, LEAF_LT)
    _blob(d, 136, 68, 28, 22, LEAF, outline=False)
    for _ in range(28):
        x = rng.randint(80, 220)
        y = rng.randint(22, 100)
        if ((x - 150) / 70) ** 2 + ((y - 62) / 40) ** 2 > 1:
            continue
        d.ellipse([x, y, x + 5, y + 4], fill=rng.choice([LEAF_LT, LEAF_DK, LEAF]))

    out = HERE / "out_oxalpha_field_tree1.png"
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
