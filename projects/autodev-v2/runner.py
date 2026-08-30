#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AutoDev v2 autonomous supervisor.

핵심 원칙
- 계획은 Grok Director만 담당한다. Ollama는 계획에 사용하지 않는다.
- 중복 작업/같은 영역 반복은 프롬프트가 아니라 로컬 코드로 차단한다.
- 작업 실패 시 그 작업이 만든 변경만 되돌리고 기존 사용자 변경은 보존한다.
- quota/login/CLI 대기 상태는 작업 실패 횟수와 클라우드 예산으로 계산하지 않는다.
- 막힌 선행 작업의 후속 작업은 BLOCKED가 아니라 waiting_dependency로 둔다.
- 한 배치 상한에 도달해도 Supervisor가 다음 배치를 이어가되 시간당 클라우드 상한을 지킨다.
"""
from __future__ import annotations

import difflib
import importlib.util
import os
import re
import shutil
import subprocess
import time
from pathlib import Path
from typing import Any

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]


def load_module(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"모듈 로드 실패: {path}")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


AUTODEV = load_module("autodev_v2_core", HERE / "autodev.py")
ORIGINAL_NORMALIZE = AUTODEV.normalize_director_tasks
_LAST_DIRECTOR_META: dict[str, Any] = {"status": "idle", "cloud_used": 0}

AREA_KEYWORDS: dict[str, tuple[str, ...]] = {
    "combat": ("combat", "battle", "attack", "skill", "damage", "enemy", "boss", "전투", "공격", "스킬", "피해", "적", "보스", "투사체"),
    "character": ("character", "player", "job", "class", "stat", "캐릭터", "플레이어", "직업", "스탯", "능력치"),
    "progression": ("progress", "level", "upgrade", "reward", "growth", "성장", "레벨", "강화", "보상", "진행"),
    "items": ("item", "inventory", "equip", "loot", "아이템", "인벤", "장비", "루팅"),
    "ui": ("ui", "hud", "menu", "panel", "button", "화면", "메뉴", "패널", "버튼", "hud"),
    "stage": ("stage", "map", "terrain", "spawn", "wave", "스테이지", "맵", "지형", "스폰", "웨이브"),
    "systems": ("save", "load", "data", "manager", "system", "pool", "저장", "로드", "데이터", "매니저", "시스템", "풀링"),
    "qa": ("test", "verify", "build", "performance", "검증", "테스트", "빌드", "성능", "qa"),
}


def _norm_text(text: str) -> str:
    text = re.sub(r"[^0-9A-Za-z가-힣]+", " ", text.lower())
    return " ".join(text.split())


def _task_text(item: dict[str, Any]) -> str:
    return _norm_text(f"{item.get('title','')} {item.get('goal', item.get('detail',''))}")


def text_similarity(a: str, b: str) -> float:
    a, b = _norm_text(a), _norm_text(b)
    if not a or not b:
        return 0.0
    seq = difflib.SequenceMatcher(None, a, b).ratio()
    sa, sb = set(a.split()), set(b.split())
    jac = len(sa & sb) / max(1, len(sa | sb))
    return max(seq, jac)


def infer_area(item: dict[str, Any]) -> str:
    explicit = _norm_text(str(item.get("area", ""))).replace(" ", "_")
    if explicit in AREA_KEYWORDS:
        return explicit
    text = _task_text(item)
    scores: list[tuple[int, str]] = []
    for area, words in AREA_KEYWORDS.items():
        score = sum(1 for w in words if w.lower() in text)
        scores.append((score, area))
    score, area = max(scores)
    return area if score > 0 else "systems"


def _history_rows(st: dict[str, Any], limit: int) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for key in ("completed", "blocked", "tasks"):
        for row in st.get(key, []):
            if isinstance(row, dict):
                rows.append(row)
    return rows[-limit:]


def recent_areas(st: dict[str, Any], limit: int = 6) -> list[str]:
    rows: list[dict[str, Any]] = []
    rows.extend(x for x in st.get("completed", []) if isinstance(x, dict))
    rows.extend(x for x in st.get("blocked", []) if isinstance(x, dict))
    rows = rows[-limit:]
    return [str(x.get("area") or infer_area(x)) for x in rows]


def area_summary(st: dict[str, Any]) -> str:
    counts = {k: {"done": 0, "blocked": 0} for k in AREA_KEYWORDS}
    for row in st.get("completed", []):
        if isinstance(row, dict):
            a = str(row.get("area") or infer_area(row))
            counts.setdefault(a, {"done": 0, "blocked": 0})["done"] += 1
    for row in st.get("blocked", []):
        if isinstance(row, dict):
            a = str(row.get("area") or infer_area(row))
            counts.setdefault(a, {"done": 0, "blocked": 0})["blocked"] += 1
    return "\n".join(f"- {a}: 완료 {v['done']} / 막힘 {v['blocked']}" for a, v in counts.items())


def guard_director_raw(cfg: dict[str, Any], st: dict[str, Any], raw: Any) -> list[dict[str, Any]]:
    if isinstance(raw, dict):
        raw = raw.get("tasks")
    if not isinstance(raw, list):
        return []

    threshold = float(cfg.get("duplicate_task_similarity", 0.88))
    history_limit = int(cfg.get("dedupe_history_limit", 40))
    history_texts = [_task_text(x) for x in _history_rows(st, history_limit)]
    recent = recent_areas(st, 4)
    default_area_cap = int(cfg.get("max_same_area_per_director_batch", 2))
    same_recent = len(recent) >= 2 and recent[-1] == recent[-2]
    area_cap = 1 if same_recent else default_area_cap

    accepted: list[tuple[int, dict[str, Any], str]] = []
    area_counts: dict[str, int] = {}
    old_to_new: dict[int, int] = {}

    for old_index, item in enumerate(raw[: int(cfg["max_tasks_per_director_batch"])], start=1):
        if not isinstance(item, dict):
            continue
        text = _task_text(item)
        if not text:
            continue
        duplicate = any(text_similarity(text, old) >= threshold for old in history_texts if old)
        duplicate = duplicate or any(text_similarity(text, _task_text(x[1])) >= threshold for x in accepted)
        if duplicate:
            print(f"[ANTI-LOOP] 중복 작업 제거: {item.get('title','(제목 없음)')}")
            continue

        area = infer_area(item)
        if area_counts.get(area, 0) >= area_cap:
            print(f"[ANTI-LOOP] 같은 영역 과다 생성 제거: {area} / {item.get('title','')}")
            continue
        copy = dict(item)
        copy["area"] = area
        accepted.append((old_index, copy, area))
        area_counts[area] = area_counts.get(area, 0) + 1
        old_to_new[old_index] = len(accepted)

    result: list[dict[str, Any]] = []
    for _old_index, item, _area in accepted:
        deps = item.get("depends_on", [])
        remapped: list[int] = []
        if isinstance(deps, list):
            for dep in deps:
                if isinstance(dep, int) and dep in old_to_new:
                    remapped.append(old_to_new[dep])
        item["depends_on"] = remapped
        result.append(item)
    return result


def normalize_guarded_tasks(cfg: dict[str, Any], st: dict[str, Any], raw: Any) -> list[dict[str, Any]]:
    guarded = guard_director_raw(cfg, st, raw)
    if not guarded:
        return []
    tasks = ORIGINAL_NORMALIZE(cfg, st, {"tasks": guarded})
    for task, src in zip(tasks, guarded):
        task["area"] = str(src.get("area") or infer_area(src))
    return tasks


def director_prompt(cfg: dict[str, Any], st: dict[str, Any]) -> str:
    areas = ", ".join(recent_areas(st, 6)) or "없음"
    return f"""당신은 '재와 별' AutoDev의 DIRECTOR다.
