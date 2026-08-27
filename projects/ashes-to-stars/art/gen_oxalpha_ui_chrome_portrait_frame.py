#!/usr/bin/env python3
"""ox-alpha UI 크롬 portrait_frame 코드합성 — 765×762 RGBA 9-slice.

배경: UiAtlas 가 ui/chrome/portrait_frame 을 초상에 DrawFit / DrawRosterFrame
(DrawSliced, ChromeSlice["portrait_frame"]=0.32) 로 깐다. 짧은 변의 32%가
코너(765×762 → ≈244px). 나노바나나 765×762 금 안쪽 테 + 남색 석 몸체 +
가운데 투명 구멍이 고딕·할로우로 읽히고, 이미 ox-alpha 인 panel·바닥과
톤이 따로 논다. 같은 웜톤 납작 팔레트의 나무 프레임으로 교체한다. 256 프랍
파이프라인 강제 아님 — 해상도 765×762 RGBA 유지.
.cs/.meta/GUID 무변경 (GUID b5aa608d097342bd8f844c17c2bba51f).

디자인: 바깥 32% 는 WOOD 프레임 띠 + OUTLINE. 네 코너는 같은 사각 석 블록
(아칸서스·필리그리 없음, panel 0.24보다 코너가 두껍다). 변은 늘려도 장식이
번지지 않게 축에 균일. 안쪽 GOLD 는 납작 1–3px 사각형(비즈 3D 금속 아님).
가운데 HOLE 완전 투명(알파 0) — 초상이 비치게. panel 과 다름: 구멍 vs
불투명 FILL, 코너 0.32 vs 0.24. § 고딕 필리그리·남색 할로우·메탈 블룸·
회로/룬·할로우나이트 없음.

9-slice: 변 장식은 늘리는 축으로 균일(상하=가로띠, 좌우=세로띠). 좌=우, 상=하.

사용: python3 gen_oxalpha_ui_chrome_portrait_frame.py
출력: art/out_oxalpha_ui_chrome_portrait_frame.png (765×762 RGBA)
"""
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
W, H = 765, 762
SLICE_FRAC = 0.32
SLICE = int(min(W, H) * SLICE_FRAC)  # 243  (762×0.32 ≈ 243.84)

OUTLINE = (40, 24, 20)
WOOD = (150, 104, 58)
GOLD = (216, 172, 96)
STONE = (132, 128, 122)
STONE_DK = (86, 82, 76)

PALETTE = {OUTLINE, WOOD, GOLD, STONE, STONE_DK}
TRANSPARENT = (0, 0, 0, 0)


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


def corner_block(d, x, y, s=108):
    """네 코너 동일. 사각 석 블록 — 필리그리 아님. panel 보다 큼."""
    d.rectangle([x, y, x + s - 1, y + s - 1], fill=_rgba(OUTLINE))
    d.rectangle([x + 8, y + 8, x + s - 9, y + s - 9], fill=_rgba(STONE_DK))
    d.rectangle([x + 20, y + 20, x + s - 21, y + s - 21], fill=_rgba(STONE))


def make():
    im = Image.new("RGBA", (W, H), TRANSPARENT)
    d = ImageDraw.Draw(im)

    # 바깥→안. 합 238 < 243 이라 GOLD 가 코너/변 슬라이스 안에 남는다.
    # 238..242 는 투명이라 슬라이스 이음에서 금테가 잘리지 않는다.
    # panel 대비 WOOD 띠가 두껍고 WALL/FILL 없음 — 가운데는 구멍.
    bands = [
        (14, OUTLINE),
        (8, STONE_DK),
        (8, STONE),
        (197, WOOD),
        (8, STONE_DK),
        (3, GOLD),
    ]
    inset = 0
    for thick, col in bands:
        ring(d, inset, thick, _rgba(col))
        inset += thick
    # 가운데 구멍 — 채우지 않음 (이미 TRANSPARENT).

    # 코너 사각 석 블록. WOOD 띠 안(inset 34), 한 변 108 → 142 < 243.
    s = 108
    o = 34
    corner_block(d, o, o, s)
    corner_block(d, W - o - s, o, s)
    corner_block(d, o, H - o - s, s)
    corner_block(d, W - o - s, H - o - s, s)

    _selfcheck(im)

    out = HERE / "out_oxalpha_ui_chrome_portrait_frame.png"
    im.save(out, optimize=True)
    print(f"→ {out.name}  {W}×{H}  {im.mode}  {out.stat().st_size}B")
    return im, out


def _selfcheck(im):
    assert im.size == (W, H) and im.mode == "RGBA"
    px = im.load()
    for y in range(H):
        for x in range(W):
            r, g, b, a = px[x, y]
            assert a in (0, 255), (x, y, a)
            if a == 0:
                assert (r, g, b, a) == TRANSPARENT, (x, y, (r, g, b, a))
            else:
                assert (r, g, b) in PALETTE, (x, y, (r, g, b))

    # 좌=우, 상=하 — 9-slice 가 한쪽으로 안 기울게
    lr = im.transpose(Image.FLIP_LEFT_RIGHT)
    tb = im.transpose(Image.FLIP_TOP_BOTTOM)
    assert im.tobytes() == lr.tobytes(), "left-right asymmetry"
    assert im.tobytes() == tb.tobytes(), "top-bottom asymmetry"

    # 변은 늘리는 축으로 균일 (코너 243px 밖)
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

    # 가운데는 완전 투명 구멍. 바깥 프레임은 불투명.
    cx, cy = W // 2, H // 2
    assert px[cx, cy] == TRANSPARENT
    assert px[cx, 0] == _rgba(OUTLINE)
    assert px[0, cy] == _rgba(OUTLINE)
    # GOLD 가 슬라이스 안쪽에 있다
    gold_y = 14 + 8 + 8 + 197 + 8  # 235
    assert px[cx, gold_y] == _rgba(GOLD)
    assert gold_y + 2 < SLICE
    # 구멍은 금테 안쪽부터
    assert px[cx, gold_y + 3] == TRANSPARENT


if __name__ == "__main__":
    make()
