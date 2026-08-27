#!/usr/bin/env python3
"""ox-alpha 던전 기둥(dungeon_pillar_1) 코드합성 — 256×256 원주.

배경: dungeon_pillar_0 은 ox-alpha 256인데 dungeon_pillar_1 는 아직 나노바나나
549×1798·1.1MB 그레이 원주+해골 띠라 톤이 따로 놀았다. 같은 차가운 석조
·같은 256, 원통 기둥+장식 띠로 교체한다(해골/눈 없음). dungeon_pillar_0
(각진 석주)·_2(부러진 꼭대기) 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, dungeon_pillar_1=2.40)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 원통 기둥 + 위아래 장식 띠(얼굴/해골 아님). 글로우 금지.
그림자는 밑 얇은 띠뿐. 각진 _0과 실루엣이 갈린다.

사용: python3 gen_oxalpha_dungeon_pillar1.py
출력: art/out_oxalpha_dungeon_pillar1.png (256×256 RGBA)
"""
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

OUTLINE = (40, 24, 20, 255)
STONE = (132, 128, 122, 255)
STONE_DK = (86, 82, 76, 255)
STONE_LT = (172, 166, 156, 255)
BONE = (214, 196, 162, 255)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx = 128

    d.rectangle([100, 226, 156, 234], fill=(30, 24, 20, 90))

    # 밑동
    d.ellipse([cx - 28, 210, cx + 28, 232], fill=OUTLINE)
    d.ellipse([cx - 24, 214, cx + 24, 228], fill=STONE_DK)

    # 원통 몸통
    d.rectangle([cx - 20, 48, cx + 20, 218], fill=OUTLINE)
    d.rectangle([cx - 16, 52, cx + 16, 214], fill=STONE)
    d.rectangle([cx - 14, 54, cx - 6, 212], fill=STONE_LT)
    d.rectangle([cx + 8, 54, cx + 14, 212], fill=STONE_DK)

    # 위·아래 장식 띠 (해골 아님, 단순 링)
    for y in (72, 176):
        d.ellipse([cx - 24, y, cx + 24, y + 18], fill=OUTLINE)
        d.ellipse([cx - 20, y + 3, cx + 20, y + 15], fill=BONE)
        d.rectangle([cx - 8, y + 6, cx + 8, y + 12], fill=STONE_DK)

    # 주두
    d.ellipse([cx - 26, 36, cx + 26, 60], fill=OUTLINE)
    d.ellipse([cx - 22, 40, cx + 22, 56], fill=STONE_LT)

    out = HERE / "out_oxalpha_dungeon_pillar1.png"
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
