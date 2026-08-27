#!/usr/bin/env python3
"""ox-alpha 탄 잔해(ash_charred_0) 코드합성 — 256×256 속빈 탄 그루터기.

배경: 필드 대형 프랍은 ox-alpha 256인데 ash_charred_0 는 아직 나노바나나
1146×1665·1.8MB 그레이 탄 그루터기라 톤이 따로 놀았다. 같은 두꺼운 외곽선
·같은 256, 웜 숯톤 속빈 잔해로 교체한다. ash_charred_1(바퀴)·_2(판자)
미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, ash_charred_0=0.90)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 위로 들쭉날쭉한 탄 파편 원통, 가운데 검은 속빈, 오른쪽 짧은 가지.
글로우/불꽃/바닥 원 금지. 그림자는 밑 얇은 띠뿐. field_stump(잘린/꺾인
생목)와 실루엣·색이 갈린다.

사용: python3 gen_oxalpha_ash_charred.py
출력: art/out_oxalpha_ash_charred.png (256×256 RGBA)
"""
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

OUTLINE = (40, 24, 20, 255)
CHAR = (86, 74, 62, 255)
CHAR_DK = (48, 40, 34, 255)
CHAR_LT = (128, 114, 96, 255)
HOLE = (24, 20, 16, 255)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx = 128

    d.rectangle([88, 214, 168, 222], fill=(30, 24, 20, 90))

    # 바깥 파편 실루엣 (들쭉날쭉 꼭대기 3봉)
    body = [
        (cx - 38, 210),
        (cx - 42, 170),
        (cx - 36, 128),
        (cx - 28, 88),   # 왼쪽 봉
        (cx - 18, 102),
        (cx - 8, 78),    # 가운데 높은 봉
        (cx + 6, 96),
        (cx + 18, 86),   # 오른쪽 봉
        (cx + 30, 120),
        (cx + 38, 164),
        (cx + 36, 210),
    ]
    d.polygon([(p[0] - 3, p[1] - 2) for p in body], fill=OUTLINE)
    d.polygon(body, fill=CHAR)
    d.polygon([(cx - 32, 200), (cx - 28, 130), (cx - 16, 92), (cx - 10, 200)], fill=CHAR_LT)
    d.polygon([(cx + 16, 200), (cx + 26, 140), (cx + 22, 100), (cx + 30, 200)], fill=CHAR_DK)
    # 세로 탄 결
    for x, y0, y1 in ((cx - 12, 110, 200), (cx + 4, 118, 200), (cx + 16, 128, 198)):
        d.line([(x, y0), (x, y1)], fill=CHAR_DK, width=2)

    # 속빈
    d.ellipse([cx - 16, 128, cx + 16, 186], fill=OUTLINE)
    d.ellipse([cx - 13, 132, cx + 13, 182], fill=HOLE)

    # 오른쪽 짧은 탄 가지
    d.polygon([(cx + 28, 156), (cx + 52, 144), (cx + 56, 152), (cx + 32, 168)], fill=OUTLINE)
    d.polygon([(cx + 30, 158), (cx + 50, 146), (cx + 52, 152), (cx + 32, 164)], fill=CHAR_DK)

    out = HERE / "out_oxalpha_ash_charred.png"
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
