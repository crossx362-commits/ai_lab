"""
개별 에이전트 제어 스크립트
특정 에이전트를 시작/종료/재시작할 수 있음
"""
import os
import sys
import subprocess
import platform

_here = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(_here, "..", "..", "..", "..", ".."))
sys.path.insert(0, PROJECT_ROOT)
sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team"))

from _shared.env_loader import load_env
load_env()

# 에이전트 정의 (이름 -> 스크립트 경로)
AGENTS = {
    "현빈": "projects/ai-team/skills/현빈_전략가/tools/crypto_market_intelligence.py",
    "데이브": "projects/ai-team/skills/데이브_주식/tools/upbit_auto_trader.py",
    "레오": "projects/ai-team/skills/레오_트레이더/tools/leo_aggressive_trader.py",
    "영숙": "projects/ai-team/skills/영숙_비서/tools/telegram_receiver.py",
}

# 영어 별칭
AGENT_ALIASES = {
    "hyunbin": "현빈",
    "dave": "데이브",
    "leo": "레오",
    "youngsuk": "영숙",
}

def get_agent_name(name: str) -> str:
    """에이전트 이름 정규화"""
    name = name.strip().lower()
    return AGENT_ALIASES.get(name, name)

def find_agent_process(agent_name: str) -> list:
    """에이전트 프로세스 찾기"""
    agent_name = get_agent_name(agent_name)
    if agent_name not in AGENTS:
        return []

    script_path = AGENTS[agent_name]
    script_filename = os.path.basename(script_path)

    pids = []
    try:
        if platform.system() == "Windows":
            needle = script_filename.replace("'", "''").lower()
            cmd = f"""
Get-CimInstance Win32_Process |
  Where-Object {{
    $_.Name -match '^python' -and
    $_.CommandLine -and
    $_.CommandLine.ToLower().Contains('{needle}')
  }} |
  Select-Object -ExpandProperty ProcessId
"""
            result = subprocess.run(
                ["powershell", "-NoProfile", "-Command", cmd],
                capture_output=True, text=True, timeout=5
            )
            if result.stdout.strip():
                pids = [int(pid) for pid in result.stdout.split() if pid.strip().isdigit()]
        else:  # macOS/Linux
            result = subprocess.run(
                ["pgrep", "-f", script_filename],
                capture_output=True, text=True
            )
            if result.returncode == 0:
                pids = [int(pid) for pid in result.stdout.strip().split('\n') if pid.strip()]
    except:
        pass

    return pids

def stop_agent(agent_name: str) -> str:
    """에이전트 종료"""
    agent_name = get_agent_name(agent_name)
    if agent_name not in AGENTS:
        available = ", ".join(AGENTS.keys())
        return f"❌ 알 수 없는 에이전트: {agent_name}\n사용 가능: {available}"

    pids = find_agent_process(agent_name)
    if not pids:
        return f"⚠️ {agent_name} 에이전트가 실행 중이 아닙니다"

    try:
        if platform.system() == "Windows":
            for pid in pids:
                subprocess.run(["taskkill", "/F", "/PID", str(pid)], capture_output=True)
        else:
            for pid in pids:
                subprocess.run(["kill", str(pid)])

        return f"✅ {agent_name} 종료 완료 (PID: {', '.join(map(str, pids))})"
    except Exception as e:
        return f"❌ {agent_name} 종료 실패: {e}"

