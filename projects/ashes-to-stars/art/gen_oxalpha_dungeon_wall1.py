#!/usr/bin/env python3
"""ox-alpha 던전 벽(dungeon_wall_1) 코드합성 — 256×256 아치 벽감.

배경: dungeon_wall_0 은 ox-alpha 256인데 dungeon_wall_1 는 아직 나노바나나
1379×1611·2.6MB 그레이 아치 벽이라 톤이 따로 놀았다. 같은 차가운 석조·같은 256,
뾰족 아치 벽감으로 교체한다. dungeon_wall_0(벽돌 4단)·_2(바위 벽) 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, dungeon_wall_1=1.50)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 세로 석조 벽 + 가운데 뾰족 아치 벽감(어두운 속으로 채움, 고리/글로우 아님).
그림자는 밑 얇은 띠뿐. 할로우 금지.

사용: python3 gen_oxalpha_dungeon_wall1.py
출력: art/out_oxalpha_dungeon_wall1.png (256×256 RGBA)
"""
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

OUTLINE = (40, 24, 20, 255)
STONE = (132, 128, 122, 255)
STONE_DK = (86, 82, 76, 255)
STONE_LT = (172, 166, 156, 255)
HOLE = (48, 44, 40, 255)


def _brick(d, x0, y0, x1, y1, fill):
    d.polygon([(x0 - 2, y0 - 2), (x1 + 2, y0 - 2), (x1 + 2, y1 + 2), (x0 - 2, y1 + 2)], fill=OUTLINE)
    d.polygon([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], fill=fill)
    d.rectangle([x0 + 2, y0 + 2, x1 - 2, y0 + 5], fill=STONE_LT)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx = 128

    d.rectangle([78, 214, 178, 222], fill=(30, 24, 20, 90))

    # 벽 몸체
    _brick(d, 84, 178, 172, 214, STONE_DK)
    _brick(d, 84, 144, 172, 180, STONE)
    _brick(d, 84, 110, 172, 146, STONE)
    _brick(d, 84, 76, 172, 112, STONE_LT)

    # 뾰족 아치 벽감 (채움)
    d.polygon([
        (cx - 22, 200), (cx - 22, 128),
        (cx, 92), (cx + 22, 128), (cx + 22, 200),
    ], fill=OUTLINE)
    d.polygon([
        (cx - 18, 196), (cx - 18, 130),
        (cx, 98), (cx + 18, 130), (cx + 18, 196),
    ], fill=HOLE)
    d.line([(cx, 102), (cx, 192)], fill=STONE_DK, width=2)

    out = HERE / "out_oxalpha_dungeon_wall1.png"
    im.save(out)
    px = im.load()
    clear = solid = 0
    for y in range(256):
        for x in range(256):
            a = px[x, y][3]
            if a < 13:
                clear += 1
            elif a > 242:
                solid += 1
    print(f"→ {out.name}  256×256  투명 {clear} · 불투명 {solid} / 65536")


if __name__ == "__main__":
    make()
