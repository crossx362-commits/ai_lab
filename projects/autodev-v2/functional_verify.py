#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Task-specific Unity acceptance verification for AutoDev.

Compilation is necessary but not sufficient. Gameplay/system tasks must also provide
an acceptance probe that executes inside the real Unity editor and checks observable
behaviour with AutoDevAssert.
"""
from __future__ import annotations

import importlib.util
import os
import re
import subprocess
import time
from pathlib import Path
from typing import Any

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
TOOLS = REPO / "projects" / "ai-team" / "skills" / "마루_게임개발" / "tools"
RUNNER_REL = Path("Assets/Editor/AutoDevAcceptance/AutoDevAcceptanceRunner.cs")
ACCEPT_DIR_REL = Path("Assets/Editor/AutoDevAcceptance")
DEFAULT_AREAS = {"combat", "character", "progression", "items", "ui", "stage", "system", "systems", "estate", "formation", "raid", "fusion", "class_change"}


class FunctionalVerificationWait(RuntimeError):
    """Environment is temporarily unable to run Unity acceptance verification."""


def _load_game_platform():
    path = TOOLS / "game_platform.py"
    spec = importlib.util.spec_from_file_location("autodev_functional_game_platform", path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"game_platform 로드 실패: {path}")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _task_id(task: dict[str, Any]) -> str:
    raw = str(task.get("id", "TASK"))
    return re.sub(r"[^0-9A-Za-z_]", "_", raw) or "TASK"


def class_name(task: dict[str, Any]) -> str:
    return f"AutoDev_{_task_id(task)}_Acceptance"


def acceptance_rel(task: dict[str, Any]) -> Path:
    return ACCEPT_DIR_REL / f"{class_name(task)}.cs"


def requires_functional(cfg: dict[str, Any], task: dict[str, Any]) -> bool:
    if not bool(cfg.get("functional_verify_enabled", True)):
        return False
    if bool(task.get("milestone", False)):
        return True
    configured = cfg.get("functional_verify_areas", cfg.get("functional_verify_categories"))
    areas = {str(x) for x in configured} if isinstance(configured, list) else DEFAULT_AREAS
    return str(task.get("area", "systems")) in areas


def worker_instructions(cfg: dict[str, Any], task: dict[str, Any]) -> str:
    if not requires_functional(cfg, task):
        return ""
    tid = _task_id(task)
    cls = class_name(task)
    rel = acceptance_rel(task).as_posix()
    done = "\n".join(f"  - {x}" for x in task.get("done_when", [])) or "  - 작업 목표의 실제 동작"
    return f"""

