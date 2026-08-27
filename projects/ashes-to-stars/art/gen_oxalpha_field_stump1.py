#!/usr/bin/env python3
"""ox-alpha 필드 그루터기(field_stump_1) 코드합성 — 256×256 꺾인 꼭대기.

배경: field_stump_0 은 ox-alpha 256인데 field_stump_1 는 아직 나노바나나
1695×1652·2.9MB 그레이 꺾인 그루터기라 톤이 따로 놀았다. 같은 웜톤 목재
팔레트·같은 256, 꺾인 꼭대기+옆 옹이구멍으로 교체한다. field_stump_0
(잘린 윗면+나이테) 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, field_stump_1=0.75)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 위로 가늘어지는 그루터기, 들쭉날쭉 꺾인 꼭대기(잘린 원판 아님),
오른쪽 옹이구멍 2개, 뭉툭한 뿌리. §6-A: 바닥 큰 원/고리/글로우 금지,
그림자는 밑 얇은 띠뿐.

사용: python3 gen_oxalpha_field_stump1.py
출력: art/out_oxalpha_field_stump1.png (256×256 RGBA)
"""
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

WOOD = (150, 104, 58, 255)
WOOD_DK = (96, 64, 34, 255)
WOOD_LT = (196, 150, 92, 255)
OUTLINE = (40, 24, 20, 255)
HOLE = (48, 32, 22, 255)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx = 128

    d.rectangle([82, 216, 176, 224], fill=(30, 24, 20, 90))

    # 뭉툭한 뿌리 4발
    roots = [
        [(cx - 54, 218), (cx - 28, 188), (cx - 12, 196), (cx - 30, 220)],
        [(cx + 54, 218), (cx + 28, 188), (cx + 12, 196), (cx + 32, 220)],
        [(cx - 18, 220), (cx - 8, 198), (cx + 4, 198), (cx - 6, 220)],
        [(cx + 8, 220), (cx + 6, 200), (cx + 18, 198), (cx + 22, 220)],
    ]
    for pts in roots:
        d.polygon([(p[0] - 2, p[1] - 2) for p in pts], fill=OUTLINE)
        d.polygon(pts, fill=WOOD_DK)

    # 몸통: 위로 가늘어지는 다각형 (꺾인 꼭대기)
    body = [
        (cx - 40, 196),
        (cx - 36, 150),
        (cx - 28, 118),
        (cx - 22, 96),   # 왼쪽 꼭대기 턱
        (cx - 8, 88),
        (cx + 4, 102),   # 가운데 깨진 이
        (cx + 16, 90),
        (cx + 28, 98),
        (cx + 34, 128),
        (cx + 40, 168),
        (cx + 38, 196),
    ]
    d.polygon([(p[0] - 3, p[1] - 2) for p in body], fill=OUTLINE)
    d.polygon(body, fill=WOOD)
    # 왼쪽 하이라이트 / 오른쪽 그늘
    d.polygon([(cx - 30, 190), (cx - 26, 130), (cx - 16, 100), (cx - 10, 190)], fill=WOOD_LT)
    d.polygon([(cx + 22, 190), (cx + 28, 140), (cx + 24, 110), (cx + 32, 190)], fill=WOOD_DK)
    # 세로 나무결
    for x, y0, y1 in ((cx - 8, 110, 188), (cx + 8, 118, 188), (cx + 18, 130, 186)):
        d.line([(x, y0), (x, y1)], fill=WOOD_DK, width=2)

    # 꼭대기 깨진 면(잘린 원판 아님)
    d.polygon([(cx - 20, 98), (cx - 8, 88), (cx + 2, 100), (cx - 6, 108)], fill=WOOD_LT)
    d.polygon([(cx + 6, 102), (cx + 16, 90), (cx + 26, 100), (cx + 14, 110)], fill=WOOD_DK)

    # 오른쪽 옹이구멍 2개
    d.ellipse([cx + 10, 138, cx + 28, 158], fill=OUTLINE)
    d.ellipse([cx + 12, 140, cx + 26, 156], fill=HOLE)
    d.ellipse([cx + 14, 162, cx + 26, 176], fill=OUTLINE)
    d.ellipse([cx + 16, 164, cx + 24, 174], fill=HOLE)

    out = HERE / "out_oxalpha_field_stump1.png"
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
