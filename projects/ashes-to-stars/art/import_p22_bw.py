#!/usr/bin/env python3
from pathlib import Path
import shutil, subprocess, sys
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import knock_bg
RES = HERE.parent / "unity" / "Assets" / "Resources"
SRC = HERE / "out_p22_bw"
SPLIT = HERE / "split_ai_sheet.py"
WALK = "walk_00 walk_01 dash_00 dash_01 dash_02 dash_03".split()
ACT = "attack_00 attack_01 special_00 hurt_00 death_00 invuln_00".split()
MOBA = "idle_00 idle_01 idle_02 idle_03 walk_00 walk_01".split()
MOBB = "walk_02 attack_00 attack_01 hurt_00 death_00 death_01".split()
HOLD_M = {"walk_03":"walk_01","walk_04":"walk_00","walk_05":"walk_02",
          "attack_02":"attack_00","attack_03":"attack_01",
          "hurt_01":"hurt_00","hurt_02":"hurt_00","hurt_03":"hurt_00",
          "death_02":"death_01","death_03":"death_01"}
# 기본 5직업은 오너 전시 시트(apply_showcase_sheets)가 붙인 13장이다.
# p22가 끝나면 전직·몹만 덮는다 — 탱·힐을 다시 덮으면 방금 적용이 사라진다.
CHARS = "guardian berserker swordsman archer summoner priest druid bard shaman elemental".split()
MOBS = "mob01 mob_chaser mob_charger mob_ranged mob_swarmer".split()

def split(sheet, names, dest, prefix):
    if not sheet.exists():
        print("없음", sheet.name); return
    dest.mkdir(parents=True, exist_ok=True)
    subprocess.run([sys.executable, str(SPLIT), str(sheet), "--cols","3","--rows","2",
                    "--names", *names, "--out-dir", str(dest), "--prefix", prefix], check=False)

def main():
    for j in CHARS:
        dest = RES / "sprites" / j
        dest.mkdir(parents=True, exist_ok=True)
        idle = SRC / f"{j}_idle_00.png"
        if idle.exists():
            knock_bg.apply_path(idle, dest / f"{j}_idle_00.png", crop=True)
        split(SRC / f"sheet_{j}_walk.png", WALK, dest, j)
        split(SRC / f"sheet_{j}_act.png", ACT, dest, j)
        print(j, len(list(dest.glob("*.png"))))
    for n in ("tank","healer"):
        p = SRC / f"portrait_{n}.png"
        if p.exists():
            d = RES / "ui" / "portraits"
            d.mkdir(parents=True, exist_ok=True)
            knock_bg.apply_path(p, d / f"{n}.png", crop=True)
            print("portrait", n)
    for m in MOBS:
        dest = RES / "sprites" / m
        dest.mkdir(parents=True, exist_ok=True)
        frames = SRC / f"frames_{m}"
        split(SRC / f"sheet_{m}_A.png", MOBA, frames, m)
        split(SRC / f"sheet_{m}_B.png", MOBB, frames, m)
        for dst, srcn in HOLD_M.items():
            s, t = frames / f"{m}_{srcn}.png", frames / f"{m}_{dst}.png"
            if s.exists() and not t.exists():
                shutil.copy2(s, t)
        if frames.exists():
            for p in frames.glob(f"{m}_*.png"):
                shutil.copy2(p, dest / p.name)
        print("mob", m, len(list(dest.glob("*.png"))))
    return 0
if __name__ == "__main__":
    raise SystemExit(main())
