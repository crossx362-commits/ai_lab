#!/usr/bin/env python3
"""ox-alpha 마을 헛간(village_barn_0) 코드합성 — 256×256 목조·석조 헛간.

배경(오너 2026-08-26 「게임 ui 좀더 시각화·이미지화·퀄리티 업」): 영지 8동은 ox-alpha
코드합성(256×256, ~5KB)으로 통일됐고 필드 마을 집(village_house_0)도 직전 바퀴에
교체됐으나, 헛간(village_barn_0)은 아직 옛 나노바나나(1838×1536·3.3MB)라 톤·해상도가
영지·마을 집과 따로 논다. 같은 팔레트·같은 256 캔버스의 목조 헛간으로 교체한다.

**크기 무영향(실측 확인, FieldDecor.cs:336)**: 프랍의 인게임 크기는
`ppu = pxHeight / TargetUnits(prop_scale.json)`으로 목표 유닛(village_barn_0=4.00)에서
역산되므로 원본 해상도(1536→256)를 바꿔도 월드 크기는 4.00유닛 그대로다. 그래서 아트만
교체하면 되고, 소비 경로(FieldDecor 이름 참조)·.meta/GUID는 무변경이다. village_house와
같은 접근(파일명 동일 스왑, SelfCheck 무변경, game_asset_names.py + PIL 육안 검증).

디자인(집보다 넓고 낮게 — 헛간으로 읽히게): 하단 석조 기초 띠 + 넓은 세로 널판 목벽(고전
헛간 판자벽) + 완만한 넓은 박공 널지붕(처마 넓게) + 중앙 대형 아치 쌍여닫이문(X자 가새) +
상단 박공에 건초 다락문(호이스트 빔) + 좌우 작은 창. §6-A 준수: 바닥 큰 원/고리/초승달
금지 — 그림자는 벽 밑 얇은 띠뿐, 글로우 없음. 실루엣 우선·두꺼운 아웃라인의 ox-alpha 결.

사용: python3 gen_oxalpha_village_barn.py
출력: art/out_oxalpha_village_barn.png (256×256 RGBA)
"""
from pathlib import Path
import random

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

# ox-alpha 팔레트 — village_house와 동일(영지 계열 저채도 웜톤)
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


