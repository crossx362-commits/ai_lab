#!/usr/bin/env python3
from __future__ import annotations
import shutil, subprocess, sys
from pathlib import Path
HERE = Path(__file__).resolve().parent
RES = HERE.parent / "unity" / "Assets" / "Resources" / "sprites"
SPLIT = HERE / "split_ai_sheet.py"
JOBS = "guardian berserker swordsman archer summoner priest druid bard shaman elemental".split()
WALK = "walk_00 walk_01 dash_00 dash_01 dash_02 dash_03".split()
ACT = "attack_00 attack_01 special_00 hurt_00 death_00 invuln_00".split()

def split(sheet, names, dest, prefix):
    if not sheet.exists():
        print("없음", sheet.name)
        return
    dest.mkdir(parents=True, exist_ok=True)
    subprocess.run([sys.executable, str(SPLIT), str(sheet), "--cols","3","--rows","2",
                    "--names", *names, "--out-dir", str(dest), "--prefix", prefix], check=False)

def main():
    src = HERE / "out_p21_adv_anim"
    n = 0
    for j in JOBS:
        dest = RES / j
        dest.mkdir(parents=True, exist_ok=True)
        split(src / f"sheet_{j}_walk.png", WALK, dest, j)
        split(src / f"sheet_{j}_act.png", ACT, dest, j)
        n += len(list(dest.glob(f"{j}_*.png")))
        print(j, len(list(dest.glob("*.png"))))
    print("장수 합", n)
    return 0
if __name__ == "__main__":
    raise SystemExit(main())
