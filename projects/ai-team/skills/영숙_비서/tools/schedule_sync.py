#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""스케줄 → launchd 동기화 (SPOF 제거의 근본 해법).

schedules.json을 단일 진실원천으로 두고, 각 잡을 **독립 launchd 에이전트**로
materialize 한다. 커스텀 스케줄러 루프(단일 프로세스 = shared fate SPOF)를 없애고,
OS 네이티브 스케줄러(launchd)가 잡마다 따로 실행한다. 한 잡이 죽어도 나머지는 무관하며,
sleep/재부팅 후 누락분도 launchd가 알아서 실행한다.

사용:
  schedule_sync.py sync      # schedules.json → 잡별 plist 생성·로드, 사라진 잡 정리
  schedule_sync.py list      # 현재 등록된 com.ailab.sched.* 표시
  schedule_sync.py clean     # 모든 com.ailab.sched.* 제거
  schedule_sync.py selftest  # cron→launchd 변환 검증(부팅·대기 없이)
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
    """cron 필드 → 정수 리스트. '*'·'a-b'·'a,b'·'n'·스텝('*/n'·'a-b/n') 지원."""
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
    """'m h dom mon dow' → launchd StartCalendarInterval 항목 리스트.
    cron·launchd 모두 0=일요일이라 요일 매핑 불필요. dom/mon은 현재 스케줄상 항상 '*'."""
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
    """'python projects/.../x.py --a b' → [PYBIN, '-u', <abs>, '--a', 'b']."""
    parts = command.split()
    # parts[0] == python|python3 → 실제 인터프리터로 치환
    rel = parts[1]
    args = parts[2:]
    script_abs = str((PROJECT_ROOT / rel).resolve())
    return [PYBIN, "-u", script_abs, *args]


def build_plist(job: dict) -> dict:
    label = LABEL_PREFIX + job["id"]
    return {
        "Label": label,
        "ProgramArguments": _program_args(job["command"]),
        "WorkingDirectory": str(PROJECT_ROOT),
        "EnvironmentVariables": {"PYTHONUTF8": "1", "HOME": str(Path.home())},
        "StartCalendarInterval": cron_to_calendar(job["cron"]),
        "RunAtLoad": False,  # 로드 시 즉시 실행 금지 — 캘린더 시각에만
        "StandardOutPath": str(LOG_DIR / f"sched_{job['id']}.out.log"),
        "StandardErrorPath": str(LOG_DIR / f"sched_{job['id']}.err.log"),
    }


def _launchctl(*args: str) -> subprocess.CompletedProcess:
    return subprocess.run(["launchctl", *args], capture_output=True, text=True)


def _load(plist_path: Path) -> None:
    _launchctl("unload", str(plist_path))  # 멱등 — 없으면 무시
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
        print(f"  ✅ {label}  ({job['cron']})")
    # schedules.json에서 사라진 잡 정리 (stale plist 제거)
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
    """cron→launchd 변환 정확성 검증 (대기·부팅 없이)."""
    cases = [
        ("0 4 * * *", 24 * 1, "매일 04:00 → 요일 없음(7일)"),
        ("30 6 * * 1-5", 5, "평일 06:30 → 5개"),
        ("0 9-15 * * 1-5", 7 * 5, "평일 9~15시 매시 → 35개"),
        ("0 17 * * 5", 1, "금요일 17:00 → 1개"),
    ]
    # '0 4 * * *' → weekday 없음, hour 1개 → 1개 항목(요일 미지정=매일)
    expect = {"0 4 * * *": 1, "30 6 * * 1-5": 5, "0 9-15 * * 1-5": 35, "0 17 * * 5": 1}
    ok = True
    for cron, _n, desc in cases:
        got = len(cron_to_calendar(cron))
        exp = expect[cron]
        mark = "✅" if got == exp else "❌"
        if got != exp:
            ok = False
        print(f"  {mark} {cron:18} → {got}항목 (기대 {exp}) — {desc}")
    # 평일 9시가 실제로 들어갔는지 표본 확인
    nine = cron_to_calendar("0 9-15 * * 1-5")
    has_mon9 = {"Hour": 9, "Minute": 0, "Weekday": 1} in nine
    print(f"  {'✅' if has_mon9 else '❌'} 월요일 09:00 항목 포함")
    return 0 if (ok and has_mon9) else 1


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
