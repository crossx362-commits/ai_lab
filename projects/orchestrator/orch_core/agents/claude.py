"""Claude Code 어댑터 — 고난도 구현·여러 파일에 걸친 변경 담당.

`claude -p`(print 모드)는 stdin으로 프롬프트를 받는다. 비대화형이므로 권한 프롬프트가 뜨면
그대로 멈춘다 — `--permission-mode acceptEdits`로 편집을 허용하고
`--permission-prompts none`으로 "물어볼 사람이 없다"를 명시한다.
"""

from __future__ import annotations

from pathlib import Path

from .base import CliAgent


class ClaudeAgent(CliAgent):
    name = "claude"
    stdin_prompt = True

    def argv(self, worktree: Path) -> list[str]:
        cmd = [self.cfg.bin, "-p",
               "--permission-mode", self.cfg.permission_mode,
               "--permission-prompts", "none",
               "--add-dir", str(worktree),
               "--output-format", "text"]
        if self.cfg.model:
            cmd += ["--model", self.cfg.model]
        return cmd