코드는 수정하지 말고 다음 개발 작업 묶음만 결정한다.

중요 원칙:
- 계획은 게임 전체 진행을 앞으로 밀어야 한다. 한 기능만 계속 파고들지 마라.
- 최근 작업 영역이 반복됐다면 다른 독립 핵심 영역을 우선한다.
- 완료/막힘/현재 큐와 같은 작업을 이름만 바꿔 다시 만들지 마라.
- BLOCKED 작업과 같은 접근을 반복하지 말고 우회 또는 독립 작업을 만든다.
- 한 작업은 한 세션에 구현/검증 가능한 크기로 쪼갠다.
- 한 묶음에서 같은 area는 최대 2개 정도만 사용한다.
- 문서 정리, 회의록, 상태보고, 장식성 리팩터링보다 플레이 가능한 기능을 우선한다.
- 마지막 작업은 가능하면 통합 확인 작업으로 둔다.

[영역별 이력]
{area_summary(st)}

[최근 작업 영역]
{areas}

[기획서 압축본]
{AUTODEV.compact_design(cfg)}

[안정 지식]
{AUTODEV.compact_handoff(cfg)}

[현재 상태]
{AUTODEV.situation(cfg, st)}

JSON만 출력:
{{
  "goal": "이번 묶음의 한 줄 목표",
  "tasks": [
    {{
      "title": "작업명",
      "goal": "무엇을 구현하는지",
      "area": "combat|character|progression|items|ui|stage|systems|qa 중 하나",
      "done_when": ["검증 가능한 조건1", "조건2"],
      "priority": 1,
      "depends_on": [1],
      "verify_mode": "compile 또는 build",
      "milestone": false
    }}
  ]
}}
"""


def _provider_state(code: int, output: str) -> str:
    text = (output or "").lower()
    if code == 88 or any(x in text for x in (
        "quota", "usage limit", "weekly limit", "usage balance exhausted",
        "payment required", "한도 소진", "한도 대기", "rate limit",
    )):
        return "temporary"
    if code in (125, 126, 127) or any(x in text for x in (
        "not logged in", "login required", "authentication required",
        "cli를 찾을 수 없습니다", "exec error",
    )):
        return "permanent"
    return "ok"


def _cloud_times(st: dict[str, Any]) -> list[float]:
    now = time.time()
    values: list[float] = []
    for x in st.get("cloud_call_times", []):
        try:
            ts = float(x)
        except Exception:
            continue
        if now - ts < 3600:
            values.append(ts)
    st["cloud_call_times"] = values[-100:]
    return values


def cloud_slot_available(cfg: dict[str, Any], st: dict[str, Any]) -> bool:
    return len(_cloud_times(st)) < int(cfg.get("max_cloud_calls_per_hour", 12))


def record_cloud_call(st: dict[str, Any]) -> None:
    values = _cloud_times(st)
    values.append(time.time())
    st["cloud_call_times"] = values[-100:]


def seconds_until_cloud_slot(cfg: dict[str, Any], st: dict[str, Any]) -> int:
    values = _cloud_times(st)
    limit = int(cfg.get("max_cloud_calls_per_hour", 12))
    if len(values) < limit:
        return 0
    return max(1, int(values[0] + 3600 - time.time()))


def director_fill(cfg: dict[str, Any], st: dict[str, Any]) -> bool:
    global _LAST_DIRECTOR_META
    _LAST_DIRECTOR_META = {"status": "failed", "cloud_used": 0}
    if not cloud_slot_available(cfg, st):
        _LAST_DIRECTOR_META = {"status": "hourly_budget", "cloud_used": 0}
        return False

    root = Path(cfg["_repo_root"])
    before_grok = int(st.setdefault("stats", {}).get("grok_calls", 0) or 0)
    before_director = int(st["stats"].get("director_calls", 0) or 0)
    code, out = AUTODEV.grok_call(
        cfg, st, director_prompt(cfg, st), role="director", cwd=root,
        max_turns=int(cfg["grok_director_max_turns"]), allow_edits=False,
    )
    pstate = _provider_state(code, out)
    if pstate != "ok":
        st["stats"]["grok_calls"] = before_grok
        st["stats"]["director_calls"] = before_director
        _LAST_DIRECTOR_META = {"status": "provider_wait" if pstate == "temporary" else "provider_error", "cloud_used": 0}
        print(f"[DIRECTOR] Grok 사용 불가 rc={code}: {out[-700:]}")
        return False

    record_cloud_call(st)
    parsed = AUTODEV.extract_json(out)
    tasks = normalize_guarded_tasks(cfg, st, parsed)
    _LAST_DIRECTOR_META = {"status": "ok" if tasks else "invalid", "cloud_used": 1}
    if not tasks:
        print("[DIRECTOR] 새 작업이 전부 중복/반복이거나 JSON이 유효하지 않습니다.")
        return False
    if isinstance(parsed, dict) and parsed.get("goal"):
        st["goal"] = str(parsed["goal"])[:500]
    st["tasks"].extend(tasks)
    st["last_director_at"] = AUTODEV.now_iso()
    st["last_director_provider"] = "grok"
    print(f"[DIRECTOR:GROK] 새 작업 {len(tasks)}개 생성: " + ", ".join(f"{t['id']}[{t.get('area')}]" for t in tasks))
    return True


def _tracked_dirty(root: Path) -> set[str]:
    paths: set[str] = set()
    for args in (["diff", "--name-only"], ["diff", "--cached", "--name-only"]):
        out = AUTODEV.git_text(root, list(args))
        paths.update(x.strip() for x in out.splitlines() if x.strip())
    return paths


def _untracked(root: Path) -> set[str]:
    out = AUTODEV.git_text(root, ["ls-files", "--others", "--exclude-standard"])
    return {x.strip() for x in out.splitlines() if x.strip()}


def _staged(root: Path) -> set[str]:
    out = AUTODEV.git_text(root, ["diff", "--cached", "--name-only"])
    return {x.strip() for x in out.splitlines() if x.strip()}


def _snapshot_file(root: Path, rel: str) -> tuple[bool, bytes | None]:
    p = root / rel
    if p.is_file():
        try:
            return True, p.read_bytes()
        except Exception:
            return True, None
    return p.exists(), None


def checkpoint(root: Path) -> dict[str, Any]:
    dirty = _tracked_dirty(root)
    untracked = _untracked(root)
    baseline = dirty | untracked
    snapshots = {rel: _snapshot_file(root, rel) for rel in baseline}
    return {
        "dirty": dirty,
        "untracked": untracked,
        "staged": _staged(root),
        "snapshots": snapshots,
    }


def _content_changed(root: Path, rel: str, snap: tuple[bool, bytes | None]) -> bool:
    existed, data = snap
    p = root / rel
    if not existed:
        return p.exists()
    if not p.exists():
        return True
    if data is None or not p.is_file():
        return False
    try:
        return p.read_bytes() != data
    except Exception:
        return True


def task_delta_paths(root: Path, cp: dict[str, Any]) -> set[str]:
    now_dirty = _tracked_dirty(root)
    now_untracked = _untracked(root)
    delta: set[str] = set()
    baseline = set(cp["dirty"]) | set(cp["untracked"])
    for rel in now_dirty:
        if rel not in baseline:
            delta.add(rel)
    for rel in now_untracked:
        if rel not in baseline:
            delta.add(rel)
    for rel, snap in cp["snapshots"].items():
        if _content_changed(root, rel, snap):
            delta.add(rel)
    return delta


def rollback_checkpoint(root: Path, cp: dict[str, Any]) -> list[str]:
    delta = task_delta_paths(root, cp)
    restored: list[str] = []
    baseline_dirty = set(cp["dirty"])
    baseline_untracked = set(cp["untracked"])
    baseline_staged = set(cp["staged"])

    for rel in sorted(delta):
        p = root / rel
        snap = cp["snapshots"].get(rel)
        try:
            if rel in baseline_dirty or rel in baseline_untracked:
                existed, data = snap if snap is not None else (False, None)
                if rel not in baseline_staged:
                    subprocess.run(["git", "restore", "--staged", "--", rel], cwd=root, capture_output=True)
                if existed:
                    if data is not None:
                        p.parent.mkdir(parents=True, exist_ok=True)
                        p.write_bytes(data)
                elif p.exists():
                    if p.is_dir():
                        shutil.rmtree(p)
                    else:
                        p.unlink()
            else:
                if rel in _untracked(root):
                    if p.is_dir():
                        shutil.rmtree(p)
                    elif p.exists():
                        p.unlink()
                else:
                    subprocess.run(
                        ["git", "restore", "--staged", "--worktree", "--source=HEAD", "--", rel],
                        cwd=root, capture_output=True,
                    )
            restored.append(rel)
        except Exception as e:
            print(f"[ROLLBACK] {rel} 복원 실패: {type(e).__name__}: {e}")
    if restored:
        print("[ROLLBACK] 실패 작업 변경 복원: " + ", ".join(restored[:12]))
    return restored


def _project_scope_problem(cfg: dict[str, Any], root: Path, cp: dict[str, Any]) -> str:
    delta = task_delta_paths(root, cp)
    if not delta:
        return "작업 후 실제 파일 변경이 없습니다."
    project_rel = str(Path(cfg["project_root"]).resolve().relative_to(root)).replace("\\", "/") + "/"
    outside = [p for p in delta if not p.replace("\\", "/").startswith(project_rel)]
    if outside:
        return "게임 프로젝트 밖 파일을 수정했습니다: " + ", ".join(outside[:8])
    max_files = int(cfg.get("max_task_changed_files", 8))
    if len(delta) > max_files:
        return f"한 작업에서 파일을 너무 많이 수정했습니다: {len(delta)}개 > {max_files}개"
    return ""


def _safe_worker_prompt(cfg: dict[str, Any], task: dict[str, Any], verify_text: str) -> str:
    return AUTODEV.worker_prompt(cfg, task, verify_text) + """

