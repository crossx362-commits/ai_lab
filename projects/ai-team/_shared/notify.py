"""Telegram notification and current ai-team daemon status helpers."""

from __future__ import annotations

import json
import os
import re
import subprocess
import sys
import urllib.error
import urllib.request
from datetime import datetime

# 텔레그램이 지원하는 HTML 태그 — 이 태그가 있을 때만 parse_mode=HTML을 쓴다.
# 평문(태그 없음)은 파싱 자체를 안 하므로 '<', '&' 등 어떤 문자가 와도 400이 원천 불가능.
_TG_HTML_TAG = re.compile(
    r"</?(b|strong|i|em|u|ins|s|strike|del|code|pre|a|tg-spoiler|blockquote)\b", re.IGNORECASE)


# 상시 데몬 (프로세스가 계속 떠 있어야 정상)
CONTINUOUS_DAEMONS = {
    "youngsuk": "telegram_receiver.py",
    "somi_monitor": "somi_price_monitor.py",
    "somi_advisor": "somi_trade_advisor.py",
    "somi_position": "somi_position_monitor.py",
    "somi_signal": "somi_signal_engine.py",
    "trend": "market_trend_alert.py",
    "somi_screener": "somi_screener.py",
    "morning": "morning_note.py",
    "hank": "us_research.py",
    "yuna": "asia_research.py",
    "leon": "eu_research.py",
    "market_desk": "market_desk.py",
    "yewon": "harness_monitor.py",
    "yewon_growth": "growth_engine.py",
    # Windows 정시 잡 실행자(영숙스케줄) — macOS는 아래 launchd 집계가 이 키를 덮어써 오탐 없음
    "scheduler": "schedule_manager.py",
}

# macOS는 아래 데몬을 launchd 정시 잡으로 운영 → 상시 프로세스가 없어도 launchd에 적재돼 있으면
# 정상(scheduled)으로 본다. (윈도우는 launchd 없음 → 프로세스 기준 그대로). 워치독 오탐 재시작 방지.
_LAUNCHD_FALLBACK = {
    "somi_position": "com.ailab.somi_position",
    "somi_screener": "com.ailab.somi_screener",
    # somi_advisor 는 폴백 제거(2026-07-02) — 동적 매수(발굴 주기+고속감시)는 상시 데몬이
    # 필요한데 launchd 정시 잡(com.ailab.somi_screener propose)을 '정상'으로 봐서 워치독이
    # 데몬을 안 살렸다. 이제 프로세스 기준 판정 → down이면 워치독/자가복구가 재기동.
    "hank": "com.ailab.sched.research_us",
    "yuna": "com.ailab.sched.research_asia",
    "leon": "com.ailab.sched.research_eu",
    "market_desk": "com.ailab.sched.research_desk",
}

# 정시 잡(조사팀·예원 등)은 단일 스케줄러 데몬이 아니라 잡별 독립 launchd 에이전트로 운영
# (com.ailab.sched.*) — SPOF 제거. 집계로 정상 여부 판단.
SCHED_PREFIX = "com.ailab.sched."

# 예약 실행 서비스 (launchd StartCalendarInterval) — 평소엔 미실행이 정상, 지정 시각에만 실행
SCHEDULED_SERVICES = {
    "somi": "com.ailab.somi",                    # 정기 리포트 (15:40)
    # somi_screener 는 상시 데몬으로 승격(CONTINUOUS_DAEMONS) — 평일 09:30/15:50 자동 발굴 전송
    # somi_position 은 상시 데몬으로 승격(CONTINUOUS_DAEMONS) — 장중 평일 N분 주기 자동 청산 루프 내장
    "yewon_selfheal": "com.ailab.yewon_selfheal", # 자가 점검/복구 (08:00)
    "harness": "com.ailab.harness",              # 시스템 점검 (09:00/21:00)
}

