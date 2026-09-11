"""Git worktree 격리 + 변경 사실 수집.

원칙:
- Agent는 자기 worktree 밖을 건드리지 않는다.
- main/master 자동 병합 금지(§2-14). 이 모듈에는 merge/push/reset --hard/force 경로가 없다.
- diff는 merge-base 기준으로 잰다 — 사이클 도중 main이 앞서면 남의 커밋이 내 변경으로 보인다.
"""

from __future__ import annotations

import subprocess
from dataclasses import dataclass, field
from pathlib import Path

FORBIDDEN = ("push", "reset", "clean", "rebase", "merge", "filter-branch")


class GitError(RuntimeError):
    pass


def git(repo: Path, *args: str, check: bool = True) -> str:
    if args and args[0] in FORBIDDEN:
        # 파괴적 명령은 이 프로그램에서 아예 못 나가게 막는다(§2-15).
        raise GitError(f"금지된 git 명령: {args[0]}")
    p = subprocess.run(
        ["git", "-C", str(repo), *args],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if check and p.returncode != 0:
        raise GitError(f"git {' '.join(args)} 실패({p.returncode}): {p.stderr.strip()}")
    return p.stdout


@dataclass
class Changes:
    files: list[str] = field(default_factory=list)
    insertions: int = 0
    deletions: int = 0
    diff: str = ""

    @property
    def changed(self) -> bool:
        return bool(self.files)

    @property
    def lines(self) -> int:
        return self.insertions + self.deletions


def is_repo(path: Path) -> bool:
    try:
        git(path, "rev-parse", "--git-dir")
        return True
    except GitError:
        return False


def head(repo: Path) -> str:
    return git(repo, "rev-parse", "HEAD").strip()


def current_branch(repo: Path) -> str:
    return git(repo, "rev-parse", "--abbrev-ref", "HEAD").strip()


def is_clean(repo: Path) -> bool:
    return git(repo, "status", "--porcelain").strip() == ""


def create_worktree(repo: Path, task_id: int) -> tuple[Path, str]:
    branch = f"autodev/task-{task_id:04d}"
    wt = repo / ".autodev" / "worktrees" / f"task_{task_id:04d}"
    wt.parent.mkdir(parents=True, exist_ok=True)
    if wt.exists():
        raise GitError(f"worktree 자리가 이미 있음: {wt}")
    git(repo, "worktree", "add", "-b", branch, str(wt), "HEAD")
    return wt, branch


def remove_worktree(repo: Path, wt: Path, branch: str | None = None) -> None:
    git(repo, "worktree", "remove", "--force", str(wt), check=False)
    if branch:
        git(repo, "branch", "-D", branch, check=False)


def collect_changes(wt: Path, base: str) -> Changes:
    """추적/미추적 모두 센다 — 새 파일만 만들고 커밋 안 한 경우를 '변경 없음'으로 오판하지 않기 위해."""
    # 미추적 파일도 diff에 포함시키려면 일단 인덱스에 올린다(커밋은 별도).
    git(wt, "add", "-A")
    merge_base = git(wt, "merge-base", base, "HEAD").strip() if base else "HEAD"
    names = [
        ln for ln in git(wt, "diff", "--cached", "--name-only", merge_base).splitlines() if ln.strip()
    ]
    numstat = git(wt, "diff", "--cached", "--numstat", merge_base)
    ins = dele = 0
    for ln in numstat.splitlines():
        parts = ln.split("\t")
        if len(parts) >= 2:
            a, b = parts[0], parts[1]
            ins += int(a) if a.isdigit() else 0
            dele += int(b) if b.isdigit() else 0
    diff = git(wt, "diff", "--cached", merge_base)
    return Changes(files=names, insertions=ins, deletions=dele, diff=diff)


def reset_worktree_to_base(wt: Path, base: str) -> None:
    """실패한 시도를 되돌린다. reset 금지 규칙을 우회하지 않도록 checkout으로만 되돌린다."""
    git(wt, "checkout", "--", ".", check=False)


def commit(wt: Path, message: str) -> str | None:
    git(wt, "add", "-A")
    if not git(wt, "diff", "--cached", "--name-only").strip():
        return None
    git(wt, "commit", "-m", message)
    return head(wt)


def violates_write_scope(files: list[str], allowed_globs: list[str]) -> list[str]:
    """허용 범위 밖 변경 파일 목록을 돌려준다(빈 리스트면 통과)."""
    import fnmatch

    bad = []
    for f in files:
        if not any(fnmatch.fnmatch(f, g) or fnmatch.fnmatch(f, g.rstrip("*") + "*") for g in allowed_globs):
            bad.append(f)
    return bad
