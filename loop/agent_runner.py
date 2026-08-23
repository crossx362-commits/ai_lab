#!/usr/bin/env python3
"""한 바퀴만 실행하는 경량 다중-AI 코디네이터.

대화 세션은 매 호출 새로 만들고, 병렬 작업은 서로 다른 Git worktree에서만
실행한다. 승인된 후보는 master가 아니라 autonomous/integration에 합친다.
"""

from __future__ import annotations

import argparse
from concurrent.futures import ThreadPoolExecutor
import fcntl
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import sys
import tempfile
from typing import NamedTuple


if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")


PROVIDERS = ("opencode", "claude", "codex", "grok")
BIN_ENV = {
    "opencode": "LOOP_OPENCODE_BIN",
    "claude": "LOOP_CLAUDE_BIN",
    "codex": "LOOP_CODEX_BIN",
    "grok": "LOOP_GROK_BIN",
}
BIN_FALLBACKS = {
    "opencode": (str(Path.home() / ".opencode/bin/opencode"), "/usr/local/bin/opencode"),
    "claude": ("/opt/homebrew/bin/claude", "/usr/local/bin/claude"),
    "codex": ("/opt/homebrew/bin/codex", "/usr/local/bin/codex"),
    "grok": (str(Path.home() / ".grok/bin/grok"), "/usr/local/bin/grok"),
}
SAFE_ID = re.compile(r"^[a-z0-9][a-z0-9._-]{0,63}$")


class Assignment(NamedTuple):
    task: dict[str, object]
    worker: str
    reviewer: str


class Candidate(NamedTuple):
    assignment: Assignment
    branch: str
    worktree: Path
    base_sha: str
    head_sha: str | None
    changed_files: tuple[str, ...]
    worker_ok: bool
    worker_log: str


class ReviewedCandidate(NamedTuple):
    candidate: Candidate
    approved: bool
    review: dict[str, object]
    review_log: str


def _path_parts(raw: str) -> tuple[str, ...]:
    if not isinstance(raw, str) or not raw.strip():
        raise ValueError("write path is empty")
    if "\\" in raw or "\x00" in raw or raw.startswith("/"):
        raise ValueError(f"unsafe write path: {raw!r}")
    path = PurePosixPath(raw)
    if any(part in ("", ".", "..") for part in path.parts):
        raise ValueError(f"unsafe write path: {raw!r}")
    if any(part.casefold() == ".git" for part in path.parts):
        raise ValueError(f"unsafe write path: {raw!r}")
    return tuple(part.casefold() for part in path.parts)


def _paths_overlap(left: str, right: str) -> bool:
    a = _path_parts(left)
    b = _path_parts(right)
    return a[: len(b)] == b or b[: len(a)] == a


def select_independent_tasks(
    tasks: list[dict[str, object]], limit: int
) -> tuple[list[dict[str, object]], list[dict[str, object]]]:
    if not 1 <= limit <= 3:
        raise ValueError("parallel limit must be 1..3")
    selected: list[dict[str, object]] = []
    deferred: list[dict[str, object]] = []
    for task in sorted(tasks, key=lambda item: str(item["id"])):
        paths = [str(value) for value in task["write_paths"]]  # type: ignore[index]
        conflict = any(
            _paths_overlap(path, other)
            for chosen in selected
            for path in paths
            for other in chosen["write_paths"]  # type: ignore[index]
        )
        if not conflict and len(selected) < limit:
            selected.append(task)
        else:
            deferred.append(task)
    return selected, deferred


def assign_providers(
    tasks: list[dict[str, object]], available: list[str]
) -> list[Assignment]:
    usable = [provider for provider in PROVIDERS if provider in available]
    if not usable:
        raise RuntimeError("사용 가능한 강한 provider가 없다")
    return [
        Assignment(task, usable[index % len(usable)], usable[(index + 1) % len(usable)])
        for index, task in enumerate(tasks)
    ]


def _git_common_dir(cwd: Path) -> Path | None:
    result = subprocess.run(
        ["git", "rev-parse", "--git-common-dir"],
        cwd=str(cwd), capture_output=True, text=True, encoding="utf-8", check=False,
    )
    if result.returncode != 0 or not result.stdout.strip():
        return None
    path = Path(result.stdout.strip())
    return path.resolve() if path.is_absolute() else (cwd / path).resolve()


