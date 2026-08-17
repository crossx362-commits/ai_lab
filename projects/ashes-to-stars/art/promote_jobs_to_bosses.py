#!/usr/bin/env python3
"""지금 기본 5종 13장을 보스 실루엣 16장으로 옮긴다. 원본은 지우지 않는다."""
from __future__ import annotations
import shutil
from pathlib import Path

HERE = Path(__file__).resolve().parent
RES = HERE.parent / "unity" / "Assets" / "Resources" / "sprites"
# 기획서 실루엣: 넓고 각진=brute, 많은 다리=serpent, 영혼=wraith, 날카로움=construct, 후드=saint
MAP = {
    "tank": "boss_brute",
    "buffer": "boss_serpent",
    "mage": "boss_wraith",
    "dps": "boss_construct",
    "healer": "boss_saint",
}
JOB = (
    "idle_00", "walk_00", "walk_01", "attack_00", "attack_01",
    "special_00", "hurt_00", "death_00",
    "dash_00", "dash_01", "dash_02", "dash_03", "invuln_00",
)
BOSS = (
    "idle_00", "idle_01", "idle_02", "idle_03",
    "attack_00", "attack_01", "attack_02", "attack_03",
    "hurt_00", "hurt_01", "hurt_02", "hurt_03",
    "death_00", "death_01", "death_02", "death_03",
)
# 13 → 16: 남는 3칸은 걷기·무적으로 채운다
TO = (
    "idle_00", "walk_00", "walk_01", "idle_00",
    "attack_00", "attack_01", "special_00", "dash_00",
    "hurt_00", "dash_01", "dash_02", "dash_03",
    "death_00", "invuln_00", "death_00", "hurt_00",
)


def main() -> int:
    for job, boss in MAP.items():
        dest = RES / boss
        dest.mkdir(parents=True, exist_ok=True)
        n = 0
        for bname, jname in zip(BOSS, TO):
            src = RES / job / f"{job}_{jname}.png"
            if not src.exists():
                print("없음", src.name)
                continue
            shutil.copy2(src, dest / f"{boss}_{bname}.png")
            n += 1
        # 정지 실루엣(아틀라스)
        idle = RES / job / f"{job}_idle_00.png"
        if idle.exists():
            shutil.copy2(idle, RES / f"{boss}.png")
        print(boss, n)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
