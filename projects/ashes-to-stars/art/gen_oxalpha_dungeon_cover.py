#!/usr/bin/env python3
"""ox-alpha 던전 엄폐(dungeon_cover_0) 코드합성 — 256×256 낮은 돌벽.

배경: dungeon_crystal_* 은 ox-alpha 256인데 dungeon_cover_0 는 아직 나노바나나
1725×1591·2.7MB 그레이 돌벽+눈광이라 톤이 따로 놀았다. 같은 차가운 석조
·같은 256, 낮은 엄폐벽으로 교체한다(눈/글로우 없음). dungeon_cover_1(벤치)
·_2(U자 벽) 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, dungeon_cover_0=1.20)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 가로로 긴 2단 석조 엄폐벽, 위 들쭉날쭉. 눈/생물/글로우 금지.
그림자는 밑 얇은 띠뿐. rubble 무더기·벽돌단과 실루엣이 갈린다.

사용: python3 gen_oxalpha_dungeon_cover.py
출력: art/out_oxalpha_dungeon_cover.png (256×256 RGBA)
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

    d.rectangle([36, 206, 220, 214], fill=(30, 24, 20, 90))

    # 아래 단 (넓은 기초)
    _block(d, 44, 168, 112, 208, STONE_DK)
    _block(d, 112, 172, 176, 208, STONE)
    _block(d, 176, 168, 216, 208, STONE_DK)
    # 위 단 들쭉날쭉
    _block(d, 52, 132, 96, 172, STONE)
    _block(d, 96, 120, 148, 172, STONE_LT)
    _block(d, 148, 136, 188, 172, STONE)
    _block(d, 188, 144, 208, 172, STONE_DK)
    # 꼭대기 뾰족 돌 두 개
    d.polygon([(108, 122), (118, 98), (132, 122)], fill=OUTLINE)
    d.polygon([(112, 120), (118, 104), (128, 120)], fill=STONE_LT)
    d.polygon([(156, 138), (168, 114), (178, 138)], fill=OUTLINE)
    d.polygon([(160, 136), (168, 118), (174, 136)], fill=STONE)

    out = HERE / "out_oxalpha_dungeon_cover.png"
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
