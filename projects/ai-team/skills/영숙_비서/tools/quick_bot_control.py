"""
빠른 봇 제어 스크립트 (최적화 버전)
토큰 사용 최소화, 실행 속도 최대화
"""
import os
import sys
import subprocess
import platform
import urllib.request
import json

# 환경변수 로드 최적화: 한 번만 로드
_ENV_LOADED = False
_TOKEN = None
_CHAT_ID = None

def load_env_fast():
    """환경변수 빠른 로드 (.env.encrypted 무시)"""
    global _ENV_LOADED, _TOKEN, _CHAT_ID

    if _ENV_LOADED:
        return

    env_path = os.path.join(os.path.dirname(__file__), "..", "..", "..", "..", "..", ".env")

    if not os.path.exists(env_path):
        return

    with open(env_path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            k, v = line.split("=", 1)
            k = k.strip()
            v = v.strip().strip('"').strip("'")

            if k == "TELEGRAM_BOT_TOKEN":
                _TOKEN = v
            elif k == "TELEGRAM_CHAT_ID":
                _CHAT_ID = v

            if _TOKEN and _CHAT_ID:
                break

    _ENV_LOADED = True

def check_token() -> bool:
    """토큰 유효성 빠른 체크 (0.5초)"""
    load_env_fast()
    if not _TOKEN:
        return False

    try:
        url = f"https://api.telegram.org/bot{_TOKEN}/getMe"
        with urllib.request.urlopen(url, timeout=2) as response:
            result = json.loads(response.read().decode())
            return result.get('ok', False)
    except:
        return False

def send_telegram(message: str) -> bool:
    """텔레그램 빠른 전송"""
    load_env_fast()
    if not _TOKEN or not _CHAT_ID:
        return False

    try:
        url = f"https://api.telegram.org/bot{_TOKEN}/sendMessage"
        data = json.dumps({"chat_id": _CHAT_ID, "text": message}).encode()
        req = urllib.request.Request(url, data, headers={"Content-Type": "application/json"})
        with urllib.request.urlopen(req, timeout=3) as response:
            result = json.loads(response.read().decode())
            return result.get('ok', False)
    except:
        return False

def get_bot_pids() -> list:
    """봇 프로세스 PID 빠른 조회"""
    pids = []

    try:
        if platform.system() == "Windows":
            # 최적화: 한 번에 커맨드라인을 조회한다.
            cmd = """
Get-CimInstance Win32_Process |
  Where-Object {
    $_.Name -match '^python' -and
    $_.CommandLine -and
    $_.CommandLine.ToLower().Contains('telegram_receiver.py')
  } |
  Select-Object -ExpandProperty ProcessId
"""
            result = subprocess.run(["powershell", "-NoProfile", "-Command", cmd],
                                    capture_output=True, text=True, timeout=3)
            if result.stdout.strip():
                pids = [int(p.strip()) for p in result.stdout.strip().split() if p.strip().isdigit()]
        else:
            result = subprocess.run(["pgrep", "-f", "telegram_receiver"],
                                    capture_output=True, text=True, timeout=2)
            if result.returncode == 0:
                pids = [int(p.strip()) for p in result.stdout.strip().split() if p.strip().isdigit()]
    except:
        pass

    return pids

def stop_bot() -> str:
    """봇 빠른 종료"""
    pids = get_bot_pids()
    if not pids:
        return "⚠️ 실행 중인 봇 없음"

    try:
        if platform.system() == "Windows":
            for pid in pids:
                subprocess.run(["taskkill", "/F", "/PID", str(pid)],
                               capture_output=True, timeout=2)
        else:
            subprocess.run(["kill"] + [str(p) for p in pids], timeout=2)

        return f"✅ 봇 종료 (PID: {','.join(map(str, pids))})"
    except Exception as e:
        return f"❌ 종료 실패: {e}"

def start_bot() -> str:
    """봇 빠른 시작"""
    # 이미 실행 중인지 체크
    if get_bot_pids():
        return "⚠️ 봇 이미 실행 중"

    script_path = os.path.join(os.path.dirname(__file__), "telegram_receiver.py")
    if not os.path.exists(script_path):
        return f"❌ 스크립트 없음: {script_path}"

    try:
        if platform.system() == "Windows":
            subprocess.Popen(["pythonw", script_path],
                             creationflags=subprocess.CREATE_NO_WINDOW,
                             env={**os.environ, "PYTHONUTF8": "1"})
        else:
            subprocess.Popen(["python3", script_path],
                             stdout=subprocess.DEVNULL,
                             stderr=subprocess.DEVNULL,
                             start_new_session=True)
        return "✅ 봇 시작"
    except Exception as e:
        return f"❌ 시작 실패: {e}"

def status() -> str:
    """봇 상태 빠른 조회"""
    token_ok = check_token()
    pids = get_bot_pids()

    status_msg = "📊 봇 상태\n\n"
    status_msg += f"토큰: {'✅ 정상' if token_ok else '❌ 오류'}\n"
    status_msg += f"프로세스: {'🟢 실행 중' if pids else '🔴 중지'}\n"

    if pids:
        status_msg += f"PID: {', '.join(map(str, pids))}"

    return status_msg

def force_logout() -> str:
    """강제 로그아웃 (409 해결용)"""
    load_env_fast()
    if not _TOKEN:
        return "❌ 토큰 없음"

    try:
        url = f"https://api.telegram.org/bot{_TOKEN}/logOut"
        with urllib.request.urlopen(url, timeout=3) as response:
            result = json.loads(response.read().decode())
            if result.get('ok'):
                return "✅ 로그아웃 성공\n⏳ 10초 대기 후 재시작하세요"
            else:
                return f"❌ 로그아웃 실패: {result}"
    except Exception as e:
        return f"❌ API 오류: {e}"

def quick_restart() -> str:
    """빠른 재시작 (전체 프로세스 3초)"""
    import time

    # 1. 종료
    stop_msg = stop_bot()

    # 2. 짧은 대기
    time.sleep(1)

    # 3. 시작
    start_msg = start_bot()

    return f"{stop_msg}\n{start_msg}"

def fix_409() -> str:
    """409 에러 빠른 해결"""
    import time

    steps = []

    # 1. 모든 봇 종료
    steps.append(stop_bot())

    # 2. 로그아웃
    time.sleep(2)
    steps.append(force_logout())

    # 3. 대기
    time.sleep(10)

    # 4. 재시작
    steps.append(start_bot())

    return "\n".join(steps)

# CLI 인터페이스
COMMANDS = {
    "status": status,
    "상태": status,
    "stop": stop_bot,
    "종료": stop_bot,
    "start": start_bot,
    "시작": start_bot,
    "restart": quick_restart,
    "재시작": quick_restart,
    "logout": force_logout,
    "로그아웃": force_logout,
    "fix409": fix_409,
}

if __name__ == "__main__":
    # UTF-8 encoding fix
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")

    if len(sys.argv) < 2:
        print("""사용법: python quick_bot_control.py <명령>

명령어:
  status, 상태      - 봇 상태 확인 (0.5초)
  start, 시작       - 봇 시작 (2초)
  stop, 종료        - 봇 종료 (1초)
  restart, 재시작   - 빠른 재시작 (3초)
  logout, 로그아웃  - 강제 로그아웃 (409 해결용)
  fix409            - 409 에러 자동 해결 (15초)

예시:
  python quick_bot_control.py status
  python quick_bot_control.py restart
  python quick_bot_control.py fix409
""")
        sys.exit(1)

    cmd = sys.argv[1].lower()
    if cmd in COMMANDS:
        result = COMMANDS[cmd]()
        print(result)

        # 텔레그램 알림 전송 (옵션)
        if "--notify" in sys.argv:
            send_telegram(result)
    else:
        print(f"❌ 알 수 없는 명령: {cmd}")
        print("사용 가능: " + ", ".join(COMMANDS.keys()))
        sys.exit(1)
