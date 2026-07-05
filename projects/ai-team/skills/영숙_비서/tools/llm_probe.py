#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""LLM 생존 프로브 — 장전에 로컬·클라우드 LLM을 실제 챗 호출로 점검.

배경(가드레일 2026-07-03): Ollama가 '모델 목록엔 보이지만' 매니페스트 파손·thinking
누수로 빈 응답을 뱉으며 조용히 죽어 있었고, 클라우드 429(크레딧 소진)와 겹치면
issue_impact가 통째로 유실됐다. 판정 기준은 '모델 존재'가 아니라 '챗 응답 성공'.

동작: 로컬(ollama)·클라우드(gpt) 각 1회 초소형 챗 → 실패한 쪽만 텔레그램 경보.
정상이면 무발송(스팸 방지). 스케줄: 평일 07:25 (조사 파이프라인 07:30 직전).
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
from _shared.llm import ollama, claude, gemini
from _shared.notify import send

load_env()

PROMPT = "상태 점검입니다. '정상' 한 단어로만 답하세요."


def _probe(name, fn):
    try:
        out = (fn(PROMPT) or "").strip()
        return (True, "") if out else (False, "빈 응답")
    except Exception as e:
        return False, str(e)[:120]


def run() -> list[str]:
    fails = []
    ok, why = _probe("ollama", ollama)
    if not ok:
        fails.append(f"로컬(Ollama) 챗 실패: {why} — `ollama show`로 매니페스트 확인")
    # GPT는 기본 체인에서 제거(오너 지시 2026-07-05) — 클로드가 클라우드 1선
    ok, why = _probe("claude", claude)
    if not ok:
        fails.append(f"클라우드(Claude) 실패: {why} — credit balance면 Anthropic 콘솔 충전")
    ok, why = _probe("gemini", gemini)
    if not ok:
        fails.append(f"클라우드(Gemini) 실패: {why}")
    return fails


if __name__ == "__main__":
    failures = run()
    if failures:
        msg = "🚨 [LLM프로브] 장전 점검 실패\n" + "\n".join("- " + f for f in failures)
        if len(failures) == 3:
            msg += "\n⚠️ 로컬·클라우드 동시 다운 = issue_impact 유실 위험(가드레일 7/3)"
        print(msg)
        send(msg)
    else:
        print("llm_probe: 로컬·클라우드 모두 정상")
