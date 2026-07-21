#!/usr/bin/env python3
"""Split the AI-generated FleetView walking sheet into per-character frames."""
from __future__ import annotations

from pathlib import Path
from PIL import Image


HERE = Path(__file__).resolve().parent
WALK_DIR = HERE / "sprites" / "generated_walk"
CHIBI_DIR = HERE / "sprites" / "chibi"
MASTER = WALK_DIR / "master_alpha.png"
KEYS = ["yewon", "youngsuk", "bomi", "teo", "baekho", "suri", "mio", "namu"]
FRAME_W = 512
FRAME_H = 560


def alpha_bbox(im: Image.Image) -> tuple[int, int, int, int]:
    return im.getchannel("A").getbbox() or (0, 0, im.width, im.height)


def keep_largest_alpha_component(im: Image.Image) -> Image.Image:
    alpha = im.getchannel("A")
    pixels = alpha.load()
    visited: set[tuple[int, int]] = set()
    best: tuple[int, int, int, int, int] | None = None

    for y in range(im.height):
        for x in range(im.width):
            if pixels[x, y] < 16 or (x, y) in visited:
                continue
            stack = [(x, y)]
            visited.add((x, y))
            count = 0
            l = r = x
            t = b = y
            while stack:
                px, py = stack.pop()
                count += 1
                l = min(l, px)
                r = max(r, px)
                t = min(t, py)
                b = max(b, py)
                for nx, ny in ((px + 1, py), (px - 1, py), (px, py + 1), (px, py - 1)):
                    if nx < 0 or ny < 0 or nx >= im.width or ny >= im.height:
                        continue
                    if (nx, ny) in visited or pixels[nx, ny] < 16:
                        continue
                    visited.add((nx, ny))
                    stack.append((nx, ny))
            if best is None or count > best[0]:
                best = (count, l, t, r + 1, b + 1)

    if best is None:
        return im
    _, l, t, r, b = best
    kept = Image.new("RGBA", im.size, (0, 0, 0, 0))
    kept.alpha_composite(im.crop((l, t, r, b)), (l, t))
    return kept


def normalize_cell(cell: Image.Image, key: str) -> Image.Image:
    cell = cell.convert("RGBA")
    cell = keep_largest_alpha_component(cell)
    bbox = alpha_bbox(cell)
    sprite = cell.crop(bbox)

    original = Image.open(CHIBI_DIR / f"{key}.png").convert("RGBA")
    original_box = alpha_bbox(original)
    target_h = original_box[3] - original_box[1]
    scale = target_h / max(1, sprite.height)
    target_w = min(FRAME_W - 28, int(sprite.width * scale))
    if target_w == FRAME_W - 28:
        target_h = int(sprite.height * (target_w / max(1, sprite.width)))
    sprite = sprite.resize((target_w, target_h), Image.Resampling.LANCZOS)

    frame = Image.new("RGBA", (FRAME_W, FRAME_H), (0, 0, 0, 0))
    x = (FRAME_W - sprite.width) // 2
    y = min(FRAME_H - sprite.height - 8, original_box[3] - sprite.height)
    frame.alpha_composite(sprite, (x, y))
    return frame


def main() -> None:
    WALK_DIR.mkdir(parents=True, exist_ok=True)
    master = Image.open(MASTER).convert("RGBA")
    cell_w = master.width // 4
    cell_h = master.height // 8

    for row, key in enumerate(KEYS):
        sheet = Image.new("RGBA", (FRAME_W * 4, FRAME_H), (0, 0, 0, 0))
        for col in range(4):
            crop = master.crop((col * cell_w, row * cell_h, (col + 1) * cell_w, (row + 1) * cell_h))
            frame = normalize_cell(crop, key)
            sheet.alpha_composite(frame, (col * FRAME_W, 0))
        out = WALK_DIR / f"{key}.png"
        sheet.save(out)
        print(f"saved {out}")


if __name__ == "__main__":
    main()
