"""Git worktree 격리 + 변경 사실 수집.

원칙:
- Agent는 자기 worktree 밖을 건드리지 않는다.
- main/master 자동 병합 금지(§2-14). 이 모듈에는 merge/push/reset --hard/force 경로가 없다.
- diff는 merge-base 기준으로 잰다 — 사이클 도중 main이 앞서면 남의 커밋이 내 변경으로 보인다.
"""

from __future__ import annotations

import subprocess
import time
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
    deleted: list[str] = field(default_factory=list)
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


def is_clean(repo: Path, subdir: str | None = None) -> bool:
    """더러운지 본다. subdir을 주면 **그 폴더만** 본다 — 모노레포(ai_lab)는 다른 세션이 늘 무언가
    고치고 있어 전체가 깨끗한 순간이 없다. 보호할 것은 우리가 건드릴 폴더의 미커밋 변경이다."""
    args = ["status", "--porcelain"] + (["--", subdir] if subdir else [])
    return git(repo, *args).strip() == ""


def rev(repo: Path, ref: str = "HEAD") -> str:
    return git(repo, "rev-parse", ref).strip()


def branch_exists(repo: Path, name: str) -> bool:
    return git(repo, "rev-parse", "--verify", "--quiet", f"refs/heads/{name}", check=False).strip() != ""


def set_branch(repo: Path, name: str, commit: str) -> None:
    """브랜치를 커밋으로 옮긴다(계획 통합 브랜치 전진용). push/reset이 아니라 branch -f 다."""
    git(repo, "branch", "-f", name, commit)


def create_worktree(repo: Path, task_id: int, subdir: str | None = None,
                    base_ref: str = "HEAD") -> tuple[Path, str]:
    """작업용 worktree. subdir이 있으면 **sparse checkout으로 그 폴더만** 내려받는다 —
    모노레포 전체를 판마다 풀면 느리고, 남의 프로젝트가 작업 디렉터리에 같이 보이면
    AI가 거기까지 손을 댄다."""
    branch = f"orch/task-{task_id:04d}"
    wt = repo / ".orch" / "worktrees" / f"task_{task_id:04d}"
    wt.parent.mkdir(parents=True, exist_ok=True)
    if wt.exists():
        raise GitError(f"worktree 자리가 이미 있음: {wt}")
    # 동시 세션이 같은 저장소에서 `orch/task-NNNN`을 지우고 만들면 refs/heads/orch **디렉터리**가
    # 사라지는 찰나에 걸려 "unable to create directory for refs/heads/orch/..."로 죽는다
    # (2026-09-12 task 406, 내 clean 루프와 Codex의 새 작업이 겹쳤다). 한 번은 다시 해본다.
    for attempt in (1, 2):
        try:
            if subdir:
                git(repo, "worktree", "add", "--no-checkout", "-b", branch, str(wt), base_ref)
                git(wt, "sparse-checkout", "set", subdir)
                git(wt, "checkout")
                if not (wt / subdir).is_dir():
                    raise GitError(f"sparse checkout 뒤에 {subdir} 가 없다 — 추적된 파일이 없는 폴더인가")
            else:
                git(repo, "worktree", "add", "-b", branch, str(wt), base_ref)
            break
        except GitError as e:
            racy = "cannot lock ref" in str(e) or "unable to create directory" in str(e)
            if attempt == 2 or not racy:
                raise
            time.sleep(1.5)
            git(repo, "worktree", "prune", check=False)
    return wt, branch


def exclude_paths(inside: Path, paths: list[Path]) -> None:
    """이 worktree 전용 exclude(info/exclude)에 경로를 적는다 — 저장소 .gitignore는 건드리지 않는다.
    심볼릭 링크는 git에 파일이라 `폴더/` 규칙에 안 걸린다(2026-09-11 task 357)."""
    top = Path(git(inside, "rev-parse", "--show-toplevel").strip())
    excl = Path(git(inside, "rev-parse", "--git-path", "info/exclude").strip())
    if not excl.is_absolute():
        excl = top / excl
    excl.parent.mkdir(parents=True, exist_ok=True)
    with excl.open("a", encoding="utf-8") as f:
        for p in paths:
            f.write(f"/{p.relative_to(top).as_posix()}\n")


