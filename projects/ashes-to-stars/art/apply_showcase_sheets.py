#!/usr/bin/env python3
"""전시용 시트에 적힌 줄 이름대로 SpriteBank 13장에 붙인다.

다시 뽑지 않는다. 칸을 자르고 배경만 뺀다.

시트에 적힌 뜻 (오너 지시: 설명을 읽고 적용):
  tank   IDLE / RUN / 방패 돌격 / 도끼 휩쓸기 / 대지의 철벽
  dps    Idle / Run / Twin Strike / Whirling Dervish / Shadow Cascade
  mage   IDLE / RUN / Abyss Blast / Shadow Teleport / Void Chasm
  healer 로코모션 / 부드러운 치료 / 정령의 채찍 / 눈부신 포옹
  buffer Walk·Float / Silk Trap / Haste Buff / Cosmic Weaver
"""
from __future__ import annotations

import sys
from pathlib import Path

import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import knock_bg

RES = HERE.parent / "unity" / "Assets" / "Resources" / "sprites"
PORT = HERE.parent / "unity" / "Assets" / "Resources" / "ui" / "portraits"
SRC = HERE / "out_p25_showcase"
FRAMES = (
    "idle_00", "walk_00", "walk_01", "attack_00", "attack_01",
    "special_00", "hurt_00", "death_00",
    "dash_00", "dash_01", "dash_02", "dash_03", "invuln_00",
)

# 파일 → 직업, 기대 줄 수, (프레임 이름 → 줄,칸)
# 칸은 0부터. 시트 라벨 순서를 그대로 따른다.
SHEETS = {
    "tank": {
        "file": "tank_checker_ko.jpg",
        "rows": 5,
        "note": "IDLE / RUN / 방패돌격 / 도끼휩쓸기 / 대지의철벽",
        "map": {
            "idle_00": (0, 0),
            "walk_00": (1, 1), "walk_01": (1, 3),
            "attack_00": (2, 2), "attack_01": (3, 2),   # 방패 돌격 · 베기
            "special_00": (3, 3),                       # 도끼 휩쓸기
            "hurt_00": (4, 2),
            "death_00": (4, 4),
            "dash_00": (1, 0), "dash_01": (1, 2),
            "dash_02": (1, 4), "dash_03": (1, 5),
            "invuln_00": (4, 5),                        # 철벽 대신 방패 대기
        },
    },
    "dps": {
        "file": "dps_vespid.png",
        "rows": 5,
        "note": "Idle / Run / Twin Strike / Whirling / Shadow Cascade",
        "map": {
            "idle_00": (0, 0),
            "walk_00": (1, 1), "walk_01": (1, 3),
            "attack_00": (2, 2), "attack_01": (2, 3),   # Twin Strike
            "special_00": (3, 3),                       # Whirling Dervish
            "hurt_00": (4, 0),
            "death_00": (4, 5),
            "dash_00": (1, 0), "dash_01": (1, 2),
            "dash_02": (1, 4), "dash_03": (1, 5),
            "invuln_00": (4, 3),                        # Cascade 한복판
        },
    },
    "mage": {
        "file": "mage_magenta.png",
        "rows": 5,
        "note": "IDLE / RUN / Abyss Blast / Shadow Teleport / Void Chasm",
        "map": {
            "idle_00": (0, 0),
            "walk_00": (1, 1), "walk_01": (1, 3),
            "attack_00": (2, 2), "attack_01": (2, 4),   # Blast · 발사
            "special_00": (4, 2),                       # Void Chasm
            "hurt_00": (3, 4),                          # 텔레포트 소멸
            "death_00": (4, 5),
            "dash_00": (3, 1), "dash_01": (3, 2),       # Shadow Teleport
            "dash_02": (3, 3), "dash_03": (3, 5),
            "invuln_00": (4, 4),
        },
    },
    "healer": {
        "file": "healer_gray.png",
        "rows": 4,
        "note": "로코모션 / 치료 / 채찍 / 눈부신 포옹",
        "map": {
            "idle_00": (0, 0),
            "walk_00": (0, 2), "walk_01": (0, 4),
            "attack_00": (2, 2), "attack_01": (2, 3),   # 정령의 채찍
            "special_00": (1, 4),                       # 부드러운 치료
            "hurt_00": (3, 5),
            "death_00": (3, 5),
            "dash_00": (0, 1), "dash_01": (0, 3),
            "dash_02": (0, 5), "dash_03": (2, 5),
            "invuln_00": (3, 3),                        # 눈부신 포옹
        },
    },
    "buffer": {
        "file": "buffer_checker.png",
        "rows": 4,
        "note": "Walk·Float / Silk Trap / Haste / Cosmic Weaver",
        "map": {
            "idle_00": (0, 0),
            "walk_00": (0, 1), "walk_01": (0, 3),
            "attack_00": (1, 3), "attack_01": (1, 4),   # Silk Trap
            "special_00": (2, 3),                       # Haste Buff
            "hurt_00": (3, 5),
            "death_00": (3, 4),
            "dash_00": (0, 2), "dash_01": (0, 4),
            "dash_02": (0, 5), "dash_03": (2, 5),
            "invuln_00": (3, 2),                        # Cosmic Weaver
        },
    },
}


