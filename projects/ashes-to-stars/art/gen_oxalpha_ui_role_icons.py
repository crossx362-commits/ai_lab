#!/usr/bin/env python3
"""UI 아틀라스 역할 아이콘 4종을 같은 ox-alpha 배지로 통일한다.

역할 아이콘은 작은 격자형 UI 표식이라 생성 이미지보다 결정론적 코드 합성이
적합하다. 기존 Rect·아틀라스 규격·GUID를 유지하고 전역 중심축 y=187에 맞춘다.
"""
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

HERE = Path(__file__).resolve().parent
ATLAS = HERE.parent / "unity/Assets/Resources/ui/ashes_to_stars_ui_atlas.png"
OUT = HERE / "out_oxalpha_ui_atlas_roles.png"
RECTS = {
    "tank": (8, 122, 125, 126),
    "damage": (137, 122, 138, 126),
    "healer": (278, 122, 142, 135),
    "buffer": (426, 122, 145, 130),
}

INK = (39, 27, 27, 255)
SHADOW = (70, 48, 39, 255)
GOLD = (213, 164, 78, 255)
GOLD_HI = (242, 205, 119, 255)
SLATE = (42, 57, 68, 255)
SLATE_HI = (76, 95, 105, 255)
BLUE = (48, 99, 139, 255)
GREEN = (78, 118, 73, 255)
CREAM = (196, 180, 139, 255)
PLUM = (103, 66, 105, 255)
PALETTE = {INK, SHADOW, GOLD, GOLD_HI, SLATE, SLATE_HI, BLUE, GREEN,
           CREAM, PLUM, (0, 0, 0, 0)}


def _badge(size):
    w, h = size
    im = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx, cy, r = w // 2, 65, 51
    d.ellipse((cx-r, cy-r, cx+r, cy+r), fill=INK)
    d.ellipse((cx-r+4, cy-r+4, cx+r-4, cy+r-4), fill=SHADOW)
    d.ellipse((cx-r+8, cy-r+8, cx+r-8, cy+r-8), fill=GOLD)
    d.ellipse((cx-r+12, cy-r+12, cx+r-12, cy+r-12), fill=SLATE)
    d.arc((cx-r+15, cy-r+15, cx+r-15, cy+r-15), 205, 335,
          fill=SLATE_HI, width=3)
    return im, d, cx, cy


def _tank(d, cx, cy):
    d.polygon(((cx, cy-35), (cx+29, cy-23), (cx+25, cy+12),
               (cx, cy+34), (cx-25, cy+12), (cx-29, cy-23)), fill=CREAM)
    d.polygon(((cx, cy-27), (cx+20, cy-18), (cx+17, cy+8),
               (cx, cy+24), (cx-17, cy+8), (cx-20, cy-18)), fill=BLUE)
    d.rectangle((cx-4, cy-20, cx+4, cy+18), fill=GOLD_HI)
    d.rectangle((cx-16, cy-5, cx+16, cy+3), fill=GOLD_HI)


def _damage(d, cx, cy):
    for flip in (-1, 1):
        d.polygon(((cx-6*flip, cy+27), (cx-12*flip, cy+21),
                   (cx+19*flip, cy-29), (cx+27*flip, cy-35),
                   (cx+23*flip, cy-24)), fill=CREAM)
        d.line((cx-12*flip, cy+18, cx+18*flip, cy-27), fill=GOLD_HI, width=4)
        d.rectangle((cx-17*flip-3, cy+15, cx-17*flip+3, cy+31), fill=SHADOW)
    d.ellipse((cx-7, cy-7, cx+7, cy+7), fill=GOLD)


def _healer(d, cx, cy):
    d.rectangle((cx-4, cy-13, cx+4, cy+34), fill=CREAM)
    d.ellipse((cx-22, cy-36, cx+22, cy+8), fill=GOLD_HI)
    d.ellipse((cx-15, cy-29, cx+15, cy+1), fill=GREEN)
    d.rectangle((cx-4, cy-23, cx+4, cy-5), fill=CREAM)
    d.rectangle((cx-13, cy-16, cx+13, cy-8), fill=CREAM)
    d.ellipse((cx-9, cy+27, cx+9, cy+37), fill=GOLD)


def _buffer(d, cx, cy):
    d.ellipse((cx-9, cy-9, cx+9, cy+9), fill=GOLD_HI)
    d.arc((cx-33, cy-26, cx+33, cy+26), 15, 170, fill=CREAM, width=5)
    d.arc((cx-33, cy-26, cx+33, cy+26), 195, 350, fill=PLUM, width=5)
    d.arc((cx-23, cy-35, cx+23, cy+35), 92, 265, fill=GREEN, width=5)
    d.arc((cx-23, cy-35, cx+23, cy+35), 275, 82, fill=BLUE, width=5)
    for x, y, color in ((-31, -8, CREAM), (31, 8, PLUM), (-8, 32, GREEN), (8, -32, BLUE)):
        d.ellipse((cx+x-5, cy+y-5, cx+x+5, cy+y+5), fill=color)


DRAW = {"tank": _tank, "damage": _damage, "healer": _healer, "buffer": _buffer}


def make():
    atlas = Image.open(ATLAS).convert("RGBA")
    assert atlas.size == (1448, 1086)
    before = atlas.copy()
    global_centers = []
    for name, (x, y, w, h) in RECTS.items():
        tile, draw, cx, cy = _badge((w, h))
        DRAW[name](draw, cx, cy)
        assert set(tile.getdata()) <= PALETTE
        assert tile.getbbox() == (cx - 51, cy - 51, cx + 52, cy + 52)
        global_centers.append(y + cy)
        atlas.paste(tile, (x, y))
    assert set(global_centers) == {187}

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
