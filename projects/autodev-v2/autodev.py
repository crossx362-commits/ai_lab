#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AutoDev v2 — 결과 중심 자율 개발 루프.

설계 원칙
- 다음 할 일은 Director가 한 번에 여러 개 뽑고 로컬 Task Queue가 소비한다.
- 실제 구현은 Grok이 기본. 같은 작업 2회 실패할 때만 Codex 1회.
- Claude는 호출하지 않는다.
- 파일 검색, Git 상태, Unity 컴파일/검증은 로컬 프로세스가 처리한다.
- 작업마다 새 headless 세션을 사용한다. 이전 대화는 넘기지 않는다.
- 동일 상태에서 무한 재시도하지 않는다.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

HERE = Path(__file__).resolve().parent
DEFAULT_CONFIG = HERE / "config.json"


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


def repo_root(start: Path | None = None) -> Path:
    start = (start or HERE).resolve()
    r = subprocess.run(
        ["git", "rev-parse", "--show-toplevel"],
        cwd=start, capture_output=True, text=True, encoding="utf-8", errors="replace"
    )
    if r.returncode != 0:
        raise RuntimeError("Git 저장소 루트를 찾지 못했습니다.")
    return Path(r.stdout.strip()).resolve()


def load_config(path: Path = DEFAULT_CONFIG) -> dict[str, Any]:
    data = json.loads(path.read_text(encoding="utf-8"))
    root = repo_root()
    data["_repo_root"] = str(root)
    for key in ("project_root", "design_file", "handoff_file", "state_file"):
        if key in data:
            data[key] = str((root / data[key]).resolve())
    return data


