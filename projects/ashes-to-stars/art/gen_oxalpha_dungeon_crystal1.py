#!/usr/bin/env python3
"""ox-alpha 던전 수정(dungeon_crystal_1) 코드합성 — 256×256 수정 군집.

배경: dungeon_crystal_0 은 ox-alpha 256인데 dungeon_crystal_1 는 아직 나노바나나
1324×1740·2.9MB 그레이 수정 군집이라 톤이 따로 놀았다. 같은 청회색·같은 256,
세 기둥 군집으로 교체한다. dungeon_crystal_0(단정)·_2(갈라진 쌍봉) 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, dungeon_crystal_1=1.40)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 키 다른 육각 기둥 3개 + 작은 밑 파편. 글로우/원광 금지.
그림자는 밑 얇은 띠뿐. 단정 _0과 실루엣이 갈린다.

사용: python3 gen_oxalpha_dungeon_crystal1.py
출력: art/out_oxalpha_dungeon_crystal1.png (256×256 RGBA)
"""
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

OUTLINE = (40, 24, 20, 255)
CRYSTAL = (110, 142, 158, 255)
CRYSTAL_DK = (62, 88, 104, 255)
CRYSTAL_LT = (168, 196, 206, 255)
STONE_DK = (86, 82, 76, 255)


def _shard(d, cx, y_top, y_bot, w, fill, hi=True):
    d.polygon([
        (cx - w - 2, y_bot + 2), (cx - w, y_top + 12), (cx, y_top - 4),
        (cx + w, y_top + 12), (cx + w + 2, y_bot + 2),
    ], fill=OUTLINE)
    d.polygon([
        (cx - w, y_bot), (cx - w + 3, y_top + 14), (cx, y_top),
        (cx + w - 3, y_top + 14), (cx + w, y_bot),
    ], fill=fill)
    if hi:
        d.polygon([(cx, y_top + 4), (cx + w - 6, y_top + 16), (cx + w - 6, y_bot - 4), (cx, y_bot - 4)], fill=CRYSTAL_LT)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)

    d.rectangle([72, 214, 184, 222], fill=(30, 24, 20, 90))

    # 밑동
    d.polygon([(80, 214), (88, 198), (168, 198), (176, 214)], fill=OUTLINE)
    d.polygon([(86, 212), (94, 200), (162, 200), (170, 212)], fill=STONE_DK)

    # 세 기둥 (뒤→앞, 키 다름)
    _shard(d, 96, 92, 200, 16, CRYSTAL_DK, hi=False)
    _shard(d, 160, 108, 200, 14, CRYSTAL, hi=True)
    _shard(d, 128, 58, 200, 18, CRYSTAL, hi=True)
    # 작은 밑 파편
    _shard(d, 112, 168, 204, 8, CRYSTAL_LT, hi=False)

    out = HERE / "out_oxalpha_dungeon_crystal1.png"
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
