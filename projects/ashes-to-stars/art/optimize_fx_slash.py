#!/usr/bin/env python3
"""기존 P23 fx_slash를 투명 512px 런타임 심볼로 최적화한다.

새 그림을 다시 생성하지 않는다. 육안으로 베기 역할을 확인한 P23 원본의 배경과
가장자리 잔여 알파를 정리한 뒤, FxPool의 회전·확대에도 잘리지 않게 여백을 둔다.
"""
from pathlib import Path

from PIL import Image

import knock_bg


HERE = Path(__file__).resolve().parent
SOURCE = HERE / "out_p23_fx" / "fx_slash.png"
TARGET = HERE.parent / "unity" / "Assets" / "Resources" / "fx" / "fx_slash.png"
SIZE = 512
CONTENT_SIZE = 480


def build() -> Image.Image:
    image = knock_bg.apply(Image.open(SOURCE), crop=True)
    image.thumbnail((CONTENT_SIZE, CONTENT_SIZE), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(image, ((SIZE - image.width) // 2, (SIZE - image.height) // 2))

    # 완전 투명 픽셀의 숨은 RGB를 비워 회전·텍스처 압축 때 검은 테두리가 번지지 않게 한다.
    pixels = canvas.load()
    for y in range(SIZE):
        for x in range(SIZE):
            if pixels[x, y][3] == 0:
                pixels[x, y] = (0, 0, 0, 0)
    return canvas


def main() -> None:
    image = build()
    TARGET.parent.mkdir(parents=True, exist_ok=True)
    image.save(TARGET, optimize=True)
    alpha = image.getchannel("A")
    histogram = alpha.histogram()
    clear = sum(histogram[:16])
    solid = sum(histogram[241:])
    print(f"→ {TARGET.name} {SIZE}×{SIZE} clear={clear} solid={solid}")


if __name__ == "__main__":
    main()
