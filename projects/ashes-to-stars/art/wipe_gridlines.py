#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""재와 별 — AI 시트의 격자선 제거 (분할 전처리).

왜 필요한가 (2026-08-15 실측):
  프롬프트에 "no grid lines"를 넣어도 모델이 셀 경계에 검은 선을 그린다. 그 선이
  마젠타 배경을 가로지르면 `split_ai_sheet.py`의 **자동 격자 검출이 실패**한다
  (배경이 이어지지 않아 셀을 하나로 본다 — "자동 검출 1셀" 오류).

  라벨 텍스트는 프롬프트 재작성으로 없앴지만(스펙 시트 형태 → 서술형), 격자선은
  같은 방법으로 안 없어졌다. 그래서 후처리로 지운다.

방법:
  마젠타가 아닌 픽셀이 한 행/열의 90% 이상을 차지하면 그 줄은 그림이 아니라 격자선이다.
  캐릭터는 아무리 커도 시트 한 줄을 가득 채우지 않는다.
  ⚠️ 임계를 낮추면 캐릭터가 가로로 넓게 퍼진 프레임(구르기·사망)을 격자선으로 오인한다.

사용:
    python3 wipe_gridlines.py out_char/char_dps_B.png [...]
    python3 wipe_gridlines.py out_char/*.png --dry-run
"""

from __future__ import annotations

import argparse
import sys

import numpy as np
from PIL import Image

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

MAGENTA = np.array([255, 0, 255])
TOL = 120          # 마젠타 판정 허용 오차(합계 거리)
LINE_RATIO = 0.90  # 이 비율 이상이 비마젠타면 격자선 후보
MAX_LINE_PX = 8    # 격자선은 얇다. 이보다 두꺼우면 그림이다
MAX_LINE_FRAC = 0.25  # 후보가 이 비율을 넘으면 판정 자체가 틀린 것으로 본다


def _runs(idx: list[int]) -> list[list[int]]:
    """연속한 인덱스를 묶는다 — 격자선은 몇 px가 이어진 덩어리다."""
    out: list[list[int]] = []
    for i in idx:
        if out and i == out[-1][-1] + 1:
            out[-1].append(i)
        else:
            out.append([i])
    return out


def wipe(path: str, dry_run: bool = False) -> int:
    im = Image.open(path).convert("RGB")
    a = np.array(im).astype(int)
    mag = np.abs(a - MAGENTA).sum(axis=2) < TOL

    raw_r = [i for i, v in enumerate((~mag).mean(axis=1)) if v > LINE_RATIO]
    raw_c = [i for i, v in enumerate((~mag).mean(axis=0)) if v > LINE_RATIO]

    # ⚠️ 2026-08-15 사고: 이 필터가 없어서 **단독 프레임(invuln)의 캐릭터를 관통하는
    #    마젠타 줄무늬 3개를 그어 이미지를 훼손했다.** 원인은 잘못된 가정이었다 —
    #    "캐릭터는 한 줄을 가득 채우지 않는다"는 6셀 시트에서만 참이고, 캐릭터가
    #    세로로 꽉 찬 단독 프레임에서는 거의 모든 열이 후보가 된다.
    #    격자선은 **얇고 드물다**는 성질로 다시 거른다.
    rows = [i for run in _runs(raw_r) if len(run) <= MAX_LINE_PX for i in run]
    cols = [i for run in _runs(raw_c) if len(run) <= MAX_LINE_PX for i in run]

    if len(rows) > a.shape[0] * MAX_LINE_FRAC or len(cols) > a.shape[1] * MAX_LINE_FRAC:
        print(f"  {path}: 격자선 후보가 너무 많다(행 {len(rows)}·열 {len(cols)}) — "
              f"단독 프레임으로 보고 건너뜀")
        return 0
    if not rows and not cols:
        print(f"  {path}: 격자선 없음 — 건너뜀")
        return 0

    print(f"  {path}: 격자선 행 {len(rows)}줄 · 열 {len(cols)}줄")
    if dry_run:
        return 0

    a[rows, :, :] = MAGENTA
    a[:, cols, :] = MAGENTA
    Image.fromarray(a.astype(np.uint8)).save(path)
    print("    ✅ 마젠타로 덮음")
    return 0


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("files", nargs="+")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()
    for f in args.files:
        wipe(f, args.dry_run)


if __name__ == "__main__":
    main()
