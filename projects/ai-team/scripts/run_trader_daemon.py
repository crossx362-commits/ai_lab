# -*- coding: utf-8 -*-
"""ASCII-path wrapper for Dave/Leo daemon processes."""

import runpy
import sys
from pathlib import Path


def main() -> int:
    if len(sys.argv) < 2 or sys.argv[1] not in {"dave", "leo"}:
        print("usage: run_trader_daemon.py dave|leo [--live]")
        return 2

    name = sys.argv[1]
    live = "--live" in sys.argv[2:]
    root = Path(__file__).resolve().parents[3]
    log_dir = root / "output" / "trading_logs"
    log_dir.mkdir(parents=True, exist_ok=True)

    stdout_path = log_dir / f"{name}_daemon.out.log"
    stderr_path = log_dir / f"{name}_daemon.err.log"
    sys.stdout = open(stdout_path, "a", encoding="utf-8", buffering=1)
    sys.stderr = open(stderr_path, "a", encoding="utf-8", buffering=1)

    if name == "dave":
        script = next(root.glob("projects/ai-team/skills/*_주식/tools/upbit_auto_trader.py"))
    else:
        script = next(root.glob("projects/ai-team/skills/레오_*/tools/leo_aggressive_trader.py"))

    print(f"[runner] starting {name}: {script}")
    sys.path.insert(0, str(script.parent))
    sys.argv = [str(script), "--daemon", "--live" if live else "--sim"]
    runpy.run_path(str(script), run_name="__main__")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
