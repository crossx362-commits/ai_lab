"""Unified notification - Telegram + agent status."""
import json
import os
import sys
import urllib.request
import urllib.error
from datetime import datetime


# ==================== TELEGRAM ====================

def send(msg: str, silent: bool = False) -> bool:
    """Send Telegram message."""
    if os.getenv("SUPPRESS_TELEGRAM") == "true":
        print(f"  [Telegram suppressed] {msg[:100]}")
        return True

    token = os.getenv("TELEGRAM_BOT_TOKEN")
    chat_id = os.getenv("TELEGRAM_CHAT_ID")
    if not token or not chat_id:
        print(f"  ❌ [Telegram] Missing credentials")
        return False

    try:
        url = f"https://api.telegram.org/bot{token}/sendMessage"
        payload = json.dumps({
            "chat_id": chat_id,
            "text": msg[:4096],  # Telegram limit
            "parse_mode": "HTML",
            "disable_notification": silent,
        }).encode()

        req = urllib.request.Request(url, data=payload, headers={"Content-Type": "application/json"})
        with urllib.request.urlopen(req, timeout=10) as r:
            res = json.loads(r.read())

        if res.get("ok"):
            print(f"  ✅ [Telegram] Sent {len(msg)} chars")
            return True
        else:
            print(f"  ❌ [Telegram] {res}")
            return False
    except Exception as e:
        print(f"  ❌ [Telegram] {e}")
        return False


# ==================== AGENT STATUS ====================

# 하드코딩 제거 - 자동 스캔 사용
ACTIVE_DAEMONS = {
    "youngsuk": "telegram_receiver.py",
    "youngsuk_schedule": "schedule_manager.py",
    "somi": "somi_kis_reporter.py",
    "yewon_monitor": "harness_monitor.py",
}


def _get_agents():
    """데몬 에이전트만 로드 (온디맨드 제외 — 런타임 체크 대상)"""
    try:
        from .agent_registry import get_agents
        agents = get_agents()
        return {slug: info["script"] for slug, info in agents.items() if info["type"] == "daemon"}
    except Exception:
        return ACTIVE_DAEMONS.copy()

_AGENTS = _get_agents()


def _find_pids(script_name: str) -> list[str]:
    """Find PIDs running given script."""
    import subprocess
    # 파일명만 추출 (경로 제거)
    script_file = script_name.split('/')[-1].lower()
    if sys.platform == "darwin":
        try:
            out = subprocess.run(
                ["pgrep", "-f", script_file],
                capture_output=True,
                text=True,
                timeout=5,
            ).stdout
            return [p for p in out.split() if p.isdigit()]
        except Exception:
            return []

    cmd = (
        "Get-CimInstance Win32_Process | "
        "Where-Object { $_.Name -match '^python' -and $_.CommandLine -and "
        f"$_.CommandLine.ToLower().Contains('{script_file}') }} | "
        "Select-Object -ExpandProperty ProcessId"
    )
    try:
        run_kwargs = {}
        if sys.platform == "win32":
            run_kwargs["creationflags"] = subprocess.CREATE_NO_WINDOW
        out = subprocess.run(
            ["powershell", "-NoProfile", "-Command", cmd],
            capture_output=True,
            text=True,
            timeout=5,
            **run_kwargs,
        ).stdout
        return [p for p in out.split() if p.isdigit()]
    except Exception:
        return []


def agent_status() -> dict[str, str]:
    """Return runtime status of all agents."""
    status = {}
    for name, script in _AGENTS.items():
        pids = _find_pids(script)
        status[name] = ",".join(pids) if pids else "down"
    return status


def status_report() -> str:
    """Generate formatted status report."""
    status = agent_status()
    lines = [f"🤖 Agent Status ({datetime.now().strftime('%Y-%m-%d %H:%M')})"]
    for name, state in status.items():
        emoji = "✅" if state != "down" else "❌"
        lines.append(f"{emoji} {name}: {state}")
    return "\n".join(lines)


def report(agent: str, action: str, detail: str = "") -> None:
    """에이전트가 작업/스케줄 시작·완료 시 영숙에게 보고."""
    msg = f"[{agent}] {action}"
    if detail:
        msg += f"\n{detail}"
    send(msg, silent=True)


# Aliases
telegram = send
status = agent_status
