#!/usr/bin/env python3
"""ox-alpha 마을 우물(village_well_0) 코드합성 — 256×256 석조·목조 우물.

배경(오너 2026-08-26 「게임 ui 좀더 시각화·이미지화·퀄리티 업」): 영지 8동·필드 마을
집(village_house_0)·헛간(village_barn_0)은 ox-alpha 코드합성(256×256, ~수 KB)으로
통일됐으나, 우물(village_well_0)은 아직 옛 나노바나나(1508×1852·3.2MB)라 톤·해상도가
영지·집·헛간과 따로 논다. 같은 팔레트·같은 256 캔버스의 석조 우물로 교체한다.

**크기 무영향(실측 확인, FieldDecor.cs:336)**: 프랍의 인게임 크기는
`ppu = pxHeight / TargetUnits(prop_scale.json)`으로 목표 유닛(village_well_0=1.60)에서
역산되므로 원본 해상도(1852→256)를 바꿔도 월드 크기는 1.60유닛 그대로다. 그래서 아트만
교체하면 되고, 소비 경로(FieldDecor 이름 참조)·.meta/GUID는 무변경이다. village_barn과
같은 접근(파일명 동일 스왑, .cs 변경 0, game_asset_names.py + PIL 육안 검증).

디자인(우물로 확실히 읽히게): 하단 원통형 석조 우물통(정면 벽돌 줄·타원 림) + 좌우 목재
지지 기둥 2본 + 상단 완만한 박공 널지붕 + 가로 도르래 빔(크랭크 손잡이) + 매달린 두레박
(밧줄). §6-A 준수: 바닥에 큰 원/고리/글로우 금지 — 우물통 자체가 실체(정면이 보이는
원통)라 스킬 링으로 안 읽히게 앞면을 세워 그리고, 그림자는 밑 얇은 띠뿐. 실루엣 우선·
두꺼운 아웃라인의 ox-alpha 결.

사용: python3 gen_oxalpha_village_well.py
출력: art/out_oxalpha_village_well.png (256×256 RGBA)
"""
from pathlib import Path
import random

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

# ox-alpha 팔레트 — village_barn/house와 동일(영지 계열 저채도 웜톤)
STONE = (150, 142, 126, 255)
STONE_DK = (108, 100, 86, 255)
STONE_LT = (188, 180, 162, 255)
MORTAR = (86, 80, 68, 255)
WOOD = (150, 104, 58, 255)
WOOD_DK = (96, 64, 34, 255)
WOOD_LT = (196, 150, 92, 255)
ROOF = (128, 88, 52, 255)
ROOF_DK = (92, 60, 34, 255)
ROOF_LT = (170, 128, 78, 255)
OUTLINE = (40, 24, 20, 255)
GOLD = (216, 172, 96, 255)
WATER = (70, 86, 96, 255)