[실제 Unity 기능 검증 필수]
이 작업은 컴파일 성공만으로 완료되지 않는다. 아래 Acceptance 검증을 같이 작성한다.
- 파일: {rel}
- 클래스 이름은 정확히 `{cls}`
- `public static void Run()` 메서드를 만든다.
- 주석에 `AUTODEV_TASK:{tid}`를 넣는다.
- 검증은 `AutoDevAssert.True/False/Equal/NotNull/Greater/Nearly` 중 하나 이상을 사용한다.
- 상수끼리 비교하거나 `True(true)` 같은 가짜 검증은 금지한다.
- 실제 게임 클래스/컴포넌트/데이터/씬을 사용해 아래 완료 조건이 관찰 가능한지 검사한다.
{done}
- 테스트를 통과시키려고 게임 기능을 우회하거나 테스트 전용 분기를 런타임 코드에 넣지 않는다.
- `AutoDevAcceptanceRunner.cs`는 공용 검증기이므로 수정 금지.
- 가능하면 만든 GameObject/임시 에셋은 Run() 끝에서 정리한다.
"""


def environment_ready(cfg: dict[str, Any]) -> tuple[bool, str]:
    if not bool(cfg.get("functional_verify_enabled", True)):
        return True, "기능 검증 비활성"
    project = Path(cfg["project_root"])
    runner = project / RUNNER_REL
    if not runner.exists():
        return False, f"공용 Unity 검증기 없음: {runner}"
    try:
        gp = _load_game_platform()
        unity, note = gp.find_unity(str(project))
        if not unity:
            return False, note
        lock_ok, lock_msg = gp.ensure_no_editor_lock(str(project))
        if not lock_ok:
            return False, lock_msg
        return True, f"{note} · {lock_msg}"
    except Exception as e:
        return False, f"Unity 검증 환경 확인 오류: {type(e).__name__}: {e}"


def _read_source(project: Path, task: dict[str, Any]) -> tuple[Path, str]:
    path = project / acceptance_rel(task)
    try:
        return path, path.read_text(encoding="utf-8", errors="replace")
    except Exception:
        return path, ""


def source_problem(cfg: dict[str, Any], task: dict[str, Any], delta_paths: set[str] | None = None) -> str:
    project = Path(cfg["project_root"])
    path, text = _read_source(project, task)
    tid = _task_id(task)
    cls = class_name(task)
    if not path.exists() or not text.strip():
        return f"작업별 Unity Acceptance 검증 파일이 없습니다: {acceptance_rel(task).as_posix()}"
    if f"AUTODEV_TASK:{tid}" not in text:
        return f"Acceptance 파일에 AUTODEV_TASK:{tid} 표식이 없습니다."
    if not re.search(rf"\bclass\s+{re.escape(cls)}\b", text):
        return f"Acceptance 클래스 이름이 `{cls}`가 아닙니다."
    if not re.search(r"public\s+static\s+void\s+Run\s*\(\s*\)", text):
        return "Acceptance 검증에 public static void Run()이 없습니다."

    assert_count = len(re.findall(r"\bAutoDevAssert\.(?:True|False|Equal|NotNull|Greater|Nearly)\s*\(", text))
    minimum = max(1, int(cfg.get("functional_verify_min_assertions", 1)))
    if assert_count < minimum:
        return f"실제 동작 Assert가 부족합니다: {assert_count}개 < {minimum}개"

    compact = re.sub(r"\s+", "", text).lower()
    fake_patterns = (
        "autodevassert.true(true",
        "autodevassert.false(false",
        "autodevassert.equal(true,true",
        "autodevassert.equal(false,false",
        "autodevassert.equal(1,1",
        "autodevassert.equal(0,0",
        "autodevassert.equal(\"\",\"\"",
    )
    if any(p in compact for p in fake_patterns):
        return "상수끼리 비교하는 가짜 Acceptance 검증을 발견했습니다."

    if delta_paths is not None:
        normalized = {x.replace("\\", "/") for x in delta_paths}
        acceptance_prefix = str((project / ACCEPT_DIR_REL).resolve().relative_to(Path(cfg["_repo_root"]))).replace("\\", "/") + "/"
        production = [p for p in normalized if not p.startswith(acceptance_prefix)]
        if not production:
            return "Acceptance 테스트만 추가했고 실제 게임 코드는 바뀌지 않았습니다."
    return ""


def _result_paths(cfg: dict[str, Any], task: dict[str, Any]) -> tuple[Path, Path]:
    root = Path(cfg["_repo_root"])
    out = root / "output" / "autodev_v2" / "acceptance"
    out.mkdir(parents=True, exist_ok=True)
    tid = _task_id(task)
    return out / f"{tid}.result.txt", out / f"{tid}.unity.log"


def run_unity_acceptance(cfg: dict[str, Any], task: dict[str, Any]) -> tuple[str, str]:
    project = Path(cfg["project_root"])
    ready, reason = environment_ready(cfg)
    if not ready:
        raise FunctionalVerificationWait(reason)

    gp = _load_game_platform()
    unity, note = gp.find_unity(str(project))
    if not unity:
        raise FunctionalVerificationWait(note)

    result_path, log_path = _result_paths(cfg, task)
    for p in (result_path, log_path):
        try:
            p.unlink()
        except OSError:
            pass

    tid = _task_id(task)
    timeout = max(120, int(cfg.get("functional_verify_timeout_seconds", 900)))
    cmd = [
        unity,
        "-batchmode",
        "-quit",
        "-projectPath", str(project),
        "-executeMethod", "AutoDevAcceptanceRunner.Run",
        "-autodevTask", tid,
        "-autodevResult", str(result_path),
        "-logFile", str(log_path),
    ]
    started = time.monotonic()
    try:
        proc = subprocess.run(
            cmd,
            cwd=Path(cfg["_repo_root"]),
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=timeout,
        )
    except subprocess.TimeoutExpired:
        return "fail", f"Unity Acceptance 검증이 {timeout}초 안에 끝나지 않았습니다. 테스트 무한대기 가능성."
    except Exception as e:
        raise FunctionalVerificationWait(f"Unity Acceptance 실행 오류: {type(e).__name__}: {e}") from e

    elapsed = time.monotonic() - started
    try:
        result = result_path.read_text(encoding="utf-8", errors="replace").strip()
    except Exception:
        result = ""
    try:
        log = log_path.read_text(encoding="utf-8", errors="replace")
    except Exception:
        log = (proc.stdout or "") + "\n" + (proc.stderr or "")

    if result.startswith("PASS") and proc.returncode == 0:
        detail = result.splitlines()[1:] if result else []
        extra = (" · " + " / ".join(detail[:3])) if detail else ""
        return "pass", f"Unity Acceptance PASS · {tid} · {elapsed:.1f}초{extra}"

    low = (result + "\n" + log[-6000:]).lower()
    if any(x in low for x in (
        "another unity instance is running",
        "project is already open",
        "project is locked",
        "licensingclient",
        "failed to update license",
    )):
        raise FunctionalVerificationWait("Unity가 잠겨 있거나 라이선스 확인이 끝나지 않아 기능 검증을 잠시 기다립니다.")

    summary = result or "Unity 결과 파일이 생성되지 않았습니다."
    errors = [ln.strip() for ln in log.splitlines() if "error" in ln.lower() or "exception" in ln.lower()]
    tail = "\n".join(errors[-8:])
    return "fail", f"Unity Acceptance FAIL · rc={proc.returncode} · {summary[-1800:]}" + (f"\n{tail[-1800:]}" if tail else "")


def verify_functional(
    cfg: dict[str, Any],
    task: dict[str, Any],
    delta_paths: set[str] | None = None,
) -> tuple[str, str]:
    if not requires_functional(cfg, task):
        return "pass", "이 작업 영역은 별도 Unity Acceptance 검증 대상이 아닙니다."
    problem = source_problem(cfg, task, delta_paths)
    if problem:
        return "fail", "[FUNCTIONAL] " + problem
    return run_unity_acceptance(cfg, task)
