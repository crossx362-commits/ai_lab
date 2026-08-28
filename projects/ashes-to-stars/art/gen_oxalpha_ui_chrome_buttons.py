#!/usr/bin/env python3
"""ox-alpha 버튼 크롬 3상태 — 원본 규격을 지키는 RGBA 9-slice 세트.

구형 회화식 금테의 상태별 크기·실루엣 흔들림을 없애고, 채택된 panel/HP/XP
크롬과 같은 납작 웜톤 팔레트로 맞춘다. 각 파일의 원본 크기는 유지하되
가운데 늘림 구간은 축 방향으로 균일하게 만든다.
"""
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
SPECS = {
    "normal": (1130, 469),
    "hover": (1162, 506),
    "pressed": (1149, 491),
}

OUTLINE = (40, 24, 20, 255)
WOOD_DK = (86, 54, 34, 255)
WOOD = (150, 104, 58, 255)
GOLD = (216, 172, 96, 255)
GOLD_HI = (242, 205, 126, 255)
BLUE = (45, 53, 67, 255)
BLUE_HI = (58, 72, 91, 255)
PRESS = (28, 31, 38, 255)
PALETTE = {OUTLINE, WOOD_DK, WOOD, GOLD, GOLD_HI, BLUE, BLUE_HI, PRESS}


def ring(draw, w, h, inset, thick, color):
    draw.rectangle((inset, inset, w - 1 - inset, inset + thick - 1), fill=color)
    draw.rectangle((inset, h - inset - thick, w - 1 - inset, h - 1 - inset), fill=color)
    draw.rectangle((inset, inset, inset + thick - 1, h - 1 - inset), fill=color)
    draw.rectangle((w - inset - thick, inset, w - 1 - inset, h - 1 - inset), fill=color)


def make_state(state, size):
    w, h = size
    fill = BLUE_HI if state == "hover" else PRESS if state == "pressed" else BLUE
    accent = GOLD_HI if state == "hover" else WOOD if state == "pressed" else GOLD
    image = Image.new("RGBA", size, OUTLINE)
    draw = ImageDraw.Draw(image)
    inset = 0
    for thick, color in ((8, OUTLINE), (12, WOOD_DK), (16, WOOD),
                         (4, accent), (7, WOOD_DK), (4, OUTLINE)):
        ring(draw, w, h, inset, thick, color)
        inset += thick
    draw.rectangle((inset, inset, w - 1 - inset, h - 1 - inset), fill=fill)

    # 상태 표식은 9-slice 모서리 안에만 둬 늘어나는 버튼에서도 형태가 보존된다.
    mark = 12 if state == "pressed" else 18
    draw.rectangle((mark, h // 2 - 24, mark + 9, h // 2 + 23), fill=accent)
    draw.rectangle((w - mark - 10, h // 2 - 24, w - mark - 1, h // 2 + 23), fill=accent)
    if state == "pressed":
        draw.rectangle((inset, inset, w - 1 - inset, inset + 7), fill=OUTLINE)

    _selfcheck(image, state)
    out = HERE / f"out_oxalpha_ui_chrome_button_{state}.png"
    image.save(out, optimize=True)
    print(f"→ {out.name}  {w}x{h}  {image.mode}  {out.stat().st_size}B")
    return image, out


def _selfcheck(image, state):
    w, h = image.size
    slice_px = int(min(w, h) * 0.16)
    assert image.mode == "RGBA" and set(image.getdata()) <= PALETTE
    assert image.tobytes() == image.transpose(Image.Transpose.FLIP_LEFT_RIGHT).tobytes()
    pixels = image.load()
    for y in range(h):
        sample = pixels[slice_px, y]
        assert all(pixels[x, y] == sample for x in range(slice_px, w - slice_px))
    expected = BLUE_HI if state == "hover" else PRESS if state == "pressed" else BLUE
    assert pixels[w // 2, h // 2] == expected


def make():
    return {state: make_state(state, size) for state, size in SPECS.items()}


if __name__ == "__main__":
    make()
