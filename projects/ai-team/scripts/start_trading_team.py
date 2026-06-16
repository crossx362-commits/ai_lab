# -*- coding: utf-8 -*-
"""
트레이딩 팀 통합 시작 스크립트
현빈(정보 수집) + 데이브(보수적 매매) + 레오(공격적 단타) 협업
"""
import os
import sys
import subprocess
import time

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


def start_process(name: str, script_path: str, args: list = None):
    """백그라운드 프로세스 시작"""
    if args is None:
        args = []

    try:
        # Windows에서 백그라운드 실행
        if sys.platform == "win32":
            cmd = ["python", script_path] + args
            process = subprocess.Popen(
                cmd,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                creationflags=subprocess.CREATE_NO_WINDOW
            )
        else:
            # Linux/Mac
            cmd = ["python3", script_path] + args + ["&"]
            process = subprocess.Popen(
                cmd,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE
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

    processes = {}

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
    if os.path.exists(dave_path):
        processes["데이브"] = start_process("데이브 (보수적 매매)", dave_path)
        time.sleep(1)
    else:
        print(f"⚠️  데이브 스크립트 없음: {dave_path}")

    # 3. 레오 (공격적 단타 - 10초 주기)
    leo_path = os.path.join(
        AI_TEAM_ROOT, "skills", "레오_트레이더", "tools", "leo_aggressive_trader.py"
    )
    if os.path.exists(leo_path):
        processes["레오"] = start_process("레오 (공격적 단타)", leo_path)
    else:
        print(f"⚠️  레오 스크립트 없음: {leo_path}")

    print("\n" + "=" * 60)
    print("✅ 트레이딩 팀 가동 완료")
    print("=" * 60)

    # 텔레그램 알림
    active_agents = [name for name, proc in processes.items() if proc is not None]
    msg = f"""
🚀 AI 트레이딩 팀 가동

✅ 활성 에이전트:
{chr(10).join(f'  • {name}' for name in active_agents)}

협업 구조:
1️⃣ 현빈 → 시장 정보 수집 (5분)
   - 연준 일정, 공포탐욕지수, 김치프리미엄
2️⃣ 데이브 → 보수적 매매 (30초)
   - 현빈 정보 참조, 퀀트 3점 + LLM 검증
3️⃣ 레오 → 공격적 단타 (10초)
   - 현빈 정보 참조, 퀀트 1점 자동 진입
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