def link_local(wd: Path, src_dir: Path, rel_paths: list[str]) -> list[str]:
    """본 체크아웃에만 있는 로컬 자원(venv·캐시 같은 미추적 폴더)을 worktree에 링크로 심는다.
    울온의 SliceSelfCheck는 server/.venv 의 파이썬으로 persist 서버를 띄운다 — worktree엔 venv가
    없어 psycopg2 부재로 게이트가 죽었다(2026-09-11 task 360)."""
    notes = []
    linked = []
    for rel in rel_paths:
        src, dst = src_dir / rel, wd / rel
        if not src.exists():
            notes.append(f"링크 원본 없음(건너뜀): {src}")
            continue
        if dst.exists() or dst.is_symlink():
            continue
        dst.parent.mkdir(parents=True, exist_ok=True)
        dst.symlink_to(src, target_is_directory=src.is_dir())
        linked.append(dst)
        notes.append(f"로컬 자원 링크: {rel} → {src}")
    if linked:
        exclude_paths(wd, linked)
    return notes


def snapshot(wd: Path) -> set[str]:
    """지금 이 폴더의 변경 목록(추적 수정 + 미추적). 게이트 전후를 비교해 게이트의 부산물을 가려낸다."""
    return {ln[3:].strip() for ln in git(wd, "status", "--porcelain", "--untracked-files=all", "--", ".").splitlines()
            if ln.strip()}


def discard_extra(wd: Path, before: set[str]) -> list[str]:
    """게이트가 남긴 부산물을 되돌린다 — 울온 SliceSelfCheck는 씬·지형을 다시 저장한다(8파일 ±10만 줄).
    그것을 AI의 변경으로 세거나 커밋하면 판정도 이력도 오염된다. worktree 안, 게이트 뒤에 새로 생긴 것만."""
    out = []
    for ln in git(wd, "status", "--porcelain", "--untracked-files=all", "--", ".").splitlines():
        if not ln.strip():
            continue
        code, path = ln[:2], ln[3:].strip()
        if path in before:
            continue
        if path.endswith(".meta") and path[:-5] in before:
            # AI가 새로 만든 자산에 Unity가 붙인 .meta — 이건 부산물이 아니라 그 자산의 일부다(없으면 커밋이 깨진다).
            continue
        top = Path(git(wd, "rev-parse", "--show-toplevel").strip())
        full = top / path
        if code.strip() == "??":
            try:
                full.unlink()
                out.append(f"부산물 삭제: {path}")
            except OSError as e:
                out.append(f"부산물 삭제 실패: {path} ({e})")
        else:
            git(wd, "restore", "--source", "HEAD", "--staged", "--worktree", "--", full.as_posix(), check=False)
            out.append(f"부산물 되돌림: {path}")
    return out


def remove_worktree(repo: Path, wt: Path, branch: str | None = None) -> None:
    git(repo, "worktree", "remove", "--force", str(wt), check=False)
    if branch:
        git(repo, "branch", "-D", branch, check=False)


def list_worktrees(repo: Path) -> list[dict]:
    out, cur = [], {}
    for ln in git(repo, "worktree", "list", "--porcelain").splitlines():
        if not ln.strip():
            if cur:
                out.append(cur)
                cur = {}
            continue
        k, _, v = ln.partition(" ")
        cur[k] = v
    if cur:
        out.append(cur)
    return out


