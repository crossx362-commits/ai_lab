#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AutoDev v2 실행 래퍼.

- Director는 로컬 Ollama를 먼저 사용한다.
- 로컬 Director가 유효한 작업 JSON을 만들지 못할 때만 기존 Grok Director로 fallback한다.
- Worker/검증/큐 로직은 autodev.py 원본을 그대로 사용한다.

이렇게 분리하면 '다음 할 일 결정'에 Grok 주간 한도를 쓰지 않으면서도
실제 구현은 기존 Grok Worker 경로를 유지할 수 있다.
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
    return ORIGINAL_DIRECTOR(cfg, st)


def main() -> int:
    AUTODEV.director_fill = director_fill
    return AUTODEV.main()


if __name__ == "__main__":
    raise SystemExit(main())
