#!/usr/bin/env python3
"""UI 아틀라스 상태·전투 표식 첫 행 8종을 ox-alpha 픽토그램으로 통일한다."""
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

HERE = Path(__file__).resolve().parent
ATLAS = HERE.parent / "unity/Assets/Resources/ui/ashes_to_stars_ui_atlas.png"
OUT = HERE / "out_oxalpha_ui_atlas_status.png"
RECTS = {
    "heart": (8, 250, 88, 88), "heart_broken": (96, 250, 92, 88),
    "poison": (190, 248, 88, 92), "fire": (286, 248, 88, 92),
    "ice": (382, 248, 96, 92), "lightning": (482, 248, 72, 92),
    "curse": (554, 248, 104, 92), "bleed": (666, 248, 96, 92),
}

INK = (39, 27, 27, 255)
SHADOW = (70, 48, 39, 255)
GOLD = (213, 164, 78, 255)
CREAM = (242, 221, 164, 255)
RED = (177, 48, 51, 255)
RED_HI = (235, 89, 73, 255)
GREEN = (71, 126, 76, 255)
GREEN_HI = (133, 174, 92, 255)
BLUE = (55, 112, 151, 255)
BLUE_HI = (127, 192, 203, 255)
PLUM = (104, 65, 116, 255)
PALETTE = {INK, SHADOW, GOLD, CREAM, RED, RED_HI, GREEN, GREEN_HI,
           BLUE, BLUE_HI, PLUM, (0, 0, 0, 0)}


def _heart(d, cx, cy, broken=False):
    p = [(cx, cy + 31), (cx - 31, cy + 2), (cx - 29, cy - 18),
         (cx - 16, cy - 30), (cx, cy - 20), (cx + 16, cy - 30),
         (cx + 29, cy - 18), (cx + 31, cy + 2)]
    d.polygon(p, fill=INK)
    q = [(cx, cy + 23), (cx - 24, cy), (cx - 22, cy - 14),
         (cx - 13, cy - 21), (cx, cy - 12), (cx + 13, cy - 21),
         (cx + 22, cy - 14), (cx + 24, cy)]
    d.polygon(q, fill=RED)
    d.line((cx - 15, cy - 12, cx - 4, cy - 17), fill=RED_HI, width=4)
    if broken:
        d.polygon(((cx-3, cy-23), (cx+7, cy-9), (cx-2, cy+1),
                   (cx+8, cy+11), (cx, cy+27), (cx-7, cy+9),
                   (cx+1, cy), (cx-8, cy-10)), fill=INK)


def _poison(d, cx, cy):
    d.polygon(((cx, cy-34), (cx+27, cy+2), (cx+22, cy+25),
               (cx, cy+34), (cx-22, cy+25), (cx-27, cy+2)), fill=INK)
    d.polygon(((cx, cy-24), (cx+19, cy+4), (cx+15, cy+19),
               (cx, cy+26), (cx-15, cy+19), (cx-19, cy+4)), fill=GREEN)
    d.ellipse((cx-13, cy+1, cx+13, cy+23), fill=SHADOW)
    d.ellipse((cx-8, cy+5, cx-1, cy+12), fill=GREEN_HI)
    d.ellipse((cx+2, cy+5, cx+9, cy+12), fill=GREEN_HI)
    d.rectangle((cx-3, cy+14, cx+3, cy+21), fill=GREEN_HI)


def _fire(d, cx, cy):
    d.polygon(((cx, cy-36), (cx+8, cy-16), (cx+19, cy-27),
               (cx+29, cy+2), (cx+22, cy+28), (cx, cy+35),
               (cx-24, cy+26), (cx-29, cy+2), (cx-13, cy-20),
               (cx-10, cy+2)), fill=INK)
    d.polygon(((cx, cy-25), (cx+7, cy-7), (cx+15, cy-15),
               (cx+21, cy+5), (cx+13, cy+24), (cx, cy+28),
               (cx-17, cy+20), (cx-20, cy+3), (cx-8, cy-12),
               (cx-7, cy+11)), fill=RED_HI)
    d.polygon(((cx, cy-4), (cx+9, cy+8), (cx+5, cy+22),
               (cx-6, cy+19), (cx-9, cy+7)), fill=GOLD)


