#!/usr/bin/env python3
"""ox-alpha 필드 그루터기(field_stump_0) 코드합성 — 256×256 잘린 윗면.

배경: field_rock_* 은 ox-alpha 256인데 field_stump_0 는 아직 나노바나나
1725×1567·2.8MB 그레이 그루터기+가운데 글로우라 톤이 따로 놀았다. 같은 웜톤
목재 팔레트·같은 256, 잘린 윗면+나이테로 교체한다. field_stump_1(꺾인 꼭대기)
·field_rock_* 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, field_stump_0=0.80)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 잘린 원통 그루터기(앞 3/4), 윗면 나이테+가운데 구멍(글로우 없음),
벌어진 뿌리 3발. §6-A: 바닥 큰 원/고리/글로우 금지, 그림자는 밑 얇은 띠뿐.

사용: python3 gen_oxalpha_field_stump.py
출력: art/out_oxalpha_field_stump.png (256×256 RGBA)
"""
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

WOOD = (150, 104, 58, 255)
WOOD_DK = (96, 64, 34, 255)
WOOD_LT = (196, 150, 92, 255)
OUTLINE = (40, 24, 20, 255)
RING = (72, 48, 28, 255)
HOLE = (48, 32, 22, 255)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx = 128

    # 밑 얇은 그림자 띠(원 아님)
    d.rectangle([78, 214, 178, 222], fill=(30, 24, 20, 90))

    # 뿌리 3발
    d.polygon([(cx - 52, 216), (cx - 22, 188), (cx - 8, 196), (cx - 28, 218)], fill=OUTLINE)
    d.polygon([(cx - 48, 214), (cx - 20, 190), (cx - 10, 196), (cx - 26, 214)], fill=WOOD_DK)
    d.polygon([(cx + 52, 216), (cx + 22, 188), (cx + 8, 196), (cx + 28, 218)], fill=OUTLINE)
    d.polygon([(cx + 48, 214), (cx + 20, 190), (cx + 10, 196), (cx + 26, 214)], fill=WOOD_DK)
    d.polygon([(cx - 10, 216), (cx - 4, 200), (cx + 4, 200), (cx + 12, 216)], fill=OUTLINE)
    d.polygon([(cx - 8, 214), (cx - 2, 202), (cx + 2, 202), (cx + 10, 214)], fill=WOOD)

    # 몸통(원통)
    d.ellipse([cx - 46, 168, cx + 46, 216], fill=OUTLINE)
    d.rectangle([cx - 46, 118, cx + 46, 192], fill=OUTLINE)
    d.ellipse([cx - 44, 170, cx + 44, 212], fill=WOOD_DK)
    d.rectangle([cx - 44, 120, cx + 44, 190], fill=WOOD)
    # 왼쪽 하이라이트 / 오른쪽 그늘
    d.rectangle([cx - 40, 124, cx - 28, 186], fill=WOOD_LT)
    d.rectangle([cx + 28, 124, cx + 40, 186], fill=WOOD_DK)
    # 나무결
    for x in range(cx - 36, cx + 38, 10):
        d.line([(x, 126), (x, 186)], fill=WOOD_DK, width=1)

    # 윗면(잘린 타원 + 나이테)
    d.ellipse([cx - 48, 96, cx + 48, 148], fill=OUTLINE)
    d.ellipse([cx - 44, 100, cx + 44, 144], fill=WOOD_LT)
    d.ellipse([cx - 34, 108, cx + 34, 136], outline=RING, width=2)
    d.ellipse([cx - 24, 114, cx + 24, 130], outline=RING, width=2)
    d.ellipse([cx - 14, 118, cx + 14, 126], outline=RING, width=1)
    # 가운데 구멍(글로우 없음)
    d.ellipse([cx - 8, 118, cx + 8, 128], fill=OUTLINE)
    d.ellipse([cx - 6, 120, cx + 6, 126], fill=HOLE)
    # 방사 균열 두 줄
    d.line([(cx - 6, 122), (cx - 38, 112)], fill=RING, width=2)
    d.line([(cx + 6, 124), (cx + 36, 132)], fill=RING, width=2)

    out = HERE / "out_oxalpha_field_stump.png"
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
