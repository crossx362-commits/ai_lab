#!/usr/bin/env python3
"""yewon_orchestrator.py — 예원(CEO) 플랜 기반 멀티 에이전트 오케스트레이션 (단계 B).

기존 yewon_dispatcher(단일 라우팅)를 격상: 하나의 지시를 여러 에이전트 작업으로
쪼개고(plan), 의존성 순서대로 실행한 뒤 결과를 취합한다.

플랜 형식:
  [{"step":1,"agent":"somi","task":"...","depends_on":[]},
   {"step":2,"agent":"youngsuk","task":"...","depends_on":[1]}]

폴백: LLM 미가용/플랜 실패 시 단일 키워드 라우팅(= 기존 dispatcher 동작)으로 안전 강등.
작업 상태는 output/cache/task_queue.json 에 기록(재시작 복원·감사용).
"""
from __future__ import annotations

import json
import sys
from datetime import datetime
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

_here = Path(__file__).resolve().parent
AI_TEAM = _here.parents[2]
PROJECT_ROOT = AI_TEAM.parents[1]
sys.path.insert(0, str(AI_TEAM))

from _shared.registry import active_agents, route_by_keyword, get_agent  # noqa: E402
from _shared.agent_loop import run_agent                       # noqa: E402
from _shared.llm import text as llm_text, is_available as llm_available  # noqa: E402
from _shared.notify import report                              # noqa: E402

sys.path.insert(0, str(_here))
import agent_factory                                           # noqa: E402

# 시스템/CEO 자체 작업 키워드 — 이런 건 자동생성 대상이 아님(예원이 직접 처리)
_CEO_HINTS = ("하네스", "harness", "스킬", "감사", "점검", "시스템", "에이전트", "현황", "상태")

QUEUE = PROJECT_ROOT / "output" / "cache" / "task_queue.json"
MAX_STEPS = 5


def _roster_text() -> str:
    lines = []
    for aid, m in active_agents().items():
        lines.append(f"  - {aid}: {m.get('role')}")
    return "\n".join(lines)


def _plan_prompt(message: str) -> str:
    return (
        "당신은 CEO 예원입니다. 아래 활성 에이전트가 있습니다:\n"
        f"{_roster_text()}\n\n"
        f'지시: "{message}"\n\n'
        "지시를 에이전트 작업들로 분해하세요. 한 에이전트로 충분하면 1개 step만.\n"
        "기존 에이전트 누구도 맡기 어려운 작업이면 agent를 \"new\"로 두고 "
        "new_role(한 줄 역할)·new_keywords(키워드 배열)를 채워 신규 에이전트를 만들게 하세요.\n"
        "JSON 배열만 반환(설명 금지):\n"
        '[{"step":1,"agent":"<id|new>","task":"구체 작업","depends_on":[],'
        '"new_role":"(new일때만)","new_keywords":["(new일때만)"]}]'
    )


def _parse_plan(raw: str | None, valid: set[str]) -> list[dict] | None:
    if not raw:
        return None
    s = raw.strip()
    a, b = s.find("["), s.rfind("]")
    cand = s[a:b + 1] if a >= 0 and b > a else s
    try:
        arr = json.loads(cand)
    except Exception:
        return None
    if not isinstance(arr, list):
        return None
    plan = []
    for i, item in enumerate(arr[:MAX_STEPS], 1):
        if not isinstance(item, dict):
            continue
        agent = str(item.get("agent", "")).lower()
        if agent != "new" and agent not in valid:
            continue
        step = {
            "step": int(item.get("step", i)),
            "agent": agent,
            "task": str(item.get("task") or "")[:300] or "(작업 미상)",
            "depends_on": [int(x) for x in item.get("depends_on", []) if str(x).isdigit()],
        }
        if agent == "new":
            step["new_role"] = str(item.get("new_role") or item.get("task") or "")[:80]
            step["new_keywords"] = [k for k in item.get("new_keywords", []) if isinstance(k, str)][:6]
        plan.append(step)
    return plan or None


def _make_plan(message: str) -> list[dict]:
    valid = set(active_agents().keys())
    if llm_available():
        raw = llm_text(_plan_prompt(message), json_mode=True, max_tokens=400,
                       temperature=0.3, lm_first=False)
        plan = _parse_plan(raw, valid)
        if plan:
            return plan
    # 폴백: 키워드 단일 라우팅
    aid = route_by_keyword(message)
    if aid:
        return [{"step": 1, "agent": aid, "task": message[:300], "depends_on": []}]
    # 매칭 에이전트 없음 → CEO 작업이 아니면 자율 생성 대상으로
    low = message.lower()
    if any(h in low for h in _CEO_HINTS):
        return [{"step": 1, "agent": "ceo", "task": message[:300], "depends_on": []}]
    return [{"step": 1, "agent": "new", "task": message[:300], "depends_on": [],
             "new_role": message[:80], "new_keywords": []}]


