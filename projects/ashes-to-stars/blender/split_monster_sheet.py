"""
재와 별 — 몬스터 스프라이트 시트 분할 (오너 제공, 2026-08-13)

    python split_monster_sheet.py

캐릭터 시트(sheet3·sheet4)와 다른 점:
  - **격자선이 없다.** 단색 파랑 배경에 스프라이트만 놓여 있다 → 빈 줄/빈 열로 나눈다
  - 왼쪽에 상태 라벨(Idle/Walk/…)이 흰 글씨로 있다 → 라벨 영역은 x로 잘라 버린다
  - 행마다 프레임 수가 다르다 (Idle 4 / Walk 6 / Attack 4 / Hurt 4 / Death 4)

같은 점 — 여기가 중요하다:
  공통 캔버스 + 가로 중앙(본체 기준) · 세로 바닥 정렬을 **그대로** 쓴다.
  프레임마다 타이트하게 자르면 재생할 때 몬스터가 위아래로 튄다(캐릭터에서 이미 겪었다).

출력: ../unity/Assets/Resources/sprites/mob01/mob01_<상태>_<번호>.png
"""
import os

from PIL import Image

from realign_base_frames import (PAD, clean_and_measure, cut_transparent, is_bg,
                                 sample_bg)

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "source_sheets", "sheet5_monster.png")
OUT = os.path.abspath(os.path.join(HERE, "..", "unity", "Assets", "Resources", "sprites"))

NAME = "mob01"
LABEL_X = 300          # 이 왼쪽은 상태 라벨(흰 글씨)이라 버린다
ROW_STATES = ["idle", "walk", "attack", "hurt", "death"]
GAP_ROW = 12           # 세로로 이만큼 비면 행 경계
GAP_COL = 10           # 가로로 이만큼 비면 프레임 경계
MIN_W = 14


def runs(flags, gap_min, offset=0):
    """True 구간을 찾되, False가 gap_min 미만이면 같은 구간으로 본다."""
    out, start, gap = [], None, 0
    for i, f in enumerate(flags):
        if f:
            if start is None:
                start = i
            gap = 0
        elif start is not None:
            gap += 1
            if gap >= gap_min:
                out.append((offset + start, offset + i - gap + 1))
                start, gap = None, 0
    if start is not None:
        out.append((offset + start, offset + len(flags)))
    return out


def main():
    im = Image.open(SRC).convert("RGB")
    w, h = im.size
    p = im.load()
    refs = sample_bg(im, 0, 0, w, h)
    print(f"시트 {w}×{h}  배경색 표본 {refs[:3]}")

    # 라벨을 뺀 영역에서 행을 찾는다
    rows = runs([any(not is_bg(p[x, y], refs) for x in range(LABEL_X, w, 2))
                 for y in range(h)], GAP_ROW)
    print(f"행 {len(rows)}개: {rows}")
    if len(rows) != len(ROW_STATES):
        raise SystemExit(f"행이 {len(rows)}개 잡혔다 — {len(ROW_STATES)}개여야 한다. "
                         "GAP_ROW나 LABEL_X를 조정할 것")

    # 1단계: 프레임 위치 수집
    plan = []
    for (y0, y1), state in zip(rows, ROW_STATES):
        cols = runs([any(not is_bg(p[x, y], refs) for y in range(y0, y1, 2))
                     for x in range(LABEL_X, w)], GAP_COL, LABEL_X)
        cols = [c for c in cols if c[1] - c[0] >= MIN_W]
        print(f"  {state}: {len(cols)}프레임")
        for idx, (x0, x1) in enumerate(cols):
            plan.append((state, idx, (x0, y0, x1, y1)))

    # 2단계: 잘라내고 노이즈를 걸러 본체 위치를 잰다
    cut = []
    for state, idx, bb in plan:
        sprite, body, kept = clean_and_measure(cut_transparent(im, bb, refs))
        cut.append((state, idx, sprite, body, kept))

    left = max(bd[0] - kp[0] + (bd[2] - bd[0]) // 2 for _, _, _, bd, kp in cut)
    right = max(kp[2] - bd[2] + (bd[2] - bd[0]) // 2 for _, _, _, bd, kp in cut)
    cw = (max(left, right) + PAD) * 2
    ch = max(kp[3] - kp[1] for _, _, _, _, kp in cut) + PAD * 2
    print(f"공통 캔버스 {cw}×{ch}  (프레임 {len(cut)}장)")

    d = os.path.join(OUT, NAME)
    os.makedirs(d, exist_ok=True)
    made = []
    for state, idx, sprite, body, kept in cut:
        canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
        x = int(round(cw / 2.0 - (body[0] + body[2]) / 2.0))
        y = ch - PAD - kept[3]
        canvas.paste(sprite, (x, y), sprite)
        path = os.path.join(d, f"{NAME}_{state}_{idx:02d}.png")
        canvas.save(path)
        made.append(os.path.basename(path))

    print(f"\n{len(made)}장 → {d}")
    for m in made:
        print("   ", m)


if __name__ == "__main__":
    main()
