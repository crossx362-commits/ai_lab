#!/usr/bin/env python3
"""정예 5포즈 → knock_bg → 22프레임 채움 → 128px → Resources/sprites/mob_* ."""
from __future__ import annotations

import shutil
import sys
from pathlib import Path

from PIL import Image

import knock_bg

HERE = Path(__file__).resolve().parent
SRC = HERE / "out_coc_elites"
DEST_ROOT = HERE.parent / "unity" / "Assets" / "Resources" / "sprites"
HEIGHT = 128

KINDS = [
    "guardian", "berserker", "swordsman", "archer", "summoner",
    "priest", "druid", "bard", "shaman", "elemental",
]
POSES = ("idle", "walk", "attack", "hurt", "death")
FILL = {
    "idle": ["idle_00", "idle_01", "idle_02", "idle_03"],
    "walk": ["walk_00", "walk_01", "walk_02", "walk_03", "walk_04", "walk_05"],
    "attack": ["attack_00", "attack_01", "attack_02", "attack_03"],
    "hurt": ["hurt_00", "hurt_01", "hurt_02", "hurt_03"],
    "death": ["death_00", "death_01", "death_02", "death_03"],
}


def scale_h(im: Image.Image, h: int) -> Image.Image:
    w, ih = im.size
    if ih == h:
        return im
    nw = max(1, round(w * h / ih))
    return im.resize((nw, h), Image.Resampling.LANCZOS)


def import_kind(kind: str) -> None:
    dest = DEST_ROOT / f"mob_{kind}"
    dest.mkdir(parents=True, exist_ok=True)
    keyed = SRC / "_keyed"
    keyed.mkdir(exist_ok=True)
    posed = {}
    for pose in POSES:
        src = SRC / f"{kind}_{pose}.png"
        if not src.exists():
            raise SystemExit(f"missing {src}")
        out = keyed / f"{kind}_{pose}.png"
        knock_bg.apply_path(src, out, crop=True)
        posed[pose] = Image.open(out).convert("RGBA")

    for pose, names in FILL.items():
        im = scale_h(posed[pose], HEIGHT)
        for name in names:
            path = dest / f"mob_{kind}_{name}.png"
            im.save(path)
            print(f"  {path.name} {im.size}")


def main() -> int:
    for kind in KINDS:
        print(f"== {kind}")
        import_kind(kind)
    # align each folder (feet-bottom shared canvas)
    sys.path.insert(0, str(HERE))
    from align_frames import align
    for kind in KINDS:
        rc = align(str(DEST_ROOT / f"mob_{kind}"))
        if rc:
            print(f"align fail {kind} rc={rc}")
            return rc
    # re-scale after align if height drifted above 128
    for kind in KINDS:
        d = DEST_ROOT / f"mob_{kind}"
        pngs = sorted(d.glob("mob_*.png"))
        if not pngs:
            continue
        sample = Image.open(pngs[0])
        if sample.size[1] > HEIGHT:
            for p in pngs:
                im = Image.open(p).convert("RGBA")
                scale_h(im, HEIGHT).save(p)
    return 0


if __name__ == "__main__":
    sys.exit(main())
