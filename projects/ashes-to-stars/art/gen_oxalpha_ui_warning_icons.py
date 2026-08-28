#!/usr/bin/env python3
"""실소비 위험 경고 단품 5종을 투명 ox-alpha 픽토그램으로 통일한다.

작은 HUD 표식은 생성기 회화 배경보다 결정론적 픽셀 UI가 적합하다. 원본
1024 캔버스와 Unity GUID를 유지하고, 색뿐 아니라 외곽 모양으로 구분한다.
"""
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
ICON_DIR = HERE.parent / "unity/Assets/Resources/ui/icons"
NAMES = ("warn_circle", "warn_beam", "warn_summon", "warn_enrage", "warn_phase")

TRANSPARENT = (0, 0, 0, 0)
INK = (39, 27, 27, 255)
SHADOW = (70, 48, 39, 255)
GOLD = (213, 164, 78, 255)
CREAM = (242, 220, 166, 255)
AMBER = (230, 126, 57, 255)
RED = (179, 60, 48, 255)
PLUM = (103, 66, 105, 255)
SLATE = (42, 57, 68, 255)
PALETTE = {TRANSPARENT, INK, SHADOW, GOLD, CREAM, AMBER, RED, PLUM, SLATE}


def badge():
    im = Image.new("RGBA", (256, 256), TRANSPARENT)
    d = ImageDraw.Draw(im)
    d.ellipse((20, 20, 235, 235), fill=INK)
    d.ellipse((27, 27, 228, 228), fill=GOLD)
    d.ellipse((35, 35, 220, 220), fill=SHADOW)
    d.ellipse((43, 43, 212, 212), fill=SLATE)
    return im, d


def circle(d):
    d.ellipse((66, 66, 189, 189), outline=AMBER, width=16)
    for a in ((119, 48, 136, 90), (119, 166, 136, 208),
              (48, 119, 90, 136), (166, 119, 208, 136)):
        d.rectangle(a, fill=CREAM)
    d.polygon(((128, 91), (143, 137), (128, 151), (113, 137)), fill=RED)


def beam(d):
    d.polygon(((71, 176), (112, 91), (123, 118), (177, 61),
               (143, 147), (132, 130)), fill=CREAM)
    d.line((68, 184, 184, 68), fill=AMBER, width=12)
    d.line((78, 194, 194, 78), fill=RED, width=7)


def summon(d):
    d.ellipse((76, 76, 180, 180), outline=PLUM, width=13)
    d.polygon(((128, 55), (146, 100), (194, 105), (157, 137),
               (169, 184), (128, 158), (87, 184), (99, 137),
               (62, 105), (110, 100)), outline=CREAM, fill=None)
    d.ellipse((113, 113, 143, 143), fill=GOLD)


def enrage(d):
    d.polygon(((76, 184), (90, 117), (112, 139), (128, 64),
               (145, 139), (169, 106), (183, 184)), fill=RED)
    d.polygon(((100, 181), (110, 143), (128, 158), (146, 131),
               (157, 181)), fill=AMBER)
    d.ellipse((90, 179, 166, 195), fill=CREAM)


def phase(d):
    d.arc((61, 61, 195, 195), 70, 250, fill=CREAM, width=17)
    d.arc((61, 61, 195, 195), 250, 430, fill=PLUM, width=17)
    d.polygon(((67, 80), (100, 73), (80, 105)), fill=CREAM)
    d.polygon(((189, 176), (156, 183), (176, 151)), fill=PLUM)
    d.rectangle((122, 86, 134, 170), fill=GOLD)


DRAW = {"warn_circle": circle, "warn_beam": beam, "warn_summon": summon,
        "warn_enrage": enrage, "warn_phase": phase}


def make():
    hashes = []
    for name in NAMES:
        target = ICON_DIR / f"{name}.png"
        source = Image.open(target).convert("RGBA")
        assert source.size == (512, 512), (name, source.size)
        backup = HERE / f"bak_higgsfield_ui_{name}.png"
        if not backup.exists():
            source.save(backup, optimize=True)

        icon, draw = badge()
        DRAW[name](draw)
        assert set(icon.getdata()) <= PALETTE
        assert icon.getbbox() == (20, 20, 236, 236)
        icon = icon.resize(source.size, Image.Resampling.NEAREST)
        icon.save(target, optimize=True)
        hashes.append(target.read_bytes())
        print(f"→ {target.name}  {target.stat().st_size}B")
    assert len(set(hashes)) == len(NAMES)


if __name__ == "__main__":
    make()
