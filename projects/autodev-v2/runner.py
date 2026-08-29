#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AutoDev v2 실행 래퍼.

- Director는 로컬 Ollama를 먼저 사용한다.
- 로컬 Director가 유효한 작업 JSON을 만들지 못할 때만 기존 Grok Director로 fallback한다.
- Worker/검증/큐 로직은 autodev.py 원본을 사용한다.
- BLOCKED 작업은 기록하되 독립 작업까지 전체 루프를 멈추지 않는다.
- 로컬 Director 호출은 클라우드 호출 예산으로 계산하지 않는다.
"""
from __future__ import annotations

import importlib.util
import os
from pathlib import Path
from typing import Any

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]


def load_module(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"모듈 로드 실패: {path}")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


AUTODEV = load_module("autodev_v2_core", HERE / "autodev.py")
LLM = load_module("autodev_v2_shared_llm", REPO / "projects/ai-team/_shared/llm.py")
ORIGINAL_DIRECTOR = AUTODEV.director_fill


def director_prompt(cfg: dict[str, Any], st: dict[str, Any]) -> str:
    return f"""당신은 '재와 별' AutoDev v2의 DIRECTOR다.
코드를 수정하지 말고 다음 개발 작업 묶음만 결정한다.

목표:
- 커밋 수나 문서 수가 아니라 실제 플레이 가능한 기능을 앞으로 진행한다.
- 작은 장식/리팩터링/상태문서보다 핵심 게임 루프를 우선한다.
- 이미 완료한 작업을 다시 만들지 않는다.
- 막힌 작업과 같은 접근을 반복하지 말고, 가능한 독립 작업이나 우회 작업을 우선한다.
- 한 번에 서로 이어지는 4~{cfg['max_tasks_per_director_batch']}개 작업만 만든다.
- 각 작업은 개발자가 한 세션 안에 구현하고 검증 가능한 크기여야 한다.
- 마지막 작업은 가능하면 수직 슬라이스 통합 검증으로 두고 milestone=true.
- 불필요한 회의, 문서 갱신, 전체 아트 통일 작업은 만들지 않는다.

[기획서 압축본]
{AUTODEV.compact_design(cfg)}

[안정 지식]
{AUTODEV.compact_handoff(cfg)}

[현재 상태]
{AUTODEV.situation(cfg, st)}

