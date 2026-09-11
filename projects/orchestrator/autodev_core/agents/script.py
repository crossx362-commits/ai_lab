"""테스트용 결정적 에이전트.

게이트를 시험하려면 **원하는 실패를 마음대로 만들 수 있는 가짜 개발자**가 필요하다.
모델을 부르면 매번 다르게 행동해서 게이트가 빨간불인지 확인할 수가 없다.
이 에이전트는 설정에 적힌 셸 명령을 worktree 안에서 그대로 실행한다(프롬프트는 stdin).

운영 경로가 아니다 — config에 `"type": "script"`로 명시해야만 쓰인다.
"""

from __future__ import annotations

import os
from pathlib import Path

from .base import CliAgent


class ScriptAgent(CliAgent):
    name = "script"
    stdin_prompt = True

    def argv(self, worktree: Path) -> list[str]:
        return [self.cfg.bin, *self.cfg.args]

    def model_name(self) -> str | None:
        return self.cfg.model or "script"

    def env(self) -> dict:
        e = super().env()
        e["AUTODEV_WORKTREE"] = "worktree"
        return e
