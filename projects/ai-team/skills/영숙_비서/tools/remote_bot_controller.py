"""
원격 봇 제어 스크립트
텔레그램 메시지를 통해 맥북/윈도우 봇을 원격으로 켜고 끌 수 있음
"""
import os
import sys
import subprocess
import platform
import shlex

_here = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(_here, "..", "..", "..", "..", ".."))
sys.path.insert(0, PROJECT_ROOT)
sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team"))

from _shared.env import load_env
from _shared.notify import send

load_env()

MACBOOK_HOST = os.getenv("MACBOOK_SSH_HOST", "")  # 예: user@192.168.0.100 또는 user@macbook.local
MACBOOK_SSH_KEY = os.getenv("MACBOOK_SSH_KEY_PATH", "")  # SSH 키 경로 (선택)

AI_TEAM_KEYWORDS = [
    "telegram_receiver.py",
    "run_youngsuk_daemon.py",
    "bot_recovery_monitor.py",
    "start_telegram_bot",
    "crypto_market_intelligence.py",
    "upbit_auto_trader.py",
    "leo_aggressive_trader.py",
    "monitor_processes.py",
    "start_trading_team.py",
    "run_trader_daemon.py",
]
AGENT_STOP_KEYWORDS = [
    "crypto_market_intelligence.py",
    "upbit_auto_trader.py",
    "leo_aggressive_trader.py",
    "monitor_processes.py",
    "start_trading_team.py",
    "run_trader_daemon.py",
]

MANUAL_STOP_DIR = os.path.join(PROJECT_ROOT, "projects", "ai-team", "scripts")


def mark_local_manual_stop_all():
    os.makedirs(MANUAL_STOP_DIR, exist_ok=True)
    flags = [".manual_stop", ".manual_stop_hyunbin", ".manual_stop_dave", ".manual_stop_leo"]
    for flag in flags:
        with open(os.path.join(MANUAL_STOP_DIR, flag), "w", encoding="utf-8") as f:
            f.write("# manual stop: direct user stop command\n")
            f.write("# Remove by explicit start/restart.\n")


def mark_local_youngsuk_stop():
    os.makedirs(MANUAL_STOP_DIR, exist_ok=True)
    with open(os.path.join(MANUAL_STOP_DIR, ".manual_stop_youngsuk"), "w", encoding="utf-8") as f:
        f.write("# manual stop: youngsuk\n")
        f.write("# Remove by explicit bot start/restart.\n")


def clear_local_youngsuk_stop():
    try:
        os.remove(os.path.join(MANUAL_STOP_DIR, ".manual_stop_youngsuk"))
    except OSError:
        pass


def _ssh_macbook(command: str, timeout: int = 10):
    if not MACBOOK_HOST:
        return None, "MACBOOK_SSH_HOST 환경변수가 설정되지 않았습니다"

    ssh_cmd = ["ssh", "-o", "BatchMode=yes", "-o", "ConnectTimeout=5"]
    if MACBOOK_SSH_KEY:
        ssh_cmd.extend(["-i", MACBOOK_SSH_KEY])
    ssh_cmd.extend([MACBOOK_HOST, command])
    try:
        return subprocess.run(ssh_cmd, capture_output=True, text=True, timeout=timeout), None
    except Exception as e:
        return None, str(e)


def _windows_pids_for_keywords(keywords):
    escaped = ",".join("'" + k.replace("'", "''").lower() + "'" for k in keywords)
    cmd = f"""
$keywords = @({escaped})
Get-CimInstance Win32_Process |
  Where-Object {{
    $cmd = $_.CommandLine.ToLower()
    $_.Name -match '^python' -and
    $_.CommandLine -and
    ($keywords | Where-Object {{ $cmd.Contains($_) }})
  }} |
  Select-Object -ExpandProperty ProcessId
"""
    result = subprocess.run(
        ["powershell", "-NoProfile", "-Command", cmd],
        capture_output=True,
        text=True,
        timeout=5,
    )
    if result.returncode != 0 or not result.stdout.strip():
        return []
    return [int(pid) for pid in result.stdout.split() if pid.strip().isdigit()]

