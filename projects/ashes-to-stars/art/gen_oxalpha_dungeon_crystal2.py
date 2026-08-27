#!/usr/bin/env python3
"""ox-alpha 던전 수정(dungeon_crystal_2) 코드합성 — 256×256 갈라진 쌍봉.

배경: dungeon_crystal_0/1 은 ox-alpha 256인데 dungeon_crystal_2 는 아직 나노바나나
1454×1798·3.0MB 그레이 갈라진 수정이라 톤이 따로 놀았다. 같은 청회색·같은 256,
가운데 금 난 쌍봉으로 교체한다. dungeon_crystal_0(단정)·_1(군집) 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, dungeon_crystal_2=1.00)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 하나의 덩어리가 세로로 갈라진 두 봉. 글로우/원광 금지.
그림자는 밑 얇은 띠뿐. 단정·군집과 실루엣이 갈린다.

사용: python3 gen_oxalpha_dungeon_crystal2.py
출력: art/out_oxalpha_dungeon_crystal2.png (256×256 RGBA)
"""
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

OUTLINE = (40, 24, 20, 255)
CRYSTAL = (110, 142, 158, 255)
CRYSTAL_DK = (62, 88, 104, 255)
CRYSTAL_LT = (168, 196, 206, 255)
STONE_DK = (86, 82, 76, 255)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx = 128

    d.rectangle([84, 214, 172, 222], fill=(30, 24, 20, 90))

    # 밑동
    d.polygon([(88, 214), (96, 198), (160, 198), (168, 214)], fill=OUTLINE)
    d.polygon([(94, 212), (102, 200), (154, 200), (162, 212)], fill=STONE_DK)

    # 왼쪽 봉
    left = [(96, 198), (92, 140), (108, 72), (124, 108), (122, 198)]
    d.polygon([(p[0] - 3, p[1] - 2) for p in left], fill=OUTLINE)
    d.polygon(left, fill=CRYSTAL_DK)
    d.polygon([(108, 80), (118, 112), (116, 190), (108, 190)], fill=CRYSTAL)

    # 오른쪽 봉 (조금 낮음)
    right = [(134, 198), (136, 120), (148, 86), (168, 128), (164, 198)]
    d.polygon([(p[0] + 2, p[1] - 2) for p in right], fill=OUTLINE)
    d.polygon(right, fill=CRYSTAL)
    d.polygon([(148, 96), (160, 128), (156, 190), (148, 190)], fill=CRYSTAL_LT)

    # 가운데 갈라진 금 (속 채움 — 고리로 안 읽힘)
    d.polygon([(122, 198), (124, 108), (134, 120), (134, 198)], fill=OUTLINE)
    d.polygon([(124, 190), (126, 118), (132, 126), (132, 190)], fill=CRYSTAL_DK)

    out = HERE / "out_oxalpha_dungeon_crystal2.png"
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
