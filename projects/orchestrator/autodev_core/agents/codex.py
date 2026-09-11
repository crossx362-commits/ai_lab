"""Codex / Astra 어댑터.

Codex와 Astra는 이 기계에서 **모델명이 아니라 추론 강도**로 갈린다 —
오너 설정(`~/.codex/config.toml`)의 기본 모델이 이미 `gpt-6-astra`이고
`model_reasoning_effort = "low"`다. 그래서 config.json에서
`extra_config: ["model_reasoning_effort=high"]`를 준 것이 Astra 등급이다.

함정 대비: 프롬프트는 argv가 아니라 **stdin**으로 준다(`codex exec -`). 여러 줄 인자가 잘려
"지시가 전달 안 된 채 성공으로 기록"되는 사고가 있었다.
"""

from __future__ import annotations

from pathlib import Path

from .base import AgentResult, CliAgent, FEEDBACK_TEMPLATE, PROMPT_TEMPLATE  # re-export

__all__ = ["CodexAgent", "AgentResult", "PROMPT_TEMPLATE", "FEEDBACK_TEMPLATE"]


class CodexAgent(CliAgent):
    name = "codex"
    stdin_prompt = True

    def argv(self, worktree: Path) -> list[str]:
        cmd = [self.cfg.bin, "exec", "-C", str(worktree),
               "-s", self.cfg.sandbox_mode, "--color", "never"]
        if self.cfg.model:
            cmd += ["-m", self.cfg.model]
        for kv in self.cfg.extra_config:
            cmd += ["-c", kv]
        cmd.append("-")  # 프롬프트를 stdin에서 읽는다
        return cmd
