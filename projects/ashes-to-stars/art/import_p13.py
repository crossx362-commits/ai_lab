#!/usr/bin/env python3
"""out_p13_adv 전직 idle·배경 3장을 Resources 이름으로 넣는다."""
from __future__ import annotations
import shutil
from pathlib import Path

HERE = Path(__file__).resolve().parent
SRC = HERE / "out_p13_adv"
RES = HERE.parent / "unity" / "Assets" / "Resources"
JOBS = (
    "guardian", "berserker", "swordsman", "archer", "summoner",
    "priest", "druid", "bard", "shaman", "elemental",
)
BGS = ("bg_title", "bg_result", "bg_dungeon")


def main() -> int:
    n = 0
    for job in JOBS:
        src = SRC / f"{job}_idle_00.png"
        if not src.exists():
            print("없음", src.name)
            continue
        dst_dir = RES / "sprites" / job
        dst_dir.mkdir(parents=True, exist_ok=True)
        dst = dst_dir / f"{job}_idle_00.png"
        shutil.copy2(src, dst)
        print("→", dst.relative_to(RES.parent.parent))
        n += 1
    for bg in BGS:
        src = SRC / f"{bg}.png"
        if not src.exists():
            print("없음", src.name)
            continue
        dst = RES / "bg" / f"{bg}.png"
        shutil.copy2(src, dst)
        print("→", dst.relative_to(RES.parent.parent))
        n += 1
    print(f"반입 {n}장")
    return 0 if n else 1


if __name__ == "__main__":
    raise SystemExit(main())
