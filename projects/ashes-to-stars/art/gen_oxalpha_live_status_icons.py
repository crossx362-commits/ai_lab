#!/usr/bin/env python3
"""전투 HUD에서 실제 소비되는 상태 아이콘 3종을 ox-alpha로 통일한다.

18px HUD 표식은 생성형 회화보다 굵은 결정론적 픽토그램이 적합하다. 아틀라스
규격과 나머지 13칸을 그대로 두고 shield/taunt/attack_up 셀만 교체한다.
"""
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

HERE = Path(__file__).resolve().parent
TARGET = HERE.parent / "unity/Assets/Resources/ui/status_icon_atlas.png"
BACKUP = HERE / "bak_higgsfield_status_icon_atlas.png"

TRANSPARENT = (0, 0, 0, 0)
INK = (39, 27, 27, 255)
SHADOW = (70, 48, 39, 255)
GOLD = (213, 164, 78, 255)
CREAM = (242, 220, 166, 255)
AMBER = (230, 126, 57, 255)
RED = (179, 60, 48, 255)
SLATE = (42, 57, 68, 255)
PALETTE = {TRANSPARENT, INK, SHADOW, GOLD, CREAM, AMBER, RED, SLATE}


def badge():
    im = Image.new("RGBA", (256, 256), TRANSPARENT)
    d = ImageDraw.Draw(im)
    d.ellipse((20, 20, 235, 235), fill=INK)
    d.ellipse((27, 27, 228, 228), fill=GOLD)
    d.ellipse((35, 35, 220, 220), fill=SHADOW)
    d.ellipse((43, 43, 212, 212), fill=SLATE)
    return im, d


def shield(d):
    d.polygon(((128, 61), (181, 80), (174, 148), (128, 194), (82, 148),
               (75, 80)), fill=CREAM, outline=INK)
    d.polygon(((128, 78), (164, 91), (158, 140), (128, 172), (98, 140),
               (92, 91)), fill=GOLD)
    d.line((128, 80, 128, 169), fill=AMBER, width=11)


def taunt(d):
    d.polygon(((128, 59), (141, 97), (183, 77), (166, 118), (202, 128),
               (166, 139), (183, 180), (141, 159), (128, 198), (115, 159),
               (73, 180), (90, 139), (54, 128), (90, 118), (73, 77),
               (115, 97)), fill=RED)
    d.ellipse((91, 91, 165, 165), fill=CREAM)
    d.rectangle((118, 101, 138, 145), fill=INK)
    d.ellipse((118, 153, 138, 173), fill=INK)


def attack_up(d):
    d.polygon(((128, 55), (185, 119), (153, 119), (153, 187),
               (103, 187), (103, 119), (71, 119)), fill=CREAM, outline=INK)
    d.polygon(((128, 78), (163, 116), (143, 116), (143, 172),
               (113, 172), (113, 116), (93, 116)), fill=AMBER)


DRAW = (shield, taunt, attack_up)


def bounds(index):
    cell = 1254 / 4
    return round(index * cell), round((index + 1) * cell)


def make():
    source = Image.open(TARGET).convert("RGBA")
    assert source.size == (1254, 1254), source.size
    if not BACKUP.exists():
        source.save(BACKUP, optimize=True)
    before = source.copy()

    y0, y1 = bounds(2)
    for col, draw_icon in enumerate(DRAW):
        x0, x1 = bounds(col)
        icon, draw = badge()
        draw_icon(draw)
        assert set(icon.getdata()) <= PALETTE
        assert icon.getbbox() == (20, 20, 236, 236)
        icon = icon.resize((x1 - x0, y1 - y0), Image.Resampling.NEAREST)
        source.paste(TRANSPARENT, (x0, y0, x1, y1))
        source.paste(icon, (x0, y0), icon)

    # 선택한 3칸 밖 픽셀은 원본과 바이트 단위로 같다.
    mask = Image.new("1", source.size, 0)
    md = ImageDraw.Draw(mask)
    md.rectangle((bounds(0)[0], y0, bounds(3)[0] - 1, y1 - 1), fill=1)
    changed = ImageChops.difference(source, before)
    changed.paste(TRANSPARENT, (bounds(0)[0], y0, bounds(3)[0], y1))
    assert changed.getbbox() is None

    source.save(TARGET, optimize=True)
    print(f"→ {TARGET.name}  {TARGET.stat().st_size}B")


if __name__ == "__main__":
    make()
