"""yewon_dispatcher.py — 예원 하네스 실행 헬퍼(축소본).

라우팅/작업 분배는 yewon_orchestrator.py + _shared/registry.py 로 이관됨(레지스트리가 SSOT).
이 모듈에 에이전트 키워드/시스템 프롬프트를 다시 넣지 마라 — 死코드가 된다.
현재 유효한 것은 _run_harness 뿐(단위테스트가 SUPPRESS_TELEGRAM 억제를 검증).
"""
import os
import subprocess
import sys

_here = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(_here, "..", "..", "..", "..", ".."))
AI_TEAM_ROOT = os.path.join(PROJECT_ROOT, "projects", "ai-team")


def _run_script(args: list[str], timeout: int = 120, extra_env: dict[str, str] | None = None) -> tuple[int, str]:
    nowin = {"creationflags": subprocess.CREATE_NO_WINDOW} if sys.platform == "win32" else {}
    result = subprocess.run(
        args,
        cwd=PROJECT_ROOT,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=timeout,
        env={**os.environ, "PYTHONUTF8": "1", **(extra_env or {})},
        **nowin,
    )
    return result.returncode, (result.stdout or result.stderr or "").strip()


def _run_harness() -> str:
    script = os.path.join(AI_TEAM_ROOT, "harness", "check_all.py")
    code, output = _run_script(
        [sys.executable, script],
        timeout=60,
        extra_env={"SUPPRESS_TELEGRAM": "true"},
    )
    has_warn = "WARN" in output or "FAIL" in output or code != 0
    note = "WARN/FAIL 감지, 구조 점검 필요" if has_warn else "모든 구조 정상"
    icon = "⚠️" if has_warn else "✅"
    return f"{icon} [예원 CEO] 하네스 체크 완료\n\n{output}\n\n{note}"
