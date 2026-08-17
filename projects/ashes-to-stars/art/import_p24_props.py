#!/usr/bin/env python3
from pathlib import Path
import shutil
HERE = Path(__file__).resolve().parent
SRC = HERE / "out_p24_props"
DST = HERE.parent / "unity" / "Assets" / "Resources" / "props"
n=0
for p in SRC.glob("*.png"):
    DST.mkdir(parents=True, exist_ok=True)
    shutil.copy2(p, DST / p.name)
    n += 1
    print("→", p.name)
print("props", n)
