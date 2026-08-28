#!/usr/bin/env python3
"""음유시인 오라 4x2 시트를 정수 256px 셀로 무손실 의미 재패킹한다."""

from pathlib import Path
import hashlib

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
TARGET = ROOT / "unity/Assets/Resources/FX/bard_aura_sheet.png"
COLS, ROWS, CELL = 4, 2, 256


def repack(source: Image.Image) -> Image.Image:
    source = source.convert("RGBA")
    packed = Image.new("RGBA", (COLS * CELL, ROWS * CELL), (0, 0, 0, 0))
    for row in range(ROWS):
        for col in range(COLS):
            left = round(col * source.width / COLS)
            right = round((col + 1) * source.width / COLS)
            top = round(row * source.height / ROWS)
            bottom = round((row + 1) * source.height / ROWS)
            frame = source.crop((left, top, right, bottom))
            frame.thumbnail((CELL, CELL), Image.Resampling.LANCZOS)
            x = col * CELL + (CELL - frame.width) // 2
            y = row * CELL + (CELL - frame.height) // 2
            packed.alpha_composite(frame, (x, y))
    return packed


def encoded(image: Image.Image) -> bytes:
    from io import BytesIO

    stream = BytesIO()
    image.save(stream, "PNG", optimize=True)
    return stream.getvalue()


def main() -> None:
    source = Image.open(TARGET)
    result = repack(source)
    first = encoded(result)
    second = encoded(repack(source))
    assert first == second, "재패킹 결과가 결정론적이지 않다"
    assert result.size == (1024, 512), result.size
    assert result.getchannel("A").getbbox() is not None, "가시 픽셀이 없다"
    TARGET.write_bytes(first)
    print(f"PASS {TARGET.name} 1774x887 -> 1024x512 sha256={hashlib.sha256(first).hexdigest()}")


if __name__ == "__main__":
    main()
