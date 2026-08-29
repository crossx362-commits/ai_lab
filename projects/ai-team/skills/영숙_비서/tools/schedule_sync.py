#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""schedules.json → macOS launchd 동기화.

각 정시 잡을 독립 launchd 에이전트로 만든다.

비용 가드:
- **정시/백그라운드 잡은 기본적으로 클라우드 LLM을 사용하지 않는다.**
- `AI_TEAM_ALLOW_CLOUD_LLM=0`, `AI_TEAM_ENABLE_CLAUDE=0`을 launchd 환경에 강제한다.
- 정말 클라우드가 필요한 잡만 schedules.json의 `environment`에서 명시적으로 opt-in 한다.

예:
  "environment": {"AI_TEAM_ALLOW_CLOUD_LLM": "1"}

이렇게 해야 Ollama 일시 실패나 오래된 fallback 코드 때문에 Grok/Codex 주간 한도가
백그라운드에서 조용히 소모되지 않는다.
"""

from __future__ import annotations

import json
import os
import plistlib
import subprocess
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
sys.stderr.reconfigure(encoding="utf-8", errors="replace")

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[4]
SCHEDULES_FILE = SCRIPT_DIR / "schedules.json"
LAUNCH_AGENTS = Path.home() / "Library" / "LaunchAgents"
LOG_DIR = PROJECT_ROOT / "output" / "bot_logs"
LABEL_PREFIX = "com.ailab.sched."
PYBIN = sys.executable


def _parse_field(field: str, lo: int, hi: int) -> list[int]:
    """cron 필드 → 정수 리스트. '*', 'a-b', 'a,b', 'n', 스텝('*/n', 'a-b/n') 지원."""
    field = field.strip()
    out: list[int] = []
    for part in field.split(","):
        step = 1
        if "/" in part:
            part, step_s = part.split("/")
            step = int(step_s)
        if part == "*":
            a, b = lo, hi
        elif "-" in part:
            a_s, b_s = part.split("-")
            a, b = int(a_s), int(b_s)
        else:
            out.append(int(part))
            continue
        out.extend(range(a, b + 1, step))
    return out


def cron_to_calendar(cron: str) -> list[dict]:
    """'m h dom mon dow' → launchd StartCalendarInterval 항목 리스트."""
    m, h, _dom, _mon, dow = cron.split()
    minutes = _parse_field(m, 0, 59)
    hours = _parse_field(h, 0, 23)
    weekdays = None if dow.strip() == "*" else _parse_field(dow, 0, 7)
    entries: list[dict] = []
    for hh in hours:
        for mm in minutes:
            if weekdays is None:
                entries.append({"Hour": hh, "Minute": mm})
            else:
                for wd in weekdays:
                    entries.append({"Hour": hh, "Minute": mm, "Weekday": wd})
    return entries


def _program_args(command: str) -> list[str]:
    """'python projects/.../x.py --a b' → 현재 Python + 절대 스크립트 경로."""
    parts = command.split()
    rel = parts[1]
    args = parts[2:]
    script_abs = str((PROJECT_ROOT / rel).resolve())
    return [PYBIN, "-u", script_abs, *args]


def _job_environment(job: dict) -> dict[str, str]:
    """launchd 잡 환경.

    백그라운드 잡은 기본 cloud-off다. 개별 잡이 정말 필요할 때만
    schedules.json의 `environment`로 명시적 opt-in 한다.
    """
    env = {
        "PYTHONUTF8": "1",
        "HOME": str(Path.home()),
        "AI_TEAM_ALLOW_CLOUD_LLM": "0",
        "AI_TEAM_ENABLE_CLAUDE": "0",
        "AI_TEAM_PROBE_CLOUD": "0",
    }
    extra = job.get("environment") or {}
    if isinstance(extra, dict):
        for key, value in extra.items():
            env[str(key)] = str(value)
    return env


def build_plist(job: dict) -> dict:
    label = LABEL_PREFIX + job["id"]
    return {
        "Label": label,
        "ProgramArguments": _program_args(job["command"]),
        "WorkingDirectory": str(PROJECT_ROOT),
        "EnvironmentVariables": _job_environment(job),
        "StartCalendarInterval": cron_to_calendar(job["cron"]),
        "RunAtLoad": False,
        "StandardOutPath": str(LOG_DIR / f"sched_{job['id']}.out.log"),
        "StandardErrorPath": str(LOG_DIR / f"sched_{job['id']}.err.log"),
    }


def _launchctl(*args: str) -> subprocess.CompletedProcess:
    return subprocess.run(["launchctl", *args], capture_output=True, text=True)


def _load(plist_path: Path) -> None:
    _launchctl("unload", str(plist_path))
    _launchctl("load", "-w", str(plist_path))


def load_jobs() -> list[dict]:
    data = json.loads(SCHEDULES_FILE.read_text(encoding="utf-8-sig"))
    return [j for j in data.get("schedules", []) if j.get("enabled", True)]


def sync() -> None:
    LAUNCH_AGENTS.mkdir(parents=True, exist_ok=True)
    LOG_DIR.mkdir(parents=True, exist_ok=True)
    jobs = load_jobs()
    wanted = set()
    for job in jobs:
        label = LABEL_PREFIX + job["id"]
        wanted.add(label)
        path = LAUNCH_AGENTS / f"{label}.plist"
        path.write_bytes(plistlib.dumps(build_plist(job)))
        _load(path)
        cloud = _job_environment(job).get("AI_TEAM_ALLOW_CLOUD_LLM") == "1"
        print(f"  ✅ {label}  ({job['cron']})  cloud={'ON' if cloud else 'OFF'}")

    for p in LAUNCH_AGENTS.glob(f"{LABEL_PREFIX}*.plist"):
        label = p.stem
        if label not in wanted:
            _launchctl("unload", str(p))
            p.unlink()
            print(f"  🗑️  제거(스케줄에서 사라짐): {label}")
    print(f"\n동기화 완료: {len(wanted)}개 잡 → 독립 launchd 에이전트")


def list_jobs() -> None:
    r = _launchctl("list")
    rows = [ln for ln in r.stdout.splitlines() if LABEL_PREFIX in ln]
    print(f"등록된 잡 {len(rows)}개:")
    for ln in rows:
        print("  " + ln)


def clean() -> None:
    for p in LAUNCH_AGENTS.glob(f"{LABEL_PREFIX}*.plist"):
        _launchctl("unload", str(p))
        p.unlink()
        print(f"  🗑️  {p.stem}")


def selftest() -> int:
    """cron 변환 + 백그라운드 cloud-off 가드 검증."""
    cases = [
        ("0 4 * * *", 1, "매일 04:00"),
        ("30 6 * * 1-5", 5, "평일 06:30"),
        ("0 9-15 * * 1-5", 35, "평일 9~15시 매시"),
        ("0 17 * * 5", 1, "금요일 17:00"),
    ]
    ok = True
    for cron, exp, desc in cases:
        got = len(cron_to_calendar(cron))
        good = got == exp
        ok = ok and good
        print(f"  {'✅' if good else '❌'} {cron:18} → {got}항목 (기대 {exp}) — {desc}")

    default_env = _job_environment({})
    default_off = (
        default_env.get("AI_TEAM_ALLOW_CLOUD_LLM") == "0"
        and default_env.get("AI_TEAM_ENABLE_CLAUDE") == "0"
        and default_env.get("AI_TEAM_PROBE_CLOUD") == "0"
    )
    print(f"  {'✅' if default_off else '❌'} 정시 잡 기본 cloud OFF")
    ok = ok and default_off

    opted = _job_environment({"environment": {"AI_TEAM_ALLOW_CLOUD_LLM": "1"}})
    optin_ok = opted.get("AI_TEAM_ALLOW_CLOUD_LLM") == "1"
    print(f"  {'✅' if optin_ok else '❌'} 명시적 cloud opt-in 지원")
    ok = ok and optin_ok

    return 0 if ok else 1


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else "sync"
    if cmd == "sync":
        sync()
    elif cmd == "list":
        list_jobs()
    elif cmd == "clean":
        clean()
    elif cmd == "selftest":
        sys.exit(selftest())
    else:
        print(__doc__)
        sys.exit(1)
