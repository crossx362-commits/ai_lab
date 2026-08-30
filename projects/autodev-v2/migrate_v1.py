#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AutoDev v1의 토큰 소모형 게임 루프를 안전하게 끄는 전환 도구."""
from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
from datetime import datetime
from pathlib import Path

HERE = Path(__file__).resolve().parent


def root() -> Path:
    r = subprocess.run(["git", "rev-parse", "--show-toplevel"], cwd=HERE,
                       capture_output=True, text=True, encoding="utf-8", errors="replace")
    if r.returncode:
        raise RuntimeError("Git 루트를 찾지 못했습니다.")
    return Path(r.stdout.strip()).resolve()


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


def patch_schedule(data: dict) -> list[str]:
    changed = []
    for job in data.get("schedules", []):
        jid = str(job.get("id", ""))
        if jid in HEAVY_IDS or jid.startswith("game_agent_"):
            if job.get("enabled", True) or job.get("run", True):
                job["enabled"] = False
                job["run"] = False
                desc = str(job.get("description", ""))
                marker = "[AutoDev v2 전환: 상시 LLM 점검 비활성]"
                if marker not in desc:
                    job["description"] = (desc + " " + marker).strip()
                changed.append(jid)
    return changed


def _archive_if_exists(path: Path, backup_dir: Path, name: str) -> None:
    if not path.exists():
        return
    dst = backup_dir / name
    shutil.move(str(path), str(dst))
    print(f"레거시 상태 격리: {path} -> {dst}")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true", help="실제로 변경")
    args = ap.parse_args()

    repo = root()
    sched = repo / "projects/ai-team/skills/영숙_비서/tools/schedules.json"
    data = None
    changed: list[str] = []
    if sched.exists():
        data = json.loads(sched.read_text(encoding="utf-8-sig"))
        changed = patch_schedule(data)
    print("비활성 대상:", ", ".join(changed) if changed else "(이미 비활성 또는 없음)")

    if not args.apply:
        print("DRY RUN입니다. 적용하려면 --apply")
        return 0

    ts = datetime.now().strftime("%Y%m%d_%H%M%S")
    backup_dir = repo / "output/autodev_v2/backups" / ts
    backup_dir.mkdir(parents=True, exist_ok=True)

    if sched.exists() and data is not None:
        shutil.copy2(sched, backup_dir / "schedules.json")
        sched.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    opt = repo / ".claude/autopilot_sessions.txt"
    if opt.exists():
        shutil.copy2(opt, backup_dir / "autopilot_sessions.txt")
        opt.write_text("# AutoDev v2 전환으로 비움 — 턴 강제연장 사용 안 함\n", encoding="utf-8")
        print("autopilot 강제연장 목록 해제")

    qa = repo / "output/qa/ashes-to-stars"
    _archive_if_exists(qa / "ORDERS.md", backup_dir, "ORDERS.md")
    _archive_if_exists(qa / "autopilot_state.json", backup_dir, "autopilot_state.json")

    sync = repo / "projects/ai-team/skills/영숙_비서/tools/schedule_sync.py"
    if sys.platform == "darwin" and sync.exists():
        print("launchd 스케줄 동기화 중...")
        try:
            r = subprocess.run(
                [sys.executable, str(sync), "sync"],
                cwd=repo,
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
            )
            if r.returncode != 0:
                detail = ((r.stderr or "") + "\n" + (r.stdout or "")).strip()[-800:]
                print("경고: schedule_sync 실패. schedules.json 변경은 이미 적용됐습니다.")
                if detail:
                    print(detail)
                print("경고: launchd 동기 실패는 엔진 시작을 막지 않습니다.")
        except Exception as e:
            print(f"경고: schedule_sync 실행 오류 {type(e).__name__}: {e}")
            print("경고: launchd 동기 실패는 엔진 시작을 막지 않습니다.")

    print("AutoDev v1 게임 회의/상시감사/ORDERS/autopilot 상태를 비활성·격리했습니다.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
