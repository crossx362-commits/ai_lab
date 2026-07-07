#!/usr/bin/env python3
"""지아 에이전트 진입점 (자동 생성).

표준 import 패턴 + 뮤텍스 락(좀비 방지) 적용.
기본 동작: LLM(_shared.llm) 기반 범용 처리 — 생성 즉시 동작한다.
역할 특화 로직이 필요하면 ROLE_SYSTEM 프롬프트/도구를 보강하면 된다.
"""
import os, sys
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

_here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(_here, "..", "..", ".."))

from _shared.env import load_env
from _shared.process import ProcessLock
from _shared.llm import text as llm_text, is_available as llm_available

load_env()
AGENT = "지아"
ROLE_SYSTEM = "당신은 '지아' 에이전트입니다. 역할: 시스템 손실 원인 분석 및 재발 방지 전략 제안. 한국어로 간결히 답하세요."


def main(message: str = "") -> str:
    msg = message or "(빈 입력)"
    if llm_available():
        out = llm_text(msg, system=ROLE_SYSTEM, max_tokens=600, temperature=0.5, lm_first=True)
        result = f"[{AGENT}] {out.strip()}" if out else f"[{AGENT}] (LLM 미응답) 수신: {msg}"
    else:
        result = f"[{AGENT}] (LLM 미가용) 수신: {msg} — 역할: 시스템 손실 원인 분석 및 재발 방지 전략 제안"
    print(result)
    return result


if __name__ == "__main__":
    with ProcessLock("지아_main"):
        main(" ".join(sys.argv[1:]))
