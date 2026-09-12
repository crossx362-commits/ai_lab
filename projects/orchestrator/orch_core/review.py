"""최종 리뷰 — 게이트를 통과한 diff를 한 등급 위가 다시 본다(§4 Astra 역할).

컴파일과 테스트가 통과해도 "목표를 축소했다", "테스트를 목표에 맞추는 대신 목표를 테스트에
맞췄다" 같은 것은 잡히지 않는다. 그래서 마지막에 사람 아닌 눈이 한 번 더 본다.

여기서도 **판정은 파일로 받는다**. 리뷰 결과 파일이 없으면 승인이 아니라 UNKNOWN이다 —
"리뷰가 돌지 않았다"를 "문제 없음"으로 바꾸지 않는다.
"""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path

REVIEW_FILE = "review.json"

PROMPT = """\
너는 비대화형 자동화 세션의 코드 리뷰 담당이다. 사람에게 묻지 마라.
**코드를 고치지 마라.** 이번 일은 판정뿐이다.

[원래 목표]
{goal}

[완료 조건]
{done_criteria}

[이미 통과한 것]
컴파일 통과, 테스트 통과({gates}).
즉 "돌아간다"는 확인됐다. 네가 볼 것은 **목표를 실제로 달성했는가**다.

[변경 내용(diff)]
{diff}

[반려해야 하는 경우]
- 목표를 임의로 축소했다(요구 중 일부만 했다).
- 테스트를 목표에 맞추지 않고, 통과하기 쉬운 것만 검사한다.
- 빈 구현·상수 반환·TODO로 때웠다.
- 명백히 동작하지 않을 코드다(컴파일만 되는 껍데기).

[승인해도 되는 경우]
- 목표를 충족했다. 스타일·취향 차이는 반려 사유가 아니다.

[반드시 이 파일을 만들 것]
{review_path}
형식(JSON):
{{"verdict": "approve" 또는 "reject", "reasons": ["짧은 사유", ...], "severity": "low|medium|high"}}
이 파일이 없으면 리뷰가 돌지 않은 것으로 처리된다.
"""


@dataclass
class Review:
    verdict: str  # APPROVE / REJECT / UNKNOWN
    reason: str
    reasons: list[str] = field(default_factory=list)
    severity: str = ""
    raw_path: Path | None = None

    @property
    def approved(self) -> bool:
        return self.verdict == "APPROVE"


def build_prompt(*, goal: str, done_criteria: str, gates: str, diff: str, review_path: Path,
                 max_diff: int = 20000) -> str:
    body = diff if len(diff) <= max_diff else diff[:max_diff] + f"\n... (diff가 길어 {max_diff}자에서 잘렸다)"
    return PROMPT.format(goal=goal, done_criteria=done_criteria or "(따로 명시되지 않음)",
                         gates=gates, diff=body, review_path=review_path)


def parse(review_path: Path) -> Review:
    if not review_path.is_file():
        return Review("UNKNOWN", "리뷰 결과 파일이 없다 — 리뷰가 돌았는지 알 수 없다")
    try:
        data = json.loads(review_path.read_text(encoding="utf-8"))
    except (ValueError, OSError) as e:
        return Review("UNKNOWN", f"리뷰 결과가 JSON이 아니다: {e}", raw_path=review_path)

    if not isinstance(data, dict) or not isinstance(data.get("reasons") or [], list):
        return Review("UNKNOWN", "리뷰 결과는 JSON 객체이며 reasons는 배열이어야 한다", raw_path=review_path)

    v = str(data.get("verdict", "")).strip().lower()
    reasons = [str(r) for r in (data.get("reasons") or [])]
    sev = str(data.get("severity", "")).strip().lower()
    if v == "approve":
        return Review("APPROVE", "리뷰 승인", reasons, sev, review_path)
    if v == "reject":
        head = reasons[0] if reasons else "사유 미기재"
        return Review("REJECT", f"리뷰 반려: {head}", reasons, sev, review_path)
    return Review("UNKNOWN", f"리뷰 판정을 읽지 못했다(verdict={v!r})", reasons, sev, review_path)
