"""
telegram_health_check.py — 코다리의 텔레그램 봇 상태 진단·자동 수정
2시간마다 호출: 프로세스 확인 → 로그 분석(Ollama) → 오류 시 재시작·보고.
"""
import os
import sys
import json
import signal
import subprocess
import time
import urllib.request
import urllib.error

_here = os.path.dirname(os.path.abspath(__file__))
# tools -> 코다리_개발자 -> skills -> ai-team
_ai_team = os.path.abspath(os.path.join(_here, "..", "..", ".."))
sys.path.insert(0, _ai_team)
_root = os.path.abspath(os.path.join(_ai_team, ".."))

from _shared.env_loader import load_env as _load_env
from _shared.telegram_notifier import send_telegram_message
import _shared.gemini_client as _gc

_BOT_SCRIPT = os.path.join(_root, "projects", "ai-team", "skills", "영숙_비서", "tools", "telegram_receiver.py")
_LOG_FILE   = os.path.join(_root, "projects", "ai-team", "skills", "영숙_비서", "tools", "telegram_receiver.log")


def _get_bot_pid() -> int | None:
    try:
        if sys.platform == "win32":
            # Windows: PowerShell을 사용하여 telegram_receiver.py가 CommandLine에 포함된 python 프로세스를 검색합니다.
            cmd = ["powershell", "-NoProfile", "-Command", "Get-WmiObject Win32_Process | Where-Object { $_.Name -match 'python' -and $_.CommandLine -like '*telegram_receiver.py*' } | Select-Object -ExpandProperty ProcessId"]
            r = subprocess.run(cmd, capture_output=True, text=True, timeout=8)
            pids = [int(p) for p in r.stdout.strip().split() if p.strip().isdigit()]
            # 현재 헬스체크 프로세스 자신의 PID는 제외
            pids = [p for p in pids if p != os.getpid()]
            return pids[0] if pids else None
        else:
            r = subprocess.run(["pgrep", "-f", "telegram_receiver.py"],
                               capture_output=True, text=True)
            pids = [int(p) for p in r.stdout.strip().split() if p.strip()]
            return pids[0] if pids else None
    except Exception:
        return None


def _telegram_api_ok(token: str) -> bool:
    """Telegram getMe 호출로 API 연결 확인."""
    try:
        url = f"https://api.telegram.org/bot{token}/getMe"
        with urllib.request.urlopen(url, timeout=8) as r:
            data = json.loads(r.read())
        return data.get("ok", False)
    except Exception:
        return False


def _read_log_tail(n: int = 60) -> str:
    if not os.path.exists(_LOG_FILE):
        return ""
    try:
        with open(_LOG_FILE, "r", encoding="utf-8", errors="replace") as f:
            lines = f.readlines()
        return "".join(lines[-n:])
    except Exception:
        return ""


def _analyze_log(log: str) -> str:
    """Ollama(DeepSeek)로 로그 분석 → 오류 요약."""
    if not log.strip():
        return "정상"
    prompt = (
        "다음은 Python 텔레그램 봇의 최근 실행 로그입니다.\n"
        "오류(Error/Exception/Traceback/Warning)가 있으면 한국어로 짧게 요약하고 원인·해결책을 제안하세요.\n"
        "문제가 없으면 '정상' 한 단어만 출력하세요.\n\n"
        f"[로그]\n{log[:3000]}"
    )
    result = _gc.text(prompt, task="coding", max_tokens=400)
    return (result or "분석 실패").strip()


def _restart_bot() -> int | None:
    """봇 재시작 후 새 PID 반환."""
    pid = _get_bot_pid()
    if pid:
        try:
            if sys.platform == "win32":
                subprocess.run(["taskkill", "/F", "/PID", str(pid)], capture_output=True)
            else:
                os.kill(pid, signal.SIGTERM)
            time.sleep(2)
        except Exception:
            pass
    log_fd = open(_LOG_FILE, "a", encoding="utf-8")
    if sys.platform == "win32":
        # Windows: 백그라운드 분리 실행 (DETACHED_PROCESS = 0x00000008)
        proc = subprocess.Popen(
            [sys.executable, _BOT_SCRIPT],
            stdout=log_fd, stderr=log_fd,
            creationflags=0x00000008,
        )
    else:
        proc = subprocess.Popen(
            [sys.executable, _BOT_SCRIPT],
            stdout=log_fd, stderr=log_fd,
            start_new_session=True,
        )
    time.sleep(4)
    return _get_bot_pid()


def run_check():
    """봇 상태 진단 메인 함수. 외부에서 직접 호출 가능."""
    _load_env()
    token = os.getenv("TELEGRAM_BOT_TOKEN", "")
    pid = _get_bot_pid()
    api_ok = _telegram_api_ok(token) if token else False
    log_tail = _read_log_tail(60)

    # ① 프로세스도 없고 API도 안 됨 → 즉시 재시작
    if not pid and not api_ok:
        analysis = _analyze_log(log_tail)
        send_telegram_message(
            f"🚨 <b>[코다리 긴급]</b> 텔레그램 봇 프로세스 없음 + API 응답 없음\n\n"
            f"📋 로그 분석:\n{analysis}\n\n🔄 재시작 시도..."
        )
        new_pid = _restart_bot()
        if new_pid:
            send_telegram_message(f"✅ <b>[코다리]</b> 봇 재시작 완료 (PID {new_pid})")
        else:
            send_telegram_message("❌ <b>[코다리]</b> 봇 재시작 실패 — 수동 점검 필요")
        return

    # ② 프로세스는 있으나 API 응답 없음 → 재시작
    if pid and not api_ok:
        analysis = _analyze_log(log_tail)
        send_telegram_message(
            f"⚠️ <b>[코다리]</b> 봇 프로세스 존재(PID {pid})하나 API 무응답\n\n"
            f"📋 분석:\n{analysis}\n\n🔄 재시작 시도..."
        )
        new_pid = _restart_bot()
        msg = f"✅ 재시작 완료 (PID {new_pid})" if new_pid else "❌ 재시작 실패 — 수동 점검 필요"
        send_telegram_message(f"🔧 <b>[코다리]</b> {msg}")
        return

    # ③ 정상 실행 중 → 로그 오류 분석
    analysis = _analyze_log(log_tail)
    if analysis != "정상":
        send_telegram_message(
            f"🔧 <b>[코다리 진단]</b> 봇 실행 중(PID {pid})이나 로그 오류 감지\n\n"
            f"📋 분석:\n{analysis}"
        )
    else:
        print(f"  [코다리] 텔레그램 봇 정상 (PID {pid})")


if __name__ == "__main__":
    run_check()
