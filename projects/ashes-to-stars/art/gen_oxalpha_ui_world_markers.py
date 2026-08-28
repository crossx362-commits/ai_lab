#!/usr/bin/env python3
"""UI 아틀라스 y=690의 월드·전투 표식 8종을 ox-alpha 픽토그램으로 통일한다."""
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

HERE = Path(__file__).resolve().parent
ATLAS = HERE.parent / "unity/Assets/Resources/ui/ashes_to_stars_ui_atlas.png"
OUT = HERE / "out_oxalpha_ui_atlas_world_markers.png"
RECTS = {
    "waypoint": (8, 690, 137, 110),
    "ice_star": (145, 690, 129, 110),
    "danger": (274, 690, 145, 110),
    "void_star": (419, 690, 129, 110),
    "galaxy": (548, 690, 156, 110),
    "nebula": (704, 690, 130, 110),
    "assault": (834, 690, 130, 110),
    "defense": (964, 690, 126, 110),
}

INK = (39, 27, 27, 255)
SHADOW = (70, 48, 39, 255)
GOLD = (213, 164, 78, 255)
CREAM = (242, 221, 164, 255)
BLUE = (55, 112, 151, 255)
BLUE_HI = (127, 192, 203, 255)
RED = (170, 55, 49, 255)
PLUM = (104, 65, 116, 255)
PALETTE = {INK, SHADOW, GOLD, CREAM, BLUE, BLUE_HI, RED, PLUM, (0, 0, 0, 0)}


def badge(d, cx, cy, fill):
    d.ellipse((cx-44, cy-44, cx+44, cy+44), fill=INK)
    d.ellipse((cx-38, cy-38, cx+38, cy+38), fill=GOLD)
    d.ellipse((cx-32, cy-32, cx+32, cy+32), fill=fill)


def star(d, cx, cy, fill, inner=8, outer=27):
    points = []
    for i in range(16):
        import math
        radius = outer if i % 2 == 0 else inner
        angle = math.radians(i * 22.5 - 90)
        points.append((cx + round(math.cos(angle) * radius),
                       cy + round(math.sin(angle) * radius)))
    d.polygon(points, fill=fill)


def draw(name, d, cx, cy):
    fill = RED if name in {"danger", "assault"} else BLUE if name in {"ice_star", "nebula", "defense"} else PLUM
    badge(d, cx, cy, fill)
    if name == "waypoint":
        star(d, cx, cy, CREAM, 6, 28)
        d.ellipse((cx-5, cy-5, cx+5, cy+5), fill=BLUE)
    elif name == "ice_star":
        d.line((cx-25, cy, cx+25, cy), fill=CREAM, width=7)
        d.line((cx, cy-25, cx, cy+25), fill=CREAM, width=7)
        d.line((cx-18, cy-18, cx+18, cy+18), fill=BLUE_HI, width=5)
        d.line((cx+18, cy-18, cx-18, cy+18), fill=BLUE_HI, width=5)
    elif name == "danger":
        star(d, cx, cy, CREAM, 13, 29)
        d.ellipse((cx-7, cy-7, cx+7, cy+7), fill=INK)
    elif name == "void_star":
        star(d, cx, cy, CREAM, 9, 27)
        d.ellipse((cx-12, cy-12, cx+12, cy+12), fill=INK)
    elif name == "galaxy":
        d.arc((cx-28, cy-17, cx+20, cy+17), 195, 530, fill=CREAM, width=8)
        d.arc((cx-20, cy-27, cx+28, cy+27), 15, 320, fill=BLUE_HI, width=7)
        d.ellipse((cx-5, cy-5, cx+5, cy+5), fill=GOLD)
    elif name == "nebula":
        d.ellipse((cx-28, cy-15, cx+28, cy+15), fill=BLUE_HI)
        d.ellipse((cx-18, cy-25, cx+18, cy+25), fill=BLUE)
        star(d, cx, cy, CREAM, 5, 17)
    elif name == "assault":
        d.polygon(((cx-7, cy-29), (cx+8, cy-29), (cx+8, cy+13),
                   (cx+19, cy+20), (cx, cy+30), (cx-19, cy+20), (cx-7, cy+13)), fill=CREAM)
        d.rectangle((cx-22, cy+14, cx+22, cy+22), fill=INK)
    else:
        d.polygon(((cx, cy-29), (cx+26, cy-20), (cx+22, cy+11),
                   (cx, cy+29), (cx-22, cy+11), (cx-26, cy-20)), fill=CREAM)
        d.polygon(((cx, cy-20), (cx+16, cy-14), (cx+13, cy+6),
                   (cx, cy+18), (cx-13, cy+6), (cx-16, cy-14)), fill=BLUE)


def make():
    atlas = Image.open(ATLAS).convert("RGBA")
    assert atlas.size == (1448, 1086)
    before = atlas.copy()
    centers = []
    for name, (x, y, w, h) in RECTS.items():
        tile = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        cx, cy = w // 2, 55
        draw(name, ImageDraw.Draw(tile), cx, cy)
        assert set(tile.getdata()) <= PALETTE
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
