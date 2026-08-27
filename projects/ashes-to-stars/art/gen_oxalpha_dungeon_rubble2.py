#!/usr/bin/env python3
"""ox-alpha 던전 잔해(dungeon_rubble_2) 코드합성 — 256×256 각진 석조 블록.

배경: dungeon_rubble_0/1 은 ox-alpha 256인데 dungeon_rubble_2 는 아직 나노바나나
1648×1626·3.6MB 그레이 각진 블록+벌레 조각이라 톤이 따로 놀았다. 같은 차가운
석조·같은 256, 깨진 직육면체로 교체한다(할로우 벌레 없음). dungeon_rubble_0
(파편 무더기)·_1(3단 벽돌) 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, dungeon_rubble_2=0.65)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 3/4 각 깨진 석조 블록, 우상단 결손, 세로 금. 생물/뿔 실루엣 금지.
글로우 금지. 그림자는 밑 얇은 띠뿐.

사용: python3 gen_oxalpha_dungeon_rubble2.py
출력: art/out_oxalpha_dungeon_rubble2.png (256×256 RGBA)
"""
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

OUTLINE = (40, 24, 20, 255)
STONE = (132, 128, 122, 255)
STONE_DK = (86, 82, 76, 255)
STONE_LT = (172, 166, 156, 255)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx = 128

    d.rectangle([78, 206, 178, 214], fill=(30, 24, 20, 90))

    # 앞면
    front = [(86, 196), (86, 118), (148, 100), (176, 118), (176, 188), (148, 206)]
    d.polygon([(p[0] - 3, p[1] - 2) for p in front], fill=OUTLINE)
    d.polygon(front, fill=STONE)
    # 윗면
    top = [(86, 118), (118, 92), (178, 74), (176, 118), (148, 100)]
    d.polygon([(p[0] - 2, p[1] - 2) for p in top], fill=OUTLINE)
    d.polygon(top, fill=STONE_LT)
    # 오른쪽 옆 (결손)
    d.polygon([(176, 118), (178, 74), (196, 92), (188, 170), (176, 188)], fill=OUTLINE)
    d.polygon([(176, 120), (180, 90), (188, 104), (182, 168)], fill=STONE_DK)
    # 우상단 깨진 구멍
    d.polygon([(150, 108), (168, 96), (174, 118), (156, 128)], fill=OUTLINE)
    d.polygon([(154, 110), (166, 102), (170, 116), (158, 124)], fill=STONE_DK)
    # 세로 금
    d.line([(128, 112), (124, 196)], fill=OUTLINE, width=2)
    # 단순 각인 두 줄(생물 아님)
    d.line([(100, 140), (118, 138)], fill=STONE_DK, width=2)
    d.line([(102, 154), (120, 152)], fill=STONE_DK, width=2)
    d.rectangle([104, 164, 112, 176], outline=STONE_DK, width=2)

    out = HERE / "out_oxalpha_dungeon_rubble2.png"
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
