"""
ollama_health_check.py — 코다리의 Ollama 연동 상태 진단·원인 분석·자동 수복
2시간마다 호출: 연결 확인 → 다운 시 원인 진단(Gemini 폴백) → 재시작 시도 → 보고.
"""
import os
import sys
import subprocess
import time

if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

_here = os.path.dirname(os.path.abspath(__file__))
_ai_team = os.path.abspath(os.path.join(_here, "..", "..", ".."))
if _ai_team not in sys.path:
    sys.path.insert(0, _ai_team)
_root = os.path.abspath(os.path.join(_ai_team, ".."))
from _shared.env_loader import load_env as _load_env
from _shared.telegram_notifier import send_telegram_message
from _shared.ollama_client import is_available, chat as lm_chat
from _shared.resource_utils import get_system_load
import _shared.gemini_client as _gc

_OLLAMA_PORT    = 11434
_TEST_PROMPT    = "파이썬에서 1+1을 출력하는 한 줄 코드만 반환하세요."
_EXPECTED_WORDS = ["print", "1+1", "2"]


def _proc_running() -> bool:
    try:
        r = subprocess.run(["pgrep", "-f", "ollama"],
                           capture_output=True, text=True)
        return bool(r.stdout.strip())
    except Exception:
        return False


def _port_in_use() -> bool:
    try:
        r = subprocess.run(["lsof", "-i", f":{_OLLAMA_PORT}", "-sTCP:LISTEN"],
                           capture_output=True, text=True)
        return bool(r.stdout.strip())
    except Exception:
        return False


def _gather_diagnostics() -> dict:
    cpu, ram_percent = get_system_load()
    return {
        "proc_running": _proc_running(),
        "port_in_use":  _port_in_use(),
        "cpu_usage":    cpu,
        "ram_usage":    ram_percent,
    }


def _analyze_cause(diag: dict) -> str:
    prompt = (
        "Ollama 로컬 AI 서버가 응답하지 않습니다. 아래 진단 정보를 보고 "
        "원인과 해결책을 한국어로 2~4문장으로 짧게 설명하세요.\n\n"
        f"- Ollama 프로세스 실행 중: {diag['proc_running']}\n"
        f"- 포트 {_OLLAMA_PORT} LISTEN 상태: {diag['port_in_use']}\n"
        f"- CPU 사용률: {diag['cpu_usage']}%\n"
        f"- RAM 사용률: {diag['ram_usage']}%\n"
    )
    result = _gc.text(prompt, task="", max_tokens=300)
    return (result or "원인 분석 불가").strip()


def _try_restart() -> bool:
    """Ollama 서버 재시작 시도."""
    try:
        subprocess.Popen(["ollama", "serve"])
        time.sleep(10)
        return is_available()
    except Exception:
        return False


def _validate_response(resp: str | None) -> bool:
    if not resp:
        return False
    lower = resp.lower()
    return any(w in lower for w in _EXPECTED_WORDS)


def run_check():
    """Ollama 연동 상태 진단 메인. 외부에서 직접 호출 가능."""
    _load_env()

    api_ok = is_available()

    if api_ok:
        resp = lm_chat(_TEST_PROMPT, task="coding", max_tokens=60)
        if _validate_response(resp):
            print("  [코다리] Ollama 정상 (코딩 모델 응답 확인)")
            return
        print(
            f"⚠️ [코다리] Ollama API 응답하나 코딩 모델 출력 품질 이상\n\n"
            f"📋 테스트 응답:\n{(resp or '')[:400]}\n\n"
            "모델이 올바르게 로드됐는지 확인하세요."
        )
        return

    diag  = _gather_diagnostics()
    cause = _analyze_cause(diag)

    diag_text = (
        f"🔍 진단 정보:\n"
        f"• Ollama 프로세스: {'실행 중' if diag['proc_running'] else '없음'}\n"
        f"• 포트 {_OLLAMA_PORT}: {'열려 있음' if diag['port_in_use'] else '닫혀 있음'}\n"
        f"• CPU 사용률: {diag['cpu_usage']}%\n"
        f"• RAM 사용률: {diag['ram_usage']}%\n\n"
        f"🧠 원인 분석:\n{cause}"
    )

    print(
        f"🚨 [코다리] Ollama API 무응답\n\n"
        f"{diag_text}\n\n"
        f"🔄 재시작 시도 중..."
    )

    recovered = _try_restart()

    if recovered:
        print("✅ [코다리] Ollama 재시작 완료 — API 정상")
    else:
        print(
            "❌ [코다리] Ollama 자동 재시작 실패\n\n"
            "수동 조치 필요:\n"
            "1. 터미널에서 `ollama serve` 실행\n"
            "2. `ollama list` 로 모델 확인\n"
            "3. 필요 시 `ollama pull <모델명>` 으로 재설치"
        )


if __name__ == "__main__":
    run_check()
