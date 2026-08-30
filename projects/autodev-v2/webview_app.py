#!/usr/bin/env python3
# patched dashboard loader
from pathlib import Path
import runpy
impl = Path(__file__).with_name('webview_runtime.py')
if impl.is_file():
    runpy.run_path(str(impl), run_name='__main__')
else:
    raise SystemExit('webview_runtime.py missing')
