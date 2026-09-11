"""에이전트 공통부.

프롬프트 문구와 판정 규칙이 어댑터마다 복사되면, 한쪽만 고쳐지고 나머지는 옛 규칙으로 돈다.
그래서 **공통은 여기 한 곳에만** 두고 어댑터는 argv 조립만 한다.
"""

from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path

from .. import proc
from ..config import AgentConfig


@dataclass
class AgentResult:
    ok: bool
    exit_code: int | None
    status: str
    output: str
    stdout_path: Path
    duration_s: float
    reason: str = ""
    stderr: str = ""          # Provider 장애(한도·인증)는 대개 **여기로** 온다

    @property
    def all_output(self) -> str:
        """판정에 쓸 전체 출력. stdout만 보다가 실전에서 뚫렸다 —
        codex는 "usage limit" 오류를 stderr로만 냈고, 시스템은 그것을 코드 실패로 오판해
        시도 3번을 태우고 BLOCKED로 세웠다(2026-09-11, 계획 7 T4)."""
        return (self.output or "") + "\n" + (self.stderr or "") + "\n" + (self.reason or "")


PROMPT_TEMPLATE = """\
너는 비대화형(헤드리스) 자동화 세션에서 실행 중이다. 사람에게 승인을 묻지 마라 —
묻는 순간 아무도 답하지 않고 이 작업은 실패로 기록된다. 계획만 쓰고 끝내는 것도 실패다.
지금 바로 파일을 편집하라.

[작업 디렉터리]
{worktree}
이 디렉터리는 너를 위해 만들어진 격리된 git worktree다. 이 안에서만 작업하라.

[허용 범위]
{allowed}
이 범위 밖의 파일은 건드리지 마라.

[목표]
{goal}

[반드시 지킬 것]
- Unity {unity_version} 프로젝트다. C# 코드가 컴파일되어야 하고 테스트가 통과해야 한다.
- 기존 테스트나 검증 코드를 **삭제·약화시켜 통과시키지 마라**. 지우면 그 자리에서 실패 처리된다.
- 목표를 임의로 축소하지 마라.
- git commit / git push / 브랜치 조작은 하지 마라. 커밋은 오케스트레이터가 한다.
- Assets/AutoDev/ 아래(검증 장치)는 수정하지 마라.
{feedback}
끝나면 무엇을 바꿨는지 3줄 이내로 요약하라.
"""

FEEDBACK_TEMPLATE = """
[직전 시도 실패 — 이번에 고쳐야 할 것]
시도 #{n} 결과: {verdict} ({reason})

{errors}

같은 방식으로 다시 시도하지 말고, 위 오류를 실제로 해결하라.
"""


class CliAgent:
    """CLI 하나를 불러 worktree를 고치게 하는 에이전트.

    하위 클래스는 `argv()`만 구현한다. 프롬프트를 stdin으로 줄지 argv로 줄지도 하위가 정한다
    (`stdin_prompt`) — CLI마다 다르고, 잘못 고르면 **지시가 잘린 채 성공으로 기록된다**.
    """

    name = "cli"
    stdin_prompt = True

    def __init__(self, cfg: AgentConfig):
        self.cfg = cfg

    # --- 하위가 구현 ---
    def argv(self, worktree: Path) -> list[str]:
        raise NotImplementedError

    # --- 공통 ---
    def model_name(self) -> str | None:
        return self.cfg.model

    def build_prompt(self, *, goal, worktree, allowed, unity_version, failure=None,
                     handoff=None) -> str:
        # 인수인계(다른 Provider에서 넘어온 경우)가 먼저다 — "이미 절반 돼 있다"를 모르면
        # 새 담당이 처음부터 다시 만든다.
        feedback = handoff or ""
        if failure:
            feedback += FEEDBACK_TEMPLATE.format(
                n=failure.get("n"),
                verdict=failure.get("verdict"),
                reason=failure.get("reason"),
                errors=failure.get("errors") or "(오류를 추출하지 못했다)",
            )
        return PROMPT_TEMPLATE.format(
            goal=goal, worktree=worktree, allowed=", ".join(allowed),
            unity_version=unity_version, feedback=feedback,
        )

    def env(self) -> dict:
        e = dict(os.environ)
        e["CI"] = "1"
        return e

    def run(self, prompt, *, worktree: Path, log_prefix: Path, conn=None, task_id=None, attempt_id=None):
        cmd = self.argv(worktree)
        if not self.stdin_prompt:
            cmd = cmd + [prompt]
        r = proc.run(
            cmd, cwd=worktree, timeout=self.cfg.timeout_sec,
            stdin_text=prompt if self.stdin_prompt else None,
            log_prefix=log_prefix, conn=conn, task_id=task_id, attempt_id=attempt_id,
            kind="agent", env=self.env(),
        )
        label = self.cfg.name
        if r.status == "SPAWN_FAILED":
            return AgentResult(False, None, r.status, "", r.stdout_path, r.duration_s,
                               reason=f"{label} 실행 불가: {r.stderr[:200]}", stderr=r.stderr)
        if r.status == "TIMEOUT":
            return AgentResult(False, r.exit_code, r.status, r.stdout, r.stdout_path, r.duration_s,
                               reason=f"{label} 타임아웃({self.cfg.timeout_sec}s)", stderr=r.stderr)
        return AgentResult(
            ok=(r.exit_code == 0), exit_code=r.exit_code, status=r.status,
            output=r.stdout, stdout_path=r.stdout_path, duration_s=r.duration_s,
            reason="" if r.exit_code == 0 else f"{label} 종료코드 {r.exit_code}",
            stderr=r.stderr,
        )
