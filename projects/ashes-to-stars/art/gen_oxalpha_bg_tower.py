#!/usr/bin/env python3
"""ox-alpha 탑 배경(bg_tower) 코드합성 — 1280×720 하늘+언덕+큐보이드 킵.

배경: 탑 화면은 UI 판(타이틀·본문이 왼쪽을 덮음)이라 필드/던전 프랍 오버레이가
없는데 bg_tower 는 아직 나노바나나 1280×720·~0.9MB 페인터리 밤 성채(감싸는 계단·
아치 창광·폭풍 하늘)라 톤이 따로 논다. 같은 웜톤 팔레트·두꺼운 외곽선의 기하 판으로
교체한다. 왼쪽은 UI라 하늘+먼 언덕만. 관심은 오른쪽 3–4단 큐보이드 석조 킵+옆면
지그재그 계단 띠. props·STATUS·W3Party·FX·bg_estate·bg_title·bg_field·bg_dungeon
·bg_result·bg_character·bg_party 미변경.

화면 배경이라 256 프랍 파이프라인 강제 아님 — 해상도 1280×720 RGB 유지.
.cs/.meta/GUID 무변경.

디자인: 위 하늘 띠 → 먼 언덕 → 오른쪽 적층 큐보이드 킵(층마다 한 단 작아짐)
+옆면 납작 지그재그 계단 띠 + 기단 흙/풀. 문 하나(어두운 직사각, 글로우 없음).
선택 창은 STONE_DK 납작 직사각(빛 아님). 타이틀 킵과 구별: 더 높고 여러 단+계단
(오를 수 있는 탑), 폐허 계단·마른 나무 없음.
§6-A: 바닥 큰 원/고리/글로우 금지. 할로우 금지. 고딕 첨두아치·창광·랜턴 없음.

사용: python3 gen_oxalpha_bg_tower.py
출력: art/out_oxalpha_bg_tower.png (1280×720 RGB)
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
STONE_LT = (158, 154, 148)


def _lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def _box(d, x0, y0, x1, y1, fill, ow=6):
    d.rectangle([x0 - ow, y0 - ow, x1 + ow, y1 + ow], fill=OUTLINE)
    d.rectangle([x0, y0, x1, y1], fill=fill)


def _cuboid(d, x0, y0, w, h, kd, front, side, ow=6):
    """정면+오른쪽 면 큐보이드 (타이틀 킵·파티 가구와 같은 문법)."""
    dy = max(10, kd // 3)
    sp = [
        (x0 + w, y0),
        (x0 + w + kd, y0 + dy),
        (x0 + w + kd, y0 + h + dy),
        (x0 + w, y0 + h),
    ]
    d.polygon(
        [
            (sp[0][0] - ow, sp[0][1] - ow),
            (sp[1][0] + ow, sp[1][1] - ow),
            (sp[2][0] + ow, sp[2][1] + ow),
            (sp[3][0] - ow, sp[3][1] + ow),
        ],
        fill=OUTLINE,
    )
    d.polygon(sp, fill=side)
    _box(d, x0, y0, x0 + w, y0 + h, front, ow=ow)


def _courses(d, x0, y0, w, h, step=28):
    for y in range(y0 + 22, y0 + h - 12, step):
        d.line([(x0 + 8, y), (x0 + w - 8, y)], fill=STONE_DK, width=3)
    for x in range(x0 + 36, x0 + w - 8, 44):
        d.line([(x, y0 + 8), (x, y0 + h - 8)], fill=STONE_DK, width=2)


def _window(d, x, y, w=11, h=15):
    """납작 STONE_DK 직사각 — 빛·글로우 아님."""
    _box(d, x, y, x + w, y + h, STONE_DK, ow=3)


def _stair_flight(d, x, y, n, sw, sh, dx, dy):
    """납작 계단 띠 (조각 고딕 아님, 직사각 밴드)."""
    for i in range(n):
        x0 = int(x + i * dx)
        y0 = int(y + i * dy)
        _box(d, x0, y0, x0 + sw, y0 + sh, STONE, ow=3)
        d.line(
            [(x0 + 4, y0 + sh // 2), (x0 + sw - 4, y0 + sh // 2)],
            fill=STONE_DK,
            width=2,
        )


def make():
    im = Image.new("RGB", (W, H), SKY_TOP)
    d = ImageDraw.Draw(im)
    rng = random.Random(20260827)

    # 하늘 가로 띠 (웜 더스크, 콜드 나이트 아님)
    horizon = 300
    mid_y = 150
    for y in range(horizon):
        if y < mid_y:
            c = _lerp(SKY_TOP, SKY_MID, y / mid_y)
        else:
            c = _lerp(SKY_MID, SKY_HOR, (y - mid_y) / (horizon - mid_y))
        d.line([(0, y), (W, y)], fill=c)

    # 1px 납작 별점 (블룸·반딧불 아님) — 왼쪽 UI 하늘만
    star = (210, 198, 176)
    for _ in range(48):
        x = rng.randint(20, 680)
        y = rng.randint(12, 130)
        d.point((x, y), fill=star)

    # 먼 언덕 두 겹 — 왼쪽은 완만(UI 자리), 기복은 중우
    d.polygon(
        [
            (0, 340), (160, 248), (380, 300), (600, 236),
            (820, 278), (1040, 242), (1280, 268),
            (1280, 400), (0, 400),
        ],
        fill=HILL_DK,
    )
    d.polygon(
        [
            (0, 375), (200, 312), (440, 352), (680, 298),
            (920, 338), (1140, 312), (1280, 328),
            (1280, 430), (0, 430),
        ],
        fill=HILL,
    )

    # 언덕 위 작은 나무 실루엣 — 왼쪽만(UI 뒤, 중경 프랍 아님)
    for x, y, s in ((130, 330, 12), (360, 344, 10), (540, 318, 13)):
        d.ellipse([x - s, y - s - 8, x + s, y + 4], fill=HILL_DK)
        d.rectangle([x - 3, y, x + 3, y + 16], fill=(96, 64, 34))

    # 흙 마당 (지평 아래)
    d.rectangle([0, 400, W, H], fill=DIRT)
    # 탑 기슭이 조금 높아지는 전경 비탈 (원/고리 아님)
    d.polygon(
        [
            (680, H), (820, 560), (980, 500), (1160, 478),
            (1280, 490), (1280, H),
        ],
        fill=DIRT_DK,
    )
    d.polygon(
        [
            (740, H), (880, 590), (1040, 528), (1200, 512),
            (1280, 522), (1280, H),
        ],
        fill=DIRT,
    )

    # 풀 패치 (성긴, 원/고리 아님) — 왼쪽 더 성기게
    for _ in range(52):
        x = rng.randint(16, W - 70)
        y = rng.randint(420, H - 28)
        if x < 700 and rng.random() < 0.42:
            continue
        ww = rng.randint(28, 96)
        hh = rng.randint(10, 22)
        d.ellipse(
            [x, y, x + ww, y + hh],
            fill=GRASS if rng.random() < 0.6 else GRASS_LT,
        )

    # 흙길 띠 (아래 중 → 탑 기단, 원/고리 아님)
    path = [(620, H), (700, 620), (780, 560), (840, 522)]
    d.line(path, fill=DIRT_DK, width=72)
    d.line(path, fill=DIRT_LT, width=48)

    # 앞쪽 흙 결
    for _ in range(110):
        x = rng.randint(0, W)
        y = rng.randint(520, H - 8)
        d.line(
            [(x, y), (x + rng.randint(8, 32), y + rng.randint(-3, 3))],
            fill=DIRT_DK,
            width=1,
        )

    # --- 기단 ---
    _box(d, 728, 588, 1228, 628, STONE_DK, ow=6)
    _box(d, 740, 598, 1212, 622, STONE, ow=0)
    for x in range(772, 1200, 48):
        d.line([(x, 600), (x, 620)], fill=STONE_DK, width=2)

    # --- 적층 큐보이드 킵 (아래→위, 단마다 한 칸 작아짐) ---
    # 오른쪽 치우침: 왼쪽 ~58%는 UI 하늘. (x0, y0, w, h, depth)
    floors = [
        (760, 438, 392, 158, 58),  # 1F 기단
        (808, 296, 312, 150, 50),  # 2F
        (848, 166, 240, 138, 42),  # 3F
        (884,  48, 176, 126, 36),  # 4F 꼭대기
    ]

    for i, (x0, y0, w, h, kd) in enumerate(floors):
        ow = 7 if i == 0 else 6
        _cuboid(d, x0, y0, w, h, kd, STONE, STONE_DK, ow=ow)
        _courses(d, x0, y0, w, h, step=26 if i == 0 else 24)
        # 지붕/난간 띠 (윗층이 앉는 선반)
        _box(d, x0 - 8, y0 - 12, x0 + w + 8, y0 + 8, STONE_DK, ow=5)
        d.line([(x0, y0 - 4), (x0 + w, y0 - 4)], fill=STONE_LT, width=2)

    # 꼭대기 총안 (작은 큐브) + 오른쪽 면 한 칸
    tx, ty, tw = 884, 48, 176
    for i in range(5):
        cx0 = tx + 10 + i * 32
        _box(d, cx0, ty - 28, cx0 + 18, ty - 6, STONE, ow=4)
    _box(d, tx + tw, ty - 22, tx + tw + 24, ty + 6, STONE_DK, ow=4)

    # 납작 창 (STONE_DK, 빛 없음) — 층마다 2
    for (x, y) in (
        (828, 468), (1008, 468),
        (860, 336), (1008, 336),
        (888, 206), (1000, 206),
        (920,  88), (984,  88),
    ):
        _window(d, x, y)

    # 문 하나 (1F, 어두운 직사각, 아치·글로우 없음)
    dx, dy = 800, 516
    _box(d, dx - 8, dy - 10, dx + 46, dy + 4, STONE, ow=4)
    _box(d, dx, dy, dx + 38, dy + 78, STONE_DK, ow=5)
    d.line([(dx + 8, dy + 12), (dx + 8, dy + 70)], fill=OUTLINE, width=2)
    d.rectangle([dx + 26, dy + 40, dx + 32, dy + 48], fill=OUTLINE)

    # --- 옆면 지그재그 계단 띠 (킵 왼쪽 면에 밀착, x>=640) ---
    # 기단 → 1F 선반
    _stair_flight(d, 708, 568, 6, 56, 14, -6, -20)
    _box(d, 672, 448, 744, 470, STONE, ow=4)
    # 1F → 2F
    _stair_flight(d, 680, 432, 6, 54, 14, 8, -20)
    _box(d, 724, 312, 808, 334, STONE, ow=4)
    # 2F → 3F
    _stair_flight(d, 692, 296, 5, 50, 13, -6, -22)
    _box(d, 662, 186, 732, 206, STONE, ow=4)
    # 3F → 4F
    _stair_flight(d, 668, 170, 5, 48, 12, 8, -20)
    _box(d, 704, 70, 780, 90, STONE, ow=4)
    # 꼭대기 짧은 접속 띠
    _box(d, 776, 52, 884, 70, STONE, ow=4)

    out = HERE / "out_oxalpha_bg_tower.png"
    im.save(out, optimize=True)
    print(f"→ {out.name}  {W}×{H}  {im.mode}  {out.stat().st_size}B")
    return im, out


if __name__ == "__main__":
    make()
