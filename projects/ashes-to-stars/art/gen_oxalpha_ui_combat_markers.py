#!/usr/bin/env python3
"""UI 아틀라스 전투 표식 둘째 행 8종을 ox-alpha 픽토그램으로 통일한다."""
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

HERE = Path(__file__).resolve().parent
ATLAS = HERE.parent / "unity/Assets/Resources/ui/ashes_to_stars_ui_atlas.png"
OUT = HERE / "out_oxalpha_ui_atlas_combat_markers.png"
RECTS = {
    "target": (0, 330, 130, 125), "focus": (130, 330, 120, 125),
    "danger": (250, 330, 145, 125), "haste": (395, 330, 130, 125),
    "guard": (525, 330, 130, 125), "cooldown": (655, 330, 130, 125),
    "elite": (785, 330, 140, 125), "rage": (925, 330, 165, 125),
}

INK = (39, 27, 27, 255)
SHADOW = (70, 48, 39, 255)
GOLD = (213, 164, 78, 255)
CREAM = (242, 221, 164, 255)
RED = (177, 48, 51, 255)
RED_HI = (235, 89, 73, 255)
BLUE = (55, 112, 151, 255)
BLUE_HI = (127, 192, 203, 255)
PLUM = (104, 65, 116, 255)
PALETTE = {INK, SHADOW, GOLD, CREAM, RED, RED_HI, BLUE, BLUE_HI,
           PLUM, (0, 0, 0, 0)}


def _badge(d, cx, cy, fill=SHADOW):
    d.ellipse((cx-48, cy-48, cx+48, cy+48), fill=INK)
    d.ellipse((cx-41, cy-41, cx+41, cy+41), fill=GOLD)
    d.ellipse((cx-34, cy-34, cx+34, cy+34), fill=fill)


def _target(d, cx, cy):
    _badge(d, cx, cy, RED)
    for r, c in ((25, CREAM), (16, RED), (7, CREAM)):
        d.ellipse((cx-r, cy-r, cx+r, cy+r), fill=c)
    d.line((cx-46, cy, cx-21, cy), fill=INK, width=6)
    d.line((cx+21, cy, cx+46, cy), fill=INK, width=6)


def _focus(d, cx, cy):
    _badge(d, cx, cy, SHADOW)
    d.line((cx, cy-31, cx, cy+31), fill=CREAM, width=7)
    d.line((cx-31, cy, cx+31, cy), fill=CREAM, width=7)
    d.polygon(((cx, cy-25), (cx+8, cy-8), (cx+25, cy),
               (cx+8, cy+8), (cx, cy+25), (cx-8, cy+8),
               (cx-25, cy), (cx-8, cy-8)), fill=GOLD)


def _danger(d, cx, cy):
    _badge(d, cx, cy, RED)
    d.ellipse((cx-22, cy-25, cx+22, cy+17), fill=INK)
    d.ellipse((cx-14, cy-17, cx+14, cy+10), fill=CREAM)
    d.ellipse((cx-10, cy-8, cx-2, cy+1), fill=INK)
    d.ellipse((cx+2, cy-8, cx+10, cy+1), fill=INK)
    d.rectangle((cx-13, cy+14, cx+13, cy+23), fill=INK)


def _haste(d, cx, cy):
    _badge(d, cx, cy, SHADOW)
    for oy in (-18, 0, 18):
        d.polygon(((cx-25, cy+oy-9), (cx+4, cy+oy-9),
                   (cx+21, cy+oy), (cx+4, cy+oy+9),
                   (cx-25, cy+oy+9), (cx-9, cy+oy)), fill=GOLD)


def _guard(d, cx, cy):
    _badge(d, cx, cy, BLUE)
    d.polygon(((cx, cy-30), (cx+28, cy-19), (cx+23, cy+16),
               (cx, cy+33), (cx-23, cy+16), (cx-28, cy-19)), fill=INK)
    d.polygon(((cx, cy-22), (cx+19, cy-14), (cx+15, cy+11),
               (cx, cy+23), (cx-15, cy+11), (cx-19, cy-14)), fill=BLUE_HI)


def _cooldown(d, cx, cy):
    _badge(d, cx, cy, BLUE)
    d.polygon(((cx-17, cy-27), (cx+17, cy-27), (cx+10, cy-13),
               (cx+3, cy-5), (cx+3, cy+5), (cx+18, cy+27),
               (cx-18, cy+27), (cx-3, cy+5), (cx-3, cy-5),
               (cx-10, cy-13)), fill=CREAM)
    d.rectangle((cx-21, cy-32, cx+21, cy-26), fill=INK)
    d.rectangle((cx-21, cy+26, cx+21, cy+32), fill=INK)


def _elite(d, cx, cy):
    _badge(d, cx, cy, PLUM)
    d.polygon(((cx, cy-28), (cx+9, cy-11), (cx+29, cy-8),
               (cx+15, cy+7), (cx+19, cy+28), (cx, cy+18),
               (cx-19, cy+28), (cx-15, cy+7), (cx-29, cy-8),
               (cx-9, cy-11)), fill=CREAM)
    d.ellipse((cx-7, cy-7, cx+7, cy+7), fill=GOLD)


def _rage(d, cx, cy):
    _badge(d, cx, cy, RED)
    d.polygon(((cx, cy-31), (cx+10, cy-12), (cx+22, cy-22),
               (cx+29, cy+5), (cx+15, cy+29), (cx, cy+22),
               (cx-15, cy+29), (cx-29, cy+5), (cx-18, cy-18),
               (cx-8, cy-5)), fill=INK)
    d.polygon(((cx, cy-21), (cx+7, cy-6), (cx+15, cy-13),
               (cx+20, cy+6), (cx+9, cy+19), (cx, cy+13),
               (cx-9, cy+19), (cx-19, cy+5), (cx-11, cy-8)), fill=RED_HI)
    d.polygon(((cx, cy-5), (cx+7, cy+8), (cx, cy+15),
               (cx-7, cy+8)), fill=GOLD)


DRAW = {"target": _target, "focus": _focus, "danger": _danger,
        "haste": _haste, "guard": _guard, "cooldown": _cooldown,
        "elite": _elite, "rage": _rage}


def make():
    atlas = Image.open(ATLAS).convert("RGBA")
    assert atlas.size == (1448, 1086)
    before = atlas.copy()
    centers = []
    for name, (x, y, w, h) in RECTS.items():
        tile = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        draw = ImageDraw.Draw(tile)
        cx, cy = w // 2, 62
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
