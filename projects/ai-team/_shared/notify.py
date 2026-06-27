"""Telegram notification and current ai-team daemon status helpers."""

from __future__ import annotations

import json
import os
import subprocess
import sys
import urllib.request
from datetime import datetime


# 상시 데몬 (프로세스가 계속 떠 있어야 정상)
CONTINUOUS_DAEMONS = {
    "youngsuk": "telegram_receiver.py",
    "somi_monitor": "somi_price_monitor.py",
}

# 예약 실행 서비스 (launchd StartCalendarInterval) — 평소엔 미실행이 정상, 지정 시각에만 실행
SCHEDULED_SERVICES = {
    "somi": "com.ailab.somi",                    # 정기 리포트 (15:40)
    "somi_screener": "com.ailab.somi_screener",  # 매수 제안 (09:30/15:50)
    "somi_position": "com.ailab.somi_position",   # 보유 포지션 익절/손절 점검 (장중)
    "harness": "com.ailab.harness",              # 시스템 점검 (09:00/21:00)
}

_AGENT_LABELS = {
    "youngsuk": "영숙 (텔레그램 비서)",
    "somi_monitor": "소미 (실시간 급변동 감시)",
    "somi": "소미 (정기 리포트)",
    "somi_screener": "소미 (매수 제안)",
    "somi_position": "소미 (포지션 익절/손절)",
    "harness": "하네스 (시스템 점검)",
}


def send(msg: str, silent: bool = False) -> bool:
    """Send a Telegram message when credentials are configured."""
    if os.getenv("SUPPRESS_TELEGRAM") == "true":
        print(f"[Telegram suppressed] {msg[:100]}")
        return True

    token = os.getenv("TELEGRAM_BOT_TOKEN")
    chat_id = os.getenv("TELEGRAM_CHAT_ID")
    if not token or not chat_id:
        print("[Telegram] Missing TELEGRAM_BOT_TOKEN or TELEGRAM_CHAT_ID")
        return False

    try:
        payload = json.dumps(
            {
                "chat_id": chat_id,
                "text": msg[:4096],
                "parse_mode": "HTML",
                "disable_notification": silent,
            }
        ).encode("utf-8")
        req = urllib.request.Request(
            f"https://api.telegram.org/bot{token}/sendMessage",
            data=payload,
            headers={"Content-Type": "application/json"},
        )
        with urllib.request.urlopen(req, timeout=10) as response:
            result = json.loads(response.read().decode("utf-8"))
        if result.get("ok"):
            print(f"[Telegram] Sent {len(msg)} chars")
            return True
        print(f"[Telegram] API error: {result}")
        return False
    except Exception as exc:
        print(f"[Telegram] {exc}")
        return False


def _find_pids(script_name: str) -> list[str]:
    script_file = script_name.lower()
    if sys.platform == "win32":
        ps = (
            "Get-CimInstance Win32_Process | "
            "Where-Object { $_.Name -match '^python' -and $_.CommandLine -and "
            f"$_.CommandLine.ToLower().Contains('{script_file}') }} | "
            "Select-Object -ExpandProperty ProcessId"
        )
        try:
            result = subprocess.run(
                ["powershell", "-NoProfile", "-Command", ps],
                capture_output=True,
                text=True,
                timeout=5,
                creationflags=subprocess.CREATE_NO_WINDOW,
            )
            return [pid for pid in result.stdout.split() if pid.isdigit()]
        except Exception:
            return []

    try:
        result = subprocess.run(["pgrep", "-f", script_file], capture_output=True, text=True, timeout=5)
        return [pid for pid in result.stdout.split() if pid.isdigit()]
    except Exception:
        return []


def _launchd_loaded(label: str) -> bool:
    """launchd에 서비스가 적재(예약)돼 있는지 확인."""
    try:
        result = subprocess.run(["launchctl", "list"], capture_output=True, text=True, timeout=5)
    except Exception:
        return False
    for line in result.stdout.splitlines():
        parts = line.split()
        if parts and parts[-1] == label:  # 마지막 컬럼=라벨 정확 일치 (somi/somi_screener 혼동 방지)
            return True
    return False


def agent_status() -> dict[str, str]:
    """상시 데몬은 프로세스, 예약 서비스는 launchd 적재 여부로 상태 판정.
    값: 'up,<pid>' | 'scheduled'(예약 대기) | 'down'."""
    status: dict[str, str] = {}
    for name, script in CONTINUOUS_DAEMONS.items():
        pids = _find_pids(script)
        status[name] = ",".join(pids) if pids else "down"
    for name, label in SCHEDULED_SERVICES.items():
        status[name] = "scheduled" if _launchd_loaded(label) else "down"
    return status


def status_report() -> str:
    lines = [f"에이전트 현황 ({datetime.now().strftime('%Y-%m-%d %H:%M')})"]
    for name, state in agent_status().items():
        label = _AGENT_LABELS.get(name, name)
        if state == "down":
            mark = "🔴 중지"
        elif state == "scheduled":
            mark = "🟢 정상 (예약 실행 대기)"
        else:
            mark = f"🟢 실행 중 (pid {state})"
        lines.append(f"- {label}: {mark}")
    return "\n".join(lines)


def report(agent: str, action: str, detail: str = "") -> None:
    msg = f"[{agent}] {action}"
    if detail:
        msg += f"\n{detail}"
    send(msg, silent=True)


telegram = send
status = agent_status