def _well_barrel(d, rng, cx, top_y, bot_y, half_w):
    """원통 석조 우물통 — 앞면이 서 있는 원통(윗면 타원 림 + 정면 벽돌)."""
    x0, x1 = cx - half_w, cx + half_w
    # 정면 원통 몸통
    d.rectangle([x0 - 3, top_y - 2, x1 + 3, bot_y + 3], fill=OUTLINE)
    d.rectangle([x0, top_y, x1, bot_y], fill=STONE)
    # 하단은 원통이라 좌우가 어두워지는 명암(둥근 느낌)
    d.rectangle([x0, top_y, x0 + 10, bot_y], fill=STONE_DK)
    d.rectangle([x1 - 10, top_y, x1, bot_y], fill=STONE_DK)
    # 정면 벽돌 줄
    row_h = 12
    y = top_y + 4
    ri = 0
    while y < bot_y - 2:
        off = (ri % 2) * 16
        d.line([(x0 + 1, y), (x1 - 1, y)], fill=MORTAR, width=1)
        bx = x0 + 6 + off
        while bx < x1 - 4:
            d.line([(bx, y + 1), (bx, min(bot_y - 2, y + row_h - 1))], fill=MORTAR, width=1)
            bx += 26
        for _ in range((x1 - x0) // 22):
            sx = rng.randint(x0 + 4, x1 - 12)
            sy = rng.randint(y + 2, min(bot_y - 4, y + row_h - 3))
            d.rectangle([sx, sy, sx + 6, sy + 3],
                        fill=STONE_DK if rng.random() < 0.5 else STONE_LT)
        y += row_h
        ri += 1
    # 윗면 림(타원) — 물이 보이는 입구
    ry = 9
    d.ellipse([x0 - 2, top_y - ry - 2, x1 + 2, top_y + ry + 2], fill=OUTLINE)
    d.ellipse([x0, top_y - ry, x1, top_y + ry], fill=STONE_LT)
    d.ellipse([x0 + 7, top_y - ry + 4, x1 - 7, top_y + ry - 3], fill=STONE_DK)
    d.ellipse([x0 + 12, top_y - ry + 6, x1 - 12, top_y + ry - 5], fill=WATER)
    # 물에 반짝(작은 밝은 획, 글로우 아님)
    d.line([(cx - 6, top_y + 1), (cx + 2, top_y + 1)], fill=(150, 168, 178, 255), width=1)


def _posts(d, lx, rx, top_y, bot_y):
    """좌우 목재 지지 기둥 2본 — 지붕을 받친다."""
    for px in (lx, rx):
        d.rectangle([px - 5, top_y, px + 5, bot_y], fill=OUTLINE)
        d.rectangle([px - 4, top_y + 1, px + 4, bot_y], fill=WOOD)
        d.line([(px - 1, top_y + 2), (px - 1, bot_y - 1)], fill=WOOD_LT, width=1)
        d.line([(px + 2, top_y + 2), (px + 2, bot_y - 1)], fill=WOOD_DK, width=1)


def _roof(d, cx, apex_y, base_y, half_w):
    """완만한 박공 널지붕 — 겹친 널 줄·명암."""
    lx = cx - half_w
    rx = cx + half_w
    d.polygon([(cx, apex_y - 3), (lx - 3, base_y + 3), (rx + 3, base_y + 3)], fill=OUTLINE)
    d.polygon([(cx, apex_y), (lx, base_y), (rx, base_y)], fill=ROOF)
    rows = 6
    for i in range(1, rows):
        t = i / rows
        yy = int(apex_y + (base_y - apex_y) * t)
        hw = int(half_w * t)
        col = ROOF_DK if i % 2 == 0 else ROOF_LT
        d.line([(cx - hw, yy), (cx + hw, yy)], fill=col, width=1)
        for sx in range(cx - hw + 6, cx + hw - 4, 18):
            d.line([(sx, yy), (sx, yy + int((base_y - apex_y) / rows))], fill=ROOF_DK, width=1)
    d.line([(cx, apex_y), (lx, base_y)], fill=ROOF_LT, width=2)
    d.line([(cx, apex_y), (rx, base_y)], fill=ROOF_DK, width=2)


def _winch(d, lx, rx, y, cx, rim_y):
    """가로 도르래 빔 + 크랭크 손잡이 + 밧줄·두레박(우물 시그니처)."""
    # 빔(좌우 기둥 사이 가로)
    d.rectangle([lx - 4, y - 4, rx + 4, y + 4], fill=OUTLINE)
    d.rectangle([lx - 3, y - 3, rx + 3, y + 3], fill=WOOD)
    d.line([(lx - 3, y - 1), (rx + 3, y - 1)], fill=WOOD_LT, width=1)
    # 크랭크 손잡이(오른쪽)
    d.line([(rx + 3, y), (rx + 12, y)], fill=OUTLINE, width=3)
    d.line([(rx + 12, y), (rx + 12, y + 9)], fill=OUTLINE, width=3)
    d.ellipse([rx + 9, y + 7, rx + 15, y + 13], fill=WOOD_LT, outline=OUTLINE)
    # 밧줄 + 두레박(중앙에서 림 위로)
    d.line([(cx, y + 3), (cx, rim_y - 10)], fill=(70, 56, 40, 255), width=2)
    d.rectangle([cx - 7, rim_y - 12, cx + 7, rim_y - 2], fill=OUTLINE)
    d.rectangle([cx - 6, rim_y - 11, cx + 6, rim_y - 3], fill=WOOD)
    # 두레박 테(철)
    d.line([(cx - 6, rim_y - 8), (cx + 6, rim_y - 8)], fill=STONE_DK, width=1)
    d.line([(cx - 6, rim_y - 4), (cx + 6, rim_y - 4)], fill=STONE_DK, width=1)
    # 손잡이 활
    d.arc([cx - 6, rim_y - 16, cx + 6, rim_y - 8], 180, 360, fill=(70, 56, 40, 255), width=1)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    rng = random.Random(20260827)  # 결정론 — 석재 명암 노이즈 재현 가능

    cx = 128
    # 밑 얇은 그림자 띠(원 아님 — §6-A)
    d.rectangle([74, 224, 182, 230], fill=(30, 24, 20, 90))

    # 우물통(정면 원통) — 하부
    barrel_top = 150
    barrel_bot = 224
    barrel_hw = 52
    _well_barrel(d, rng, cx, barrel_top, barrel_bot, barrel_hw)

    # 좌우 지지 기둥(림 뒤에서 지붕까지)
    lx, rx = cx - 40, cx + 40
    _posts(d, lx, rx, 78, barrel_top - 6)

    # 지붕(완만한 박공)
    _roof(d, cx, 44, 82, half_w=68)

    # 도르래 빔 + 크랭크 + 두레박
    _winch(d, lx, rx, 92, cx, barrel_top)

    out = HERE / "out_oxalpha_village_well.png"
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
