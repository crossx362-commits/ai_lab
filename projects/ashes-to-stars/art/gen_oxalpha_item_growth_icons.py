#!/usr/bin/env python3
"""재화·성장 아이템 2종을 작은 크기용 ox-alpha 픽토그램으로 통일한다."""
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

HERE = Path(__file__).resolve().parent
TARGET = HERE.parent / "unity/Assets/Resources/ui/item_atlas.png"
BACKUP = HERE / "bak_pre_oxalpha_item_growth_icons.png"

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
CELLS = {"gold": (2, 3), "advancement_material": (3, 3)}


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


def gold(d):
    # 겹친 세 금화와 강화 별: 보상 골드·강화석의 이중 소비 의미를 함께 보존한다.
    d.ellipse((56, 118, 138, 177), fill=AMBER)
    d.rectangle((56, 139, 138, 164), fill=AMBER)
    d.ellipse((56, 109, 138, 159), fill=GOLD)
    d.ellipse((118, 118, 200, 177), fill=AMBER)
    d.rectangle((118, 139, 200, 164), fill=AMBER)
    d.ellipse((118, 109, 200, 159), fill=GOLD)
    d.ellipse((87, 68, 169, 127), fill=AMBER)
    d.rectangle((87, 89, 169, 114), fill=AMBER)
    d.ellipse((87, 59, 169, 109), fill=CREAM)
    d.polygon(((128, 69), (137, 87), (157, 89), (142, 102),
               (147, 122), (128, 111), (109, 122), (114, 102),
               (99, 89), (119, 87)), fill=RED)


def advancement_material(d):
    # 위로 자라는 결정과 이중 갈매기로 전직·성장 재료를 작은 크기에서도 구분한다.
    d.polygon(((128, 52), (174, 101), (153, 188), (103, 188), (82, 101)), fill=CREAM)
    d.polygon(((128, 65), (157, 108), (141, 172), (115, 172), (99, 108)), fill=GREEN)
    d.polygon(((128, 82), (144, 113), (128, 145), (112, 113)), fill=GOLD)
    d.line((88, 157, 128, 137, 168, 157), fill=AMBER, width=13)
    d.line((94, 180, 128, 163, 162, 180), fill=RED, width=11)


DRAW = {"gold": gold, "advancement_material": advancement_material}


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
