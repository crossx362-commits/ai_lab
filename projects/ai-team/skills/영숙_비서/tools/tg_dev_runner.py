#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""텔레그램 '개발 요청' 헤드리스 실행기.

영숙이 받은 '개발 <요청>'을 격리된 git worktree에서 헤드리스 claude로 실행하고,
변경 사항을 새 브랜치에 커밋한 뒤 diff 요약을 텔레그램으로 보고한다.
master(봇·데몬이 도는 작업트리)는 건드리지 않는다 — 사용자가 검토 후 직접 머지.
"""

from __future__ import annotations

import shutil
import subprocess
import sys
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[4]
sys.path.insert(0, str(PROJECT_ROOT / "projects" / "ai-team"))

from _shared.env import load_env  # noqa: E402
from _shared.telegram import send  # noqa: E402

load_env(str(PROJECT_ROOT))


def _git(*args: str, cwd: Path | None = None) -> subprocess.CompletedProcess:
    return subprocess.run(
        ["git", *args], cwd=str(cwd or PROJECT_ROOT),
        capture_output=True, text=True,
    )


def run(branch: str, request: str) -> None:
    print("텔레그램 연동이 영구 비활성화되어 개발 요청 실행이 중단되었습니다.")
    return


if __name__ == "__main__":
    run("", "")