def build_provider_command(
    provider: str,
    prompt: str,
    max_turns: int,
    role: str,
    cwd: Path | None = None,
) -> list[str]:
    if provider == "opencode":
        # 권한·MCP는 저장소 opencode.json이 결정한다. reviewer도 같은 실행기다.
        return [
            "opencode", "run",
            "--model", os.environ.get("LOOP_OPENCODE_MODEL", "opencode/x-preview-f-free"),
            prompt,
        ]
    if provider == "claude":
        permission = "acceptEdits" if role == "worker" else "plan"
        return [
            "claude", "-p", prompt,
            "--model", os.environ.get("LOOP_CLAUDE_MODEL", "fable"),
            "--fallback-model", os.environ.get("LOOP_CLAUDE_FALLBACK_MODEL", "opus5"),
            "--no-session-persistence",
            "--permission-mode", permission,
            "--output-format", "json",
        ]
    if provider == "codex":
        sandbox = "workspace-write" if role == "worker" else "read-only"
        reasoning_env = "LOOP_CODEX_REASONING" if role == "worker" else "LOOP_CODEX_PLANNING_REASONING"
        reasoning_default = "xhigh" if role == "worker" else "medium"
        command = [
            "codex", "exec", "--ephemeral",
            "--ignore-user-config",
            "--model", os.environ.get("LOOP_CODEX_MODEL", "gpt-5.6-sol"),
            "--sandbox", sandbox,
        ]
        if role == "worker" and cwd is not None:
            common_dir = _git_common_dir(cwd)
            if common_dir is not None:
                command.extend(["--add-dir", str(common_dir)])
        command.extend([
            "--json",
            "-c", f'model_reasoning_effort="{os.environ.get(reasoning_env, reasoning_default)}"',
            "-",
        ])
        return command
    if provider == "grok":
        return [
            "grok",
            "--model", os.environ.get("LOOP_GROK_MODEL", "grok-4.6"),
            "--no-memory",
            "--no-subagents",
            "--max-turns", str(max_turns),
            "--permission-mode", "auto",
            "--output-format", "streaming-json",
            "-p", prompt,
        ]
    raise ValueError(f"unknown provider: {provider}")


def find_provider_binary(provider: str) -> str | None:
    configured = os.environ.get(BIN_ENV[provider], "").strip()
    candidates = (configured, shutil.which(provider) or "", *BIN_FALLBACKS[provider])
    for candidate in candidates:
        if candidate and Path(candidate).is_file() and os.access(candidate, os.X_OK):
            return str(Path(candidate).resolve())
    return None


def available_providers() -> list[str]:
    requested = [
        value.strip().lower()
        for value in os.environ.get("LOOP_PROVIDERS", "claude,codex,grok").split(",")
        if value.strip()
    ]
    unknown = sorted(set(requested) - set(PROVIDERS))
    if unknown:
        raise ValueError(f"unknown providers: {', '.join(unknown)}")
    return [provider for provider in PROVIDERS if provider in requested and find_provider_binary(provider)]


def _child_env() -> dict[str, str]:
    env = dict(os.environ)
    for key in (
        "ANTHROPIC_API_KEY", "OPENAI_API_KEY", "XAI_API_KEY",
        "CLAUDE_CODE_USE_BEDROCK", "CLAUDE_CODE_USE_VERTEX",
    ):
        env.pop(key, None)
    return env


def run_provider(
    provider: str,
    prompt: str,
    cwd: Path,
    log_path: Path,
    max_turns: int,
    timeout_s: int,
    role: str,
) -> tuple[int, str]:
    command = build_provider_command(provider, prompt, max_turns, role, cwd=cwd)
    executable = find_provider_binary(provider)
    if not executable:
        return 127, f"{provider} executable not found"
    command[0] = executable
    stdin = prompt if provider == "codex" else None
    try:
        result = subprocess.run(
            command,
            cwd=str(cwd),
            input=stdin,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=timeout_s,
            env=_child_env(),
            check=False,
        )
        output = (result.stdout or "") + (result.stderr or "")
        rc = result.returncode
    except subprocess.TimeoutExpired as exc:
        output = (exc.stdout or "") + (exc.stderr or "") + f"\nTIMEOUT {timeout_s}s\n"
        rc = 124
    log_path.parent.mkdir(parents=True, exist_ok=True)
    log_path.write_text(output, encoding="utf-8")
    return rc, output


