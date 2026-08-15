#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""재와 별 — 애니메이션 프레임 캔버스 정렬기.

왜 필요한가 (2026-08-15 실측 사고):
  P2 몹 4계열을 게임에 연결했더니 **걸을 때 몹이 튀었다.** 원인은 로직이 아니라 그림이었다 —
  프레임마다 캔버스 크기가 달랐다:

      mob_chaser: idle/walk_00~01 = 140x128 · walk_02~05 = 173x128 · hurt = 182x128 · death = 127x128

  6장씩 묶여 있는 것이 단서다. 시트 6셀 단위로 생성한 뒤 **시트별로 따로 크롭**해서
  같은 시트 안에서만 크기가 같았다. 걷기 6프레임이 두 시트에 걸쳐 있으니 재생 중
  walk_01 → walk_02에서 캔버스가 33px 점프한다.

  게다가 `SpriteBank`는 **idle 한 장으로만 피벗·PPU를 계산**해 나머지 프레임에 그대로
  적용한다(프레임마다 재면 그것대로 튀기 때문). 그래서 캔버스가 다르면 그 피벗이
  전부 어긋난다. 기존 `mob01`은 22장 전부 156x145로 일관해서 이 문제가 없었다.

처방 (`GAME_DEV_HANDOFF.md` §5에 이미 적혀 있던 것):
  공통 캔버스 + **가로는 내용 중앙, 세로는 바닥 정렬**.

  ⚠️ 크기 정규화는 **서 있는 자세끼리만** 한다. 눕는 death를 같은 규칙으로 늘리면
  거대해진다 — 그래서 death는 캔버스만 맞추고 **스케일은 건드리지 않는다**.

사용:
    python3 align_frames.py <디렉터리> [--dry-run]
    python3 align_frames.py ../unity/Assets/Resources/sprites/mob_chaser
"""

from __future__ import annotations

import argparse
import glob
import os
import sys

from PIL import Image

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")


def content_box(im: Image.Image):
    """알파가 있는 영역. 완전 투명이면 None."""
    return im.getchannel("A").getbbox()


def align(directory: str, dry_run: bool = False) -> int:
    files = sorted(glob.glob(os.path.join(directory, "*.png")))
    if not files:
        print(f"  ⚠️ png 없음: {directory}")
        return 1

    ims = {f: Image.open(f).convert("RGBA") for f in files}
    boxes = {f: content_box(im) for f, im in ims.items()}

    empty = [f for f, b in boxes.items() if b is None]
    if empty:
        # 완전 투명 프레임은 정렬 기준을 못 만든다 — 조용히 넘기면 그 프레임만 어긋난다.
        print(f"  ⚠️ 내용이 없는 프레임 {len(empty)}장 — 정렬 불가, 확인 필요:")
        for f in empty[:3]:
            print(f"      {os.path.basename(f)}")
        return 1

    # 공통 캔버스 = 모든 프레임의 내용을 담을 수 있는 최소 크기
    max_w = max(b[2] - b[0] for b in boxes.values())
    max_h = max(b[3] - b[1] for b in boxes.values())

    sizes = {im.size for im in ims.values()}
    print(f"  {os.path.basename(directory)}: {len(files)}장, 캔버스 {len(sizes)}종 "
          f"{sorted(sizes)[:4]} → 공통 {max_w}x{max_h}")
    if len(sizes) == 1 and all(
            im.size == (max_w, max_h) for im in ims.values()):
        print("    이미 정렬됨 — 건너뜀")
        return 0
    if dry_run:
        return 0

    for f, im in ims.items():
        b = boxes[f]
        crop = im.crop(b)                       # 내용만 추출
        canvas = Image.new("RGBA", (max_w, max_h), (0, 0, 0, 0))
        # 가로 중앙 · 세로 바닥 — 쿼터뷰 Y정렬이 발 기준이라 바닥이 어긋나면 땅에서 뜬다
        x = (max_w - crop.width) // 2
        y = max_h - crop.height
        canvas.paste(crop, (x, y))
        canvas.save(f)
    print(f"    ✅ {len(files)}장 정렬 완료 (가로 중앙·세로 바닥)")
    return 0


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("dirs", nargs="+")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    rc = 0
    for d in args.dirs:
        rc |= align(d, args.dry_run)
    sys.exit(rc)


if __name__ == "__main__":
    main()
