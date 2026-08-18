#!/usr/bin/env python3
"""오너가 준 기본 5직업 스프라이트 시트를 게임 프레임으로 반입한다.

원본(`캐릭터스프라이트.zip`)의 구조:
  - 한 장 1792×1008, 세로로 7개 행 = Idle / Move / Attack / Skill1 / Skill2 / Skill3 / Death
  - 각 행 위에 검은 라벨 띠("Idle (쉼하기)")가 붙어 있고, 그 아래가 프레임 영역
  - 프레임 영역의 배경은 **투명 체커보드가 픽셀로 구워진** 상태(밝은 회색 241 / 197 격자)
  - 가로로 6프레임

배경 제거를 색만으로 하면 안 된다 — 힐러의 흰 로브·탱커의 회색 갑옷이 체커보드와 같은
무채색이라 몸이 뚫린다. 그래서 **테두리에서 연결된 영역만** 지운다(flood fill):
체커 색이면서 프레임 가장자리와 이어져 있어야 배경이다. 캐릭터 안쪽의 흰 픽셀은
바깥과 끊겨 있으므로 살아남는다.

    python3 import_owner_sheets.py <zip을 푼 폴더> [--dry-run]
"""
import argparse
import os
import sys
from collections import deque

import numpy as np
from PIL import Image

# 시트 파일명이 UUID라 어떤 직업인지 알 수 없다. 시트 맨 아래 한글 라벨을 사람이 읽어
# 확정한 순서다(정렬한 파일명 기준). 새 zip을 받으면 여기부터 다시 확인할 것.
SHEET_JOBS = {
    "13601aad": "healer",   # 힐러
    "b6a5b853": "mage",     # 마법딜러
    "b871e406": "buffer",   # 서포터
    "e05ab71e": "dps",      # 물리딜러
    "f7177c1e": "tank",     # 탱커
}

# 원본 행 → 게임의 13프레임 계약(SpriteBank.JOB_FRAMES). 원본에 없는 것은 가장
# 가까운 행에서 빌린다. 대시 4장은 Move에서 뽑아 이동감을 유지한다.
ROW_IDLE, ROW_MOVE, ROW_ATTACK, ROW_SKILL1, ROW_SKILL2, ROW_SKILL3, ROW_DEATH = range(7)
FRAME_PLAN = [
    # 대기도 6프레임이다. 1장만 쓰면 서 있는 캐릭터가 **완전히 정지**해서
    # "애니메이션이 안 나온다"로 읽힌다(오너 재지적 2026-08-18) — 걷기·공격이
    # 도는 것과 별개로, 화면에서 가장 오래 보이는 상태가 대기다.
    ("idle_00", ROW_IDLE, 0),
    ("idle_01", ROW_IDLE, 1),
    ("idle_02", ROW_IDLE, 2),
    ("idle_03", ROW_IDLE, 3),
    ("idle_04", ROW_IDLE, 4),
    ("idle_05", ROW_IDLE, 5),
    # 원본은 동작마다 6프레임이다. 계약이 2장이던 시절엔 4장을 버렸다
    # (오너 지적 2026-08-18 "애니메이션 제대로 적용 안 된 거 같음") — 전부 쓴다.
    ("walk_00", ROW_MOVE, 0),
    ("walk_01", ROW_MOVE, 1),
    ("walk_02", ROW_MOVE, 2),
    ("walk_03", ROW_MOVE, 3),
    ("walk_04", ROW_MOVE, 4),
    ("walk_05", ROW_MOVE, 5),
    ("attack_00", ROW_ATTACK, 0),
    ("attack_01", ROW_ATTACK, 1),
    ("attack_02", ROW_ATTACK, 2),
    ("attack_03", ROW_ATTACK, 3),
    ("attack_04", ROW_ATTACK, 4),
    ("attack_05", ROW_ATTACK, 5),
    ("special_00", ROW_SKILL3, 2),
    ("hurt_00", ROW_DEATH, 0),
    ("death_00", ROW_DEATH, 4),
    ("dash_00", ROW_MOVE, 1),
    ("dash_01", ROW_MOVE, 2),
    ("dash_02", ROW_MOVE, 4),
    ("dash_03", ROW_MOVE, 5),
    ("invuln_00", ROW_SKILL1, 2),
]

COLS = 6
CHECKER = (241.0, 197.0)
# 프레임 칸을 두르는 검은 테두리 선 두께(실측 3px 내외). 이 안쪽에서 flood를 시작한다.
BORDER = 3


def label_bands(a: np.ndarray) -> list[tuple[int, int]]:
    """검은 라벨 띠 사이의 프레임 영역을 찾는다."""
    dark = (a.max(axis=2) < 60).mean(axis=1)
    runs, start = [], None
    for y, v in enumerate(dark):
        if v > 0.80 and start is None:
            start = y
        elif v <= 0.80 and start is not None:
            if y - start >= 8:
                runs.append((start, y))
            start = None
    if start is not None and len(dark) - start >= 8:
        runs.append((start, len(dark)))

    bands = []
    for i, (_, end) in enumerate(runs):
        top = end
        bot = runs[i + 1][0] if i + 1 < len(runs) else a.shape[0]
        if bot - top > 60:
            bands.append((top, bot))
    return bands


