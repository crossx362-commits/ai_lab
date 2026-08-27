#!/usr/bin/env python3
"""ox-alpha 던전 엄폐(dungeon_cover_1) 코드합성 — 256×256 깨진 돌벤치.

배경: dungeon_cover_0 은 ox-alpha 256인데 dungeon_cover_1 는 아직 나노바나나
1926×1485·2.9MB 그레이 벤치라 톤이 따로 놀았다. 같은 차가운 석조·같은 256,
3/4 깨진 벤치로 교체한다. dungeon_cover_0(낮은 벽)·_2(U자 벽) 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, dungeon_cover_1=1.20)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 등받이+좌석+다리 있는 돌벤치, 오른쪽 깨짐. 글로우 금지.
그림자는 밑 얇은 띠뿐. 엄폐벽 _0과 실루엣이 갈린다.

사용: python3 gen_oxalpha_dungeon_cover1.py
출력: art/out_oxalpha_dungeon_cover1.png (256×256 RGBA)
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

    d.rectangle([56, 206, 200, 214], fill=(30, 24, 20, 90))

    # 등받이
    d.polygon([(68, 96), (72, 168), (188, 168), (184, 92), (160, 88), (148, 108), (72, 108)], fill=OUTLINE)
    d.polygon([(74, 100), (76, 164), (182, 164), (178, 98), (162, 94), (150, 112), (76, 112)], fill=STONE_DK)
    d.rectangle([78, 104, 176, 114], fill=STONE_LT)
    # 등받이 왼쪽 깨진 모서리
    d.polygon([(68, 96), (88, 92), (84, 108), (72, 108)], fill=STONE)

    # 좌석
    d.polygon([(64, 164), (72, 148), (196, 148), (188, 168), (72, 168)], fill=OUTLINE)
    d.polygon([(70, 162), (76, 152), (190, 152), (182, 164), (76, 164)], fill=STONE_LT)

    # 다리 (오른쪽 깨짐)
    d.rectangle([78, 166, 92, 208], fill=OUTLINE)
    d.rectangle([80, 168, 90, 206], fill=STONE)
    d.rectangle([158, 166, 172, 200], fill=OUTLINE)
    d.rectangle([160, 168, 170, 198], fill=STONE_DK)
    # 떨어진 조각
    d.polygon([(176, 200), (192, 192), (204, 208), (180, 210)], fill=OUTLINE)
    d.polygon([(180, 200), (190, 196), (198, 206), (182, 208)], fill=STONE)

    out = HERE / "out_oxalpha_dungeon_cover1.png"
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
