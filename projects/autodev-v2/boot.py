#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Clean AutoDev v2 boot.

Does not touch v1 schedules, launchd, or ORDERS. Starts the Grok loop only.
"""
from __future__ import annotations

import os
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent


def prepare() -> tuple[bool, str]:
    import start
    env = start.compat_env()
    if env is None:
        return False, "Grok CLI를 찾지 못했습니다. `grok version`을 확인해 주세요."
    os.environ.update(env)
    return True, ""


def install_loop_ext() -> None:
    try:
        import loop_ext
        loop_ext.install()
    except Exception as e:
        print(f"[BOOT] loop_ext skipped: {type(e).__name__}: {e}", flush=True)


def run_supervisor() -> int:
    install_loop_ext()
    import runner_entry
    old = sys.argv[:]
    sys.argv = [str(HERE / "engine.py"), "run", "--continuous"]
    try:
        return int(runner_entry.main())
    finally:
        sys.argv = old
