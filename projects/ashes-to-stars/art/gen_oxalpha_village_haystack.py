#!/usr/bin/env python3
"""ox-alpha 마을 건초더미(village_haystack_0) 코드합성 — 256×256 원뿔 건초.

배경: 집·헛간·우물·수레·가로등·울타리는 ox-alpha 256으로 통일됐으나
건초더미(village_haystack_0)는 아직 옛 나노바나나(1714×1573·3.2MB, 그레이+청광)라
톤·해상도가 따로 논다. 같은 웜톤 팔레트·같은 256 캔버스의 원뿔 건초더미로 교체한다.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, village_haystack_0=2.20)이라 해상도 스왑이 스케일-세이프.
.cs 변경 0. 파일명 동일 → .meta/GUID 무변경, FieldDecor 이름 참조 무변경.

디자인(건초더미로 확실히 읽히게): 넓은 타원 밑동 + 위로 갈수록 좁아지는 짚 무더기
(겹친 타원·짧은 짚 획) + 꼭대기 목재 말뚝. §6-A: 바닥 큰 원/고리/글로우 금지 —
그림자는 밑 얇은 띠뿐, 옛 나노바나나 청광 없음. 실루엣 우선·두꺼운 아웃라인.

사용: python3 gen_oxalpha_village_haystack.py
출력: art/out_oxalpha_village_haystack.png (256×256 RGBA)
"""
from pathlib import Path
import random

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

WOOD = (150, 104, 58, 255)
WOOD_DK = (96, 64, 34, 255)
WOOD_LT = (196, 150, 92, 255)
OUTLINE = (40, 24, 20, 255)
HAY = (196, 164, 78, 255)
HAY_DK = (150, 118, 50, 255)
HAY_MD = (176, 140, 64, 255)
GOLD = (216, 172, 96, 255)


def _mound(d, rng, cx, cy, w, h, fill, outline=True):
    if outline:
        d.ellipse([cx - w - 3, cy - h - 3, cx + w + 3, cy + h + 3], fill=OUTLINE)
    d.ellipse([cx - w, cy - h, cx + w, cy + h], fill=fill)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    rng = random.Random(20260827)
    cx = 128

    # 밑 얇은 그림자 띠(원 아님 — §6-A). 큰 원/글로우 금지.
    d.rectangle([56, 222, 200, 230], fill=(30, 24, 20, 90))

    # 밑동(넓고 납작)
    _mound(d, rng, cx, 196, 78, 28, HAY_DK)
    _mound(d, rng, cx - 8, 190, 70, 24, HAY_MD, outline=False)
    _mound(d, rng, cx + 18, 192, 48, 20, HAY, outline=False)

    # 중간 더미
    _mound(d, rng, cx, 154, 58, 36, HAY)
    _mound(d, rng, cx - 14, 148, 40, 26, GOLD, outline=False)
    _mound(d, rng, cx + 16, 152, 34, 22, HAY_DK, outline=False)

    # 윗더미(원뿔 끝)
    _mound(d, rng, cx + 2, 112, 36, 28, HAY)
    _mound(d, rng, cx - 6, 104, 24, 18, GOLD, outline=False)
    d.polygon([(cx, 72), (cx - 28, 118), (cx + 30, 118)], fill=OUTLINE)
    d.polygon([(cx, 76), (cx - 24, 116), (cx + 26, 116)], fill=HAY)
    d.line([(cx, 76), (cx - 24, 116)], fill=GOLD, width=2)
    d.line([(cx, 76), (cx + 26, 116)], fill=HAY_DK, width=2)

    # 짚 획(실루엣 안쪽에만)
    for _ in range(90):
        x = rng.randint(60, 196)
        y = rng.randint(86, 214)
        # 더미 대략 타원 안
        if ((x - cx) / 80) ** 2 + ((y - 160) / 70) ** 2 > 1.05:
            continue
        col = rng.choice([HAY_DK, GOLD, HAY_MD, HAY])
        dx = rng.randint(-8, 8)
        dy = rng.randint(-3, 5)
        d.line([(x, y), (x + dx, y + dy)], fill=col, width=1)

    # 꼭대기 목재 말뚝(건초더미 시그니처)
    d.rectangle([cx - 4, 48, cx + 4, 86], fill=OUTLINE)
    d.rectangle([cx - 3, 50, cx + 3, 84], fill=WOOD)
    d.line([(cx - 1, 52), (cx - 1, 82)], fill=WOOD_LT, width=1)
    d.polygon([(cx, 40), (cx - 6, 54), (cx + 6, 54)], fill=OUTLINE)
    d.polygon([(cx, 42), (cx - 4, 52), (cx + 4, 52)], fill=WOOD_LT)

    out = HERE / "out_oxalpha_village_haystack.png"
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
