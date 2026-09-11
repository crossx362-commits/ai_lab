"""Grok 어댑터 — 조사·제2 의견 담당(기본), 필요하면 구현도 가능.

`grok -p <프롬프트>`는 프롬프트를 **argv로** 받는다(stdin 모드가 없다). macOS에서 우리는
셸을 거치지 않고 리스트 argv로 exec하므로 여러 줄이 잘리지 않는다 —
다만 "CLI마다 전달 방식이 다르다"는 사실 자체가 과거 사고의 원인이었으므로 여기 적어 둔다.
전달이 실제로 됐는지는 결국 **파일이 바뀌었는가**로 판정한다(오케스트레이터가 git으로 본다).
"""

from __future__ import annotations

from pathlib import Path

from .base import CliAgent


class GrokAgent(CliAgent):
    name = "grok"
    stdin_prompt = False  # 프롬프트가 argv 끝에 붙는다

    def argv(self, worktree: Path) -> list[str]:
        cmd = [self.cfg.bin, "--cwd", str(worktree), "--always-approve"]
        if self.cfg.model:
            cmd += ["--model", self.cfg.model]
        cmd += ["-p"]
        return cmd
