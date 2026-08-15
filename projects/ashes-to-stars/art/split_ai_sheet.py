"""
AI 생성 시트 → 개별 프레임 (정렬 포함)

왜 기존 `blender/split_sheets.py`를 안 쓰는가:
  그건 오너 시트 규격(배경 #0D0D0D, 밝기 45~130 격자선, 셀 128px, 여백 6px)에
  맞춰져 있다. AI는 그 규격을 지키지 못한다 — 격자선을 안 그리고, 셀 크기도
  들쭉날쭉하다. 규격을 못 지키는 생성기를 탓하기보다 **행·열을 알고 자르는**
  쪽이 싸다(문서 §2 주석의 판단과 같다: 사람이 다시 그리는 것보다 스크립트가 싸다).

정렬이 핵심이다:
  AI 시트는 셀마다 캐릭터 크기와 발 높이가 다르다. 그대로 자르면 재생 중에
  캐릭터가 커졌다 작아졌다 하고 발이 땅에서 뜬다. `SpriteBank`는 몹도
  **idle 한 장으로 배율을 정해 전 프레임에 같은 값**을 쓰므로(주석 §294),
  크기 편차를 런타임이 흡수해주지 않는다 — 이미지 단계에서 맞춰야 한다.

  ① 셀마다 내용 bbox를 잰다
  ② 가장 큰 내용을 기준으로 **공통 캔버스**를 잡는다
  ③ 가로는 중앙, 세로는 **발밑(bbox 하단)**을 같은 선에 맞춘다

사용:
    python3 split_ai_sheet.py sheet.png --cols 3 --rows 2 \
        --names idle_00 idle_01 walk_00 walk_01 attack_00 death_00 \
        --out-dir out_mobs/mob_chaser --prefix mob_chaser
"""
from __future__ import annotations

import argparse
import os
import sys

import numpy as np
from PIL import Image

CHROMA = (255, 0, 255)


def cell_boxes(img: Image.Image, cols: int, rows: int):
    w, h = img.width // cols, img.height // rows
    for r in range(rows):
        for c in range(cols):
            yield (c * w, r * h, (c + 1) * w, (r + 1) * h)


def _segments(mask_1d: np.ndarray, min_gap: int) -> list[tuple[int, int]]:
    """1차원 점유 배열에서 '내용 덩어리' 구간들을 뽑는다."""
    idx = np.where(mask_1d)[0]
    if len(idx) == 0:
        return []
    segs, start, prev = [], idx[0], idx[0]
    for i in idx[1:]:
        if i - prev > min_gap:          # 빈 줄이 충분히 길면 경계
            segs.append((start, prev + 1))
            start = i
        prev = i
    segs.append((start, prev + 1))
    return segs


def auto_cell_boxes(img: Image.Image, tol: int = 120, min_gap_frac: float = 0.012):
    """
    격자를 **검출**한다 — AI가 요청한 행·열을 안 지키기 때문이다(2026-08-15 실측:
    5×2를 요청했는데 4×3이 나왔다). 행·열을 미리 알아야 하는 구조면 그때마다
    이름 매핑이 통째로 어긋난다. 배경이 순수 마젠타라 검출은 결정적이고 싸다.

    행을 먼저 가르고, **행마다 따로** 열을 가른다 — 행별로 개체 수가 다를 수 있고
    (마지막 행이 덜 찬 경우), 전역 열 분할은 그걸 못 다룬다.
    """
    a = np.asarray(img.convert("RGB")).astype(np.int16)
    d = np.sqrt(((a - np.array(CHROMA)) ** 2).sum(axis=2))
    fg = d >= tol                                   # 내용 = 마젠타가 아닌 곳

    row_gap = max(2, int(img.height * min_gap_frac))
    col_gap = max(2, int(img.width * min_gap_frac))
    boxes = []
    # 유령 셀 거르기: 격자선을 지운 자리에 1~2px 잔여가 남아 셀로 잡힌다
    # (2026-08-15 실측: 6셀 시트에서 폭 1px짜리 7번째 셀이 나와 이름 매핑이 어긋났다).
    # 캐릭터 셀은 시트 폭·높이의 최소 5%는 된다 — 그보다 작으면 그림이 아니다.
    min_w = max(4, int(img.width * 0.05))
    min_h = max(4, int(img.height * 0.05))

    for y0, y1 in _segments(fg.any(axis=1), row_gap):
        band = fg[y0:y1]
        if y1 - y0 < min_h:
            continue
        for x0, x1 in _segments(band.any(axis=0), col_gap):
            if x1 - x0 < min_w:
                continue
            boxes.append((int(x0), int(y0), int(x1), int(y1)))
    return boxes


