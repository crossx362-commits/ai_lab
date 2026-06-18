"""
ollama_health_check.py — 코다리의 Ollama 연동 상태 진단·원인 분석·자동 수복
2시간마다 호출: 연결 확인 → 다운 시 원인 진단(Gemini 폴백) → 재시작 시도 → 보고.
"""
import os
import sys
import subprocess
import time

_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(8):
    if os.path.isfile(os.path.join(_root, "ENV_MANIFEST.json")):
        break
    _root = os.path.dirname(_root)
_ai_team = os.path.join(_root, "projects", "ai-team")
if _ai_team not in sys.path:
    sys.path.insert(0, _ai_team)

from _shared.env import load_env as _load_env
from _shared.llm import ollama as lm_chat
from _shared.resource_utils import get_system_load
import _shared.gemini_client as _gc

def _simple_is_available() -> bool:
    try:
        import urllib.request
        import json
        url = os.getenv("OLLAMA_URL", "http://localhost:11434/v1/chat/completions").replace("/chat/completions", "/models")
        with urllib.request.urlopen(url, timeout=5) as r:
            data = json.loads(r.read())
        return bool(data.get("data", []))
    except Exception:
        return False

_OLLAMA_PORT    = 11434
_TEST_PROMPT    = "파이썬에서 1+1을 출력하는 한 줄 코드만 반환하세요."
_EXPECTED_WORDS = ["print", "1+1", "2"]
_LOG_FILE       = os.path.join(_root, "reports", "history", "kodari_ollama_log.md")


def _kodari_log(msg: str, level: str = "info"):
    """코다리 내부 로그 파일에 기록 (텔레그램 미사용)."""
    import datetime
    os.makedirs(os.path.dirname(_LOG_FILE), exist_ok=True)
    ts = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    icon = {"info": "ℹ️", "warn": "⚠️", "error": "❌", "critical": "🚨"}.get(level, "•")
    with open(_LOG_FILE, "a", encoding="utf-8") as f:
        f.write(f"\n- [{ts}] {icon} [{level.upper()}] {msg}")


def _escalate_if_critical(msg: str):
    """자동 복구 불가 시 예원 CEO 디스패처 통해 코다리에 업무 전달."""
    try:
        import importlib.util as _ilu
        _dp = os.path.join(_ai_team, "skills", "예원_CEO", "tools", "yewon_dispatcher.py")
        spec = _ilu.spec_from_file_location("yewon_dispatcher", _dp)
        mod = _ilu.module_from_spec(spec)
        spec.loader.exec_module(mod)
        mod.dispatch_and_execute(f"코다리야, Ollama 서버가 다운되어 자동 복구에 실패했어. 직접 확인하고 고쳐줘: {msg}")
        print("  [코다리] CEO 디스패처를 통해 복구 업무 전달 완료")
    except Exception as e:
        print(f"  [코다리] 디스패처 전달 실패: {e}")


def _proc_running() -> bool:
    try:
        if sys.platform == "win32":
            r = subprocess.run(
                ["tasklist", "/fi", "imagename eq ollama.exe"],
                capture_output=True, text=True, encoding="utf-8", errors="replace"
            )
            return "ollama.exe" in r.stdout.lower()
        else:
            r = subprocess.run(["pgrep", "-f", "ollama"], capture_output=True, text=True)
            return bool(r.stdout.strip())
    except Exception:
        return False


def _port_in_use() -> bool:
    try:
        if sys.platform == "win32":
            r = subprocess.run(
                ["netstat", "-ano"],
                capture_output=True, text=True, encoding="utf-8", errors="replace"
            )
            return f":{_OLLAMA_PORT} " in r.stdout
        else:
            r = subprocess.run(
                ["lsof", "-i", f":{_OLLAMA_PORT}", "-sTCP:LISTEN"],
                capture_output=True, text=True
            )
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
        return _simple_is_available()
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

    api_ok = _simple_is_available()

    if api_ok:
        resp = lm_chat(_TEST_PROMPT, task="coding", max_tokens=60)
        if _validate_response(resp):
            print("  [코다리] Ollama 정상 (코딩 모델 응답 확인)")
            return
        # 품질 이상 → 코다리 내부 로그만
        _kodari_log("Ollama API 응답하나 코딩 모델 출력 품질 이상", level="warn")
        return

    diag  = _gather_diagnostics()
    cause = _analyze_cause(diag)

    diag_msg = (
        f"Ollama API 무응답\n"
        f"프로세스: {'실행중' if diag['proc_running'] else '없음'} | "
        f"포트{_OLLAMA_PORT}: {'열림' if diag['port_in_use'] else '닫힘'} | "
        f"CPU:{diag['cpu_usage']}% RAM:{diag['ram_usage']}%\n"
        f"원인: {cause}"
    )
    _kodari_log(diag_msg, level="error")
    print(f"  [코다리] {diag_msg}")
    print("  [코다리] Ollama 재시작 시도 중...")

    recovered = _try_restart()

    if recovered:
        _kodari_log("Ollama 재시작 완료 — API 정상", level="info")
        print("  [코다리] ✅ Ollama 재시작 완료")
    else:
        err_msg = (
            "Ollama 자동 재시작 실패 — 수동 조치 필요\n"
            "1. `ollama serve` 실행\n"
            "2. `ollama list` 모델 확인\n"
            "3. 필요 시 `ollama pull <모델명>`"
        )
        _kodari_log(err_msg, level="critical")
        print(f"  [코다리] ❌ {err_msg}")
        # 수동 조치 불가능한 경우에만 사장님께 전달 (critical 레벨)
        _escalate_if_critical(err_msg)


if __name__ == "__main__":
    run_check()
