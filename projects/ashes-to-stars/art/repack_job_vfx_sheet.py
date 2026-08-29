#!/usr/bin/env python3
"""허용된 런타임 VFX 4x2 시트를 정수 256px 셀로 안전하게 재패킹한다."""

import argparse
import hashlib
from io import BytesIO
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
FX_DIR = ROOT / "unity/Assets/Resources/FX"
SHEETS = {
    "bard_aura": "bard_aura_sheet.png",
    "dps_slash": "dps_slash_sheet.png",
    "tank_slash": "tank_slash_sheet.png",
    "mage_fire": "mage_fire_sheet.png",
    "priest_heal": "priest_heal_sheet.png",
    "tank_barrier": "tank_barrier_sheet.png",
}
STATUS_SHEETS = {
    "poison_status": "poison_status_sheet.png",
    "freeze_status": "freeze_status_sheet.png",
    "boss_aoe_warning": "boss_aoe_warning_sheet.png",
}
COLS, ROWS, CELL, PADDING = 4, 2, 256, 8
SOURCE_SIZE = (1774, 887)
PACKED_SIZE = (COLS * CELL, ROWS * CELL)


def repack(source: Image.Image) -> Image.Image:
    source = source.convert("RGBA")
    packed = Image.new("RGBA", PACKED_SIZE, (0, 0, 0, 0))
    for row in range(ROWS):
        for col in range(COLS):
            frame = source.crop(
                (
                    round(col * source.width / COLS),
                    round(row * source.height / ROWS),
                    round((col + 1) * source.width / COLS),
                    round((row + 1) * source.height / ROWS),
                )
            )
            frame.thumbnail((CELL - PADDING * 2, CELL - PADDING * 2), Image.Resampling.LANCZOS)
            x = col * CELL + (CELL - frame.width) // 2
            y = row * CELL + (CELL - frame.height) // 2
            packed.alpha_composite(frame, (x, y))
    return packed


def encoded(image: Image.Image) -> bytes:
    stream = BytesIO()
    image.save(stream, "PNG", optimize=True)
    return stream.getvalue()


def assert_contract(image: Image.Image) -> None:
    assert image.size == PACKED_SIZE, image.size
    alpha = image.getchannel("A")
    assert alpha.getbbox() is not None, "가시 픽셀이 없다"
    for row in range(ROWS):
        for col in range(COLS):
            frame = alpha.crop((col * CELL, row * CELL, (col + 1) * CELL, (row + 1) * CELL))
            assert frame.crop((0, 0, CELL, PADDING)).getbbox() is None, "위 안전 여백 침범"
            assert frame.crop((0, CELL - PADDING, CELL, CELL)).getbbox() is None, "아래 안전 여백 침범"
            assert frame.crop((0, 0, PADDING, CELL)).getbbox() is None, "왼쪽 안전 여백 침범"
            assert frame.crop((CELL - PADDING, 0, CELL, CELL)).getbbox() is None, "오른쪽 안전 여백 침범"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    allowed = SHEETS | STATUS_SHEETS
    parser.add_argument("sheet", choices=sorted(allowed), help="재패킹할 허용 시트 키")
    parser.add_argument("source", nargs="?", type=Path, help="1774x887 원본 PNG. 생략하면 완료본을 검증")
    args = parser.parse_args()

    target = FX_DIR / allowed[args.sheet]
    source_path = args.source or target
    with Image.open(source_path) as opened:
        source = opened.convert("RGBA")

    if source_path == target and source.size == PACKED_SIZE:
        assert_contract(source)
        current = encoded(source)
        assert current == target.read_bytes(), "기존 PNG 인코딩이 결정론 계약과 다르다"
        print(f"PASS {target.name} already 1024x512 sha256={hashlib.sha256(current).hexdigest()}")
        return

    assert source.size == SOURCE_SIZE, f"예상하지 않은 원본 크기: {source.size}"
    result = repack(source)
    assert_contract(result)
    first = encoded(result)
    assert first == encoded(repack(source)), "재패킹 결과가 결정론적이지 않다"
    target.write_bytes(first)
    print(f"PASS {target.name} 1774x887 -> 1024x512 sha256={hashlib.sha256(first).hexdigest()}")


if __name__ == "__main__":
    main()
