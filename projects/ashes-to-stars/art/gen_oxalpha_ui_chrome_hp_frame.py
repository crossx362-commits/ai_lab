#!/usr/bin/env python3
"""ox-alpha 일반 HP 크롬 — 1194x206 RGBA 가로 9-slice 프레임.

구형 원본의 좌우 검은 잔상과 회화식 번짐을 없애고, 채택된 panel과 같은
납작 웜톤 팔레트로 맞춘다. UiAtlas.ChromeSlice=0.18이므로 37px 밖의 가운데
늘림 구간은 축 방향으로 완전히 균일하며, 일반 체력바는 보스 봉인 문양 없이
얇은 황동 모서리만 써 위계를 낮춘다.
"""
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
W, H = 1194, 206
SLICE = int(min(W, H) * 0.18)  # 37

OUTLINE = (40, 24, 20, 255)
WOOD_DK = (86, 54, 34, 255)
WOOD = (150, 104, 58, 255)
GOLD = (216, 172, 96, 255)
FILL = (35, 31, 29, 255)
PALETTE = {OUTLINE, WOOD_DK, WOOD, GOLD, FILL}


def ring(draw, inset, thick, color):
    draw.rectangle((inset, inset, W - 1 - inset, inset + thick - 1), fill=color)
    draw.rectangle((inset, H - inset - thick, W - 1 - inset, H - 1 - inset), fill=color)
    draw.rectangle((inset, inset, inset + thick - 1, H - 1 - inset), fill=color)
    draw.rectangle((W - inset - thick, inset, W - 1 - inset, H - 1 - inset), fill=color)


def make():
    image = Image.new("RGBA", (W, H), FILL)
    draw = ImageDraw.Draw(image)
    inset = 0
    # 보스판보다 얇고 차분한 황동 테두리: 파티원 HP가 상단 보스바와 경쟁하지 않는다.
    for thick, color in ((5, OUTLINE), (8, WOOD_DK), (10, WOOD),
                         (2, GOLD), (6, WOOD_DK), (3, OUTLINE)):
        ring(draw, inset, thick, color)
        inset += thick
    draw.rectangle((inset, inset, W - 1 - inset, H - 1 - inset), fill=FILL)

    # 슬라이스 안쪽에만 작은 체력 표식. 좌우 대칭이라 어느 폭으로 늘려도 흔들리지 않는다.
    draw.rectangle((10, 91, 18, 114), fill=GOLD)
    draw.rectangle((13, 96, 15, 109), fill=OUTLINE)
    draw.rectangle((W - 19, 91, W - 11, 114), fill=GOLD)
    draw.rectangle((W - 16, 96, W - 14, 109), fill=OUTLINE)

    _selfcheck(image)
    out = HERE / "out_oxalpha_ui_chrome_hp_frame.png"
    image.save(out, optimize=True)
    print(f"→ {out.name}  {W}x{H}  {image.mode}  {out.stat().st_size}B")
    return image, out


def _selfcheck(image):
    assert image.size == (W, H) and image.mode == "RGBA"
    assert set(image.getdata()) <= PALETTE
    assert image.tobytes() == image.transpose(Image.Transpose.FLIP_LEFT_RIGHT).tobytes()
    assert image.tobytes() == image.transpose(Image.Transpose.FLIP_TOP_BOTTOM).tobytes()
    pixels = image.load()
    for y in range(H):
        sample = pixels[SLICE, y]
        assert all(pixels[x, y] == sample for x in range(SLICE, W - SLICE))
    assert pixels[W // 2, H // 2] == FILL


if __name__ == "__main__":
    make()
