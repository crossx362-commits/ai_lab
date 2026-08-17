#!/usr/bin/env python3
"""Imagine 5직업 장을 SpriteBank 13장으로 넣는다. 보스 폴더는 안 건드린다."""
from __future__ import annotations
import sys
from pathlib import Path
from PIL import Image
import numpy as np

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import knock_bg

SRC = HERE / "out_imagine5"
RES = HERE.parent / "unity" / "Assets" / "Resources" / "sprites"
PORT = HERE.parent / "unity" / "Assets" / "Resources" / "ui" / "portraits"
JOBS = "tank dps mage healer buffer".split()
FRAMES = (
    "idle_00", "walk_00", "walk_01", "attack_00", "attack_01",
    "special_00", "hurt_00", "death_00",
    "dash_00", "dash_01", "dash_02", "dash_03", "invuln_00",
)


def load(job: str, key: str) -> Image.Image | None:
    for name in (f"{job}_{key}.png", f"{job}_{key}_src.jpg", f"{job}_{key}.jpg"):
        p = SRC / name
        if p.exists():
            return Image.open(p)
    return None


def matte(im: Image.Image, h: int = 192) -> Image.Image:
    im = knock_bg.apply(im, crop=True)
    if im.height != h:
        w = max(1, round(im.width * h / im.height))
        im = im.resize((w, h), Image.Resampling.LANCZOS)
    return im


def main() -> int:
    for job in JOBS:
        dest = RES / job
        dest.mkdir(parents=True, exist_ok=True)
        idle = load(job, "idle")
        w0 = load(job, "walk_00") or idle
        w1 = load(job, "walk_01") or idle
        atk = load(job, "attack") or load(job, "attack_01") or idle
        dash = load(job, "dash") or w0
        raw = {
            "idle_00": idle, "walk_00": w0, "walk_01": w1,
            "attack_00": w0, "attack_01": atk,
            "special_00": atk, "hurt_00": idle, "death_00": idle,
            "dash_00": dash, "dash_01": w0, "dash_02": dash, "dash_03": w1,
            "invuln_00": idle,
        }
        n = 0
        for name in FRAMES:
            im = raw[name]
            if im is None:
                print("없음", job, name)
                continue
            out = matte(im)
            out.save(dest / f"{job}_{name}.png")
            n += 1
        if idle:
            PORT.mkdir(parents=True, exist_ok=True)
            matte(idle).save(PORT / f"{job}.png")
        print(job, n)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
