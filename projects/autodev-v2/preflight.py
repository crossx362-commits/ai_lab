#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AutoDev v2 시작 전 안전/비용 구조 감사.

프롬프트가 아니라 코드로 다음을 강제한다.
- v1 게임 회의/상시감사 스케줄이 꺼져 있어야 한다.
- autopilot 강제연장 목록이 비어 있어야 한다.
- Claude 직접 호출은 기본 비활성이어야 한다.
- v2 컨텍스트/호출 상한이 비정상적으로 풀려 있지 않아야 한다.
"""
from __future__ import annotations

import json
import os
import subprocess
import sys
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


def repo_root() -> Path:
    r = subprocess.run(["git", "rev-parse", "--show-toplevel"], cwd=HERE,
                       capture_output=True, text=True, encoding="utf-8", errors="replace")
    if r.returncode:
        raise RuntimeError("Git 저장소 루트를 찾지 못했습니다.")
    return Path(r.stdout.strip()).resolve()


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

    # Claude는 사용자가 명시적으로 다시 켜기 전까지 기본 비활성이어야 한다.
    if os.getenv("AI_TEAM_ENABLE_CLAUDE", "0").strip().lower() in {"1", "true", "yes", "on"}:
        problems.append("AI_TEAM_ENABLE_CLAUDE가 활성화되어 있음")

    return problems


def main() -> int:
    problems = check()
    if problems:
        print("[PREFLIGHT] FAIL")
        for p in problems:
            print(" - " + p)
        return 1
    print("[PREFLIGHT] PASS — v1 토큰 소모 루프 OFF / v2 예산 가드 정상")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
