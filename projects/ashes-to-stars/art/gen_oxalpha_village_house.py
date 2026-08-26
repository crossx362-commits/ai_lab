#!/usr/bin/env python3
"""ox-alpha 마을 집(village_house_0) 코드합성 — 256×256 목조·석조 오두막.

배경(오너 2026-08-26 「게임 ui 좀더 시각화·이미지화·퀄리티 업」): 영지 8동은 ox-alpha
코드합성(256×256, ~5KB)으로 통일됐으나, **필드 마을 구성물**(FieldDecor.BuildVillage로
아레나 밖에 세워지는 village_*)은 아직 옛 나노바나나(2000px·2~3MB)라 톤·해상도가 영지와
따로 논다. village_house_0(2.0MB)을 같은 팔레트·같은 256 캔버스의 목조 오두막으로 교체한다.

**크기 무영향(실측 확인, FieldDecor.cs:336)**: 프랍의 인게임 크기는
`ppu = pxHeight / TargetUnits(prop_scale.json)`으로 목표 유닛(village_house_0=3.60)에서
역산되므로 원본 해상도(2000→256)를 바꿔도 월드 크기는 3.60유닛 그대로다. STATUS가 적어둔
「FieldDecor.ScaleFor가 원본 픽셀 크기를 하드코딩 → 256 전환 시 스케일 재튜닝 필요」는
사실이 아니다(그런 ScaleFor 심볼 자체가 없다). 그래서 아트만 교체하면 된다.

디자인: 하단 석벽(웜그레이 블록+모르타르 줄) + 상단 목조 뼈대(크림 회벽+갈색 스터드) +
급경사 박공 나무 널지붕(겹친 널·명암) + 아치 나무문 + 덧창 창문 + 우측 석조 굴뚝(작은 연기).
§6-A 준수: 바닥에 큰 원/고리/초승달 금지(스킬 범위로 읽힘) — 그림자는 벽 밑 얇은 띠뿐,
글로우 없음. 실루엣 우선·두꺼운 아웃라인의 ox-alpha 결을 유지한다.

사용: python3 gen_oxalpha_village_house.py
출력: art/out_oxalpha_village_house.png (256×256 RGBA)
"""
from pathlib import Path
import random

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

# ox-alpha 팔레트 — 영지 계열 저채도 웜톤과 같은 돌·나무·금빛/붉은 액센트
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
SMOKE = (176, 170, 160, 150)