def parse_json_payload(text: str) -> dict[str, object]:
    fenced = re.findall(r"```(?:json)?\s*(\{.*?\})\s*```", text, flags=re.DOTALL)
    candidates = fenced + [line.strip() for line in text.splitlines() if line.strip().startswith("{")]
    decoder = json.JSONDecoder()
    for start, char in enumerate(text):
        if char == "{":
            try:
                value, _ = decoder.raw_decode(text[start:])
                if isinstance(value, dict):
                    candidates.append(json.dumps(value))
            except json.JSONDecodeError:
                pass
    decoded: list[dict[str, object]] = []

    def collect(value: object) -> None:
        if isinstance(value, dict):
            decoded.append(value)
            for child in value.values():
                collect(child)
        elif isinstance(value, list):
            for child in value:
                collect(child)
        elif isinstance(value, str) and "{" in value:
            try:
                child, _ = decoder.raw_decode(value[value.index("{"):])
            except json.JSONDecodeError:
                return
            collect(child)

    for candidate in candidates:
        try:
            value = json.loads(candidate)
        except (json.JSONDecodeError, TypeError):
            continue
        collect(value)
    for value in reversed(decoded):
        if "approved" in value or "tasks" in value:
            return value
    if decoded:
        return decoded[-1]
    raise ValueError("JSON object not found in provider output")


def _validate_task(task: object) -> dict[str, object]:
    if not isinstance(task, dict):
        raise ValueError("task must be an object")
    allowed = {"id", "goal", "write_paths", "tests", "visual"}
    if set(task) - allowed or not {"id", "goal", "write_paths"}.issubset(task):
        raise ValueError("task fields are invalid")
    task_id = task["id"]
    goal = task["goal"]
    paths = task["write_paths"]
    tests = task.get("tests", [])
    if not isinstance(task_id, str) or not SAFE_ID.fullmatch(task_id):
        raise ValueError("task id is invalid")
    if not isinstance(goal, str) or not goal.strip():
        raise ValueError("task goal is empty")
    if not isinstance(paths, list) or not paths or not all(isinstance(path, str) for path in paths):
        raise ValueError("write_paths must be a non-empty string list")
    for path in paths:
        _path_parts(path)
    if not isinstance(tests, list) or not all(isinstance(test, str) for test in tests):
        raise ValueError("tests must be a string list")
    return {
        "id": task_id,
        "goal": goal.strip(),
        "write_paths": paths,
        "tests": tests,
        "visual": bool(task.get("visual", False)),
    }


def load_task_manifest(path: Path) -> list[dict[str, object]]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict) or set(value) != {"tasks"} or not isinstance(value["tasks"], list):
        raise ValueError("manifest must contain only a tasks list")
    tasks = [_validate_task(task) for task in value["tasks"]]
    ids = [str(task["id"]) for task in tasks]
    if len(ids) != len(set(ids)):
        raise ValueError("duplicate task id")
    return tasks


def validate_changed_paths(changed: list[str], allowed: list[str]) -> None:
    allowed_parts = [_path_parts(path) for path in allowed]
    for raw in changed:
        parts = _path_parts(raw)
        if not any(parts[: len(prefix)] == prefix for prefix in allowed_parts):
            raise ValueError(f"changed file outside write_paths: {raw}")


def _git(root: Path, *args: str, check: bool = True) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["git", *args], cwd=str(root), capture_output=True, text=True,
        encoding="utf-8", errors="replace", check=check,
    )


def _task_key(task: dict[str, object]) -> str:
    raw = json.dumps(task, ensure_ascii=False, sort_keys=True).encode("utf-8")
    return hashlib.sha256(raw).hexdigest()


def _read_json(path: Path, default: object) -> object:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return default


def _atomic_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile("w", encoding="utf-8", dir=path.parent, delete=False) as handle:
        json.dump(value, handle, ensure_ascii=False, indent=2, sort_keys=True)
        handle.write("\n")
        temp_path = Path(handle.name)
    os.replace(temp_path, path)


