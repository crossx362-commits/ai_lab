#!/usr/bin/env python3
"""out_p13_icons 40장을 combat_icon_atlas 8×6에 얹는다. 없는 칸은 기존 아틀라스를 남긴다."""
from __future__ import annotations
import sys
from pathlib import Path
from PIL import Image

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import knock_bg

SRC = HERE / "out_p13_icons"
DST = HERE.parent / "unity" / "Assets" / "Resources" / "ui" / "combat_icon_atlas.png"
W, H, COLS, ROWS = 1448, 1086, 8, 6
CW, CH = W // COLS, H // ROWS

# CombatIconAtlas.Pieces 와 같은 순서
KEYS = [
    "tank_charge", "tank_taunt", "tank_slam", "tank_barrier",
    "tank_last_stand", "tank_guard", "aggro", "damage_reduce",
    "damage_slash", "damage_critical", "damage_spin", "damage_dodge",
    "damage_arrow_volley", "damage_blink", "damage_weakpoint", "damage_execute",
    "heal_staff", "heal_burst", "cleanse", "revive",
    "heal_halo", "heal_party", "regeneration", "heal_beacon",
    "buffer_aura", "buffer_song", "buffer_scroll", "buffer_haste",
    "buffer_defense", "buffer_speed", "buffer_cooldown", "buffer_link",
    "warn_circle", "warn_cone", "warn_charge", "warn_beam",
    "warn_summon", "warn_heal_check", "warn_enrage", "warn_phase",
    "selected", "stunned", "silenced", "rooted",
    "knockback", "interrupted", "shielded", "low_health",
]


def main() -> int:
    atlas = Image.open(DST).convert("RGBA") if DST.exists() else Image.new("RGBA", (W, H), (0, 0, 0, 0))
    if atlas.size != (W, H):
        atlas = atlas.resize((W, H), Image.Resampling.NEAREST)
    n = 0
    for i, key in enumerate(KEYS):
        src = SRC / f"{key}.png"
        if not src.exists():
            continue
        icon = knock_bg.apply(Image.open(src), crop=True)
        icon.thumbnail((CW - 8, CH - 8), Image.Resampling.LANCZOS)
        cell = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
        cell.alpha_composite(icon, ((CW - icon.width) // 2, (CH - icon.height) // 2))
        c, r = i % COLS, i // COLS
        atlas.paste(cell, (c * CW, r * CH))
        n += 1
        print("→", key)
    DST.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(DST)
    print(f"아이콘 {n}칸 → {DST.name} {atlas.size}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
