#!/usr/bin/env python3
"""ox-alpha 영지 배경(bg_estate) 코드합성 — 1280×720 마당+하늘.

배경: 영지 건물·프랍은 ox-alpha 256인데 bg_estate 는 아직 나노바나나
1280×720·1.1MB 페인터리 밤 요새(본성·오두막·깃발·창광)라 톤이 따로 놀고,
건물 스프라이트와 이중으로 겹친다. 같은 웜톤 팔레트·두꺼운 외곽선의 빈 마당
판으로 교체한다(건물/깃발/창광 없음). props·STATUS·W3Party·FX 미변경.

화면 배경이라 256 프랍 파이프라인 강제 아님 — 해상도 1280×720 유지.
.cs/.meta/GUID 무변경.

디자인: 위 하늘 띠 → 먼 언덕 → 흙 마당+풀 패치+흙길. §6-A: 바닥 큰 원/고리
/글로우 금지. 할로우 금지.

사용: python3 gen_oxalpha_bg_estate.py
출력: art/out_oxalpha_bg_estate.png (1280×720 RGB)
"""
from pathlib import Path
import random

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
W, H = 1280, 720

OUTLINE = (40, 24, 20)
SKY_TOP = (92, 118, 148)
SKY_MID = (156, 148, 132)
SKY_HOR = (196, 168, 118)
HILL_DK = (78, 96, 58)
HILL = (102, 122, 68)
GRASS = (110, 132, 72)
GRASS_LT = (148, 168, 90)
DIRT = (150, 118, 78)
DIRT_LT = (176, 140, 96)
DIRT_DK = (108, 82, 52)
STONE = (132, 128, 122)
STONE_DK = (86, 82, 76)
WOOD = (150, 104, 58)


def _lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def make():
    im = Image.new("RGB", (W, H), SKY_TOP)
    d = ImageDraw.Draw(im)
    rng = random.Random(20260827)

    # 하늘 가로 띠
    horizon = 310
    for y in range(horizon):
        if y < 160:
            c = _lerp(SKY_TOP, SKY_MID, y / 160)
        else:
            c = _lerp(SKY_MID, SKY_HOR, (y - 160) / (horizon - 160))
        d.line([(0, y), (W, y)], fill=c)

    # 먼 언덕 두 겹
    d.polygon([(0, 340), (180, 250), (420, 300), (680, 240), (980, 290), (1280, 255), (1280, 380), (0, 380)], fill=HILL_DK)
    d.polygon([(0, 370), (220, 310), (510, 350), (790, 300), (1100, 340), (1280, 315), (1280, 420), (0, 420)], fill=HILL)

    # 언덕 위 작은 나무 실루엣(멀리, 건물 아님)
    for x, y, s in ((160, 318, 14), (490, 328, 12), (860, 312, 16), (1140, 328, 11)):
        d.ellipse([x - s, y - s - 8, x + s, y + 4], fill=HILL_DK)
        d.rectangle([x - 3, y, x + 3, y + 18], fill=(96, 64, 34))

    # 마당
    d.rectangle([0, 400, W, H], fill=DIRT)
    # 풀 패치
    for _ in range(48):
        x = rng.randint(20, W - 40)
        y = rng.randint(420, H - 30)
        w = rng.randint(28, 90)
        h = rng.randint(10, 22)
        d.ellipse([x, y, x + w, y + h], fill=GRASS if rng.random() < 0.6 else GRASS_LT)

    # 흙길 (아래 중앙 → 중경, 원/고리 아님)
    path = [(520, H), (620, 470), (760, 430), (820, 410), (900, 400)]
    d.line(path, fill=DIRT_DK, width=78)
    d.line(path, fill=DIRT_LT, width=52)

    # 먼 낮은 돌담 (건물 아님, 수평)
    d.rectangle([40, 388, 1240, 408], fill=OUTLINE)
    d.rectangle([44, 392, 1236, 404], fill=STONE)
    for x in range(60, 1230, 48):
        d.line([(x, 392), (x, 404)], fill=STONE_DK, width=2)

    # 앞쪽 흙 결
    for _ in range(90):
        x = rng.randint(0, W)
        y = rng.randint(500, H - 8)
        d.line([(x, y), (x + rng.randint(8, 28), y + rng.randint(-2, 2))], fill=DIRT_DK, width=1)

    out = HERE / "out_oxalpha_bg_estate.png"
    im.save(out, optimize=True)
    print(f"→ {out.name}  {W}×{H}  {im.mode}  {out.stat().st_size}B")


if __name__ == "__main__":
    make()
