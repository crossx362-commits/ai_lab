"""
봇 복구 모니터링 스크립트
logOut 후 봇 토큰이 복구될 때까지 대기하고 알림
"""
import os
import sys
import time
import urllib.request
import json
from datetime import datetime

_here = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(_here, "..", "..", "..", "..", ".."))
sys.path.insert(0, PROJECT_ROOT)
sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team"))

from _shared.env import load_env
load_env()

TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "").strip()
CHAT_ID = os.getenv("TELEGRAM_CHAT_ID", "").strip()

def check_bot_health():
    """봇 토큰이 정상 작동하는지 확인"""
    try:
        url = f"https://api.telegram.org/bot{TOKEN}/getMe"
        req = urllib.request.Request(url)
        with urllib.request.urlopen(req, timeout=5) as response:
            result = json.loads(response.read().decode())
            return result.get('ok', False)
    except Exception as e:
        return False

def send_notification(message):
    """텔레그램 알림 전송"""
    try:
        url = f"https://api.telegram.org/bot{TOKEN}/sendMessage"
        data = {"chat_id": CHAT_ID, "text": message}
        req = urllib.request.Request(
            url,
            json.dumps(data).encode(),
            headers={"Content-Type": "application/json"}
        )
        urllib.request.urlopen(req, timeout=5)
        return True
    except:
        return False

def start_bot_if_needed():
    """봇 프로세스가 실행 중이 아니면 시작"""
    import subprocess
    import platform

    bot_path = os.path.join(_here, "telegram_receiver.py")

    if platform.system() == "Windows":
        # Windows에서 pythonw로 봇 시작
        try:
            subprocess.Popen(
                ["pythonw", bot_path],
                creationflags=subprocess.CREATE_NO_WINDOW,
                env={**os.environ, "PYTHONUTF8": "1"}
            )
            return True
        except:
            return False
    else:
        # MacBook/Linux
        try:
            subprocess.Popen(
                ["python3", bot_path],
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
                start_new_session=True
            )
            return True
        except:
            return False

def main():
    """메인 모니터링 루프"""
    print(f"[{datetime.now().strftime('%H:%M:%S')}] 봇 복구 모니터링 시작...")
    print(f"[{datetime.now().strftime('%H:%M:%S')}] 최대 20분 동안 1분마다 체크합니다.")

    max_attempts = 20
    attempt = 0

    while attempt < max_attempts:
        attempt += 1
        elapsed = attempt

        print(f"[{datetime.now().strftime('%H:%M:%S')}] 체크 {attempt}/{max_attempts}...")

        if check_bot_health():
            print(f"[{datetime.now().strftime('%H:%M:%S')}] ✅ 봇 토큰 복구 확인!")

            # 봇 프로세스 시작
            print(f"[{datetime.now().strftime('%H:%M:%S')}] 봇 프로세스 시작 중...")
            if start_bot_if_needed():
                time.sleep(5)  # 봇 초기화 대기

                # 알림 전송
                message = f"""✅ 텔레그램 봇 복구 완료!

⏱️ 복구 시간: {elapsed}분
🤖 봇 프로세스: 자동 시작 완료

이제 봇에게 메시지를 보내보세요!
예: "현황", "일정", "봇상태"
"""
                if send_notification(message):
                    print(f"[{datetime.now().strftime('%H:%M:%S')}] 📱 텔레그램 알림 전송 완료")
                else:
                    print(f"[{datetime.now().strftime('%H:%M:%S')}] ⚠️ 알림 전송 실패 (봇은 정상)")
            else:
                print(f"[{datetime.now().strftime('%H:%M:%S')}] ⚠️ 봇 시작 실패 - 수동으로 시작 필요")
                send_notification("⚠️ 봇 토큰은 복구되었으나 봇 시작 실패\n수동으로 시작해주세요.")

            return True

        if attempt < max_attempts:
            print(f"[{datetime.now().strftime('%H:%M:%S')}] ⏳ 아직 복구 안 됨. 1분 후 재시도...")
            time.sleep(60)

    print(f"[{datetime.now().strftime('%H:%M:%S')}] ❌ 20분 경과 - 모니터링 종료")
    print(f"[{datetime.now().strftime('%H:%M:%S')}] 수동으로 봇 상태를 확인해주세요.")

    return False

if __name__ == "__main__":
    try:
        success = main()
        sys.exit(0 if success else 1)
    except KeyboardInterrupt:
        print(f"\n[{datetime.now().strftime('%H:%M:%S')}] 모니터링 중단됨")
        sys.exit(0)
    except Exception as e:
        print(f"[{datetime.now().strftime('%H:%M:%S')}] ❌ 오류: {e}")
        sys.exit(1)
