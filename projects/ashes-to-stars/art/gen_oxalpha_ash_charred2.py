#!/usr/bin/env python3
"""ox-alpha 탄 잔해(ash_charred_2) 코드합성 — 256×256 탄 판자 묶음.

배경: ash_charred_0/1 은 ox-alpha 256인데 ash_charred_2 는 아직 나노바나나
1761×1779·4.0MB 그레이 판자+뿔형 탄흔이라 톤이 따로 놀았다. 같은 웜 숯톤
·같은 256, 세로 판자 4장+불규칙 탄 얼룩으로 교체한다(할로우 뿔 실루엣 없음).
ash_charred_0(그루터기)·_1(바퀴) 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, ash_charred_2=0.85)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 3/4 각 세로 판자 4장, 가운데 불규칙 탄 얼룩(생물/뿔 실루엣 아님).
글로우 금지. 그림자는 밑 얇은 띠뿐.

사용: python3 gen_oxalpha_ash_charred2.py
출력: art/out_oxalpha_ash_charred2.png (256×256 RGBA)
"""
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

OUTLINE = (40, 24, 20, 255)
CHAR = (86, 74, 62, 255)
CHAR_DK = (48, 40, 34, 255)
CHAR_LT = (128, 114, 96, 255)
HOLE = (24, 20, 16, 255)


def _plank(d, x0, y0, x1, y1, fill):
    d.polygon([(x0 - 2, y0 - 2), (x1 + 2, y0 - 2), (x1 + 2, y1 + 2), (x0 - 2, y1 + 2)], fill=OUTLINE)
    d.polygon([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], fill=fill)
    # 나무결
    mid = (x0 + x1) // 2
    d.line([(mid, y0 + 8), (mid, y1 - 8)], fill=CHAR_DK, width=1)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)

    d.rectangle([64, 208, 192, 216], fill=(30, 24, 20, 90))

    # 세로 판자 4장 (폭·높이 살짝 다르게, 아래 들쭉날쭉)
    _plank(d, 72, 78, 98, 206, CHAR_DK)
    _plank(d, 98, 70, 128, 210, CHAR)
    _plank(d, 128, 74, 158, 208, CHAR_LT)
    _plank(d, 158, 82, 186, 204, CHAR)

    # 가운데 불규칙 탄 얼룩 (뿔/벌레/아이콘 실루엣 아님)
    d.ellipse([106, 116, 152, 172], fill=HOLE)
    d.ellipse([100, 132, 128, 164], fill=HOLE)
    d.ellipse([132, 124, 158, 156], fill=HOLE)
    d.ellipse([118, 136, 140, 158], fill=CHAR_DK)

    out = HERE / "out_oxalpha_ash_charred2.png"
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