def cut_background(cell: np.ndarray, prune: bool = True) -> np.ndarray:
    """체커보드 배경만 투명하게. 가장자리에서 연결된 것만 지운다."""
    h, w, _ = cell.shape
    neutral = (cell.max(axis=2) - cell.min(axis=2)) < 30
    grey = cell.mean(axis=2)
    checker = neutral & (
        (np.abs(grey - CHECKER[0]) < 34) | (np.abs(grey - CHECKER[1]) < 34)
    )

    # 가장자리에서 시작하는 flood fill — 캐릭터 안쪽 흰 픽셀은 바깥과 끊겨 살아남는다.
    # ⚠️ 시트의 각 프레임 칸은 **검은 테두리 선**으로 둘러싸여 있다(실측: 가장자리 픽셀
    #    밝기 3~90). 그래서 맨 바깥 줄에서 seed를 잡으면 체커 판정을 하나도 통과하지 못해
    #    flood가 시작조차 안 된다(1차 시도: 배경이 그대로 남았다). 테두리 안쪽에서 seed를
    #    잡고, 테두리 자체는 배경으로 확정한다 — 어차피 오너가 "검은 부분 잘라내라"고 한 것.
    bg = np.zeros((h, w), dtype=bool)
    q = deque()
    inset = BORDER

    def seed(y: int, x: int) -> None:
        if 0 <= y < h and 0 <= x < w and checker[y, x] and not bg[y, x]:
            bg[y, x] = True
            q.append((y, x))

    for x in range(w):
        seed(inset, x)
        seed(h - 1 - inset, x)
    for y in range(h):
        seed(y, inset)
        seed(y, w - 1 - inset)
    while q:
        y, x = q.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and checker[ny, nx] and not bg[ny, nx]:
                bg[ny, nx] = True
                q.append((ny, nx))

    bg[:BORDER, :] = True
    bg[-BORDER:, :] = True
    bg[:, :BORDER] = True
    bg[:, -BORDER:] = True

    alpha = np.where(bg, 0, 255).astype(np.uint8)
    if prune:
        alpha = keep_main_body(alpha)
    return np.dstack([cell.astype(np.uint8), alpha])


def keep_main_body(alpha: np.ndarray, ratio: float = 0.30) -> np.ndarray:
    """본체에서 떨어진 조각을 버린다.

    칸 경계가 옆 프레임의 캐릭터를 조금 물고 들어온다(실측: dps idle 오른쪽 끝에
    옆 칸 조각이 붙어 bbox 폭이 226px로 부풀었다). 그러면 공통 캔버스가 통째로
    넓어지고, 그 폭이 배율을 지배해 초상화에서 캐릭터가 콩알만 해진다.
    가장 큰 덩어리 대비 `ratio` 미만인 덩어리는 잘라낸다.
    """
    h, w = alpha.shape
    seen = np.zeros((h, w), dtype=bool)
    comps = []
    for sy in range(h):
        for sx in range(w):
            if alpha[sy, sx] == 0 or seen[sy, sx]:
                continue
            q = deque([(sy, sx)])
            seen[sy, sx] = True
            cells = []
            while q:
                y, x = q.popleft()
                cells.append((y, x))
                for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < h and 0 <= nx < w and alpha[ny, nx] > 0 and not seen[ny, nx]:
                        seen[ny, nx] = True
                        q.append((ny, nx))
            comps.append(cells)
    if not comps:
        return alpha
    biggest = max(len(c) for c in comps)
    out = np.zeros_like(alpha)
    for c in comps:
        if len(c) >= biggest * ratio:
            for y, x in c:
                out[y, x] = alpha[y, x]
    return out


