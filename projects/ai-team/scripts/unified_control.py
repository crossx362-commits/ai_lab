#!/usr/bin/env python3
"""
통합 제어 시스템 (Unified Control)
봇 제어, 에이전트 제어, 자동 수정을 하나의 스크립트로

사용법:
    python unified_control.py bot status              # 봇 상태
    python unified_control.py bot start               # 봇 시작
    python unified_control.py agent status            # 에이전트 상태
    python unified_control.py agent start 데이브      # 데이브 시작
    python unified_control.py heal                    # 자동 수정
    python unified_control.py heal --daemon           # 데몬 모드
"""
import os
import sys
import subprocess
import platform
import urllib.request
import json
import time
from datetime import datetime

# UTF-8 설정
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")

# 경로 설정
_here = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(_here, "..", ".."))
AI_TEAM_ROOT = os.path.join(PROJECT_ROOT, "projects", "ai-team")
sys.path.insert(0, AI_TEAM_ROOT)

try:
    from _shared.env import load_env
    load_env()
except:
    # env_loader 없으면 직접 로드
    pass

# ============================================================
# 환경 변수 캐싱
# ============================================================
_ENV_LOADED = False
_TOKEN = None
_CHAT_ID = None

def load_env_fast():
    """환경변수 빠른 로드"""
    global _ENV_LOADED, _TOKEN, _CHAT_ID
    if _ENV_LOADED:
        return

    _TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "").strip()
    _CHAT_ID = os.getenv("TELEGRAM_CHAT_ID", "").strip()
    _ENV_LOADED = True

# ============================================================
# 봇 제어
# ============================================================
def get_bot_pids():
    """봇 프로세스 PID 조회"""
    pids = []
    try:
        if platform.system() == "Windows":
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

def bot_status():
    """봇 상태 조회"""
    load_env_fast()

    # 토큰 체크
    token_ok = False
    if _TOKEN:
        try:
            url = f"https://api.telegram.org/bot{_TOKEN}/getMe"
            with urllib.request.urlopen(url, timeout=2) as r:
                result = json.loads(r.read().decode())
                token_ok = result.get('ok', False)
        except:
            pass

    # 프로세스 체크
    pids = get_bot_pids()

    print("📊 봇 상태\n")
    print(f"토큰: {'✅ 정상' if token_ok else '❌ 오류'}")
    print(f"프로세스: {'🟢 실행 중' if pids else '🔴 중지'}")
    if pids:
        print(f"PID: {', '.join(map(str, pids))}")

def bot_start():
    """봇 시작"""
    if get_bot_pids():
        print("⚠️ 봇 이미 실행 중")
        return

    script_path = os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "영숙_비서", "tools", "telegram_receiver.py")

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
        print("✅ 봇 시작")
    except Exception as e:
        print(f"❌ 시작 실패: {e}")

def bot_stop():
    """봇 종료"""
    pids = get_bot_pids()
    if not pids:
        print("⚠️ 실행 중인 봇 없음")
        return

    try:
        if platform.system() == "Windows":
            for pid in pids:
                subprocess.run(["taskkill", "/F", "/PID", str(pid)], capture_output=True)
        else:
            subprocess.run(["kill"] + [str(p) for p in pids])
        print(f"✅ 봇 종료 (PID: {','.join(map(str, pids))})")
    except Exception as e:
        print(f"❌ 종료 실패: {e}")

# ============================================================
# 에이전트 제어
# ============================================================
AGENTS = {
    "시그널": "projects/ai-team/skills/시그널_분석가/tools/market_signal.py",
    "데이브": "projects/ai-team/skills/데이브_주식/tools/upbit_auto_trader.py",
    "레오": "projects/ai-team/skills/레오_트레이더/tools/leo_aggressive_trader.py",
    "영숙": "projects/ai-team/skills/영숙_비서/tools/telegram_receiver.py",
}

AGENT_ALIASES = {
    "signal": "시그널", "pulse": "시그널", "펄스": "시그널",
    "dave": "데이브", "leo": "레오", "youngsuk": "영숙"
}

