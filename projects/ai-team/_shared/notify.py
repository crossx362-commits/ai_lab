"""Unified notification - Telegram + agent status."""
import json
import os
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

_AGENTS = {
    "yewon": "예원_CEO/tools/yewon_dispatcher.py",
    "youngsuk": "영숙_비서/tools/telegram_receiver.py",
    "kodari": "코다리_개발자/tools/web_preview.py",
    "kevin": "케빈_인프라/tools/vercel_manager.py",
    "timo": "티모_디자이너/tools/petnna_reviewer.py",
    "hyunbin": "현빈_전략가/tools/crypto_market_intelligence.py",
    "dave": "데이브_주식/tools/upbit_auto_trader.py",
    "leo": "레오_트레이더/tools/leo_aggressive_trader.py",
    "kyungsu": "경수_수사관/tools/comment_forensics.py",
    "royul": "로율_변호사/tools/tax_simulator.py",
}


def _find_pids(script_name: str) -> list[str]:
    """Find PIDs running given script (Windows only)."""
    import subprocess
    # 파일명만 추출 (경로 제거)
    script_file = script_name.split('/')[-1].lower()
    cmd = (
        "Get-CimInstance Win32_Process | "
        "Where-Object { $_.Name -match '^python' -and $_.CommandLine -and "
        f"$_.CommandLine.ToLower().Contains('{script_file}') }} | "
        "Select-Object -ExpandProperty ProcessId"
    )
    try:
        out = subprocess.run(
            ["powershell", "-NoProfile", "-Command", cmd],
            capture_output=True,
            text=True,
            timeout=5,
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


# Aliases
telegram = send
status = agent_status
