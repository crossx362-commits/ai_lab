# -*- coding: utf-8 -*-
"""
트레이딩 팀 상태 확인 스크립트
"""
import os
import sys

if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except:
        pass

_here = os.path.dirname(os.path.abspath(__file__))
AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, ".."))
sys.path.insert(0, AI_TEAM_ROOT)

from _shared.env import load_env
load_env()

def check_api_keys():
    """업비트 API 키 상태 확인"""
    access = os.getenv("UPBIT_ACCESS_KEY", "").strip('"').strip("'")
    secret = os.getenv("UPBIT_SECRET_KEY", "").strip('"').strip("'")

    print("=" * 60)
    print("📋 업비트 API 키 상태")
    print("=" * 60)

    if not access or not secret:
        print("❌ API 키가 비어있습니다")
        return False
    elif "입력" in access or "입력" in secret:
        print("❌ API 키가 자리표시자입니다")
        return False
    else:
        print(f"✅ ACCESS_KEY: {access[:10]}...{access[-4:]}")
        print(f"✅ SECRET_KEY: {secret[:10]}...{secret[-4:]}")
        return True

def test_api_connection():
    """실제 API 연결 테스트"""
    print("\n" + "=" * 60)
    print("🔌 업비트 API 연결 테스트")
    print("=" * 60)

    try:
        import pyupbit
        access = os.getenv("UPBIT_ACCESS_KEY", "").strip('"').strip("'")
        secret = os.getenv("UPBIT_SECRET_KEY", "").strip('"').strip("'")

        if not access or not secret:
            print("❌ API 키 없음")
            return False

        upbit = pyupbit.Upbit(access, secret)
        balances = upbit.get_balances()

        if balances is None or (isinstance(balances, dict) and "error" in balances):
            print(f"❌ API 호출 실패: {balances}")
            return False

        print("✅ API 연결 성공!")
        print(f"\n💰 현재 잔고:")
        for bal in balances:
            currency = bal.get("currency")
            balance = float(bal.get("balance", 0))
            if balance > 0:
                print(f"   {currency}: {balance:,.2f}")
        return True

    except ImportError:
        print("⚠️ pyupbit 모듈이 설치되지 않았습니다")
        print("   pip install pyupbit")
        return False
    except Exception as e:
        print(f"❌ 에러 발생: {e}")
        import traceback
        traceback.print_exc()
        return False

def check_running_processes():
    """실행 중인 트레이딩 프로세스 확인"""
    print("\n" + "=" * 60)
    print("⚙️ 실행 중인 프로세스")
    print("=" * 60)

    try:
        import subprocess
        result = subprocess.run(
            ["powershell", "-Command", "Get-Process python* | Select-Object Id,ProcessName,StartTime | Format-Table"],
            capture_output=True,
            text=True,
            timeout=5
        )
        print(result.stdout)
    except Exception as e:
        print(f"프로세스 확인 실패: {e}")

def check_log_files():
    """최근 로그 파일 확인"""
    print("\n" + "=" * 60)
    print("📄 최근 로그 파일")
    print("=" * 60)

    log_dir = os.path.join(os.path.dirname(AI_TEAM_ROOT), "output", "trading_logs")
    if not os.path.exists(log_dir):
        print(f"로그 디렉토리 없음: {log_dir}")
        return

    for filename in ["dave_daemon.out.log", "leo_daemon.out.log", "hyunbin_daemon.out.log"]:
        filepath = os.path.join(log_dir, filename)
        if os.path.exists(filepath):
            import datetime
            mtime = os.path.getmtime(filepath)
            mtime_str = datetime.datetime.fromtimestamp(mtime).strftime("%Y-%m-%d %H:%M:%S")
            size = os.path.getsize(filepath)
            print(f"✅ {filename}")
            print(f"   마지막 수정: {mtime_str}")
            print(f"   크기: {size:,} bytes")

            # 마지막 몇 줄 읽기
            try:
                with open(filepath, "r", encoding="utf-8", errors="ignore") as f:
                    lines = f.readlines()
                    last_lines = lines[-3:] if len(lines) >= 3 else lines
                    print(f"   최근 로그:")
                    for line in last_lines:
                        print(f"      {line.strip()}")
            except:
                pass
            print()
        else:
            print(f"❌ {filename} - 파일 없음")

def main():
    print("\n" + "=" * 70)
    print("🤖 AI 트레이딩 팀 상태 체크")
    print("=" * 70 + "\n")

    # 1. API 키 확인
    has_keys = check_api_keys()

    # 2. API 연결 테스트
    if has_keys:
        api_ok = test_api_connection()
    else:
        api_ok = False

    # 3. 실행 중인 프로세스
    check_running_processes()

    # 4. 로그 파일
    check_log_files()

    # 최종 판정
    print("\n" + "=" * 70)
    print("📊 최종 상태")
    print("=" * 70)

    if api_ok:
        print("✅ 실거래 가능 상태입니다")
        print("   - API 키: 정상")
        print("   - API 연결: 성공")
        print("\n💡 실거래 시작 방법:")
        print("   python projects/ai-team/scripts/start_trading_team.py --live")
    else:
        print("⚠️ 시뮬레이션 모드만 가능합니다")
        print("   - API 키를 확인하세요")
        print("\n💡 시뮬레이션 시작 방법:")
        print("   python projects/ai-team/scripts/start_trading_team.py")

    print("=" * 70 + "\n")

if __name__ == "__main__":
    main()