def _save_queue(record: dict) -> None:
    QUEUE.parent.mkdir(parents=True, exist_ok=True)
    history = []
    if QUEUE.exists():
        try:
            history = json.loads(QUEUE.read_text(encoding="utf-8"))
        except Exception:
            history = []
    history.append(record)
    QUEUE.write_text(json.dumps(history[-50:], ensure_ascii=False, indent=2), encoding="utf-8")


def orchestrate(message: str) -> str:
    report("예원", "오케스트레이션 시작", message[:80])
    plan = _make_plan(message)
    ts = datetime.now().isoformat(timespec="seconds")

    rec = {"ts": ts, "message": message, "plan": plan, "steps": []}
    done: dict[int, str] = {}
    out_blocks = [f"🧭 [예원 CEO] 작업 분배 ({len(plan)}단계)"]
    for s in plan:
        deps = ", ".join(str(d) for d in s["depends_on"]) or "-"
        out_blocks.append(f"  step{s['step']} → {s['agent']} (선행:{deps}): {s['task']}")

    # 의존성 순서대로 순차 실행 (depends_on 충족된 step부터)
    pending = list(plan)
    guard = 0
    while pending and guard < MAX_STEPS * 2:
        guard += 1
        progressed = False
        for s in list(pending):
            if all(d in done for d in s["depends_on"]):
                context = ""
                if s["depends_on"]:
                    prior = "\n".join(done[d][:400] for d in s["depends_on"])
                    context = f"\n[선행 결과 참고]\n{prior}"

                exec_agent = s["agent"]
                # 신규 에이전트 필요 step → 설계문서 단계 C: 사람 승인 게이트.
                # 자동 생성 금지. 무슨 에이전트·무슨 목적인지 설명해 승인 요청만 한다.
                if exec_agent == "new" or not get_agent(exec_agent):
                    need = s.get("new_role") or s["task"]
                    spec = agent_factory.propose(need)
                    agent_factory.save_proposal(spec, need)
                    overlaps = spec.get("_overlaps") or []
                    expl = (
                        "\n🆕 [예원] 이 작업은 기존 에이전트가 못 맡아 새 에이전트가 필요합니다 — 승인 요청\n"
                        f"  • 이름: {spec.get('display')}\n"
                        f"  • 역할: {spec.get('role')}\n"
                        f"  • 목적(미해결 작업): {need[:120]}\n"
                        f"  • 담당 키워드: {', '.join(spec.get('keywords', [])) or '-'}"
                        + (f"\n  ⚠️ 기존 역할과 키워드 중복: {overlaps} (승인 시 거부될 수 있음)" if overlaps else "")
                        + "\n  → 만들려면 '에이전트 승인', 안 만들면 '에이전트 거절'이라고 답해줘요."
                    )
                    out_blocks.append(expl)
                    rec["steps"].append({"step": s["step"], "agent": "new",
                                         "proposed": spec.get("display"), "status": "awaiting_approval"})
                    done[s["step"]] = f"신규 에이전트 '{spec.get('display')}' 승인 대기"
                    pending.remove(s); progressed = True
                    continue

                result = run_agent(exec_agent, s["task"] + context)
                done[s["step"]] = result
                rec["steps"].append({"step": s["step"], "agent": exec_agent, "ok": "✅" in result[:60]})
                out_blocks.append(f"\n── step{s['step']} [{exec_agent}] ──\n{result}")
                pending.remove(s)
                progressed = True
        if not progressed:
            for s in pending:
                out_blocks.append(f"\n⚠️ step{s['step']} [{s['agent']}] 선행 미충족으로 건너뜀")
            break

    # 다중 단계면 최종 종합
    if len([k for k in done]) > 1 and llm_available():
        joined = "\n\n".join(f"step{k}:\n{v[:600]}" for k, v in done.items())
        summary = llm_text(
            f"다음은 '{message}' 지시에 대한 단계별 결과다. CEO 관점 3~4줄 최종 종합:\n\n{joined}",
            max_tokens=300, temperature=0.4, lm_first=False)
        if summary:
            out_blocks.append(f"\n🏁 [예원 최종 종합]\n{summary.strip()}")

    _save_queue(rec)
    return "\n".join(out_blocks)


def _cli() -> None:
    if len(sys.argv) < 2:
        print("사용법: python yewon_orchestrator.py \"지시 내용\"")
        return
    print(orchestrate(" ".join(sys.argv[1:])))


if __name__ == "__main__":
    _cli()
