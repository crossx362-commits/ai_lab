#!/usr/bin/env python3
"""Build FleetView sprite sheets from the existing chibi character images."""
from __future__ import annotations

from pathlib import Path
from PIL import Image, ImageDraw, ImageEnhance


HERE = Path(__file__).resolve().parent
SRC = HERE / "sprites" / "chibi"
OUT = HERE / "sprites" / "chibi_sheets"
WALK_SRC = HERE / "sprites" / "generated_walk"
SEATED_SRC = HERE / "sprites" / "generated_seated"
KEYS = ["yewon", "youngsuk", "bomi", "teo", "baekho", "suri", "mio", "namu"]
FRAME_W = 512
FRAME_H = 560
SEATED_CELLS = {
    "yewon": ("batch_a_full.png", 0), "youngsuk": ("batch_a_full.png", 1),
    "bomi": ("batch_a_full.png", 2), "suri": ("batch_a_full.png", 3),
    "teo": ("batch_b_full.png", 0), "baekho": ("batch_b_full.png", 1),
    "mio": ("batch_b_full.png", 2), "namu": ("batch_b_full.png", 3),
}


def alpha_bbox(im: Image.Image) -> tuple[int, int, int, int]:
    return im.getchannel("A").getbbox() or (0, 0, im.width, im.height)


def keep_largest_alpha_component(im: Image.Image) -> Image.Image:
    alpha = im.getchannel("A")
    px = alpha.load()
    visited: set[tuple[int, int]] = set()
    best: tuple[int, list[tuple[int, int]]] | None = None
    for y in range(im.height):
        for x in range(im.width):
            if px[x, y] < 16 or (x, y) in visited:
                continue
            stack, component = [(x, y)], []
            visited.add((x, y))
            while stack:
                point = stack.pop()
                component.append(point)
                cx, cy = point
                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                    if 0 <= nx < im.width and 0 <= ny < im.height and (nx, ny) not in visited and px[nx, ny] >= 16:
                        visited.add((nx, ny))
                        stack.append((nx, ny))
            if best is None or len(component) > best[0]:
                best = (len(component), component)
    if best is None:
        return im
    mask = Image.new("L", im.size, 0)
    mask_px = mask.load()
    for x, y in best[1]:
        mask_px[x, y] = px[x, y]
    kept = im.copy()
    kept.putalpha(mask)
    return kept


def blank() -> Image.Image:
    return Image.new("RGBA", (FRAME_W, FRAME_H), (0, 0, 0, 0))


def paste_centered(canvas: Image.Image, part: Image.Image, x_shift: int = 0, y: int | None = None) -> None:
    x = (FRAME_W - part.width) // 2 + x_shift
    if y is None:
        y = FRAME_H - part.height - 8
    canvas.alpha_composite(part, (x, y))


def erase_box(im: Image.Image, box: tuple[int, int, int, int]) -> None:
    alpha = im.getchannel("A")
    d = ImageDraw.Draw(alpha)
    d.rectangle(box, fill=0)
    im.putalpha(alpha)


def paste_transformed(
    canvas: Image.Image,
    base: Image.Image,
    box: tuple[int, int, int, int],
    dx: int,
    dy: int,
    angle: float,
) -> None:
    part = base.crop(box)
    part = part.rotate(angle, resample=Image.Resampling.BICUBIC, expand=True)
    cx = (box[0] + box[2]) // 2 + dx
    cy = (box[1] + box[3]) // 2 + dy
    x = int(cx - part.width / 2)
    y = int(cy - part.height / 2)
    canvas.alpha_composite(part, (x, y))


def load_generated_walk_frames(key: str) -> list[Image.Image] | None:
    path = WALK_SRC / f"{key}.png"
    if not path.exists():
        return None
    sheet = Image.open(path).convert("RGBA")
    cell_w = sheet.width // 4
    frames = []
    for i in range(4):
        frame = sheet.crop((i * cell_w, 0, (i + 1) * cell_w, sheet.height))
        if frame.size != (FRAME_W, FRAME_H):
            normalized = blank()
            frame.thumbnail((FRAME_W, FRAME_H), Image.Resampling.LANCZOS)
            paste_centered(normalized, frame)
            frame = normalized
        frames.append(frame)
    return frames