def _stone_wall(d, rng, x0, y0, x1, y1):
    """하단 석벽 — 아웃라인 박스 + 어긋난 벽돌 줄(모르타르) + 명암 결."""
    d.rectangle([x0 - 2, y0 - 2, x1 + 2, y1 + 2], fill=OUTLINE)
    d.rectangle([x0, y0, x1, y1], fill=STONE)
    row_h = 13
    y = y0
    ri = 0
    while y < y1:
        # 벽돌 줄마다 반 칸씩 어긋난 세로 이음선
        off = (ri % 2) * 16
        bx = x0 + 4 + off
        while bx < x1 - 3:
            d.line([(bx, y + 1), (bx, min(y1 - 1, y + row_h - 1))], fill=MORTAR, width=1)
            bx += 30
        # 가로 모르타르 줄
        yy = min(y1 - 1, y + row_h)
        d.line([(x0 + 1, yy), (x1 - 1, yy)], fill=MORTAR, width=1)
        # 블록 명암 몇 개(지어내지 않는 균일 노이즈 — 결정론 시드)
        for _ in range((x1 - x0) // 22):
            sx = rng.randint(x0 + 3, x1 - 12)
            sy = rng.randint(y + 2, min(y1 - 3, y + row_h - 3))
            if rng.random() < 0.5:
                d.rectangle([sx, sy, sx + 6, sy + 3], fill=STONE_DK)
            else:
                d.rectangle([sx, sy, sx + 6, sy + 3], fill=STONE_LT)
        y += row_h
        ri += 1


def _timber_upper(d, x0, y0, x1, y1):
    """상단 목조 뼈대 — 크림 회벽 패널 + 갈색 세로 스터드 + 대각 가새(하프팀버)."""
    d.rectangle([x0 - 2, y0 - 2, x1 + 2, y1 + 2], fill=OUTLINE)
    d.rectangle([x0, y0, x1, y1], fill=PLASTER)
    # 회벽 아래쪽 옅은 그늘
    d.rectangle([x0, y1 - 6, x1, y1], fill=PLASTER_DK)
    # 상·하 가로 보
    for yy in (y0, y1 - 4):
        d.rectangle([x0, yy, x1, yy + 4], fill=WOOD)
        d.line([(x0, yy), (x1, yy)], fill=WOOD_LT, width=1)
        d.line([(x0, yy + 4), (x1, yy + 4)], fill=WOOD_DK, width=1)
    # 세로 스터드 4개
    for sx in (x0 + 26, x0 + 58, x1 - 58, x1 - 26):
        d.rectangle([sx - 3, y0, sx + 3, y1], fill=WOOD)
        d.line([(sx - 3, y0), (sx - 3, y1)], fill=WOOD_LT, width=1)
        d.line([(sx + 3, y0), (sx + 3, y1)], fill=WOOD_DK, width=1)
    # 좌우 칸 대각 가새(V자)
    d.line([(x0 + 4, y1 - 4), (x0 + 26, y0 + 4)], fill=WOOD_DK, width=3)
    d.line([(x1 - 4, y1 - 4), (x1 - 26, y0 + 4)], fill=WOOD_DK, width=3)


def _roof(d, cx, apex_y, base_y, half_w, eave):
    """급경사 박공 나무 널지붕 — 겹친 널 줄·명암, 처마가 벽 밖으로 나온다."""
    lx = cx - half_w - eave
    rx = cx + half_w + eave
    # 실루엣(아웃라인 삼각)
    d.polygon([(cx, apex_y - 3), (lx - 3, base_y + 3), (rx + 3, base_y + 3)], fill=OUTLINE)
    d.polygon([(cx, apex_y), (lx, base_y), (rx, base_y)], fill=ROOF)
    # 널 줄 — 처마와 평행하게 위에서 아래로, 아래로 갈수록 밝게(빛)
    rows = 9
    for i in range(1, rows):
        t = i / rows
        yy = int(apex_y + (base_y - apex_y) * t)
        # 이 높이의 지붕 좌우 끝
        hw = int(half_w * t) + int(eave * t)
        col = ROOF_DK if i % 2 == 0 else ROOF_LT
        d.line([(cx - hw, yy), (cx + hw, yy)], fill=col, width=1)
        # 널 세로 이음(엇갈림)
        off = (i % 2) * 9
        for sx in range(cx - hw + 6 + off, cx + hw - 4, 18):
            d.line([(sx, yy), (sx, yy + int((base_y - apex_y) / rows))], fill=ROOF_DK, width=1)
    # 용마루 밝은 능선
    d.line([(cx, apex_y + 2), (cx, apex_y + 10)], fill=ROOF_LT, width=2)
    # 박공 끝 밝은 바지널(테두리 널)
    d.line([(cx, apex_y), (lx, base_y)], fill=ROOF_LT, width=2)
    d.line([(cx, apex_y), (rx, base_y)], fill=ROOF_DK, width=2)


def _door(d, cx, y0, y1):
    """아치 나무문 — 널빤지 + 철 띠 + 손잡이."""
    w = 15
    d.rectangle([cx - w - 2, y0 - 2, cx + w + 2, y1 + 2], fill=OUTLINE)
    d.rectangle([cx - w, y0, cx + w, y1], fill=WOOD)
    d.pieslice([cx - w, y0 - w, cx + w, y0 + w], 180, 360, fill=WOOD, outline=OUTLINE)
    # 널 세로 결
    for sx in (cx - 7, cx, cx + 7):
        d.line([(sx, y0 - 4), (sx, y1 - 2)], fill=WOOD_DK, width=1)
    # 철 띠 2줄
    for yy in (y0 + 6, y1 - 8):
        d.line([(cx - w, yy), (cx + w, yy)], fill=STONE_DK, width=2)
    # 손잡이
    d.ellipse([cx + 6, (y0 + y1) // 2, cx + 10, (y0 + y1) // 2 + 4], fill=GOLD, outline=OUTLINE)


def _window(d, x0, y0):
    """덧창 창문 — 아웃라인 틀 + 십자 창살 + 밝은 유리 + 나무 덧창."""
    s = 18
    d.rectangle([x0 - 2, y0 - 2, x0 + s + 2, y0 + s + 2], fill=OUTLINE)
    d.rectangle([x0, y0, x0 + s, y0 + s], fill=(120, 128, 120, 255))  # 저채도 유리
    d.line([(x0 + s // 2, y0), (x0 + s // 2, y0 + s)], fill=OUTLINE, width=2)
    d.line([(x0, y0 + s // 2), (x0 + s, y0 + s // 2)], fill=OUTLINE, width=2)
    # 유리 하이라이트
    d.line([(x0 + 2, y0 + 2), (x0 + s // 2 - 2, y0 + 2)], fill=STONE_LT, width=1)
    # 좌우 덧창
    for dx in (-6, s + 6):
        d.rectangle([x0 + dx - 3, y0 - 1, x0 + dx + 3, y0 + s + 1], fill=WOOD, outline=OUTLINE)
        d.line([(x0 + dx, y0), (x0 + dx, y0 + s)], fill=WOOD_DK, width=1)


def _chimney_smoke(d, x, y_top):
    """우측 석조 굴뚝 + 작은 연기(§6-A: 글로우 없음, 바닥 원 없음)."""
    d.rectangle([x - 2, y_top - 2, x + 14, y_top + 34], fill=OUTLINE)
    d.rectangle([x, y_top, x + 12, y_top + 32], fill=STONE)
    for yy in range(y_top + 4, y_top + 30, 7):
        d.line([(x, yy), (x + 12, yy)], fill=MORTAR, width=1)
    d.rectangle([x - 2, y_top - 2, x + 14, y_top + 4], fill=STONE_DK)  # 관 갓
    # 연기 세 방울(위로 작아지며 흐려짐)
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
    d.rectangle([56, 214, 200, 220], fill=(30, 24, 20, 90))

    # 하단 석벽 · 상단 목조
    _stone_wall(d, rng, 60, 150, 196, 214)
    _timber_upper(d, 66, 100, 190, 150)

    # 지붕(벽 위, 처마가 목조 폭보다 넓게)
    _roof(d, cx, 46, 100, half_w=62, eave=8)

    # 굴뚝(지붕 우측 경사 위)
    _chimney_smoke(d, 168, 60)

    # 문·창문(하단 석벽 정면)
    _door(d, cx, 168, 212)
    _window(d, 74, 168)
    _window(d, 168, 168)

    out = HERE / "out_oxalpha_village_house.png"
    im.save(out)
    # 실측 로그 — 투명/불투명 픽셀 수(반입 판정용)
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