def _smooth(x: np.ndarray, k: int = 15) -> np.ndarray:
    k = max(3, k | 1)
    ker = np.ones(k) / k
    return np.convolve(x, ker, mode="same")


def _runs(mask: np.ndarray, min_len: int) -> list[tuple[int, int]]:
    idx = np.where(mask)[0]
    if len(idx) == 0:
        return []
    out, s, p = [], int(idx[0]), int(idx[0])
    for i in idx[1:]:
        i = int(i)
        if i - p > 3:
            if p - s + 1 >= min_len:
                out.append((s, p + 1))
            s = i
        p = i
    if p - s + 1 >= min_len:
        out.append((s, p + 1))
    return out


def _pink(rgb: np.ndarray) -> np.ndarray:
    r = rgb[..., 0].astype(np.int16)
    g = rgb[..., 1].astype(np.int16)
    b = rgb[..., 2].astype(np.int16)
    return (r > 150) & (b > 120) & (g < np.minimum(r, b) - 25)


def row_bands(rgb: np.ndarray, expect: int) -> list[tuple[int, int]]:
    """캐릭터 줄. 마젠타 장이면 마젠타가 아닌 띠, 아니면 분산 띠."""
    h = rgb.shape[0]
    pink = _pink(rgb)
    if float(pink.mean()) > 0.12:
        fill = (~pink).mean(axis=1)
        sm = _smooth(fill, max(9, h // 90))
        bands = _runs(sm > 0.06, min_len=max(36, h // 22))
    else:
        var = rgb.reshape(h, -1).astype(np.float32).var(axis=1)
        sm = _smooth(var, max(11, h // 80))
        thr = float(np.percentile(sm, 45))
        bands = _runs(sm > thr, min_len=max(40, h // 20))
    if len(bands) == expect:
        return bands
    if len(bands) > expect:
        bands = sorted(bands, key=lambda b: b[1] - b[0], reverse=True)[:expect]
        return sorted(bands)
    top = bands[0][0] if bands else int(h * 0.08)
    bot = bands[-1][1] if bands else h
    step = (bot - top) / expect
    return [(int(top + i * step), int(top + (i + 1) * step)) for i in range(expect)]


def col_boxes(rgb: np.ndarray, y0: int, y1: int, expect: int = 6) -> list[tuple[int, int]]:
    """한 줄 안에서 6칸. 마젠타 홈이 있으면 그걸 경계로 쓴다."""
    band = rgb[y0:y1]
    r, g, b = band[..., 0].astype(np.int16), band[..., 1].astype(np.int16), band[..., 2].astype(np.int16)
    pink = (r > 150) & (b > 120) & (g < np.minimum(r, b) - 25)
    gutter = pink.mean(axis=0)
    if float(gutter.mean()) > 0.15:
        content = _runs(gutter < 0.45, min_len=max(24, band.shape[1] // 14))
        if len(content) >= expect - 1:
            return content[:expect] if len(content) > expect else content
    w = band.shape[1]
    step = w / expect
    pad = int(step * 0.04)
    return [(int(i * step) + pad, int((i + 1) * step) - pad) for i in range(expect)]


def split_grid(im: Image.Image, expect_rows: int) -> list[list[Image.Image]]:
    rgb = np.asarray(im.convert("RGB"))
    rows = row_bands(rgb, expect_rows)
    grid = []
    for y0, y1 in rows:
        # 줄 위아래 라벨을 조금 자른다
        pad_y = max(2, (y1 - y0) // 16)
        yy0, yy1 = y0 + pad_y, y1 - max(2, pad_y // 4)
        cols = col_boxes(rgb, yy0, yy1, 6)
        row = []
        for x0, x1 in cols:
            cell = im.crop((x0, yy0, x1, yy1))
            cell = knock_bg.apply(cell, crop=True)
            row.append(cell)
        grid.append(row)
    return grid


def at(grid, rc) -> Image.Image:
    r, c = rc
    row = grid[min(r, len(grid) - 1)]
    if not row:
        raise SystemExit("빈 줄")
    if c < 0:
        c = len(row) + c
    return row[min(c, len(row) - 1)]


def align(frames: dict[str, Image.Image], height: int = 192) -> dict[str, Image.Image]:
    boxes = []
    for im in frames.values():
        a = np.asarray(im.convert("RGBA"))[..., 3]
        ys, xs = np.where(a > 16)
        if len(xs):
            boxes.append((int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1))
    if not boxes:
        return frames
    cw = max(b[2] - b[0] for b in boxes) + 10
    ch = max(b[3] - b[1] for b in boxes) + 10
    scale = height / ch
    out = {}
    for name, im in frames.items():
        a = np.asarray(im.convert("RGBA"))[..., 3]
        ys, xs = np.where(a > 16)
        if not len(xs):
            out[name] = Image.new("RGBA", (max(1, round(cw * scale)), height), (0, 0, 0, 0))
            continue
        crop = im.convert("RGBA").crop((int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1))
        canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
        canvas.alpha_composite(crop, ((cw - crop.width) // 2, ch - 4 - crop.height))
        out[name] = canvas.resize((max(1, round(cw * scale)), height), Image.Resampling.LANCZOS)
    return out


def preview(job: str, grid, frames) -> None:
    d = SRC / "_preview"
    d.mkdir(parents=True, exist_ok=True)
    tw = 110
    bar = Image.new("RGBA", (len(FRAMES) * tw, 210), (18, 18, 22, 255))
    for i, name in enumerate(FRAMES):
        im = frames[name].copy()
        im.thumbnail((tw - 8, 192))
        bar.alpha_composite(im, (i * tw + 4, 200 - im.height))
    bar.save(d / f"{job}_mapped.png")


def write_job(job: str, frames: dict[str, Image.Image]) -> int:
    dest = RES / job
    dest.mkdir(parents=True, exist_ok=True)
    for name, im in frames.items():
        im.save(dest / f"{job}_{name}.png")
    PORT.mkdir(parents=True, exist_ok=True)
    frames["idle_00"].save(PORT / f"{job}.png")
    return len(frames)


def main() -> int:
    for job, spec in SHEETS.items():
        path = SRC / spec["file"]
        if not path.exists():
            print("없음", path.name)
            continue
        im = Image.open(path)
        grid = split_grid(im, spec["rows"])
        print(f"{job} ({spec['note']}): " + " ".join(str(len(r)) for r in grid) + "칸")
        raw = {name: at(grid, rc) for name, rc in spec["map"].items()}
        frames = align(raw)
        if job == "dps":
            frames = {k: knock_bg.strip_gray_checker(v) for k, v in frames.items()}
        preview(job, grid, frames)
        n = write_job(job, frames)
        dirty = []
        for name, im in frames.items():
            pct = knock_bg.leftover_white_pct(im)
            if pct > 2.0 and name not in ("invuln_00", "special_00", "attack_01"):
                dirty.append(f"{name} {pct:.1f}%")
        if dirty:
            print(f"  ⚠️ 흰 잔재 {len(dirty)}: " + ", ".join(dirty))
        print(f"  → sprites/{job}/ {n}장")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
