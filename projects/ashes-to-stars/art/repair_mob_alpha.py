#!/usr/bin/env python3
"""p22 몹 5계열 시트를 다시 나눠 Resources에 넣는다. 생성하지 않는다.

옛 반입은 몸이 뚫린 프레임을 남겼다. 시트는 채워져 있다.
직업 13장·전직은 덮지 않는다.
"""
from __future__ import annotations

import shutil
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
SPLIT = HERE / "split_ai_sheet.py"
SRC = HERE / "out_p22_bw"
RES = HERE.parent / "unity" / "Assets" / "Resources" / "sprites"
MOBS = "mob01 mob_chaser mob_charger mob_ranged mob_swarmer".split()
MOBA = "idle_00 idle_01 idle_02 idle_03 walk_00 walk_01".split()
MOBB = "walk_02 attack_00 attack_01 hurt_00 death_00 death_01".split()
HOLD = {
    "walk_03": "walk_01",
    "walk_04": "walk_00",
    "walk_05": "walk_02",
    "attack_02": "attack_00",
    "attack_03": "attack_01",
    "hurt_01": "hurt_00",
    "hurt_02": "hurt_00",
    "hurt_03": "hurt_00",
    "death_02": "death_01",
    "death_03": "death_01",
}


def split(sheet: Path, names: list[str], dest: Path, prefix: str) -> None:
    dest.mkdir(parents=True, exist_ok=True)
    subprocess.run(
        [
            sys.executable, str(SPLIT), str(sheet),
            "--cols", "3", "--rows", "2",
            "--names", *names,
            "--out-dir", str(dest),
            "--prefix", prefix,
        ],
        check=True,
    )


def main() -> int:
    for name in MOBS:
        frames = SRC / f"frames_{name}"
        if frames.exists():
            shutil.rmtree(frames)
        split(SRC / f"sheet_{name}_A.png", MOBA, frames, name)
        split(SRC / f"sheet_{name}_B.png", MOBB, frames, name)
        for dst, srcn in HOLD.items():
            s, t = frames / f"{name}_{srcn}.png", frames / f"{name}_{dst}.png"
            if s.exists():
                shutil.copy2(s, t)
        dest = RES / name
        dest.mkdir(parents=True, exist_ok=True)
        n = 0
        for p in sorted(frames.glob(f"{name}_*.png")):
            shutil.copy2(p, dest / p.name)
            n += 1
        print(name, n, "→", dest)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