def _stone_base(d, rng, x0, y0, x1, y1):
    """하단 석조 기초 띠 — 낮은 벽돌 줄(집의 석벽을 얕게)."""
    d.rectangle([x0 - 2, y0 - 2, x1 + 2, y1 + 2], fill=OUTLINE)
    d.rectangle([x0, y0, x1, y1], fill=STONE)
    row_h = 11
    y = y0
    ri = 0
    while y < y1:
        off = (ri % 2) * 15
        bx = x0 + 4 + off
        while bx < x1 - 3:
            d.line([(bx, y + 1), (bx, min(y1 - 1, y + row_h - 1))], fill=MORTAR, width=1)
            bx += 28
        yy = min(y1 - 1, y + row_h)
        d.line([(x0 + 1, yy), (x1 - 1, yy)], fill=MORTAR, width=1)
        for _ in range((x1 - x0) // 24):
            sx = rng.randint(x0 + 3, x1 - 12)
            sy = rng.randint(y + 2, min(y1 - 3, y + row_h - 3))
            d.rectangle([sx, sy, sx + 6, sy + 3],
                        fill=STONE_DK if rng.random() < 0.5 else STONE_LT)
        y += row_h
        ri += 1


def _plank_wall(d, x0, y0, x1, y1):
    """세로 널판 목벽 — 고전 헛간 판자벽(세로 널 + 명암 이음선 + 상하 보)."""
    d.rectangle([x0 - 2, y0 - 2, x1 + 2, y1 + 2], fill=OUTLINE)
    d.rectangle([x0, y0, x1, y1], fill=WOOD)
    # 세로 널 이음선(엇갈린 명암으로 판자 결)
    px = x0 + 8
    i = 0
    while px < x1 - 2:
        col = WOOD_DK if i % 2 == 0 else WOOD_LT
        d.line([(px, y0 + 1), (px, y1 - 1)], fill=col, width=1)
        px += 12
        i += 1
    # 상·하 가로 보(띠판)
    for yy in (y0, y1 - 5):
        d.rectangle([x0, yy, x1, yy + 5], fill=WOOD_DK)
        d.line([(x0, yy), (x1, yy)], fill=WOOD_LT, width=1)


def _roof(d, cx, apex_y, base_y, half_w, eave):
    """완만한 넓은 박공 널지붕 — 겹친 널 줄·명암, 처마가 벽 밖으로 넓게."""
    lx = cx - half_w - eave
    rx = cx + half_w + eave
    d.polygon([(cx, apex_y - 3), (lx - 3, base_y + 3), (rx + 3, base_y + 3)], fill=OUTLINE)
    d.polygon([(cx, apex_y), (lx, base_y), (rx, base_y)], fill=ROOF)
    rows = 8
    for i in range(1, rows):
        t = i / rows
        yy = int(apex_y + (base_y - apex_y) * t)
        hw = int(half_w * t) + int(eave * t)
        col = ROOF_DK if i % 2 == 0 else ROOF_LT
        d.line([(cx - hw, yy), (cx + hw, yy)], fill=col, width=1)
        off = (i % 2) * 10
        for sx in range(cx - hw + 6 + off, cx + hw - 4, 20):
            d.line([(sx, yy), (sx, yy + int((base_y - apex_y) / rows))], fill=ROOF_DK, width=1)
    # 용마루·박공 바지널
    d.line([(cx, apex_y + 2), (cx, apex_y + 9)], fill=ROOF_LT, width=2)
    d.line([(cx, apex_y), (lx, base_y)], fill=ROOF_LT, width=2)
    d.line([(cx, apex_y), (rx, base_y)], fill=ROOF_DK, width=2)


def _barn_doors(d, cx, y0, y1):
    """중앙 대형 아치 쌍여닫이문 — 세로 널 + X자 가새 + 철 경첩·손잡이."""
    w = 30  # 반폭(문 전체 60px — 헛간답게 크게)
    d.rectangle([cx - w - 3, y0 - 3, cx + w + 3, y1 + 3], fill=OUTLINE)
    # 아치 상단
    d.pieslice([cx - w, y0 - w, cx + w, y0 + w], 180, 360, fill=WOOD, outline=OUTLINE)
    d.rectangle([cx - w, y0, cx + w, y1], fill=WOOD)
    # 중앙 분할선(쌍여닫이)
    d.line([(cx, y0 - w // 2), (cx, y1)], fill=OUTLINE, width=2)
    # 각 문짝 세로 널
    for sx in range(cx - w + 5, cx + w - 3, 8):
        if abs(sx - cx) < 2:
            continue
        d.line([(sx, y0), (sx, y1 - 2)], fill=WOOD_DK, width=1)
    # X자 가새(문 힘목 — 헛간 시그니처)
    for side in (-1, 1):
        ox = cx + (w // 2) * side
        d.line([(cx + 2 * side, y1 - 3), (ox + (w // 2 - 3) * side, y0 + 4)], fill=WOOD_LT, width=2)
        d.line([(cx + 2 * side, y0 + 4), (ox + (w // 2 - 3) * side, y1 - 3)], fill=WOOD_LT, width=2)
    # 철 경첩 띠(상·하, 좌우 문짝)
    for yy in (y0 + 5, y1 - 7):
        d.line([(cx - w + 1, yy), (cx - 2, yy)], fill=STONE_DK, width=2)
        d.line([(cx + 2, yy), (cx + w - 1, yy)], fill=STONE_DK, width=2)
    # 손잡이 2개
    for dx in (-6, 6):
        d.ellipse([cx + dx - 2, (y0 + y1) // 2, cx + dx + 2, (y0 + y1) // 2 + 4],
                  fill=GOLD, outline=OUTLINE)


def _hayloft(d, cx, y_top):
    """상단 박공 건초 다락문 + 호이스트 빔(헛간 시그니처, §6-A 글로우 없음)."""
    w = 11
    d.rectangle([cx - w - 2, y_top - 2, cx + w + 2, y_top + 20], fill=OUTLINE)
    d.rectangle([cx - w, y_top, cx + w, y_top + 18], fill=WOOD_DK)
    # 다락문 세로 널
    for sx in (cx - 5, cx, cx + 5):
        d.line([(sx, y_top + 1), (sx, y_top + 17)], fill=WOOD, width=1)
    # 호이스트 빔(용마루에서 앞으로 튀어나온 도르래 대)
    d.line([(cx, y_top - 10), (cx, y_top - 2)], fill=WOOD, width=3)
    d.line([(cx, y_top - 10), (cx + 10, y_top - 10)], fill=WOOD, width=3)
    d.ellipse([cx + 8, y_top - 8, cx + 13, y_top - 3], outline=OUTLINE, fill=STONE_DK)


def _window(d, x0, y0):
    """작은 창 — 틀 + 십자 창살 + 저채도 유리."""
    s = 14
    d.rectangle([x0 - 2, y0 - 2, x0 + s + 2, y0 + s + 2], fill=OUTLINE)
    d.rectangle([x0, y0, x0 + s, y0 + s], fill=(120, 128, 120, 255))
    d.line([(x0 + s // 2, y0), (x0 + s // 2, y0 + s)], fill=OUTLINE, width=2)
    d.line([(x0, y0 + s // 2), (x0 + s, y0 + s // 2)], fill=OUTLINE, width=2)
    d.line([(x0 + 2, y0 + 2), (x0 + s // 2 - 2, y0 + 2)], fill=STONE_LT, width=1)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    rng = random.Random(20260827)  # 결정론 — 석재 명암 노이즈 재현 가능

    cx = 128
    # 벽 밑 얇은 그림자 띠(원 아님 — §6-A). 헛간은 넓으니 띠도 넓게.
    d.rectangle([44, 216, 212, 222], fill=(30, 24, 20, 90))

    # 몸통 — 헛간은 집보다 넓고 낮게(널판 목벽 + 얕은 석조 기초)
    _plank_wall(d, 48, 118, 208, 210)
    _stone_base(d, rng, 48, 194, 208, 210)

    # 지붕(완만·넓은 처마)
    _roof(d, cx, 60, 118, half_w=82, eave=10)

    # 상단 박공 건초 다락 + 호이스트
    _hayloft(d, cx, 74)

    # 중앙 대형 쌍여닫이문
    _barn_doors(d, cx, 150, 208)

    # 좌우 작은 창
    _window(d, 66, 150)
    _window(d, 176, 150)

    out = HERE / "out_oxalpha_village_barn.png"
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
