"""테스트용 결정적 에이전트.

게이트를 시험하려면 **원하는 실패를 마음대로 만들 수 있는 가짜 개발자**가 필요하다.
모델을 부르면 매번 다르게 행동해서 게이트가 빨간불인지 확인할 수가 없다.
이 에이전트는 설정에 적힌 셸 명령을 worktree 안에서 그대로 실행한다(프롬프트는 stdin).

운영 경로가 아니다 — config에 `"type": "script"`로 명시해야만 쓰인다.
"""

from __future__ import annotations

import os
from pathlib import Path

from .. import proc
from ..config import AgentConfig
from .codex import AgentResult, PROMPT_TEMPLATE, FEEDBACK_TEMPLATE


class ScriptAgent:
    name = "script"

    def __init__(self, cfg: AgentConfig):
        self.cfg = cfg

    def model_name(self) -> str | None:
        return self.cfg.model or "script"

    def build_prompt(self, *, goal, worktree, allowed, unity_version, failure=None) -> str:
        feedback = ""
        if failure:
            feedback = FEEDBACK_TEMPLATE.format(
                n=failure.get("n"),
                verdict=failure.get("verdict"),
                reason=failure.get("reason"),
                errors=failure.get("errors") or "(없음)",
            )
        return PROMPT_TEMPLATE.format(
            goal=goal, worktree=worktree, allowed=", ".join(allowed),
            unity_version=unity_version, feedback=feedback,
        )

    def run(self, prompt, *, worktree: Path, log_prefix: Path, conn=None, task_id=None, attempt_id=None):
        cmd = [self.cfg.bin, *self.cfg.args]
        env = dict(os.environ)
        env["AUTODEV_WORKTREE"] = str(worktree)
        env["AUTODEV_ATTEMPT"] = str(attempt_id or 0)
        r = proc.run(
            cmd, cwd=worktree, timeout=self.cfg.timeout_sec, stdin_text=prompt,
            log_prefix=log_prefix, conn=conn, task_id=task_id, attempt_id=attempt_id,
            kind="agent", env=env,
        )
        if r.status == "SPAWN_FAILED":
            return AgentResult(False, None, r.status, "", r.stdout_path, r.duration_s,
                               reason=f"script 실행 불가: {r.stderr[:200]}")
        if r.status == "TIMEOUT":
            return AgentResult(False, r.exit_code, r.status, r.stdout, r.stdout_path, r.duration_s,
                               reason=f"script 타임아웃({self.cfg.timeout_sec}s)")
        return AgentResult(
            ok=(r.exit_code == 0), exit_code=r.exit_code, status=r.status,
            output=r.stdout, stdout_path=r.stdout_path, duration_s=r.duration_s,
            reason="" if r.exit_code == 0 else f"script 종료코드 {r.exit_code}",
        )
