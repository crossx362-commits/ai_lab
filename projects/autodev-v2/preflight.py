#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AutoDev v2 시작 전 안전/비용/CLI 호환성 감사.

프롬프트가 아니라 코드로 다음을 강제한다.
- v1 게임 회의/상시감사 스케줄이 꺼져 있어야 한다.
- autopilot 강제연장 목록/Stop 훅이 없어야 한다.
- Claude 직접 호출은 기본 비활성이어야 한다.
- v2 컨텍스트/호출 상한이 비정상적으로 풀려 있지 않아야 한다.
- Grok의 핵심 headless/수정 옵션은 반드시 있어야 한다.
- 실제 Unity Acceptance 검증기가 존재하고 안전한 설정이어야 한다.
- 버전별 절약 옵션은 없더라도 호환 래퍼가 생략하므로 경고만 한다.

Grok 검사는 `--help`/`version`만 사용하므로 모델 토큰을 소비하지 않는다.
"""
from __future__ import annotations

import importlib.util
import json
import os
import subprocess
from pathlib import Path

HERE = Path(__file__).resolve().parent

HEAVY_IDS = {
    "game_council",
    "game_agents_priority",
    "game_image_quality",
    "game_agent_bomi",
    "game_agent_suri",
    "game_agent_baekho",
    "game_agent_mio",
    "game_agent_teo",
}

GROK_REQUIRED_FLAGS = (
    "--cwd",
    "--output-format",
    "--max-turns",
)

GROK_OPTIONAL_SAVING_FLAGS = (
    "--no-plan",
    "--no-subagents",
    "--no-memory",
    "--disable-web-search",
)

_LAST_GROK_WARNINGS: list[str] = []


def repo_root() -> Path:
    r = subprocess.run(["git", "rev-parse", "--show-toplevel"], cwd=HERE,
                       capture_output=True, text=True, encoding="utf-8", errors="replace")
    if r.returncode:
        raise RuntimeError("Git 저장소 루트를 찾지 못했습니다.")
    return Path(r.stdout.strip()).resolve()


def _load_autodev_module():
    spec = importlib.util.spec_from_file_location("autodev_v2_preflight", HERE / "autodev.py")
    if spec is None or spec.loader is None:
        raise RuntimeError("autodev.py 로드 실패")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _real_grok(mod) -> str | None:
    pinned = os.getenv("AUTODEV_REAL_GROK", "").strip()
    if pinned and Path(pinned).exists():
        return pinned
    return mod.find_grok_cli()


def _successful_help(exe: str) -> str:
    for cmd in ([exe, "--no-auto-update", "--help"], [exe, "--help"]):
        try:
            r = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                timeout=15,
                encoding="utf-8",
                errors="replace",
            )
            text = ((r.stdout or "") + "\n" + (r.stderr or "")).strip()
            if r.returncode == 0 and text:
                return text
        except Exception:
            continue
    return ""


def check_grok_cli() -> list[str]:
    global _LAST_GROK_WARNINGS
    _LAST_GROK_WARNINGS = []
    problems: list[str] = []

    try:
        mod = _load_autodev_module()
        exe = _real_grok(mod)
    except Exception as e:
        return [f"Grok CLI 검사기 로드 실패: {e}"]

    if not exe:
        return ["grok CLI를 찾지 못함. 설치/로그인 상태를 확인하세요."]

    help_text = _successful_help(exe)
    if not help_text:
        return ["grok --help 출력 확인 실패"]

    if "--single" not in help_text and "-p" not in help_text:
        problems.append("Grok CLI에 headless -p/--single 옵션이 없음")

    for flag in GROK_REQUIRED_FLAGS:
        if flag not in help_text:
            problems.append(f"Grok CLI 핵심 옵션 미지원: {flag}")

    if "--always-approve" not in help_text and "--yolo" not in help_text:
        problems.append("Grok CLI Worker 자동수정 옵션(--always-approve/--yolo) 미지원")

    missing_optional = [f for f in GROK_OPTIONAL_SAVING_FLAGS if f not in help_text]
    if missing_optional:
        _LAST_GROK_WARNINGS.append(
            "현재 Grok CLI 미지원 선택 절약 옵션: " + ", ".join(missing_optional)
            + " — AutoDev 호환 모드에서 자동 생략"
        )

    try:
        source = (HERE / "autodev.py").read_text(encoding="utf-8", errors="replace")
        if 'cmd += ["--agent-profile"' in source:
            problems.append("AutoDev 실행 argv에 폐기된 --agent-profile이 다시 들어옴")
    except Exception as e:
        problems.append(f"autodev.py CLI 소스 검사 실패: {e}")

    if not (HERE / "grok_compat.py").exists():
        problems.append("Grok 버전 호환 래퍼(grok_compat.py) 없음")

    return problems


def check_functional_verify(root: Path, cfg: dict) -> list[str]:
    problems: list[str] = []
    if not cfg.get("functional_verify_enabled", True):
        problems.append("functional_verify_enabled가 꺼져 있어 컴파일만으로 완료될 수 있음")
        return problems

    if not (HERE / "functional_verify.py").exists():
        problems.append("작업별 실제 기능 검증기(functional_verify.py) 없음")

    project_raw = str(cfg.get("project_root", ""))
    project = Path(project_raw)
    if not project.is_absolute():
        project = root / project
    acceptance_runner = project / "Assets/Editor/AutoDevAcceptance/AutoDevAcceptanceRunner.cs"
    if not acceptance_runner.exists():
        problems.append("Unity 공용 Acceptance 실행기 없음: " + str(acceptance_runner))

    areas = cfg.get("functional_verify_areas")
    if not isinstance(areas, list) or not areas:
        problems.append("functional_verify_areas가 비어 있음")
    else:
        essential = {"combat", "character", "progression", "items", "ui", "stage"}
        missing = sorted(essential - {str(x) for x in areas})
        if missing:
            problems.append("실제 기능 검증에서 핵심 게임 영역 누락: " + ", ".join(missing))

    try:
        wait = int(cfg.get("functional_verify_wait_seconds", 0))
        if wait < 30:
            problems.append("Unity 기능검증 재시도 간격이 30초보다 짧아 반복 실행 위험")
    except Exception:
        problems.append("functional_verify_wait_seconds가 숫자가 아님")

    try:
        timeout = int(cfg.get("functional_verify_timeout_seconds", 0))
        if timeout < 120 or timeout > 1800:
            problems.append("Unity 기능검증 timeout은 120~1800초 범위여야 함")
    except Exception:
        problems.append("functional_verify_timeout_seconds가 숫자가 아님")

    return problems


def check() -> list[str]:
    root = repo_root()
    problems: list[str] = []

    cfg_path = HERE / "config.json"
    try:
        cfg = json.loads(cfg_path.read_text(encoding="utf-8"))
    except Exception as e:
        return [f"config.json 읽기 실패: {e}"]

    if cfg.get("handoff_file") != "projects/autodev-v2/KNOWLEDGE.md":
        problems.append("handoff_file이 v2 KNOWLEDGE.md가 아님")
    if int(cfg.get("max_candidate_files", 999)) > 5:
        problems.append("관련 파일 상한이 5개보다 큼")
    if int(cfg.get("max_context_chars", 999999)) > 16000:
        problems.append("컨텍스트 상한이 16000자를 초과")
    if int(cfg.get("max_cloud_calls_per_run", 999)) > 12:
        problems.append("실행당 클라우드 호출 상한이 12회를 초과")
    if int(cfg.get("max_cloud_calls_per_hour", 999)) > 18:
        problems.append("시간당 클라우드 호출 상한이 18회를 초과")
    if int(cfg.get("max_grok_attempts_per_task", 999)) > 2:
        problems.append("작업당 Grok 시도가 2회를 초과")
    if int(cfg.get("max_codex_attempts_per_task", 999)) > 1:
        problems.append("작업당 Codex 시도가 1회를 초과")

    if not (HERE / "CORE_RULES.md").exists():
        problems.append("CORE_RULES.md 없음")
    if not (HERE / "KNOWLEDGE.md").exists():
        problems.append("KNOWLEDGE.md 없음")

    problems.extend(check_functional_verify(root, cfg))

    sched = root / "projects/ai-team/skills/영숙_비서/tools/schedules.json"
    if sched.exists():
        try:
            data = json.loads(sched.read_text(encoding="utf-8-sig"))
            for job in data.get("schedules", []):
                jid = str(job.get("id", ""))
                if jid in HEAVY_IDS or jid.startswith("game_agent_"):
                    if job.get("enabled", True) or job.get("run", True):
                        problems.append(f"v1 스케줄 활성: {jid}")
        except Exception as e:
            problems.append(f"schedules.json 읽기 실패: {e}")

    opt = root / ".claude/autopilot_sessions.txt"
    if opt.exists():
        active = [ln.strip() for ln in opt.read_text(encoding="utf-8", errors="replace").splitlines()
                  if ln.strip() and not ln.lstrip().startswith("#")]
        if active:
            problems.append("autopilot 강제연장 세션이 남아 있음")

    hooks = root / ".codex/hooks.json"
    if hooks.exists():
        try:
            hook_data = json.loads(hooks.read_text(encoding="utf-8"))
            stop_hooks = (hook_data.get("hooks") or {}).get("Stop") or []
            if stop_hooks:
                text = json.dumps(stop_hooks, ensure_ascii=False)
                if "autopilot_stop_hook.py" in text:
                    problems.append("Codex Stop 훅에 autopilot 강제연장이 남아 있음")
        except Exception as e:
            problems.append(f".codex/hooks.json 읽기 실패: {e}")

    if os.getenv("AI_TEAM_ENABLE_CLAUDE", "0").strip().lower() in {"1", "true", "yes", "on"}:
        problems.append("AI_TEAM_ENABLE_CLAUDE가 활성화되어 있음")

    problems.extend(check_grok_cli())
    return problems


def _grok_version_text() -> str:
    try:
        mod = _load_autodev_module()
        exe = _real_grok(mod)
        if not exe:
            return "미발견"
        for cmd in ([exe, "version"], [exe, "--version"]):
            r = subprocess.run(cmd, capture_output=True, text=True, timeout=10,
                               encoding="utf-8", errors="replace")
            text = ((r.stdout or "") + " " + (r.stderr or "")).strip()
            if text and r.returncode == 0:
                return text.splitlines()[0][:160]
    except Exception:
        pass
    return "확인 불가"


def main() -> int:
    problems = check()

    for warning in _LAST_GROK_WARNINGS:
        print(f"[PREFLIGHT] WARN — {warning}")

    if problems:
        print("[PREFLIGHT] FAIL")
        for p in problems:
            print(" - " + p)
        if any("Grok" in p or "grok" in p for p in problems):
            print("\nGrok 핵심 CLI 기능이 빠져 있습니다. `grok update` 또는 재설치를 확인하세요.")
        return 1

    print(f"[PREFLIGHT] Grok: {_grok_version_text()}")
    print("[PREFLIGHT] PASS — CLI / Anti-Loop / 비용 가드 / 실제 Unity Acceptance 검증 준비 정상")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
