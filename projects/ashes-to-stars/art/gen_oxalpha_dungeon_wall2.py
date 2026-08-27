#!/usr/bin/env python3
"""ox-alpha 던전 벽(dungeon_wall_2) 코드합성 — 256×256 들쭉날쭉 바위 벽.

배경: dungeon_wall_0/1 은 ox-alpha 256인데 dungeon_wall_2 는 아직 나노바나나
1499×1492·2.4MB 그레이 바위 벽이라 톤이 따로 놀았다. 같은 차가운 석조·같은 256,
각진 바위 벽으로 교체한다. dungeon_wall_0(벽돌)·_1(아치) 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, dungeon_wall_2=1.50)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 밑이 넓고 위가 들쭉날쭉한 바위 벽(벽돌/아치와 실루엣이 갈림).
글로우 금지. 그림자는 밑 얇은 띠뿐.

사용: python3 gen_oxalpha_dungeon_wall2.py
출력: art/out_oxalpha_dungeon_wall2.png (256×256 RGBA)
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

    d.rectangle([64, 214, 192, 222], fill=(30, 24, 20, 90))

    # 바깥 실루엣
    body = [
        (68, 212), (72, 168), (64, 128), (88, 92),
        (108, 72), (128, 96), (148, 68), (176, 88),
        (192, 128), (186, 168), (188, 212),
    ]
    d.polygon([(p[0] - 3, p[1] - 2) for p in body], fill=OUTLINE)
    d.polygon(body, fill=STONE)
    # 왼쪽 그늘 / 오른쪽 하이라이트
    d.polygon([(76, 200), (80, 140), (100, 88), (112, 200)], fill=STONE_DK)
    d.polygon([(148, 200), (160, 96), (176, 120), (172, 200)], fill=STONE_LT)
    # 갈라진 금
    d.line([(128, 100), (124, 208)], fill=OUTLINE, width=2)
    d.line([(108, 140), (148, 148)], fill=STONE_DK, width=2)

    out = HERE / "out_oxalpha_dungeon_wall2.png"
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
