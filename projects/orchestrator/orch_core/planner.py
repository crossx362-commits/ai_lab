"""Planner — 목표 하나를 작은 Task 여러 개로 분해한다(§6).

**응답을 stdout에서 긁지 않는다.** 모델에게 `plan.json`을 쓰게 하고 그 파일을 읽는다.
이유: stdout은 진행 로그·요약이 섞여 파싱이 불안정하고, 무엇보다 "모델이 뭔가 말했다"와
"계획이 실제로 생겼다"가 구분되지 않는다. 파일이 없으면 UNKNOWN이다 — 빈 계획을 만들지 않는다.
"""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path

PLAN_FILE = "plan.json"

PROMPT = """\
너는 비대화형 자동화 세션의 설계 담당이다. 사람에게 묻지 마라 — 답할 사람이 없다.
코드를 고치지 마라. 이번 일은 **분해**뿐이다.

[대상 프로젝트]
{project}
{unity_version}. 아래 목표를 구현 가능한 작은 Task로 나눈다.

[목표]
{goal}

[분해 규칙]
- Task 하나는 한 사람이 한 번에 끝낼 수 있는 크기여야 한다. 2~8개로 나눠라.
- 각 Task는 **그 자체로 컴파일되고 테스트가 통과할 수 있어야** 한다(반쪽 상태로 남기지 마라).
- 앞선 Task에 의존하면 depends_on에 그 key를 적는다. 순환 의존은 금지다.
- done_criteria는 "무엇을 보면 끝난 줄 아는가"를 검증 가능하게 적는다.
- 테스트 작성도 Task로 넣어라.
- 추측으로 파일 경로를 지어내지 마라. 확실하지 않으면 expected_files를 비워 둔다.

[반드시 이 파일을 만들 것]
{plan_path}
형식(JSON, 주석 없이):
{{
  "tasks": [
    {{
      "key": "T1",
      "goal": "한 줄 목표",
      "done_criteria": "무엇이 되면 끝인가",
      "depends_on": [],
      "risk": "low|medium|high",
      "expected_files": ["Assets/..."]
    }}
  ]
}}
이 파일을 만들지 않으면 실패로 기록된다. 설명은 파일 안에만 쓴다.
"""


@dataclass
class PlanTask:
    key: str
    goal: str
    done_criteria: str = ""
    depends_on: list[str] = field(default_factory=list)
    risk: str = "medium"
    expected_files: list[str] = field(default_factory=list)


@dataclass
class Plan:
    ok: bool
    reason: str
    tasks: list[PlanTask] = field(default_factory=list)
    raw_path: Path | None = None


def build_prompt(*, goal: str, project: Path, unity_version: str, plan_path: Path) -> str:
    return PROMPT.format(goal=goal, project=project, unity_version=unity_version, plan_path=plan_path)


def _topo_ok(tasks: list[PlanTask]) -> str | None:
    """순환 의존과 미지의 의존을 잡는다. 문제가 있으면 사유 문자열을 돌려준다."""
    keys = {t.key for t in tasks}
    for t in tasks:
        for d in t.depends_on:
            if d not in keys:
                return f"{t.key}이(가) 없는 Task '{d}'에 의존한다"
    # 위상 정렬이 되는지 확인
    pending = {t.key: set(t.depends_on) for t in tasks}
    done: set[str] = set()
    while pending:
        ready = [k for k, deps in pending.items() if deps <= done]
        if not ready:
            return f"순환 의존이 있다: {', '.join(sorted(pending))}"
        for k in ready:
            done.add(k)
            pending.pop(k)
    return None


def parse(plan_path: Path) -> Plan:
    if not plan_path.is_file():
        return Plan(False, f"계획 파일이 만들어지지 않았다: {plan_path.name}")
    try:
        data = json.loads(plan_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as e:
        return Plan(False, f"계획 파일이 JSON이 아니다: {e}", raw_path=plan_path)

    raw = data.get("tasks")
    if not isinstance(raw, list) or not raw:
        return Plan(False, "계획에 task가 없다", raw_path=plan_path)

    tasks: list[PlanTask] = []
    for i, item in enumerate(raw, 1):
        if not isinstance(item, dict) or not item.get("goal"):
            return Plan(False, f"{i}번째 task에 goal이 없다", raw_path=plan_path)
        tasks.append(PlanTask(
            key=str(item.get("key") or f"T{i}"),
            goal=str(item["goal"]).strip(),
            done_criteria=str(item.get("done_criteria", "")).strip(),
            depends_on=[str(d) for d in item.get("depends_on", []) or []],
            risk=str(item.get("risk", "medium")),
            expected_files=[str(f) for f in item.get("expected_files", []) or []],
        ))

    if len({t.key for t in tasks}) != len(tasks):
        return Plan(False, "task key가 중복된다", raw_path=plan_path)
    bad = _topo_ok(tasks)
    if bad:
        return Plan(False, bad, raw_path=plan_path)
    return Plan(True, f"task {len(tasks)}개", tasks=tasks, raw_path=plan_path)


def order(tasks: list[PlanTask]) -> list[PlanTask]:
    """의존성 순서대로 정렬한다(parse를 통과한 계획만 넣을 것)."""
    by_key = {t.key: t for t in tasks}
    pending = {t.key: set(t.depends_on) for t in tasks}
    out, done = [], set()
    while pending:
        ready = sorted(k for k, deps in pending.items() if deps <= done)
        for k in ready:
            out.append(by_key[k])
            done.add(k)
            pending.pop(k)
    return out