[AutoDev v2 추가 안전 규칙]
- git add/commit/push/reset/checkout/restore/stash 금지. Git 상태는 읽기만 한다.
- 이 작업과 직접 관련 없는 파일은 수정하지 않는다.
- 완료 조건을 충족하지 못했으면 성공했다고 주장하지 않는다.
- 한 작업에서 넓게 뜯어고치지 말고 가장 작은 수정으로 끝낸다.
"""


def _undo_provider_stat(st: dict[str, Any], key: str, before: int) -> None:
    st.setdefault("stats", {})[key] = before


def safe_execute_one(cfg: dict[str, Any], st: dict[str, Any], task: dict[str, Any], run_stats: dict[str, int]) -> str:
    root = Path(cfg["_repo_root"])
    project = Path(cfg["project_root"])
    cp = checkpoint(root)
    task["status"] = "working"
    task["area"] = str(task.get("area") or infer_area(task))
    AUTODEV.save_state(cfg, st)

    last_verify = str(task.get("last_error", ""))
    before_fp = AUTODEV.diff_fingerprint(root)
    same_failure_count = 0
    temporary_provider_wait = False
    permanent_provider_error = ""

    max_grok = int(cfg["max_grok_attempts_per_task"])
    while int(task.get("attempts_grok", 0)) < max_grok:
        if run_stats["cloud_calls"] >= int(cfg["max_cloud_calls_per_run"]) or not cloud_slot_available(cfg, st):
            rollback_checkpoint(root, cp)
            task["status"] = "pending"
            task["last_error"] = "클라우드 안전 예산에 도달해 다음 배치에서 이어갑니다."
            AUTODEV.save_state(cfg, st)
            return "budget"

        before_stat = int(st.setdefault("stats", {}).get("grok_calls", 0) or 0)
        code, out = AUTODEV.grok_call(
            cfg, st, _safe_worker_prompt(cfg, task, last_verify),
            role="worker", cwd=project,
            max_turns=int(cfg["grok_worker_max_turns"]), allow_edits=True,
        )
        pstate = _provider_state(code, out)
        if pstate != "ok":
            _undo_provider_stat(st, "grok_calls", before_stat)
            if pstate == "temporary":
                temporary_provider_wait = True
                last_verify = f"Grok 사용량/로그인 대기 rc={code}\n{out[-1800:]}"
            else:
                permanent_provider_error = f"Grok 실행 환경 오류 rc={code}\n{out[-1800:]}"
                last_verify = permanent_provider_error
            print("[PROVIDER] Grok 호출은 작업 실패 횟수/예산에서 제외합니다.")
            break

        task["attempts_grok"] = int(task.get("attempts_grok", 0)) + 1
        run_stats["cloud_calls"] += 1
        record_cloud_call(st)
        if code != 0:
            last_verify = f"Grok 실행 실패 rc={code}\n{out[-2000:]}"

        after_fp = AUTODEV.diff_fingerprint(root)
        if after_fp == before_fp:
            last_verify = (last_verify + "\n\n[로컬 판정] 파일 변화 없음. 같은 분석 반복 금지.").strip()
        else:
            scope_problem = _project_scope_problem(cfg, root, cp)
            if scope_problem:
                last_verify = "[로컬 범위 가드] " + scope_problem
                rollback_checkpoint(root, cp)
                before_fp = AUTODEV.diff_fingerprint(root)
            else:
                before_fp = after_fp
                status, vout = AUTODEV.verify_task(cfg, task)
                fp = AUTODEV.verify_fingerprint(status, vout)
                print(f"\n[VERIFY] {status.upper()}\n{vout[-2500:]}")
                if status == "pass":
                    AUTODEV.finish_task(cfg, st, task, vout)
                    return "done"
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
                        print("[ANTI-LOOP] 같은 검증 실패가 반복되어 Grok 재시도를 조기 종료합니다.")
                        break
        task["last_error"] = last_verify
        AUTODEV.save_state(cfg, st)

    max_codex = int(cfg["max_codex_attempts_per_task"])
    while int(task.get("attempts_codex", 0)) < max_codex:
        if run_stats["cloud_calls"] >= int(cfg["max_cloud_calls_per_run"]) or not cloud_slot_available(cfg, st):
            rollback_checkpoint(root, cp)
            task["status"] = "pending"
            task["last_error"] = "클라우드 안전 예산에 도달해 다음 배치에서 이어갑니다."
            AUTODEV.save_state(cfg, st)
            return "budget"

        prompt = _safe_worker_prompt(cfg, task, last_verify) + "\n\nGrok이 이미 실패했다. 같은 접근을 반복하지 말고 원인을 좁혀 다른 최소 수정으로 해결하라."
        before_codex = AUTODEV.diff_fingerprint(root)
        before_stat = int(st.setdefault("stats", {}).get("codex_calls", 0) or 0)
        code, out = AUTODEV.codex_call(cfg, st, prompt, project)
        pstate = _provider_state(code, out)
        if pstate != "ok":
            _undo_provider_stat(st, "codex_calls", before_stat)
            if pstate == "temporary":
                temporary_provider_wait = True
                last_verify = f"Codex 사용량/로그인 대기 rc={code}\n{out[-1800:]}"
            else:
                permanent_provider_error = f"Codex 실행 환경 오류 rc={code}\n{out[-1800:]}"
                last_verify = permanent_provider_error
            print("[PROVIDER] Codex 호출은 작업 실패 횟수/예산에서 제외합니다.")
            break

        task["attempts_codex"] = int(task.get("attempts_codex", 0)) + 1
        run_stats["cloud_calls"] += 1
        record_cloud_call(st)
        after_codex = AUTODEV.diff_fingerprint(root)
        if code != 0:
            last_verify = f"Codex 실행 실패 rc={code}\n{out[-2000:]}"
        elif after_codex == before_codex:
            last_verify = "Codex 실행 후 파일 변화 없음.\n" + out[-1500:]
        else:
            scope_problem = _project_scope_problem(cfg, root, cp)
            if scope_problem:
                last_verify = "[로컬 범위 가드] " + scope_problem
            else:
                status, vout = AUTODEV.verify_task(cfg, task)
                print(f"\n[VERIFY] {status.upper()}\n{vout[-2500:]}")
                if status == "pass":
                    AUTODEV.finish_task(cfg, st, task, vout)
                    return "done"
                last_verify = vout
        task["last_error"] = last_verify
        AUTODEV.save_state(cfg, st)

    rollback_checkpoint(root, cp)
    if temporary_provider_wait:
        task["status"] = "pending"
        task["last_error"] = last_verify or "AI 사용량이 회복되기를 기다립니다."
        AUTODEV.save_state(cfg, st)
        return "waiting_provider"

    AUTODEV.block_task(st, task, last_verify or permanent_provider_error or "최대 재시도 소진")
    return "blocked"


def refresh_dependency_states(st: dict[str, Any]) -> None:
    completed_ids = {str(x.get("id")) for x in st.get("completed", []) if isinstance(x, dict)}
    blocked_ids = {str(x.get("id")) for x in st.get("blocked", []) if isinstance(x, dict)}
    active_ids = {str(x.get("id")) for x in st.get("tasks", []) if isinstance(x, dict)}
    for task in st.get("tasks", []):
        if not isinstance(task, dict):
            continue
        if task.get("status") == "working":
            task["status"] = "pending"
            task["last_error"] = "이전 실행이 작업 중 종료되어 안전하게 다시 시도합니다."
        deps = [str(x) for x in task.get("depends_on", [])]
        blocked = [x for x in deps if x in blocked_ids]
        missing = [x for x in deps if x not in completed_ids and x not in blocked_ids and x not in active_ids]
        if blocked or missing:
            task["status"] = "waiting_dependency"
            task["wait_reason"] = (
                "막힌 선행 작업: " + ", ".join(blocked) if blocked
                else "찾을 수 없는 선행 작업: " + ", ".join(missing)
            )
        elif task.get("status") == "waiting_dependency":
            task["status"] = "pending"
            task.pop("wait_reason", None)


def next_ready(st: dict[str, Any]) -> dict[str, Any] | None:
    completed_ids = {str(x.get("id")) for x in st.get("completed", []) if isinstance(x, dict)}
    candidates: list[dict[str, Any]] = []
    for task in st.get("tasks", []):
        if not isinstance(task, dict) or task.get("status") != "pending":
            continue
        if all(str(dep) in completed_ids for dep in task.get("depends_on", [])):
            task["area"] = str(task.get("area") or infer_area(task))
            candidates.append(task)
    if not candidates:
        return None
    recent = recent_areas(st, 2)
    repeated = recent[-1] if len(recent) >= 2 and recent[-1] == recent[-2] else ""
    candidates.sort(key=lambda t: (
        1 if repeated and t.get("area") == repeated else 0,
        -int(t.get("priority", 0)),
        str(t.get("created_at", "")),
        str(t.get("id", "")),
    ))
    return candidates[0]


def _waiting_signature(st: dict[str, Any]) -> str:
    rows = []
    for task in st.get("tasks", []):
        if isinstance(task, dict) and task.get("status") == "waiting_dependency":
            rows.append(f"{task.get('id')}:{task.get('wait_reason','')}")
    return "|".join(sorted(rows))


def should_replan_waiting(cfg: dict[str, Any], st: dict[str, Any]) -> bool:
    sig = _waiting_signature(st)
    if not sig:
        return True
    old_sig = str(st.get("last_waiting_replan_signature", ""))
    try:
        old_at = float(st.get("last_waiting_replan_at", 0) or 0)
    except Exception:
        old_at = 0.0
    cooldown = int(cfg.get("blocked_replan_cooldown_seconds", 1800))
    if sig == old_sig and time.time() - old_at < cooldown:
        return False
    st["last_waiting_replan_signature"] = sig
    st["last_waiting_replan_at"] = time.time()
    return True


def _pause_seconds(cfg: dict[str, Any], reason: str, no_progress_batches: int, st: dict[str, Any]) -> int:
    if reason == "hourly_budget":
        return min(60, max(5, seconds_until_cloud_slot(cfg, st)))
    if reason == "provider_wait":
        return int(cfg.get("provider_pause_seconds", 60))
    if no_progress_batches >= int(cfg.get("max_no_progress_batches", 2)):
        return int(cfg.get("stalled_pause_seconds", 180))
    return int(cfg.get("supervisor_pause_seconds", 15))


def run_loop(cfg: dict[str, Any], continuous: bool) -> int:
    st = AUTODEV.load_state(cfg)
    refresh_dependency_states(st)
    AUTODEV.save_state(cfg, st)
    supervisor = bool(continuous and cfg.get("supervisor_enabled", False))
    no_progress_batches = 0
    batch_number = 0

    while True:
        batch_number += 1
        run_stats = {"cloud_calls": 0, "tasks": 0}
        batch_progress = False
        reason = "batch_complete"
        print(f"\n[SUPERVISOR] 배치 {batch_number} 시작")

        while True:
            refresh_dependency_states(st)
            AUTODEV.print_status(cfg, st)
            task = next_ready(st)

            if task is None:
                waiting = [t for t in st.get("tasks", []) if isinstance(t, dict) and t.get("status") == "waiting_dependency"]
                pending = [t for t in st.get("tasks", []) if isinstance(t, dict) and t.get("status") == "pending"]
                if pending:
                    reason = "dependency_deadlock"
                    print("[ANTI-LOOP] 실행 가능한 작업이 없고 의존성 교착이 있습니다. 같은 큐를 반복하지 않습니다.")
                    break

                if waiting and not should_replan_waiting(cfg, st):
                    reason = "waiting_dependency"
                    print("[ANTI-LOOP] 같은 막힘 상태에서 Director를 다시 부르지 않고 대기합니다.")
                    break

                if run_stats["cloud_calls"] >= int(cfg["max_cloud_calls_per_run"]):
                    reason = "batch_budget"
                    break
                if not cloud_slot_available(cfg, st):
                    reason = "hourly_budget"
                    break

                ok = director_fill(cfg, st)
                run_stats["cloud_calls"] += int(_LAST_DIRECTOR_META.get("cloud_used", 0) or 0)
                AUTODEV.save_state(cfg, st)
                if ok:
                    continue
                reason = str(_LAST_DIRECTOR_META.get("status", "director_failed"))
                break

            outcome = safe_execute_one(cfg, st, task, run_stats)
            AUTODEV.save_state(cfg, st)
            if outcome in {"done", "blocked"}:
                run_stats["tasks"] += 1
            if outcome == "done":
                batch_progress = True
            if not continuous:
                return 0 if outcome == "done" else 2
            if outcome in {"waiting_provider", "budget"}:
                reason = "provider_wait" if outcome == "waiting_provider" else "batch_budget"
                break
            if run_stats["tasks"] >= int(cfg["max_tasks_per_run"]):
                reason = "batch_task_limit"
                break
            if run_stats["cloud_calls"] >= int(cfg["max_cloud_calls_per_run"]):
                reason = "batch_budget"
                break

        AUTODEV.save_state(cfg, st)
        if not supervisor:
            if reason in {"provider_wait", "waiting_dependency", "hourly_budget", "batch_budget", "batch_task_limit", "batch_complete"}:
                return 0
            return 3

        no_progress_batches = 0 if batch_progress else no_progress_batches + 1
        pause = _pause_seconds(cfg, reason, no_progress_batches, st)
        print(f"[SUPERVISOR] 배치 종료 이유={reason} · {pause}초 후 다시 전체 상태를 보고 다음 배치를 시작합니다.")
        deadline = time.time() + pause
        while time.time() < deadline:
            time.sleep(min(1.0, max(0.05, deadline - time.time())))


def main() -> int:
    AUTODEV.director_fill = director_fill
    AUTODEV.next_ready = next_ready
    AUTODEV.run_loop = run_loop
    AUTODEV.execute_one = safe_execute_one
    return AUTODEV.main()


if __name__ == "__main__":
    raise SystemExit(main())
