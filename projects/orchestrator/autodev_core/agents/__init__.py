"""Agent 어댑터. 어댑터 하나가 죽어도 오케스트레이터는 죽지 않는다(§3)."""

from .codex import CodexAgent

REGISTRY = {"codex": CodexAgent}
