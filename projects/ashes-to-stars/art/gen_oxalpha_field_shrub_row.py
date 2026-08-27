#!/usr/bin/env python3
"""ox-alpha 필드 관목줄(field_shrub_row_0) 코드합성 — 256×256 가로 덤불 줄.

배경: field_stump_* 은 ox-alpha 256인데 field_shrub_row_0 는 아직 나노바나나
1786×1189·2.2MB 그레이 마른 관목 네 포기라 톤이 따로 놀았다. 같은 웜톤
올리브 팔레트·같은 256, 가로로 늘어선 생덤불 줄로 교체한다.
field_bush_* (한 덩이)·field_stump_* 미변경.

**크기 무영향(실측, FieldDecor.cs:336)**: ppu = pxHeight / TargetUnits
(prop_scale.json, field_shrub_row_0=1.10)이라 해상도 스왑이 스케일-세이프.
.cs/.meta/GUID 무변경.

디자인: 작은 올리브 덤불 4포기 가로 줄(한 덩이 bush와 구별, 나무 아님).
§6-A: 바닥 큰 원/고리/글로우 금지, 그림자는 밑 얇은 띠뿐.

사용: python3 gen_oxalpha_field_shrub_row.py
출력: art/out_oxalpha_field_shrub_row.png (256×256 RGBA)
"""
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

OUTLINE = (40, 24, 20, 255)
LEAF = (110, 132, 72, 255)
LEAF_DK = (70, 92, 48, 255)
LEAF_LT = (148, 168, 90, 255)
WOOD_DK = (96, 64, 34, 255)


def _blob(d, cx, cy, w, h, fill, outline=True):
    if outline:
        d.ellipse([cx - w - 3, cy - h - 3, cx + w + 3, cy + h + 3], fill=OUTLINE)
    d.ellipse([cx - w, cy - h, cx + w, cy + h], fill=fill)


def _shrub(d, cx, cy, s, fill, hi=True):
    # 짧은 밑동
    d.rectangle([cx - 3, cy + s - 4, cx + 3, cy + s + 10], fill=OUTLINE)
    d.rectangle([cx - 2, cy + s - 2, cx + 2, cy + s + 8], fill=WOOD_DK)
    _blob(d, cx - int(s * 0.45), cy + 4, int(s * 0.7), int(s * 0.55), LEAF_DK)
    _blob(d, cx + int(s * 0.4), cy + 6, int(s * 0.65), int(s * 0.5), LEAF_DK)
    _blob(d, cx, cy, s, int(s * 0.75), fill)
    if hi:
        _blob(d, cx - 4, cy - 6, int(s * 0.4), int(s * 0.3), LEAF_LT, outline=False)


def make():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)

    d.rectangle([28, 198, 228, 206], fill=(30, 24, 20, 90))

    # 4포기 가로 줄 (높이 살짝 다르게)
    _shrub(d, 52, 168, 22, LEAF_DK, hi=False)
    _shrub(d, 100, 156, 28, LEAF)
    _shrub(d, 154, 162, 26, LEAF)
    _shrub(d, 206, 170, 22, LEAF_DK, hi=False)

    out = HERE / "out_oxalpha_field_shrub_row.png"
    im.save(out)
    px = im.load()
    clear = solid = 0
    for y in range(256):
        for x in range(256):
            a = px[x, y][3]
            if a < 13:
                clear += 1
            elif a > 242:
                solid += 1
    print(f"→ {out.name}  256×256  투명 {clear} · 불투명 {solid} / 65536")


if __name__ == "__main__":
    make()
