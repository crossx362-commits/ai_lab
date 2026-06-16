# -*- coding: utf-8 -*-
"""Start the trading team as a detached background process and write logs."""

import os
import subprocess
import sys
from datetime import datetime
from pathlib import Path


def main() -> int:
    workspace_root = Path(__file__).resolve().parents[3]
    script = workspace_root / "projects" / "ai-team" / "scripts" / "start_trading_team.py"
    log_dir = workspace_root / "output" / "trading_logs"
    log_dir.mkdir(exist_ok=True)

    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    out_path = log_dir / f"trading_team_{stamp}.out.log"
    err_path = log_dir / f"trading_team_{stamp}.err.log"
    pid_path = log_dir / "trading_team.pid"

    args = [sys.executable, str(script)]
    if "--live" in sys.argv[1:]:
        args.append("--live")

    creationflags = 0
    if sys.platform == "win32":
        creationflags = subprocess.CREATE_NEW_PROCESS_GROUP | subprocess.DETACHED_PROCESS

    out_file = open(out_path, "ab", buffering=0)
    err_file = open(err_path, "ab", buffering=0)
    process = subprocess.Popen(
        args,
        cwd=str(workspace_root),
        stdout=out_file,
        stderr=err_file,
        stdin=subprocess.DEVNULL,
        creationflags=creationflags,
        close_fds=False,
    )

    pid_path.write_text(str(process.pid), encoding="utf-8")
    print(f"TRADING_TEAM_PID={process.pid}")
    print(f"LOG_OUT={out_path}")
    print(f"LOG_ERR={err_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
