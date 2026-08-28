#!/usr/bin/env python3
"""목숨·귀환 계열 아이템 4종을 작은 크기용 ox-alpha 픽토그램으로 통일한다."""
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

HERE = Path(__file__).resolve().parent
TARGET = HERE.parent / "unity/Assets/Resources/ui/item_atlas.png"
BACKUP = HERE / "bak_pre_oxalpha_item_life_icons.png"

T = (0, 0, 0, 0)
INK = (39, 27, 27, 255)
SHADOW = (70, 48, 39, 255)
GOLD = (213, 164, 78, 255)
CREAM = (242, 220, 166, 255)
AMBER = (230, 126, 57, 255)
RED = (179, 60, 48, 255)
SLATE = (42, 57, 68, 255)
GREEN = (91, 145, 86, 255)
PALETTE = {T, INK, SHADOW, GOLD, CREAM, AMBER, RED, SLATE, GREEN}
CELLS = {
    "revival_tea": (2, 2), "scroll_of_return": (3, 2),
    "reborn_stone": (0, 3), "special_job_token": (1, 3),
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


def revival_tea(d):
    d.rectangle((88, 82, 168, 103), fill=GOLD)
    d.rectangle((101, 65, 155, 87), fill=CREAM)
    d.polygon(((82, 100), (174, 100), (163, 188), (93, 188)), fill=GREEN)
    d.rectangle((101, 117, 155, 169), fill=CREAM)
    d.rectangle((118, 125, 138, 161), fill=RED)
    d.rectangle((110, 133, 146, 153), fill=RED)


def scroll_of_return(d):
    d.rectangle((80, 66, 176, 190), fill=CREAM)
    d.ellipse((65, 57, 103, 85), fill=GOLD)
    d.ellipse((153, 171, 191, 199), fill=GOLD)
    d.rectangle((84, 67, 172, 83), fill=GOLD)
    d.rectangle((84, 173, 172, 189), fill=GOLD)
    d.line((151, 111, 108, 111, 108, 144, 151, 144), fill=RED, width=13)
    d.polygon(((105, 127), (128, 106), (128, 148)), fill=RED)


def reborn_stone(d):
    d.polygon(((128, 48), (181, 101), (158, 190), (98, 190), (75, 101)), fill=CREAM)
    d.polygon(((128, 59), (164, 106), (145, 174), (111, 174), (92, 106)), fill=AMBER)
    d.polygon(((128, 79), (148, 113), (128, 155), (108, 113)), fill=RED)


def special_job_token(d):
    d.ellipse((61, 61, 195, 195), fill=GOLD)
    d.ellipse((73, 73, 183, 183), fill=CREAM)
    d.ellipse((85, 85, 171, 171), fill=RED)
    points = ((128, 91), (138, 116), (165, 116), (144, 133),
              (152, 162), (128, 145), (104, 162), (112, 133),
              (91, 116), (118, 116))
    d.polygon(points, fill=GOLD)


DRAW = {
    "revival_tea": revival_tea, "scroll_of_return": scroll_of_return,
    "reborn_stone": reborn_stone, "special_job_token": special_job_token,
}


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
