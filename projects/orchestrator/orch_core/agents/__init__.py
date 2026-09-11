"""Agent 어댑터. 어댑터 하나가 죽어도 오케스트레이터는 죽지 않는다(§3).

등록은 **type** 기준이다 — 같은 어댑터를 이름만 달리해 여러 벌 설정할 수 있게 하기 위해
(codex와 astra는 같은 어댑터에 추론 강도만 다르다).
"""

import os

from .base import AgentResult, CliAgent
from .claude import ClaudeAgent
from .codex import CodexAgent
from .grok import GrokAgent
from .script import ScriptAgent

REGISTRY = {
    "codex": CodexAgent,
    "claude": ClaudeAgent,
    "grok": GrokAgent,
    "script": ScriptAgent,
}


def build(cfg):
    cls = REGISTRY.get(cfg.type)
    if cls is None:
        raise KeyError(f"미지원 agent type: {cfg.type}")
    # 게이트를 시험하는 판이 실제 유료 모델을 부르는 사고를 구조적으로 막는다
    # (2026-09-11: --agent 기본값 탓에 사다리가 무시돼 codex가 226초 돌았다).
    # 문서 규칙이 아니라 코드로 막아야 재발하지 않는다.
    if os.getenv("ORCH_NO_CLOUD") and cls is not ScriptAgent:
        raise KeyError(f"ORCH_NO_CLOUD 상태에서 '{cfg.name}'(type={cfg.type})는 부를 수 없다 — script만 허용")
    return cls(cfg)