JSON만 출력:
{{
  "goal": "이번 묶음의 한 줄 목표",
  "tasks": [
    {{
      "title": "작업명",
      "goal": "무엇을 구현하는지",
      "done_when": ["검증 가능한 조건1", "조건2"],
      "priority": 1,
      "depends_on": [1],
      "verify_mode": "compile 또는 build",
      "milestone": false
    }}
  ]
}}
"""


def local_director_enabled() -> bool:
    return os.environ.get("AUTODEV_LOCAL_DIRECTOR", "1").strip().lower() in {"1", "true", "yes", "on"}


def call_local_director(prompt: str) -> str | None:
    """공용 LLM 모듈을 로컬 전용 모드로 잠깐 실행한다. 클라우드 fallback은 금지한다."""
    old_cloud = os.environ.get("AI_TEAM_ALLOW_CLOUD_LLM")
    old_primary = os.environ.get("AI_TEAM_LLM_PRIMARY")
    try:
        os.environ["AI_TEAM_ALLOW_CLOUD_LLM"] = "0"
        os.environ["AI_TEAM_LLM_PRIMARY"] = "ollama"
        return LLM.text(
            prompt,
            max_tokens=2600,
            temperature=0.2,
            json_mode=True,
            task="coding",
            lm_first=True,
        )
    finally:
        if old_cloud is None:
            os.environ.pop("AI_TEAM_ALLOW_CLOUD_LLM", None)
        else:
            os.environ["AI_TEAM_ALLOW_CLOUD_LLM"] = old_cloud
        if old_primary is None:
            os.environ.pop("AI_TEAM_LLM_PRIMARY", None)
        else:
            os.environ["AI_TEAM_LLM_PRIMARY"] = old_primary


def apply_director_result(cfg: dict[str, Any], st: dict[str, Any], out: str) -> bool:
    parsed = AUTODEV.extract_json(out)
    tasks = AUTODEV.normalize_director_tasks(cfg, st, parsed)
    if not tasks:
        return False
    if isinstance(parsed, dict) and parsed.get("goal"):
        st["goal"] = str(parsed["goal"])[:500]
    st["tasks"].extend(tasks)
    st["last_director_at"] = AUTODEV.now_iso()
    st["last_director_provider"] = "ollama"
    stats = st.setdefault("stats", {})
    stats["director_local_calls"] = int(stats.get("director_local_calls", 0)) + 1
    print(f"[DIRECTOR:LOCAL] 새 작업 {len(tasks)}개 생성: " + ", ".join(t["id"] for t in tasks))
    print("[DIRECTOR:LOCAL] Grok 주간 사용량 사용 안 함")
    return True


def director_fill(cfg: dict[str, Any], st: dict[str, Any]) -> bool:
    if local_director_enabled():
        prompt = director_prompt(cfg, st)
        out = call_local_director(prompt)
        if out and apply_director_result(cfg, st, out):
            return True
        print("[DIRECTOR:LOCAL] 사용 가능한 로컬 결과가 없어 Grok Director로 fallback")
    st["last_director_provider"] = "grok"
    return ORIGINAL_DIRECTOR(cfg, st)


def _cascade_blocked_dependencies(st: dict[str, Any]) -> int:
    """이미 BLOCKED 된 작업만을 의존하는 후속 작업을 명시적으로 BLOCKED 처리한다.

    이 작업을 하지 않으면 next_ready()가 None인 채 pending 작업만 남아
    자율 루프가 의존성 교착으로 종료될 수 있다.
    """
    blocked_ids = {str(x.get("id")) for x in st.get("blocked", []) if isinstance(x, dict) and x.get("id")}
    if not blocked_ids:
        return 0
    victims: list[tuple[dict[str, Any], list[str]]] = []
    for task in list(st.get("tasks", [])):
        if not isinstance(task, dict) or task.get("status") != "pending":
            continue
        bad = [str(dep) for dep in task.get("depends_on", []) if str(dep) in blocked_ids]
        if bad:
            victims.append((task, bad))
    for task, bad in victims:
        AUTODEV.block_task(
            st,
            task,
            "선행 작업이 BLOCKED 되어 자동 진행할 수 없음: " + ", ".join(bad),
        )
    return len(victims)


def run_loop(cfg: dict[str, Any], continuous: bool) -> int:
    """BLOCKED를 격리하면서 가능한 작업을 예산 상한까지 계속 수행한다."""
    st = AUTODEV.load_state(cfg)
    run_stats = {"cloud_calls": 0, "tasks": 0}
    max_tasks = int(cfg["max_tasks_per_run"])
    max_cloud = int(cfg["max_cloud_calls_per_run"])

    while True:
        AUTODEV.print_status(cfg, st)

        task = AUTODEV.next_ready(st)
        if not task:
            cascaded = _cascade_blocked_dependencies(st)
            if cascaded:
                AUTODEV.save_state(cfg, st)
                print(f"[LOOP] BLOCKED 선행 작업에 묶인 후속 작업 {cascaded}개를 격리했습니다.")
                continue

            pending = [t for t in st.get("tasks", []) if isinstance(t, dict) and t.get("status") == "pending"]
            if pending:
                ids = ", ".join(str(t.get("id", "?")) for t in pending)
                print(f"실행 가능한 작업이 없습니다. 의존성 교착 가능성: {ids}")
                return 3

            if run_stats["cloud_calls"] >= max_cloud:
                print("이번 실행의 클라우드 호출 예산을 모두 사용했습니다.")
                return 0

            grok_before = int(st.get("stats", {}).get("grok_calls", 0) or 0)
            codex_before = int(st.get("stats", {}).get("codex_calls", 0) or 0)
            if not director_fill(cfg, st):
                AUTODEV.save_state(cfg, st)
                return 4
            grok_after = int(st.get("stats", {}).get("grok_calls", 0) or 0)
            codex_after = int(st.get("stats", {}).get("codex_calls", 0) or 0)
            cloud_delta = max(0, grok_after - grok_before) + max(0, codex_after - codex_before)
            run_stats["cloud_calls"] += cloud_delta
            AUTODEV.save_state(cfg, st)
            task = AUTODEV.next_ready(st)
            if not task:
                return 4

        ok = AUTODEV.execute_one(cfg, st, task, run_stats)
        AUTODEV.save_state(cfg, st)
        run_stats["tasks"] += 1

        if not continuous:
            return 0 if ok else 2
        if run_stats["tasks"] >= max_tasks:
            print(f"이번 실행의 작업 상한({max_tasks})에 도달했습니다. 무한 소모 방지를 위해 종료합니다.")
            return 0
        if run_stats["cloud_calls"] >= max_cloud:
            print("이번 실행의 클라우드 호출 예산에 도달했습니다. 종료합니다.")
            return 0
        if not ok:
            print("[LOOP] 작업 하나가 BLOCKED 되었지만 다른 실행 가능한 작업을 계속 찾습니다.")


def main() -> int:
    AUTODEV.director_fill = director_fill
    AUTODEV.run_loop = run_loop
    return AUTODEV.main()


if __name__ == "__main__":
    raise SystemExit(main())