def _planner_prompt(prompt_file: Path, repo_root: Path, limit: int) -> str:
    return f"""You are the lean planning pass for one autonomous game-development lap. Do not invoke process skills, spawn subagents, or produce a prose plan; this coordinator already defines the process.
Read {prompt_file} then docs in this exact order: docs/feedback/INBOX.md, docs/STATUS.md. That pair decides this lap. Then docs/GAME_WORKLOG.md and docs/GAME_DESIGN_ASHES_TO_STARS.md if present. docs/DESIGN.md is a stub summary — never the game design source.
Pick at most {limit} small unfinished queue items from STATUS/INBOX. ascii id only (lowercase, digits, hyphen). write_paths must be repo-relative files the worker may edit, including docs/STATUS.md.
Do not edit files yourself.
Return only JSON: {{"tasks":[{{"id":"short-id","goal":"one concrete outcome","write_paths":["repo/relative/path"],"tests":["command for worker to run"],"visual":false}}]}}.
If the queue is empty return {{"tasks":[]}}. Repository: {repo_root}
"""



def plan_tasks(
    repo_root: Path,
    prompt_file: Path,
    providers: list[str],
    limit: int,
    log_dir: Path,
    max_turns: int,
    timeout_s: int,
) -> list[dict[str, object]]:
    prompt = _planner_prompt(prompt_file, repo_root, limit)
    for provider in providers:
        rc, output = run_provider(
            provider, prompt, repo_root, log_dir / f"planner-{provider}.log",
            max_turns, timeout_s, "planner",
        )
        if rc != 0:
            continue
        try:
            payload = parse_json_payload(output)
            raw_tasks = payload.get("tasks")
            if not isinstance(raw_tasks, list):
                continue
            tasks = [_validate_task(task) for task in raw_tasks]
            ids = [str(task["id"]) for task in tasks]
            if len(ids) != len(set(ids)):
                raise ValueError("duplicate task id")
            return tasks
        except ValueError:
            continue
    raise RuntimeError("planner가 유효한 task manifest를 만들지 못했다")


def _worker_prompt(prompt_file: Path, task: dict[str, object], base_sha: str) -> str:
    return f"""You are a dispatched worker for one already-approved task in a completely new session. Do not brainstorm, make another plan, invoke process skills, or spawn subagents. Read only {prompt_file}, relevant repository AI instructions, docs/feedback/INBOX.md, docs/GAME_WORKLOG.md, docs/STATUS.md, DESIGN.md, and docs/DESIGN.md in that priority order, then execute immediately.
Implement exactly one assigned task and do not duplicate work already present in branches/commits.
TASK: {json.dumps(task, ensure_ascii=False, sort_keys=True)}
BASE SHA: {base_sha}
You may change only write_paths. Run the listed tests and visually inspect applicable game output. Image generation is allowed only through Higgsfield or Grok Imagine; if unavailable, do not substitute a weaker generator.
After automated checks pass, commit the exact changed files before visual inspection. If visual inspection requires fixes, test and make a second exact-file commit. Update docs/STATUS.md this lap (PROMPT §④). Do not leave uncommitted files.
"""


def _review_prompt(prompt_file: Path, candidate: Candidate) -> str:
    task = candidate.assignment.task
    return f"""You are a dispatched reviewer in a new independent session. Do not brainstorm, make another plan, invoke process skills, or spawn subagents. Read {prompt_file} and inspect git diff {candidate.base_sha}..{candidate.head_sha} in this worktree. Do not edit or commit.
Verify the task outcome, changed-file scope, tests, actual game behavior, and visual quality. Check that existing work from other AIs was not duplicated. For visual work require Higgsfield/Grok Imagine provenance and sprite spacing/center alignment.
TASK: {json.dumps(task, ensure_ascii=False, sort_keys=True)}
Approve only when critical defects are 0, every applicable category is at least 4/5, and weighted score is at least 85.
Return only JSON: {{"approved":true,"critical":0,"score":90,"categories":{{"function":5,"quality":4}},"summary":"short evidence"}}.
"""


def _create_candidate(
    repo_root: Path,
    run_root: Path,
    run_id: str,
    assignment: Assignment,
    base_sha: str,
) -> tuple[Assignment, str, Path]:
    task_id = str(assignment.task["id"])
    branch = f"autonomous/loop-{run_id}-{task_id}"
    worktree = run_root / f"worker-{task_id}"
    _git(repo_root, "worktree", "add", "-b", branch, str(worktree), base_sha)
    return assignment, branch, worktree


