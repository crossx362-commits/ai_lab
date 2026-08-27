#!/usr/bin/env python3
"""ox-alpha 필드 덤불(field_bush_1) 코드합성 — 256×256 가로로 넓은 쌍봉 덤불.

배경: field_bush_0 은 ox-alpha 256(588b3675)인데 field_bush_1 은 아직 나노바나나
1850×1686·3.4MB 그레이 가시덤불+눈광이라 톤이 따로 논다. 같은 웜톤·같은 256,
둥근 _0과 구별되는 가로 쌍봉 덤불로 교체한다. field_bush_0/2 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, field_bush_1=0.75)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 좌·우 두 봉우리 올리브 덤불 + 작은 금빛 열매(글로우 아님). §6-A: 바닥 큰
원/고리/글로우 금지, 옛 눈광 없음, 그림자는 밑 얇은 띠뿐.

사용: python3 gen_oxalpha_field_bush1.py
출력: art/out_oxalpha_field_bush1.png (256×256 RGBA)
"""
from pathlib import Path
import random

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

OUTLINE = (40, 24, 20, 255)
LEAF = (110, 132, 72, 255)
LEAF_DK = (70, 92, 48, 255)
LEAF_LT = (148, 168, 90, 255)
WOOD_DK = (96, 64, 34, 255)
GOLD = (216, 172, 96, 255)


def _blob(d, cx, cy, w, h, fill, outline=True):
    if outline:
        d.ellipse([cx - w - 3, cy - h - 3, cx + w + 3, cy + h + 3], fill=OUTLINE)
    d.ellipse([cx - w, cy - h, cx + w, cy + h], fill=fill)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    rng = random.Random(20260827)

    d.rectangle([48, 198, 208, 206], fill=(30, 24, 20, 90))

    # 짧은 가지
    d.line([(90, 196), (78, 170)], fill=OUTLINE, width=5)
    d.line([(90, 196), (78, 170)], fill=WOOD_DK, width=3)
    d.line([(166, 196), (178, 172)], fill=OUTLINE, width=5)
    d.line([(166, 196), (178, 172)], fill=WOOD_DK, width=3)

    # 쌍봉(가로로 넓음 — 둥근 _0과 실루엣 분리)
    _blob(d, 88, 154, 42, 32, LEAF_DK)
    _blob(d, 168, 152, 44, 34, LEAF)
    _blob(d, 128, 162, 36, 24, LEAF_DK)
    _blob(d, 80, 140, 26, 20, LEAF_LT, outline=False)
    _blob(d, 176, 138, 24, 18, LEAF_LT, outline=False)
    for _ in range(22):
        x = rng.randint(50, 206)
        y = rng.randint(118, 186)
        left = ((x - 88) / 48) ** 2 + ((y - 152) / 36) ** 2 <= 1
        right = ((x - 168) / 50) ** 2 + ((y - 150) / 38) ** 2 <= 1
        mid = ((x - 128) / 40) ** 2 + ((y - 162) / 26) ** 2 <= 1
        if not (left or right or mid):
            continue
        d.ellipse([x, y, x + 5, y + 4], fill=rng.choice([LEAF_LT, LEAF_DK, LEAF]))

    # 작은 열매(점, 글로우 아님)
    for x, y in ((76, 138), (96, 148), (160, 136), (184, 150), (128, 156)):
        d.ellipse([x - 2, y - 2, x + 3, y + 3], fill=GOLD)

    out = HERE / "out_oxalpha_field_bush1.png"
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