def normalize_agent_name(name):
    """에이전트 이름 정규화"""
    name = name.strip().lower()
    return AGENT_ALIASES.get(name, name)

def find_agent_pids(agent_name):
    """에이전트 프로세스 찾기"""
    agent_name = normalize_agent_name(agent_name)
    if agent_name not in AGENTS:
        return []

    script_filename = os.path.basename(AGENTS[agent_name])
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
            result = subprocess.run(["powershell", "-NoProfile", "-Command", cmd],
                                    capture_output=True, text=True, timeout=5)
            if result.stdout.strip():
                pids = [int(p) for p in result.stdout.split() if p.strip().isdigit()]
        else:
            result = subprocess.run(["pgrep", "-f", script_filename],
                                    capture_output=True, text=True)
            if result.returncode == 0:
                pids = [int(p) for p in result.stdout.strip().split('\n') if p.strip()]
    except:
        pass

    return pids

def agent_status(agent_name=None):
    """에이전트 상태 조회"""
    if agent_name:
        agent_name = normalize_agent_name(agent_name)
        if agent_name not in AGENTS:
            print(f"❌ 알 수 없는 에이전트: {agent_name}")
            print(f"사용 가능: {', '.join(AGENTS.keys())}")
            return

        pids = find_agent_pids(agent_name)
        status = "🟢 실행 중" if pids else "🔴 중지"
        pid_info = f" (PID: {', '.join(map(str, pids))})" if pids else ""
        print(f"{agent_name}: {status}{pid_info}")
    else:
        print("📊 에이전트 상태\n")
        for name in AGENTS.keys():
            pids = find_agent_pids(name)
            status = "🟢" if pids else "🔴"
            pid_info = f" (PID: {', '.join(map(str, pids))})" if pids else ""
            print(f"{status} {name}{pid_info}")

def agent_start(agent_name):
    """에이전트 시작"""
    agent_name = normalize_agent_name(agent_name)
    if agent_name not in AGENTS:
        print(f"❌ 알 수 없는 에이전트: {agent_name}")
        return

    if find_agent_pids(agent_name):
        print(f"⚠️ {agent_name} 이미 실행 중")
        return

    script_path = os.path.join(PROJECT_ROOT, AGENTS[agent_name])

    try:
        if platform.system() == "Windows":
            if agent_name == "영숙":
                proc = subprocess.Popen(["pythonw", script_path],
                                        creationflags=subprocess.CREATE_NO_WINDOW,
                                        env={**os.environ, "PYTHONUTF8": "1"})
            else:
                proc = subprocess.Popen(["python", script_path],
                                        creationflags=subprocess.CREATE_NO_WINDOW,
                                        env={**os.environ, "PYTHONUTF8": "1"})
        else:
            proc = subprocess.Popen(["python3", script_path],
                                    stdout=subprocess.DEVNULL,
                                    stderr=subprocess.DEVNULL,
                                    start_new_session=True)
        print(f"✅ {agent_name} 시작 (PID: {proc.pid})")
    except Exception as e:
        print(f"❌ {agent_name} 시작 실패: {e}")

def agent_stop(agent_name):
    """에이전트 종료"""
    agent_name = normalize_agent_name(agent_name)
    if agent_name not in AGENTS:
        print(f"❌ 알 수 없는 에이전트: {agent_name}")
        return

    pids = find_agent_pids(agent_name)
    if not pids:
        print(f"⚠️ {agent_name} 실행 중이 아님")
        return

    try:
        if platform.system() == "Windows":
            for pid in pids:
                subprocess.run(["taskkill", "/F", "/PID", str(pid)], capture_output=True)
        else:
            for pid in pids:
                subprocess.run(["kill", str(pid)])

        # 트레이더 종료 시 자동 재시작 방지 플래그
        if agent_name in ["데이브", "레오"]:
            flag_path = os.path.join(PROJECT_ROOT, "projects", "ai-team", "scripts", ".manual_stop")
            with open(flag_path, "w") as f:
                f.write(f"# Manually stopped at {datetime.now().isoformat()}\n")

        print(f"✅ {agent_name} 종료 (PID: {', '.join(map(str, pids))})")
    except Exception as e:
        print(f"❌ {agent_name} 종료 실패: {e}")

