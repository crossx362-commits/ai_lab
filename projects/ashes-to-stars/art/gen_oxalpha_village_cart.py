#!/usr/bin/env python3
"""ox-alpha 마을 수레(village_cart_0) 코드합성 — 256×256 목조 이륜 손수레.

배경: 영지 8동·마을 집·헛간·우물은 ox-alpha 256(~수 KB)으로 통일됐으나
수레(village_cart_0)는 아직 옛 나노바나나(1740×1310·2.3MB, 그레이)라 톤·해상도가
따로 논다. 같은 웜톤 팔레트·같은 256 캔버스의 목조 수레로 교체한다.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, village_cart_0=1.30)이라 해상도 스왑이 스케일-세이프.
.cs 변경 0. 파일명 동일 → .meta/GUID 무변경, FieldDecor 이름 참조 무변경.

디자인(수레로 확실히 읽히게): 목조 상자 짐칸(가로 널·철띠) + 이륜(근륜 큼·원륜 작음,
살대·허브) + 앞으로 뻗은 채 2본+손잡이 가로대 + 짐칸 건초 더미. §6-A: 바닥 큰
원/고리/글로우 금지 — 그림자는 밑 얇은 띠뿐, 바퀴 살대가 채워 스킬 링으로 안 읽힘.
실루엣 우선·두꺼운 아웃라인의 ox-alpha 결.

사용: python3 gen_oxalpha_village_cart.py
출력: art/out_oxalpha_village_cart.png (256×256 RGBA)
"""
from pathlib import Path
import math
import random

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

# ox-alpha 팔레트 — village_barn/house/well과 동일(영지 계열 저채도 웜톤)
WOOD = (150, 104, 58, 255)
WOOD_DK = (96, 64, 34, 255)
WOOD_LT = (196, 150, 92, 255)
OUTLINE = (40, 24, 20, 255)
GOLD = (216, 172, 96, 255)
HAY = (196, 164, 78, 255)
HAY_DK = (150, 118, 50, 255)
IRON = (86, 80, 68, 255)
IRON_LT = (128, 120, 104, 255)


def _wheel(d, cx, cy, r, spokes=8):
    """살대 목륜. 림·살·허브가 채워 바닥 고리로 안 읽힌다."""
    d.ellipse([cx - r - 3, cy - r - 3, cx + r + 3, cy + r + 3], fill=OUTLINE)
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=WOOD_DK)
    d.ellipse([cx - r + 6, cy - r + 6, cx + r - 6, cy + r - 6], fill=WOOD_LT)
    # 살대
    for i in range(spokes):
        ang = math.radians(i * (360 / spokes) - 20)
        x2 = int(cx + (r - 7) * math.cos(ang))
        y2 = int(cy + (r - 7) * math.sin(ang))
        d.line([(cx, cy), (x2, y2)], fill=OUTLINE, width=4)
        d.line([(cx, cy), (x2, y2)], fill=WOOD, width=2)
    # 안쪽 림
    d.ellipse([cx - r + 5, cy - r + 5, cx + r - 5, cy + r - 5], outline=WOOD_DK, width=3)
    # 허브
    d.ellipse([cx - 10, cy - 10, cx + 10, cy + 10], fill=OUTLINE)
    d.ellipse([cx - 8, cy - 8, cx + 8, cy + 8], fill=IRON)
    d.ellipse([cx - 3, cy - 3, cx + 3, cy + 3], fill=GOLD)


