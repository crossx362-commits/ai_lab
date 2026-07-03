#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""핵심 상태·학습 데이터 일일 백업 — output/cache는 gitignore라 유실되면 복구 불가.

보호 대상:
  - output/cache/  (watchlist·모의 원장·청산기록 somi_closed_trades = 한별 튜닝 학습 원천)
  - .env / .env.encrypted  (전 에이전트 시크릿 — 실수 삭제 대비)

방식: ~/ai_lab_backups/state_YYYY-MM-DD.tar.gz 로 스냅샷, RETENTION_DAYS 초과분 삭제.
스케줄: schedules.json 'state_backup' (매일 20:00 — 맥 저녁 가동 시간대).
"""
import os
import sys
import tarfile
import time
from datetime import date

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, "output")):
        break
    _root = os.path.dirname(_root)

BACKUP_DIR = os.path.expanduser("~/ai_lab_backups")
RETENTION_DAYS = 14
SOURCES = ["output/cache", ".env", ".env.encrypted"]


def run() -> str:
    os.makedirs(BACKUP_DIR, exist_ok=True)
    os.chmod(BACKUP_DIR, 0o700)  # 시크릿 포함 — 소유자 전용
    dest = os.path.join(BACKUP_DIR, f"state_{date.today().isoformat()}.tar.gz")
    n = 0
    with tarfile.open(dest, "w:gz") as tar:
        for rel in SOURCES:
            src = os.path.join(_root, rel)
            if os.path.exists(src):
                tar.add(src, arcname=rel)
                n += 1
    # 보존 기한 초과분 정리
    removed = 0
    cutoff = time.time() - RETENTION_DAYS * 86400
    for name in os.listdir(BACKUP_DIR):
        p = os.path.join(BACKUP_DIR, name)
        if name.startswith("state_") and os.path.isfile(p) and os.stat(p).st_mtime < cutoff:
            os.remove(p)
            removed += 1
    size_kb = os.path.getsize(dest) // 1024
    return f"백업 {os.path.basename(dest)} ({n}개 소스, {size_kb}KB), 만료 삭제 {removed}건"


if __name__ == "__main__":
    print("state_backup: " + run())
