"""스프라이트 배경 제거 — 마젠타가 없어도 작동한다.

왜 생겼나(2026-08-18 실측, 클로드): spec_p22 첫 6장은 칸 안이 흰색·회색·남색이고
마젠타는 칸 사이 줄뿐이거나 아예 없었다. `chroma_key`는 #FF00FF만 지운다.
흰 배경을 '흰 픽셀 전부'로 지우면 할로우 가면(뼈색 흰색)이 같이 판다.

규칙:
  1. 마젠타(#FF00FF 근처)는 거리로 지운다(기존 크로마 + 디스필).
  2. 남은 불투명이 가장자리에 닿아 있고 그 색이 배경처럼 보이면, **가장자리에서만**
     같은 색을 따라 지운다. 검은 외곽선이 가면을 지킨다.
  3. 작은 덩어리(WALK L 같은 라벨)는 버린다.
  4. 너무 많이 지워지면 되돌린다 — 어두운 FX를 통째로 삼키지 않기 위해.
"""
from __future__ import annotations

from collections import deque

import numpy as np
from PIL import Image

CHROMA = np.array([255, 0, 255], dtype=np.int16)
CHROMA_TOL = 120
FLOOD_TOL = 32
LIGHT_L = 140          # 흰·밝은 회색 배경
MIN_KEEP_FRAC = 0.12   # 원래 불투명의 이 비율 아래로 내려가면 플러드 취소
SMALL_FRAC = 0.08      # 제일 큰 덩어리 대비 이보다 작으면 라벨


def _despill(rgb: np.ndarray, keep: np.ndarray) -> np.ndarray:
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    spill = np.minimum(r, b) - g
    hit = keep & (spill > 0)
    cap = g + np.maximum(0, (spill * 0.25).astype(np.int16))
    out = rgb.copy()
    out[..., 0] = np.where(hit, np.minimum(r, cap), r)
    out[..., 2] = np.where(hit, np.minimum(b, cap), b)
    return np.clip(out, 0, 255).astype(np.uint8)


def _chroma_mask(rgb: np.ndarray, tol: int = CHROMA_TOL) -> np.ndarray:
    d2 = ((rgb.astype(np.int32) - CHROMA.astype(np.int32)) ** 2).sum(axis=2)
    near = d2 < (tol * tol)
    # 힉스가 내는 '마젠타'는 #FF00FF가 아니라 분홍(184,63,145)인 경우가 많다.
    r = rgb[..., 0].astype(np.int16)
    g = rgb[..., 1].astype(np.int16)
    b = rgb[..., 2].astype(np.int16)
    pink = (r > 150) & (b > 120) & (g < np.minimum(r, b) - 25)
    return near | pink


def _checker_mask(rgb: np.ndarray) -> np.ndarray:
    """포토샵 투명 바둑판. 밝은 칸 + 그에 맞닿은 어두운 칸. 몸(어두운 채색)은 안 지운다."""
    r = rgb[..., 0].astype(np.int16)
    g = rgb[..., 1].astype(np.int16)
    b = rgb[..., 2].astype(np.int16)
    gray = (np.abs(r - g) < 22) & (np.abs(g - b) < 22) & (np.abs(r - b) < 22)
    L = (0.299 * r + 0.587 * g + 0.114 * b)
    # 상한 232 — JPEG 흰 칸(~228)은 지우고 뼈색 가면(~244)은 남긴다.
    light = gray & (L > 140) & (L <= 232)
    dark = gray & (L < 130)
    near = light.copy()
    for _ in range(3):
        p = np.pad(near, 1, constant_values=False)
        near = near | p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]
    return light | (dark & near)


