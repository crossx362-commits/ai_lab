#!/usr/bin/env python3
from pathlib import Path
import shutil
HERE = Path(__file__).resolve().parent
SRC = HERE / "out_p23_fx"
RES = HERE.parent / "unity" / "Assets" / "Resources"
SINGLES = "fx_hit fx_slash fx_heal fx_fire fx_shield fx_taunt fx_summon fx_death".split()
SHEETS = (
    "tank_slash_sheet dps_slash_sheet mage_fire_sheet priest_heal_sheet "
    "tank_barrier_sheet bard_aura_sheet poison_status_sheet freeze_status_sheet "
    "boss_aoe_warning_sheet boss_charge_warning_sheet boss_cone_warning_sheet "
    "boss_interrupt_sheet boss_summon_portal_sheet"
).split()
ICONS = (
    "rogue_dash ranger_arrow druid_thorns bard_note bard_aura druid_regen "
    "revive cleanse boss_circle boss_cone boss_portal boss_charge critical dodge "
    "damage_reduce loot"
).split()

def cp(name, dest):
    src = SRC / f"{name}.png"
    if not src.exists():
        print("없음", name); return
    dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dest)
    print("→", dest)

def main():
    for n in SINGLES + SHEETS:
        for folder in (RES/"fx", RES/"FX"):
            cp(n, folder / f"{n}.png")
    for n in ICONS:
        cp(n, RES/"fx"/"icons"/f"{n}.png")
    return 0
if __name__ == "__main__":
    raise SystemExit(main())
