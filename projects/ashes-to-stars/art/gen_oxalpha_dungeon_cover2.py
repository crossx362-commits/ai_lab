#!/usr/bin/env python3
"""ox-alpha 던전 엄폐(dungeon_cover_2) 코드합성 — 256×256 U자 돌벽.

배경: dungeon_cover_0/1 은 ox-alpha 256인데 dungeon_cover_2 는 아직 나노바나나
1693×1421·2.5MB 그레이 U자 벽이라 톤이 따로 놀았다. 같은 차가운 석조·같은 256,
가운데 꺼진 U자 엄폐벽으로 교체한다. dungeon_cover_0(낮은 벽)·_1(벤치) 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, dungeon_cover_2=1.30)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 좌우 높은 봉 + 가운데 낮은 턱(U자, 엿보기 가능). 글로우 금지.
그림자는 밑 얇은 띠뿐. 직선 벽·벤치와 실루엣이 갈린다.

사용: python3 gen_oxalpha_dungeon_cover2.py
출력: art/out_oxalpha_dungeon_cover2.png (256×256 RGBA)
"""
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

OUTLINE = (40, 24, 20, 255)
STONE = (132, 128, 122, 255)
STONE_DK = (86, 82, 76, 255)
STONE_LT = (172, 166, 156, 255)


def _block(d, x0, y0, x1, y1, fill):
    d.polygon([(x0 - 2, y0 - 2), (x1 + 2, y0 - 2), (x1 + 2, y1 + 2), (x0 - 2, y1 + 2)], fill=OUTLINE)
    d.polygon([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], fill=fill)
    d.rectangle([x0 + 2, y0 + 2, x1 - 2, y0 + 6], fill=STONE_LT)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)

    d.rectangle([40, 208, 216, 216], fill=(30, 24, 20, 90))

    # 아래 단 (연속 기초)
    _block(d, 48, 172, 208, 210, STONE_DK)
    # 가운데 낮은 턱
    _block(d, 96, 148, 160, 176, STONE)
    # 왼쪽 높은 봉
    _block(d, 48, 96, 96, 176, STONE)
    d.polygon([(56, 98), (72, 72), (88, 98)], fill=OUTLINE)
    d.polygon([(60, 96), (72, 78), (84, 96)], fill=STONE_LT)
    # 오른쪽 높은 봉
    _block(d, 160, 104, 208, 176, STONE)
    d.polygon([(168, 106), (184, 78), (200, 106)], fill=OUTLINE)
    d.polygon([(172, 104), (184, 84), (196, 104)], fill=STONE_LT)

    out = HERE / "out_oxalpha_dungeon_cover2.png"
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
