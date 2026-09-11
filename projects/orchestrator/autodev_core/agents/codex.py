"""Codex CLI 어댑터.

함정 대비(기존 시스템 사고에서 가져온 것):
- 프롬프트는 argv가 아니라 **stdin**으로 준다. 여러 줄 인자가 잘려 "지시가 전달 안 됨"을
  성공으로 기록한 사고가 있었다. `codex exec`는 stdin 프롬프트를 공식 지원한다.
- 비대화형이므로 "승인받고 고쳐라"가 데드락이다 — 프롬프트에 즉시 편집을 명시한다.
- rc=0은 "모델이 응답했다"일 뿐 "파일을 고쳤다"가 아니다. 판정은 호출부에서 git으로 한다.
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
- Unity {unity_version} 프로젝트다. C# 코드가 컴파일되어야 한다.
- 기존 테스트나 검증 코드를 삭제하거나 약화시켜 통과시키지 마라.
- 목표를 임의로 축소하지 마라.
- git commit / git push / 브랜치 조작은 하지 마라. 커밋은 오케스트레이터가 한다.
- Assets/AutoDev/ 아래(검증 장치)는 수정하지 마라.
{feedback}
끝나면 무엇을 바꿨는지 3줄 이내로 요약하라.
"""

FEEDBACK_TEMPLATE = """
[직전 시도 실패 — 이번에 고쳐야 할 것]
시도 #{n} 결과: {verdict} ({reason})

Unity가 보고한 오류:
{errors}

같은 방식으로 다시 시도하지 말고, 위 오류를 실제로 해결하라.
"""


class CodexAgent:
    name = "codex"

    def __init__(self, cfg: AgentConfig):
        self.cfg = cfg

    def model_name(self) -> str | None:
        return self.cfg.model

    def build_prompt(self, *, goal, worktree, allowed, unity_version, failure=None) -> str:
        feedback = ""
        if failure:
            feedback = FEEDBACK_TEMPLATE.format(
                n=failure.get("n"),
                verdict=failure.get("verdict"),
                reason=failure.get("reason"),
                errors=failure.get("errors") or "(Unity 로그에서 CS 오류를 추출하지 못했다)",
            )
        return PROMPT_TEMPLATE.format(
            goal=goal,
            worktree=worktree,
            allowed=", ".join(allowed),
            unity_version=unity_version,
            feedback=feedback,
        )

    def run(
        self,
        prompt: str,
        *,
        worktree: Path,
        log_prefix: Path,
        conn=None,
        task_id=None,
        attempt_id=None,
    ) -> AgentResult:
        cmd = [
            self.cfg.bin,
            "exec",
            "-C",
            str(worktree),
            "-s",
            self.cfg.sandbox_mode,
            "--color",
            "never",
            "-",  # 프롬프트를 stdin에서 읽는다
        ]
        if self.cfg.model:
            cmd[2:2] = ["-m", self.cfg.model]

        env = dict(os.environ)
        env["CI"] = "1"

        r = proc.run(
            cmd,
            cwd=worktree,
            timeout=self.cfg.timeout_sec,
            stdin_text=prompt,
            log_prefix=log_prefix,
            conn=conn,
            task_id=task_id,
            attempt_id=attempt_id,
            kind="agent",
            env=env,
        )

        if r.status == "SPAWN_FAILED":
            return AgentResult(False, None, r.status, "", r.stdout_path, r.duration_s,
                               reason=f"codex 실행 불가: {r.stderr[:200]}")
        if r.status == "TIMEOUT":
            return AgentResult(False, r.exit_code, r.status, r.stdout, r.stdout_path, r.duration_s,
                               reason=f"codex 타임아웃({self.cfg.timeout_sec}s)")
        return AgentResult(
            ok=(r.exit_code == 0),
            exit_code=r.exit_code,
            status=r.status,
            output=r.stdout,
            stdout_path=r.stdout_path,
            duration_s=r.duration_s,
            reason="" if r.exit_code == 0 else f"codex 종료코드 {r.exit_code}",
        )
