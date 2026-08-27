#!/usr/bin/env python3
"""ox-alpha 던전 수정(dungeon_crystal_0) 코드합성 — 256×256 단정 육각 기둥.

배경: dungeon_rubble_* 은 ox-alpha 256인데 dungeon_crystal_0 는 아직 나노바나나
1453×1748·2.7MB 그레이 가면형 수정+눈광이라 톤이 따로 놀았다. 같은 두꺼운
외곽선·같은 256, 단정 육각 기둥으로 교체한다(할로우 눈/가면 없음).
dungeon_crystal_1(군집)·_2(갈라진 쌍봉) 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, dungeon_crystal_0=0.90)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 하나의 육각 수정 기둥 + 작은 석조 밑동. 글로우/원형 눈광/바닥 원 금지.
그림자는 밑 얇은 띠뿐.

사용: python3 gen_oxalpha_dungeon_crystal.py
출력: art/out_oxalpha_dungeon_crystal.png (256×256 RGBA)
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

    d.rectangle([96, 214, 160, 222], fill=(30, 24, 20, 90))

    # 작은 석조 밑동
    d.polygon([(cx - 28, 214), (cx - 22, 198), (cx + 22, 198), (cx + 28, 214)], fill=OUTLINE)
    d.polygon([(cx - 24, 212), (cx - 18, 200), (cx + 18, 200), (cx + 24, 212)], fill=STONE_DK)

    # 육각 기둥 (앞 3면)
    # 왼쪽 면
    d.polygon([(cx - 22, 198), (cx - 18, 78), (cx, 64), (cx, 198)], fill=OUTLINE)
    d.polygon([(cx - 18, 194), (cx - 14, 82), (cx, 72), (cx, 194)], fill=CRYSTAL_DK)
    # 앞면
    d.polygon([(cx, 64), (cx + 18, 78), (cx + 18, 198), (cx, 198)], fill=OUTLINE)
    d.polygon([(cx, 72), (cx + 14, 84), (cx + 14, 194), (cx, 194)], fill=CRYSTAL)
    # 오른쪽 하이라이트 면
    d.polygon([(cx + 18, 78), (cx + 26, 92), (cx + 24, 198), (cx + 18, 198)], fill=OUTLINE)
    d.polygon([(cx + 18, 86), (cx + 22, 96), (cx + 20, 194), (cx + 16, 194)], fill=CRYSTAL_LT)
    # 꼭대기 면
    d.polygon([(cx - 18, 78), (cx, 52), (cx + 18, 78), (cx, 64)], fill=OUTLINE)
    d.polygon([(cx - 12, 78), (cx, 58), (cx + 12, 78), (cx, 68)], fill=CRYSTAL_LT)
    # 패싯 금
    d.line([(cx, 72), (cx, 194)], fill=CRYSTAL_DK, width=2)

    out = HERE / "out_oxalpha_dungeon_crystal.png"
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
