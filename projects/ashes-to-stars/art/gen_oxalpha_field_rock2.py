#!/usr/bin/env python3
"""ox-alpha 필드 바위(field_rock_2) 코드합성 — 256×256 잔돌 세 알.

배경: field_rock_0/1 은 ox-alpha 256인데 field_rock_2 는 아직 나노바나나
1850×1600·3.8MB 그레이 룬돌+글로우라 톤이 따로 놀았다. 같은 웜톤·같은 256,
큰 _0·쌍석 _1과 구별되는 작은 잔돌 세 알로 교체한다. field_rock_0/1 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, field_rock_2=0.30)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 작은 솔리드 잔돌 3알(룬/글로우/구멍 없음). §6-A: 바닥 큰 원/고리/글로우
금지, 그림자는 밑 얇은 띠뿐.

사용: python3 gen_oxalpha_field_rock2.py
출력: art/out_oxalpha_field_rock2.png (256×256 RGBA)
"""
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

STONE = (150, 142, 126, 255)
STONE_DK = (108, 100, 86, 255)
STONE_LT = (188, 180, 162, 255)
OUTLINE = (40, 24, 20, 255)


def _pebble(d, pts, fill, hi=None):
    d.polygon([(p[0] - 1, p[1] - 1) for p in pts], fill=OUTLINE)
    d.polygon(pts, fill=fill)
    if hi:
        d.polygon(hi, fill=STONE_LT)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)

    d.rectangle([96, 188, 160, 194], fill=(30, 24, 20, 90))

    # 세 알(작음)
    _pebble(d, [(108, 176), (104, 162), (116, 152), (132, 158), (130, 174), (118, 180)],
            STONE, hi=[(110, 160), (118, 154), (124, 162)])
    _pebble(d, [(132, 180), (136, 166), (150, 160), (160, 170), (154, 182), (140, 184)],
            STONE_DK, hi=[(140, 166), (148, 162), (150, 170)])
    _pebble(d, [(122, 168), (126, 156), (138, 154), (142, 164), (134, 170)],
            STONE, hi=[(128, 158), (134, 156), (136, 162)])

    out = HERE / "out_oxalpha_field_rock2.png"
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
