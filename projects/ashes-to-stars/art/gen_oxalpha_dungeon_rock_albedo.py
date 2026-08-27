#!/usr/bin/env python3
"""ox-alpha 던전 바닥 알베도(dungeon_rock_albedo) 코드합성 — 2048×2048 타일.

배경: GroundHollowSelfCheck 가 ground/dungeon_rock_albedo 를 wrap Repeat 로
깔고 정점 색을 곱한다. 나노바나나 2048×2048·~4.6MB 남색·보라 갈라진 할로우
석판+흰 별자갈이라 밤/공허로 읽히고, 이미 ox-alpha 인 bg_dungeon·던전 프랍과
톤이 따로 논다. 같은 웜톤 납작 팔레트의 석조 홀 바닥 타일로 교체한다. 256
프랍 파이프라인 강제 아님 — 해상도 2048×2048 RGB 유지. .cs/.meta/GUID 무변경.

디자인: FLOOR_DK 모르타르 바탕 + 가장자리 넘어가는 비정형 직사각 석판
(STONE/STONE_LT/FLOOR, OUTLINE 얇은 줄눈) → 성긴 작은 STONE_DK 칩.
§6-A: 바닥 큰 원/고리/글로우 금지. 흰 별자갈·남색/보라 할로우 금지.
길·건물·수정·기둥 없음. 필드 흙+풀(field_plain)과 구별되는 석조 홀 바닥.

타일링: wrap Repeat. 가장자리를 넘는 석판·금·칩은 반대편으로 이어진다
(modulo 9-stamp). 줄눈 격자 자체도 주기 분할이라 이음매가 맞다.

사용: python3 gen_oxalpha_dungeon_rock_albedo.py
출력: art/out_oxalpha_dungeon_rock_albedo.png (2048×2048 RGB)
"""
from pathlib import Path
import random

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
W, H = 2048, 2048

OUTLINE = (40, 24, 20)
STONE = (132, 128, 122)
STONE_DK = (86, 82, 76)
STONE_LT = (158, 154, 148)
FLOOR = (118, 112, 104)
FLOOR_LT = (140, 134, 124)
FLOOR_DK = (78, 74, 68)
WALL = (96, 94, 90)

PALETTE = [OUTLINE, STONE, STONE_DK, STONE_LT, FLOOR, FLOOR_LT, FLOOR_DK, WALL]
PAVER_FILLS = [STONE, STONE, STONE, FLOOR, FLOOR, FLOOR_LT, STONE_LT, WALL]


def _palette(colors):
    pal = Image.new("P", (1, 1))
    data = []
    for c in colors:
        data.extend(c)
    data.extend([0] * (768 - len(data)))
    pal.putpalette(data)
    return pal


def _quantize(im, colors):
    return im.quantize(palette=_palette(colors), dither=Image.NONE).convert("RGB")


def _wrap_upscale(small, out_w, out_h):
    """작은 RGB 타일을 3×3 붙인 뒤 보간·중앙 크롭 — 가장자리가 이어진다."""
    pw, ph = small.size
    tiled = Image.new("RGB", (pw * 3, ph * 3))
    for y in range(3):
        for x in range(3):
            tiled.paste(small, (x * pw, y * ph))
    big = tiled.resize((out_w * 3, out_h * 3), Image.BILINEAR)
    return big.crop((out_w, out_h, out_w * 2, out_h * 2))


def _small_noise(n, rng, colors, weights):
    im = Image.new("RGB", (n, n))
    px = im.load()
    bag = []
    for c, w in zip(colors, weights):
        bag.extend([c] * w)
    for y in range(n):
        for x in range(n):
            px[x, y] = bag[rng.randrange(len(bag))]
    return im


def _wraps():
    for oy in (-H, 0, H):
        for ox in (-W, 0, W):
            yield ox, oy


def w_ellipse(d, cx, cy, rx, ry, fill):
    for ox, oy in _wraps():
        d.ellipse(
            [cx + ox - rx, cy + oy - ry, cx + ox + rx, cy + oy + ry],
            fill=fill,
        )


def w_line(d, x0, y0, x1, y1, fill, width=1):
    for ox, oy in _wraps():
        d.line(
            [(x0 + ox, y0 + oy), (x1 + ox, y1 + oy)],
            fill=fill,
            width=width,
        )


def w_polygon(d, pts, fill):
    for ox, oy in _wraps():
        d.polygon([(x + ox, y + oy) for x, y in pts], fill=fill)


def _partition(total, min_s, max_s, rng):
    """total 을 [min_s, max_s] 조각으로 나눠 합이 정확히 total (주기 타일)."""
    parts = []
    left = total
    while left > 0:
        hi = max_s + (90 if rng.random() < 0.14 else 0)
        if left <= hi:
            if left < min_s and parts:
                parts[-1] += left
            else:
                parts.append(left)
            break
        cap = min(hi, left - min_s)
        if cap < min_s:
            parts.append(left)
            break
        s = rng.randint(min_s, cap)
        parts.append(s)
        left -= s
    drift = total - sum(parts)
    if drift and parts:
        parts[-1] += drift
    return parts


def _jitter_rect(x0, y0, x1, y1, rng, j=5):
    """직사각 석판 모서리를 조금만 비틀어 비정형 슬래브로."""
    return [
        (x0 + rng.randint(-j, j), y0 + rng.randint(-j, j)),
        (x1 + rng.randint(-j, j), y0 + rng.randint(-j, j)),
        (x1 + rng.randint(-j, j), y1 + rng.randint(-j, j)),
        (x0 + rng.randint(-j, j), y1 + rng.randint(-j, j)),
    ]