def _ice(d, cx, cy):
    for angle in (0, 45, 90, 135):
        import math
        a = math.radians(angle); dx, dy = int(31*math.cos(a)), int(31*math.sin(a))
        d.line((cx-dx, cy-dy, cx+dx, cy+dy), fill=INK, width=9)
        d.line((cx-dx, cy-dy, cx+dx, cy+dy), fill=BLUE_HI, width=4)
    d.ellipse((cx-8, cy-8, cx+8, cy+8), fill=CREAM)


def _lightning(d, cx, cy):
    p = ((cx+8, cy-36), (cx-20, cy+2), (cx-5, cy+2),
         (cx-13, cy+35), (cx+22, cy-8), (cx+6, cy-8))
    d.polygon(p, fill=INK)
    q = ((cx+6, cy-27), (cx-13, cy-1), (cx+1, cy-1),
         (cx-6, cy+24), (cx+15, cy-5), (cx+1, cy-5))
    d.polygon(q, fill=GOLD)


def _curse(d, cx, cy):
    d.ellipse((cx-34, cy-28, cx+34, cy+28), fill=INK)
    d.ellipse((cx-25, cy-19, cx+25, cy+19), fill=PLUM)
    d.polygon(((cx-22, cy), (cx-9, cy-11), (cx+9, cy-11),
               (cx+22, cy), (cx+9, cy+11), (cx-9, cy+11)), fill=CREAM)
    d.ellipse((cx-7, cy-7, cx+7, cy+7), fill=INK)
    d.polygon(((cx-29, cy-8), (cx-18, cy-28), (cx-12, cy-9)), fill=GOLD)
    d.polygon(((cx+29, cy-8), (cx+18, cy-28), (cx+12, cy-9)), fill=GOLD)


def _bleed(d, cx, cy):
    for ox, oy in ((-19, -14), (0, 0), (19, 14)):
        d.polygon(((cx+ox+8, cy+oy-26), (cx+ox-8, cy+oy+25),
                   (cx+ox, cy+oy+31), (cx+ox+15, cy+oy-20)), fill=INK)
        d.line((cx+ox+6, cy+oy-20, cx+ox-5, cy+oy+23), fill=RED_HI, width=5)


DRAW = {"heart": lambda d,x,y: _heart(d,x,y),
        "heart_broken": lambda d,x,y: _heart(d,x,y,True),
        "poison": _poison, "fire": _fire, "ice": _ice,
        "lightning": _lightning, "curse": _curse, "bleed": _bleed}


def make():
    atlas = Image.open(ATLAS).convert("RGBA")
    assert atlas.size == (1448, 1086)
    before = atlas.copy()
    centers = []
    for name, (x, y, w, h) in RECTS.items():
        tile = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        draw = ImageDraw.Draw(tile); cx, cy = w // 2, 46
        DRAW[name](draw, cx, cy)
        assert set(tile.getdata()) <= PALETTE
        assert tile.getbbox() is not None
        atlas.paste(tile, (x, y)); centers.append(y + cy)
    assert max(centers) - min(centers) <= 2

    mask = Image.new("1", atlas.size); md = ImageDraw.Draw(mask)
    for x, y, w, h in RECTS.values(): md.rectangle((x, y, x+w-1, y+h-1), fill=1)
    changed = ImageChops.difference(before, atlas)
    outside = Image.new("RGBA", atlas.size)
    outside.paste(changed, mask=Image.eval(mask, lambda p: 0 if p else 255))
    assert outside.getbbox() is None
    atlas.save(OUT, optimize=True)
    print(f"→ {OUT.name}  {atlas.width}x{atlas.height}  {OUT.stat().st_size}B")


if __name__ == "__main__":
    make()
