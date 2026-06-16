# -*- coding: utf-8 -*-
"""
중복 프로세스 자동 정리 스크립트
30개 이상 python 프로세스 감지 시 자동 정리 후 재시작
"""
import os
import sys
import subprocess
import time

_here = os.path.dirname(os.path.abspath(__file__))
AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, ".."))
sys.path.insert(0, AI_TEAM_ROOT)

from _shared.env_loader import load_env
from _shared.telegram_notifier import send_telegram_message

load_env()


def count_python_processes():
    """Python 프로세스 개수 확인"""
    try:
        if sys.platform == "win32":
            result = subprocess.run(
                ["powershell", "-Command", "Get-Process python* | Measure-Object | Select-Object -ExpandProperty Count"],
                capture_output=True,
                text=True,
                timeout=10
            )
            count = int(result.stdout.strip())
            return count
        else:
            result = subprocess.run(["ps", "aux"], capture_output=True, text=True)
            count = len([line for line in result.stdout.split('\n') if 'python' in line.lower()])
            return count
    except Exception as e:
        print(f"프로세스 개수 확인 실패: {e}")
        return 0


def kill_all_python_processes():
    """모든 Python 프로세스 종료"""
    try:
        if sys.platform == "win32":
            subprocess.run(
                ["powershell", "-Command", "Get-Process python* | Stop-Process -Force"],
                timeout=30
            )
        else:
            subprocess.run(["pkill", "-9", "python"])

        time.sleep(3)
        print("✅ 모든 Python 프로세스 종료 완료")
        return True
    except Exception as e:
        print(f"❌ 프로세스 종료 실패: {e}")
        return False


def start_trading_bots():
    """트레이딩 봇 재시작"""
    bots = []

    # 1. 현빈 (정보 수집)
    hyunbin_path = os.path.join(AI_TEAM_ROOT, "skills", "현빈_전략가", "tools", "crypto_market_intelligence.py")
    try:
        proc = subprocess.Popen(
            ["python", hyunbin_path, "--daemon"],
            cwd=os.path.dirname(hyunbin_path),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE
        )
        bots.append(("현빈", proc.pid))
        print(f"✅ 현빈 시작 (PID: {proc.pid})")
        time.sleep(5)  # 초기 정보 수집 대기
    except Exception as e:
        print(f"❌ 현빈 시작 실패: {e}")

    # 2. 데이브 (보수적 매매)
    dave_path = os.path.join(AI_TEAM_ROOT, "skills", "데이브_주식", "tools", "upbit_auto_trader.py")
    try:
        proc = subprocess.Popen(
            ["python", dave_path],
            cwd=os.path.dirname(dave_path),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE
        )
        bots.append(("데이브", proc.pid))
        print(f"✅ 데이브 시작 (PID: {proc.pid})")
        time.sleep(2)
    except Exception as e:
        print(f"❌ 데이브 시작 실패: {e}")

    # 3. 레오 (공격적 단타)
    leo_path = os.path.join(AI_TEAM_ROOT, "skills", "레오_트레이더", "tools", "leo_aggressive_trader.py")
    try:
        proc = subprocess.Popen(
            ["python", leo_path],
            cwd=os.path.dirname(leo_path),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE
        )
        bots.append(("레오", proc.pid))
        print(f"✅ 레오 시작 (PID: {proc.pid})")
    except Exception as e:
        print(f"❌ 레오 시작 실패: {e}")

    return bots


def main():
    """메인 실행"""
    print("=" * 60)
    print("중복 프로세스 자동 정리 및 재시작")
    print("=" * 60)

    # 현재 프로세스 개수 확인
    count = count_python_processes()
    print(f"\n현재 Python 프로세스: {count}개")

    if count >= 30:
        print(f"\n⚠️ 중복 프로세스 감지 ({count}개 ≥ 30개)")
        send_telegram_message(f"⚠️ 중복 프로세스 {count}개 감지 - 자동 정리 시작")

        # 모든 프로세스 종료
        if kill_all_python_processes():
            print("\n트레이딩 봇 재시작 중...")

            # 트레이딩 봇 재시작
            bots = start_trading_bots()

            # 결과 보고
            bot_list = "\n".join([f"  • {name} (PID: {pid})" for name, pid in bots])
            msg = f"✅ 트레이딩 팀 재시작 완료\n\n{bot_list}"
            print(f"\n{msg}")
            send_telegram_message(msg)
        else:
            send_telegram_message("❌ 프로세스 정리 실패 - 수동 확인 필요")
    else:
        print(f"✅ 정상 범위 (< 30개)")

    print("\n" + "=" * 60)


if __name__ == "__main__":
    main()
