#!/usr/bin/env python3
"""UI 아틀라스의 실제 소비 건물 표식 4종을 ox-alpha 픽토그램으로 통일한다."""
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

HERE = Path(__file__).resolve().parent
ATLAS = HERE.parent / "unity/Assets/Resources/ui/ashes_to_stars_ui_atlas.png"
OUT = HERE / "out_oxalpha_ui_atlas_building_icons.png"
RECTS = {
    "smith": (638, 570, 122, 124),
    "auction": (329, 572, 130, 121),
    "mausoleum": (793, 570, 121, 124),
    "barracks": (483, 571, 123, 119),
}

INK = (39, 27, 27, 255)
SHADOW = (70, 48, 39, 255)
GOLD = (213, 164, 78, 255)
CREAM = (242, 221, 164, 255)
BLUE = (55, 112, 151, 255)
BLUE_HI = (127, 192, 203, 255)
STONE = (151, 164, 170, 255)
PLUM = (104, 65, 116, 255)
PALETTE = {INK, SHADOW, GOLD, CREAM, BLUE, BLUE_HI, STONE, PLUM,
           (0, 0, 0, 0)}


def _badge(d, cx, cy, fill):
    d.ellipse((cx-44, cy-44, cx+44, cy+44), fill=INK)
    d.ellipse((cx-38, cy-38, cx+38, cy+38), fill=GOLD)
    d.ellipse((cx-32, cy-32, cx+32, cy+32), fill=fill)


def _smith(d, cx, cy):
    _badge(d, cx, cy, SHADOW)
    d.polygon(((cx-27, cy+12), (cx+21, cy+12), (cx+28, cy+20),
               (cx+17, cy+27), (cx-20, cy+27), (cx-29, cy+20)), fill=STONE)
    d.rectangle((cx-4, cy-23, cx+4, cy+13), fill=CREAM)
    d.rounded_rectangle((cx-22, cy-29, cx+16, cy-15), radius=4, fill=GOLD)
    d.rectangle((cx+13, cy-24, cx+25, cy-20), fill=GOLD)


def _auction(d, cx, cy):
    _badge(d, cx, cy, BLUE)
    d.polygon(((cx-25, cy+18), (cx+24, cy+18), (cx+29, cy+27),
               (cx-30, cy+27)), fill=CREAM)
    d.line((cx-17, cy-17, cx+17, cy+17), fill=INK, width=10)
    d.rounded_rectangle((cx-27, cy-29, cx-2, cy-14), radius=4, fill=GOLD)
    d.rounded_rectangle((cx+2, cy-2, cx+27, cy+13), radius=4, fill=GOLD)


def _mausoleum(d, cx, cy):
    _badge(d, cx, cy, PLUM)
    d.polygon(((cx, cy-29), (cx+27, cy-12), (cx+23, cy-5),
               (cx-23, cy-5), (cx-27, cy-12)), fill=CREAM)
    d.rectangle((cx-22, cy-5, cx+22, cy+26), fill=STONE)
    d.rectangle((cx-5, cy+4, cx+5, cy+26), fill=INK)
    d.rectangle((cx-13, cy+11, cx+13, cy+18), fill=INK)


def _barracks(d, cx, cy):
    _badge(d, cx, cy, BLUE)
    d.rectangle((cx-24, cy-13, cx+24, cy+27), fill=STONE)
    d.rectangle((cx-28, cy-22, cx-15, cy-8), fill=STONE)
    d.rectangle((cx-6, cy-22, cx+6, cy-8), fill=STONE)
    d.rectangle((cx+15, cy-22, cx+28, cy-8), fill=STONE)
    d.rounded_rectangle((cx-7, cy+7, cx+7, cy+27), radius=5, fill=INK)
    d.rectangle((cx-3, cy-30, cx+3, cy+4), fill=CREAM)
    d.polygon(((cx+3, cy-29), (cx+25, cy-22), (cx+3, cy-14)), fill=GOLD)


DRAW = {"smith": _smith, "auction": _auction,
        "mausoleum": _mausoleum, "barracks": _barracks}


def make():
    atlas = Image.open(ATLAS).convert("RGBA")
    assert atlas.size == (1448, 1086)
    before = atlas.copy()
    centers = []
    for name, (x, y, w, h) in RECTS.items():
        tile = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        draw = ImageDraw.Draw(tile)
        cx, cy = w // 2, 62 - (y - 570)
        DRAW[name](draw, cx, cy)
        assert set(tile.getdata()) <= PALETTE
        assert tile.getbbox() is not None
        atlas.paste(tile, (x, y))
        centers.append(y + cy)
    assert len(set(centers)) == 1

    mask = Image.new("1", atlas.size)
    md = ImageDraw.Draw(mask)
    for x, y, w, h in RECTS.values():
        md.rectangle((x, y, x+w-1, y+h-1), fill=1)
    changed = ImageChops.difference(before, atlas)
    outside = Image.new("RGBA", atlas.size)
    outside.paste(changed, mask=Image.eval(mask, lambda p: 0 if p else 255))
    assert outside.getbbox() is None
    atlas.save(OUT, optimize=True)
    print(f"→ {OUT.name}  {atlas.width}x{atlas.height}  {OUT.stat().st_size}B")


if __name__ == "__main__":
    make()
