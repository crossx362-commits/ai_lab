"""픽셀아트 바닥 타일 — 블렌더 백그라운드.

기존 gen_ground_tiles.py는 부드러운 노이즈라 픽셀 집·나무와 갈린다.
이 스크립트는 64칸 도트를 랩어라운드 노이즈로 칠한 뒤 8배 최근접으로 512를 만든다.
GUI 세션을 지우지 않으려면 반드시 --background 로 돌린다.

    /Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup \\
        --python projects/ashes-to-stars/blender/gen_pixel_ground.py
"""
from __future__ import annotations

import os

import bpy

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out_ground")
CELL = 64
SCALE = 8

# (이름, 바탕, 어두운 얼룩, 밝은 얼룩, 점)
BIOMES = [
    ("field_plain",  (72, 90, 64), (52, 66, 48), (98, 114, 78), (64, 80, 56)),
    ("field_ash",    (58, 52, 54), (42, 38, 40), (78, 70, 68), (50, 46, 48)),
    ("dungeon_rock", (52, 50, 56), (36, 34, 40), (70, 68, 74), (44, 42, 48)),
    ("estate_soil",  (86, 68, 48), (64, 50, 36), (110, 88, 62), (74, 58, 42)),
]


def u32(n: int) -> int:
    return n & 0xFFFFFFFF


def hash2(x: int, y: int, seed: int) -> float:
    h = u32(x * 374761393 + y * 668265263 + seed * 1274126177)
    h = u32(h ^ (h >> 13))
    h = u32(h * 1274126177)
    return h / 4294967296.0


def vnoise(x: float, y: float, seed: int) -> float:
    x0, y0 = int(x) % CELL, int(y) % CELL
    x1, y1 = (x0 + 1) % CELL, (y0 + 1) % CELL
    tx, ty = x - int(x), y - int(y)
    tx = tx * tx * (3.0 - 2.0 * tx)
    ty = ty * ty * (3.0 - 2.0 * ty)
    a = hash2(x0, y0, seed)
    b = hash2(x1, y0, seed)
    c = hash2(x0, y1, seed)
    d = hash2(x1, y1, seed)
    return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty


def mix(a, b, t):
    return tuple(int(a[i] * (1 - t) + b[i] * t) for i in range(3))


def paint(base, dark, light, speck):
    pix = [0] * (CELL * CELL * 4)
    for y in range(CELL):
        for x in range(CELL):
            n = vnoise(x / 5.5, y / 5.5, 11)
            m = vnoise(x / 2.2 + 20, y / 2.2, 29)
            s = hash2(x, y, 71)
            if n < 0.38:
                col = mix(base, dark, (0.38 - n) / 0.38)
            elif n > 0.68:
                col = mix(base, light, (n - 0.68) / 0.32)
            else:
                col = base
            if m > 0.78:
                col = mix(col, dark, 0.35)
            if s > 0.92:
                col = speck
            i = (y * CELL + x) * 4
            pix[i] = col[0] / 255.0
            pix[i + 1] = col[1] / 255.0
            pix[i + 2] = col[2] / 255.0
            pix[i + 3] = 1.0
    return pix


def save(name, pix):
    src = bpy.data.images.new(name + "_src", CELL, CELL, alpha=False)
    src.pixels = pix
    big = bpy.data.images.new(name, CELL * SCALE, CELL * SCALE, alpha=False)
    src.scale(CELL * SCALE, CELL * SCALE)
    # scale() is bilinear — 도트를 살리려면 직접 복제한다
    src.scale(CELL, CELL)
    src.pixels = pix
    out = [0.0] * (CELL * SCALE * CELL * SCALE * 4)
    for y in range(CELL):
        for x in range(CELL):
            i = (y * CELL + x) * 4
            r, g, b = pix[i], pix[i + 1], pix[i + 2]
            for dy in range(SCALE):
                for dx in range(SCALE):
                    j = ((y * SCALE + dy) * CELL * SCALE + (x * SCALE + dx)) * 4
                    out[j] = r
                    out[j + 1] = g
                    out[j + 2] = b
                    out[j + 3] = 1.0
    big.pixels = out
    path = os.path.join(OUT, name + "_albedo.png")
    big.filepath_raw = path
    big.file_format = "PNG"
    big.save()
    print("wrote", path)


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, base, dark, light, speck in BIOMES:
        save(name, paint(base, dark, light, speck))


if __name__ == "__main__":
    main()
