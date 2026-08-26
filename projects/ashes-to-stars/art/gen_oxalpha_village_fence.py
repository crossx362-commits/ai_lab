#!/usr/bin/env python3
"""ox-alpha 마을 울타리(village_fence_0) 코드합성 — 256×256 목조 직선 목책.

배경: 집·헛간·우물·수레·가로등은 ox-alpha 256으로 통일됐으나
울타리(village_fence_0)는 아직 옛 나노바나나(1140×1490·1.8MB, 그레이)라 톤·해상도가
따로 논다. 같은 웜톤 팔레트·같은 256 캔버스의 직선 목책으로 교체한다.
village_fence_1 은 다음 칸 — 이 스크립트는 fence_0만 출력한다.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, village_fence_0=0.90)이라 해상도 스왑이 스케일-세이프.
.cs 변경 0. 파일명 동일 → .meta/GUID 무변경, FieldDecor 이름 참조 무변경.

디자인(목책으로 확실히 읽히게): 끝 기둥 2본(두껍·뾰족) + 중간 말뚝 3본 + 가로대 2줄
(널판). §6-A: 바닥 큰 원/고리/글로우 금지, 그림자는 밑 얇은 띠뿐.

사용: python3 gen_oxalpha_village_fence.py
출력: art/out_oxalpha_village_fence.png (256×256 RGBA)
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


def _post(d, rng, cx, top, bot, half_w, pointed=True):
    x0, x1 = cx - half_w, cx + half_w
    d.rectangle([x0 - 2, top + (10 if pointed else 0) - 2, x1 + 2, bot + 2], fill=OUTLINE)
    d.rectangle([x0, top + (10 if pointed else 0), x1, bot], fill=WOOD)
    d.line([(x0 + 2, top + 12), (x0 + 2, bot - 1)], fill=WOOD_LT, width=1)
    d.line([(x1 - 1, top + 12), (x1 - 1, bot - 1)], fill=WOOD_DK, width=1)
    if pointed:
        d.polygon([(cx, top - 2), (x0 - 2, top + 14), (x1 + 2, top + 14)], fill=OUTLINE)
        d.polygon([(cx, top), (x0, top + 12), (x1, top + 12)], fill=WOOD_LT)
        d.line([(cx, top), (x0, top + 12)], fill=WOOD, width=1)
    for _ in range(2):
        sy = rng.randint(top + 18, bot - 8)
        d.line([(x0 + 1, sy), (x1 - 1, sy)], fill=WOOD_DK, width=1)


def _rail(d, x0, y, x1, thick=10):
    d.rectangle([x0 - 2, y - 2, x1 + 2, y + thick + 2], fill=OUTLINE)
    d.rectangle([x0, y, x1, y + thick], fill=WOOD)
    d.line([(x0, y + 2), (x1, y + 2)], fill=WOOD_LT, width=1)
    d.line([(x0, y + thick - 2), (x1, y + thick - 2)], fill=WOOD_DK, width=1)
    # 널 이음
    for x in range(x0 + 28, x1 - 8, 36):
        d.line([(x, y + 1), (x, y + thick - 1)], fill=WOOD_DK, width=1)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    rng = random.Random(20260827)

    # 밑 얇은 그림자 띠(원 아님 — §6-A)
    d.rectangle([28, 214, 228, 222], fill=(30, 24, 20, 90))

    bot = 216
    # 가로대(말뚝 뒤로)
    _rail(d, 36, 118, 220, thick=12)
    _rail(d, 36, 168, 220, thick=12)

    # 중간 말뚝 3
    for cx, top, hw in ((88, 92, 8), (128, 86, 9), (168, 94, 8)):
        _post(d, rng, cx, top, bot, hw, pointed=True)

    # 끝 기둥(더 굵고 높음)
    _post(d, rng, 48, 72, bot, 12, pointed=True)
    _post(d, rng, 208, 76, bot, 12, pointed=True)

    # 가로대를 기둥 앞으로 한 줄씩 다시(가려지지 않는 이음)
    d.rectangle([58, 120, 198, 130], fill=WOOD)
    d.line([(58, 122), (198, 122)], fill=WOOD_LT, width=1)
    d.rectangle([58, 170, 198, 180], fill=WOOD)
    d.line([(58, 172), (198, 172)], fill=WOOD_LT, width=1)
    # 못
    for x in (48, 88, 128, 168, 208):
        d.ellipse([x - 3, 122, x + 3, 128], fill=IRON)
        d.ellipse([x - 3, 172, x + 3, 178], fill=IRON)

    out = HERE / "out_oxalpha_village_fence.png"
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
