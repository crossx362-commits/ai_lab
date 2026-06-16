# -*- coding: utf-8 -*-
"""Start Youngsuk Telegram receiver with file logging."""

import runpy
import sys
from pathlib import Path


def main() -> int:
    root = Path(__file__).resolve().parents[3]
    log_dir = root / "output" / "bot_logs"
    log_dir.mkdir(parents=True, exist_ok=True)

    sys.stdout = open(log_dir / "youngsuk_daemon.out.log", "a", encoding="utf-8", buffering=1)
    sys.stderr = open(log_dir / "youngsuk_daemon.err.log", "a", encoding="utf-8", buffering=1)

    script = next(root.glob("projects/ai-team/skills/*_비서/tools/telegram_receiver.py"))
    print(f"[runner] starting youngsuk: {script}")
    sys.path.insert(0, str(script.parent))
    sys.argv = [str(script)]
    runpy.run_path(str(script), run_name="__main__")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