def _run_worker(
    prompt_file: Path,
    assignment: Assignment,
    branch: str,
    worktree: Path,
    base_sha: str,
    log_dir: Path,
    max_turns: int,
    timeout_s: int,
    providers: list[str],
) -> Candidate:
    task_id = str(assignment.task["id"])
    attempts = [assignment.worker] + [
        provider for provider in providers if provider != assignment.worker
    ]
    failures: list[str] = []
    for worker in attempts:
        rc, _ = run_provider(
            worker,
            _worker_prompt(prompt_file, assignment.task, base_sha),
            worktree,
            log_dir / f"worker-{task_id}-{worker}.log",
            max_turns,
            timeout_s,
            "worker",
        )
        head = _git(worktree, "rev-parse", "HEAD", check=False).stdout.strip()
        changed = tuple(
            line for line in _git(
                worktree, "diff", "--name-only", f"{base_sha}..{head}", check=False
            ).stdout.splitlines() if line
        )
        clean = not _git(worktree, "status", "--porcelain", check=False).stdout.strip()
        current = Assignment(
            assignment.task,
            worker,
            next((provider for provider in providers if provider != worker), worker),
        )
        if rc == 0 and bool(head) and head != base_sha and clean:
            try:
                validate_changed_paths(
                    list(changed),
                    [str(path) for path in assignment.task["write_paths"]],  # type: ignore[index]
                )
            except ValueError as exc:
                return Candidate(
                    current, branch, worktree, base_sha, head, changed, False,
                    f"{worker}: {exc}",
                )
            return Candidate(current, branch, worktree, base_sha, head, changed, True, "")
        if not clean:
            return Candidate(
                current, branch, worktree, base_sha, head or None, changed, False,
                f"{worker}: worker left uncommitted changes",
            )
        if head and head != base_sha:
            return Candidate(
                current, branch, worktree, base_sha, head, changed, False,
                f"{worker}: worker failed after making a commit",
            )
        failures.append(f"{worker}: worker exit {rc}" if rc else f"{worker}: worker made no commit")

    return Candidate(
        assignment, branch, worktree, base_sha, base_sha, (), False, "; ".join(failures)
    )


def _run_review(
    prompt_file: Path,
    candidate: Candidate,
    log_dir: Path,
    max_turns: int,
    timeout_s: int,
    providers: list[str],
) -> ReviewedCandidate:
    task_id = str(candidate.assignment.task["id"])
    if not candidate.worker_ok:
        return ReviewedCandidate(candidate, False, {"summary": candidate.worker_log}, "")
    reviewer_order = [candidate.assignment.reviewer]
    reviewer_order.extend(
        provider for provider in providers
        if provider not in reviewer_order and provider != candidate.assignment.worker
    )
    if candidate.assignment.worker not in reviewer_order:
        reviewer_order.append(candidate.assignment.worker)
    failures: list[str] = []
    last_output = ""
    for reviewer in reviewer_order:
        rc, output = run_provider(
            reviewer,
            _review_prompt(prompt_file, candidate),
            candidate.worktree,
            log_dir / f"review-{task_id}-{reviewer}.log",
            max_turns,
            timeout_s,
            "reviewer",
        )
        last_output = output
        if rc != 0:
            failures.append(f"{reviewer}: reviewer exit {rc}")
            continue
        try:
            review = parse_json_payload(output)
        except ValueError as exc:
            failures.append(f"{reviewer}: {exc}")
            continue
        categories = review.get("categories")
        category_ok = isinstance(categories, dict) and bool(categories) and all(
            isinstance(value, (int, float)) and value >= 4 for value in categories.values()
        )
        approved = (
            review.get("approved") is True
            and review.get("critical") == 0
            and isinstance(review.get("score"), (int, float))
            and float(review["score"]) >= 85
            and category_ok
        )
        actual = candidate._replace(assignment=Assignment(
            candidate.assignment.task, candidate.assignment.worker, reviewer
        ))
        return ReviewedCandidate(actual, approved, review, output)
    return ReviewedCandidate(candidate, False, {"summary": "; ".join(failures)}, last_output)