def build_walk_frame(base: Image.Image, pose: str) -> Image.Image:
    """Keep the original body art, moving only slices from that same image."""
    box = alpha_bbox(base)
    l, t, r, b = box
    w = r - l
    h = b - t
    cx = (l + r) // 2
    foot_y = min(FRAME_H - 20, b - 8)
    cycle = {
        "walk_a": (-18, 10, -5),
        "pass_a": (-8, 4, 0),
        "walk_b": (18, 10, -5),
        "pass_b": (8, 4, 0),
    }
    swing, lift, bob = cycle[pose]
    arm_top = t + int(h * 0.34)
    arm_bot = t + int(h * 0.72)
    leg_top = t + int(h * 0.58)
    left_arm = (max(0, l), arm_top, min(FRAME_W, l + int(w * 0.36)), arm_bot)
    right_arm = (max(0, r - int(w * 0.36)), arm_top, min(FRAME_W, r), arm_bot)
    left_leg = (max(0, l + int(w * 0.16)), leg_top, min(FRAME_W, l + int(w * 0.54)), b)
    right_leg = (max(0, l + int(w * 0.46)), leg_top, min(FRAME_W, l + int(w * 0.84)), b)

    frame = blank()
    shadow = Image.new("RGBA", (FRAME_W, FRAME_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(shadow, "RGBA")
    d.ellipse((cx - 94, foot_y - 12, cx + 94, foot_y + 8), fill=(22, 17, 31, 72))
    frame.alpha_composite(shadow)

    frame.alpha_composite(base, (0, bob))
    paste_transformed(frame, base, left_leg, swing, -lift + bob, swing * 0.20)
    paste_transformed(frame, base, right_leg, -swing, lift // 2 + bob, -swing * 0.20)
    paste_transformed(frame, base, left_arm, -swing // 2, lift // 2 + bob, -swing * 0.18)
    paste_transformed(frame, base, right_arm, swing // 2, -lift // 2 + bob, swing * 0.18)
    return frame


def load_generated_seated_frames(key: str) -> list[Image.Image]:
    filename, row = SEATED_CELLS[key]
    sheet = Image.open(SEATED_SRC / filename).convert("RGBA")
    cell_w = sheet.width // 2
    top, bottom = round(row * sheet.height / 4), round((row + 1) * sheet.height / 4)
    frames = []
    for col in range(2):
        cell = sheet.crop((col * cell_w, top, (col + 1) * cell_w, bottom))
        cell = keep_largest_alpha_component(cell)
        subject = cell.crop(alpha_bbox(cell))
        subject.thumbnail((430, 430), Image.Resampling.LANCZOS)
        frame = blank()
        paste_centered(frame, subject, y=FRAME_H - subject.height - 10)
        frames.append(frame)
    return frames


def build_sit_frame(base: Image.Image, key: str, pose: str) -> Image.Image:
    return load_generated_seated_frames(key)[0 if pose == "sit" else 1]


def build_sheet(key: str) -> Image.Image:
    base = Image.open(SRC / f"{key}.png").convert("RGBA")
    walk_frames = load_generated_walk_frames(key) or [
        build_walk_frame(base, pose="walk_a"),
        build_walk_frame(base, pose="pass_a"),
        build_walk_frame(base, pose="walk_b"),
        build_walk_frame(base, pose="pass_b"),
    ]
    frames = [
        base.copy(),
        ImageEnhance.Brightness(base).enhance(0.97),
        *walk_frames,
        build_sit_frame(base, key, pose="sit"),
        build_sit_frame(base, key, pose="type"),
    ]
    sheet = blank().resize((FRAME_W * len(frames), FRAME_H))
    for i, frame in enumerate(frames):
        sheet.alpha_composite(frame, (i * FRAME_W, 0))
    return sheet


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    sheets = []
    for key in KEYS:
        sheet = build_sheet(key)
        sheet.save(OUT / f"{key}.png")
        sheets.append((key, sheet))
        print(f"saved {OUT / f'{key}.png'} ({sheet.width}x{sheet.height})")

    thumb_w, thumb_h = 256, 280
    contact = Image.new("RGBA", (4 * thumb_w, 2 * thumb_h), (28, 22, 38, 255))
    for i, (_key, sheet) in enumerate(sheets):
        frame = sheet.crop((0, 0, FRAME_W, FRAME_H))
        frame.thumbnail((thumb_w - 36, thumb_h - 36), Image.Resampling.LANCZOS)
        x = (i % 4) * thumb_w + (thumb_w - frame.width) // 2
        y = (i // 4) * thumb_h + (thumb_h - frame.height) // 2
        contact.alpha_composite(frame, (x, y))
    contact.save(OUT / "contact.png")
    print(f"saved {OUT / 'contact.png'}")


if __name__ == "__main__":
    main()
