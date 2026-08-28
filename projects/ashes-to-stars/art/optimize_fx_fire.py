#!/usr/bin/env python3
"""기존 생성형 fx_fire를 투명 512px 런타임 심볼로 최적화한다.

새 그림을 다시 생성하지 않는다. P23 원본에 이미 검증된 배경 제거를 적용한 뒤,
실제 FxPool 표시 크기(높이 2유닛)에 맞춰 Lanczos 축소하고 투명 RGB를 정리한다.
"""
from pathlib import Path

from PIL import Image

import knock_bg


HERE = Path(__file__).resolve().parent
SOURCE = HERE / "out_p23_fx" / "fx_fire.png"
TARGET = HERE.parent / "unity" / "Assets" / "Resources" / "fx" / "fx_fire.png"
SIZE = 512
CONTENT_SIZE = 480


def build() -> Image.Image:
    image = knock_bg.apply(Image.open(SOURCE), crop=True)
    # 불꽃은 FxPool에서 0.45→1.60배로 크게 팽창하므로 캔버스 가장자리 잘림을 피한다.
    image.thumbnail((CONTENT_SIZE, CONTENT_SIZE), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(image, ((SIZE - image.width) // 2, (SIZE - image.height) // 2))

    # 완전 투명 픽셀의 숨은 RGB를 비워 텍스처 압축 가장자리의 회색 번짐을 막는다.
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
    print(f"→ {TARGET.name} {image.size[0]}×{image.size[1]} clear={clear} solid={solid}")


if __name__ == "__main__":
    main()
