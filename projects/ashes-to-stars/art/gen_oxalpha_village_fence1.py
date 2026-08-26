#!/usr/bin/env python3
"""ox-alpha 마을 울타리 모서리(village_fence_1) 코드합성 — 256×256 L자 목책.

배경: village_fence_0 은 ox-alpha 256 직선 목책으로 교체됨(5536cb00).
fence_1 은 아직 옛 나노바나나(1592×1361·1.9MB, 그레이 3/4)라 톤이 따로 논다.
같은 웜톤·같은 256, 직선과 구별되는 L자 모서리 목책으로 교체한다.
village_fence_0 파일은 이 스크립트가 쓰지 않는다.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, village_fence_1=0.95)이라 해상도 스왑이 스케일-세이프.
.cs 변경 0. 파일명 동일 → .meta/GUID 무변경.

디자인: 굵은 모서리 기둥 + 오른쪽 가로 목책(말뚝 2+가로대 2) + 앞쪽 세로 목책
(말뚝 2+가로대 2, 짧게). §6-A: 바닥 큰 원/고리/글로우 금지, 그림자는 밑 얇은 띠뿐.

사용: python3 gen_oxalpha_village_fence1.py
출력: art/out_oxalpha_village_fence1.png (256×256 RGBA)
"""
from pathlib import Path
import random

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

WOOD = (150, 104, 58, 255)
WOOD_DK = (96, 64, 34, 255)
WOOD_LT = (196, 150, 92, 255)
OUTLINE = (40, 24, 20, 255)
IRON = (86, 80, 68, 255)


def _post(d, rng, cx, top, bot, half_w):
    x0, x1 = cx - half_w, cx + half_w
    d.rectangle([x0 - 2, top + 8, x1 + 2, bot + 2], fill=OUTLINE)
    d.rectangle([x0, top + 10, x1, bot], fill=WOOD)
    d.line([(x0 + 2, top + 12), (x0 + 2, bot - 1)], fill=WOOD_LT, width=1)
    d.line([(x1 - 1, top + 12), (x1 - 1, bot - 1)], fill=WOOD_DK, width=1)
    d.polygon([(cx, top - 2), (x0 - 2, top + 14), (x1 + 2, top + 14)], fill=OUTLINE)
    d.polygon([(cx, top), (x0, top + 12), (x1, top + 12)], fill=WOOD_LT)
    for _ in range(2):
        sy = rng.randint(top + 20, bot - 10)
        d.line([(x0 + 1, sy), (x1 - 1, sy)], fill=WOOD_DK, width=1)


def _rail_h(d, x0, y, x1, thick=10):
    d.rectangle([x0 - 2, y - 2, x1 + 2, y + thick + 2], fill=OUTLINE)
    d.rectangle([x0, y, x1, y + thick], fill=WOOD)
    d.line([(x0, y + 2), (x1, y + 2)], fill=WOOD_LT, width=1)
    d.line([(x0, y + thick - 2), (x1, y + thick - 2)], fill=WOOD_DK, width=1)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    rng = random.Random(20260827)
    bot = 216

    # 밑 얇은 그림자 띠 — L자 발자국(원 아님)
    d.rectangle([40, 214, 128, 222], fill=(30, 24, 20, 90))
    d.rectangle([112, 214, 228, 222], fill=(30, 24, 20, 90))

    # 오른쪽 가로 날개(레일 먼저)
    _rail_h(d, 118, 120, 228, 11)
    _rail_h(d, 118, 168, 228, 11)
    _post(d, rng, 168, 96, bot, 8)
    _post(d, rng, 216, 88, bot, 9)

    # 앞쪽(아래) 날개 — 짧게 앞으로(화면 아래쪽 = 카메라 쪽)
    _rail_h(d, 48, 128, 118, 11)
    _rail_h(d, 48, 176, 118, 11)
    _post(d, rng, 56, 108, bot, 8)
    _post(d, rng, 92, 100, bot, 8)

    # 모서리 기둥(제일 굵고 높음, 마지막에)
    _post(d, rng, 128, 68, bot, 14)

    # 레일을 모서리 앞으로 한 겹
    d.rectangle([140, 122, 206, 131], fill=WOOD)
    d.line([(140, 124), (206, 124)], fill=WOOD_LT, width=1)
    d.rectangle([140, 170, 206, 179], fill=WOOD)
    d.line([(140, 172), (206, 172)], fill=WOOD_LT, width=1)
    d.rectangle([58, 130, 116, 139], fill=WOOD)
    d.line([(58, 132), (116, 132)], fill=WOOD_LT, width=1)
    d.rectangle([58, 178, 116, 187], fill=WOOD)
    d.line([(58, 180), (116, 180)], fill=WOOD_LT, width=1)
    for x, y in ((128, 124), (168, 124), (216, 124), (128, 172), (168, 172), (216, 172),
                 (56, 132), (92, 132), (128, 132), (56, 180), (92, 180), (128, 180)):
        d.ellipse([x - 3, y, x + 3, y + 6], fill=IRON)

    out = HERE / "out_oxalpha_village_fence1.png"
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
