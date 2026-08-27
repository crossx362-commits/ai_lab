#!/usr/bin/env python3
"""ox-alpha 던전 배경(bg_dungeon) 코드합성 — 2752×1536 석조 홀 빈 판.

배경: 던전 잔해·수정·엄폐·벽·기둥 프랍은 ox-alpha 256인데 bg_dungeon 은 아직
나노바나나 2752×1536·~5.0MB 페인터리 카타콤(오른쪽 3아치 콜로네이드·횃불 청광
·이끼·왼쪽 안개 공허)이라 톤이 따로 놀고, 256 프랍과 이중으로 겹친다. 같은
차가운 석조 팔레트·두꺼운 외곽선의 빈 홀 판으로 교체한다(기둥/아치/횃불/수정
/잔해/엄폐벽 없음). props·STATUS·W3Party·FX·bg_estate·bg_title·bg_field 미변경.

화면 배경이라 256 프랍 파이프라인 강제 아님 — 해상도 2752×1536 RGBA 유지.
.cs/.meta/GUID 무변경.

디자인: 위 천장 띠 → 강철청 안개(FLAT) → 먼 수평 석조 띠(+직사각 입구 하나)
→ 금 간 석판 바닥. 왼쪽은 더 어둡고 열린 공허(플레이·프랍 스폰).
§6-A: 바닥 큰 원/고리/글로우 금지. 할로우 눈 금지. 횃불·금빛 하늘·WOOD 풀 없음.

사용: python3 gen_oxalpha_bg_dungeon.py
출력: art/out_oxalpha_bg_dungeon.png (2752×1536 RGBA)
"""
from pathlib import Path
import random

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
W, H = 2752, 1536

OUTLINE = (40, 24, 20)
STONE = (132, 128, 122)
STONE_DK = (86, 82, 76)
STONE_LT = (158, 154, 148)
FLOOR = (118, 112, 104)
FLOOR_LT = (140, 134, 124)
FLOOR_DK = (78, 74, 68)
WALL = (96, 94, 90)
WALL_DK = (62, 60, 58)
CEILING = (48, 50, 56)
AIR = (72, 78, 88)


def _lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def make():
    im = Image.new("RGB", (W, H), CEILING)
    d = ImageDraw.Draw(im)
    rng = random.Random(20260827)

    ceiling_h = 220
    wall_top = 740
    wall_bot = 834
    wall_x0 = 620  # 왼쪽은 열린 공허 — 석조 띠는 중우만
    void_x = 720

    # 천장 판 (어두운 석조, 하늘 아님)
    d.rectangle([0, 0, W, ceiling_h], fill=CEILING)
    for y in (56, 118, 176):
        d.line([(0, y), (W, y)], fill=WALL_DK, width=2)
    for x in range(0, W, 96):
        d.line([(x, 0), (x, ceiling_h)], fill=WALL_DK, width=1)
    # 천장 하단 실루엣
    d.rectangle([0, ceiling_h - 10, W, ceiling_h], fill=OUTLINE)

    # 홀 깊이 — 가로 띠 (웜 더스크/금빛 하늘 아님, FLAT 강철청)
    for y in range(ceiling_h, wall_top):
        t = (y - ceiling_h) / max(1, wall_top - ceiling_h)
        if t < 0.55:
            c = _lerp(CEILING, AIR, t / 0.55)
        else:
            c = _lerp(AIR, WALL_DK, (t - 0.55) / 0.45)
        d.line([(0, y), (W, y)], fill=c)

    # 왼쪽 공허 (FLAT 어두운 판, 라디얼 글로우 아님)
    d.polygon(
        [
            (0, ceiling_h),
            (void_x, ceiling_h + 12),
            (void_x - 160, wall_top),
            (0, wall_top),
        ],
        fill=CEILING,
    )

    # 먼 수평 석조 띠 (영지 낮은 돌담과 같은 문법, 건물/기둥 아님)
    ow = 8
    d.rectangle([wall_x0 - ow, wall_top - ow, W, wall_bot + 6], fill=OUTLINE)
    d.rectangle([wall_x0, wall_top, W, wall_bot], fill=WALL)
    d.rectangle([wall_x0, wall_top, W, wall_top + 8], fill=STONE_LT)
    d.line([(wall_x0, wall_top + 32), (W, wall_top + 32)], fill=WALL_DK, width=2)
    d.line([(wall_x0, wall_top + 62), (W, wall_top + 62)], fill=WALL_DK, width=2)
    for x in range(wall_x0 + 28, W, 52):
        d.line([(x, wall_top), (x, wall_bot)], fill=WALL_DK, width=2)

    # 직사각 입구 하나 (오른쪽, 3아치/부수아르/횃불 아님)
    ox0, oy0, ox1, oy1 = 1848, wall_top + 10, 2088, wall_bot - 4
    d.rectangle([ox0 - 6, oy0 - 6, ox1 + 6, oy1 + 6], fill=OUTLINE)
    d.rectangle([ox0, oy0, ox1, oy1], fill=CEILING)

    # 바닥 석판 (필드 흙길처럼 그라운드 플레이트 — 프랍 아님, 띠/타일, 원/고리 아님)
    d.rectangle([0, wall_bot, W, H], fill=FLOOR)
    # 왼쪽 바닥은 더 어둡게 (공허·플레이 공간)
    d.polygon(
        [
            (0, wall_bot),
            (void_x - 40, wall_bot),
            (void_x - 280, H),
            (0, H),
        ],
        fill=FLOOR_DK,
    )

    y = wall_bot
    row_h = 20
    row = 0
    while y < H:
        h = row_h
        stagger = (row % 2) * (36 + row * 5)
        col_w = 64 + row * 10
        x = -stagger
        while x < W:
            cx = x + col_w // 2
            if cx < void_x - 80:
                fill = FLOOR_DK if rng.random() < 0.7 else FLOOR
            elif rng.random() < 0.20:
                fill = FLOOR_LT if rng.random() < 0.55 else FLOOR_DK
            else:
                fill = None
            if fill is not None:
                d.rectangle([x + 1, y + 1, x + col_w - 2, y + h - 2], fill=fill)
            d.line([(x, y), (x, min(y + h, H))], fill=FLOOR_DK, width=2)
            x += col_w
        d.line([(0, y), (W, y)], fill=FLOOR_DK, width=3)
        y += h
        row_h = min(int(row_h * 1.16) + 2, 108)
        row += 1

    # 금 간 석판 균열 (짧은 꺾인 선, 원/고리 아님)
    for _ in range(64):
        x = rng.randint(80, W - 80)
        yy = rng.randint(wall_bot + 36, H - 24)
        x2 = x + rng.randint(-48, 48)
        y2 = yy + rng.randint(10, 40)
        d.line([(x, yy), (x2, y2)], fill=FLOOR_DK, width=2)
        if rng.random() < 0.45:
            d.line(
                [(x2, y2), (x2 + rng.randint(-28, 28), y2 + rng.randint(6, 22))],
                fill=OUTLINE,
                width=1,
            )

    # 앞쪽 석판 결
    for _ in range(110):
        x = rng.randint(0, W)
        yy = rng.randint(1120, H - 8)
        d.line(
            [(x, yy), (x + rng.randint(8, 30), yy + rng.randint(-2, 2))],
            fill=FLOOR_DK,
            width=1,
        )

    im = im.convert("RGBA")
    out = HERE / "out_oxalpha_bg_dungeon.png"
    im.save(out, optimize=True)
    print(f"→ {out.name}  {W}×{H}  {im.mode}  {out.stat().st_size}B")
    return im, out


if __name__ == "__main__":
    make()
