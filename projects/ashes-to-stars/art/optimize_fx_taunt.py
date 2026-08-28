#!/usr/bin/env python3
"""기존 P23 fx_taunt를 투명 512px 런타임 심볼로 최적화한다.

새 그림을 다시 생성하지 않는다. P4 동명 원본은 황금 보호 고리라 방어막과
혼동되므로, P23 동심 충격파를 유지해 FxPool 규격에 맞춘다.
"""
from pathlib import Path

from PIL import Image

import knock_bg


HERE = Path(__file__).resolve().parent
SOURCE = HERE / "out_p23_fx" / "fx_taunt.png"
TARGET = HERE.parent / "unity" / "Assets" / "Resources" / "fx" / "fx_taunt.png"
SIZE = 512
CONTENT_SIZE = 480


def build() -> Image.Image:
    image = knock_bg.apply(Image.open(SOURCE), crop=True)
    image.thumbnail((CONTENT_SIZE, CONTENT_SIZE), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(image, ((SIZE - image.width) // 2, (SIZE - image.height) // 2))

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
    histogram = image.getchannel("A").histogram()
    clear = sum(histogram[:16])
    solid = sum(histogram[241:])
    print(f"→ {TARGET.name} {SIZE}×{SIZE} clear={clear} solid={solid}")


if __name__ == "__main__":
    main()