def gc(repo: Path, keep_paths: set[str]) -> list[str]:
    """등록만 남은 worktree와 DB가 모르는 잔재를 정리한다.

    잔재 worktree는 디스크만 먹는 게 아니다 — 다음 판이 그걸 자기 것으로 오인하면
    "누가 고쳤는지" 자체가 흐려진다.
    """
    notes = []
    git(repo, "worktree", "prune")
    notes.append("git worktree prune 실행")
    for wt in list_worktrees(repo):
        path = wt.get("worktree")
        if not path or path == str(repo):
            continue
        if path in keep_paths:
            continue
        if "/.orch/worktrees/" not in path:
            continue  # 우리가 만든 것만 손댄다
        notes.append(f"DB에 없는 잔재 worktree: {path}")
    return notes


def collect_changes(wt: Path, base: str) -> Changes:
    """추적/미추적 모두 센다 — 새 파일만 만들고 커밋 안 한 경우를 '변경 없음'으로 오판하지 않기 위해.

    `wt`는 **작업 디렉터리**다(모노레포면 worktree/subdir). 이 폴더 밑만 재고 경로는 이 폴더 상대로
    돌려준다(`--relative`) — allowed/protected 글롭이 target 폴더 기준으로 쓰이기 때문이다."""
    # 미추적 파일도 diff에 포함시키려면 일단 인덱스에 올린다(커밋은 별도).
    git(wt, "add", "-A", "--", ".")
    merge_base = git(wt, "merge-base", base, "HEAD").strip() if base else "HEAD"
    names = [
        ln for ln in git(wt, "diff", "--cached", "--relative", "--name-only", merge_base, "--", ".").splitlines()
        if ln.strip()
    ]
    numstat = git(wt, "diff", "--cached", "--relative", "--numstat", merge_base, "--", ".")
    ins = dele = 0
    for ln in numstat.splitlines():
        parts = ln.split("\t")
        if len(parts) >= 2:
            a, b = parts[0], parts[1]
            ins += int(a) if a.isdigit() else 0
            dele += int(b) if b.isdigit() else 0
    deleted = [
        ln for ln in git(wt, "diff", "--cached", "--relative", "--diff-filter=D", "--name-only",
                         merge_base, "--", ".").splitlines()
        if ln.strip()
    ]
    diff = git(wt, "diff", "--cached", "--relative", merge_base, "--", ".")
    # 작업 폴더 **밖**으로 새어나간 변경(모노레포에서 `../옆앱/파일`)도 센다 — NC(2026-09-11)에서
    # 밖에 쓴 것이 보이지 않아 PASS가 났다. 스테이지하지 않고 status로만 본다(커밋에 섞이면 안 된다).
    prefix = git(wt, "rev-parse", "--show-prefix").strip()
    if prefix:
        import os
        for ln in git(wt, "status", "--porcelain", "--untracked-files=all", "--", ":/").splitlines():
            path = ln[3:].strip()
            if path.startswith(prefix) or not path:
                continue
            names.append(os.path.relpath(path, prefix))   # "../other_app.txt" 꼴 — 글롭에 안 맞아 범위 밖으로 잡힌다
    return Changes(files=names, deleted=deleted, insertions=ins, deletions=dele, diff=diff)


def restore_tracked(wt: Path, ref: str = "HEAD") -> None:
    """추적 파일을 ref 상태로 되돌린다(스테이지·작업본 모두).

    `reset`은 이 모듈에서 금지돼 있으므로 `restore`만 쓴다. 새로 생긴 미추적 파일은 남긴다 —
    지우는 것은 되돌릴 수 없고, 여기서 필요한 건 "변조된 게이트 복구"뿐이다.
    """
    git(wt, "restore", "--source", ref, "--staged", "--worktree", "--", ".", check=False)


def commit(wt: Path, message: str) -> str | None:
    git(wt, "add", "-A", "--", ".")
    if not git(wt, "diff", "--cached", "--name-only", "--", ".").strip():
        return None
    git(wt, "commit", "-m", message)
    return head(wt)


