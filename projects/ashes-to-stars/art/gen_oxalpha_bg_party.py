#!/usr/bin/env python3
"""ox-alpha 파티 배경(bg_party) 코드합성 — 1280×720 석조 작전실.

배경: 파티 편성 화면은 UI 판(로스터 카드가 가운데를 덮음)이라 필드/던전 프랍 오버레이가
없는데 bg_party 는 아직 나노바나나 1280×720·~1.0MB 페인터리 작전실(지도 탁자·토큰·
매달린 랜턴 글로우·문양 깃발·바닥 안개)이라 톤이 따로 논다. 같은 석조+웜 목재
팔레트·두꺼운 외곽선의 기하 판으로 교체한다. 가운데·중상은 UI라 빈 석판만.
가구는 중하 빈 탁자 + 가장자리 의자(왼/뒤/오른). props·STATUS·W3Party·FX
·bg_estate·bg_title·bg_field·bg_dungeon·bg_result·bg_character 미변경.

화면 배경이라 256 프랍 파이프라인 강제 아님 — 해상도 1280×720 RGB 유지.
.cs/.meta/GUID 무변경.

디자인: 위 천장 띠 → 석조 홀 깊이(FLAT) → 먼 수평 석조 띠 → 금 간 석판 바닥.
중하 WOOD 탁자 직사각(윗면 빔 — 지도·토큰 없음). 의자 큐보이드 3(왼·뒤·오른 가장자리).
랜턴·불·글로우·깃발 문양·안개·사람·원/고리 없음.
§6-A: 바닥 큰 원/고리/글로우 금지. 할로우 금지.

사용: python3 gen_oxalpha_bg_party.py
출력: art/out_oxalpha_bg_party.png (1280×720 RGB)
"""
from pathlib import Path
import random

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
W, H = 1280, 720

OUTLINE = (40, 24, 20)
STONE = (132, 128, 122)
STONE_DK = (86, 82, 76)
STONE_LT = (158, 154, 148)
FLOOR = (118, 112, 104)
FLOOR_LT = (140, 134, 124)
FLOOR_DK = (78, 74, 68)
WALL = (96, 94, 90)
WALL_DK = (62, 60, 58)
CEILING = (52, 46, 42)
AIR = (78, 72, 66)
WOOD = (150, 104, 58)
WOOD_DK = (96, 64, 34)
WOOD_LT = (176, 140, 96)
DIRT_DK = (108, 82, 52)
GOLD = (216, 172, 96)


def _lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def _box(d, x0, y0, x1, y1, fill, ow=5):
    d.rectangle([x0 - ow, y0 - ow, x1 + ow, y1 + ow], fill=OUTLINE)
    d.rectangle([x0, y0, x1, y1], fill=fill)