def split_row(row: np.ndarray) -> list[np.ndarray]:
    """한 행을 **캐릭터가 실제로 있는 자리**로 쪼갠다.

    ⚠️ 처음엔 행 폭을 6등분했다. 그런데 시트마다 행마다 프레임 수가 다르다 —
    실측: 물리딜러(dps)의 Move 행은 **8프레임**이었다(중심 105·318·551·769·985·
    1212·1435·1658). 6등분하면 한 칸에 기사가 둘 들어가고, 그러면 프레임마다
    실루엣이 300%씩 널뛰어 "애니메이션이 안 나온다"가 된다.
    그래서 개수를 가정하지 않고 덩어리를 찾아 그 사이 빈 곳에서 자른다.
    """
    rgba = cut_background(row, prune=False)     # 행 단위에선 덩어리를 지우면 안 된다
    col = (rgba[..., 3] > 40).sum(axis=0)
    runs, start = [], None
    for x, on in enumerate(col > 2):
        if on and start is None:
            start = x
        elif not on and start is not None:
            if x - start > 15:                  # 아주 가는 것은 잔재로 본다
                runs.append((start, x))
            start = None
    if start is not None and len(col) - start > 15:
        runs.append((start, len(col)))
    if not runs:
        return []

    # 이펙트(지팡이 빛 등)가 닿아 이웃 둘이 한 덩어리로 붙는다(실측: mage Move 5번 칸에
    # 마법사 둘). 다른 덩어리 폭의 중앙값보다 크게 넓으면 그 배수만큼 균등 분할한다.
    widths = sorted(e - s for s, e in runs)
    med = widths[len(widths) // 2]
    split: list[tuple[int, int]] = []
    for s, e in runs:
        n = max(1, round((e - s) / max(1, med)))
        if n == 1:
            split.append((s, e))
            continue
        step = (e - s) / n
        for k in range(n):
            split.append((int(s + k * step), int(s + (k + 1) * step)))
    runs = split

    cells = []
    for i, (s, e) in enumerate(runs):
        left = 0 if i == 0 else (runs[i - 1][1] + s) // 2
        right = rgba.shape[1] if i == len(runs) - 1 else (e + runs[i + 1][0]) // 2
        cells.append(cut_background(row[:, left:right]))
    return cells


def center_on_canvas(frames: list[np.ndarray], pad: int = 6) -> list[Image.Image]:
    """공통 캔버스에 가로 중앙·세로 바닥 정렬.

    시트마다 따로 crop하면 같은 직업 안에서도 프레임마다 캔버스가 달라져 걷기가 튄다
    (2026-08-15 P2 몹 사고와 같은 계열) — 한 직업의 모든 프레임을 **하나의 캔버스**로 묶는다.
    """
    boxes = []
    for f in frames:
        ys, xs = np.where(f[..., 3] > 40)
        boxes.append((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1) if len(xs) else None)

    valid = [b for b in boxes if b]
    cw = max(b[2] - b[0] for b in valid) + pad * 2
    ch = max(b[3] - b[1] for b in valid) + pad * 2

    out = []
    for f, b in zip(frames, boxes):
        canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
        if b:
            crop = Image.fromarray(f[b[1]:b[3], b[0]:b[2]], "RGBA")
            x = (cw - crop.width) // 2                 # 가로 중앙
            y = ch - pad - crop.height                 # 세로 바닥 (발이 같은 선에)
            canvas.paste(crop, (x, y), crop)
        out.append(canvas)
    return out


def process(path: str, job: str, dest_root: str, dry: bool) -> int:
    im = Image.open(path).convert("RGB")
    a = np.asarray(im).astype(np.float32)
    bands = label_bands(a)
    if len(bands) < 7:
        print(f"   ⚠️ {job}: 행을 {len(bands)}개만 찾았다(7 기대) — 건너뜀")
        return 0

    cells: dict[tuple[int, int], np.ndarray] = {}
    for r in range(7):
        top, bot = bands[r]
        for c, cell in enumerate(split_row(a[top:bot])):
            cells[(r, c)] = cell

    frames, names = [], []
    for name, row, col in FRAME_PLAN:
        cell = cells.get((row, col))
        if cell is None or (cell[..., 3] > 40).sum() < 50:
            cell = cells[(ROW_IDLE, 0)]          # 빈 칸이면 idle로 대체
        frames.append(cell)
        names.append(name)

    images = center_on_canvas(frames)
    dest = os.path.join(dest_root, job)
    if dry:
        print(f"   {job}: 캔버스 {images[0].width}×{images[0].height}, {len(images)}장 (dry-run)")
        return len(images)

    os.makedirs(dest, exist_ok=True)
    for name, img in zip(names, images):
        img.save(os.path.join(dest, f"{job}_{name}.png"))
    print(f"   {job}: 캔버스 {images[0].width}×{images[0].height}, {len(images)}장 → {dest}")
    return len(images)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("src", help="zip을 푼 폴더")
    ap.add_argument("--dest", default=os.path.join(
        os.path.dirname(os.path.abspath(__file__)),
        "..", "unity", "Assets", "Resources", "sprites"))
    ap.add_argument("--dry-run", action="store_true")
    ns = ap.parse_args()

    total = 0
    for f in sorted(os.listdir(ns.src)):
        if not f.lower().endswith((".jpg", ".jpeg", ".png")):
            continue
        job = SHEET_JOBS.get(f[:8])
        if not job:
            print(f"   ❔ {f}: 직업 매핑 없음 — SHEET_JOBS 확인 필요")
            continue
        total += process(os.path.join(ns.src, f), job, ns.dest, ns.dry_run)
    print(f"\n총 {total}장")
    return 0 if total else 1


if __name__ == "__main__":
    raise SystemExit(main())
