#!/usr/bin/env python3
"""ox-alpha 결과 배경(bg_result) 코드합성 — 2752×1536 더스크+전장 표식.

배경: 결과 화면은 UI 판이라 필드/던전 프랍 오버레이가 없는데 bg_result 는
아직 나노바나나 2752×1536·~4.2MB 페인터리 캠프(원형 방패 둘·나무 십자 깃대
+해진 깃발·돌반지 모닥불+잉걸불·굽은 나무·안개)라 톤이 따로 논다. 같은
웜톤 팔레트·두꺼운 외곽선의 기하 판으로 교체한다. 왼쪽 ~50–60%는 본문 UI
라 하늘+먼 언덕+어두운 빈 흙만. 관심은 오른쪽(큐보이드 나무 말뚝+방패 원반).
props·STATUS·W3Party·FX·bg_estate·bg_title·bg_field·bg_dungeon 미변경.

화면 배경이라 256 프랍 파이프라인 강제 아님 — 해상도 2752×1536 RGBA 유지.
.cs/.meta/GUID 무변경.

디자인: 위 하늘 띠 → 먼 언덕 → 흙 마당+흙길대 +오른쪽 나무 말뚝·방패 원반
(+맨오른쪽 마른 나무 실루엣). 모닥불·잉걸불·연기·돌반지·글자·글로우 없음.
§6-A: 바닥 큰 원/고리/글로우 금지. 할로우 금지.

사용: python3 gen_oxalpha_bg_result.py
출력: art/out_oxalpha_bg_result.png (2752×1536 RGBA)
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
DIRT = (150, 118, 78)
DIRT_LT = (176, 140, 96)
DIRT_DK = (108, 82, 52)
WOOD = (150, 104, 58)
WOOD_DK = (96, 64, 34)
WOOD_LT = (176, 140, 96)
STONE = (132, 128, 122)
STONE_DK = (86, 82, 76)
GOLD = (216, 172, 96)


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


def _cuboid(d, x0, y0, w, h, kd, front, side, ow=8):
    """정면+오른쪽 면 큐보이드 (킵/말뚝 공통 문법)."""
    sp = [
        (x0 + w, y0),
        (x0 + w + kd, y0 + 18),
        (x0 + w + kd, y0 + h + 18),
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


def _shield(d, cx, cy, r):
    """둥근 방패 원반 + STONE 보스 + 납작 GOLD 악센트 (글로우 아님)."""
    ow = 8
    d.ellipse([cx - r - ow, cy - r - ow, cx + r + ow, cy + r + ow], fill=OUTLINE)
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=STONE)
    # 테 띠
    rim = max(10, r // 7)
    d.ellipse(
        [cx - r + rim, cy - r + rim, cx + r - rim, cy + r - rim],
        fill=STONE_DK,
    )
    inner = rim + max(6, r // 10)
    d.ellipse(
        [cx - r + inner, cy - r + inner, cx + r - inner, cy + r - inner],
        fill=STONE,
    )
    # 보스
    br = max(14, r // 5)
    d.ellipse([cx - br - 5, cy - br - 5, cx + br + 5, cy + br + 5], fill=OUTLINE)
    d.ellipse([cx - br, cy - br, cx + br, cy + br], fill=STONE_DK)
    # 납작 금 점 (블룸 없음)
    gr = max(5, r // 12)
    d.ellipse([cx - gr, cy - gr, cx + gr, cy + gr], fill=GOLD)


def make():
    im = Image.new("RGB", (W, H), SKY_TOP)
    d = ImageDraw.Draw(im)
    rng = random.Random(20260827)

    # 하늘 가로 띠 (웜 더스크, 콜드 던전 홀 아님)
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
    for _ in range(56):
        x = rng.randint(40, W - 40)
        y = rng.randint(20, 280)
        d.point((x, y), fill=star)

    # 먼 언덕 두 겹 — 왼쪽은 완만(UI 자리), 기복은 중우
    d.polygon(
        [
            (0, 760), (260, 620), (560, 680), (900, 600), (1240, 650),
            (1580, 560), (1920, 520), (2280, 580), (2520, 510), (2752, 540),
            (2752, 880), (0, 880),
        ],
        fill=HILL_DK,
    )
    d.polygon(
        [
            (0, 840), (300, 730), (680, 790), (1040, 720), (1400, 770),
            (1760, 690), (2100, 740), (2440, 680), (2752, 720),
            (2752, 960), (0, 960),
        ],
        fill=HILL,
    )

    # 흙 마당 (지평 아래). 왼쪽은 더 어두운 빈 판 — 본문 UI
    d.rectangle([0, 868, W, H], fill=DIRT)
    d.polygon(
        [
            (0, 868),
            (1580, 868),
            (1420, H),
            (0, H),
        ],
        fill=DIRT_DK,
    )
    # 오른쪽이 조금 높아지는 전경 비탈 (캠프 자리)
    d.polygon(
        [
            (1680, H), (1880, 1220), (2140, 1000), (2420, 920),
            (2752, 880), (2752, H),
        ],
        fill=DIRT_DK,
    )
    d.polygon(
        [
            (1760, H), (1960, 1280), (2220, 1080), (2500, 1000),
            (2752, 980), (2752, H),
        ],
        fill=DIRT,
    )

    # 흙길 띠 (아래 중우 → 말뚝 기슭, 원/고리 아님)
    path = [(1520, H), (1680, 1280), (1860, 1080), (1980, 940)]
    d.line(path, fill=DIRT_DK, width=108)
    d.line(path, fill=DIRT, width=76)
    d.line(path, fill=DIRT_LT, width=48)

    # 성긴 풀 패치 (왼쪽은 더 성기게, 원/고리 아님)
    grass = (110, 132, 72)
    grass_lt = (148, 168, 90)
    for _ in range(48):
        x = rng.randint(20, W - 80)
        y = rng.randint(920, H - 40)
        if x < 1500 and rng.random() < 0.72:
            continue
        w = rng.randint(36, 110)
        hh = rng.randint(12, 26)
        d.ellipse([x, y, x + w, y + hh], fill=grass if rng.random() < 0.6 else grass_lt)

    # --- 오른쪽 전장 표식 (모닥불/깃발 글자 없음) ---
    # 큐보이드 나무 말뚝
    px, py, pw, ph, pd = 1988, 548, 64, 760, 28
    _cuboid(d, px, py, pw, ph, pd, WOOD, WOOD_DK, ow=8)
    # 가로결
    for y in range(py + 48, py + ph - 20, 56):
        d.line([(px + 8, y), (px + pw - 8, y)], fill=WOOD_DK, width=3)
    d.line([(px + 18, py + 12), (px + 22, py + ph - 12)], fill=WOOD_LT, width=4)
    d.line([(px + 44, py + 20), (px + 40, py + ph - 16)], fill=WOOD_DK, width=3)
    # 가로 막대 하나 (깃발 그림/글자 없음 — 십자 구조만)
    bx0, by0, bw, bh, bd = px - 92, py + 96, 248, 36, 18
    _cuboid(d, bx0, by0, bw, bh, bd, WOOD, WOOD_DK, ow=6)
    d.line([(bx0 + 12, by0 + 10), (bx0 + bw - 12, by0 + 10)], fill=WOOD_LT, width=3)
    d.line([(bx0 + 12, by0 + bh - 10), (bx0 + bw - 12, by0 + bh - 10)], fill=WOOD_DK, width=2)
    # 말뚝 기단 흙 블록
    _box(d, px - 36, py + ph - 18, px + pw + 48, py + ph + 36, DIRT_DK, ow=6)
    _box(d, px - 18, py + ph - 8, px + pw + 28, py + ph + 22, DIRT, ow=0)

    # 방패 원반 둘 (STONE 보스, 납작 GOLD)
    _shield(d, 1864, 1196, 96)
    _shield(d, 2188, 1248, 74)

    # 맨오른쪽 마른 나무 실루엣 (WOOD 줄기, 잎·눈구멍 없음)
    tx, ty = 2488, 1368
    _poly(
        d,
        [(tx - 40, ty), (tx - 12, ty - 58), (tx + 12, ty - 58), (tx + 44, ty)],
        WOOD_DK,
        ow=8,
    )
    trunk = [(tx - 18, ty - 50), (tx + 4, 820), (tx + 32, 820), (tx + 14, ty - 50)]
    _poly(d, trunk, WOOD, ow=10)
    d.line([(tx - 2, ty - 70), (tx + 16, 840)], fill=WOOD_LT, width=4)
    d.line([(tx + 12, ty - 60), (tx + 24, 850)], fill=WOOD_DK, width=3)
    for y in (940, 1040, 1160, 1280):
        d.line([(tx - 6, y), (tx + 18, y - 6)], fill=WOOD_DK, width=2)
    _wood_line(d, (tx + 8, 880), (tx - 120, 680), 14, 8)
    _wood_line(d, (tx - 70, 740), (tx - 170, 600), 11, 6)
    _wood_line(d, (tx - 80, 750), (tx - 50, 560), 10, 5)
    _wood_line(d, (tx + 20, 900), (tx + 150, 700), 14, 8)
    _wood_line(d, (tx + 100, 760), (tx + 190, 620), 11, 6)
    _wood_line(d, (tx + 110, 770), (tx + 80, 560), 10, 5)
    _wood_line(d, (tx + 12, 1020), (tx - 90, 900), 12, 7)
    _wood_line(d, (tx + 22, 1080), (tx + 130, 960), 11, 6)
    _wood_line(d, (tx + 10, 840), (tx - 20, 540), 11, 6)
    _wood_line(d, (tx + 24, 860), (tx + 70, 560), 11, 6)

    # 앞쪽 흙 결
    for _ in range(140):
        x = rng.randint(0, W)
        y = rng.randint(1120, H - 8)
        d.line(
            [(x, y), (x + rng.randint(10, 36), y + rng.randint(-3, 3))],
            fill=DIRT_DK,
            width=1,
        )

    im = im.convert("RGBA")
    # 완전 불투명
    im.putalpha(255)
    out = HERE / "out_oxalpha_bg_result.png"
    im.save(out, optimize=True)
    print(f"→ {out.name}  {W}×{H}  {im.mode}  {out.stat().st_size}B")
    return im, out


if __name__ == "__main__":
    make()
