#!/usr/bin/env python3
"""ox-alpha 타이틀 배경(bg_title) 코드합성 — 2752×1536 하늘+언덕+탑.

배경: 영지·프랍은 ox-alpha인데 bg_title 은 아직 나노바나나
2752×1536·4.5MB 페인터리 밤(들쭉날쭉 탑·폐허 계단·마른 나무·반딧불)이라
톤이 따로 논다. 같은 웜톤 팔레트·두꺼운 외곽선의 기하 판으로 교체한다.
왼쪽 ~54%는 타이틀 UI 패널이라 하늘+먼 언덕만. 관심은 중우(큐보이드 탑)
+오른쪽(돌 폐허 계단+마른 나무). props·STATUS·W3Party·FX·bg_estate 미변경.

화면 배경이라 256 프랍 파이프라인 강제 아님 — 해상도 2752×1536 유지.
.cs/.meta/GUID 무변경.

디자인: 위 하늘 띠 → 먼 언덕 → 큐보이드 킵 실루엣 → 흙 언덕+풀 패치+흙길대
+오른쪽 돌계단·마른 나무. §6-A: 바닥 큰 원/고리/글로우 금지. 할로우 금지.
글자·깃발·창광·오두막·반딧불 없음.

사용: python3 gen_oxalpha_bg_title.py
출력: art/out_oxalpha_bg_title.png (2752×1536 RGBA)
"""
from pathlib import Path
import random

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
W, H = 2752, 1536

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
WOOD_DK = (96, 64, 34)
WOOD_LT = (176, 140, 96)


def _lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def _box(d, x0, y0, x1, y1, fill, ow=6):
    d.rectangle([x0 - ow, y0 - ow, x1 + ow, y1 + ow], fill=OUTLINE)
    d.rectangle([x0, y0, x1, y1], fill=fill)


def _poly(d, pts, fill, ow=8):
    d.line(pts + [pts[0]], fill=OUTLINE, width=ow * 2)
    d.polygon(pts, fill=fill)


def _wood_line(d, a, b, ow, tw):
    d.line([a, b], fill=OUTLINE, width=ow)
    d.line([a, b], fill=WOOD, width=tw)


