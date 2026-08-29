#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AutoDev v2 시작 전 안전/비용/CLI 호환성 감사.

프롬프트가 아니라 코드로 다음을 강제한다.
- v1 게임 회의/상시감사 스케줄이 꺼져 있어야 한다.
- autopilot 강제연장 목록/Stop 훅이 없어야 한다.
- Claude 직접 호출은 기본 비활성이어야 한다.
- v2 컨텍스트/호출 상한이 비정상적으로 풀려 있지 않아야 한다.
- 설치된 Grok CLI가 v2 절약형 headless 옵션을 실제 지원해야 한다.

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
    "--no-plan",
    "--no-subagents",
    "--no-memory",
    "--disable-web-search",
)


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


def check_grok_cli() -> list[str]:
    """실제 설치된 Grok CLI가 v2가 쓰는 옵션과 맞는지 0토큰으로 검사한다."""
    problems: list[str] = []
    try:
        mod = _load_autodev_module()
        exe = mod.find_grok_cli()
    except Exception as e:
        return [f"Grok CLI 검사기 로드 실패: {e}"]

    if not exe:
        return ["grok CLI를 찾지 못함. 설치/로그인 상태를 확인하세요."]

    help_text = mod.grok_help(exe)
    if not help_text:
        return ["grok --help 출력 확인 실패"]

    if "--single" not in help_text and "-p" not in help_text:
        problems.append("Grok CLI에 headless -p/--single 옵션이 없음")
    for flag in GROK_REQUIRED_FLAGS:
        if flag not in help_text:
            problems.append(f"Grok CLI 필수 옵션 미지원: {flag}")
    if "--always-approve" not in help_text and "--yolo" not in help_text:
        problems.append("Grok CLI Worker 자동수정 옵션(--always-approve/--yolo) 미지원")

    try:
        probe = mod.build_grok_command(
            exe,
            "AutoDev v2 CLI 호환성 검사. 실제 모델 호출은 하지 않음.",
            HERE,
            max_turns=1,
            allow_edits=False,
            help_text=help_text,
        )
        if "--agent-profile" in probe:
            problems.append("AutoDev가 폐기된 --agent-profile 옵션을 생성함")
    except Exception as e:
        problems.append(f"Grok 명령 생성 실패: {e}")

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
    if int(cfg.get("max_grok_attempts_per_task", 999)) > 2:
        problems.append("작업당 Grok 시도가 2회를 초과")
    if int(cfg.get("max_codex_attempts_per_task", 999)) > 1:
        problems.append("작업당 Codex 시도가 1회를 초과")

    if not (HERE / "CORE_RULES.md").exists():
        problems.append("CORE_RULES.md 없음")
    if not (HERE / "KNOWLEDGE.md").exists():
        problems.append("KNOWLEDGE.md 없음")

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
        exe = mod.find_grok_cli()
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
    if problems:
        print("[PREFLIGHT] FAIL")
        for p in problems:
            print(" - " + p)
        if any("Grok" in p or "grok" in p for p in problems):
            print("\nGrok CLI 문제면 먼저 `grok update` 후 다시 실행하세요.")
        return 1
    print(f"[PREFLIGHT] Grok: {_grok_version_text()}")
    print("[PREFLIGHT] PASS — CLI 호환 / v1 루프 OFF / Stop 강제연장 OFF / v2 예산 가드 정상")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