def _hay(d, rng, cx, cy):
    """짐칸 위 건초 — 겹친 타원 + 짚 획."""
    blobs = [
        (cx - 8, cy + 2, 28, 14),
        (cx + 16, cy, 24, 13),
        (cx + 2, cy - 10, 26, 12),
        (cx - 18, cy - 4, 18, 10),
    ]
    for x, y, w, h in blobs:
        d.ellipse([x - w - 2, y - h - 2, x + w + 2, y + h + 2], fill=OUTLINE)
    for x, y, w, h in blobs:
        d.ellipse([x - w, y - h, x + w, y + h], fill=HAY)
        d.ellipse([x - w + 6, y - h + 3, x + 4, y + 2], fill=GOLD)
    for _ in range(22):
        x = rng.randint(cx - 36, cx + 36)
        y = rng.randint(cy - 18, cy + 10)
        d.line([(x, y), (x + rng.randint(-7, 7), y + rng.randint(-4, 4))],
               fill=HAY_DK if rng.random() < 0.55 else GOLD, width=1)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    rng = random.Random(20260827)

    # 밑 얇은 그림자 띠(원 아님 — §6-A)
    d.rectangle([42, 224, 214, 232], fill=(30, 24, 20, 90))

    # 먼 바퀴(왼쪽 뒤)
    _wheel(d, 92, 188, 32, spokes=8)

    # 채(짐칸 뒤로 깔기)
    d.line([(24, 158), (86, 150)], fill=OUTLINE, width=8)
    d.line([(24, 158), (86, 150)], fill=WOOD, width=5)
    d.line([(24, 174), (84, 164)], fill=OUTLINE, width=8)
    d.line([(24, 174), (84, 164)], fill=WOOD, width=5)
    # 손잡이 가로대
    d.rectangle([16, 148, 30, 184], fill=OUTLINE)
    d.rectangle([18, 150, 28, 182], fill=WOOD_LT)
    d.ellipse([14, 144, 32, 156], fill=WOOD)
    d.ellipse([14, 176, 32, 188], fill=WOOD)

    # 차축(두 바퀴 사이)
    d.line([(92, 188), (178, 200)], fill=OUTLINE, width=5)
    d.line([(92, 188), (178, 200)], fill=IRON, width=3)

    # 짐칸 옆면(3/4 사다리꼴)
    side = [(78, 118), (198, 108), (210, 172), (76, 184)]
    d.polygon([(p[0] - 3, p[1] - 2) for p in side], fill=OUTLINE)
    d.polygon(side, fill=WOOD)
    # 가로 널
    for i in range(6):
        t = (i + 1) / 7.0
        y_l = int(118 + (184 - 118) * t)
        y_r = int(108 + (172 - 108) * t)
        col = WOOD_DK if i % 2 == 0 else WOOD_LT
        d.line([(80, y_l), (206, y_r)], fill=col, width=1)
        if i % 2 == 0:
            d.line([(80, y_l + 1), (206, y_r + 1)], fill=WOOD_DK, width=1)
    # 철띠 두 줄
    d.line([(80, 140), (206, 132)], fill=IRON, width=3)
    d.line([(80, 162), (208, 154)], fill=IRON, width=3)
    # 세로 철
    d.line([(96, 120), (94, 178)], fill=IRON_LT, width=2)
    d.line([(186, 112), (198, 168)], fill=IRON_LT, width=2)

    # 윗면(열린 짐칸 — 안쪽이 어둡게)
    top = [(80, 118), (198, 108), (184, 90), (96, 98)]
    d.polygon([(p[0], p[1] - 2) for p in top], fill=OUTLINE)
    d.polygon(top, fill=WOOD_DK)
    d.line([(96, 98), (184, 90)], fill=WOOD_LT, width=2)
    d.line([(80, 118), (96, 98)], fill=WOOD, width=2)
    d.line([(198, 108), (184, 90)], fill=WOOD_LT, width=2)

    # 앞판(오른쪽 짧은 면)
    front = [(198, 108), (210, 172), (196, 164), (184, 90)]
    d.polygon(front, fill=WOOD_LT)
    d.line([(198, 108), (210, 172)], fill=OUTLINE, width=2)
    d.line([(190, 118), (202, 160)], fill=WOOD_DK, width=1)

    # 건초(윗면에서 솟게)
    _hay(d, rng, 138, 92)

    # 가까운 바퀴(오른쪽, 큼 — 실루엣 시그니처, 제일 앞)
    _wheel(d, 178, 200, 44, spokes=8)

    # 채 앞쪽을 한 번 더(가려지지 않게)
    d.line([(24, 158), (78, 150)], fill=OUTLINE, width=6)
    d.line([(24, 158), (78, 150)], fill=WOOD, width=3)
    d.line([(24, 174), (76, 164)], fill=OUTLINE, width=6)
    d.line([(24, 174), (76, 164)], fill=WOOD, width=3)
    d.rectangle([16, 148, 30, 184], fill=OUTLINE)
    d.rectangle([18, 150, 28, 182], fill=WOOD_LT)

    out = HERE / "out_oxalpha_village_cart.png"
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
