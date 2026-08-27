#!/usr/bin/env python3
"""ox-alpha 캐릭터 배경(bg_character) 코드합성 — 1280×720 석조 병영 홀.

배경: 캐릭터 화면은 UI 판(왼쪽 바둑판·오른쪽 초상)이라 필드/던전 프랍 오버레이가
없는데 bg_character 는 아직 나노바나나 1280×720·~1.1MB 페인터리 병영(침낭 셋·궤짝
·철 화로 주황 글로우·창/방패 랙·해진 깃발)이라 톤이 따로 논다. 같은 석조+웜 목재
팔레트·두꺼운 외곽선의 기하 판으로 교체한다. 가운데는 UI라 빈 석판만. 가구는
가장자리(왼 침낭·궤짝, 오른 나무 랙 실루엣). props·STATUS·W3Party·FX
·bg_estate·bg_title·bg_field·bg_dungeon·bg_result 미변경.

화면 배경이라 256 프랍 파이프라인 강제 아님 — 해상도 1280×720 RGB 유지.
.cs/.meta/GUID 무변경.

디자인: 위 천장 띠 → 석조 홀 깊이(FLAT, 바깥 더스크 하늘 아님) → 먼 수평 석조 띠
(+납작 어두운 창 하나) → 금 간 석판 바닥. 왼뒤 WOOD 침낭 직사각+궤짝 큐보이드,
오른뒤 WOOD 세로 막대 랙(창/방패 없음). 화로·불·잉걸불·연기·돌반지·깃발 문양
·사람 없음.
§6-A: 바닥 큰 원/고리/글로우 금지. 할로우 금지.

사용: python3 gen_oxalpha_bg_character.py
출력: art/out_oxalpha_bg_character.png (1280×720 RGB)
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
    rng = random.Random(20260827)

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

    # 먼 수평 석조 띠 (던전 홀과 같은 문법, 전폭 — 병영은 닫힌 실내)
    ow = 6
    d.rectangle([0, wall_top - ow, W, wall_bot + 4], fill=OUTLINE)
    d.rectangle([0, wall_top, W, wall_bot], fill=WALL)
    d.rectangle([0, wall_top, W, wall_top + 6], fill=STONE_LT)
    d.line([(0, wall_top + 22), (W, wall_top + 22)], fill=WALL_DK, width=2)
    d.line([(0, wall_top + 42), (W, wall_top + 42)], fill=WALL_DK, width=2)
    for x in range(24, W, 44):
        d.line([(x, wall_top), (x, wall_bot)], fill=WALL_DK, width=2)

    # 납작 어두운 창 하나 (글로우 아님, 중상 — UI 가운데와 안 겹치게 작게)
    wx0, wy0, wx1, wy1 = 596, 168, 684, 248
    d.rectangle([wx0 - 5, wy0 - 5, wx1 + 5, wy1 + 5], fill=OUTLINE)
    d.rectangle([wx0, wy0, wx1, wy1], fill=CEILING)

    # 바닥 석판
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

    # --- 왼뒤 가구 (가장자리, 가운데 UI 자리 아님) ---
    # 침낭 직사각 3 (WOOD, 두꺼운 외곽, 말린 결만)
    beds = (
        (28, 404, 148, 428),
        (18, 436, 132, 458),
        (36, 468, 156, 492),
    )
    for x0, y0, x1, y1 in beds:
        _box(d, x0, y0, x1, y1, WOOD, ow=4)
        mid = (y0 + y1) // 2
        d.line([(x0 + 8, mid), (x1 - 8, mid)], fill=WOOD_DK, width=2)
        d.line([(x0 + 10, y0 + 4), (x1 - 10, y0 + 4)], fill=WOOD_LT, width=2)
        # 머리 쪽 말린 덩어리 (직사각, 원/고리 아님)
        hx1 = x0 + 18
        _box(d, x0 + 2, y0 + 2, hx1, y1 - 2, WOOD_DK, ow=0)

    # 궤짝 큐보이드 2
    _cuboid(d, 168, 400, 52, 36, 14, WOOD, WOOD_DK, ow=4)
    d.line([(176, 412), (212, 412)], fill=WOOD_DK, width=2)
    d.line([(176, 424), (212, 424)], fill=WOOD_DK, width=2)
    # 납작 금 걸쇠 (블룸 없음)
    d.rectangle([188, 414, 200, 422], fill=OUTLINE)
    d.rectangle([190, 416, 198, 420], fill=GOLD)

    _cuboid(d, 172, 448, 44, 30, 12, WOOD, WOOD_DK, ow=4)
    d.line([(180, 458), (208, 458)], fill=WOOD_DK, width=2)
    d.rectangle([188, 456, 198, 464], fill=OUTLINE)
    d.rectangle([190, 458, 196, 462], fill=GOLD)

    # --- 오른뒤 나무 랙 실루엣 (세로 막대만, 창/방패/문양 없음) ---
    # 기단
    _box(d, 1096, 500, 1256, 524, DIRT_DK, ow=4)
    _box(d, 1108, 508, 1244, 520, WOOD_DK, ow=0)
    # 세로 기둥 4
    poles = (1116, 1152, 1188, 1224)
    for px in poles:
        _box(d, px, 300, px + 16, 512, WOOD, ow=4)
        d.line([(px + 4, 312), (px + 4, 500)], fill=WOOD_LT, width=2)
        d.line([(px + 11, 320), (px + 11, 496)], fill=WOOD_DK, width=2)
        for y in (340, 400, 460):
            d.line([(px + 2, y), (px + 14, y)], fill=WOOD_DK, width=2)
    # 가로 막대 둘 (십자 구조만, 무기 없음)
    _box(d, 1112, 328, 1244, 344, WOOD, ow=4)
    d.line([(1120, 334), (1236, 334)], fill=WOOD_LT, width=2)
    _box(d, 1112, 456, 1244, 470, WOOD, ow=4)
    d.line([(1120, 461), (1236, 461)], fill=WOOD_DK, width=2)

    out = HERE / "out_oxalpha_bg_character.png"
    im.save(out, optimize=True)
    print(f"→ {out.name}  {W}×{H}  {im.mode}  {out.stat().st_size}B")
    return im, out


if __name__ == "__main__":
    make()
