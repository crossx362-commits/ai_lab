#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Copy AutoDev infra from origin/master. Fetch is best-effort."""
from __future__ import annotations

import io
import tarfile
from pathlib import Path

from procutil import run_retry

INFRA_PATHS = (
    "projects/autodev-v2",
    ".github/workflows/autodev-v2-tests.yml",
    "projects/ashes-to-stars/unity/Assets/Editor/AutoDevAcceptance/AutoDevAcceptanceRunner.cs",
)


def apply(repo: Path) -> tuple[bool, str]:
    fetch_ok = False
    fetch_msg = ""
    try:
        fetch = run_retry(
            ["git", "-c", "core.hooksPath=/dev/null", "fetch", "origin", "master"],
            cwd=repo, capture_output=True, text=True, timeout=180,
            encoding="utf-8", errors="replace", attempts=6,
        )
        fetch_ok = fetch.returncode == 0
        fetch_msg = (fetch.stderr or fetch.stdout or "")[-800:]
    except Exception as e:
        fetch_ok = False
        fetch_msg = f"{type(e).__name__}: {e}"
    try:
        arc = run_retry(
            ["git", "archive", "--format=tar", "origin/master", *INFRA_PATHS],
            cwd=repo, capture_output=True, timeout=60, attempts=6,
        )
    except Exception as e:
        return False, f"git archive 실패: {e}\nfetch: {fetch_msg}"
    if arc.returncode != 0:
        err = arc.stderr.decode("utf-8", "replace")[-800:] if isinstance(arc.stderr, (bytes, bytearray)) else str(arc.stderr or "")[-800:]
        return False, f"git archive 실패: {err}\nfetch: {fetch_msg}"
    with tarfile.open(fileobj=io.BytesIO(arc.stdout), mode="r:") as tf:
        tf.extractall(repo)
    if fetch_ok:
        return True, "AutoDev 시스템 파일 최신화 완료"
    return True, "fetch는 건너뛰고 이미 있는 origin/master로 최신화했습니다."
