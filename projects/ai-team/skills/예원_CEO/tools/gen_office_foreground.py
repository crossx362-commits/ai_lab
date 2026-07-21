#!/usr/bin/env python3
"""Extract foreground furniture from the office background for FleetView depth."""
from __future__ import annotations

from pathlib import Path
from PIL import Image, ImageDraw


HERE = Path(__file__).resolve().parent
SRC = HERE / "office_bg_chibi.png"
OUT = HERE / "office_fg_chibi.png"


def add_rect_mask(mask: Image.Image, box: tuple[int, int, int, int]) -> None:
    ImageDraw.Draw(mask).rectangle(box, fill=255)


CHAIR_FRONT_MASKS = [
    (481, 452), (640, 452), (800, 452), (959, 452),
    (481, 622), (640, 622), (800, 622), (959, 622),
]
assert len(CHAIR_FRONT_MASKS) == 8


def main() -> None:
    bg = Image.open(SRC).convert("RGBA")
    mask = Image.new("L", bg.size, 0)

    # Desk edges cover typing hands without cutting across the character's waist.
    for box in [
        (405, 397, 557, 410),   # back row desk 1 edge
        (568, 397, 714, 410),   # back row desk 2 edge
        (728, 397, 872, 410),   # back row desk 3 edge
        (888, 397, 1031, 410),  # back row desk 4 edge
        (402, 567, 557, 580),   # front row desk 1 edge
        (567, 567, 714, 580),   # front row desk 2 edge
        (727, 567, 872, 580),   # front row desk 3 edge
        (888, 567, 1031, 580),  # front row desk 4 edge
        (1045, 250, 1310, 360), # meeting table front half
        (565, 900, 780, 955),   # lounge sofa low front lip
        (120, 270, 295, 305),   # CEO sofa/table low front lip
        (185, 858, 390, 900),   # cafe counter low front lip
    ]:
        add_rect_mask(mask, box)

    # Keep the backrest behind the character; only the seat lip and stem sit in front.
    draw = ImageDraw.Draw(mask)
    for cx, cy in CHAIR_FRONT_MASKS:
        draw.ellipse((cx - 25, cy - 18, cx + 25, cy + 3), fill=255)
        draw.rounded_rectangle((cx - 5, cy - 8, cx + 5, cy + 18), radius=4, fill=255)

    # Feather very lightly so the overlay edges do not look cut out.
    fg = Image.new("RGBA", bg.size, (0, 0, 0, 0))
    fg.alpha_composite(bg)
    fg.putalpha(mask)
    fg.save(OUT)
    print(f"saved {OUT} ({fg.width}x{fg.height})")


if __name__ == "__main__":
    main()