def get_current_platform():
    """현재 실행 중인 플랫폼 반환"""
    return "macbook" if platform.system() == "Darwin" else "windows"

def check_bot_running(platform_name=None):
    """봇이 실행 중인지 확인"""
    if platform_name is None:
        platform_name = get_current_platform()

    try:
        if platform_name == "windows":
            return bool(_windows_pids_for_keywords(["telegram_receiver.py"]))
        else:  # macbook
            result = subprocess.run(
                ["pgrep", "-f", "telegram_receiver.py"],
                capture_output=True, text=True
            )
            return result.returncode == 0
    except Exception as e:
        print(f"❌ 봇 상태 확인 실패: {e}")
        return False

def stop_local_bot():
    """로컬(현재 머신)의 봇 종료"""
    platform_name = get_current_platform()
    mark_local_youngsuk_stop()

    try:
        if platform_name == "windows":
            pids = _windows_pids_for_keywords(["telegram_receiver.py"])
            for pid in pids:
                subprocess.run(["taskkill", "/F", "/PID", str(pid)], capture_output=True, timeout=3)
            return "✅ Windows 봇 종료 완료" if pids else "⚠️ Windows 봇 실행 중 아님"
        else:  # macbook
            subprocess.run(["pkill", "-f", "telegram_receiver.py"])
            return "✅ MacBook 봇 종료 완료"
    except Exception as e:
        return f"❌ 봇 종료 실패: {e}"

def start_local_bot():
    """로컬(현재 머신)의 봇 시작"""
    platform_name = get_current_platform()
    bot_path = os.path.join(_here, "telegram_receiver.py")
    clear_local_youngsuk_stop()

    try:
        if platform_name == "windows":
            subprocess.Popen(
                ["pythonw", bot_path],
                creationflags=subprocess.CREATE_NO_WINDOW,
                env={**os.environ, "PYTHONUTF8": "1"}
            )
            return "✅ Windows 봇 시작 완료"
        else:  # macbook
            subprocess.Popen(
                ["python3", bot_path],
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
                start_new_session=True
            )
            return "✅ MacBook 봇 시작 완료"
    except Exception as e:
        return f"❌ 봇 시작 실패: {e}"

def restart_local_bot():
    """로컬(현재 머신)의 봇 재시작"""
    stop_result = stop_local_bot()
    import time
    time.sleep(2)
    start_result = start_local_bot()
    return f"{stop_result}\n{start_result}"

def stop_remote_macbook_bot():
    """SSH를 통해 맥북의 봇 종료"""
    if not MACBOOK_HOST:
        return "❌ MACBOOK_SSH_HOST 환경변수가 설정되지 않았습니다"

    result, error = _ssh_macbook("pkill -f telegram_receiver.py || true", timeout=10)
    if error:
        return f"❌ SSH 연결 실패: {error}"
    if result.returncode == 0:
        return "✅ MacBook 봇 원격 종료 완료"
    return f"❌ MacBook 봇 종료 실패: {result.stderr.strip()}"


def stop_remote_macbook_agents():
    """SSH로 MacBook의 AI-team 에이전트/봇을 모두 종료하고 재시작하지 않는다."""
    if not MACBOOK_HOST:
        return "❌ MACBOOK_SSH_HOST 환경변수가 설정되지 않았습니다"

    patterns = " ".join(shlex.quote(k) for k in AGENT_STOP_KEYWORDS)
    remote_cmd = f"""
mkdir -p "$HOME/ai_lab/projects/ai-team/scripts" 2>/dev/null || true
for flag in .manual_stop .manual_stop_hyunbin .manual_stop_dave .manual_stop_leo; do
  printf '%s\\n' '# manual stop: direct user stop command' '# Remove by explicit start/restart.' > "$HOME/ai_lab/projects/ai-team/scripts/$flag" 2>/dev/null || true
done
for pattern in {patterns}; do
  pkill -f "$pattern" 2>/dev/null || true
done
sleep 1
for pattern in {patterns}; do
  pgrep -af "$pattern" 2>/dev/null || true
done
"""
    result, error = _ssh_macbook(remote_cmd, timeout=15)
    if error:
        return f"❌ MacBook 전체 종료 실패: {error}"

    leftovers = [line for line in result.stdout.splitlines() if line.strip()]
    if leftovers:
        preview = "\n".join(leftovers[:5])
        return f"⚠️ MacBook 종료 명령 완료, 남은 프로세스 확인 필요:\n{preview}"
    return "✅ MacBook AI-team 에이전트 전체 종료 완료 (재시작 없음)"


