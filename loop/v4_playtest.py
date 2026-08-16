#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""V4 외부 테스터 10명 — 키트 로드·세션 실행·보고서.

    python3 loop/v4_playtest.py            # 키트만
    python3 loop/v4_playtest.py --run      # unity_meas에서 10세션

사람 70% 계속은 여기서 닫지 않는다.
"""
from __future__ import annotations

import json
import os
import subprocess
import sys
from datetime import datetime, timedelta
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
KIT = HERE / "v4_testers.json"
OUT = ROOT / "output" / "qa" / "ashes-to-stars" / "v4_playtest"
SESSIONS = OUT / "sessions.json"
REPORT = OUT / "report.json"
UNITY = Path(os.getenv(
    "UNITY_BIN",
    "/Applications/Unity/Hub/Editor/6000.5.6f1/Unity.app/Contents/MacOS/Unity",
))
MEAS = ROOT / "projects" / "ashes-to-stars" / "unity_meas"
SYNC = ROOT / "projects" / "ashes-to-stars" / "sync_meas.sh"


def load_kit() -> dict:
    data = json.loads(KIT.read_text(encoding="utf-8"))
    testers = data.get("testers") or []
    if len(testers) != 10:
        raise ValueError(f"테스터는 10명이어야 한다 (실제 {len(testers)})")
    ids = [t["id"] for t in testers]
    if len(set(ids)) != 10:
        raise ValueError("테스터 id가 겹친다")
    names = [t["favorite"] for t in testers]
    if len(set(names)) != 10:
        raise ValueError("애착 캐릭터 이름이 겹친다")
    return data


def load_sessions() -> dict | None:
    if not SESSIONS.is_file():
        return None
    try:
        return json.loads(SESSIONS.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        return None


def write_report(kit: dict, sessions: dict | None) -> dict:
    OUT.mkdir(parents=True, exist_ok=True)
    rows = (sessions or {}).get("sessions") or []
    deleted = sum(1 for r in rows if r.get("deleted"))
    continued = sum(1 for r in rows if r.get("continued"))
    report = {
        "gate": "V4",
        "testers": len(kit["testers"]),
        "ran_at": (sessions or {}).get("ran_at") or "",
        "deleted": deleted,
        "continued": continued,
        "human_70": "pending",
        "recheck_after": (
            datetime.now() + timedelta(hours=int(kit.get("recheck_hours") or 24))
        ).strftime("%Y-%m-%d %H:%M"),
        "note": "10세션은 삭제 경로 실측이다. 70% 계속은 사람 응답이 오기 전에 닫지 않는다.",
        "sessions": rows or kit["testers"],
    }
    REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    return report


def run_unity() -> int:
    if not UNITY.is_file():
        print("Unity 실행 파일이 없다:", UNITY)
        return 2
    if SYNC.is_file():
        subprocess.run(["bash", str(SYNC)], check=False)
    OUT.mkdir(parents=True, exist_ok=True)
    log = OUT / "unity.log"
    cmd = [
        str(UNITY), "-batchmode", "-quit", "-nographics",
        "-projectPath", str(MEAS),
        "-executeMethod", "AshesToStars.V4ExternalPlaytest.Run",
        "-logFile", str(log),
    ]
    print("▶", " ".join(cmd))
    r = subprocess.run(cmd, check=False)
    print("unity exit", r.returncode)
    return r.returncode


def main() -> int:
    kit = load_kit()
    do_run = "--run" in sys.argv
    code = 0
    if do_run:
        code = run_unity()
    sessions = load_sessions()
    report = write_report(kit, sessions)
    print(f"테스터 {report['testers']}명 · 삭제 {report['deleted']} · 계속경로 {report['continued']} · 사람70% {report['human_70']}")
    print("보고서", REPORT)
    return 0 if (not do_run or (code == 0 and report["deleted"] == 10)) else 1


if __name__ == "__main__":
    raise SystemExit(main())