def touches_protected(files: list[str], protected_globs: list[str]) -> list[str]:
    """검증 장치를 건드린 파일 목록.

    AI가 테스트·게이트를 지워서 PASS를 만드는 것을 금지한다(§2-16). 프롬프트로 부탁하는 것과
    코드로 막는 것은 다르다 — 부탁은 언젠가 무시된다.
    """
    import fnmatch

    return [f for f in files if any(fnmatch.fnmatch(f, g) for g in protected_globs)]


def violates_write_scope(files: list[str], allowed_globs: list[str]) -> list[str]:
    """허용 범위 밖 변경 파일 목록을 돌려준다(빈 리스트면 통과)."""
    import fnmatch

    bad = []
    for f in files:
        if not any(fnmatch.fnmatch(f, g) or fnmatch.fnmatch(f, g.rstrip("*") + "*") for g in allowed_globs):
            bad.append(f)
    return bad


def integrate(repo: Path, branch: str, commit: str, subdir: str | None = None) -> tuple[bool, str]:
    """계획 통합 브랜치에 task 커밋 하나를 올린다 — 병렬로 끝난 판들이 **한 줄로** 쌓이게.

    · 통합 브랜치 끝이 그 커밋의 부모면 ref만 옮긴다(빨리감기).
    · 아니면(같은 물결의 다른 판이 먼저 올라갔다) 전용 worktree에서 cherry-pick으로 얹는다.
      충돌하면 **추측해서 합치지 않는다** — 되돌리고 실패를 돌려준다. 그 Task는 새 끝 위에서 다시 한다.
    merge는 이 프로그램에서 금지다(FORBIDDEN). 여기서도 쓰지 않는다.
    통합 worktree는 **항상 분리된 HEAD**로 둔다 — 브랜치를 물고 있으면 `branch -f`가 거부된다
    (2026-09-12 NC parallel에서 실측: 물결 2가 통째로 죽었다).
    """
    tip = rev(repo, branch)
    parent = git(repo, "rev-parse", f"{commit}^").strip()
    if parent == tip:
        git(repo, "update-ref", f"refs/heads/{branch}", commit, tip)
        return True, f"빨리감기 {tip[:8]} → {commit[:8]}"
    wt = repo / ".orch" / "integrate"
    try:
        if not wt.exists():
            if subdir:
                git(repo, "worktree", "add", "--no-checkout", "--detach", str(wt), tip)
                git(wt, "sparse-checkout", "set", subdir)
                git(wt, "checkout")
            else:
                git(repo, "worktree", "add", "--detach", str(wt), tip)
        git(wt, "cherry-pick", "--quit", check=False)   # 지난 판이 남긴 중간 상태를 먼저 치운다
        git(wt, "checkout", "--detach", "-f", tip)
        r = subprocess.run(["git", "-C", str(wt), "cherry-pick", commit],
                           capture_output=True, text=True, encoding="utf-8", errors="replace")
        out = (r.stdout or "") + (r.stderr or "")
        if r.returncode == 0:
            new = rev(wt, "HEAD")
            git(repo, "update-ref", f"refs/heads/{branch}", new, tip)
            return True, f"cherry-pick {commit[:8]} → {new[:8]}"
        if "empty" in out:
            # 다른 판이 같은 내용을 이미 올렸다. 빈 커밋을 만들지 않는다 — 실패도 아니다.
            git(wt, "cherry-pick", "--quit", check=False)
            return True, f"이미 같은 내용이 올라가 있다({commit[:8]}) — 건너뜀"
        conflicted = git(wt, "diff", "--name-only", "--diff-filter=U", check=False).strip()
        git(wt, "cherry-pick", "--abort", check=False)
        git(wt, "cherry-pick", "--quit", check=False)
        return False, f"통합 충돌 — 새 끝 위에서 다시 해야 한다: {conflicted[:200] or out.strip()[:120]}"
    except GitError as e:
        return False, f"통합 실패: {e}"
