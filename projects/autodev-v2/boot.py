#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Clean AutoDev boot: find Grok, start the new loop."""
from __future__ import annotations

import os


def prepare() -> tuple[bool, str]:
    import start
    env = start.compat_env()
    if env is None:
        return False, "Grok CLI를 찾지 못했습니다. `grok version`을 확인해주세요."
    os.environ.update(env)
    return True, ""


def run_supervisor() -> int:
    import loop
    return int(loop.main())
