#!/usr/bin/env python3
"""할로우 생성물을 Resources 이름으로 넣는다. 단계: portraits|mobs|boss|props|fx|chrome|ground|all"""
from __future__ import annotations
import shutil
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
RES = HERE.parent / "unity" / "Assets" / "Resources"
SPLIT = HERE / "split_ai_sheet.py"

MOB_A = "idle_00 idle_01 idle_02 idle_03 walk_00 walk_01".split()
MOB_B = "walk_02 attack_00 attack_01 hurt_00 death_00 death_01".split()
MOB_HOLD = {
    "walk_03": "walk_01", "walk_04": "walk_00", "walk_05": "walk_02",
    "attack_02": "attack_00", "attack_03": "attack_01",
    "hurt_01": "hurt_00", "hurt_02": "hurt_00", "hurt_03": "hurt_00",
    "death_02": "death_01", "death_03": "death_01",
}
BOSS_A = "idle_00 idle_01 idle_02 idle_03 attack_00 attack_01".split()
BOSS_B = "attack_02 hurt_00 hurt_01 death_00 death_01 death_02".split()
BOSS_HOLD = {"attack_03": "attack_01", "hurt_02": "hurt_00", "hurt_03": "hurt_01", "death_03": "death_02"}
MOBS = "mob01 mob_chaser mob_charger mob_ranged mob_swarmer".split()
BOSSES = "boss_brute boss_serpent boss_wraith boss_construct".split()


def cp(src: Path, dst: Path) -> bool:
    if not src.exists():
        print("없음", src.name)
        return False
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dst)
    print("→", dst)
    return True


def split_sheet(sheet: Path, names: list[str], out_dir: Path, prefix: str) -> None:
    if not sheet.exists():
        print("시트 없음", sheet.name)
        return
    out_dir.mkdir(parents=True, exist_ok=True)
    subprocess.run(
        [sys.executable, str(SPLIT), str(sheet), "--cols", "3", "--rows", "2",
         "--names", *names, "--out-dir", str(out_dir), "--prefix", prefix],
        check=False,
    )


def holds(folder: Path, prefix: str, mapping: dict[str, str]) -> None:
    for dst_name, src_name in mapping.items():
        src = folder / f"{prefix}_{src_name}.png"
        dst = folder / f"{prefix}_{dst_name}.png"
        if src.exists() and not dst.exists():
            shutil.copy2(src, dst)


def portraits() -> None:
    src = HERE / "out_p12_ui"
    for n in ("tank", "dps", "mage", "healer", "buffer"):
        cp(src / f"portrait_{n}.png", RES / "ui" / "portraits" / f"{n}.png")


def mobs() -> None:
    root = HERE / "out_p14_mobs"
    for m in MOBS:
        frames = root / f"frames_{m}"
        split_sheet(root / f"sheet_{m}_A.png", MOB_A, frames, m)
        split_sheet(root / f"sheet_{m}_B.png", MOB_B, frames, m)
        holds(frames, m, MOB_HOLD)
        dest = RES / "sprites" / m
        dest.mkdir(parents=True, exist_ok=True)
        for p in frames.glob(f"{m}_*.png"):
            shutil.copy2(p, dest / p.name)
        print("몹", m, len(list(dest.glob("*.png"))))


def boss() -> None:
    root = HERE / "out_p15_boss"
    for b in BOSSES:
        frames = root / f"frames_{b}"
        split_sheet(root / f"sheet_{b}_A.png", BOSS_A, frames, b)
        split_sheet(root / f"sheet_{b}_B.png", BOSS_B, frames, b)
        holds(frames, b, BOSS_HOLD)
        dest = RES / "sprites" / b
        dest.mkdir(parents=True, exist_ok=True)
        for p in frames.glob(f"{b}_*.png"):
            shutil.copy2(p, dest / p.name)
        print("보스", b, len(list(dest.glob("*.png"))))


def props() -> None:
    src = HERE / "out_p16_props"
    dest = RES / "props"
    n = 0
    for p in src.glob("*.png"):
        if cp(p, dest / p.name):
            n += 1
    print("프랍", n)


def fx() -> None:
    src = HERE / "out_p17_fx"
    for folder in (RES / "fx", RES / "FX"):
        folder.mkdir(parents=True, exist_ok=True)
        for p in src.glob("fx_*.png"):
            shutil.copy2(p, folder / p.name)
    print("fx")


def chrome() -> None:
    src = HERE / "out_p18_chrome"
    dest = RES / "ui" / "chrome"
    for p in src.glob("*.png"):
        cp(p, dest / p.name)
    print("chrome")


def ground() -> None:
    cp(HERE / "out_p19_ground" / "field_plain_albedo.png",
       RES / "ground" / "field_plain_albedo.png")


def main(argv: list[str]) -> int:
    step = argv[1] if len(argv) > 1 else "all"
    fns = {
        "portraits": portraits, "mobs": mobs, "boss": boss, "props": props,
        "fx": fx, "chrome": chrome, "ground": ground,
    }
    if step == "all":
        for fn in fns.values():
            fn()
        return 0
    if step not in fns:
        print("unknown", step)
        return 2
    fns[step]()
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
