#!/usr/bin/env python3
"""ox-alpha 재 뼈(ash_bone_1) 코드합성 — 256×256 C형 갈비/척추.

배경: ash_bone_0 은 ox-alpha 256인데 ash_bone_1 는 아직 나노바나나
1524×1731·2.4MB 그레이 탄 갈비라 톤이 따로 놀았다. 같은 웜 뼈톤·같은 256,
C형 갈비로 교체한다. ash_bone_0(대각 대퇴골) 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, ash_bone_1=0.70)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 왼쪽 큰 원형 속빈 + 오른쪽 위로 굽은 갈비. 글로우/불씨 금지.
그림자는 밑 얇은 띠뿐. bone_0 대퇴골과 실루엣이 갈린다.

사용: python3 gen_oxalpha_ash_bone1.py
출력: art/out_oxalpha_ash_bone1.png (256×256 RGBA)
"""
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

OUTLINE = (40, 24, 20, 255)
BONE = (214, 196, 162, 255)
BONE_DK = (168, 148, 118, 255)
BONE_LT = (232, 218, 188, 255)
CHAR_DK = (48, 40, 34, 255)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)

    d.rectangle([88, 210, 176, 218], fill=(30, 24, 20, 90))

    # C형 몸통 (왼쪽 고리 + 오른쪽 기둥)
    body = [
        (96, 198), (78, 170), (72, 132), (86, 96),
        (118, 78), (148, 86), (168, 110),
        (176, 70), (196, 78), (188, 128),
        (180, 168), (168, 202), (148, 214),
        (132, 198), (140, 164), (136, 128),
        (118, 112), (98, 128), (96, 160), (108, 188),
    ]
    d.polygon([(p[0] - 3, p[1] - 2) for p in body], fill=OUTLINE)
    d.polygon(body, fill=BONE)
    d.polygon([(88, 140), (100, 108), (124, 96), (118, 140)], fill=BONE_LT)
    d.polygon([(156, 120), (176, 90), (182, 150), (164, 180)], fill=BONE_DK)

    # 왼쪽 속빈 (고리로 안 읽히게 살대 없이 뼈로 둘러쌈)
    d.ellipse([86, 108, 138, 164], fill=OUTLINE)
    d.ellipse([92, 114, 132, 158], fill=CHAR_DK)

    # 위쪽 갈고리
    d.polygon([(168, 78), (196, 62), (204, 78), (180, 96)], fill=OUTLINE)
    d.polygon([(172, 80), (194, 66), (198, 76), (178, 92)], fill=BONE_LT)

    # 아래 짧은 발
    d.polygon([(148, 198), (168, 190), (176, 214), (150, 216)], fill=OUTLINE)
    d.polygon([(152, 200), (164, 194), (170, 210), (154, 212)], fill=BONE_DK)

    out = HERE / "out_oxalpha_ash_bone1.png"
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
