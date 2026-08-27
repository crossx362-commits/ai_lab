#!/usr/bin/env python3
"""ox-alpha 월드맵 배경(bg_worldmap) 코드합성 — 1280×720 우주 성계.

배경: 월드맵은 UI 오버레이 화면(노드·라벨이 위, 하단 도크)인데 bg_worldmap 은
아직 나노바나나 1280×720·~0.87MB 페인터리 성계(남색 공허·황토 성운·구형 글로우
·그레인·비네트)라 톤이 따로 논다. 같은 ox-alpha 납작 팔레트·두꺼운 외곽선의
궤도+행성 원반 판으로 교체한다. 왼쪽·위는 타이틀/HUD라 연다. 관심은 중우 4행성.
props·STATUS·W3Party·FX·bg_estate·bg_title·bg_field·bg_dungeon·bg_result
·bg_character·bg_party·bg_tower 미변경.

화면 배경이라 256 프랍 파이프라인 강제 아님 — 해상도 1280×720 RGB 유지.
.cs/.meta/GUID 무변경.

디자인: 위 하늘 띠(SKY_TOP→AIR, 그레인 없음) → 4–6 동심 타원 궤도(얇은
OUTLINE/STONE_DK, 살짝 비정형이나 기하) → 궤도 위 행성 원반 4개(납작 채움+
두꺼운 외곽선). 성운·글로우 테·3D 구·나침반·글자 없음.
§6-A 바닥 마법진 고리는 궤도 타원에 적용하지 않는다 — 궤도가 월드맵 자체다.

사용: python3 gen_oxalpha_bg_worldmap.py
출력: art/out_oxalpha_bg_worldmap.png (1280×720 RGB)
"""
from pathlib import Path
import math
import random

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
W, H = 1280, 720

OUTLINE = (40, 24, 20)
SKY_TOP = (48, 50, 56)
AIR = (72, 78, 88)
STONE = (132, 128, 122)
STONE_DK = (86, 82, 76)
STONE_LT = (158, 154, 148)
HILL = (102, 122, 68)
WOOD = (150, 104, 58)
GOLD = (216, 172, 96)


def _lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def _orbit_xy(cx, cy, rx, ry, rot, ang, wobble):
    """타원 위 한 점. wobble 은 기하 비정형(스케치 그레인 아님)."""
    a = math.radians(ang)
    wr = 1.0 + wobble * math.cos(3.0 * a)
    x = rx * wr * math.cos(a)
    y = ry * wr * math.sin(a)
    cr, sr = math.cos(rot), math.sin(rot)
    return cx + x * cr - y * sr, cy + x * sr + y * cr


def _orbit_pts(cx, cy, rx, ry, rot, wobble, n=360):
    return [
        _orbit_xy(cx, cy, rx, ry, rot, 360.0 * i / n, wobble)
        for i in range(n)
    ]


def _disc(d, x, y, r, fill, ow=6):
    """납작 원반 + 두꺼운 외곽선 (3D 구·글로우 테 아님)."""
    ix, iy, ir = int(round(x)), int(round(y)), int(r)
    d.ellipse(
        [ix - ir - ow, iy - ir - ow, ix + ir + ow, iy + ir + ow],
        fill=OUTLINE,
    )
    d.ellipse([ix - ir, iy - ir, ix + ir, iy + ir], fill=fill)


def make():
    im = Image.new("RGB", (W, H), SKY_TOP)
    d = ImageDraw.Draw(im)
    rng = random.Random(20260827)

    # 하늘 가로 띠 (콜드 플랫, 페인터리 그레인·비네트 아님)
    for y in range(H):
        d.line([(0, y), (W, y)], fill=_lerp(SKY_TOP, AIR, y / (H - 1)))

    # 1px 납작 별점 (블룸·반짝임 아님) — 왼쪽·위·가운데는 성기게(타이틀/노드)
    for _ in range(86):
        x = rng.randint(12, W - 12)
        y = rng.randint(10, H - 12)
        if x < 420 and y < 160 and rng.random() < 0.55:
            continue
        if 480 < x < 900 and 180 < y < 480 and rng.random() < 0.40:
            continue
        d.point((x, y), fill=STONE_LT if rng.random() < 0.7 else STONE)

    # 동심 타원 궤도 6 — 중우 치우침, 전부 온캔버스, 하단 도크(~y520) 위 행성
    cx, cy = 700.0, 350.0
    rot = math.radians(14.0)
    orbits = [
        (148, 56, 0.028),
        (232, 90, 0.032),
        (328, 126, 0.026),
        (430, 164, 0.030),
        (538, 204, 0.024),
        (580, 220, 0.018),
    ]
    for i, (rx, ry, wob) in enumerate(orbits):
        pts = _orbit_pts(cx, cy, rx, ry, rot, wob)
        pts.append(pts[0])
        col = OUTLINE if i % 2 == 0 else STONE_DK
        d.line(pts, fill=col, width=2 if i > 0 else 3)

    # 행성 원반 4 — 궤도 위에 앉힘 (레이아웃만 구 Nano Banana 번역)
    # (orbit_i, angle_deg, radius, fill)
    planets = [
        (1, -88.0, 17, STONE_DK),  # 안쪽 작은 돌 — 계 위쪽
        (2, -142.0, 25, WOOD),     # 나무빛 — 안쪽 행성 왼쪽
        (3, -32.0, 38, HILL),      # 풀빛 — 중우
        (4, -20.0, 52, STONE_LT),  # 큰 석조 — 우측 (도크 위)
    ]
    placed = []
    for oi, ang, pr, fill in planets:
        rx, ry, wob = orbits[oi]
        x, y = _orbit_xy(cx, cy, rx, ry, rot, ang, wob)
        placed.append((x, y, pr, fill))
        _disc(d, x, y, pr, fill, ow=7 if pr >= 38 else 6)

    # HILL 행성: GOLD 납작 악센트 하나 (글로우 아님, 작은 원반)
    hx, hy, hr, _ = placed[2]
    _disc(d, hx - hr * 0.28, hy - hr * 0.18, 7, GOLD, ow=3)

    # 큰 석조 행성: STONE_DK 납작 크레이터 둘 (마블 스월 아님)
    lx, ly, lr, _ = placed[3]
    _disc(d, lx - 16, ly - 8, 9, STONE_DK, ow=3)
    _disc(d, lx + 11, ly + 12, 6, STONE, ow=3)

    out = HERE / "out_oxalpha_bg_worldmap.png"
    im.save(out, optimize=True)
    print(f"→ {out.name}  {W}×{H}  {im.mode}  {out.stat().st_size}B")
    return im, out


if __name__ == "__main__":
    make()
