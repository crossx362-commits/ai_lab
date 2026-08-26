#!/usr/bin/env python3
"""ox-alpha 영지 공사판(scaffold) 코드합성 — 256×256 반투명 프레임 오버레이.

배경(오너 2026-08-26 「게임 ui 좀더 시각화·퀄리티 업」): 영지 8동 _0/_1/_2는 전부
ox-alpha 코드합성(256×256, ~5KB)으로 통일됐는데, 건물이 공사 중일 때 위에 겹쳐 그려지는
공용 공사판 `estate_scaffold_0.png`만 옛 나노바나나(1864×2028·4MB)로 남아 있었다.
EstateYard.DrawScaffoldIfBusy가 이 그림을 건물 박스에 72% 알파로 ScaleToFit 겹치므로,
같은 팔레트·같은 256 캔버스로 만든 「비계(飛階) 프레임」이 건물 위에 어울리게 얹힌다.

디자인: 좌·우 나무 기둥 + 가로 발판(널) 2단 + X자 가새 + 상단에 작은 공사 깃발.
가운데는 비워 둔다(프레임이라 밑 건물이 보여야 한다 — 72% 오버레이). §6-A 준수:
바닥에 큰 원/고리/초승달 금지(스킬 범위로 읽힘). 장식은 목재 결과 깃발뿐, 글로우 없음.

사용: python3 gen_oxalpha_scaffold.py
출력: art/out_oxalpha_estate_scaffold_0.png (256×256 RGBA)
"""
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

# ox-alpha 목재 팔레트(영지 계열 웜톤과 같은 저채도 갈색 + 금빛/붉은 깃발 액센트)
WOOD = (150, 104, 58, 255)
WOOD_DK = (96, 64, 34, 255)
WOOD_LT = (196, 150, 92, 255)
OUTLINE = (40, 24, 20, 255)
GOLD = (216, 172, 96, 255)
BANNER = (150, 34, 34, 255)


def _post(d, x, y0, y1):
    """세로 나무 기둥 — 아웃라인 + 밝은 결 하이라이트."""
    w = 7
    d.rectangle([x - w // 2 - 1, y0, x + w // 2 + 1, y1], fill=OUTLINE)
    d.rectangle([x - w // 2, y0 + 1, x + w // 2, y1 - 1], fill=WOOD)
    # 나뭇결 밝은 줄
    d.line([(x - 1, y0 + 2), (x - 1, y1 - 2)], fill=WOOD_LT, width=1)
    d.line([(x + 2, y0 + 2), (x + 2, y1 - 2)], fill=WOOD_DK, width=1)


def _plank(d, x0, x1, y):
    """가로 발판 널빤지 — 아웃라인 + 윗면 하이라이트 + 널 이음선."""
    h = 9
    d.rectangle([x0, y - 1, x1, y + h + 1], fill=OUTLINE)
    d.rectangle([x0 + 1, y, x1 - 1, y + h], fill=WOOD)
    d.line([(x0 + 1, y + 1), (x1 - 1, y + 1)], fill=WOOD_LT, width=1)
    d.line([(x0 + 1, y + h - 1), (x1 - 1, y + h - 1)], fill=WOOD_DK, width=1)
    # 널 이음선(세로 틈) — 판자 여러 장으로 읽히게
    for sx in range(x0 + 18, x1 - 6, 22):
        d.line([(sx, y + 1), (sx, y + h - 1)], fill=WOOD_DK, width=1)


def _brace(d, x0, y0, x1, y1):
    """대각 가새(사재) — 얇은 나무 막대."""
    d.line([(x0, y0), (x1, y1)], fill=OUTLINE, width=4)
    d.line([(x0, y0), (x1, y1)], fill=WOOD, width=2)


def _flag(d, x, top):
    """상단 작은 공사 깃발 — 장대 + 붉은 사각 페넌트(§6-A: 바닥 아님·글로우 없음)."""
    d.line([(x, top), (x, top + 26)], fill=OUTLINE, width=3)
    d.line([(x, top), (x, top + 26)], fill=GOLD, width=1)
    d.polygon([(x + 1, top), (x + 22, top + 4), (x + 1, top + 12)],
              fill=BANNER, outline=OUTLINE)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)

    # 기둥 두 개 — 건물 좌우를 감싸되 안쪽은 비운다
    lx, rx = 58, 198
    top, bot = 66, 232
    _post(d, lx, top, bot)
    _post(d, rx, top, bot)

    # 발판 2단 — 상단·하단(가운데는 비워 밑 건물이 보이게)
    _plank(d, lx - 4, rx + 4, 96)
    _plank(d, lx - 4, rx + 4, 176)

    # X자 가새 — 위 칸·아래 칸 각각
    _brace(d, lx, 108, rx, 168)
    _brace(d, rx, 108, lx, 168)
    _brace(d, lx, 188, rx, 228)
    _brace(d, rx, 188, lx, 228)

    # 발판 위 작은 자재 더미(나무 판 몇 장) — 공사 중임을 읽히게
    for i, py in enumerate((90, 86)):
        d.rectangle([lx + 20 + i * 3, py, lx + 70 + i * 3, py + 4],
                    fill=WOOD_LT, outline=OUTLINE)

    # 상단 공사 깃발
    _flag(d, lx, top - 24)

    out = HERE / "out_oxalpha_estate_scaffold_0.png"
    im.save(out)
    # 실측 로그
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
