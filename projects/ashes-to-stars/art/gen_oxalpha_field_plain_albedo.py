#!/usr/bin/env python3
"""ox-alpha 필드 바닥 알베도(field_plain_albedo) 코드합성 — 2048×2048 타일.

배경: NoiseTerrain 이 ground/field_plain_albedo 를 wrap Repeat 로 깔고 정점 색을
곱한다. 나노바나나 2048×2048·~5.8MB 남색 갈라진 할로우 땅+흰 별자갈이라
밤/공허로 읽히고, 이미 ox-alpha 인 bg_field·필드 프랍과 톤이 따로 논다.
같은 웜톤 납작 팔레트의 흙+풀 필드 타일로 교체한다. 256 프랍 파이프라인
강제 아님 — 해상도 2048×2048 RGB 유지. .cs/.meta/GUID 무변경.

디자인: DIRT 바탕 + DIRT_LT/DIRT_DK 결 → 가장자리 넘어가는 비정형
GRASS/GRASS_LT/HILL 타원 얼룩 → 성긴 작은 STONE 자갈 → 짧은 풀 획.
§6-A: 바닥 큰 원/고리/글로우 금지. 흰 별자갈·남색 할로우 금지. 고리 길·건물 없음.
타일링: wrap Repeat. 가장자리를 넘는 얼룩은 반대편으로 이어진다(modulo 9-stamp).

사용: python3 gen_oxalpha_field_plain_albedo.py
출력: art/out_oxalpha_field_plain_albedo.png (2048×2048 RGB)
"""
from pathlib import Path
import random

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
W, H = 2048, 2048

OUTLINE = (40, 24, 20)
GRASS = (110, 132, 72)
GRASS_LT = (148, 168, 90)
DIRT = (150, 118, 78)
DIRT_LT = (176, 140, 96)
DIRT_DK = (108, 82, 52)
HILL = (102, 122, 68)
STONE = (132, 128, 122)


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


def _overlay_not(dst, src, skip):
    """src 가 skip 이 아닌 픽셀만 dst 에 덮는다 (풀 마스크 합성)."""
    db = bytearray(dst.tobytes())
    sb = src.tobytes()
    sr, sg, sv = skip
    for i in range(0, len(sb), 3):
        if sb[i] != sr or sb[i + 1] != sg or sb[i + 2] != sv:
            db[i] = sb[i]
            db[i + 1] = sb[i + 1]
            db[i + 2] = sb[i + 2]
    return Image.frombytes("RGB", dst.size, bytes(db))


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


def make():
    rng = random.Random(20260827)

    dirt_coarse = _small_noise(
        32, rng, [DIRT, DIRT_LT, DIRT_DK], [18, 7, 5]
    )
    dirt_fine = _small_noise(
        128, rng, [DIRT, DIRT_LT, DIRT_DK], [22, 5, 4]
    )
    im = _quantize(
        _wrap_upscale(dirt_coarse, W, H), [DIRT, DIRT_LT, DIRT_DK]
    )
    fine = _quantize(
        _wrap_upscale(dirt_fine, W, H), [DIRT, DIRT_LT, DIRT_DK]
    )
    # 고주파 흙 결: 고운 맵의 LT/DK 만 일부 얹음 (전면 페인터리 그레인 아님)
    db = bytearray(im.tobytes())
    fb = fine.tobytes()
    rng_g = random.Random(20260827 + 7)
    for i in range(0, len(fb), 3):
        if fb[i : i + 3] != bytes(DIRT) and rng_g.random() < 0.22:
            db[i : i + 3] = fb[i : i + 3]
    im = Image.frombytes("RGB", (W, H), bytes(db))

    # 저주파 풀 밭 (wrap-safe 노이즈 얼룩, 원/고리 아님)
    grass_map = _small_noise(
        16,
        rng,
        [DIRT, GRASS, GRASS_LT, HILL],
        [14, 10, 5, 4],
    )
    grass_big = _quantize(
        _wrap_upscale(grass_map, W, H), [DIRT, GRASS, GRASS_LT, HILL]
    )
    im = _overlay_not(im, grass_big, DIRT)

    d = ImageDraw.Draw(im)

    # 비정형 풀 타원 얼룩 — 클러스터당 겹타원, 9-stamp 로 가장자리 연속
    for _ in range(52):
        cx = rng.uniform(0, W)
        cy = rng.uniform(0, H)
        fill = rng.choice([GRASS, GRASS, GRASS_LT, HILL])
        nblob = rng.randint(2, 4)
        for _b in range(nblob):
            rx = rng.randint(28, 140)
            ry = rng.randint(14, 70)
            jx = cx + rng.randint(-40, 40)
            jy = cy + rng.randint(-28, 28)
            w_ellipse(d, jx, jy, rx, ry, fill)

    # 작은 풀 턱 (타원, 고리 아님)
    for _ in range(90):
        cx = rng.uniform(0, W)
        cy = rng.uniform(0, H)
        rx = rng.randint(10, 36)
        ry = rng.randint(6, 18)
        fill = GRASS_LT if rng.random() < 0.55 else HILL
        w_ellipse(d, cx, cy, rx, ry, fill)

    # 풀 안 흙 구멍 (길·고리 아님)
    for _ in range(18):
        cx = rng.uniform(0, W)
        cy = rng.uniform(0, H)
        rx = rng.randint(18, 70)
        ry = rng.randint(10, 36)
        fill = rng.choice([DIRT, DIRT_LT, DIRT_DK])
        w_ellipse(d, cx, cy, rx, ry, fill)

    # 성긴 작은 STONE 자갈 (흰 별·십자 아님, 납작 타원)
    for _ in range(160):
        cx = rng.uniform(0, W)
        cy = rng.uniform(0, H)
        rx = rng.randint(2, 6)
        ry = rng.randint(1, 4)
        w_ellipse(d, cx, cy, rx, ry, STONE)
        if rng.random() < 0.35:
            w_ellipse(d, cx + 0.6, cy + 0.8, max(1, rx - 1), max(1, ry - 1), DIRT_DK)

    # 짧은 풀 획 (세로 기미, 프랍 아님)
    for _ in range(420):
        x = rng.uniform(0, W)
        y = rng.uniform(0, H)
        dx = rng.randint(-3, 3)
        dy = -rng.randint(6, 16)
        col = GRASS_LT if rng.random() < 0.5 else HILL
        w_line(d, x, y, x + dx, y + dy, col, width=1)

    # 얇은 흙 결/금 (OUTLINE, 프레임·고리 아님)
    for _ in range(70):
        x = rng.uniform(0, W)
        y = rng.uniform(0, H)
        dx = rng.randint(4, 18)
        dy = rng.randint(-4, 4)
        w_line(d, x, y, x + dx, y + dy, OUTLINE if rng.random() < 0.4 else DIRT_DK, width=1)

    out = HERE / "out_oxalpha_field_plain_albedo.png"
    im.save(out, optimize=True)
    print(f"→ {out.name}  {W}×{H}  {im.mode}  {out.stat().st_size}B")
    return im, out


if __name__ == "__main__":
    make()
