#!/usr/bin/env python3
"""ox-alpha 던전 잔해(dungeon_rubble_1) 코드합성 — 256×256 쌓인 벽돌 3단.

배경: dungeon_rubble_0 은 ox-alpha 256인데 dungeon_rubble_1 는 아직 나노바나나
1701×1458·2.0MB 그레이 벽돌 단이라 톤이 따로 놀았다. 같은 차가운 석조·같은 256,
3단 벽돌로 교체한다. dungeon_rubble_0(파편 무더기)·_2(각진 블록) 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, dungeon_rubble_1=0.55)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 위에서 본 3단 석조 벽돌(가로 이음). 무더기 _0과 실루엣이 갈린다.
글로우·할로우 금지. 그림자는 밑 얇은 띠뿐.

사용: python3 gen_oxalpha_dungeon_rubble1.py
출력: art/out_oxalpha_dungeon_rubble1.png (256×256 RGBA)
"""
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

OUTLINE = (40, 24, 20, 255)
STONE = (132, 128, 122, 255)
STONE_DK = (86, 82, 76, 255)
STONE_LT = (172, 166, 156, 255)


def _brick(d, x0, y0, x1, y1, fill):
    d.polygon([(x0 - 2, y0 - 2), (x1 + 2, y0 - 2), (x1 + 2, y1 + 2), (x0 - 2, y1 + 2)], fill=OUTLINE)
    d.polygon([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], fill=fill)
    # 윗면 하이라이트
    d.rectangle([x0 + 2, y0 + 2, x1 - 2, y0 + 7], fill=STONE_LT)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)

    d.rectangle([64, 196, 192, 204], fill=(30, 24, 20, 90))

    # 아래 단 (넓은 기초)
    _brick(d, 72, 168, 184, 198, STONE_DK)
    # 가운데 단
    _brick(d, 80, 140, 132, 170, STONE)
    _brick(d, 132, 140, 176, 170, STONE_DK)
    # 위 단 3장
    _brick(d, 86, 114, 118, 142, STONE_LT)
    _brick(d, 118, 114, 148, 142, STONE)
    _brick(d, 148, 114, 170, 142, STONE_DK)
    # 가운데 세로 금
    d.line([(128, 118), (128, 168)], fill=OUTLINE, width=2)

    out = HERE / "out_oxalpha_dungeon_rubble1.png"
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
