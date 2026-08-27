#!/usr/bin/env python3
"""ox-alpha 던전 벽(dungeon_wall_0) 코드합성 — 256×256 구멍 난 벽돌 벽.

배경: dungeon_cover_* 은 ox-alpha 256인데 dungeon_wall_0 는 아직 나노바나나
1454×1664·2.9MB 그레이 벽돌 벽이라 톤이 따로 놀았다. 같은 차가운 석조·같은 256,
4단 벽돌 벽(빠진 블록 2개)으로 교체한다. dungeon_wall_1(아치 벽감)·_2
(바위 벽) 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, dungeon_wall_0=1.50)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 3/4 각 4단 석조 벽, 빠진 블록 두 칸(어두운 속). 글로우 금지.
그림자는 밑 얇은 띠뿐. 낮은 엄폐벽 cover_0보다 높고 좁다.

사용: python3 gen_oxalpha_dungeon_wall.py
출력: art/out_oxalpha_dungeon_wall.png (256×256 RGBA)
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
    d.rectangle([x0 + 2, y0 + 2, x1 - 2, y0 + 6], fill=STONE_LT)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)

    d.rectangle([72, 214, 184, 222], fill=(30, 24, 20, 90))

    # 4단, 아래부터. 구멍은 2단 오른쪽·3단 왼쪽
    rows = [
        (174, STONE_DK, [(76, 128), (128, 184)]),
        (142, STONE, [(76, 124), (124, 180)]),
        (110, STONE, [(76, 128), (128, 184)]),
        (78, STONE_LT, [(80, 122), (122, 168)]),
    ]
    holes = {(1, 1), (2, 0)}  # (row_from_bottom, brick_index)
    for ri, (y, fill, xs) in enumerate(rows):
        y1 = y + 34
        for bi, (x0, x1) in enumerate(xs):
            if (ri, bi) in holes:
                _brick(d, x0, y, x1, y1, HOLE)
            else:
                _brick(d, x0, y, x1, y1, fill if bi == 0 else STONE_DK)

    out = HERE / "out_oxalpha_dungeon_wall.png"
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