def _harsh_checker_mask(rgb: np.ndarray) -> np.ndarray:
    """모서리가 바둑판일 때만. JPEG 흰·검정 칸(L≈255/0)을 추가로 지운다."""
    r = rgb[..., 0].astype(np.int16)
    g = rgb[..., 1].astype(np.int16)
    b = rgb[..., 2].astype(np.int16)
    gray = (np.abs(r - g) < 28) & (np.abs(g - b) < 28)
    L = (0.299 * r + 0.587 * g + 0.114 * b)
    white = gray & (L > 200) & (L <= 236)
    black = gray & (L < 90)
    near = white.copy()
    for _ in range(4):
        p = np.pad(near, 1, constant_values=False)
        near = near | p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]
    return white | (black & near)


def _corner_is_checker(rgb: np.ndarray) -> bool:
    h, w = rgb.shape[:2]
    p = min(14, h // 5 or 1, w // 5 or 1)
    boxes = ((0, 0, p, p), (0, w - p, p, w), (h - p, 0, h, p), (h - p, w - p, h, w))
    r = rgb[..., 0].astype(np.int16)
    g = rgb[..., 1].astype(np.int16)
    b = rgb[..., 2].astype(np.int16)
    gray = (np.abs(r - g) < 28) & (np.abs(g - b) < 28)
    L = (0.299 * r + 0.587 * g + 0.114 * b)
    for y0, x0, y1, x1 in boxes:
        sl = gray[y0:y1, x0:x1]
        if sl.size == 0:
            continue
        ll = L[y0:y1, x0:x1]
        light = float((sl & (ll > 160)).mean())
        dark = float((sl & (ll < 110)).mean())
        if light > 0.18 and dark > 0.18:
            return True
    return False


def _center_has_mask(rgb: np.ndarray) -> bool:
    """가운데에 뼈색 가면이 있으면 흰 바둑판 강제를 쓰지 않는다."""
    h, w = rgb.shape[:2]
    y0, y1, x0, x1 = h // 4, 3 * h // 4, w // 4, 3 * w // 4
    patch = rgb[y0:y1, x0:x1]
    r, g, b = patch[..., 0].astype(np.int16), patch[..., 1].astype(np.int16), patch[..., 2].astype(np.int16)
    gray = (np.abs(r - g) < 22) & (np.abs(g - b) < 22)
    L = 0.299 * r + 0.587 * g + 0.114 * b
    return float((gray & (L > 220)).mean()) > 0.06


def _corner_ref(rgb: np.ndarray, opaque: np.ndarray, patch: int = 8):
    """네 모서리의 불투명 픽셀 중앙값 = 배경색. 캐릭터가 중앙에 있으면 모서리는 배경이다."""
    h, w = opaque.shape
    p = min(patch, h // 4 or 1, w // 4 or 1)
    boxes = ((0, 0, p, p), (0, w - p, p, w), (h - p, 0, h, p), (h - p, w - p, h, w))
    chunks = []
    for y0, x0, y1, x1 in boxes:
        sl = opaque[y0:y1, x0:x1]
        if sl.any():
            chunks.append(rgb[y0:y1, x0:x1][sl].astype(np.int16))
    if not chunks:
        return None
    return np.median(np.concatenate(chunks, axis=0), axis=0)


def _flood_to_ref(rgb: np.ndarray, opaque: np.ndarray, ref: np.ndarray,
                  l1_tol: int) -> np.ndarray:
    """모서리 색(ref)에 가까운 픽셀만, 모서리에서 연결되면 배경."""
    h, w = opaque.shape
    l1 = np.abs(rgb.astype(np.int16) - ref.astype(np.int16)).sum(axis=2)
    allowed = opaque & (l1 <= l1_tol)
    if not allowed.any():
        return np.zeros((h, w), dtype=bool)
    bg = np.zeros((h, w), dtype=bool)
    q: deque[tuple[int, int]] = deque()
    for x in range(w):
        if allowed[0, x]:
            bg[0, x] = True
            q.append((0, x))
        if allowed[h - 1, x]:
            bg[h - 1, x] = True
            q.append((h - 1, x))
    for y in range(1, h - 1):
        if allowed[y, 0]:
            bg[y, 0] = True
            q.append((y, 0))
        if allowed[y, w - 1]:
            bg[y, w - 1] = True
            q.append((y, w - 1))
    while q:
        y, x = q.popleft()
        for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
            if 0 <= ny < h and 0 <= nx < w and allowed[ny, nx] and not bg[ny, nx]:
                bg[ny, nx] = True
                q.append((ny, nx))
    return bg


def _labels(mask: np.ndarray) -> tuple[np.ndarray, int]:
    h, w = mask.shape
    lab = np.zeros((h, w), dtype=np.int32)
    n = 0
    ys, xs = np.where(mask)
    for y, x in zip(ys.tolist(), xs.tolist()):
        if lab[y, x]:
            continue
        n += 1
        q: deque[tuple[int, int]] = deque([(y, x)])
        lab[y, x] = n
        while q:
            cy, cx = q.popleft()
            for ny, nx in ((cy - 1, cx), (cy + 1, cx), (cy, cx - 1), (cy, cx + 1)):
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and lab[ny, nx] == 0:
                    lab[ny, nx] = n
                    q.append((ny, nx))
    return lab, n


def _drop_small(keep: np.ndarray) -> np.ndarray:
    lab, n = _labels(keep)
    if n <= 1:
        return keep
    counts = np.bincount(lab.ravel())
    counts[0] = 0
    biggest = int(counts.max())
    if biggest <= 0:
        return keep
    good = np.zeros(n + 1, dtype=bool)
    for i in range(1, n + 1):
        if counts[i] >= biggest * SMALL_FRAC:
            good[i] = True
    return good[lab]


def apply(img: Image.Image, crop: bool = False) -> Image.Image:
    """RGBA로 배경을 뺀다. crop=True면 내용 bbox로 자른다(단독 장)."""
    im = img.convert("RGBA")
    arr = np.asarray(im).copy()
    rgb = arr[..., :3]
    alpha = arr[..., 3]
    opaque0 = alpha > 0
    if not opaque0.any():
        return im

    mag = _chroma_mask(rgb)
    # 바둑판은 모서리가 바둑판일 때만. 가면(뼈색 큰 원)을 회색 바둑판으로 지우면 안 된다.
    chk = _checker_mask(rgb) | _harsh_checker_mask(rgb) if _corner_is_checker(rgb) else np.zeros(alpha.shape, dtype=bool)
    alpha = np.where(mag | chk, 0, alpha)
    rgb = _despill(rgb.astype(np.int16), alpha > 0)

    opaque = alpha > 0
    ref = _corner_ref(rgb, opaque)
    mag_frac = float(mag.mean())
    # 마젠타를 충분히 지웠으면 칸 안 배경은 이미 투명. 모서리에 남은 검은
    # 격자선을 따라가면 몸을 먹는다 → 플러드는 마젠타가 거의 없을 때만.
    if ref is not None and mag_frac < 0.08:
        ref_l = float(0.299 * ref[0] + 0.587 * ref[1] + 0.114 * ref[2])
        # 흰 배경은 가면(뼈색)과 가까우니 허용폭을 좁힌다. 외곽선이 막는다.
        l1_tol = 36 if ref_l >= LIGHT_L else FLOOD_TOL * 3
        bg = _flood_to_ref(rgb, opaque, ref, l1_tol)
        kept = opaque & ~bg
        if kept.any() and kept.mean() >= MIN_KEEP_FRAC * max(float(opaque0.mean()), 1e-6):
            alpha = np.where(bg, 0, alpha)

    keep = alpha > 0
    if keep.any():
        keep = _drop_paper_blobs(rgb, keep)
        keep = _drop_small(keep)
        alpha = np.where(keep, alpha, 0)

    out = np.dstack([rgb, alpha.astype(np.uint8)])
    im = Image.fromarray(out, "RGBA")
    if crop:
        ys, xs = np.where(alpha > 0)
        if len(xs):
            im = im.crop((int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1))
    return im


def _drop_paper_blobs(rgb: np.ndarray, keep: np.ndarray) -> np.ndarray:
    """남은 흰 카드·바둑판 덩어리를 지운다. 초승달 베기(속이 빈 흰 덩어리)는 남긴다.

    2026-08-18 사고: 적용만 하고 화면을 안 보니 딜러 대시가 흰 사각형으로 떴다.
    """
    r = rgb[..., 0].astype(np.int16)
    g = rgb[..., 1].astype(np.int16)
    b = rgb[..., 2].astype(np.int16)
    sat = np.maximum(np.maximum(r, g), b) - np.minimum(np.minimum(r, g), b)
    L = 0.299 * r + 0.587 * g + 0.114 * b
    paper = keep & (sat < 28) & ((L > 200) | (L < 80))
    if not paper.any():
        return keep
    lab, n = _labels(paper)
    if n == 0:
        return keep
    counts = np.bincount(lab.ravel())
    out = keep.copy()
    for i in range(1, n + 1):
        if counts[i] < 20:
            out[lab == i] = False
            continue
        ys, xs = np.where(lab == i)
        bw = int(xs.max()) - int(xs.min()) + 1
        bh = int(ys.max()) - int(ys.min()) + 1
        area = int(counts[i])
        solid = area / max(1, bw * bh)
        ll = L[lab == i]
        mix = float(ll.std()) > 35
        paper = float(ll.mean()) > 210 and float(ll.std()) < 20 and solid > 0.72
        edge = (int(xs.min()) <= 2 or int(ys.min()) <= 2
                or int(xs.max()) >= keep.shape[1] - 3
                or int(ys.max()) >= keep.shape[0] - 3)
        if edge and (mix or paper):
            out[lab == i] = False
    return out


def strip_gray_checker(img: Image.Image) -> Image.Image:
    """뼈색 가면이 없는 장(딜러) 전용. 회색 바둑판만 지운다."""
    im = img.convert("RGBA")
    a = np.asarray(im).copy()
    rgb, al = a[..., :3].astype(np.int16), a[..., 3]
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    gray = (np.abs(r - g) < 25) & (np.abs(g - b) < 25) & (np.abs(r - b) < 25)
    L = 0.299 * r + 0.587 * g + 0.114 * b
    kill = gray & ((L > 175) | (L < 110))
    al = np.where(kill, 0, al)
    return Image.fromarray(np.dstack([a[..., :3], al]), "RGBA")


def leftover_white_pct(img: Image.Image) -> float:
    """불투명 흰 종이(채도 낮고 밝음) 비율. 초승달 베기는 모양이 달라 거의 안 잡힌다."""
    a = np.asarray(img.convert("RGBA"))
    rgb, al = a[..., :3].astype(np.int16), a[..., 3]
    op = al > 40
    if not op.any():
        return 0.0
    sat = rgb.max(2) - rgb.min(2)
    L = 0.299 * rgb[..., 0] + 0.587 * rgb[..., 1] + 0.114 * rgb[..., 2]
    gray = (np.abs(rgb[..., 0] - rgb[..., 1]) < 18) & (np.abs(rgb[..., 1] - rgb[..., 2]) < 18)
    # 크림색 힐러 옷(채도 있음)은 종이로 세지 않는다.
    paper = op & gray & (sat < 18) & (L > 210)
    chk = op & gray & (L > 140) & (L < 236)
    return float((paper | chk).mean() * 100)


def apply_path(src, dst=None, crop: bool = True) -> None:
    from pathlib import Path
    src_p = Path(src)
    dst_p = Path(dst) if dst is not None else src_p
    dst_p.parent.mkdir(parents=True, exist_ok=True)
    apply(Image.open(src_p), crop=crop).save(dst_p)
