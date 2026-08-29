#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AutoDev v2 원클릭 진입점: v1 토큰 소모 루프를 끄고 v2를 연속 실행한다."""
from __future__ import annotations
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent


def main() -> int:
    mig = subprocess.run([sys.executable, str(HERE / "migrate_v1.py"), "--apply"])
    if mig.returncode != 0:
        print("v1 전환 단계가 실패해 v2 시작을 중단합니다.")
        return mig.returncode
    return subprocess.run([sys.executable, str(HERE / "autodev.py"), "run", "--continuous"]).returncode


if __name__ == "__main__":
    raise SystemExit(main())