def stop_local_ai_team_processes(skip_current=True):
    """현재 머신의 AI-team 에이전트/봇을 종료한다."""
    mark_local_manual_stop_all()
    if platform.system() == "Windows":
        pids = _windows_pids_for_keywords(AGENT_STOP_KEYWORDS)
    else:
        pids = []
        for keyword in AGENT_STOP_KEYWORDS:
            result = subprocess.run(["pgrep", "-f", keyword], capture_output=True, text=True)
            if result.returncode == 0:
                pids.extend(int(pid) for pid in result.stdout.split() if pid.isdigit())

    current_pid = os.getpid()
    killed = []
    for pid in pids:
        if skip_current and pid == current_pid:
            continue
        try:
            if platform.system() == "Windows":
                subprocess.run(["taskkill", "/F", "/PID", str(pid)], capture_output=True, timeout=3)
            else:
                subprocess.run(["kill", "-TERM", str(pid)], capture_output=True, timeout=3)
            killed.append(pid)
        except Exception:
            pass
    if killed:
        return f"✅ 로컬 AI-team 종료 완료 (PID: {', '.join(map(str, killed))})"
    return "⚠️ 로컬 종료 대상 없음"


def emergency_stop_all_no_restart(skip_current=True):
    """원격 MacBook을 먼저 멈춘 뒤 로컬도 멈춘다. 시작 명령은 실행하지 않는다."""
    mark_local_manual_stop_all()
    results = []
    if MACBOOK_HOST and get_current_platform() == "windows":
        results.append(stop_remote_macbook_agents())
    results.append(stop_local_ai_team_processes(skip_current=skip_current))
    return "\n".join(results)

def start_remote_macbook_bot():
    """SSH를 통해 맥북의 봇 시작"""
    if not MACBOOK_HOST:
        return "❌ MACBOOK_SSH_HOST 환경변수가 설정되지 않았습니다"

    try:
        # 맥북에서 봇 스크립트 경로 찾기 (사용자 환경에 맞게 수정 필요)
        remote_bot_path = f"~/ai_lab/projects/ai-team/skills/영숙_비서/tools/telegram_receiver.py"

        ssh_cmd = ["ssh"]
        if MACBOOK_SSH_KEY:
            ssh_cmd.extend(["-i", MACBOOK_SSH_KEY])
        ssh_cmd.extend([
            MACBOOK_HOST,
            f"nohup python3 {remote_bot_path} > /dev/null 2>&1 &"
        ])

        result = subprocess.run(ssh_cmd, capture_output=True, text=True, timeout=10)
        if result.returncode == 0:
            return "✅ MacBook 봇 원격 시작 완료"
        else:
            return f"❌ MacBook 봇 시작 실패: {result.stderr}"
    except Exception as e:
        return f"❌ SSH 연결 실패: {e}"

