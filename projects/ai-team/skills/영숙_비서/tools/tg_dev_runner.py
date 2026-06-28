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
from _shared.notify import send  # noqa: E402

load_env(str(PROJECT_ROOT))


def _git(*args: str, cwd: Path | None = None) -> subprocess.CompletedProcess:
    return subprocess.run(
        ["git", *args], cwd=str(cwd or PROJECT_ROOT),
        capture_output=True, text=True,
    )


def run(branch: str, request: str) -> None:
    import os
    claude = shutil.which("claude") or "/usr/local/bin/claude"
    if not os.access(claude, os.X_OK):
        send(f"❌ 개발 작업 실패 — claude 실행파일을 찾을 수 없음({claude}). 데몬 PATH 확인 필요.")
        return
    worktree = PROJECT_ROOT.parent / f"ailab-dev-{branch}"
    _git("worktree", "prune")  # 이전 크래시로 남은 stale worktree 정리

    # 1) 격리 worktree + 새 브랜치 (master 작업트리 보호)
    r = _git("worktree", "add", "-b", branch, str(worktree))
    if r.returncode != 0:
        send(f"❌ 개발 작업 실패 — worktree 생성 불가\n{r.stderr[:300]}")
        return

    try:
        # 2) 헤드리스 claude 실행 (격리된 worktree에서)
        #    acceptEdits: 파일 수정만 자동 승인, 검토·테스트·머지는 사람이(안전 모드)
        try:
            proc = subprocess.run(
                [claude, "-p", request, "--permission-mode", "acceptEdits"],
                cwd=str(worktree), capture_output=True, text=True, timeout=900,
            )
            summary = (proc.stdout or proc.stderr or "").strip()[-1200:]
        except subprocess.TimeoutExpired:
            send(f"⏱️ 개발 작업 시간초과(15분) — 브랜치 {branch} 확인 필요")
            return
        except Exception as exc:
            send(f"❌ claude 실행 실패: {exc}")
            return

        # 3) 변경 커밋
        _git("add", "-A", cwd=worktree)
        diff = _git("diff", "--cached", "--stat", cwd=worktree).stdout.strip()
        if not diff:
            send(f"ℹ️ 개발 작업 완료 — 변경 없음.\n요청: {request}\n\nclaude 요약:\n{summary[:700]}")
            return
        _git("commit", "-m", f"tg-dev: {request[:60]}", cwd=worktree)
        send(
            f"✅ 개발 작업 완료 (브랜치 {branch})\n"
            f"요청: {request}\n\n"
            f"변경 파일:\n{diff[:700]}\n\n"
            f"검토 후 반영하려면 (master에서):\n  git merge {branch}\n"
            f"폐기하려면:\n  git branch -D {branch}\n\n"
            f"claude 요약:\n{summary[:700]}"
        )
    finally:
        # 4) worktree 디렉터리만 정리(브랜치 ref는 main 저장소에 남아 머지 가능)
        _git("worktree", "remove", str(worktree), "--force")


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("usage: tg_dev_runner.py <branch> <request>")
        sys.exit(1)
    # 어떤 예외로 죽어도 소유자에게 침묵하지 않도록 최상위 보호 — '시작했어'만 받고 무한대기 방지
    try:
        run(sys.argv[1], sys.argv[2])
    except Exception as exc:
        try:
            send(f"❌ 개발 작업 비정상 종료: {exc}")
        except Exception:
            pass
        raise
