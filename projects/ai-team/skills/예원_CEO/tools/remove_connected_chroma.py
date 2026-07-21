#!/usr/bin/env python3
"""Remove only border-connected green chroma while preserving green clothing."""
from __future__ import annotations

import argparse
from collections import deque
from pathlib import Path

from PIL import Image


def is_background(pixel: tuple[int, int, int, int]) -> bool:
    r, g, b, _a = pixel
    return g > 170 and g > r * 1.45 and g > b * 1.35


def remove_connected_chroma(source: Path, destination: Path) -> None:
    image = Image.open(source).convert("RGBA")
    pixels = image.load()
    width, height = image.size
    queue: deque[tuple[int, int]] = deque()
    visited: set[tuple[int, int]] = set()

    for x in range(width):
        queue.extend(((x, 0), (x, height - 1)))
    for y in range(height):
        queue.extend(((0, y), (width - 1, y)))

    while queue:
        x, y = queue.popleft()
        if (x, y) in visited or not is_background(pixels[x, y]):
            continue
        visited.add((x, y))
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < width and 0 <= ny < height and (nx, ny) not in visited:
                queue.append((nx, ny))

    alpha = Image.new("L", image.size, 255)
    alpha_pixels = alpha.load()
    for x, y in visited:
        alpha_pixels[x, y] = 0
    image.putalpha(alpha)
    destination.parent.mkdir(parents=True, exist_ok=True)
    image.save(destination)
    print(f"saved {destination} ({len(visited)} background pixels removed)")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("input", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    remove_connected_chroma(args.input, args.output)


if __name__ == "__main__":
    main()
