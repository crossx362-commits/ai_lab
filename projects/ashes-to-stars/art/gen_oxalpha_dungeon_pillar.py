#!/usr/bin/env python3
"""ox-alpha 던전 기둥(dungeon_pillar_0) 코드합성 — 256×256 각진 석주.

배경: dungeon_wall_* 은 ox-alpha 256인데 dungeon_pillar_0 는 아직 나노바나나
913×1775·2.1MB 그레이 석주+눈광이라 톤이 따로 놀았다. 같은 차가운 석조
·같은 256, 3/4 각진 기둥으로 교체한다(눈/글로우 없음). dungeon_pillar_1
(원주)·_2(부러진 꼭대기) 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, dungeon_pillar_0=2.40)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 밑동+몸통+주두가 있는 각진 석주. 글로우/눈/할로우 금지.
그림자는 밑 얇은 띠뿐.

사용: python3 gen_oxalpha_dungeon_pillar.py
출력: art/out_oxalpha_dungeon_pillar.png (256×256 RGBA)
"""
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

OUTLINE = (40, 24, 20, 255)
STONE = (132, 128, 122, 255)
STONE_DK = (86, 82, 76, 255)
STONE_LT = (172, 166, 156, 255)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx = 128

    d.rectangle([96, 226, 160, 234], fill=(30, 24, 20, 90))

    # 밑동
    d.polygon([(cx - 32, 228), (cx - 28, 208), (cx + 28, 208), (cx + 32, 228)], fill=OUTLINE)
    d.polygon([(cx - 28, 226), (cx - 24, 210), (cx + 24, 210), (cx + 28, 226)], fill=STONE_DK)

    # 몸통 앞면
    d.polygon([(cx - 20, 208), (cx - 18, 52), (cx + 6, 44), (cx + 8, 208)], fill=OUTLINE)
    d.polygon([(cx - 16, 206), (cx - 14, 56), (cx + 4, 50), (cx + 4, 206)], fill=STONE)
    # 오른쪽 면
    d.polygon([(cx + 8, 208), (cx + 6, 44), (cx + 22, 56), (cx + 24, 208)], fill=OUTLINE)
    d.polygon([(cx + 8, 206), (cx + 8, 54), (cx + 18, 62), (cx + 20, 206)], fill=STONE_DK)
    # 각인 두 줄(생물 아님)
    d.line([(cx - 10, 90), (cx, 88)], fill=STONE_DK, width=2)
    d.line([(cx - 10, 140), (cx, 138)], fill=STONE_DK, width=2)
    d.rectangle([cx - 8, 112, cx - 2, 122], outline=STONE_DK, width=2)

    # 주두
    d.polygon([(cx - 26, 56), (cx - 22, 36), (cx + 10, 28), (cx + 14, 48), (cx + 6, 56)], fill=OUTLINE)
    d.polygon([(cx - 22, 54), (cx - 18, 40), (cx + 8, 34), (cx + 10, 50)], fill=STONE_LT)
    d.polygon([(cx + 14, 48), (cx + 10, 28), (cx + 28, 40), (cx + 26, 58)], fill=OUTLINE)
    d.polygon([(cx + 14, 50), (cx + 12, 34), (cx + 24, 44), (cx + 22, 56)], fill=STONE_DK)

    out = HERE / "out_oxalpha_dungeon_pillar.png"
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
