#!/usr/bin/env python3
"""Import estate_*_1/_2 + scaffold into Resources/props with .meta (knock_bg)."""
from pathlib import Path
import re
import sys
import uuid

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import knock_bg

SRC = HERE / "out_estate_tiers"
DST = HERE.parent / "unity" / "Assets" / "Resources" / "props"
META_TMPL = (DST / "estate_keep_0.png.meta").read_text(encoding="utf-8")

def write_meta(png: Path):
    meta = png.with_suffix(png.suffix + ".meta")
    if meta.exists():
        return
    guid = uuid.uuid4().hex
    text = re.sub(r"guid: [0-9a-fA-F]+", f"guid: {guid}", META_TMPL, count=1)
    meta.write_text(text, encoding="utf-8")
    print("meta", png.name, guid)

n = 0
DST.mkdir(parents=True, exist_ok=True)
for p in sorted(SRC.glob("estate_*.png")):
    # never overwrite existing _0 from this importer if somehow present
    if re.search(r"_0\.png$", p.name) and p.name != "estate_scaffold_0.png":
        print("skip _0", p.name)
        continue
    out = DST / p.name
    knock_bg.apply_path(p, out, crop=True)
    write_meta(out)
    n += 1
    print("→", p.name)
print("imported", n)