def atomic_json(path: Path, data: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    tmp.replace(path)


def new_state() -> dict[str, Any]:
    return {
        "version": 2,
        "goal": "",
        "tasks": [],
        "completed": [],
        "blocked": [],
        "next_task_number": 1,
        "stats": {
            "grok_calls": 0,
            "codex_calls": 0,
            "director_calls": 0,
            "tasks_done": 0,
            "tasks_blocked": 0,
        },
        "last_director_at": None,
        "updated_at": now_iso(),
    }


def load_state(cfg: dict[str, Any]) -> dict[str, Any]:
    p = Path(cfg["state_file"])
    if not p.exists():
        st = new_state()
        atomic_json(p, st)
        return st
    try:
        st = json.loads(p.read_text(encoding="utf-8"))
    except Exception:
        bad = p.with_name(p.name + f".broken.{int(time.time())}")
        p.replace(bad)
        st = new_state()
        st["recovered_from"] = str(bad)
        atomic_json(p, st)
    return st


def save_state(cfg: dict[str, Any], st: dict[str, Any]) -> None:
    st["updated_at"] = now_iso()
    atomic_json(Path(cfg["state_file"]), st)


def run_cmd(
    args: list[str],
    cwd: Path,
    timeout: int = 120,
    env: dict[str, str] | None = None,
) -> tuple[int, str]:
    try:
        r = subprocess.run(
            args, cwd=cwd, capture_output=True, text=True,
            encoding="utf-8", errors="replace", timeout=timeout, env=env
        )
        return r.returncode, ((r.stdout or "") + ("\n" + r.stderr if r.stderr else "")).strip()
    except subprocess.TimeoutExpired as e:
        out = (e.stdout or "") if isinstance(e.stdout, str) else ""
        return 124, (out + f"\nTIMEOUT after {timeout}s").strip()
    except Exception as e:
        return 125, f"{type(e).__name__}: {e}"


def git_text(root: Path, args: list[str], timeout: int = 60) -> str:
    _, out = run_cmd(["git", *args], root, timeout=timeout)
    return out


def diff_fingerprint(root: Path) -> str:
    """작업트리 diff 내용을 해시한다. 줄 수만 같은 변경도 구분한다."""
    try:
        p = subprocess.Popen(
            ["git", "diff", "--binary", "--no-ext-diff"],
            cwd=root, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL
        )
        h = hashlib.sha256()
        assert p.stdout is not None
        for chunk in iter(lambda: p.stdout.read(1024 * 1024), b""):
            h.update(chunk)
        p.wait(timeout=30)
        untracked = git_text(root, ["ls-files", "--others", "--exclude-standard"])
        h.update(untracked.encode("utf-8", "replace"))
        return h.hexdigest()
    except Exception:
        return hashlib.sha256(git_text(root, ["status", "--porcelain=v1"]).encode()).hexdigest()


def changed_files(root: Path) -> list[str]:
    tracked = git_text(root, ["diff", "--name-only"]).splitlines()
    untracked = git_text(root, ["ls-files", "--others", "--exclude-standard"]).splitlines()
    seen: list[str] = []
    for x in tracked + untracked:
        x = x.strip()
        if x and x not in seen:
            seen.append(x)
    return seen


def compact_design(cfg: dict[str, Any]) -> str:
    """기획서 전체 대신 제목/확정(✅)/우선순위 표식 중심의 압축 문맥을 만든다."""
    p = Path(cfg["design_file"])
    if not p.exists():
        return "(기획서 없음)"
    lines = p.read_text(encoding="utf-8", errors="replace").splitlines()
    picked: list[str] = []
    for ln in lines:
        s = ln.strip()
        if not s:
            continue
        if s.startswith("#") or "✅" in s or re.search(r"\bP[0-2]\b", s):
            picked.append(s)
    if len("\n".join(picked)) < 4000:
        picked.extend(lines[:120])
    text = "\n".join(picked)
    return text[: int(cfg["max_context_chars"]) // 2]


def compact_handoff(cfg: dict[str, Any]) -> str:
    p = Path(cfg["handoff_file"])
    if not p.exists():
        return "(핸드오프 없음)"
    text = p.read_text(encoding="utf-8", errors="replace")
    return text[-4000:]


def situation(cfg: dict[str, Any], st: dict[str, Any]) -> str:
    root = Path(cfg["_repo_root"])
    recent = git_text(root, ["log", "--oneline", "-8"])
    status = git_text(root, ["status", "--short"]) or "(없음)"
    completed = "\n".join(
        f"- {x.get('title','')}" for x in st.get("completed", [])[-10:]
    ) or "(없음)"
    blocked = "\n".join(
        f"- {x.get('title','')}: {x.get('last_error','')[:240]}"
        for x in st.get("blocked", [])[-5:]
    ) or "(없음)"
    return (
        f"[최근 커밋]\n{recent}\n\n"
        f"[현재 변경]\n{status[:4000]}\n\n"
        f"[최근 완료]\n{completed}\n\n"
        f"[막힌 작업]\n{blocked}\n"
    )


def subscription_env(cfg: dict[str, Any], provider: str) -> dict[str, str]:
    env = os.environ.copy()
    if cfg.get("prefer_subscription_oauth", True):
        if provider == "grok":
            env.pop("XAI_API_KEY", None)
        elif provider == "codex":
            for k in ("OPENAI_API_KEY", "OPENAI_BASE_URL"):
                env.pop(k, None)
    return env


def stream_process(cmd: list[str], cwd: Path, timeout: int, env: dict[str, str], tag: str) -> tuple[int, str]:
    """출력은 실시간으로 보여주되 최종 파싱을 위해 보관한다."""
    print(f"\n[{tag}] 시작")
    started = time.time()
    lines: list[str] = []
    try:
        p = subprocess.Popen(
            cmd, cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
            text=True, encoding="utf-8", errors="replace", bufsize=1, env=env
        )
        assert p.stdout is not None
        while True:
            if time.time() - started > timeout:
                p.kill()
                lines.append(f"TIMEOUT after {timeout}s")
                return 124, "\n".join(lines)
            line = p.stdout.readline()
            if line:
                line = line.rstrip("\n")
                lines.append(line)
                print(f"[{tag}] {line}", flush=True)
            elif p.poll() is not None:
                break
            else:
                time.sleep(0.05)
        return p.wait(), "\n".join(lines)
    except Exception as e:
        return 125, f"{type(e).__name__}: {e}"


def grok_call(
    cfg: dict[str, Any],
    st: dict[str, Any],
    prompt: str,
    *,
    role: str,
    cwd: Path,
    max_turns: int,
    allow_edits: bool,
) -> tuple[int, str]:
    exe = shutil.which("grok")
    if not exe:
        return 127, "grok CLI를 찾을 수 없습니다. 먼저 grok 로그인/설치를 확인하세요."
    root = Path(cfg["_repo_root"])
    profile = root / ".grok" / "agents" / f"autodev-v2-{role}.md"
    cmd = [
        exe, "--no-auto-update",
        "-p", prompt,
        "--cwd", str(cwd),
        "--output-format", "plain",
        "--max-turns", str(max_turns),
        "--no-subagents",
        "--no-memory",
        "--disable-web-search",
    ]
    if profile.exists():
        cmd += ["--agent-profile", str(profile)]
    if allow_edits:
        cmd += ["--always-approve"]
    st["stats"]["grok_calls"] = int(st["stats"].get("grok_calls", 0)) + 1
    if role == "director":
        st["stats"]["director_calls"] = int(st["stats"].get("director_calls", 0)) + 1
    return stream_process(
        cmd, cwd, timeout=900 if allow_edits else 300,
        env=subscription_env(cfg, "grok"), tag=f"GROK:{role}"
    )


def codex_call(cfg: dict[str, Any], st: dict[str, Any], prompt: str, cwd: Path) -> tuple[int, str]:
    exe = shutil.which("codex")
    if not exe:
        return 127, "codex CLI를 찾을 수 없습니다."
    fd, outpath = tempfile.mkstemp(prefix="autodev_v2_codex_", suffix=".txt")
    os.close(fd)
    try:
        cmd = [exe, "exec", "--skip-git-repo-check", "-o", outpath, prompt]
        print("\n[CODEX:fallback] 시작")
        r = subprocess.run(
            cmd, cwd=cwd, capture_output=True, text=True,
            encoding="utf-8", errors="replace", timeout=900,
            env=subscription_env(cfg, "codex")
        )
        st["stats"]["codex_calls"] = int(st["stats"].get("codex_calls", 0)) + 1
        body = Path(outpath).read_text(encoding="utf-8", errors="replace").strip()
        if r.stdout:
            print(r.stdout[-3000:])
        if r.stderr and r.returncode != 0:
            print(r.stderr[-3000:])
        return r.returncode, body or (r.stderr or r.stdout or "")
    except subprocess.TimeoutExpired:
        return 124, "Codex timeout"
    finally:
        try:
            os.unlink(outpath)
        except OSError:
            pass


def extract_json(text: str) -> Any:
    """코드펜스/서문이 있어도 첫 JSON 객체/배열을 찾는다."""
    text = text.strip()
    try:
        return json.loads(text)
    except Exception:
        pass
    decoder = json.JSONDecoder()
    starts = [i for i, ch in enumerate(text) if ch in "[{"]
    for i in starts:
        try:
            obj, _ = decoder.raw_decode(text[i:])
            return obj
        except Exception:
            continue
    return None


def normalize_director_tasks(cfg: dict[str, Any], st: dict[str, Any], raw: Any) -> list[dict[str, Any]]:
    if isinstance(raw, dict):
        raw = raw.get("tasks")
    if not isinstance(raw, list):
        return []
    limit = int(cfg["max_tasks_per_director_batch"])
    out: list[dict[str, Any]] = []
    batch_ids: list[str] = []
    for item in raw[:limit]:
        if not isinstance(item, dict):
            continue
        title = str(item.get("title", "")).strip()
        goal = str(item.get("goal", item.get("detail", ""))).strip()
        done = item.get("done_when", item.get("verify", []))
        if isinstance(done, str):
            done = [done]
        if not title or not goal or not isinstance(done, list) or not done:
            continue
        num = int(st.get("next_task_number", 1))
        tid = f"T{num:04d}"
        st["next_task_number"] = num + 1
        dep_indexes = item.get("depends_on", [])
        deps: list[str] = []
        if isinstance(dep_indexes, list):
            for x in dep_indexes:
                if isinstance(x, int) and 1 <= x <= len(batch_ids):
                    deps.append(batch_ids[x - 1])
        task = {
            "id": tid,
            "title": title[:160],
            "goal": goal[:1200],
            "done_when": [str(x)[:500] for x in done[:6]],
            "priority": max(1, min(100, int(item.get("priority", 50)))),
            "depends_on": deps,
            "verify_mode": str(item.get("verify_mode", "compile")).lower(),
            "milestone": bool(item.get("milestone", False)),
            "status": "pending",
            "attempts_grok": 0,
            "attempts_codex": 0,
            "created_at": now_iso(),
            "last_error": "",
            "last_verify_fingerprint": "",
        }
        if task["verify_mode"] not in {"compile", "build"}:
            task["verify_mode"] = "compile"
        batch_ids.append(tid)
        out.append(task)
    return out


def director_fill(cfg: dict[str, Any], st: dict[str, Any]) -> bool:
    root = Path(cfg["_repo_root"])
    prompt = f"""당신은 '재와 별' AutoDev v2의 DIRECTOR다.
코드를 수정하지 말고 다음 개발 작업 묶음만 결정한다.

목표:
- 커밋 수나 문서 수가 아니라 실제 플레이 가능한 기능을 앞으로 진행한다.
- 작은 장식/리팩터링/상태문서보다 핵심 게임 루프를 우선한다.
- 이미 완료한 작업을 다시 만들지 않는다.
- 한 번에 서로 이어지는 4~{cfg['max_tasks_per_director_batch']}개 작업만 만든다.
- 각 작업은 개발자가 한 세션 안에 구현하고 검증 가능한 크기여야 한다.
- 마지막 작업은 가능하면 수직 슬라이스 통합 검증으로 두고 milestone=true.
- 불필요한 회의, 문서 갱신, 전체 아트 통일 작업은 만들지 않는다.

[기획서 압축본]
{compact_design(cfg)}

[최근 핸드오프]
{compact_handoff(cfg)}

[현재 상태]
{situation(cfg, st)}

JSON만 출력:
{{
  "goal": "이번 묶음의 한 줄 목표",
  "tasks": [
    {{
      "title": "작업명",
      "goal": "무엇을 구현하는지",
      "done_when": ["검증 가능한 조건1", "조건2"],
      "priority": 1,
      "depends_on": [1],
      "verify_mode": "compile 또는 build",
      "milestone": false
    }}
  ]
}}
"""
    code, out = grok_call(
        cfg, st, prompt, role="director", cwd=root,
        max_turns=int(cfg["grok_director_max_turns"]), allow_edits=False
    )
    if code != 0:
        print(f"[DIRECTOR] 실패 rc={code}: {out[-1000:]}")
        return False
    parsed = extract_json(out)
    tasks = normalize_director_tasks(cfg, st, parsed)
    if not tasks:
        print("[DIRECTOR] 유효한 작업 JSON을 만들지 못했습니다.")
        return False
    if isinstance(parsed, dict) and parsed.get("goal"):
        st["goal"] = str(parsed["goal"])[:500]
    st["tasks"].extend(tasks)
    st["last_director_at"] = now_iso()
    print(f"[DIRECTOR] 새 작업 {len(tasks)}개 생성: " + ", ".join(t["id"] for t in tasks))
    return True


def next_ready(st: dict[str, Any]) -> dict[str, Any] | None:
    done_ids = {x.get("id") for x in st.get("completed", [])}
    candidates = []
    for t in st.get("tasks", []):
        if t.get("status") != "pending":
            continue
        if all(dep in done_ids for dep in t.get("depends_on", [])):
            candidates.append(t)
    if not candidates:
        return None
    candidates.sort(key=lambda t: (-int(t.get("priority", 0)), t.get("created_at", ""), t.get("id", "")))
    return candidates[0]


def candidate_files(cfg: dict[str, Any], task: dict[str, Any], verify_text: str = "") -> list[str]:
    """AI 없이 관련 파일 후보를 좁힌다. 오류 경로 > rg 키워드 > 최근 변경 순."""
    root = Path(cfg["_repo_root"])
    project = Path(cfg["project_root"])
    maxn = int(cfg["max_candidate_files"])
    found: list[str] = []
    for m in re.findall(r"((?:Assets|projects/ashes-to-stars/unity/Assets)/[^\s:()]+\.cs)", verify_text):
        p = m
        if p.startswith("Assets/"):
            p = str(project.relative_to(root) / p)
        if p not in found:
            found.append(p)
    text = f"{task.get('title','')} {task.get('goal','')} {' '.join(task.get('done_when',[]))}"
    words = []
    for w in re.findall(r"[A-Za-z_][A-Za-z0-9_]{2,}|[가-힣]{2,}", text):
        lw = w.lower()
        if lw not in {"완료", "기능", "시스템", "검증", "작업", "처리"} and lw not in [x.lower() for x in words]:
            words.append(w)
    rg = shutil.which("rg")
    if rg:
        for w in words[:8]:
            if len(found) >= maxn:
                break
            code, out = run_cmd(
                [rg, "-l", "--glob", "*.cs", "--glob", "!Library/**", "--glob", "!Temp/**", w, "."],
                project, timeout=12
            )
            if code in (0, 1):
                for rel in out.splitlines():
                    p = str((project / rel).resolve().relative_to(root))
                    if p not in found:
                        found.append(p)
                    if len(found) >= maxn:
                        break
    if len(found) < maxn:
        recent = git_text(root, ["diff", "--name-only"]).splitlines()
        for p in recent:
            if p.endswith(".cs") and p.startswith("projects/ashes-to-stars/unity/") and p not in found:
                found.append(p)
                if len(found) >= maxn:
                    break
    return found[:maxn]


def context_packet(cfg: dict[str, Any], task: dict[str, Any], verify_text: str = "") -> str:
    root = Path(cfg["_repo_root"])
    files = candidate_files(cfg, task, verify_text)
    chunks = []
    budget = int(cfg["max_context_chars"])
    per = max(1200, budget // max(1, len(files)))
    for rel in files:
        p = root / rel
        try:
            txt = p.read_text(encoding="utf-8", errors="replace")
            if len(txt) > per:
                txt = txt[: per // 2] + "\n...[중략]...\n" + txt[-per // 2 :]
            chunks.append(f"### {rel}\n{txt}")
        except Exception:
            continue
    files_text = "\n\n".join(chunks)
    err = verify_text[-int(cfg["max_verify_output_chars"]):] if verify_text else "(없음)"
    return f"[로컬이 고른 관련 파일 최대 {cfg['max_candidate_files']}개]\n{files_text or '(후보 없음 — 필요한 파일만 직접 검색)'}\n\n[직전 검증 오류]\n{err}"


def worker_prompt(cfg: dict[str, Any], task: dict[str, Any], verify_text: str = "") -> str:
    done = "\n".join(f"- {x}" for x in task.get("done_when", []))
    return f"""당신은 AutoDev v2의 WORKER다. 다음 작업 하나만 끝낸다.

[작업]
ID: {task['id']}
제목: {task['title']}
목표: {task['goal']}

[완료 조건]
{done}

{context_packet(cfg, task, verify_text)}

[강제 규칙]
1. 다음 할 일을 기획하지 말고 이 작업만 구현한다.
2. 관련 없는 리팩터링/정리/아트 통일/STATUS·회의록 작성 금지.
3. 처음부터 프로젝트 전체를 읽지 마라. 제공된 후보부터 보고 추가 파일은 꼭 필요한 것만, 총 5개 안팎을 우선한다.
4. Git commit/push 금지. 사용자의 기존 미커밋 변경을 되돌리지 마라.
5. 최소 변경으로 구현한다.
6. 가능한 테스트/정적 검사를 실행하되 Unity 에디터를 강제 종료하지 마라.
7. 같은 접근이 실패하면 무한 반복하지 말고 실패 원인을 명확히 남겨라.
8. 완료 조건을 충족하면 더 개선하지 말고 즉시 종료한다.
"""


def compile_verify(cfg: dict[str, Any]) -> tuple[str, str]:
    root = Path(cfg["_repo_root"])
    tool = root / "projects/ai-team/skills/마루_게임개발/tools/game_compile_check.py"
    if not tool.exists():
        return "skip", "game_compile_check.py 없음"
    code, out = run_cmd(
        [sys.executable, str(tool), "--project", cfg["project_root"]],
        root, timeout=700
    )
    out = out[-int(cfg["max_verify_output_chars"]):]
    if code == 0:
        return "pass", out
    if code == 2:
        return "skip", out
    return "fail", out


def full_build_verify(cfg: dict[str, Any]) -> tuple[str, str]:
    root = Path(cfg["_repo_root"])
    tool = root / "projects/ai-team/skills/마루_게임개발/tools/game_build_verify.py"
    if not tool.exists():
        return "skip", "game_build_verify.py 없음"
    code, out = run_cmd([sys.executable, str(tool)], root, timeout=2400)
    out = out[-int(cfg["max_verify_output_chars"]):]
    return ("pass" if code == 0 else "fail"), out


def verify_task(cfg: dict[str, Any], task: dict[str, Any]) -> tuple[str, str]:
    status, out = compile_verify(cfg)
    if status != "pass":
        return status, out
    if task.get("verify_mode") == "build" or (task.get("milestone") and cfg.get("full_verify_on_milestone")):
        return full_build_verify(cfg)
    return "pass", out


def verify_fingerprint(status: str, output: str) -> str:
    normalized = re.sub(r"\d{2}:\d{2}:\d{2}", "<time>", output)
    return hashlib.sha256((status + "\n" + normalized).encode("utf-8", "replace")).hexdigest()


def finish_task(cfg: dict[str, Any], st: dict[str, Any], task: dict[str, Any], verify_out: str) -> None:
    root = Path(cfg["_repo_root"])
    task["status"] = "done"
    task["completed_at"] = now_iso()
    task["changed_files"] = changed_files(root)[:30]
    task["verification"] = verify_out[-2000:]
    st["completed"].append(dict(task))
    st["tasks"] = [t for t in st["tasks"] if t.get("id") != task["id"]]
    st["stats"]["tasks_done"] = int(st["stats"].get("tasks_done", 0)) + 1
    print(f"\n✅ {task['id']} 완료: {task['title']}")


def block_task(st: dict[str, Any], task: dict[str, Any], error: str) -> None:
    task["status"] = "blocked"
    task["blocked_at"] = now_iso()
    task["last_error"] = error[-3000:]
    st["blocked"].append(dict(task))
    st["tasks"] = [t for t in st["tasks"] if t.get("id") != task["id"]]
    st["stats"]["tasks_blocked"] = int(st["stats"].get("tasks_blocked", 0)) + 1
    print(f"\n🛑 {task['id']} BLOCKED: {task['title']}\n{error[-1500:]}")


def execute_one(cfg: dict[str, Any], st: dict[str, Any], task: dict[str, Any], run_stats: dict[str, int]) -> bool:
    root = Path(cfg["_repo_root"])
    project = Path(cfg["project_root"])
    last_verify = task.get("last_error", "")
    before = diff_fingerprint(root)
    same_failure_count = 0

    max_grok = int(cfg["max_grok_attempts_per_task"])
    while int(task.get("attempts_grok", 0)) < max_grok:
        if run_stats["cloud_calls"] >= int(cfg["max_cloud_calls_per_run"]):
            return False
        task["attempts_grok"] = int(task.get("attempts_grok", 0)) + 1
        run_stats["cloud_calls"] += 1
        code, out = grok_call(
            cfg, st, worker_prompt(cfg, task, last_verify),
            role="worker", cwd=project,
            max_turns=int(cfg["grok_worker_max_turns"]), allow_edits=True
        )
        if code != 0:
            last_verify = f"Grok 실행 실패 rc={code}\n{out[-2000:]}"
        after = diff_fingerprint(root)
        if after == before:
            last_verify = (last_verify + "\n\n[로컬 판정] 작업트리 변화 없음. 같은 분석만 반복하지 말 것.").strip()
        else:
            before = after
            status, vout = verify_task(cfg, task)
            fp = verify_fingerprint(status, vout)
            print(f"\n[VERIFY] {status.upper()}\n{vout[-2500:]}")
            if status == "pass":
                finish_task(cfg, st, task, vout)
                return True
            if status == "skip":
                last_verify = "검증 장치를 실행할 수 없어 완료 판정 불가.\n" + vout
            else:
                if fp == task.get("last_verify_fingerprint"):
                    same_failure_count += 1
                else:
                    same_failure_count = 0
                task["last_verify_fingerprint"] = fp
                last_verify = vout
                if same_failure_count >= 1:
                    print("[LOOP GUARD] 같은 검증 실패가 반복되어 Grok 재시도를 조기 종료합니다.")
                    break
        task["last_error"] = last_verify
        save_state(cfg, st)

    max_codex = int(cfg["max_codex_attempts_per_task"])
    while int(task.get("attempts_codex", 0)) < max_codex:
        if run_stats["cloud_calls"] >= int(cfg["max_cloud_calls_per_run"]):
            return False
        task["attempts_codex"] = int(task.get("attempts_codex", 0)) + 1
        run_stats["cloud_calls"] += 1
        prompt = worker_prompt(cfg, task, last_verify) + "\n\nGrok이 이미 실패했다. 같은 접근을 반복하지 말고 원인을 좁혀 최소 수정으로 해결하라."
        before_codex = diff_fingerprint(root)
        code, out = codex_call(cfg, st, prompt, project)
        after_codex = diff_fingerprint(root)
        if code != 0:
            last_verify = f"Codex 실행 실패 rc={code}\n{out[-2000:]}"
        elif after_codex == before_codex:
            last_verify = "Codex 실행 후 작업트리 변화 없음.\n" + out[-1500:]
        else:
            status, vout = verify_task(cfg, task)
            print(f"\n[VERIFY] {status.upper()}\n{vout[-2500:]}")
            if status == "pass":
                finish_task(cfg, st, task, vout)
                return True
            last_verify = vout
        task["last_error"] = last_verify
        save_state(cfg, st)

    block_task(st, task, last_verify or "최대 재시도 소진")
    return False


def print_status(cfg: dict[str, Any], st: dict[str, Any]) -> None:
    ready = next_ready(st)
    print("=" * 68)
    print("AutoDev v2")
    print(f"목표: {st.get('goal') or '(아직 Director 미실행)'}")
    print(f"대기: {len(st.get('tasks', []))}  완료: {len(st.get('completed', []))}  막힘: {len(st.get('blocked', []))}")
    print(f"Grok 호출: {st['stats'].get('grok_calls',0)}  Codex 호출: {st['stats'].get('codex_calls',0)}  Director: {st['stats'].get('director_calls',0)}")
    if ready:
        print(f"다음: {ready['id']} {ready['title']}")
    print(f"상태파일: {cfg['state_file']}")
    print("=" * 68)


def run_loop(cfg: dict[str, Any], continuous: bool) -> int:
    st = load_state(cfg)
    run_stats = {"cloud_calls": 0, "tasks": 0}
    max_tasks = int(cfg["max_tasks_per_run"])

    while True:
        print_status(cfg, st)
        if st.get("blocked") and cfg.get("stop_on_blocked", True):
            print("막힌 작업이 있어 자동 루프를 중지합니다. 상태를 확인한 뒤 unblock 하세요.")
            save_state(cfg, st)
            return 2
        task = next_ready(st)
        if not task:
            pending = [t for t in st.get("tasks", []) if t.get("status") == "pending"]
            if pending:
                ids = ", ".join(t["id"] for t in pending)
                print(f"실행 가능한 작업이 없습니다. 의존성 교착 가능성: {ids}")
                return 3
            if run_stats["cloud_calls"] >= int(cfg["max_cloud_calls_per_run"]):
                print("이번 실행의 클라우드 호출 예산을 모두 사용했습니다.")
                return 0
            if not director_fill(cfg, st):
                save_state(cfg, st)
                return 4
            run_stats["cloud_calls"] += 1
            save_state(cfg, st)
            task = next_ready(st)
            if not task:
                return 4
        ok = execute_one(cfg, st, task, run_stats)
        save_state(cfg, st)
        run_stats["tasks"] += 1
        if not ok and cfg.get("stop_on_blocked", True):
            return 2
        if not continuous:
            return 0
        if run_stats["tasks"] >= max_tasks:
            print(f"이번 실행의 작업 상한({max_tasks})에 도달했습니다. 무한 소모 방지를 위해 종료합니다.")
            return 0
        if run_stats["cloud_calls"] >= int(cfg["max_cloud_calls_per_run"]):
            print("이번 실행의 클라우드 호출 예산에 도달했습니다. 종료합니다.")
            return 0


def unblock(cfg: dict[str, Any], st: dict[str, Any], tid: str) -> bool:
    idx = next((i for i, t in enumerate(st.get("blocked", [])) if t.get("id") == tid), None)
    if idx is None:
        return False
    t = st["blocked"].pop(idx)
    t["status"] = "pending"
    t["attempts_grok"] = 0
    t["attempts_codex"] = 0
    t["last_verify_fingerprint"] = ""
    st["tasks"].append(t)
    save_state(cfg, st)
    return True


def main() -> int:
    ap = argparse.ArgumentParser(description="AutoDev v2 — Grok 중심 결과 기반 자율 개발")
    ap.add_argument("--config", default=str(DEFAULT_CONFIG))
    sub = ap.add_subparsers(dest="cmd", required=True)
    rp = sub.add_parser("run")
    rp.add_argument("--continuous", action="store_true", help="작업 큐를 예산 상한까지 연속 실행")
    sub.add_parser("status")
    up = sub.add_parser("unblock")
    up.add_argument("task_id")
    sub.add_parser("reset-queue", help="미완료 큐만 비우고 완료 이력은 보존")
    a = ap.parse_args()
    cfg = load_config(Path(a.config))
    st = load_state(cfg)
    if a.cmd == "status":
        print_status(cfg, st)
        return 0
    if a.cmd == "run":
        return run_loop(cfg, bool(a.continuous))
    if a.cmd == "unblock":
        if not unblock(cfg, st, a.task_id):
            print("해당 BLOCKED 작업을 찾지 못했습니다.")
            return 1
        print(f"{a.task_id} 재시도 가능 상태로 복구했습니다.")
        return 0
    if a.cmd == "reset-queue":
        st["tasks"] = []
        save_state(cfg, st)
        print("대기 큐를 비웠습니다. 다음 run에서 Director가 새 묶음을 만듭니다.")
        return 0
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