def _integration_ref(repo_root: Path) -> str:
    exists = _git(
        repo_root, "show-ref", "--verify", "--quiet",
        "refs/heads/autonomous/integration", check=False,
    ).returncode == 0
    return "autonomous/integration" if exists else os.environ.get("LOOP_BASE_REF", "master")


def _append_status(status_path: Path, run_id: str, merged: list[ReviewedCandidate]) -> None:
    status_path.parent.mkdir(parents=True, exist_ok=True)
    current = status_path.read_text(encoding="utf-8") if status_path.exists() else "# 개발 상태\n"
    lines = ["", f"## 자동 루프 {run_id}", ""]
    for item in merged:
        lines.append(
            f"- `{item.candidate.branch}` `{item.candidate.head_sha}` — "
            f"worker {item.candidate.assignment.worker}, reviewer {item.candidate.assignment.reviewer}: 승인"
        )
    status_path.write_text(current.rstrip() + "\n" + "\n".join(lines) + "\n", encoding="utf-8")


def integrate_candidates(
    repo_root: Path,
    run_root: Path,
    run_id: str,
    reviewed: list[ReviewedCandidate],
) -> list[ReviewedCandidate]:
    approved = [item for item in reviewed if item.approved]
    if not approved:
        return []
    ref_exists = _git(
        repo_root, "show-ref", "--verify", "--quiet",
        "refs/heads/autonomous/integration", check=False,
    ).returncode == 0
    if not ref_exists:
        _git(repo_root, "branch", "autonomous/integration", approved[0].candidate.base_sha)
    integration_tree = run_root / "integration"
    _git(repo_root, "worktree", "add", str(integration_tree), "autonomous/integration")
    merged: list[ReviewedCandidate] = []
    try:
        for item in approved:
            result = _git(
                integration_tree, "merge", "--no-ff", "--no-edit",
                item.candidate.branch, check=False,
            )
            if result.returncode != 0:
                _git(integration_tree, "merge", "--abort", check=False)
                continue
            merged.append(item)
        if merged:
            _append_status(integration_tree / "docs/STATUS.md", run_id, merged)
            _git(integration_tree, "add", "--", "docs/STATUS.md")
            _git(
                integration_tree, "commit", "-m",
                f"상태(재와별): 자율 루프 {run_id} 인수인계",
            )
            if os.environ.get("LOOP_PUSH", "1") == "1":
                _git(integration_tree, "push", "origin", "autonomous/integration", check=False)
    finally:
        _git(repo_root, "worktree", "remove", "--force", str(integration_tree), check=False)
    return merged


