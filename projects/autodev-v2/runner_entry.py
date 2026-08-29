#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AutoDev v2 실행 진입점.

기존 runner의 자율 루프는 그대로 사용하고 Codex fallback만 실시간 스트리밍으로 교체한다.
"""
from __future__ import annotations

import os
import shutil
import tempfile
from pathlib import Path
from typing import Any

import runner

AUTODEV = runner.AUTODEV


def codex_call(cfg: dict[str, Any], st: dict[str, Any], prompt: str, cwd: Path) -> tuple[int, str]:
    exe = shutil.which("codex")
    if not exe:
        return 127, "codex CLI를 찾을 수 없습니다."

    fd, outpath = tempfile.mkstemp(prefix="autodev_v2_codex_", suffix=".txt")
    os.close(fd)
    try:
        cmd = [exe, "exec", "--skip-git-repo-check", "-o", outpath, prompt]
        st["stats"]["codex_calls"] = int(st["stats"].get("codex_calls", 0)) + 1
        rc, streamed = AUTODEV.stream_process(
            cmd,
            cwd,
            timeout=900,
            env=AUTODEV.subscription_env(cfg, "codex"),
            tag="CODEX:fallback",
        )
        try:
            body = Path(outpath).read_text(encoding="utf-8", errors="replace").strip()
        except Exception:
            body = ""
        return rc, body or streamed
    finally:
        try:
            os.unlink(outpath)
        except OSError:
            pass


def main() -> int:
    AUTODEV.codex_call = codex_call
    return runner.main()


if __name__ == "__main__":
    raise SystemExit(main())
