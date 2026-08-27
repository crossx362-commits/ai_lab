#!/usr/bin/env python3
"""ox-alpha 마을 큰 집(village_house_1) 코드합성 — 256×256 2층 하프팀버 저택.

배경(오너 2026-08-26 「게임 ui 좀더 시각화·이미지화·퀄리티 업」): 영지 8동·마을
집(village_house_0)·헛간·우물은 ox-alpha 코드합성(256×256, ~3KB)으로 통일됐으나
village_house_1(2.0MB 나노바나나 2000px)은 아직 옛 톤·해상도라 마을에서 따로 논다.
같은 웜톤 팔레트·같은 256 캔버스로 교체한다.

**house_0과 구분**: prop_scale.json에서 village_house_1=4.6유닛으로 마을에서 가장 크다
(house_0=3.6·house_2=2.8). 그래서 **2층 저택**으로 그린다 — 하단 석벽 1층 + 상단 목조
2층(살짝 내민 젯티 오버행) + 급경사 박공 널지붕 + 2층 정면 창 2개 + 1층 아치문·창 +
좌우 석조 굴뚝 2개. house_0(단층 오두막)보다 확실히 크고 격 있는 실루엣으로 읽힌다.

**크기 무영향(실측 확인, FieldDecor.cs:336)**: 프랍의 인게임 크기는
`ppu = pxHeight / TargetUnits(prop_scale.json)`으로 목표 유닛(village_house_1=4.6)에서
역산되므로 원본 해상도(2000→256)를 바꿔도 월드 크기는 4.6유닛 그대로다. .cs 변경 0.

§6-A 준수: 바닥에 큰 원/고리/초승달 금지 — 그림자는 벽 밑 얇은 띠뿐, 글로우 없음.
house_0의 팔레트·헬퍼 결(두꺼운 아웃라인·실루엣 우선)을 그대로 계승한다.

사용: python3 gen_oxalpha_village_house1.py
출력: art/out_oxalpha_village_house1.png (256×256 RGBA)
"""
from pathlib import Path
import random

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

# ox-alpha 팔레트 — village_house_0과 동일(마을 톤 통일)
STONE = (150, 142, 126, 255)
STONE_DK = (108, 100, 86, 255)
STONE_LT = (188, 180, 162, 255)
MORTAR = (86, 80, 68, 255)
WOOD = (150, 104, 58, 255)
WOOD_DK = (96, 64, 34, 255)
WOOD_LT = (196, 150, 92, 255)
PLASTER = (206, 190, 158, 255)
PLASTER_DK = (170, 152, 120, 255)
ROOF = (128, 88, 52, 255)
ROOF_DK = (92, 60, 34, 255)
ROOF_LT = (170, 128, 78, 255)
OUTLINE = (40, 24, 20, 255)
GOLD = (216, 172, 96, 255)