def _cuboid(d, x0, y0, w, h, kd, front, side, ow=5):
    """정면+오른쪽 면 큐보이드 (결과 말뚝·영지 킵과 같은 문법)."""
    sp = [
        (x0 + w, y0),
        (x0 + w + kd, y0 + 10),
        (x0 + w + kd, y0 + h + 10),
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


def make():
    im = Image.new("RGB", (W, H), CEILING)
    d = ImageDraw.Draw(im)
    rng = random.Random(202608271)

    ceiling_h = 96
    wall_top = 332
    wall_bot = 392

    # 천장 판 (어두운 석조, 하늘 아님)
    d.rectangle([0, 0, W, ceiling_h], fill=CEILING)
    for y in (28, 56, 80):
        d.line([(0, y), (W, y)], fill=WALL_DK, width=2)
    for x in range(0, W, 80):
        d.line([(x, 0), (x, ceiling_h)], fill=WALL_DK, width=1)
    d.rectangle([0, ceiling_h - 8, W, ceiling_h], fill=OUTLINE)

    # 홀 깊이 — 가로 띠 (웜 더스크/금빛 하늘 아님, FLAT 석조)
    for y in range(ceiling_h, wall_top):
        t = (y - ceiling_h) / max(1, wall_top - ceiling_h)
        if t < 0.55:
            c = _lerp(CEILING, AIR, t / 0.55)
        else:
            c = _lerp(AIR, WALL_DK, (t - 0.55) / 0.45)
        d.line([(0, y), (W, y)], fill=c)

    # 먼 수평 석조 띠 (던전 홀과 같은 문법, 전폭 — 작전실은 닫힌 실내)
    ow = 6
    d.rectangle([0, wall_top - ow, W, wall_bot + 4], fill=OUTLINE)
    d.rectangle([0, wall_top, W, wall_bot], fill=WALL)
    d.rectangle([0, wall_top, W, wall_top + 6], fill=STONE_LT)
    d.line([(0, wall_top + 22), (W, wall_top + 22)], fill=WALL_DK, width=2)
    d.line([(0, wall_top + 42), (W, wall_top + 42)], fill=WALL_DK, width=2)
    for x in range(24, W, 44):
        d.line([(x, wall_top), (x, wall_bot)], fill=WALL_DK, width=2)

    # 바닥 석판 (중상·가운데는 창/가구 없이 빈 석조 — 파티 UI)
    d.rectangle([0, wall_bot, W, H], fill=FLOOR)

    y = wall_bot
    row_h = 16
    row = 0
    while y < H:
        h = row_h
        stagger = (row % 2) * (28 + row * 4)
        col_w = 52 + row * 8
        x = -stagger
        while x < W:
            if rng.random() < 0.22:
                fill = FLOOR_LT if rng.random() < 0.55 else FLOOR_DK
                d.rectangle([x + 1, y + 1, x + col_w - 2, y + h - 2], fill=fill)
            d.line([(x, y), (x, min(y + h, H))], fill=FLOOR_DK, width=2)
            x += col_w
        d.line([(0, y), (W, y)], fill=FLOOR_DK, width=3)
        y += h
        row_h = min(int(row_h * 1.16) + 2, 72)
        row += 1

    # 금 간 석판 균열 (짧은 꺾인 선, 원/고리 아님)
    for _ in range(36):
        x = rng.randint(40, W - 40)
        yy = rng.randint(wall_bot + 24, H - 16)
        x2 = x + rng.randint(-36, 36)
        y2 = yy + rng.randint(8, 28)
        d.line([(x, yy), (x2, y2)], fill=FLOOR_DK, width=2)
        if rng.random() < 0.45:
            d.line(
                [(x2, y2), (x2 + rng.randint(-20, 20), y2 + rng.randint(4, 16))],
                fill=OUTLINE,
                width=1,
            )

    # 앞쪽 석판 결
    for _ in range(70):
        x = rng.randint(0, W)
        yy = rng.randint(540, H - 8)
        d.line(
            [(x, yy), (x + rng.randint(6, 22), yy + rng.randint(-2, 2))],
            fill=FLOOR_DK,
            width=1,
        )

    # --- 뒤 의자 (탁자 뒤·살짝 왼쪽, 가운데 UI 자리 아님) ---
    _cuboid(d, 488, 456, 54, 20, 14, WOOD, WOOD_DK, ow=4)
    _box(d, 496, 404, 534, 458, WOOD, ow=4)
    d.line([(504, 412), (504, 450)], fill=WOOD_LT, width=2)
    d.line([(526, 414), (526, 448)], fill=WOOD_DK, width=2)
    d.line([(500, 428), (530, 428)], fill=WOOD_DK, width=2)
    # 납작 금 리벳
    d.rectangle([512, 420, 520, 428], fill=OUTLINE)
    d.rectangle([514, 422, 518, 426], fill=GOLD)

    # --- 중하 탁자 (직사각, 윗면 빔 — 지도·토큰·문양 없음) ---
    # 다리 (가장자리만)
    _box(d, 356, 556, 380, 612, WOOD_DK, ow=4)
    _box(d, 900, 556, 924, 612, WOOD_DK, ow=4)
    _box(d, 356, 604, 380, 616, DIRT_DK, ow=0)
    _box(d, 900, 604, 924, 616, DIRT_DK, ow=0)
    # 상판
    _box(d, 340, 508, 940, 564, WOOD, ow=5)
    d.rectangle([340, 548, 940, 564], fill=WOOD_DK)
    d.line([(352, 520), (928, 520)], fill=WOOD_LT, width=2)
    d.line([(352, 536), (928, 536)], fill=WOOD_DK, width=2)
    # 모서리 납작 금 장석 (블룸 없음)
    d.rectangle([352, 516, 366, 528], fill=OUTLINE)
    d.rectangle([354, 518, 364, 526], fill=GOLD)
    d.rectangle([914, 516, 928, 528], fill=OUTLINE)
    d.rectangle([916, 518, 926, 526], fill=GOLD)

    # --- 왼 의자 (탁자 왼 가장자리) ---
    _cuboid(d, 248, 528, 56, 22, 14, WOOD, WOOD_DK, ow=4)
    _box(d, 244, 468, 264, 550, WOOD, ow=4)
    d.line([(250, 476), (250, 542)], fill=WOOD_LT, width=2)
    d.line([(258, 480), (258, 538)], fill=WOOD_DK, width=2)
    d.line([(248, 500), (260, 500)], fill=WOOD_DK, width=2)
    d.rectangle([250, 488, 258, 496], fill=OUTLINE)
    d.rectangle([252, 490, 256, 494], fill=GOLD)

    # --- 오른 의자 (탁자 오른 가장자리) ---
    _cuboid(d, 968, 528, 56, 22, 14, WOOD, WOOD_DK, ow=4)
    _box(d, 1012, 468, 1032, 550, WOOD, ow=4)
    d.line([(1018, 476), (1018, 542)], fill=WOOD_LT, width=2)
    d.line([(1026, 480), (1026, 538)], fill=WOOD_DK, width=2)
    d.line([(1016, 500), (1028, 500)], fill=WOOD_DK, width=2)
    d.rectangle([1018, 488, 1026, 496], fill=OUTLINE)
    d.rectangle([1020, 490, 1024, 494], fill=GOLD)

    out = HERE / "out_oxalpha_bg_party.png"
    im.save(out, optimize=True)
    print(f"→ {out.name}  {W}×{H}  {im.mode}  {out.stat().st_size}B")
    return im, out


if __name__ == "__main__":
    make()
