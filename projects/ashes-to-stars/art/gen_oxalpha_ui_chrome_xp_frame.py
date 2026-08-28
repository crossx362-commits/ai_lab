#!/usr/bin/env python3
"""ox-alpha 경험치 크롬 — 1280x344 RGBA 가로 9-slice 프레임.

구형 원본의 과대 모서리 장식과 회화식 질감을 없애고, 채택된 HP 크롬과 같은
납작 웜톤 팔레트로 맞춘다. UiAtlas.ChromeSlice=0.16이므로 55px 밖의 가운데
늘림 구간은 축 방향으로 완전히 균일하다. 얇은 청람색 인레이로 경험치 바만
구분하되 작은 캐릭터 카드에서 체력 정보보다 먼저 튀지 않게 한다.
"""
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
W, H = 1280, 344
SLICE = int(min(W, H) * 0.16)  # 55

OUTLINE = (40, 24, 20, 255)
WOOD_DK = (86, 54, 34, 255)
WOOD = (150, 104, 58, 255)
GOLD = (216, 172, 96, 255)
XP_BLUE = (92, 142, 160, 255)
FILL = (35, 31, 29, 255)
PALETTE = {OUTLINE, WOOD_DK, WOOD, GOLD, XP_BLUE, FILL}


def ring(draw, inset, thick, color):
    draw.rectangle((inset, inset, W - 1 - inset, inset + thick - 1), fill=color)
    draw.rectangle((inset, H - inset - thick, W - 1 - inset, H - 1 - inset), fill=color)
    draw.rectangle((inset, inset, inset + thick - 1, H - 1 - inset), fill=color)
    draw.rectangle((W - inset - thick, inset, W - 1 - inset, H - 1 - inset), fill=color)


def make():
    image = Image.new("RGBA", (W, H), FILL)
    draw = ImageDraw.Draw(image)
    inset = 0
    for thick, color in ((8, OUTLINE), (12, WOOD_DK), (14, WOOD),
                         (3, GOLD), (5, XP_BLUE), (6, WOOD_DK), (4, OUTLINE)):
        ring(draw, inset, thick, color)
        inset += thick
    draw.rectangle((inset, inset, W - 1 - inset, H - 1 - inset), fill=FILL)

    # 코너 슬라이스 안쪽의 작은 경험 표식. 늘림 구간과 실제 게이지를 침범하지 않는다.
    draw.rectangle((15, 152, 25, 191), fill=XP_BLUE)
    draw.rectangle((19, 160, 21, 183), fill=GOLD)
    draw.rectangle((W - 26, 152, W - 16, 191), fill=XP_BLUE)
    draw.rectangle((W - 22, 160, W - 20, 183), fill=GOLD)

    _selfcheck(image)
    out = HERE / "out_oxalpha_ui_chrome_xp_frame.png"
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
