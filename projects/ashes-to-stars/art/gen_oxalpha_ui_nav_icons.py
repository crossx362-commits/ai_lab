#!/usr/bin/env python3
"""UI 아틀라스 상단의 하단 도크/화면 헤더 아이콘 5종을 통일한다.

구형 회화 아이콘은 조각마다 외곽 폭과 바닥선이 달랐다. 기존 Rect·아틀라스
규격·GUID는 유지하고, 같은 104px 배지와 중심축 안에 장소별 단순 실루엣을 그린다.
픽셀 격자형 UI라 이미지 생성기보다 결정론적 코드 합성이 적합한 품목이다.
"""
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

HERE = Path(__file__).resolve().parent
ATLAS = HERE.parent / "unity/Assets/Resources/ui/ashes_to_stars_ui_atlas.png"
OUT = HERE / "out_oxalpha_ui_atlas_nav.png"
RECTS = {
    "territory": (12, 0, 120, 122),
    "field": (140, 0, 135, 126),
    "tower": (275, 0, 120, 128),
    "worldmap": (407, 0, 135, 128),
    "characters": (550, 0, 130, 128),
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
PALETTE = {INK, SHADOW, GOLD, GOLD_HI, SLATE, SLATE_HI, BLUE, GREEN, CREAM,
           (0, 0, 0, 0)}


def _badge(size):
    w, h = size
    im = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    # Rect 높이는 122/126/128로 제각각이지만 아틀라스 전역 중심축은 y=63으로 고정한다.
    cx, cy = w // 2, 63
    r = 51
    d.ellipse((cx-r, cy-r, cx+r, cy+r), fill=INK)
    d.ellipse((cx-r+4, cy-r+4, cx+r-4, cy+r-4), fill=SHADOW)
    d.ellipse((cx-r+8, cy-r+8, cx+r-8, cy+r-8), fill=GOLD)
    d.ellipse((cx-r+12, cy-r+12, cx+r-12, cy+r-12), fill=SLATE)
    d.arc((cx-r+15, cy-r+15, cx+r-15, cy+r-15), 205, 335,
          fill=SLATE_HI, width=3)
    return im, d, cx, cy


def _territory(d, cx, cy):
    d.rectangle((cx-29, cy+12, cx+29, cy+29), fill=CREAM)
    d.rectangle((cx-24, cy-16, cx+24, cy+20), fill=CREAM)
    d.rectangle((cx-35, cy-7, cx-22, cy+25), fill=CREAM)
    d.rectangle((cx+22, cy-7, cx+35, cy+25), fill=CREAM)
    d.polygon(((cx-35, cy-7), (cx-28, cy-20), (cx-22, cy-7)), fill=GOLD_HI)
    d.polygon(((cx+22, cy-7), (cx+28, cy-20), (cx+35, cy-7)), fill=GOLD_HI)
    d.polygon(((cx-24, cy-16), (cx, cy-34), (cx+24, cy-16)), fill=GOLD_HI)
    d.rectangle((cx-7, cy+6, cx+7, cy+29), fill=INK)


def _field(d, cx, cy):
    d.polygon(((cx-35, cy+24), (cx-17, cy-3), (cx-2, cy+24)), fill=GREEN)
    d.polygon(((cx-12, cy+24), (cx+7, cy-16), (cx+27, cy+24)), fill=GREEN)
    d.polygon(((cx+12, cy+24), (cx+27, cy+2), (cx+39, cy+24)), fill=GREEN)
    d.line((cx-31, cy+30, cx+32, cy-30), fill=GOLD_HI, width=5)
    d.ellipse((cx+24, cy-34, cx+38, cy-20), fill=GOLD_HI)


def _tower(d, cx, cy):
    d.polygon(((cx-23, cy+31), (cx-18, cy-25), (cx-9, cy-34),
               (cx+9, cy-34), (cx+18, cy-25), (cx+23, cy+31)), fill=CREAM)
    d.rectangle((cx-27, cy+24, cx+27, cy+31), fill=GOLD)
    d.rectangle((cx-12, cy-11, cx+12, cy-4), fill=INK)
    d.rectangle((cx-8, cy+8, cx+8, cy+31), fill=INK)
    d.rectangle((cx-2, cy-44, cx+2, cy-30), fill=GOLD_HI)
    d.polygon(((cx+2, cy-44), (cx+18, cy-39), (cx+2, cy-34)), fill=BLUE)


def _worldmap(d, cx, cy):
    d.ellipse((cx-33, cy-33, cx+33, cy+33), fill=BLUE)
    d.ellipse((cx-33, cy-33, cx+33, cy+33), outline=GOLD_HI, width=4)
    d.arc((cx-16, cy-33, cx+16, cy+33), 90, 270, fill=CREAM, width=3)
    d.arc((cx-16, cy-33, cx+16, cy+33), 270, 90, fill=CREAM, width=3)
    d.line((cx-30, cy, cx+30, cy), fill=CREAM, width=3)
    d.polygon(((cx, cy-28), (cx+7, cy-5), (cx+28, cy), (cx+7, cy+5),
               (cx, cy+28), (cx-7, cy+5), (cx-28, cy), (cx-7, cy-5)),
              fill=GOLD_HI)


def _characters(d, cx, cy):
    d.ellipse((cx-10, cy-31, cx+10, cy-11), fill=CREAM)
    d.polygon(((cx-28, cy+28), (cx-21, cy-2), (cx-10, cy-12),
               (cx+10, cy-12), (cx+21, cy-2), (cx+28, cy+28)), fill=BLUE)
    d.ellipse((cx-34, cy-14, cx-18, cy+2), fill=SLATE_HI)
    d.polygon(((cx-42, cy+25), (cx-38, cy+3), (cx-28, cy-6),
               (cx-18, cy+3), (cx-14, cy+25)), fill=GREEN)
    d.ellipse((cx+18, cy-14, cx+34, cy+2), fill=SLATE_HI)
    d.polygon(((cx+14, cy+25), (cx+18, cy+3), (cx+28, cy-6),
               (cx+38, cy+3), (cx+42, cy+25)), fill=SHADOW)


DRAW = {
    "territory": _territory,
    "field": _field,
    "tower": _tower,
    "worldmap": _worldmap,
    "characters": _characters,
}


def make():
    atlas = Image.open(ATLAS).convert("RGBA")
    assert atlas.size == (1448, 1086)
    before = atlas.copy()
    centers = []
    for name, (x, y, w, h) in RECTS.items():
        tile, draw, cx, cy = _badge((w, h))
        DRAW[name](draw, cx, cy)
        assert set(tile.getdata()) <= PALETTE
        assert tile.getbbox() == (cx - 51, cy - 51, cx + 52, cy + 52)
        centers.append((cx, cy))
        atlas.paste(tile, (x, y))
    assert {c[1] for c in centers} == {63}

    mask = Image.new("1", atlas.size)
    md = ImageDraw.Draw(mask)
    for x, y, w, h in RECTS.values():
        md.rectangle((x, y, x + w - 1, y + h - 1), fill=1)
    changed = ImageChops.difference(before, atlas)
    outside = Image.new("RGBA", atlas.size)
    outside.paste(changed, mask=Image.eval(mask, lambda p: 0 if p else 255))
    assert outside.getbbox() is None

    atlas.save(OUT, optimize=True)
    print(f"→ {OUT.name}  {atlas.width}x{atlas.height}  {OUT.stat().st_size}B")


if __name__ == "__main__":
    make()
