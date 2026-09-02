#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AutoDev v2 compatibility helpers.

Canonical runtime entrypoint is engine.py (normally started by the dashboard).
This module only keeps CLI-wrapper preparation helpers and delegates old
`start.py` launches to engine.main().
"""
from __future__ import annotations

import os
import shlex
import shutil
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
RUNTIME_BIN = REPO / "output" / "autodev_v2" / "runtime_bin"


def find_real_cli(name: str) -> str | None:
    exe = shutil.which(name)
    if exe:
        return exe
    for p in (
        f"/usr/local/bin/{name}",
        f"/opt/homebrew/bin/{name}",
        str(Path.home() / ".local" / "bin" / name),
    ):
        if os.path.isfile(p) and os.access(p, os.X_OK):
            return p
    return None


def write_wrapper(name: str, script: Path) -> Path:
    wrapper = RUNTIME_BIN / name
    wrapper.write_text(
        "#!/bin/sh\nexec " + shlex.quote(sys.executable) + " "
        + shlex.quote(str(script)) + ' "$@"\n',
        encoding="utf-8",
    )
    wrapper.chmod(0o755)
    return wrapper


def compat_env() -> dict[str, str] | None:
    grok = find_real_cli("grok")
    if not grok:
        print("Grok CLI를 찾을 수 없습니다. `grok version`을 먼저 확인하세요.")
        return None
    codex = find_real_cli("codex")

    RUNTIME_BIN.mkdir(parents=True, exist_ok=True)
    write_wrapper("grok", HERE / "grok_compat.py")
    if codex:
        write_wrapper("codex", HERE / "codex_compat.py")

    env = os.environ.copy()
    env["AUTODEV_REAL_GROK"] = grok
    if codex:
        env["AUTODEV_REAL_CODEX"] = codex
    env["PATH"] = str(RUNTIME_BIN) + os.pathsep + env.get("PATH", "")

    env.setdefault("AUTODEV_GROK_QUOTA_COOLDOWN_SECONDS", "3600")
    env.setdefault(
        "AUTODEV_GROK_QUOTA_STATE",
        str(REPO / "output" / "autodev_v2" / "grok_quota_exhausted.json"),
    )
    env.setdefault("AUTODEV_CODEX_QUOTA_COOLDOWN_SECONDS", "300")
    env.setdefault(
        "AUTODEV_CODEX_QUOTA_STATE",
        str(REPO / "output" / "autodev_v2" / "codex_quota_exhausted.json"),
    )

    print(f"Grok CLI: {grok}")
    print(f"Codex CLI: {codex or '(미설치/미발견)'}")
    print("Director: Grok 전담 · Ollama 계획 사용 안 함")
    print("Anti-Loop: 중복 작업/같은 영역 반복/같은 실패를 로컬 코드로 차단")
    print("Rollback: 실패 작업의 변경만 복원하고 기존 사용자 변경은 보존")
    print("검증: 컴파일 PASS + 작업별 실제 Unity Acceptance PASS 필수")
    print("Unity 사용 중: 구현은 진행할 수 있고 완료 판정만 검증 대기")
    print("Supervisor: 배치 상한 후에도 계속 실행 · 시간당 클라우드 상한 적용")
    print("Provider 쿼터 보호: Grok 1시간 / Codex 5분 후 실제 재확인")
    return env


def main() -> int:
    print("start.py는 호환 진입점입니다. 단일 engine.py로 위임합니다.")
    import engine
    return engine.main()


if __name__ == "__main__":
    raise SystemExit(main())
