#!/usr/bin/env python3
"""ox-alpha 던전 기둥(dungeon_pillar_2) 코드합성 — 256×256 부러진 석주.

배경: dungeon_pillar_0/1 은 ox-alpha 256인데 dungeon_pillar_2 는 아직 나노바나나
1100×1551·1.3MB 그레이 부러진 기둥이라 톤이 따로 놀았다. 같은 차가운 석조
·같은 256, V자 부러진 꼭대기로 교체한다. dungeon_pillar_0(각진)·_1(원주) 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, dungeon_pillar_2=2.20)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 주두 없는 석주, 꼭대기 V자 파손 + 세로 금. 글로우 금지.
그림자는 밑 얇은 띠뿐. 온전한 _0·원주 _1과 실루엣이 갈린다.

사용: python3 gen_oxalpha_dungeon_pillar2.py
출력: art/out_oxalpha_dungeon_pillar2.png (256×256 RGBA)
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

    d.rectangle([96, 222, 160, 230], fill=(30, 24, 20, 90))

    # 밑동
    d.polygon([(cx - 30, 224), (cx - 26, 204), (cx + 26, 204), (cx + 30, 224)], fill=OUTLINE)
    d.polygon([(cx - 26, 222), (cx - 22, 206), (cx + 22, 206), (cx + 26, 222)], fill=STONE_DK)

    # 몸통 — 꼭대기 V자
    body = [
        (cx - 18, 206),
        (cx - 16, 88),
        (cx - 4, 52),   # 왼쪽 봉
        (cx + 4, 78),   # V 바닥
        (cx + 18, 48),  # 오른쪽 봉
        (cx + 20, 90),
        (cx + 22, 206),
    ]
    d.polygon([(p[0] - 3, p[1] - 2) for p in body], fill=OUTLINE)
    d.polygon(body, fill=STONE)
    d.polygon([(cx - 12, 200), (cx - 10, 90), (cx + 2, 80), (cx + 2, 200)], fill=STONE_LT)
    d.polygon([(cx + 8, 200), (cx + 10, 92), (cx + 16, 60), (cx + 16, 200)], fill=STONE_DK)
    # 세로 금
    d.line([(cx + 2, 80), (cx + 2, 200)], fill=OUTLINE, width=2)
    # 꼭대기 깨진 면
    d.polygon([(cx - 4, 56), (cx + 4, 78), (cx + 2, 88), (cx - 8, 72)], fill=STONE_LT)

    out = HERE / "out_oxalpha_dungeon_pillar2.png"
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
