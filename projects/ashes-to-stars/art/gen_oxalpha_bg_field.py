#!/usr/bin/env python3
"""ox-alpha 필드 배경(bg_field) 코드합성 — 1280×720 풀밭+하늘.

배경: 필드 나무·바위·덤불·그루터기 프랍은 ox-alpha 256인데 bg_field 는 아직
나노바나나 1280×720·~1.0MB 페인터리 밤 들판(바위·나무·울타리 그려넣음)이라
톤이 따로 놀고, 256 프랍과 이중으로 겹친다. 같은 웜톤 팔레트·두꺼운 외곽선의
빈 풀밭 판으로 교체한다(바위/나무/울타리 없음). props·STATUS·W3Party·FX
·bg_estate·bg_title 미변경.

화면 배경이라 256 프랍 파이프라인 강제 아님 — 해상도 1280×720 유지.
.cs/.meta/GUID 무변경.

디자인: 위 하늘 띠 → 먼 언덕 → 풀밭+성긴 풀 패치+우하단→지평 흙길 띠.
§6-A: 바닥 큰 원/고리/글로우 금지. 할로우 금지.

사용: python3 gen_oxalpha_bg_field.py
출력: art/out_oxalpha_bg_field.png (1280×720 RGB)
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
WOOD = (150, 104, 58)


def _lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def make():
    im = Image.new("RGB", (W, H), SKY_TOP)
    d = ImageDraw.Draw(im)
    rng = random.Random(20260827)

    # 하늘 가로 띠 (웜 더스크, 콜드 나이트 아님)
    horizon = 310
    for y in range(horizon):
        if y < 160:
            c = _lerp(SKY_TOP, SKY_MID, y / 160)
        else:
            c = _lerp(SKY_MID, SKY_HOR, (y - 160) / (horizon - 160))
        d.line([(0, y), (W, y)], fill=c)

    # 먼 언덕 두 겹 (들판 기복)
    d.polygon(
        [
            (0, 350),
            (140, 255),
            (340, 305),
            (560, 235),
            (820, 285),
            (1040, 245),
            (1280, 270),
            (1280, 400),
            (0, 400),
        ],
        fill=HILL_DK,
    )
    d.polygon(
        [
            (0, 385),
            (200, 320),
            (460, 360),
            (720, 305),
            (980, 345),
            (1180, 318),
            (1280, 330),
            (1280, 430),
            (0, 430),
        ],
        fill=HILL,
    )

    # 언덕 위 작은 나무 실루엣(멀리, 중경 프랍 아님)
    for x, y, s in ((150, 328, 13), (430, 338, 11), (790, 318, 15), (1080, 332, 12)):
        d.ellipse([x - s, y - s - 8, x + s, y + 4], fill=HILL_DK)
        d.rectangle([x - 3, y, x + 3, y + 16], fill=(96, 64, 34))

    # 풀밭 (흙 마당 아님 — 필드 판)
    d.rectangle([0, 400, W, H], fill=GRASS)
    # 앞쪽 살짝 어두운 기복 (원/고리 아님)
    d.polygon(
        [
            (0, 510),
            (280, 470),
            (560, 500),
            (860, 455),
            (1280, 490),
            (1280, 560),
            (0, 545),
        ],
        fill=HILL,
    )
    d.polygon(
        [
            (0, 620),
            (220, 590),
            (540, 630),
            (880, 575),
            (1280, 610),
            (1280, H),
            (0, H),
        ],
        fill=GRASS,
    )

    # 성긴 풀 패치 (원/고리 아님, 타원 얼룩)
    for _ in range(56):
        x = rng.randint(16, W - 50)
        y = rng.randint(420, H - 24)
        w = rng.randint(30, 100)
        h = rng.randint(10, 22)
        d.ellipse([x, y, x + w, y + h], fill=GRASS_LT if rng.random() < 0.55 else HILL)

    # 흙길 띠 — 우하단 → 중앙 지평 (원/고리 아님, 밴드)
    path = [(1240, H), (1120, 590), (980, 510), (840, 450), (730, 415), (660, 400)]
    d.line(path, fill=DIRT_DK, width=88)
    d.line(path, fill=DIRT, width=64)
    d.line(path, fill=DIRT_LT, width=42)

    # 길 위 흙 결
    for _ in range(40):
        t = rng.random()
        # 경로 대략 보간
        xs = [p[0] for p in path]
        ys = [p[1] for p in path]
        n = len(path) - 1
        i = min(int(t * n), n - 1)
        lt = (t * n) - i
        x = int(xs[i] + (xs[i + 1] - xs[i]) * lt) + rng.randint(-18, 18)
        y = int(ys[i] + (ys[i + 1] - ys[i]) * lt) + rng.randint(-8, 8)
        d.line(
            [(x, y), (x + rng.randint(6, 18), y + rng.randint(-2, 2))],
            fill=DIRT_DK,
            width=1,
        )

    # 앞쪽 풀 결 (세로 짧은 획, 프랍 아님)
    for _ in range(110):
        x = rng.randint(0, W)
        y = rng.randint(520, H - 6)
        d.line(
            [(x, y), (x + rng.randint(-2, 2), y - rng.randint(4, 10))],
            fill=GRASS_LT if rng.random() < 0.5 else HILL_DK,
            width=1,
        )

    out = HERE / "out_oxalpha_bg_field.png"
    im.save(out, optimize=True)
    print(f"→ {out.name}  {W}×{H}  {im.mode}  {out.stat().st_size}B")
    return im, out


if __name__ == "__main__":
    make()
