#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Retry process creation when macOS returns EAGAIN / Errno 35."""
from __future__ import annotations

import time
import subprocess
from typing import Any


def is_busy(exc: BaseException) -> bool:
    if isinstance(exc, BlockingIOError):
        return True
    return getattr(exc, "errno", None) == 35


def run_retry(*args: Any, attempts: int = 8, **kwargs: Any) -> subprocess.CompletedProcess[Any]:
    last: BaseException | None = None
    for i in range(attempts):
        try:
            return subprocess.run(*args, **kwargs)
        except (BlockingIOError, OSError) as e:
            last = e
            if not is_busy(e):
                raise
            time.sleep(0.4 * (i + 1))
    raise last or OSError("process spawn failed")


def popen_retry(*args: Any, attempts: int = 8, **kwargs: Any) -> subprocess.Popen[Any]:
    last: BaseException | None = None
    sessions = [kwargs.get("start_new_session", True), False]
    for i in range(attempts):
        for session in sessions:
            kw = dict(kwargs)
            kw["start_new_session"] = session
            try:
                return subprocess.Popen(*args, **kw)
            except (BlockingIOError, OSError) as e:
                last = e
                if not is_busy(e):
                    raise
                time.sleep(0.4 * (i + 1))
    raise last or OSError("process spawn failed")
