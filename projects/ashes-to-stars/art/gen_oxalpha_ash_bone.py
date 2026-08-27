#!/usr/bin/env python3
"""ox-alpha 재 뼈(ash_bone_0) 코드합성 — 256×256 대각 대퇴골.

배경: ash_charred_* 은 ox-alpha 256인데 ash_bone_0 는 아직 나노바나나
1435×1764·2.7MB 그레이 탄 뼈라 톤이 따로 놀았다. 같은 두꺼운 외곽선·같은 256,
웜 뼈톤 대퇴골로 교체한다. ash_bone_1(갈비형) 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, ash_bone_0=1.20)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 우상→좌하 대각 대퇴골. 위 관절 둥근 머리+속빈, 아래 부러진 끝.
글로우/불씨 금지. 그림자는 밑 얇은 띠뿐. bone_1 갈비 실루엣과 갈린다.

사용: python3 gen_oxalpha_ash_bone.py
출력: art/out_oxalpha_ash_bone.png (256×256 RGBA)
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

    d.rectangle([70, 214, 150, 222], fill=(30, 24, 20, 90))

    # 대퇴골 몸통 (우상 관절 → 좌하 부러진 끝)
    shaft = [
        (168, 78), (186, 92), (178, 118),
        (150, 150), (118, 180), (88, 204),
        (72, 198), (96, 168), (128, 132), (158, 98),
    ]
    d.polygon([(p[0] - 3, p[1] - 2) for p in shaft], fill=OUTLINE)
    d.polygon(shaft, fill=BONE)
    d.polygon([(170, 96), (176, 110), (140, 148), (132, 136)], fill=BONE_LT)
    d.polygon([(110, 168), (124, 176), (92, 198), (84, 190)], fill=BONE_DK)

    # 관절 머리
    d.ellipse([156, 52, 214, 110], fill=OUTLINE)
    d.ellipse([160, 56, 210, 106], fill=BONE)
    d.ellipse([166, 60, 196, 88], fill=BONE_LT)
    # 속빈
    d.ellipse([176, 70, 200, 94], fill=OUTLINE)
    d.ellipse([180, 74, 196, 90], fill=CHAR_DK)

    # 부러진 끝 (들쭉날쭉)
    d.polygon([(68, 188), (86, 196), (74, 214), (52, 204), (58, 190)], fill=OUTLINE)
    d.polygon([(70, 190), (82, 196), (72, 210), (56, 202)], fill=BONE_LT)
    d.polygon([(62, 196), (70, 204), (60, 208)], fill=CHAR_DK)

    out = HERE / "out_oxalpha_ash_bone.png"
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
