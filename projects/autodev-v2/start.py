#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AutoDev v2 원클릭 진입점.

1) v1 토큰 소모 루프 비활성화
2) preflight로 실제 비활성/예산 가드 확인
3) v2 연속 실행
"""
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

    audit = subprocess.run([sys.executable, str(HERE / "preflight.py")])
    if audit.returncode != 0:
        print("preflight가 실패해 토큰 소모형 구형 루프가 살아 있을 가능성이 있습니다. v2를 시작하지 않습니다.")
        return audit.returncode

    return subprocess.run([sys.executable, str(HERE / "autodev.py"), "run", "--continuous"]).returncode


if __name__ == "__main__":
    raise SystemExit(main())