def make():
    rng = random.Random(20260827 + 41)

    # 모르타르 바탕 (필드 흙이 아님 — 석조 줄눈)
    im = Image.new("RGB", (W, H), FLOOR_DK)
    d = ImageDraw.Draw(im)

    # 줄눈에 성긴 OUTLINE 결 (프레임·고리 아님)
    mortar_n = _small_noise(48, rng, [FLOOR_DK, OUTLINE, WALL], [18, 4, 3])
    mortar_big = _quantize(_wrap_upscale(mortar_n, W, H), [FLOOR_DK, OUTLINE, WALL])
    db = bytearray(im.tobytes())
    mb = mortar_big.tobytes()
    rng_m = random.Random(20260827 + 41 + 3)
    for i in range(0, len(mb), 3):
        if mb[i : i + 3] != bytes(FLOOR_DK) and rng_m.random() < 0.35:
            db[i : i + 3] = mb[i : i + 3]
    im = Image.frombytes("RGB", (W, H), bytes(db))
    d = ImageDraw.Draw(im)

    # 주기 분할 석판 격자 — 가로·세로 합이 W/H 이라 타일 이음이 맞다
    row_hs = _partition(H, 70, 132, rng)
    y = 0
    for rh in row_hs:
        widths = _partition(W, 88, 196, rng)
        stagger = rng.randint(28, 110)
        x = stagger
        for ww in widths:
            inset = rng.randint(4, 7)
            x0 = x + inset
            y0 = y + inset
            x1 = x + ww - inset
            y1 = y + rh - inset
            if x1 - x0 < 20 or y1 - y0 < 16:
                x += ww
                continue
            fill = rng.choice(PAVER_FILLS)
            pts = _jitter_rect(x0, y0, x1, y1, rng, j=rng.randint(2, 6))
            w_polygon(d, pts, fill)
            # 얇은 OUTLINE 줄눈 (두꺼운 프레임 아님, 모르타르 가장자리)
            for a, b in zip(pts, pts[1:] + pts[:1]):
                w_line(d, a[0], a[1], b[0], b[1], OUTLINE, width=1)
            # 윗변 하이라이트 (납작 ox-alpha, 그라데이션 아님)
            if rng.random() < 0.55 and fill != STONE_LT:
                hx0 = pts[0][0] + 8
                hy0 = min(pts[0][1], pts[1][1]) + 3
                hx1 = pts[1][0] - 8
                w_line(d, hx0, hy0, hx1, hy0, STONE_LT, width=2)
            x += ww
        y += rh

    # 석판 안 고주파 결: wrap-safe 노이즈의 LT/DK 만 일부 얹음
    grain = _small_noise(
        96, rng, [STONE, STONE_LT, FLOOR, FLOOR_LT, STONE_DK, WALL], [10, 4, 8, 4, 3, 3]
    )
    grain_big = _quantize(
        _wrap_upscale(grain, W, H),
        [STONE, STONE_LT, FLOOR, FLOOR_LT, STONE_DK, WALL],
    )
    db = bytearray(im.tobytes())
    gb = grain_big.tobytes()
    rng_g = random.Random(20260827 + 41 + 7)
    mort = (bytes(FLOOR_DK), bytes(OUTLINE))
    for i in range(0, len(gb), 3):
        if db[i : i + 3] in mort:
            continue
        if gb[i : i + 3] != db[i : i + 3] and rng_g.random() < 0.16:
            db[i : i + 3] = gb[i : i + 3]
    im = Image.frombytes("RGB", (W, H), bytes(db))
    d = ImageDraw.Draw(im)

    # 짧은 석판 금 (OUTLINE/FLOOR_DK, 프레임·고리 아님)
    for _ in range(110):
        x = rng.uniform(0, W)
        yv = rng.uniform(0, H)
        segs = rng.randint(2, 4)
        x1, y1 = x, yv
        col = OUTLINE if rng.random() < 0.45 else FLOOR_DK
        for _s in range(segs):
            x2 = x1 + rng.randint(-22, 22)
            y2 = y1 + rng.randint(6, 28)
            w_line(d, x1, y1, x2, y2, col, width=1)
            x1, y1 = x2, y2

    # 성긴 작은 STONE_DK 칩 (흰 별·십자 아님, 납작 타원)
    for _ in range(220):
        cx = rng.uniform(0, W)
        cy = rng.uniform(0, H)
        rx = rng.randint(1, 4)
        ry = rng.randint(1, 3)
        w_ellipse(d, cx, cy, rx, ry, STONE_DK)
        if rng.random() < 0.25:
            w_ellipse(
                d,
                cx + 0.8,
                cy + 0.6,
                max(1, rx - 1),
                max(1, ry - 1),
                OUTLINE,
            )

    # 아주 짧은 마모 획 (세로 기미·프랍 아님)
    for _ in range(80):
        x = rng.uniform(0, W)
        yv = rng.uniform(0, H)
        dx = rng.randint(-6, 6)
        dy = rng.randint(-3, 3)
        col = STONE_DK if rng.random() < 0.5 else FLOOR_DK
        w_line(d, x, yv, x + dx, yv + dy, col, width=1)

    im = _quantize(im, PALETTE)
    out = HERE / "out_oxalpha_dungeon_rock_albedo.png"
    im.save(out, optimize=True)
    print(f"→ {out.name}  {W}×{H}  {im.mode}  {out.stat().st_size}B")
    return im, out


if __name__ == "__main__":
    make()
