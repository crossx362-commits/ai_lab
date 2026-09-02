#!/usr/bin/env python3
"""Navy+gold 9-slice UI chrome matching the tank portrait palette.

Corners hold L-brackets and rivets. Edges are uniform bands so DrawSliced
does not smear ornaments. Source sizes stay identical to the current
Resources/ui/chrome files.
"""
from pathlib import Path
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
OUT = HERE / "out_ui_chrome"
RES = HERE.parent / "unity" / "Assets" / "Resources" / "ui" / "chrome"

INK = (18, 16, 22, 255)
NAVY = (28, 36, 52, 255)
NAVY_HI = (42, 56, 82, 255)
NAVY_DK = (20, 26, 38, 255)
GOLD = (212, 168, 72, 255)
GOLD_HI = (242, 205, 118, 255)
GOLD_DK = (148, 108, 42, 255)
STEEL = (92, 96, 108, 255)

SIZES = {
    "panel": (868, 870),
    "button_normal": (1130, 469),
    "button_hover": (1162, 506),
    "button_pressed": (1149, 491),
    "portrait_frame": (765, 762),
    "hp_frame": (1194, 206),
    "xp_frame": (1280, 344),
    "boss_hp_frame": (1194, 206),
}


def ring(d, w, h, inset, thick, color):
    if thick < 1:
        return
    x0, y0 = inset, inset
    x1, y1 = w - 1 - inset, h - 1 - inset
    d.rectangle([x0, y0, x1, y0 + thick - 1], fill=color)
    d.rectangle([x0, y1 - thick + 1, x1, y1], fill=color)
    d.rectangle([x0, y0, x0 + thick - 1, y1], fill=color)
    d.rectangle([x1 - thick + 1, y0, x1, y1], fill=color)


def rivet(d, x, y, gold=GOLD, hi=GOLD_HI):
    d.rectangle([x, y, x + 7, y + 7], fill=INK)
    d.rectangle([x + 1, y + 1, x + 6, y + 6], fill=gold)
    d.point((x + 2, y + 2), fill=hi)


def l_corner(d, x, y, s, flip_x, flip_y, accent):
    # L bracket inside the slice corner. Uniform across four corners via flips.
    pts = []
    def px(i, j):
        xx = x + (s - 1 - i if flip_x else i)
        yy = y + (s - 1 - j if flip_y else j)
        return xx, yy
    # long arms
    for i in range(10, s - 14):
        for t in range(0, 5):
            d.point(px(i, 10 + t), fill=accent)
            d.point(px(10 + t, i), fill=accent)
    for i in range(10, s - 18):
        d.point(px(i, 12), fill=GOLD_HI if accent == GOLD else GOLD)
        d.point(px(12, i), fill=GOLD_HI if accent == GOLD else GOLD)


def frame(w, h, bands, fill, accent, corner_s):
    im = Image.new("RGBA", (w, h), fill)
    d = ImageDraw.Draw(im)
    inset = 0
    for thick, color in bands:
        ring(d, w, h, inset, thick, color)
        inset += thick
    d.rectangle([inset, inset, w - 1 - inset, h - 1 - inset], fill=fill)
    s = min(corner_s, inset + 8, w // 3, h // 3)
    l_corner(d, 0, 0, s, False, False, accent)
    l_corner(d, w - s, 0, s, True, False, accent)
    l_corner(d, 0, h - s, s, False, True, accent)
    l_corner(d, w - s, h - s, s, True, True, accent)
    pad = 14
    rivet(d, pad, pad, accent)
    rivet(d, w - pad - 8, pad, accent)
    rivet(d, pad, h - pad - 8, accent)
    rivet(d, w - pad - 8, h - pad - 8, accent)
    return im


def make_panel():
    bands = [
        (10, INK),
        (8, STEEL),
        (14, NAVY_DK),
        (6, GOLD),
        (10, NAVY),
        (3, GOLD_DK),
        (8, INK),
    ]
    return frame(868, 870, bands, NAVY, GOLD, 188)


def make_button(state, size):
    w, h = size
    fill = NAVY_HI if state == "hover" else NAVY_DK if state == "pressed" else NAVY
    accent = GOLD_HI if state == "hover" else GOLD_DK if state == "pressed" else GOLD
    bands = [
        (8, INK),
        (12, NAVY_DK),
        (16, STEEL if state != "pressed" else INK),
        (4, accent),
        (7, NAVY_DK),
        (4, INK),
    ]
    return frame(w, h, bands, fill, accent, min(w, h) // 3)


def make_bar(size, accent):
    w, h = size
    bands = [
        (6, INK),
        (8, NAVY_DK),
        (10, STEEL),
        (3, accent),
        (6, INK),
    ]
    return frame(w, h, bands, NAVY_DK, accent, min(h - 8, 72))


def make_portrait():
    w, h = SIZES["portrait_frame"]
    bands = [
        (8, INK),
        (10, STEEL),
        (12, NAVY_DK),
        (5, GOLD),
        (8, NAVY),
        (3, GOLD_DK),
        (8, INK),
    ]
    im = frame(w, h, bands, (0, 0, 0, 255), GOLD, 170)
    return im


def save(name, im):
    OUT.mkdir(parents=True, exist_ok=True)
    RES.mkdir(parents=True, exist_ok=True)
    path_out = OUT / f"{name}.png"
    path_res = RES / f"{name}.png"
    im.save(path_out)
    im.save(path_res)
    print(name, im.size, "->", path_res)


def main():
    save("panel", make_panel())
    save("button_normal", make_button("normal", SIZES["button_normal"]))
    save("button_hover", make_button("hover", SIZES["button_hover"]))
    save("button_pressed", make_button("pressed", SIZES["button_pressed"]))
    save("portrait_frame", make_portrait())
    save("hp_frame", make_bar(SIZES["hp_frame"], (200, 72, 64, 255)))
    save("xp_frame", make_bar(SIZES["xp_frame"], GOLD))
    save("boss_hp_frame", make_bar(SIZES["boss_hp_frame"], GOLD_HI))


if __name__ == "__main__":
    main()