def get_bot_status():
    """모든 플랫폼의 봇 상태 조회"""
    current = get_current_platform()
    local_running = check_bot_running()

    status = f"📊 봇 상태\n\n"
    status += f"현재 플랫폼: {current.upper()}\n"
    status += f"로컬 봇: {'🟢 실행 중' if local_running else '🔴 중지'}\n"

    # 원격 맥북 상태 확인 (SSH 가능한 경우)
    if MACBOOK_HOST and current == "windows":
        try:
            ssh_cmd = ["ssh"]
            if MACBOOK_SSH_KEY:
                ssh_cmd.extend(["-i", MACBOOK_SSH_KEY])
            ssh_cmd.extend([MACBOOK_HOST, "pgrep -f telegram_receiver.py"])

            result = subprocess.run(ssh_cmd, capture_output=True, text=True, timeout=5)
            remote_running = result.returncode == 0
            status += f"원격 MacBook: {'🟢 실행 중' if remote_running else '🔴 중지'}\n"
        except:
            status += f"원격 MacBook: ⚠️ 상태 확인 불가\n"

    return status

def stop_all_bots():
    """모든 플랫폼의 봇 종료 (로컬 + 원격)"""
    results = []

    # 원격 맥북 봇 종료를 먼저 수행해야 현재 봇이 응답을 보낼 수 있다.
    if MACBOOK_HOST:
        remote_result = stop_remote_macbook_bot()
        results.append(f"맥북: {remote_result}")

    local_result = stop_local_bot()
    results.append(f"로컬: {local_result}")

    return "\n".join(results)

def start_all_bots():
    """모든 플랫폼의 봇 시작 (로컬 + 원격)"""
    results = []

    # 로컬 봇 시작
    local_result = start_local_bot()
    results.append(f"로컬: {local_result}")

    # 원격 맥북 봇 시작 (SSH 설정되어 있으면)
    if MACBOOK_HOST:
        import time
        time.sleep(1)  # 로컬 시작 후 1초 대기
        remote_result = start_remote_macbook_bot()
        results.append(f"맥북: {remote_result}")

    return "\n".join(results)

def handle_bot_command(command: str) -> str:
    """봇 제어 명령 처리

    명령어:
    - 봇상태 / bot status
    - 봇종료 / bot stop
    - 봇시작 / bot start
    - 봇재시작 / bot restart
    - 봇전체종료 / stop all bots
    - 봇전체시작 / start all bots
    - 맥북봇종료 / macbook stop
    - 맥북봇시작 / macbook start
    """
    command = command.strip().lower().replace(" ", "")

    if command in ["봇상태", "botstatus", "status"]:
        return get_bot_status()

    elif command in ["봇전체종료", "봇모두종료", "stopallbots", "killallbots"]:
        return emergency_stop_all_no_restart(skip_current=True)

    elif command in ["에이전트전체종료", "전체에이전트종료", "stopallagents", "killallagents"]:
        return emergency_stop_all_no_restart(skip_current=True)

    elif command in ["봇전체시작", "봇모두시작", "startallbots"]:
        return start_all_bots()

    elif command in ["봇종료", "botstop", "stop"]:
        return stop_local_bot()

    elif command in ["봇시작", "botstart", "start"]:
        return start_local_bot()

    elif command in ["봇재시작", "botrestart", "restart"]:
        return restart_local_bot()

    elif command in ["맥북봇종료", "macbookstop", "맥북종료"]:
        return stop_remote_macbook_bot()

    elif command in ["맥북봇시작", "macbookstart", "맥북시작"]:
        return start_remote_macbook_bot()

    else:
        return """❓ 알 수 없는 명령어

사용 가능한 명령어:
• 봇상태 - 모든 봇 상태 확인
• 봇종료 - 현재 머신의 봇 종료
• 봇시작 - 현재 머신의 봇 시작
• 봇재시작 - 현재 머신의 봇 재시작
• 맥북봇종료 - 원격으로 맥북 봇 종료
• 맥북봇시작 - 원격으로 맥북 봇 시작"""

if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")

    if len(sys.argv) > 1:
        command = " ".join(sys.argv[1:])
        result = handle_bot_command(command)
        print(result)
        if "--notify" in sys.argv:
            send(result)
    else:
        print("사용법: python remote_bot_controller.py <명령어>")
        print(handle_bot_command("help"))
