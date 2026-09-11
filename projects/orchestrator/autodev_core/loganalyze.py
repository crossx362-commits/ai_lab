"""로그 분석 — 오류를 "파일:줄 + 그 자리의 실제 코드"로 바꾼다.

긴 Unity 로그를 통째로 모델에 던지면 두 가지가 나빠진다: 비용이 붙고, 정작 봐야 할 줄이 묻힌다.
여기서는 **오류가 가리키는 자리를 열어서** 보여준다 — 모델이 파일을 다시 찾을 필요가 없게.

추측하지 않는다: 파일을 못 찾으면 못 찾았다고 적는다.
"""

from __future__ import annotations

import re
from dataclasses import dataclass, field
from pathlib import Path

# Unity/Roslyn 형식:  Assets/Game/Scripts/X.cs(12,9): error CS1525: ...
_ERR = re.compile(r"^(?P<file>[^\s(]+\.cs)\((?P<line>\d+),(?P<col>\d+)\):\s*error\s+(?P<code>CS\d+):\s*(?P<msg>.*)$")
# NUnit 실패:  Full.Test.Name :: message
_TEST = re.compile(r"^(?P<name>[\w.+<>`]+)\s*::\s*(?P<msg>.*)$")


@dataclass
class Finding:
    file: str | None
    line: int | None
    code: str
    message: str
    excerpt: str = ""

    def render(self) -> str:
        head = f"{self.code} {self.file or '(파일 미상)'}"
        if self.line:
            head += f":{self.line}"
        out = [f"- {head}\n  {self.message}"]
        if self.excerpt:
            out.append(self.excerpt)
        return "\n".join(out)


@dataclass
class Analysis:
    findings: list[Finding] = field(default_factory=list)
    unmapped: list[str] = field(default_factory=list)

    @property
    def files(self) -> list[str]:
        seen = []
        for f in self.findings:
            if f.file and f.file not in seen:
                seen.append(f.file)
        return seen

    def render(self, limit: int = 12) -> str:
        parts = []
        if self.findings:
            parts.append("[오류가 가리키는 자리]")
            parts += [f.render() for f in self.findings[:limit]]
            if len(self.findings) > limit:
                parts.append(f"... 외 {len(self.findings) - limit}건")
        if self.unmapped:
            parts.append("[파일을 특정하지 못한 오류]")
            parts += [f"- {u}" for u in self.unmapped[:limit]]
        return "\n".join(parts)


def _excerpt(root: Path, rel: str, line: int, span: int = 8) -> str:
    p = root / rel
    if not p.is_file():
        return ""
    try:
        lines = p.read_text(encoding="utf-8", errors="replace").splitlines()
    except OSError:
        return ""
    lo, hi = max(0, line - span), min(len(lines), line + span - 1)
    out = [f"  --- {rel}:{lo + 1}-{hi} ---"]
    for i in range(lo, hi):
        mark = ">>" if (i + 1) == line else "  "
        out.append(f"  {mark}{i + 1:5d}| {lines[i]}")
    return "\n".join(out)


def analyze(messages: list[str], worktree: Path) -> Analysis:
    """컴파일 오류·테스트 실패 문자열 목록을 분석한다."""
    a = Analysis()
    seen = set()
    for raw in messages:
        line = raw.strip()
        if not line or line in seen:
            continue
        seen.add(line)

        m = _ERR.search(line)
        if m:
            rel = m.group("file")
            # 로그는 프로젝트 상대 경로로 찍히지만 절대 경로일 때도 있다.
            if rel.startswith("/"):
                try:
                    rel = str(Path(rel).relative_to(worktree))
                except ValueError:
                    pass
            ln = int(m.group("line"))
            a.findings.append(Finding(rel, ln, m.group("code"), m.group("msg").strip(),
                                      _excerpt(worktree, rel, ln)))
            continue

        t = _TEST.search(line)
        if t:
            name = t.group("name")
            # 테스트 이름에서 파일을 역추적한다 — 클래스명과 파일명이 같은 관례를 이용하되,
            # 실제로 파일이 있을 때만 적는다(없으면 미상으로 남긴다).
            cls = name.split("::")[0].split("(")[0].strip().split(".")
            guess = None
            for part in reversed(cls):
                hits = list(worktree.glob(f"Assets/**/{part}.cs"))
                if hits:
                    guess = str(hits[0].relative_to(worktree))
                    break
            a.findings.append(Finding(guess, None, "TEST", f"{name} :: {t.group('msg')}"))
            continue

        a.unmapped.append(line)
    return a
