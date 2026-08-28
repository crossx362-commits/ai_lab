#!/usr/bin/env python3
"""ox-alpha 보스 HP 크롬 — 1194x206 RGBA 가로 9-slice 프레임.

기존 나노바나나 원본의 양끝 검은 잔상과 비대칭 문양을 없애고, 채택된 panel과
같은 납작 웜톤 팔레트로 맞춘다. UiAtlas.ChromeSlice=0.18이므로 37px 안에
모든 코너 장식을 가두고 가운데 늘림 구간은 축 방향으로 완전히 균일하게 만든다.
"""
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
W, H = 1194, 206
SLICE = int(min(W, H) * 0.18)  # 37

OUTLINE = (40, 24, 20, 255)
WOOD_DK = (86, 54, 34, 255)
WOOD = (150, 104, 58, 255)
GOLD = (216, 172, 96, 255)
FILL = (35, 31, 29, 255)
PALETTE = {OUTLINE, WOOD_DK, WOOD, GOLD, FILL}


def ring(d, inset, thick, color):
    d.rectangle((inset, inset, W - 1 - inset, inset + thick - 1), fill=color)
    d.rectangle((inset, H - inset - thick, W - 1 - inset, H - 1 - inset), fill=color)
    d.rectangle((inset, inset, inset + thick - 1, H - 1 - inset), fill=color)
    d.rectangle((W - inset - thick, inset, W - 1 - inset, H - 1 - inset), fill=color)


def make():
    im = Image.new("RGBA", (W, H), FILL)
    d = ImageDraw.Draw(im)
    inset = 0
    for thick, color in ((5, OUTLINE), (8, WOOD_DK), (12, WOOD),
                         (3, GOLD), (5, WOOD_DK), (4, OUTLINE)):
        ring(d, inset, thick, color)
        inset += thick
    d.rectangle((inset, inset, W - 1 - inset, H - 1 - inset), fill=FILL)

    # 보스 전용 위험 표식: 코너 슬라이스 안의 좌우·상하 대칭 금색 봉인.
    d.rectangle((8, 78, 20, 127), fill=GOLD)
    d.rectangle((12, 87, 16, 118), fill=OUTLINE)
    d.rectangle((W - 21, 78, W - 9, 127), fill=GOLD)
    d.rectangle((W - 17, 87, W - 13, 118), fill=OUTLINE)

    _selfcheck(im)
    out = HERE / "out_oxalpha_ui_chrome_boss_hp_frame.png"
    im.save(out, optimize=True)
    print(f"→ {out.name}  {W}x{H}  {im.mode}  {out.stat().st_size}B")
    return im, out


def _selfcheck(im):
    assert im.size == (W, H) and im.mode == "RGBA"
    assert set(im.getdata()) <= PALETTE
    assert im.tobytes() == im.transpose(Image.Transpose.FLIP_LEFT_RIGHT).tobytes()
    assert im.tobytes() == im.transpose(Image.Transpose.FLIP_TOP_BOTTOM).tobytes()
    px = im.load()
    for y in range(H):
        sample = px[SLICE, y]
        assert all(px[x, y] == sample for x in range(SLICE, W - SLICE))
    assert px[W // 2, H // 2] == FILL


if __name__ == "__main__":
    make()