def start_agent(agent_name: str) -> str:
    """에이전트 시작"""
    agent_name = get_agent_name(agent_name)
    if agent_name not in AGENTS:
        available = ", ".join(AGENTS.keys())
        return f"❌ 알 수 없는 에이전트: {agent_name}\n사용 가능: {available}"

    # 이미 실행 중인지 확인
    pids = find_agent_process(agent_name)
    if pids:
        return f"⚠️ {agent_name} 이미 실행 중 (PID: {', '.join(map(str, pids))})"

    script_path = os.path.join(PROJECT_ROOT, AGENTS[agent_name])
    if not os.path.exists(script_path):
        return f"❌ 스크립트 없음: {script_path}"

    try:
        if platform.system() == "Windows":
            # Windows에서 백그라운드로 시작
            if agent_name == "영숙":  # 텔레그램 봇은 pythonw로
                process = subprocess.Popen(
                    ["pythonw", script_path],
                    creationflags=subprocess.CREATE_NO_WINDOW,
                    env={**os.environ, "PYTHONUTF8": "1"}
                )
            else:
                process = subprocess.Popen(
                    ["python", script_path],
                    creationflags=subprocess.CREATE_NO_WINDOW,
                    env={**os.environ, "PYTHONUTF8": "1"}
                )
            return f"✅ {agent_name} 시작 완료 (PID: {process.pid})"
        else:  # macOS/Linux
            process = subprocess.Popen(
                ["python3", script_path],
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
                start_new_session=True
            )
            return f"✅ {agent_name} 시작 완료 (PID: {process.pid})"
    except Exception as e:
        return f"❌ {agent_name} 시작 실패: {e}"

def restart_agent(agent_name: str) -> str:
    """에이전트 재시작"""
    stop_result = stop_agent(agent_name)
    import time
    time.sleep(2)
    start_result = start_agent(agent_name)
    return f"{stop_result}\n{start_result}"

def get_agent_status(agent_name: str = None) -> str:
    """에이전트 상태 조회"""
    if agent_name:
        agent_name = get_agent_name(agent_name)
        if agent_name not in AGENTS:
            available = ", ".join(AGENTS.keys())
            return f"❌ 알 수 없는 에이전트: {agent_name}\n사용 가능: {available}"

        pids = find_agent_process(agent_name)
        status = "🟢 실행 중" if pids else "🔴 중지"
        pid_info = f" (PID: {', '.join(map(str, pids))})" if pids else ""
        return f"{agent_name}: {status}{pid_info}"

    # 전체 에이전트 상태
    status_lines = ["📊 에이전트 상태\n"]
    for name in AGENTS.keys():
        pids = find_agent_process(name)
        status = "🟢" if pids else "🔴"
        pid_info = f" (PID: {', '.join(map(str, pids))})" if pids else ""
        status_lines.append(f"{status} {name}{pid_info}")

    return "\n".join(status_lines)

def handle_agent_command(command: str) -> str:
    """에이전트 제어 명령 처리

    명령어 형식:
    - <에이전트명> 시작 / start <agent>
    - <에이전트명> 종료 / stop <agent>
    - <에이전트명> 재시작 / restart <agent>
    - <에이전트명> 상태 / status <agent>
    - 에이전트상태 / agent status (전체)
    """
    command = command.strip().lower()

    # "에이전트상태" - 전체 상태
    if command in ["에이전트상태", "agentstatus"]:
        return get_agent_status()

    # 명령어 파싱
    parts = command.split()
    if len(parts) < 2:
        return """❓ 명령어 형식:
• <에이전트명> 시작 (예: 데이브 시작)
• <에이전트명> 종료 (예: 레오 종료)
• <에이전트명> 재시작
• <에이전트명> 상태
• 에이전트상태 (전체)

사용 가능한 에이전트: """ + ", ".join(AGENTS.keys())

    # "데이브 시작" 형식
    agent_name = parts[0]
    action = parts[1]

    # "start dave" 형식도 지원
    if agent_name in ["start", "stop", "restart", "status"]:
        action = agent_name
        agent_name = parts[1] if len(parts) > 1 else None
        if not agent_name:
            return "❌ 에이전트 이름을 지정하세요"

    agent_name = get_agent_name(agent_name)

    if action in ["시작", "start", "켜", "켜줘"]:
        return start_agent(agent_name)
    elif action in ["종료", "stop", "끄", "꺼", "꺼줘"]:
        return stop_agent(agent_name)
    elif action in ["재시작", "restart", "리스타트"]:
        return restart_agent(agent_name)
    elif action in ["상태", "status"]:
        return get_agent_status(agent_name)
    else:
        return f"❌ 알 수 없는 동작: {action}\n사용 가능: 시작, 종료, 재시작, 상태"

if __name__ == "__main__":
    if len(sys.argv) > 1:
        command = " ".join(sys.argv[1:])
        result = handle_agent_command(command)
        print(result)
    else:
        print("사용법: python agent_controller.py <명령어>")
        print(handle_agent_command("help"))
