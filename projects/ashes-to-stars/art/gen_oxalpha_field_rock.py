#!/usr/bin/env python3
"""ox-alpha 필드 바위(field_rock_0) 코드합성 — 256×256 웜톤 석조 바위.

배경: 나무·덤불·집은 ox-alpha 256인데 field_rock_0 은 아직 나노바나나
1540×1601·2.2MB 그레이 구멍바위라 톤이 따로 논다. 같은 웜톤 석조 팔레트·같은 256의
솔리드 바위로 교체한다. field_rock_1/2 는 다음 칸.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, field_rock_0=0.90)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 불규칙 다각형 본체 + 벽돌 결·명암(구멍/눈 없음). §6-A: 바닥 큰 원/고리/글로우
금지, 그림자는 밑 얇은 띠뿐.

사용: python3 gen_oxalpha_field_rock.py
출력: art/out_oxalpha_field_rock.png (256×256 RGBA)
"""
from pathlib import Path
import random

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

STONE = (150, 142, 126, 255)
STONE_DK = (108, 100, 86, 255)
STONE_LT = (188, 180, 162, 255)
MORTAR = (86, 80, 68, 255)
OUTLINE = (40, 24, 20, 255)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    rng = random.Random(20260827)
    cx = 128

    d.rectangle([72, 198, 184, 206], fill=(30, 24, 20, 90))

    # 불규칙 바위 실루엣(솔리드 — 구멍 없음)
    body = [
        (88, 176), (78, 148), (92, 118), (118, 104),
        (150, 108), (176, 128), (184, 156), (170, 186),
        (138, 196), (104, 192),
    ]
    d.polygon([(p[0] - 2, p[1] - 2) for p in body], fill=OUTLINE)
    d.polygon(body, fill=STONE)
    # 좌상 하이라이트 / 우하 그늘
    d.polygon([(92, 124), (118, 108), (148, 118), (128, 148), (96, 150)], fill=STONE_LT)
    d.polygon([(150, 150), (176, 132), (180, 158), (164, 184), (140, 176)], fill=STONE_DK)
    # 갈라진 결
    d.line([(100, 140), (132, 168)], fill=MORTAR, width=2)
    d.line([(128, 120), (148, 154)], fill=MORTAR, width=1)
    d.line([(110, 172), (150, 180)], fill=MORTAR, width=1)
    for _ in range(10):
        x = rng.randint(96, 168)
        y = rng.randint(120, 180)
        d.rectangle([x, y, x + 6, y + 3],
                    fill=STONE_DK if rng.random() < 0.5 else STONE_LT)

    out = HERE / "out_oxalpha_field_rock.png"
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
