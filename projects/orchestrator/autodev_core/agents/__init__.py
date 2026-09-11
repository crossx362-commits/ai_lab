"""Agent 어댑터. 어댑터 하나가 죽어도 오케스트레이터는 죽지 않는다(§3).

등록은 **type** 기준이다 — 같은 어댑터를 이름만 달리해 여러 벌 설정할 수 있게 하기 위해.
"""

from .codex import CodexAgent
from .script import ScriptAgent

REGISTRY = {
    "codex": CodexAgent,
    "script": ScriptAgent,
}


def build(cfg):
    cls = REGISTRY.get(cfg.type)
    if cls is None:
        raise KeyError(f"미지원 agent type: {cfg.type}")
    return cls(cfg)
