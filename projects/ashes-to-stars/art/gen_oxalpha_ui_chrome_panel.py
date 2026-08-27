#!/usr/bin/env python3
"""ox-alpha UI 크롬 panel 코드합성 — 868×870 RGBA 9-slice.

배경: UiAtlas 가 ui/chrome/panel 을 DrawSliced 로 깐다. ChromeSlice["panel"]=0.24
이라 짧은 변의 24%가 코너(868×870 → ≈208px). 나노바나나 868×870 금 아칸서스
필리그리 코너 + 비즈 금테 + 남색 얼룩 채움이라 고딕·할로우로 읽히고, 이미
ox-alpha 인 바닥·배경과 톤이 따로 논다. 같은 웜톤 납작 팔레트의 나무 프레임
패널로 교체한다. 256 프랍 파이프라인 강제 아님 — 해상도 868×870 RGBA 유지.
.cs/.meta/GUID 무변경 (GUID 96f94a7ba7684211937a0cce08b08861).

디자인: 바깥 24% 는 WOOD 프레임 띠 + OUTLINE. 네 코너는 같은 사각 블록
(아칸서스·필리그리 없음). 변은 늘려도 장식이 번지지 않게 축에 균일. 안쪽 GOLD 는
납작 1–3px 사각형(비즈 3D 금속 아님). 가운데 FILL 불투명(알파 255) —
글씨 밑이라 결·고리 없음. § 고딕 필리그리·남색 할로우·메탈 블룸·할로우나이트 없음.

9-slice: 변 장식은 늘리는 축으로 균일(상하=가로띠, 좌우=세로띠). 좌=우, 상=하.

사용: python3 gen_oxalpha_ui_chrome_panel.py
출력: art/out_oxalpha_ui_chrome_panel.png (868×870 RGBA)
"""
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
W, H = 868, 870
SLICE_FRAC = 0.24
SLICE = int(min(W, H) * SLICE_FRAC)  # 208

OUTLINE = (40, 24, 20)
WOOD = (150, 104, 58)
GOLD = (216, 172, 96)
STONE = (132, 128, 122)
STONE_DK = (86, 82, 76)
WALL = (96, 94, 90)
FILL = (48, 42, 36)

PALETTE = {OUTLINE, WOOD, GOLD, STONE, STONE_DK, WALL, FILL}


def _rgba(c):
    return (*c, 255)


def ring(d, inset, thickness, color):
    """축에 균일한 사각 고리. thickness px, 바깥 가장자리가 inset."""
    if thickness < 1:
        return
    x0, y0 = inset, inset
    x1, y1 = W - 1 - inset, H - 1 - inset
    if x1 < x0 or y1 < y0:
        return
    t = thickness
    d.rectangle([x0, y0, x1, y0 + t - 1], fill=color)
    d.rectangle([x0, y1 - t + 1, x1, y1], fill=color)
    d.rectangle([x0, y0, x0 + t - 1, y1], fill=color)
    d.rectangle([x1 - t + 1, y0, x1, y1], fill=color)


def corner_block(d, x, y, s=84):
    """네 코너 동일. 사각 석+나무 블록 — 필리그리 아님."""
    d.rectangle([x, y, x + s - 1, y + s - 1], fill=_rgba(OUTLINE))
    d.rectangle([x + 6, y + 6, x + s - 7, y + s - 7], fill=_rgba(STONE))
    d.rectangle([x + 16, y + 16, x + s - 17, y + s - 17], fill=_rgba(WOOD))


def make():
    im = Image.new("RGBA", (W, H), _rgba(FILL))
    d = ImageDraw.Draw(im)

    # 바깥→안. 합 203 < 208 이라 GOLD·안쪽 OUTLINE 이 코너/변 슬라이스 안에 남는다.
    # 203..207 은 FILL 이라 슬라이스 이음에서 금테가 잘리지 않는다.
    bands = [
        (12, OUTLINE),
        (6, STONE_DK),
        (8, WALL),
        (162, WOOD),
        (6, STONE_DK),
        (3, GOLD),
        (6, OUTLINE),
    ]
    inset = 0
    for thick, col in bands:
        ring(d, inset, thick, _rgba(col))
        inset += thick
    d.rectangle([inset, inset, W - 1 - inset, H - 1 - inset], fill=_rgba(FILL))

    # 코너 사각 블록. WOOD 띠 안(inset 28), 한 변 84 → 112 < 208.
    s = 84
    o = 28
    corner_block(d, o, o, s)
    corner_block(d, W - o - s, o, s)
    corner_block(d, o, H - o - s, s)
    corner_block(d, W - o - s, H - o - s, s)

    _selfcheck(im)

    out = HERE / "out_oxalpha_ui_chrome_panel.png"
    im.save(out, optimize=True)
    print(f"→ {out.name}  {W}×{H}  {im.mode}  {out.stat().st_size}B")
    return im, out


def _selfcheck(im):
    assert im.size == (W, H) and im.mode == "RGBA"
    px = im.load()
    for y in range(H):
        for x in range(W):
            r, g, b, a = px[x, y]
            assert a == 255, (x, y, a)
            assert (r, g, b) in PALETTE, (x, y, (r, g, b))

    # 좌=우, 상=하 — 9-slice 가 한쪽으로 안 기울게
    lr = im.transpose(Image.FLIP_LEFT_RIGHT)
    tb = im.transpose(Image.FLIP_TOP_BOTTOM)
    assert im.tobytes() == lr.tobytes(), "left-right asymmetry"
    assert im.tobytes() == tb.tobytes(), "top-bottom asymmetry"

    # 변은 늘리는 축으로 균일 (코너 208px 밖)
    for y in range(SLICE):
        c0 = px[SLICE, y]
        for x in range(SLICE, W - SLICE):
            assert px[x, y] == c0, ("top-edge smear", x, y)
    for y in range(H - SLICE, H):
        c0 = px[SLICE, y]
        for x in range(SLICE, W - SLICE):
            assert px[x, y] == c0, ("bot-edge smear", x, y)
    for x in range(SLICE):
        c0 = px[x, SLICE]
        for y in range(SLICE, H - SLICE):
            assert px[x, y] == c0, ("left-edge smear", x, y)
    for x in range(W - SLICE, W):
        c0 = px[x, SLICE]
        for y in range(SLICE, H - SLICE):
            assert px[x, y] == c0, ("right-edge smear", x, y)

    # 가운데는 납작 FILL
    cx, cy = W // 2, H // 2
    assert px[cx, cy] == _rgba(FILL)
    # GOLD 가 슬라이스 안쪽에 있다
    gold_y = 12 + 6 + 8 + 162 + 6  # 194
    assert px[cx, gold_y] == _rgba(GOLD)
    assert gold_y + 2 < SLICE


if __name__ == "__main__":
    make()
