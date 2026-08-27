#!/usr/bin/env python3
"""ox-alpha 던전 잔해(dungeon_rubble_0) 코드합성 — 256×256 깨진 돌무더기.

배경: ash_* 은 ox-alpha 256인데 dungeon_rubble_0 는 아직 나노바나나
1838×1361·3.3MB 그레이 돌무더기라 톤이 따로 놀았다. 같은 두꺼운 외곽선
·같은 256, 차가운 석조 무더기로 교체한다. dungeon_rubble_1(쌓인 벽돌)·_2
(각진 블록) 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, dungeon_rubble_0=0.60)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 각진 석조 파편 4~5개 무더기(필드 둥근 바위와 구별). 해골 구멍·
글로우·할로우 금지. 그림자는 밑 얇은 띠뿐.

사용: python3 gen_oxalpha_dungeon_rubble.py
출력: art/out_oxalpha_dungeon_rubble.png (256×256 RGBA)
"""
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

OUTLINE = (40, 24, 20, 255)
STONE = (132, 128, 122, 255)
STONE_DK = (86, 82, 76, 255)
STONE_LT = (172, 166, 156, 255)


def _chunk(d, pts, fill, hi=None):
    d.polygon([(p[0] - 2, p[1] - 2) for p in pts], fill=OUTLINE)
    d.polygon(pts, fill=fill)
    if hi:
        d.polygon(hi, fill=STONE_LT)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)

    d.rectangle([72, 204, 184, 212], fill=(30, 24, 20, 90))

    # 아래 큰 파편
    _chunk(d, [(78, 196), (72, 168), (110, 150), (148, 168), (140, 200), (96, 208)],
           STONE_DK, hi=[(88, 170), (108, 156), (118, 172)])
    _chunk(d, [(140, 198), (150, 164), (186, 158), (196, 186), (176, 206), (148, 206)],
           STONE, hi=[(158, 168), (176, 162), (178, 178)])
    # 위 작은 파편
    _chunk(d, [(104, 158), (98, 132), (128, 118), (148, 138), (136, 162)],
           STONE, hi=[(112, 136), (126, 124), (132, 140)])
    _chunk(d, [(128, 148), (142, 128), (168, 132), (162, 156), (140, 160)],
           STONE_DK)
    _chunk(d, [(116, 128), (122, 108), (140, 112), (134, 130)],
           STONE_LT)

    out = HERE / "out_oxalpha_dungeon_rubble.png"
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
