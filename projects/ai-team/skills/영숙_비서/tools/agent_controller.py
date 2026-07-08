"""Start, stop, and inspect the currently installed ai-team daemons."""

from __future__ import annotations

import os
import subprocess
import sys
import time
from datetime import datetime
from pathlib import Path


if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")


HERE = Path(__file__).resolve()
AI_TEAM_ROOT = HERE.parents[3]
PROJECT_ROOT = AI_TEAM_ROOT.parents[1]
LOG_DIR = PROJECT_ROOT / "output" / "bot_logs"
CREATE_NO_WINDOW = subprocess.CREATE_NO_WINDOW if sys.platform == "win32" else 0

if str(AI_TEAM_ROOT) not in sys.path:
    sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.env import load_env


load_env(str(PROJECT_ROOT))


AGENTS = {
    "영숙": {
        "script": AI_TEAM_ROOT / "skills" / "영숙_비서" / "tools" / "telegram_receiver.py",
        "args": [],
        "log": "youngsuk_telegram",
    },
    # 정시 잡 실행자 — macOS는 잡별 launchd 에이전트(com.ailab.sched.*), Windows는 이 데몬이 유일한
    # 실행자(2026-07-02 복구: launchd 이관이 Windows 정시 잡을 통째로 정지시켰던 사고).
    "영숙스케줄": {
        "script": AI_TEAM_ROOT / "skills" / "영숙_비서" / "tools" / "schedule_manager.py",
        "args": ["--daemon"],
        "log": "youngsuk_scheduler",
    },
    "소미": {
        "script": AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools" / "somi_price_monitor.py",
        "args": [],
        "log": "somi_price_monitor",
    },
    "소미제안": {
        "script": AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools" / "somi_trade_advisor.py",
        "args": ["--daemon"],
        "log": "somi_trade_advisor",
    },
    "소미포지션": {
        "script": AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools" / "somi_position_monitor.py",
        "args": ["--daemon"],
        "log": "somi_position_monitor",
    },
    "추세알림": {
        "script": AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools" / "market_trend_alert.py",
        "args": ["--daemon", "--interval", "900"],
        "log": "market_trend_alert",
    },
    "소미발굴": {
        "script": AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools" / "somi_screener.py",
        "args": ["--daemon", "--times", "09:30,10:30,11:30,13:00,14:00,15:00,15:50"],
        "log": "somi_screener",
    },
    "예원": {
        "script": AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "harness_monitor.py",
        "args": [],
        "log": "yewon_harness_monitor",
    },
    "행크": {
        "script": AI_TEAM_ROOT / "skills" / "행크_미국조사" / "tools" / "us_research.py",
        "args": ["--daemon"],
        "log": "hank_us_research",
    },
    "유나": {
        "script": AI_TEAM_ROOT / "skills" / "유나_아시아조사" / "tools" / "asia_research.py",
        "args": ["--daemon"],
        "log": "yuna_asia_research",
    },
    "레온": {
        "script": AI_TEAM_ROOT / "skills" / "레온_유럽조사" / "tools" / "eu_research.py",
        "args": ["--daemon"],
        "log": "leon_eu_research",
    },
    "마켓데스크": {
        "script": AI_TEAM_ROOT / "skills" / "마켓데스크_시장종합" / "tools" / "market_desk.py",
        "args": ["--daemon"],
        "log": "market_desk",
    },
    "모닝노트": {
        "script": AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools" / "morning_note.py",
        "args": ["--daemon", "--send"],
        "log": "morning_note",
    },
    "성장엔진": {
        "script": AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "growth_engine.py",
        "args": ["--daemon"],
        "log": "yewon_growth_engine",
    },
    "대시보드": {
        "script": AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "status_dashboard.py",
        "args": [],
        "log": "status_dashboard",
    },
}

ALIASES = {
    "youngsuk": "영숙",
    "somi": "소미",
    "somi_trade": "소미제안",
    "trade": "소미제안",
    "ceo": "예원",
    "yewon": "예원",
    "hank": "행크",
    "yuna": "유나",
    "leon": "레온",
    "market": "마켓데스크",
    "marketdesk": "마켓데스크",
    "morning": "모닝노트",
    "morningnote": "모닝노트",
    "trend": "추세알림",
    # harness_monitor 자동재시작은 notify.CONTINUOUS_DAEMONS의 영어 키를 그대로 넘긴다.
    # 아래 매핑이 없으면 Windows에서 해당 데몬 재시작이 조용히 실패한다(자가복구 불능).
    "somi_monitor": "소미",
    "somi_advisor": "소미제안",
    "scheduler": "영숙스케줄",
    "somi_position": "소미포지션",
    "somi_screener": "소미발굴",
    "market_desk": "마켓데스크",
    "yewon_growth": "성장엔진",
    "growth": "성장엔진",
    "dashboard": "대시보드",
}


