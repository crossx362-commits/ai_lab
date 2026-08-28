#!/usr/bin/env python3
"""UI 아틀라스의 장비 등급 프레임 5종을 ox-alpha 슬롯 문법으로 통일한다.

등급 조각의 기존 좌표를 유지해 런타임 Rect/GUID를 건드리지 않는다. 프레임은
같은 중심·안쪽 안전영역을 쓰고, 등급은 색과 모서리 표식 수로만 구분한다.
"""
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

HERE = Path(__file__).resolve().parent
ATLAS = HERE.parent / "unity/Assets/Resources/ui/ashes_to_stars_ui_atlas.png"
OUT = HERE / "out_oxalpha_ui_atlas_rarity.png"
RECTS = {
    "common": (8, 800, 80, 92),
    "uncommon": (93, 800, 88, 92),
    "rare": (185, 800, 91, 92),
    "heroic": (280, 800, 89, 92),
    "legendary": (373, 800, 96, 92),
}
ACCENTS = {
    "common": (150, 156, 166, 255),
    "uncommon": (91, 166, 92, 255),
    "rare": (63, 139, 211, 255),
    "heroic": (151, 76, 190, 255),
    "legendary": (226, 169, 55, 255),
}
OUTLINE = (35, 25, 27, 255)
SHADOW = (66, 48, 48, 255)
WELL = (16, 24, 35, 238)


def frame(size, accent, rank):
    w, h = size
    im = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    # 모든 등급이 같은 7px 외곽과 10px 안전영역을 가져 아이콘 중심이 흔들리지 않는다.
    d.rounded_rectangle((1, 1, w - 2, h - 2), radius=10, fill=OUTLINE)
    d.rounded_rectangle((3, 3, w - 4, h - 4), radius=8, fill=SHADOW)
    d.rounded_rectangle((5, 5, w - 6, h - 6), radius=7, fill=accent)
    d.rounded_rectangle((8, 8, w - 9, h - 9), radius=5, fill=WELL)
    # 위계 표식은 모서리에만 두어 중앙 장비 실루엣을 가리지 않는다.
    marks = rank + 1
    for i in range(marks):
        x = 8 + i * 7
        d.polygon(((x, 5), (x + 3, 2), (x + 6, 5), (x + 3, 8)), fill=accent)
        xr = w - 9 - i * 7
        d.polygon(((xr, h - 6), (xr + 3, h - 9), (xr + 6, h - 6),
                   (xr + 3, h - 3)), fill=accent)
    return im


def make():
    atlas = Image.open(ATLAS).convert("RGBA")
    assert atlas.size == (1448, 1086)
    before = atlas.copy()
    for rank, (name, (x, y, w, h)) in enumerate(RECTS.items()):
        tile = frame((w, h), ACCENTS[name], rank)
        assert tile.getbbox() == (1, 1, w - 1, h - 1)
        assert tile.getpixel((w // 2, h // 2)) == WELL
        atlas.paste(tile, (x, y))

    # 지정 영역 밖 픽셀은 한 점도 바뀌면 안 된다.
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
