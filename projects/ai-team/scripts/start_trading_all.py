#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""데이브/레오/시그널 통합 트레이딩 시스템 시작 (start_trading_team.py 래퍼)."""
import os
import sys
import subprocess

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

_here = os.path.dirname(os.path.abspath(__file__))
script_path = os.path.join(_here, "start_trading_team.py")

def main():
    print("=" * 60)
    print("🚀 AI Team 통합 트레이딩 시스템 시작 (start_trading_team.py 래핑)")
    print("=" * 60)
    print("  - 코인 트레이딩: 시그널, 데이브, 레오")
    print("  - 주식 트레이딩: 데이브 주식 (--with-stock)")
    print()

    try:
        creationflags = subprocess.CREATE_NO_WINDOW if sys.platform == "win32" else 0
        extra_args = sys.argv[1:]
        cmd = [sys.executable, script_path, "--live", "--with-stock"] + extra_args
        
        subprocess.Popen(
            cmd,
            creationflags=creationflags,
            close_fds=True
        )
        print("✅ start_trading_team.py 백그라운드 구동 성공")
    except Exception as e:
        print(f"❌ 런처 실행 실패: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()