def get_agent_name(name: str) -> str:
    key = name.strip()
    return ALIASES.get(key.lower(), key)


def _process_query(script_name: str) -> list[int]:
    needle = script_name.lower().replace("'", "''")
    if sys.platform == "win32":
        command = (
            "Get-CimInstance Win32_Process | "
            "Where-Object { $_.Name -match '^python' -and $_.CommandLine -and "
            f"$_.CommandLine.ToLower().Contains('{needle}') }} | "
            "Select-Object -ExpandProperty ProcessId"
        )
        result = subprocess.run(
            ["powershell", "-NoProfile", "-Command", command],
            capture_output=True,
            text=True,
            timeout=5,
            creationflags=CREATE_NO_WINDOW,
        )
        return [int(pid) for pid in result.stdout.split() if pid.isdigit()]

    result = subprocess.run(["pgrep", "-f", needle], capture_output=True, text=True, timeout=5)
    return [int(pid) for pid in result.stdout.split() if pid.isdigit()]


def find_agent_process(agent_name: str) -> list[int]:
    info = AGENTS.get(get_agent_name(agent_name))
    return _process_query(info["script"].name) if info else []


def stop_agent(agent_name: str) -> str:
    name = get_agent_name(agent_name)
    if name not in AGENTS:
        return f"알 수 없는 에이전트: {name}\n사용 가능: {', '.join(AGENTS)}"
    pids = find_agent_process(name)
    if not pids:
        return f"{name}은 실행 중이 아닙니다."
    for pid in pids:
        if sys.platform == "win32":
            subprocess.run(["taskkill", "/F", "/PID", str(pid)], capture_output=True, creationflags=CREATE_NO_WINDOW)
        else:
            subprocess.run(["kill", str(pid)], capture_output=True)
    return f"{name} 종료 완료 (PID: {', '.join(map(str, pids))})"


def start_agent(agent_name: str) -> str:
    name = get_agent_name(agent_name)
    info = AGENTS.get(name)
    if not info:
        return f"알 수 없는 에이전트: {name}\n사용 가능: {', '.join(AGENTS)}"
    pids = find_agent_process(name)
    if pids:
        return f"{name} 이미 실행 중 (PID: {', '.join(map(str, pids))})"
    script = info["script"]
    if not script.exists():
        return f"스크립트가 없습니다: {script}"

    LOG_DIR.mkdir(parents=True, exist_ok=True)
    with (LOG_DIR / f"{info['log']}.out.log").open("ab") as out, (LOG_DIR / f"{info['log']}.err.log").open("ab") as err:
        process = subprocess.Popen(
            [sys.executable, str(script), *info["args"]],
            cwd=str(PROJECT_ROOT),
            stdout=out,
            stderr=err,
            creationflags=CREATE_NO_WINDOW,
            # PYTHONUNBUFFERED(2026-07-03): 버퍼링 때문에 데몬 로그가 비어 관찰 불가 —
            # 미장소미 첫 세션 검증 불가 사례. 라인 단위 즉시 flush.
            env={**os.environ, "PYTHONUTF8": "1", "PYTHONUNBUFFERED": "1"},
        )
    return f"{name} 시작 완료 (PID: {process.pid})"


def restart_agent(agent_name: str) -> str:
    stop_result = stop_agent(agent_name)
    time.sleep(1)
    return f"{stop_result}\n{start_agent(agent_name)}"


def get_agent_status(agent_name: str | None = None) -> str:
    if agent_name:
        name = get_agent_name(agent_name)
        if name not in AGENTS:
            return f"알 수 없는 에이전트: {name}\n사용 가능: {', '.join(AGENTS)}"
        pids = find_agent_process(name)
        return f"{name}: {'실행 중' if pids else '중지'}" + (f" (PID: {', '.join(map(str, pids))})" if pids else "")

    lines = ["에이전트 상태"]
    for name in AGENTS:
        pids = find_agent_process(name)
        lines.append(f"- {name}: {'실행 중' if pids else '중지'}" + (f" (PID: {', '.join(map(str, pids))})" if pids else ""))
    return "\n".join(lines)


# ── 전체 봇 원격 종료/기동 (텔레그램 "봇 다 꺼/켜") ──────────────────────
# 종료 시에도 유지할 봇: 영숙(텔레그램 제어통로)·예원(워치독 — 플래그 준수하며 부활 담당)·대시보드(무해).
_KEEP_ON_SHUTDOWN = {"영숙", "예원", "대시보드"}
# 플래그: 존재하면 워치독이 다운 봇을 되살리지 않음(부활 억제). 텔레그램 재기동 통로는 유지.
BOTS_OFF_FLAG = PROJECT_ROOT / "output" / "cache" / "BOTS_OFF"
# launchd KeepAlive 트레이딩 봇(kill론 부활) — launchctl로 정지/재적재해야 함.
_KEEPALIVE_LABELS = {"소미": "com.ailab.somi_monitor"}


