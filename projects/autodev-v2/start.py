#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AutoDev v2 원클릭 진입점.

1) v1 토큰 소모 루프 비활성화
2) 실제 Grok CLI를 찾아 버전 호환 래퍼 준비
3) preflight로 안전/예산/핵심 CLI 기능 확인
4) v2 연속 실행
"""
from __future__ import annotations

import os
import shlex
import shutil
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
RUNTIME_BIN = REPO / "output" / "autodev_v2" / "runtime_bin"


def find_real_grok() -> str | None:
    exe = shutil.which("grok")
    if exe:
        return exe
    for p in (
        "/usr/local/bin/grok",
        "/opt/homebrew/bin/grok",
        str(Path.home() / ".local" / "bin" / "grok"),
    ):
        if os.path.isfile(p) and os.access(p, os.X_OK):
            return p
    return None


def compat_env() -> dict[str, str] | None:
    """AutoDev 프로세스 안에서만 Grok 호환 래퍼를 PATH 앞에 둔다."""
    real = find_real_grok()
    if not real:
        print("Grok CLI를 찾을 수 없습니다. `grok version`을 먼저 확인하세요.")
        return None

    RUNTIME_BIN.mkdir(parents=True, exist_ok=True)
    wrapper = RUNTIME_BIN / "grok"
    compat = HERE / "grok_compat.py"
    wrapper.write_text(
        "#!/bin/sh\nexec "
        + shlex.quote(sys.executable)
        + " "
        + shlex.quote(str(compat))
        + ' "$@"\n',
        encoding="utf-8",
    )
    wrapper.chmod(0o755)

    env = os.environ.copy()
    env["AUTODEV_REAL_GROK"] = real
    env["PATH"] = str(RUNTIME_BIN) + os.pathsep + env.get("PATH", "")
    print(f"Grok CLI: {real}")
    print("Grok CLI 호환 모드: 설치 버전에 없는 선택 절약 옵션은 자동 생략")
    return env


def main() -> int:
    mig = subprocess.run([sys.executable, str(HERE / "migrate_v1.py"), "--apply"])
    if mig.returncode != 0:
        print("v1 전환 단계가 실패해 v2 시작을 중단합니다.")
        return mig.returncode

    env = compat_env()
    if env is None:
        return 127

    audit = subprocess.run([sys.executable, str(HERE / "preflight.py")], env=env)
    if audit.returncode != 0:
        print("preflight가 실패했습니다. 핵심 CLI/안전 가드를 확인한 뒤 v2를 다시 실행하세요.")
        return audit.returncode

    return subprocess.run(
        [sys.executable, str(HERE / "autodev.py"), "run", "--continuous"],
        env=env,
    ).returncode


if __name__ == "__main__":
    raise SystemExit(main())
