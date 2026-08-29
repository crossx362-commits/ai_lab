#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""LLM 생존 프로브.

기본은 로컬 Ollama만 실제 챗 호출한다.
클라우드 구독/할당량은 생존 확인만으로 태우지 않는다.
정말 클라우드 경로까지 점검해야 할 때만 AI_TEAM_PROBE_CLOUD=1을 명시한다.
"""
import os
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, "_shared")):
        break
    _root = os.path.dirname(_root)
if _root not in sys.path:
    sys.path.insert(0, _root)

from _shared.env import load_env
from _shared.llm import ollama, grok_build, gpt_codex, gemini
from _shared.telegram import send

load_env()
PROMPT = "상태 점검입니다. '정상' 한 단어로만 답하세요."


def _probe(fn):
    try:
        out = (fn(PROMPT) or "").strip()
        return (True, "") if out else (False, "빈 응답")
    except Exception as e:
        return False, str(e)[:120]


def _env_on(name: str) -> bool:
    return os.getenv(name, "0").strip().lower() in {"1", "true", "yes", "on"}


def run() -> list[str]:
    fails = []
    ok, why = _probe(ollama)
    if not ok:
        fails.append(f"로컬(Ollama) 챗 실패: {why} — `ollama show`로 확인")

    # 기본 OFF. 상태 점검 때문에 Grok/Codex/Gemini 한도를 소모하지 않는다.
    if _env_on("AI_TEAM_PROBE_CLOUD"):
        for name, fn in (("Grok", grok_build), ("Codex", gpt_codex), ("Gemini", gemini)):
            ok, why = _probe(fn)
            if not ok:
                fails.append(f"클라우드({name}) 실패: {why}")
    return fails


if __name__ == "__main__":
    failures = run()
    if failures:
        msg = "🚨 [LLM프로브] 점검 실패\n" + "\n".join("- " + f for f in failures)
        print(msg)
        send(msg)
    else:
        mode = "로컬+클라우드" if _env_on("AI_TEAM_PROBE_CLOUD") else "로컬만(클라우드 호출 0)"
        print(f"llm_probe: 정상 — {mode}")