_AGENT_LABELS = {
    "youngsuk": "영숙 (텔레그램 비서)",
    "scheduler": "정시 잡 (조사팀·예원 — launchd 잡별 분리)",
    "somi_monitor": "소미 (실시간 급변동 감시)",
    "somi_advisor": "소미 (매수 판단/제안)",
    "somi": "소미 (정기 리포트)",
    "somi_signal": "소미 (매수신호 엔진)",
    "trend": "소미 (시장추세 알림)",
    "somi_screener": "소미 (유망종목 발굴)",
    "somi_position": "소미 (포지션 익절/손절)",
    "morning": "소미 (모닝노트 브리핑)",
    "hank": "행크 (미국 시장 조사)",
    "yuna": "유나 (아시아 시장 조사)",
    "leon": "레온 (유럽 시장 조사)",
    "market_desk": "마켓데스크 (시장 종합)",
    "yewon": "예원 (CEO 하네스 모니터)",
    "yewon_selfheal": "예원 (자가 점검/복구)",
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

    def _post(parse_mode: str | None) -> dict:
        body = {"chat_id": chat_id, "text": msg[:4096], "disable_notification": silent}
        if parse_mode:
            body["parse_mode"] = parse_mode
        req = urllib.request.Request(
            f"https://api.telegram.org/bot{token}/sendMessage",
            data=json.dumps(body).encode("utf-8"),
            headers={"Content-Type": "application/json"},
        )
        with urllib.request.urlopen(req, timeout=10) as response:
            return json.loads(response.read().decode("utf-8"))

    wants_html = bool(_TG_HTML_TAG.search(msg))
    try:
        try:
            result = _post("HTML" if wants_html else None)
        except urllib.error.HTTPError as he:
            if he.code != 400 or not wants_html:
                raise
            # HTML 태그가 있어도 본문 나머지가 파싱을 깨면 400 → 평문 폴백(서식은 잃어도 전달 보장)
            result = _post(None)
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


def _sched_count() -> int:
    """적재된 com.ailab.sched.* 정시 잡 개수."""
    try:
        result = subprocess.run(["launchctl", "list"], capture_output=True, text=True, timeout=5)
    except Exception:
        return 0
    return sum(1 for ln in result.stdout.splitlines()
               if ln.split() and ln.split()[-1].startswith(SCHED_PREFIX))


def agent_status() -> dict[str, str]:
    """상시 데몬은 프로세스, 예약 서비스는 launchd 적재 여부로 상태 판정.
    값: 'up,<pid>' | 'scheduled' | 'sched:<n>'(정시 잡 n개) | 'down'."""
    status: dict[str, str] = {}
    for name, script in CONTINUOUS_DAEMONS.items():
        pids = _find_pids(script)
        if pids:
            status[name] = ",".join(pids)
        elif sys.platform != "win32" and name in _LAUNCHD_FALLBACK and _launchd_loaded(_LAUNCHD_FALLBACK[name]):
            status[name] = "scheduled"   # macOS: launchd 정시 잡으로 운영 중 → 정상
        else:
            status[name] = "down"
    # launchd 기반 상태(스케줄러·예약 서비스)는 macOS 전용 — Windows엔 launchd가 없어
    # 항상 'down'으로 오탐되므로 집계에서 제외(예원 하네스 오재시작·알림 스팸 방지).
    if sys.platform != "win32":
        n = _sched_count()
        status["scheduler"] = f"sched:{n}" if n else "down"
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
        elif state.startswith("sched:"):
            mark = f"🟢 정상 ({state.split(':')[1]}개 잡 예약)"
        else:
            mark = f"🟢 실행 중 (pid {state})"
        lines.append(f"- {label}: {mark}")
    return "\n".join(lines)


def publish_report(title: str, body: str) -> bool:
    """보고서·브리핑은 노션에 작성하고 텔레그램엔 '제목 + 노션 링크'만 보낸다.
    노션 불가(키 없음/실패) 시 본문을 텔레그램으로 폴백 전송(보고 유실 방지)."""
    url = ""
    try:
        from _shared import research
        url = research.notion_report(title, body)
    except Exception:
        url = ""
    if url:
        return send(f"📄 {title}\n{url}")
    # 폴백: 노션 실패 → 본문 그대로 전송(분할)
    ok = True
    for i in range(0, len(body), 3900):
        ok = send(body[i:i + 3900]) and ok
    return ok


def report(agent: str, action: str, detail: str = "") -> None:
    msg = f"[{agent}] {action}"
    if detail:
        msg += f"\n{detail}"
    send(msg, silent=True)


telegram = send
status = agent_status