def _stone_wall(d, rng, x0, y0, x1, y1):
    """하단 석벽 — 아웃라인 박스 + 어긋난 벽돌 줄(모르타르) + 명암 결."""
    d.rectangle([x0 - 2, y0 - 2, x1 + 2, y1 + 2], fill=OUTLINE)
    d.rectangle([x0, y0, x1, y1], fill=STONE)
    row_h = 13
    y = y0
    ri = 0
    while y < y1:
        off = (ri % 2) * 16
        bx = x0 + 4 + off
        while bx < x1 - 3:
            d.line([(bx, y + 1), (bx, min(y1 - 1, y + row_h - 1))], fill=MORTAR, width=1)
            bx += 30
        yy = min(y1 - 1, y + row_h)
        d.line([(x0 + 1, yy), (x1 - 1, yy)], fill=MORTAR, width=1)
        lo, hi = y + 2, min(y1 - 3, y + row_h - 3)
        if lo <= hi:
            for _ in range((x1 - x0) // 22):
                sx = rng.randint(x0 + 3, x1 - 12)
                sy = rng.randint(lo, hi)
                d.rectangle([sx, sy, sx + 6, sy + 3],
                            fill=STONE_DK if rng.random() < 0.5 else STONE_LT)
        y += row_h
        ri += 1


def _timber_upper(d, x0, y0, x1, y1, studs):
    """목조 층 — 크림 회벽 패널 + 갈색 세로 스터드 + 상·하 가로 보.

    studs: 세로 스터드 x좌표 목록(층 폭에 맞춰 호출부가 준다)."""
    d.rectangle([x0 - 2, y0 - 2, x1 + 2, y1 + 2], fill=OUTLINE)
    d.rectangle([x0, y0, x1, y1], fill=PLASTER)
    d.rectangle([x0, y1 - 6, x1, y1], fill=PLASTER_DK)
    for yy in (y0, y1 - 4):
        d.rectangle([x0, yy, x1, yy + 4], fill=WOOD)
        d.line([(x0, yy), (x1, yy)], fill=WOOD_LT, width=1)
        d.line([(x0, yy + 4), (x1, yy + 4)], fill=WOOD_DK, width=1)
    for sx in studs:
        d.rectangle([sx - 3, y0, sx + 3, y1], fill=WOOD)
        d.line([(sx - 3, y0), (sx - 3, y1)], fill=WOOD_LT, width=1)
        d.line([(sx + 3, y0), (sx + 3, y1)], fill=WOOD_DK, width=1)


def _roof(d, cx, apex_y, base_y, half_w, eave):
    """급경사 박공 나무 널지붕 — 겹친 널 줄·명암, 처마가 벽 밖으로 나온다."""
    lx = cx - half_w - eave
    rx = cx + half_w + eave
    d.polygon([(cx, apex_y - 3), (lx - 3, base_y + 3), (rx + 3, base_y + 3)], fill=OUTLINE)
    d.polygon([(cx, apex_y), (lx, base_y), (rx, base_y)], fill=ROOF)
    rows = 10
    for i in range(1, rows):
        t = i / rows
        yy = int(apex_y + (base_y - apex_y) * t)
        hw = int(half_w * t) + int(eave * t)
        col = ROOF_DK if i % 2 == 0 else ROOF_LT
        d.line([(cx - hw, yy), (cx + hw, yy)], fill=col, width=1)
        off = (i % 2) * 9
        for sx in range(cx - hw + 6 + off, cx + hw - 4, 18):
            d.line([(sx, yy), (sx, yy + int((base_y - apex_y) / rows))], fill=ROOF_DK, width=1)
    d.line([(cx, apex_y + 2), (cx, apex_y + 12)], fill=ROOF_LT, width=2)
    d.line([(cx, apex_y), (lx, base_y)], fill=ROOF_LT, width=2)
    d.line([(cx, apex_y), (rx, base_y)], fill=ROOF_DK, width=2)


def _door(d, cx, y0, y1):
    """아치 나무문 — 널빤지 + 철 띠 + 손잡이."""
    w = 16
    d.rectangle([cx - w - 2, y0 - 2, cx + w + 2, y1 + 2], fill=OUTLINE)
    d.rectangle([cx - w, y0, cx + w, y1], fill=WOOD)
    d.pieslice([cx - w, y0 - w, cx + w, y0 + w], 180, 360, fill=WOOD, outline=OUTLINE)
    for sx in (cx - 8, cx, cx + 8):
        d.line([(sx, y0 - 4), (sx, y1 - 2)], fill=WOOD_DK, width=1)
    for yy in (y0 + 6, y1 - 8):
        d.line([(cx - w, yy), (cx + w, yy)], fill=STONE_DK, width=2)
    d.ellipse([cx + 7, (y0 + y1) // 2, cx + 11, (y0 + y1) // 2 + 4], fill=GOLD, outline=OUTLINE)


def _window(d, x0, y0, s=18):
    """덧창 창문 — 아웃라인 틀 + 십자 창살 + 밝은 유리 + 나무 덧창."""
    d.rectangle([x0 - 2, y0 - 2, x0 + s + 2, y0 + s + 2], fill=OUTLINE)
    d.rectangle([x0, y0, x0 + s, y0 + s], fill=(120, 128, 120, 255))
    d.line([(x0 + s // 2, y0), (x0 + s // 2, y0 + s)], fill=OUTLINE, width=2)
    d.line([(x0, y0 + s // 2), (x0 + s, y0 + s // 2)], fill=OUTLINE, width=2)
    d.line([(x0 + 2, y0 + 2), (x0 + s // 2 - 2, y0 + 2)], fill=STONE_LT, width=1)
    for dx in (-6, s + 6):
        d.rectangle([x0 + dx - 3, y0 - 1, x0 + dx + 3, y0 + s + 1], fill=WOOD, outline=OUTLINE)
        d.line([(x0 + dx, y0), (x0 + dx, y0 + s)], fill=WOOD_DK, width=1)


def _chimney(d, x, y_top, smoke=True):
    """석조 굴뚝 (+ 작은 연기; §6-A: 글로우 없음, 바닥 원 없음)."""
    d.rectangle([x - 2, y_top - 2, x + 14, y_top + 30], fill=OUTLINE)
    d.rectangle([x, y_top, x + 12, y_top + 28], fill=STONE)
    for yy in range(y_top + 4, y_top + 26, 7):
        d.line([(x, yy), (x + 12, yy)], fill=MORTAR, width=1)
    d.rectangle([x - 2, y_top - 2, x + 14, y_top + 4], fill=STONE_DK)
    if smoke:
        for i, (dx, dy, r) in enumerate([(3, -8, 6), (7, -18, 5), (11, -27, 4)]):
            a = 150 - i * 40
            d.ellipse([x + dx - r, y_top + dy - r, x + dx + r, y_top + dy + r],
                      fill=(176, 170, 160, max(40, a)))


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    rng = random.Random(20260827)  # 결정론 — 벽 명암 노이즈 재현 가능

    cx = 128
    # 벽 밑 얇은 그림자 띠(원 아님 — §6-A)
    d.rectangle([48, 226, 208, 232], fill=(30, 24, 20, 90))

    # 1층 하단 석벽
    _stone_wall(d, rng, 54, 158, 202, 226)
    # 2층 목조 — 좌우로 살짝 내민 젯티 오버행(석벽보다 넓다)
    _timber_upper(d, 48, 104, 208, 158, studs=(48 + 30, 48 + 68, 208 - 68, 208 - 30))
    # 오버행 아래 그림자(2층이 1층보다 튀어나온 표시)
    d.rectangle([48, 158, 202, 161], fill=(40, 30, 24, 70))

    # 급경사 박공 널지붕(2층 위, 처마가 넓게)
    _roof(d, cx, 40, 104, half_w=84, eave=8)

    # 좌우 굴뚝 2개(큰 집 표시)
    _chimney(d, 70, 56, smoke=False)
    _chimney(d, 176, 52, smoke=True)

    # 2층 정면 창 2개
    _window(d, 84, 118, s=20)
    _window(d, 152, 118, s=20)

    # 1층 아치문 + 좌우 창
    _door(d, cx, 178, 224)
    _window(d, 64, 176, s=18)
    _window(d, 174, 176, s=18)

    out = HERE / "out_oxalpha_village_house1.png"
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
