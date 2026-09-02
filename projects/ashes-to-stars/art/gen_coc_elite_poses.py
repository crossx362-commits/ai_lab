#!/usr/bin/env python3
"""정예 10종 walk/attack/hurt/death — idle 참조, nano_banana_flash 2k."""
from __future__ import annotations

import os
import shutil
import subprocess
import sys
import urllib.request
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path

HERE = Path(__file__).resolve().parent
OUT = HERE / "out_coc_elites"
CLI = shutil.which("higgsfield") or "/opt/homebrew/bin/higgsfield"
MODEL = "nano_banana_flash"

KINDS = {
    "guardian": "cracked navy-gold knight, red visor slit, sun-and-moon round shield, heavy mace, gold filigree plate",
    "berserker": "silver-haired horned barbarian, red glowing eyes, dual rune battle-axes, fur-and-gold armor, navy cape",
    "swordsman": "navy-gold samurai, horned kabuto, two-handed katana, grey-gold cloak",
    "archer": "hooded navy-gold ranger, gold-trim hood hiding one eye, wooden bow, quiver of arrows on back",
    "summoner": "one-eyed silver cultist mask, purple hood, gold-trim robes, forked staff with purple vortex orb",
    "priest": "ash-faced holy priest, white-gold tattered hood, floating gold halo ring, spiked holy staff",
    "druid": "antlered bark-mask druid, glowing green eyes, wooden crystal staff, navy-gold leaf armor",
    "bard": "grey-bearded jester bard, patched navy-gold fool cap with bells, lute with a skull headstock",
    "shaman": "skull-crowned teal shaman, crossed bones headdress, bone staff with teal orb, gold-navy rags",
    "elemental": "crowned fire spirit, molten rock face, gold circlet, navy-gold chestplate, flame arms and hair",
}

POSES = {
    "walk": (
        "WALK keyframe. Mid-stride walking LEFT (toward screen-left). "
        "Clear opposite-arm opposite-leg pose: one foot planted, the other stepping forward. "
        "3/4 view facing left. Same character, standing, not attacking."
    ),
    "attack": {
        "guardian": "ATTACK keyframe facing LEFT. Shield forward, mace mid-swing toward screen-left.",
        "berserker": "ATTACK keyframe facing LEFT. Both rune axes raised mid-chop toward screen-left.",
        "swordsman": "ATTACK keyframe facing LEFT. Two-handed katana slash across toward screen-left.",
        "archer": "ATTACK keyframe facing LEFT. Bow fully drawn, arrow nocked, aiming screen-left.",
        "summoner": "ATTACK keyframe facing LEFT. Staff thrust, purple vortex orb flaring toward screen-left.",
        "priest": "ATTACK keyframe facing LEFT. Holy staff raised, gold halo flaring, blessing blast left.",
        "druid": "ATTACK keyframe facing LEFT. Crystal staff thrust, green glow blasting screen-left.",
        "bard": "ATTACK keyframe facing LEFT. Lute swung/strummed aggressively toward screen-left.",
        "shaman": "ATTACK keyframe facing LEFT. Skull staff curse, teal drip blasting screen-left.",
        "elemental": "ATTACK keyframe facing LEFT. Both flame arms blasting fire toward screen-left.",
    },
    "hurt": (
        "HURT keyframe. Recoiling from a hit coming from screen-left. "
        "Body leaning back, wounded grimace, same standing character, not fallen."
    ),
    "death": (
        "DEATH keyframe. Collapsing / fallen, defeated, still the same character. "
        "Readable silhouette, not a pile of dust. No extra corpses."
    ),
}

CONTRACT = (
    "Keep the EXACT same chibi character as the reference image: same face, same outfit, "
    "same colors, same proportions, same silhouette details. Clash of Clans / Clash Royale "
    "quality hand-painted chibi, thick clean outlines, navy-gold-ash palette. "
    "Single full-body character, centered. Flat solid pure magenta #FF00FF filling the ENTIRE "
    "background. No text, no labels, no ground, no cast shadow, no extra props, no panel border. "
    "NOT Hollow Knight, NOT photoreal, NOT pixel art, NOT 3D render."
)


def pose_text(kind: str, pose: str) -> str:
    p = POSES[pose]
    if isinstance(p, dict):
        return p[kind]
    return p


def generate_one(kind: str, pose: str) -> str:
    dest = OUT / f"{kind}_{pose}.png"
    if dest.exists() and dest.stat().st_size > 50_000:
        return f"skip {kind}_{pose}"
    idle = OUT / f"{kind}_idle.png"
    if not idle.exists():
        return f"FAIL missing idle {kind}"
    prompt = (
        f"{CONTRACT}\n\n"
        f"Subject: {KINDS[kind]}.\n"
        f"{pose_text(kind, pose)}\n"
        "Match the attached reference identity exactly; only change the pose."
    )
    cmd = [
        CLI, "generate", "create", MODEL,
        "--resolution", "2k",
        "--aspect-ratio", "1:1",
        "--prompt", prompt,
        "--image-references", str(idle),
        "--wait", "--wait-timeout", "10m",
    ]
    last = ""
    for attempt in range(1, 4):
        p = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", timeout=900)
        out = (p.stdout or "") + (p.stderr or "")
        last = out
        urls = [ln.strip() for ln in (p.stdout or "").splitlines() if ln.strip().startswith("http")]
        if not urls:
            print(f"  retry {attempt}/3 {kind}_{pose}: {out.strip()[:160]}", flush=True)
            continue
        url = urls[-1]
        (OUT / f"{kind}_{pose}.url").write_text(url + "\n")
        (OUT / f"{kind}_{pose}.log").write_text(url + "\n")
        with urllib.request.urlopen(url, timeout=180) as r:
            dest.write_bytes(r.read())
        return f"ok {kind}_{pose} {dest.stat().st_size}"
    return f"FAIL {kind}_{pose}: {last.strip()[:200]}"


def main() -> int:
    OUT.mkdir(parents=True, exist_ok=True)
    jobs = [(k, p) for k in KINDS for p in ("walk", "attack", "hurt", "death")]
    workers = int(os.environ.get("HF_WORKERS", "4"))
    print(f"jobs={len(jobs)} workers={workers}", flush=True)
    fails = 0
    with ThreadPoolExecutor(max_workers=workers) as ex:
        futs = {ex.submit(generate_one, k, p): (k, p) for k, p in jobs}
        for fut in as_completed(futs):
            msg = fut.result()
            print(msg, flush=True)
            if msg.startswith("FAIL"):
                fails += 1
    print(f"done fails={fails}", flush=True)
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