def _launchctl(action: str, label: str) -> None:
    uid = os.getuid() if hasattr(os, "getuid") else 0
    if action == "off":
        subprocess.run(["launchctl", "bootout", f"gui/{uid}/{label}"], capture_output=True)
    else:  # on — 재적재(부팅). 이미 적재면 kickstart로 기동.
        plist = Path.home() / "Library" / "LaunchAgents" / f"{label}.plist"
        subprocess.run(["launchctl", "bootstrap", f"gui/{uid}", str(plist)], capture_output=True)
        subprocess.run(["launchctl", "kickstart", "-k", f"gui/{uid}/{label}"], capture_output=True)


def stop_all_bots() -> str:
    """맥 봇 전체 정지 — 플래그 세워 워치독 부활 억제 + 개별 종료. 제어통로(영숙·워치독·대시보드)는 유지."""
    from _shared.notify import CONTINUOUS_DAEMONS  # noqa: E402
    BOTS_OFF_FLAG.parent.mkdir(parents=True, exist_ok=True)
    BOTS_OFF_FLAG.write_text(datetime.now().isoformat(), encoding="utf-8")
    stopped, kept = [], []
    for key in CONTINUOUS_DAEMONS:
        name = get_agent_name(key)  # 영어 키 → 한국어 에이전트명
        if name in _KEEP_ON_SHUTDOWN or name not in AGENTS:
            kept.append(name)
            continue
        if name in _KEEPALIVE_LABELS and sys.platform == "darwin":  # launchd KeepAlive → launchctl 정지(부활 차단)
            _launchctl("off", _KEEPALIVE_LABELS[name])
            stopped.append(f"{name}(launchd)")
        else:                                   # 워치독 관리 → kill (플래그로 재기동 차단)
            if find_agent_process(name):
                stop_agent(name)
            stopped.append(name)
    return ("🛑 맥 봇 전체 정지 (플래그 ON — 워치독 부활 억제)\n"
            f"정지({len(stopped)}): {', '.join(sorted(set(stopped)))}\n"
            f"유지({len(set(kept))}): {', '.join(sorted(set(kept)))}\n"
            "다시 켜려면: '봇 다 켜'")


def start_all_bots() -> str:
    """맥 봇 전체 기동 — 플래그 해제 후 재기동(워치독이 나머지도 자동 복구)."""
    from _shared.notify import CONTINUOUS_DAEMONS  # noqa: E402
    if BOTS_OFF_FLAG.exists():
        BOTS_OFF_FLAG.unlink()
    started = []
    for key in CONTINUOUS_DAEMONS:
        name = get_agent_name(key)
        if name in _KEEP_ON_SHUTDOWN or name not in AGENTS:
            continue
        if name in _KEEPALIVE_LABELS and sys.platform == "darwin":
            _launchctl("on", _KEEPALIVE_LABELS[name])
            started.append(f"{name}(launchd)")
        else:
            if not find_agent_process(name):
                start_agent(name)
            started.append(name)
    return ("▶️ 맥 봇 전체 기동 (플래그 OFF)\n"
            f"기동({len(started)}): {', '.join(sorted(set(started)))}\n"
            "누락분은 워치독이 5분 내 자동 복구합니다.")


def handle_agent_command(command: str) -> str:
    command = command.strip()
    low = command.lower().replace(" ", "")
    if low in {"allstop", "봇다꺼", "전체종료", "맥봇종료", "봇전부종료"}:
        return stop_all_bots()
    if low in {"allstart", "봇다켜", "전체시작", "맥봇시작", "봇전부시작"}:
        return start_all_bots()
    if command.lower() in {"agentstatus", "status", "상태", "에이전트상태"}:
        return get_agent_status()

    parts = command.split()
    if len(parts) < 2:
        return "형식: <에이전트명> <시작|종료|재시작|상태>\n사용 가능: " + ", ".join(AGENTS)

    agent_name, action = parts[0], parts[1]
    if agent_name.lower() in {"start", "stop", "restart", "status"}:
        action, agent_name = agent_name, parts[1]

    if action in {"시작", "start", "켜", "켜줘"}:
        return start_agent(agent_name)
    if action in {"종료", "stop", "꺼", "꺼줘"}:
        return stop_agent(agent_name)
    if action in {"재시작", "restart"}:
        return restart_agent(agent_name)
    if action in {"상태", "status"}:
        return get_agent_status(agent_name)
    return f"알 수 없는 동작: {action}\n사용 가능: 시작, 종료, 재시작, 상태"


if __name__ == "__main__":
    print(handle_agent_command(" ".join(sys.argv[1:]) if len(sys.argv) > 1 else "status"))
