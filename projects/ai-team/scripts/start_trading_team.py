# -*- coding: utf-8 -*-
"""
트레이딩 팀 통합 시작 스크립트
현빈(정보 수집) + 데이브(보수적 매매) + 레오(공격적 단타) 협업
"""
import os
import sys
import subprocess
import time
import importlib.util

if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except: pass

_here = os.path.dirname(os.path.abspath(__file__))
AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, ".."))
sys.path.insert(0, AI_TEAM_ROOT)

from _shared.env_loader import load_env
from _shared.telegram_notifier import send_telegram_message

load_env()


def has_module(module_name: str) -> bool:
    return importlib.util.find_spec(module_name) is not None


def start_process(name: str, script_path: str, args: list = None):
    """백그라운드 프로세스 시작"""
    if args is None:
        args = []

    try:
        cmd = [sys.executable, script_path] + args
        creationflags = subprocess.CREATE_NO_WINDOW if sys.platform == "win32" else 0
        process = subprocess.Popen(
            cmd,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            creationflags=creationflags,
        )

        print(f"✅ {name} 시작 (PID: {process.pid})")
        return process
    except Exception as e:
        print(f"❌ {name} 시작 실패: {e}")
        return None


def main():
    """트레이딩 팀 시작"""
    print("=" * 60)
    print("🚀 AI 트레이딩 팀 가동 시작")
    print("=" * 60)

    live_mode = "--live" in sys.argv[1:]
    trade_mode_args = ["--daemon", "--live"] if live_mode else ["--daemon", "--sim"]
    mode_label = "실거래" if live_mode else "시뮬레이션"
    print(f"모드: {mode_label}")
    print("실거래를 원하면 --live를 명시해야 합니다.")

    processes = {}
    can_trade = has_module("pyupbit")
    if not can_trade:
        print("⚠️  pyupbit 모듈이 없어 데이브/레오 매매 루프는 시작하지 않습니다.")
        print("   트레이딩 기능을 쓰려면 현재 파이썬 환경에 pyupbit를 설치하세요.")

    # 1. 현빈 (시장 정보 수집 - 5분 주기)
    hyunbin_path = os.path.join(
        AI_TEAM_ROOT, "skills", "현빈_전략가", "tools", "crypto_market_intelligence.py"
    )
    if os.path.exists(hyunbin_path):
        processes["현빈"] = start_process("현빈 (정보 수집)", hyunbin_path, ["--daemon"])
        time.sleep(2)  # 초기 정보 수집 대기
    else:
        print(f"⚠️  현빈 스크립트 없음: {hyunbin_path}")

    # 2. 데이브 (보수적 매매 - 30초 주기)
    dave_path = os.path.join(
        AI_TEAM_ROOT, "skills", "데이브_주식", "tools", "upbit_auto_trader.py"
    )
    if os.path.exists(dave_path) and can_trade:
        dave_args = ["--daemon"] if live_mode else ["--daemon", "--sim"]
        processes["데이브"] = start_process(f"데이브 (보수적 매매/{mode_label})", dave_path, dave_args)
        time.sleep(1)
    elif os.path.exists(dave_path):
        print("⚠️  데이브 스크립트는 있지만 pyupbit가 없어 시작하지 않음")
    else:
        print(f"⚠️  데이브 스크립트 없음: {dave_path}")

    # 3. 레오 (공격적 단타 - 10초 주기)
    leo_path = os.path.join(
        AI_TEAM_ROOT, "skills", "레오_트레이더", "tools", "leo_aggressive_trader.py"
    )
    if os.path.exists(leo_path) and can_trade:
        processes["레오"] = start_process(f"레오 (공격적 단타/{mode_label})", leo_path, trade_mode_args)
    elif os.path.exists(leo_path):
        print("⚠️  레오 스크립트는 있지만 pyupbit가 없어 시작하지 않음")
    else:
        print(f"⚠️  레오 스크립트 없음: {leo_path}")

    print("\n" + "=" * 60)
    print("✅ 트레이딩 팀 가동 완료")
    print("=" * 60)

    # 텔레그램 알림
    active_agents = [name for name, proc in processes.items() if proc is not None]
    msg = f"""
🚀 AI 트레이딩 팀 가동 ({mode_label})

✅ 활성 에이전트:
{chr(10).join(f'  • {name}' for name in active_agents)}

협업 구조:
1️⃣ 현빈 → 시장 정보 수집 (5분)
   - 연준 일정, 공포탐욕지수, 김치프리미엄
2️⃣ 데이브 → 보수적 매매 (30초)
   - 현빈 정보 참조, 퀀트 3점 + LLM 검증
3️⃣ 레오 → 공격적 단타 (10초)
   - 현빈 정보 참조, 퀀트 2점 이상 + 위험 필터
"""
    send_telegram_message(msg)

    print(f"\n실행 중인 프로세스:")
    for name, proc in processes.items():
        if proc:
            print(f"  {name}: PID {proc.pid}")

    print("\n프로세스를 종료하려면 Ctrl+C를 누르세요.")

    # 프로세스 모니터링
    try:
        while True:
            time.sleep(10)

            # 프로세스 상태 체크
            for name, proc in list(processes.items()):
                if proc and proc.poll() is not None:
                    print(f"⚠️  {name} 프로세스 종료됨 (exit code: {proc.returncode})")
                    send_telegram_message(f"⚠️ {name} 프로세스 종료 감지")

    except KeyboardInterrupt:
        print("\n\n프로세스 종료 중...")

        for name, proc in processes.items():
            if proc:
                print(f"  {name} 종료 중...")
                proc.terminate()
                proc.wait(timeout=5)

        print("\n✅ 모든 프로세스 종료 완료")
        send_telegram_message("🛑 AI 트레이딩 팀 가동 중지")


if __name__ == "__main__":
    main()
