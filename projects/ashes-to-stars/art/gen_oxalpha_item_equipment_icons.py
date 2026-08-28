#!/usr/bin/env python3
"""실제 장비 화면이 소비하는 6부위 아이콘을 작은 크기용 ox-alpha 픽토그램으로 통일한다."""
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

HERE = Path(__file__).resolve().parent
TARGET = HERE.parent / "unity/Assets/Resources/ui/item_atlas.png"
BACKUP = HERE / "bak_higgsfield_item_atlas.png"

T = (0, 0, 0, 0)
INK = (39, 27, 27, 255)
SHADOW = (70, 48, 39, 255)
GOLD = (213, 164, 78, 255)
CREAM = (242, 220, 166, 255)
AMBER = (230, 126, 57, 255)
RED = (179, 60, 48, 255)
SLATE = (42, 57, 68, 255)
PALETTE = {T, INK, SHADOW, GOLD, CREAM, AMBER, RED, SLATE}
CELLS = {
    "sword": (0, 0), "helmet": (0, 1), "armor": (1, 1),
    "gloves": (2, 1), "boots": (3, 1), "amulet": (1, 2),
}


def bounds(index):
    cell = 1254 / 4
    return round(index * cell), round((index + 1) * cell)


def badge():
    im = Image.new("RGBA", (256, 256), T)
    d = ImageDraw.Draw(im)
    d.ellipse((20, 20, 235, 235), fill=INK)
    d.ellipse((28, 28, 227, 227), fill=GOLD)
    d.ellipse((37, 37, 218, 218), fill=SHADOW)
    d.ellipse((44, 44, 211, 211), fill=SLATE)
    return im, d


def sword(d):
    d.polygon(((151, 55), (181, 45), (171, 76), (111, 155), (94, 138)), fill=CREAM)
    d.polygon(((102, 145), (120, 163), (99, 180), (84, 164)), fill=GOLD)
    d.line((93, 170, 70, 193), fill=AMBER, width=18)
    d.ellipse((59, 184, 79, 204), fill=RED)


def helmet(d):
    d.pieslice((69, 55, 187, 177), 180, 360, fill=CREAM)
    d.rectangle((69, 113, 187, 153), fill=CREAM)
    d.polygon(((69, 127), (100, 145), (100, 190), (78, 174)), fill=GOLD)
    d.polygon(((187, 127), (156, 145), (156, 190), (178, 174)), fill=GOLD)
    d.polygon(((115, 56), (128, 40), (141, 56), (128, 117)), fill=AMBER)
    d.rectangle((102, 139, 154, 153), fill=INK)


def armor(d):
    d.polygon(((92, 65), (116, 53), (140, 53), (164, 65), (192, 104),
               (169, 126), (161, 194), (95, 194), (87, 126), (64, 104)), fill=CREAM)
    d.polygon(((92, 65), (116, 82), (140, 82), (164, 65), (155, 181),
               (101, 181)), fill=RED)
    d.polygon(((128, 91), (143, 117), (128, 147), (113, 117)), fill=GOLD)


def gloves(d):
    d.polygon(((73, 91), (91, 71), (105, 82), (111, 59), (128, 64),
               (128, 146), (107, 190), (76, 177)), fill=CREAM)
    d.polygon(((183, 91), (165, 71), (151, 82), (145, 59), (128, 64),
               (128, 146), (149, 190), (180, 177)), fill=GOLD)
    d.line((81, 151, 116, 165), fill=AMBER, width=9)
    d.line((175, 151, 140, 165), fill=AMBER, width=9)


def boots(d):
    d.polygon(((79, 57), (123, 57), (116, 143), (98, 172), (65, 174),
               (57, 154), (85, 132)), fill=CREAM)
    d.polygon(((133, 57), (177, 57), (171, 132), (199, 154), (191, 174),
               (158, 172), (140, 143)), fill=GOLD)
    d.rectangle((75, 90, 118, 105), fill=AMBER)
    d.rectangle((138, 90, 181, 105), fill=AMBER)


def amulet(d):
    d.arc((65, 45, 191, 171), 180, 360, fill=CREAM, width=12)
    d.line((66, 108, 107, 158), fill=CREAM, width=12)
    d.line((190, 108, 149, 158), fill=CREAM, width=12)
    d.polygon(((128, 137), (158, 163), (128, 205), (98, 163)), fill=GOLD)
    d.polygon(((128, 151), (145, 166), (128, 190), (111, 166)), fill=RED)


DRAW = {"sword": sword, "helmet": helmet, "armor": armor,
        "gloves": gloves, "boots": boots, "amulet": amulet}


def make():
    current = Image.open(TARGET).convert("RGBA")
    assert current.size == (1254, 1254), current.size
    if not BACKUP.exists():
        current.save(BACKUP, optimize=True)
    source = Image.open(BACKUP).convert("RGBA")
    before = source.copy()

    for key, (col, row) in CELLS.items():
        x0, x1 = bounds(col)
        y0, y1 = bounds(row)
        icon, draw = badge()
        DRAW[key](draw)
        assert set(icon.getdata()) <= PALETTE
        assert icon.getbbox() == (20, 20, 236, 236)
        icon = icon.resize((x1 - x0, y1 - y0), Image.Resampling.NEAREST)
        source.paste(T, (x0, y0, x1, y1))
        source.paste(icon, (x0, y0), icon)

    changed = ImageChops.difference(source, before)
    for col, row in CELLS.values():
        x0, x1 = bounds(col)
        y0, y1 = bounds(row)
        changed.paste(T, (x0, y0, x1, y1))
    assert changed.getbbox() is None
    source.save(TARGET, optimize=True)
    print(f"→ {TARGET.name}  {TARGET.stat().st_size}B")


if __name__ == "__main__":
    make()