def make():
    im = Image.new("RGB", (W, H), SKY_TOP)
    d = ImageDraw.Draw(im)
    rng = random.Random(20260827)

    # 하늘 가로 띠 (웜 더스크, 콜드 그레이블루 아님)
    horizon = 662
    mid_y = 341
    for y in range(horizon):
        if y < mid_y:
            c = _lerp(SKY_TOP, SKY_MID, y / mid_y)
        else:
            c = _lerp(SKY_MID, SKY_HOR, (y - mid_y) / (horizon - mid_y))
        d.line([(0, y), (W, y)], fill=c)

    # 1px 납작 별점 (블룸·반딧불 아님)
    star = (210, 198, 176)
    for _ in range(70):
        x = rng.randint(40, W - 40)
        y = rng.randint(20, 280)
        d.point((x, y), fill=star)

    # 먼 언덕 두 겹 — 왼쪽은 완만(UI 자리), 기복은 중우
    d.polygon(
        [
            (0, 720), (220, 560), (520, 620), (860, 540), (1180, 600),
            (1480, 530), (1760, 500), (2100, 560), (2420, 490), (2752, 540),
            (2752, 860), (0, 860),
        ],
        fill=HILL_DK,
    )
    d.polygon(
        [
            (0, 800), (280, 680), (640, 740), (980, 670), (1320, 720),
            (1680, 650), (2020, 700), (2360, 640), (2752, 690),
            (2752, 940), (0, 940),
        ],
        fill=HILL,
    )

    # 큐보이드 킵 — 중우 실루엣 (타이틀 식별, 깃발·창광 없음)
    kx, ky, kw, kh, kd = 1788, 228, 148, 430, 52
    # 오른쪽 면 (어두운 돌)
    side = [
        (kx + kw, ky),
        (kx + kw + kd, ky + 22),
        (kx + kw + kd, ky + kh + 22),
        (kx + kw, ky + kh),
    ]
    d.polygon(side, fill=OUTLINE)
    d.polygon(
        [
            (kx + kw, ky + 6),
            (kx + kw + kd - 8, ky + 24),
            (kx + kw + kd - 8, ky + kh + 14),
            (kx + kw, ky + kh - 6),
        ],
        fill=STONE_DK,
    )
    # 정면
    _box(d, kx, ky, kx + kw, ky + kh, STONE, ow=8)
    # 가로 돌줄
    for y in range(ky + 48, ky + kh - 20, 52):
        d.line([(kx + 8, y), (kx + kw - 8, y)], fill=STONE_DK, width=3)
    for x in range(kx + 36, kx + kw - 8, 44):
        d.line([(x, ky + 8), (x, ky + kh - 8)], fill=STONE_DK, width=2)
    # 지붕 띠
    _box(d, kx - 10, ky - 18, kx + kw + 10, ky + 8, STONE_DK, ow=6)
    # 총안 (작은 큐브)
    for i in range(5):
        cx0 = kx + 6 + i * 28
        _box(d, cx0, ky - 44, cx0 + 18, ky - 14, STONE, ow=4)
    # 꼭대기 오른쪽 면 한 칸
    _box(d, kx + kw, ky - 36, kx + kw + 28, ky + 4, STONE_DK, ow=4)

    # 마당/흙 언덕 (지평 아래)
    d.rectangle([0, 854, W, H], fill=DIRT)
    # 오른쪽이 조금 높아지는 전경 비탈
    d.polygon(
        [
            (1680, H), (1900, 1180), (2150, 980), (2420, 900),
            (2752, 860), (2752, H),
        ],
        fill=DIRT_DK,
    )
    d.polygon(
        [
            (1760, H), (1980, 1240), (2220, 1060), (2480, 980),
            (2752, 960), (2752, H),
        ],
        fill=DIRT,
    )

    # 풀 패치 (성긴, 원/고리 아님)
    for _ in range(56):
        x = rng.randint(20, W - 80)
        y = rng.randint(900, H - 40)
        # 왼쪽은 더 성기게
        if x < 1480 and rng.random() < 0.45:
            continue
        w = rng.randint(36, 110)
        h = rng.randint(12, 26)
        d.ellipse([x, y, x + w, y + h], fill=GRASS if rng.random() < 0.6 else GRASS_LT)

    # 흙길 띠 (아래 중우 → 탑 기슭, 원/고리 아님)
    path = [(1620, H), (1700, 1240), (1788, 1040), (1840, 900)]
    d.line(path, fill=DIRT_DK, width=96)
    d.line(path, fill=DIRT_LT, width=64)

    # 오른쪽 돌 폐허 계단 (블록, 부서진 기둥)
    # 기단
    _box(d, 2080, 1288, 2688, 1410, STONE_DK, ow=7)
    _box(d, 2100, 1304, 2660, 1394, STONE, ow=0)
    for x in range(2140, 2640, 56):
        d.line([(x, 1308), (x, 1390)], fill=STONE_DK, width=3)
    # 계단 단
    steps = [
        (2140, 1240, 2520, 1310),
        (2200, 1176, 2560, 1252),
        (2260, 1112, 2600, 1190),
        (2320, 1048, 2630, 1126),
        (2380, 984, 2650, 1062),
        (2440, 920, 2668, 998),
    ]
    for (x0, y0, x1, y1) in steps:
        _box(d, x0, y0, x1, y1, STONE, ow=6)
        d.line([(x0 + 8, (y0 + y1) // 2), (x1 - 8, (y0 + y1) // 2)], fill=STONE_DK, width=3)

    # 부서진 문주 (창광 없는 빈 입구)
    _box(d, 2488, 760, 2536, 940, STONE, ow=6)
    _box(d, 2610, 790, 2658, 940, STONE, ow=6)
    _box(d, 2480, 732, 2664, 776, STONE_DK, ow=6)
    # 무너진 상인방 조각
    _box(d, 2568, 748, 2620, 772, STONE, ow=4)
    _box(d, 2588, 980, 2648, 1028, STONE_DK, ow=5)

    # 기슭 잔해 블록
    for (x0, y0, x1, y1) in (
        (2020, 1360, 2100, 1428),
        (2680, 1340, 2740, 1416),
        (2188, 1410, 2280, 1470),
        (2410, 1424, 2504, 1488),
    ):
        _box(d, x0, y0, x1, y1, STONE if rng.random() < 0.5 else STONE_DK, ow=5)

    # 마른 나무 한 그루 (WOOD 줄기, 두꺼운 OUTLINE, 잎·눈구멍 없음)
    tx, ty = 2310, 1380  # 뿌리
    # 뿌리
    _poly(
        d,
        [(tx - 48, ty), (tx - 16, ty - 70), (tx + 14, ty - 70), (tx + 52, ty)],
        WOOD_DK,
        ow=8,
    )
    # 줄기 (살짝 기울)
    trunk = [(tx - 22, ty - 60), (tx + 10, 780), (tx + 42, 780), (tx + 18, ty - 60)]
    _poly(d, trunk, WOOD, ow=10)
    d.line([(tx - 4, ty - 80), (tx + 18, 800)], fill=WOOD_LT, width=4)
    d.line([(tx + 16, ty - 70), (tx + 30, 810)], fill=WOOD_DK, width=3)
    # 옹이(가로결만, 눈구멍 아님)
    for y in (920, 1020, 1140, 1260):
        d.line([(tx - 8, y), (tx + 22, y - 8)], fill=WOOD_DK, width=2)

    # 가지 (비틀린 마른 가지)
    _wood_line(d, (tx + 8, 840), (tx - 140, 620), 16, 9)
    _wood_line(d, (tx - 80, 700), (tx - 200, 560), 12, 6)
    _wood_line(d, (tx - 90, 710), (tx - 70, 540), 10, 5)
    _wood_line(d, (tx + 24, 860), (tx + 170, 640), 16, 9)
    _wood_line(d, (tx + 110, 720), (tx + 210, 560), 12, 6)
    _wood_line(d, (tx + 120, 730), (tx + 90, 520), 10, 5)
    _wood_line(d, (tx + 16, 980), (tx - 110, 860), 14, 8)
    _wood_line(d, (tx + 28, 1040), (tx + 160, 900), 12, 7)
    _wood_line(d, (tx + 8, 780), (tx - 40, 480), 11, 6)
    _wood_line(d, (tx + 30, 800), (tx + 80, 500), 11, 6)

    # 앞쪽 흙 결
    for _ in range(140):
        x = rng.randint(0, W)
        y = rng.randint(1100, H - 8)
        d.line(
            [(x, y), (x + rng.randint(10, 36), y + rng.randint(-3, 3))],
            fill=DIRT_DK,
            width=1,
        )

    im = im.convert("RGBA")
    out = HERE / "out_oxalpha_bg_title.png"
    im.save(out, optimize=True)
    print(f"→ {out.name}  {W}×{H}  {im.mode}  {out.stat().st_size}B")


if __name__ == "__main__":
    make()
