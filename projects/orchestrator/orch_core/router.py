"""승격 라우터 — 어느 등급의 에이전트를 쓸지 정한다.

원칙(§5·§15): 처음부터 가장 비싼 등급에 보내지 않는다. 다만 **고정 규칙만으로 정하지 않고**
작업 성격과 그동안의 실패를 점수로 본다. 점수와 사유를 항상 함께 돌려준다 —
"왜 이 등급이 붙었는지"가 로그에 남아야 나중에 규칙을 고칠 수 있다.
"""

from __future__ import annotations

import re
from dataclasses import dataclass

# 사다리: 낮은 등급 → 높은 등급. config.json의 ladder가 이 순서를 정한다.
# 각 시도는 사다리에서 한 칸을 고른다. 아래로 내려가는 일은 없다(한 번 올린 등급은 유지).

_HARD_HINT = re.compile(
    r"(리팩터|refactor|아키텍처|architecture|여러 파일|전반|설계|동시성|race|네트워크 동기화|상속 구조)", re.I)
_RESEARCH_HINT = re.compile(
    r"(최신|new API|deprecat|버전|migrat|어떤 패키지|라이브러리 선택|바뀐|changelog)", re.I)


@dataclass
class Decision:
    agent: str
    score: int
    reason: str
    research: bool = False


def decide(*, goal: str, ladder: list[str], attempt: int, failures: list[dict],
           changed_files: int = 0, research_agent: str | None = None) -> Decision:
    """이번 시도에 쓸 에이전트를 고른다.

    attempt는 1부터. failures는 지금까지의 실패 기록(각 dict에 verdict/reason).
    """
    if not ladder:
        raise ValueError("ladder가 비었다")

    score = 0
    why = []

    if _HARD_HINT.search(goal or ""):
        score += 2
        why.append("목표 문구가 구조 변경급")
    if changed_files >= 5:
        score += 1
        why.append(f"변경 파일 {changed_files}개")

    real_failures = [f for f in failures if f.get("verdict") not in (None, "UNKNOWN")]
    if real_failures:
        score += len(real_failures)
        why.append(f"실패 {len(real_failures)}회")

    # 같은 이유로 두 번 이상 막혔으면 같은 등급에 또 맡기지 않는다.
    reasons = [f.get("reason", "") for f in real_failures]
    if len(reasons) >= 2 and reasons[-1] and reasons[-1] == reasons[-2]:
        score += 2
        why.append("같은 이유로 반복 실패")

    # 한 시도에 한 칸 이상 뛰지 않는다 — 두 칸을 건너뛰면 중간 등급이 풀 수 있었는지
    # 영영 알 수 없고, 비싼 등급이 실제로 필요했는지도 확인이 안 된다.
    rung = min(score // 2, len(ladder) - 1, attempt)
    agent = ladder[rung]

    research = bool(research_agent) and bool(_RESEARCH_HINT.search(goal or "")) and attempt == 1
    if research:
        why.append("최신 정보가 필요한 목표")

    reason = f"점수 {score} → 사다리 {rung + 1}/{len(ladder)}({agent})"
    if why:
        reason += " — " + ", ".join(why)
    return Decision(agent=agent, score=score, reason=reason, research=research)
