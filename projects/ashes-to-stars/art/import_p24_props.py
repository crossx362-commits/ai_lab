#!/usr/bin/env python3
from pathlib import Path
import sys
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import knock_bg
SRC = HERE / "out_p24_props"
DST = HERE.parent / "unity" / "Assets" / "Resources" / "props"
n=0
for p in SRC.glob("*.png"):
    DST.mkdir(parents=True, exist_ok=True)
    knock_bg.apply_path(p, DST / p.name, crop=True)
    n += 1
    print("→", p.name)
print("props", n)