def run_lap(repo_root: Path, prompt_file: Path, task_file: Path | None) -> int:
    if not (repo_root / ".git").exists() and _git(repo_root, "rev-parse", "--git-dir", check=False).returncode != 0:
        raise RuntimeError(f"not a git repository: {repo_root}")
    max_turns = int(os.environ.get("LOOP_MAX_TURNS", "30"))
    timeout_s = int(os.environ.get("LOOP_SESSION_TIMEOUT", "1800"))
    max_parallel = int(os.environ.get("LOOP_MAX_PARALLEL", "3"))
    mode = os.environ.get("LOOP_MODE", "auto").strip().lower()
    if mode not in {"auto", "single", "parallel"}:
        raise ValueError("LOOP_MODE must be auto, single, or parallel")
    limit = 1 if mode == "single" else max_parallel
    if not 1 <= limit <= 3:
        raise ValueError("LOOP_MAX_PARALLEL must be 1..3")

    state_root = repo_root / "output/cache/autonomous_loop"
    state_root.mkdir(parents=True, exist_ok=True)
    lock_handle = (state_root / "coordinator.lock").open("a+", encoding="utf-8")
    try:
        fcntl.flock(lock_handle.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
    except BlockingIOError:
        print("다른 자율 루프가 실행 중이라 이번 poll을 건너뜁니다.")
        return 75

    run_id = __import__("datetime").datetime.now().strftime("%Y%m%d-%H%M%S-%f")
    date = __import__("datetime").date.today().isoformat()
    log_dir = repo_root / "logs" / date / run_id
    log_dir.mkdir(parents=True, exist_ok=True)
    run_root = state_root / "worktrees" / run_id
    run_root.mkdir(parents=True, exist_ok=True)
    summary: dict[str, object] = {"run_id": run_id, "mode": mode, "tasks": []}
    worktrees: list[Path] = []
    try:
        providers = available_providers()
        if not providers:
            summary["outcome"] = "infrastructure_hold"
            summary["reason"] = "no strong provider is available"
            _atomic_json(log_dir / "run.json", summary)
            return 75

        manifest_path = task_file
        if manifest_path is None:
            configured = os.environ.get("LOOP_TASK_FILE", "").strip()
            if configured:
                manifest_path = Path(configured)
                if not manifest_path.is_absolute():
                    manifest_path = repo_root / manifest_path
            elif (repo_root / "loop/TASKS.json").exists():
                manifest_path = repo_root / "loop/TASKS.json"
        tasks = (
            load_task_manifest(manifest_path)
            if manifest_path
            else plan_tasks(repo_root, prompt_file, providers, limit, log_dir, max_turns, timeout_s)
        )
        completed_path = state_root / "completed.json"
        completed_value = _read_json(completed_path, {})
        completed = completed_value if isinstance(completed_value, dict) else {}
        tasks = [task for task in tasks if _task_key(task) not in completed]
        selected, deferred = select_independent_tasks(tasks, limit)
        summary["deferred"] = [task["id"] for task in deferred]
        if not selected:
            summary["outcome"] = "idle"
            _atomic_json(log_dir / "run.json", summary)
            return 10

        assignments = assign_providers(selected, providers)
        base_ref = _integration_ref(repo_root)
        base_sha = _git(repo_root, "rev-parse", base_ref).stdout.strip()
        prepared: list[tuple[Assignment, str, Path]] = []
        for assignment in assignments:
            item = _create_candidate(repo_root, run_root, run_id, assignment, base_sha)
            prepared.append(item)
            worktrees.append(item[2])
        with ThreadPoolExecutor(max_workers=len(prepared)) as pool:
            futures = [
                pool.submit(
                    _run_worker, prompt_file, assignment, branch, worktree,
                    base_sha, log_dir, max_turns, timeout_s, providers,
                )
                for assignment, branch, worktree in prepared
            ]
            candidates = [future.result() for future in futures]
        with ThreadPoolExecutor(max_workers=len(candidates)) as pool:
            futures = [
                pool.submit(
                    _run_review, prompt_file, candidate, log_dir,
                    max_turns, timeout_s, providers,
                )
                for candidate in candidates
            ]
            reviewed = [future.result() for future in futures]

        merged = integrate_candidates(repo_root, run_root, run_id, reviewed)
        for item in merged:
            completed[_task_key(item.candidate.assignment.task)] = {
                "branch": item.candidate.branch,
                "commit": item.candidate.head_sha,
                "run_id": run_id,
            }
        _atomic_json(completed_path, completed)
        summary["tasks"] = [
            {
                "id": item.candidate.assignment.task["id"],
                "worker": item.candidate.assignment.worker,
                "reviewer": item.candidate.assignment.reviewer,
                "branch": item.candidate.branch,
                "commit": item.candidate.head_sha,
                "changed_files": item.candidate.changed_files,
                "approved": item.approved,
                "integrated": item in merged,
                "review": item.review,
            }
            for item in reviewed
        ]
        summary["outcome"] = "completed" if merged else "rejected"
        _atomic_json(log_dir / "run.json", summary)
        # AI 세션과 검토까지 마친 바퀴는 후보가 불합격이어도 정상적으로 계수한다.
        return 0
    finally:
        for worktree in worktrees:
            _git(repo_root, "worktree", "remove", "--force", str(worktree), check=False)
        lock_handle.close()


def main() -> int:
    parser = argparse.ArgumentParser(description="재와별 자율 개발 루프 한 바퀴")
    parser.add_argument("--repo-root", required=True, type=Path)
    parser.add_argument("--prompt-file", required=True, type=Path)
    parser.add_argument("--task-file", type=Path)
    args = parser.parse_args()
    try:
        return run_lap(args.repo_root.resolve(), args.prompt_file.resolve(), args.task_file)
    except (OSError, ValueError, RuntimeError, subprocess.SubprocessError) as exc:
        print(f"자율 루프 오류: {exc}", file=sys.stderr)
        return 75 if isinstance(exc, RuntimeError) else 1


if __name__ == "__main__":
    raise SystemExit(main())
