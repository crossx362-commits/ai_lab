#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""output 로그 정리 — 비대 로그 회전(copytruncate) + 사어(死語) 로그 삭제.

reports_manager.py cleanup은 reports/ 폴더만 다루고 output/trading_logs·bot_logs는
무한 증식하던 사각지대(9MB+ err 로그, 제거된 옛 에이전트 로그 잔존)를 메운다.

정책:
  - 크기 > MAX_MB: 꼬리 TAIL_LINES줄을 <이름>.1 로 보존 후 원본은 제자리 truncate(0)
    (데몬이 열어둔 fd를 깨지 않는 copytruncate 방식 — 새 inode 생성 금지)
  - 수정된 지 STALE_DAYS일 초과: 삭제 (제거된 에이전트의 잔존 로그)
  - .1 아카이브도 STALE_DAYS 지나면 삭제

스케줄: schedules.json 'log_janitor' (매일 04:10, launchd via schedule_sync)
"""
import os
import sys
import time

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, "output")):
        break
    _root = os.path.dirname(_root)

LOG_DIRS = [
    os.path.join(_root, "output", "trading_logs"),
    os.path.join(_root, "output", "bot_logs"),
]
MAX_MB = 5
TAIL_LINES = 2000
STALE_DAYS = 45


def _rotate(path: str) -> str:
    """꼬리 보존 후 제자리 truncate — 열린 append fd 안전."""
    keep = ""
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as f:
            keep = "".join(f.readlines()[-TAIL_LINES:])
    except Exception:
        pass
    if keep:
        with open(path + ".1", "w", encoding="utf-8") as f:
            f.write(keep)
    with open(path, "r+") as f:
        f.truncate(0)
    return f"회전 {os.path.basename(path)}"


def run() -> list[str]:
    acts: list[str] = []
    now = time.time()
    for d in LOG_DIRS:
        if not os.path.isdir(d):
            continue
        for name in sorted(os.listdir(d)):
            path = os.path.join(d, name)
            if not os.path.isfile(path):
                continue
            try:
                st = os.stat(path)
                if now - st.st_mtime > STALE_DAYS * 86400:
                    os.remove(path)
                    acts.append(f"삭제(사어 {STALE_DAYS}일+) {name}")
                elif st.st_size > MAX_MB * 1024 * 1024 and not name.endswith(".1"):
                    acts.append(_rotate(path))
            except Exception as e:
                acts.append(f"실패 {name}: {e}")
    return acts


if __name__ == "__main__":
    actions = run()
    if actions:
        print(f"log_janitor: {len(actions)}건")
        for a in actions:
            print("  - " + a)
    else:
        print("log_janitor: 정리 대상 없음")