def keyed(cell: Image.Image, tol: int = 120) -> Image.Image:
    """마젠타 제거 + 디스필. aigen.chroma_key와 같은 규칙을 쓴다."""
    a = np.asarray(cell.convert("RGB")).astype(np.int16)
    d = np.sqrt(((a - np.array(CHROMA)) ** 2).sum(axis=2))
    alpha = np.where(d < tol, 0, 255).astype(np.uint8)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    spill = np.minimum(r, b) - g
    hit = (alpha == 255) & (spill > 0)
    cap = g + np.maximum(0, (spill * 0.25).astype(np.int16))
    rgb = a.copy()
    rgb[..., 0] = np.where(hit, np.minimum(r, cap), r)
    rgb[..., 2] = np.where(hit, np.minimum(b, cap), b)
    return Image.fromarray(
        np.dstack([np.clip(rgb, 0, 255).astype(np.uint8), alpha]), "RGBA")


def bbox_of(im: Image.Image):
    a = np.asarray(im)[..., 3]
    ys, xs = np.where(a > 128)
    if len(xs) == 0:
        return None
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def main(argv=None):
    ap = argparse.ArgumentParser(description="AI 시트 분할 + 정렬")
    ap.add_argument("sheet")
    ap.add_argument("--cols", type=int, default=0, help="0이면 격자 자동 검출")
    ap.add_argument("--rows", type=int, default=0)
    ap.add_argument("--names", nargs="+", required=True)
    ap.add_argument("--out-dir", required=True)
    ap.add_argument("--prefix", required=True)
    ap.add_argument("--height", type=int, default=128, help="최종 프레임 높이")
    ap.add_argument("--pad", type=int, default=4)
    ns = ap.parse_args(argv)

    sheet = Image.open(ns.sheet)
    if ns.cols and ns.rows:
        boxes_raw = list(cell_boxes(sheet, ns.cols, ns.rows))
        how = f"고정 격자 {ns.cols}×{ns.rows}"
    else:
        boxes_raw = auto_cell_boxes(sheet)
        how = f"자동 검출 {len(boxes_raw)}셀"
    cells = [keyed(sheet.crop(b)) for b in boxes_raw]

    if len(ns.names) != len(cells):
        # 개수가 어긋나면 **조용히 밀려 쓰지 않는다** — 이름과 그림이 한 칸씩 밀리면
        # 걷기 프레임이 죽는 모션으로 재생되고, 그건 화면을 봐야만 발견된다.
        raise SystemExit(f"{how}: 셀 {len(cells)}개 ≠ 이름 {len(ns.names)}개.\n"
                         f"  셀 좌표: {boxes_raw}\n"
                         f"  → 이름 개수를 맞추거나 --cols/--rows로 격자를 직접 지정할 것")
    print(f"[분할] {how}")

    boxes = [bbox_of(c) for c in cells]
    good = [(c, b, n) for c, b, n in zip(cells, boxes, ns.names) if b]
    if not good:
        raise SystemExit("내용이 있는 셀이 없다 — 크로마키 임계나 행·열을 확인할 것")

    # ② 공통 캔버스 — 가장 큰 내용을 담을 수 있어야 잘리지 않는다
    cw = max(b[2] - b[0] for _, b, _ in good) + ns.pad * 2
    chh = max(b[3] - b[1] for _, b, _ in good) + ns.pad * 2

    os.makedirs(ns.out_dir, exist_ok=True)
    scale = ns.height / chh
    report = []
    for cell, b, name in good:
        crop = cell.crop(b)
        canvas = Image.new("RGBA", (cw, chh), (0, 0, 0, 0))
        # ③ 가로 중앙 · 세로는 발밑을 공통 바닥선(캔버스 하단 - pad)에 맞춘다
        canvas.alpha_composite(crop, ((cw - crop.width) // 2, chh - ns.pad - crop.height))
        out = canvas.resize((max(1, round(cw * scale)), ns.height), Image.Resampling.BOX)
        dst = os.path.join(ns.out_dir, f"{ns.prefix}_{name}.png")
        out.save(dst)
        report.append((name, crop.width, crop.height))

    hs = [h for _, _, h in report]
    print(f"{len(report)}프레임 → {ns.out_dir}  캔버스 {cw}x{chh} → {out.width}x{ns.height}")
    print(f"  내용 높이 편차: {min(hs)}~{max(hs)}px (편차 {max(hs)/max(1,min(hs)):.2f}배)"
          + ("  ⚠️ 2배 넘으면 재생 중 크기가 튄다" if max(hs) / max(1, min(hs)) > 2 else ""))
    missing = [n for n in ns.names if n not in [r[0] for r in report]]
    if missing:
        print(f"  ⚠️ 빈 셀({len(missing)}): {', '.join(missing)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