# ============================================================
# 자동 수정
# ============================================================
def check_and_fix_env(key):
    """환경 변수 체크 및 자동 수정"""
    env_path = os.path.join(PROJECT_ROOT, ".env")

    if not os.path.exists(env_path):
        print(f"❌ .env 파일 없음")
        return False

    with open(env_path, "r", encoding="utf-8") as f:
        for line in f:
            if line.startswith(f"{key}="):
                value = line.split("=", 1)[1].strip().strip('"').strip("'")
                if value:
                    print(f"✅ {key} 정상")
                    return True
                else:
                    print(f"⚠️ {key} 값 비어있음")
                    return False

    print(f"❌ {key} 키 없음")
    return False

def auto_heal():
    """자동 수정 실행"""
    print("=" * 60)
    print("🏥 에이전트 자가 수정")
    print("=" * 60)
    print()

    # 환경 변수 체크
    print("🔍 환경 변수 체크...")
    check_and_fix_env("GEMINI_API_KEY")
    check_and_fix_env("TELEGRAM_BOT_TOKEN")
    print()

    # 에이전트 상태 체크
    print("🔍 에이전트 상태 체크...")
    agent_status()
    print()

    print("✅ 체크 완료")

# ============================================================
# CLI
# ============================================================
def show_help():
    """도움말 출력"""
    print("""
통합 제어 시스템 (Unified Control)

사용법:
  python unified_control.py <명령> [옵션]

봇 제어:
  bot status              봇 상태 확인
  bot start               봇 시작
  bot stop                봇 종료
  bot restart             봇 재시작

에이전트 제어:
  agent status            전체 에이전트 상태
  agent status <이름>     특정 에이전트 상태
  agent start <이름>      에이전트 시작
  agent stop <이름>       에이전트 종료

자동 수정:
  heal                    자동 수정 실행
  heal --daemon           데몬 모드 (30분마다)

에이전트: 펄스, 데이브, 레오, 영숙

예시:
  python unified_control.py bot status
  python unified_control.py agent start 데이브
  python unified_control.py heal
""")

def main():
    """메인 함수"""
    if len(sys.argv) < 2:
        show_help()
        sys.exit(1)

    command = sys.argv[1].lower()

    # 봇 제어
    if command == "bot":
        if len(sys.argv) < 3:
            print("❌ bot 하위 명령 필요 (status/start/stop/restart)")
            sys.exit(1)

        action = sys.argv[2].lower()
        if action == "status":
            bot_status()
        elif action == "start":
            bot_start()
        elif action == "stop":
            bot_stop()
        elif action == "restart":
            bot_stop()
            time.sleep(2)
            bot_start()
        else:
            print(f"❌ 알 수 없는 명령: {action}")

    # 에이전트 제어
    elif command == "agent":
        if len(sys.argv) < 3:
            agent_status()
            sys.exit(0)

        action = sys.argv[2].lower()

        if action == "status":
            if len(sys.argv) > 3:
                agent_status(sys.argv[3])
            else:
                agent_status()
        elif action == "start":
            if len(sys.argv) < 4:
                print("❌ 에이전트 이름 필요")
                sys.exit(1)
            agent_start(sys.argv[3])
        elif action == "stop":
            if len(sys.argv) < 4:
                print("❌ 에이전트 이름 필요")
                sys.exit(1)
            agent_stop(sys.argv[3])
        else:
            print(f"❌ 알 수 없는 명령: {action}")

    # 자동 수정
    elif command == "heal":
        auto_heal()

    # 도움말
    elif command in ["help", "-h", "--help"]:
        show_help()

    else:
        print(f"❌ 알 수 없는 명령: {command}")
        show_help()
        sys.exit(1)

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n\n중단됨")
        sys.exit(0)
    except Exception as e:
        print(f"\n❌ 오류: {e}")
        sys.exit(1)
