#!/usr/bin/env python3
"""UI 아틀라스 재화·탐험 표식 8종을 ox-alpha 픽토그램으로 통일한다."""
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

HERE = Path(__file__).resolve().parent
ATLAS = HERE.parent / "unity/Assets/Resources/ui/ashes_to_stars_ui_atlas.png"
OUT = HERE / "out_oxalpha_ui_atlas_resource_icons.png"
RECTS = {
    "copper": (0, 455, 130, 115), "silver": (130, 455, 125, 115),
    "gold": (255, 455, 140, 115), "crystal": (395, 455, 130, 115),
    "herb": (525, 455, 130, 115), "astral": (655, 455, 130, 115),
    "scroll": (785, 455, 140, 115), "compass": (925, 455, 165, 115),
}

INK = (39, 27, 27, 255)
SHADOW = (70, 48, 39, 255)
COPPER = (154, 82, 51, 255)
SILVER = (151, 164, 170, 255)
GOLD = (213, 164, 78, 255)
CREAM = (242, 221, 164, 255)
BLUE = (55, 112, 151, 255)
BLUE_HI = (127, 192, 203, 255)
GREEN = (71, 119, 73, 255)
PLUM = (104, 65, 116, 255)
PALETTE = {INK, SHADOW, COPPER, SILVER, GOLD, CREAM, BLUE, BLUE_HI,
           GREEN, PLUM, (0, 0, 0, 0)}


def _badge(d, cx, cy, fill):
    d.ellipse((cx-44, cy-44, cx+44, cy+44), fill=INK)
    d.ellipse((cx-38, cy-38, cx+38, cy+38), fill=GOLD)
    d.ellipse((cx-32, cy-32, cx+32, cy+32), fill=fill)


def _coin(d, cx, cy, fill):
    _badge(d, cx, cy, fill)
    d.polygon(((cx, cy-23), (cx+7, cy-7), (cx+23, cy),
               (cx+7, cy+7), (cx, cy+23), (cx-7, cy+7),
               (cx-23, cy), (cx-7, cy-7)), fill=CREAM)
    d.ellipse((cx-5, cy-5, cx+5, cy+5), fill=INK)


def _crystal(d, cx, cy):
    _badge(d, cx, cy, SHADOW)
    d.polygon(((cx, cy-30), (cx+22, cy-8), (cx+14, cy+27),
               (cx-17, cy+24), (cx-26, cy-5)), fill=BLUE)
    d.polygon(((cx, cy-25), (cx+4, cy-6), (cx-3, cy+20),
               (cx-14, cy+17), (cx-19, cy-4)), fill=BLUE_HI)


def _herb(d, cx, cy):
    _badge(d, cx, cy, SHADOW)
    d.line((cx, cy+28, cx, cy-13), fill=CREAM, width=6)
    d.ellipse((cx-27, cy-20, cx+1, cy+3), fill=GREEN)
    d.ellipse((cx-1, cy-31, cx+27, cy-5), fill=GREEN)
    d.ellipse((cx-22, cy+1, cx+1, cy+23), fill=GREEN)
    d.ellipse((cx-5, cy-8, cx+20, cy+15), fill=CREAM)


def _astral(d, cx, cy):
    _badge(d, cx, cy, PLUM)
    d.ellipse((cx-30, cy-13, cx+30, cy+13), outline=BLUE_HI, width=6)
    d.polygon(((cx, cy-27), (cx+7, cy-7), (cx+27, cy),
               (cx+7, cy+7), (cx, cy+27), (cx-7, cy+7),
               (cx-27, cy), (cx-7, cy-7)), fill=CREAM)
    d.ellipse((cx+22, cy-5, cx+32, cy+5), fill=GOLD)


def _scroll(d, cx, cy):
    _badge(d, cx, cy, BLUE)
    d.rounded_rectangle((cx-25, cy-27, cx+24, cy+27), radius=8, fill=CREAM)
    d.ellipse((cx-31, cy-28, cx-13, cy-11), fill=GOLD)
    d.ellipse((cx+13, cy+11, cx+31, cy+28), fill=GOLD)
    d.line((cx-14, cy-7, cx+14, cy-7), fill=INK, width=5)
    d.line((cx-14, cy+5, cx+8, cy+5), fill=SHADOW, width=5)


def _compass(d, cx, cy):
    _badge(d, cx, cy, SHADOW)
    d.ellipse((cx-27, cy-27, cx+27, cy+27), outline=CREAM, width=5)
    d.polygon(((cx, cy-31), (cx+10, cy-5), (cx+31, cy),
               (cx+10, cy+5), (cx, cy+31), (cx-10, cy+5),
               (cx-31, cy), (cx-10, cy-5)), fill=GOLD)
    d.polygon(((cx, cy-24), (cx+6, cy), (cx, cy+5), (cx-6, cy)), fill=CREAM)
    d.ellipse((cx-5, cy-5, cx+5, cy+5), fill=INK)


def make():
    atlas = Image.open(ATLAS).convert("RGBA")
    assert atlas.size == (1448, 1086)
    before = atlas.copy()
    centers = []
    for name, (x, y, w, h) in RECTS.items():
        tile = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        draw = ImageDraw.Draw(tile)
        cx, cy = w // 2, 57
        if name in ("copper", "silver", "gold"):
            _coin(draw, cx, cy, {"copper": COPPER, "silver": SILVER, "gold": GOLD}[name])
        else:
            {"crystal": _crystal, "herb": _herb, "astral": _astral,
             "scroll": _scroll, "compass": _compass}[name](draw, cx, cy)
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
