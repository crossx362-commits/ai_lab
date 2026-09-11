"""에이전트 공통부.

프롬프트 문구와 판정 규칙이 어댑터마다 복사되면, 한쪽만 고쳐지고 나머지는 옛 규칙으로 돈다.
그래서 **공통은 여기 한 곳에만** 두고 어댑터는 argv 조립만 한다.
"""

from __future__ import annotations

import os
import re
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
    tokens: int | None = None  # CLI가 보고한 실제 토큰. 보고 안 하면 None(어림값을 넣지 않는다)

    @property
    def all_output(self) -> str:
        """판정에 쓸 전체 출력. stdout만 보다가 실전에서 뚫렸다 —
        codex는 "usage limit" 오류를 stderr로만 냈고, 시스템은 그것을 코드 실패로 오판해
        시도 3번을 태우고 BLOCKED로 세웠다(2026-09-11, 계획 7 T4)."""
        return (self.output or "") + "\n" + (self.stderr or "") + "\n" + (self.reason or "")


# CLI가 스스로 보고한 사용량만 읽는다. 형식이 바뀌면 조용히 None이 되고, 그때는
# "보고 없음"이라고 말한다 — 옛 형식으로 읽은 값을 지금 값인 척하는 것보다 낫다.
_TOKENS = re.compile(r"tokens?\s+used[^\d]{0,20}([\d,]+)", re.I)


def parse_tokens(text: str) -> int | None:
    m = _TOKENS.search(text or "")
    if not m:
        return None
    try:
        n = int(m.group(1).replace(",", ""))
    except ValueError:
        return None
    return n if n > 0 else None


# --- 컨텍스트 상한(§10) -------------------------------------------------
# 프롬프트는 조용히 커진다. 유니티 오류 로그 한 번이면 수십만 자가 되고, 그때 CLI는
# "너무 길다"고 죽거나 **앞부분을 스스로 잘라먹는다** — 어느 쪽이든 목표 문구가 사라진다.
# 그래서 우리가 먼저, **어디를 얼마나 잘랐는지 프롬프트 안에 적으면서** 자른다.
# 조용히 자르면 AI는 자기가 전부 봤다고 믿는다. 그게 이 규칙의 이유다.
PROMPT_MAX_DEFAULT = 48000


def clamp(text: str, limit: int, label: str) -> str:
    """머리와 꼬리를 남기고 가운데를 접는다. 오류는 끝에, 맥락은 앞에 있기 때문이다."""
    if limit <= 0 or len(text) <= limit:
        return text
    keep = max(200, (limit - 120) // 2)
    cut = len(text) - keep * 2
    return (text[:keep]
            + f"\n\n…[{label}: 총 {len(text):,}자 중 가운데 {cut:,}자 생략 — 전문은 logs/에 있다]…\n\n"
            + text[-keep:])


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
                     handoff=None, max_chars: int = 0) -> str:
        cap = max_chars or PROMPT_MAX_DEFAULT
        # 인수인계(다른 Provider에서 넘어온 경우)가 먼저다 — "이미 절반 돼 있다"를 모르면
        # 새 담당이 처음부터 다시 만든다.
        # 지시문(목표·완료 조건·규칙)은 **절대 자르지 않는다.** 자를 곳은 피드백뿐이다 —
        # 목표가 잘리면 AI가 요구사항을 축소하는데, 그건 §6 위반이고 여기서 만들면 안 된다.
        feedback = clamp(handoff or "", int(cap * 0.35), "인수인계")
        if failure:
            feedback += FEEDBACK_TEMPLATE.format(
                n=failure.get("n"),
                verdict=failure.get("verdict"),
                reason=failure.get("reason"),
                errors=clamp(failure.get("errors") or "(오류를 추출하지 못했다)",
                             int(cap * 0.45), "오류 로그"),
            )
        p = PROMPT_TEMPLATE.format(
            goal=goal, worktree=worktree, allowed=", ".join(allowed),
            unity_version=unity_version, feedback=feedback,
        )
        if len(p) > cap:          # 그래도 넘치면 피드백만 더 접는다(지시문은 손대지 않는다)
            room = cap - (len(p) - len(feedback))
            feedback = clamp(feedback, max(400, room), "피드백 전체")
            p = PROMPT_TEMPLATE.format(
                goal=goal, worktree=worktree, allowed=", ".join(allowed),
                unity_version=unity_version, feedback=feedback,
            )
        return p

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
            tokens=parse_tokens((r.stdout or "") + "\n" + (r.stderr or "")),
        )
