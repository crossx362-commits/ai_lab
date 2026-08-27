#!/usr/bin/env python3
"""ox-alpha 탄 잔해(ash_charred_1) 코드합성 — 256×256 탄 목륜.

배경: ash_charred_0 은 ox-alpha 256인데 ash_charred_1 는 아직 나노바나나
1575×1761·2.9MB 그레이 탄 바퀴라 톤이 따로 놀았다. 같은 웜 숯톤·같은 256,
살대 목륜으로 교체한다. ash_charred_0(속빈 그루터기)·_2(판자) 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, ash_charred_1=0.50)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 3/4 각 탄 목륜 8살. 림·살·허브가 채워 바닥 고리로 안 읽힌다.
글로우/불꽃 금지. 그림자는 밑 얇은 띠뿐. village_cart 생목 바퀴와 색이 갈린다.

사용: python3 gen_oxalpha_ash_charred1.py
출력: art/out_oxalpha_ash_charred1.png (256×256 RGBA)
"""
import math
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

OUTLINE = (40, 24, 20, 255)
CHAR = (86, 74, 62, 255)
CHAR_DK = (48, 40, 34, 255)
CHAR_LT = (128, 114, 96, 255)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx, cy, r = 128, 148, 62

    d.rectangle([cx - r + 8, cy + r - 4, cx + r - 8, cy + r + 6], fill=(30, 24, 20, 90))

    # 바깥 림
    d.ellipse([cx - r - 4, cy - r - 4, cx + r + 4, cy + r + 4], fill=OUTLINE)
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=CHAR_DK)
    d.ellipse([cx - r + 14, cy - r + 14, cx + r - 14, cy + r - 14], fill=CHAR)
    # 위쪽 하이라이트 림
    d.arc([cx - r + 2, cy - r + 2, cx + r - 2, cy + r - 2], 200, 340, fill=CHAR_LT, width=6)
    # 금간 틈 (위오른쪽)
    d.polygon([(cx + 28, cy - r + 4), (cx + 40, cy - r + 18), (cx + 34, cy - r + 22),
               (cx + 22, cy - r + 8)], fill=OUTLINE)

    # 8살
    for i in range(8):
        ang = math.radians(i * 45 - 18)
        x2 = int(cx + (r - 16) * math.cos(ang))
        y2 = int(cy + (r - 16) * math.sin(ang))
        d.line([(cx, cy), (x2, y2)], fill=OUTLINE, width=6)
        d.line([(cx, cy), (x2, y2)], fill=CHAR_LT if i % 2 == 0 else CHAR, width=3)

    # 안쪽 림
    d.ellipse([cx - r + 12, cy - r + 12, cx + r - 12, cy + r - 12], outline=OUTLINE, width=3)
    # 허브
    d.ellipse([cx - 16, cy - 16, cx + 16, cy + 16], fill=OUTLINE)
    d.ellipse([cx - 13, cy - 13, cx + 13, cy + 13], fill=CHAR_DK)
    d.ellipse([cx - 6, cy - 6, cx + 6, cy + 6], fill=CHAR_LT)

    out = HERE / "out_oxalpha_ash_charred1.png"
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
