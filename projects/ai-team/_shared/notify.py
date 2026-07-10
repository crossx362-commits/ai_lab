"""ai-team 데몬 상태 조회 헬퍼 — 프로세스/launchd 생존 여부만 다룬다.

텔레그램 발신은 `_shared/telegram.py`로 분리됨(2026-07-09 재구축) — 이 모듈은
send/report 등을 더 이상 갖지 않는다. status_report()는 텔레그램 API를 안 건드리는
순수 상태-텍스트 생성기라 여기 남아있다(호출부가 그 문자열을 어떻게 보낼지는 모른다)."""

from __future__ import annotations

import os
import subprocess
import sys
from datetime import datetime


# 상시 데몬 (프로세스가 계속 떠 있어야 정상)
CONTINUOUS_DAEMONS = {
    "youngsuk": "telegram_receiver.py",
    "yewon": "harness_monitor.py",
    "bomi_qa": "petnna_qa_patrol.py",  # 봄이 — 펫나 QA 상시 순찰 (정기 + 변경 감지)
    "suri_dev": "petnna_dev_engine.py",  # 수리 — 펫나 자동 개선 엔진 (QA 결과 → 수정 → 재검수)
    "teo_test": "petnna_test_engineer.py",  # 테오 — E2E 테스트 자동 작성·실행
    "baekho_backend": "petnna_backend_guard.py",  # 백호 — Supabase 계약 감사 (매일)
    "mio_design": "petnna_design_review.py",  # 미오 — 디자인 리뷰 (주 1회 월)
    "namu_pm": "petnna_product_manager.py",  # 나무 — 기획 PM (주 1회 화)
    # Windows 정시 잡 실행자(영숙스케줄) — macOS는 아래 launchd 집계가 이 키를 덮어써 오탐 없음
    "scheduler": "schedule_manager.py",
}

# macOS는 아래 데몬을 launchd 정시 잡으로 운영 → 상시 프로세스가 없어도 launchd에 적재돼 있으면
# 정상(scheduled)으로 본다. (윈도우는 launchd 없음 → 프로세스 기준 그대로). 워치독 오탐 재시작 방지.
_LAUNCHD_FALLBACK = {}

# 펫나 6종 데몬은 이중 가동 방지를 위해 Windows에선 기본 미실행(PETNNA_AGENTS_ON_WINDOWS=true로만 해제,
# 각 데몬 daemon()이 기동 즉시 자진 종료). 이 경우 프로세스 부재가 '정상'이므로 'down'으로 집계하면
# 워치독(check_and_restart_bots)이 매 주기 재시작+텔레그램 스팸을 낸다 → 'disabled'로 표기해 재시작 대상 제외.
_PETNNA_WINDOWS_GATED = {
    "bomi_qa", "suri_dev", "teo_test", "baekho_backend", "mio_design", "namu_pm",
}

# 정시 잡(예원 등)은 단일 스케줄러 데몬이 아니라 잡별 독립 launchd 에이전트로 운영
# (com.ailab.sched.*) — SPOF 제거. 집계로 정상 여부 판단.
SCHED_PREFIX = "com.ailab.sched."

# 예약 실행 서비스 (launchd StartCalendarInterval) — 평소엔 미실행이 정상, 지정 시각에만 실행
SCHEDULED_SERVICES = {
    "yewon_selfheal": "com.ailab.yewon_selfheal", # 자가 점검/복구 (08:00)
    "harness": "com.ailab.harness",              # 시스템 점검 (09:00/21:00)
}

_AGENT_LABELS = {
    "youngsuk": "영숙 (텔레그램 비서)",
    "scheduler": "정시 잡 (예원 — launchd 잡별 분리)",
    "yewon": "예원 (CEO 하네스 모니터)",
    "yewon_selfheal": "예원 (자가 점검/복구)",
    "bomi_qa": "봄이 (펫나 QA 순찰)",
    "suri_dev": "수리 (펫나 자동 개선)",
    "teo_test": "테오 (펫나 E2E 테스트)",
    "baekho_backend": "백호 (펫나 백엔드 감사)",
    "mio_design": "미오 (펫나 디자인 리뷰)",
    "namu_pm": "나무 (펫나 기획)",
    "harness": "하네스 (시스템 점검)",
}


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

    # pgrep 금지(2026-07-02): BSD(macOS) pgrep은 자기 조상 프로세스를 매칭에서 제외한다 —
    # 데몬이 자기/부모 상태를 조회하면 항상 down 오탐(워치독 점검의 yewon=down 깜빡임,
    # 대시보드 자기상태 down의 원인). ps 전체 파싱은 조상 제외가 없어 정확하다.
    try:
        result = subprocess.run(["ps", "-axo", "pid=,command="], capture_output=True, text=True, timeout=5)
        pids = []
        for line in result.stdout.splitlines():
            pid, _, cmd = line.strip().partition(" ")
            cmd = cmd.lower()
            # Windows 분기와 동일하게 python 실행 프로세스만 — 스크립트명을 '언급'만 한
            # 셸/grep 오탐 방지
            if pid.isdigit() and script_file in cmd and "python" in cmd:
                pids.append(pid)
        return pids
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
        if parts and parts[-1] == label:  # 마지막 컬럼=라벨 정확 일치 (접두사 겹침으로 인한 오탐 방지)
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
    값: 'up,<pid>' | 'scheduled' | 'sched:<n>'(정시 잡 n개) | 'down' | 'disabled' | 'misconfig'."""
    status: dict[str, str] = {}
    # 게이트 플래그의 '부재'와 '명시적 OFF'를 구분한다(2026-07-10 함대 전멸 사고).
    # 부재 = .env 재암호화 중 유실됐을 수 있다 → 'misconfig'(경보 대상).
    # 명시적 false = 이 기계에선 안 돌리는 게 설계상 정상 → 'disabled'(경보/재시작 제외).
    # 둘을 같은 'disabled'로 인코딩하면 안전장치가 침묵장치로 뒤집힌다.
    gate = os.getenv("PETNNA_AGENTS_ON_WINDOWS")
    for name, script in CONTINUOUS_DAEMONS.items():
        pids = _find_pids(script)
        if pids:
            status[name] = ",".join(pids)
        elif (sys.platform == "win32" and name in _PETNNA_WINDOWS_GATED
              and gate != "true"):
            status[name] = "disabled" if gate is not None else "misconfig"
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
        elif state == "disabled":
            mark = "⚪ 비활성 (이 기계에선 미운영)"
        elif state == "misconfig":
            mark = "🟠 설정 유실 — PETNNA_AGENTS_ON_WINDOWS 없음 (.env 재암호화 확인)"
        elif state == "scheduled":
            mark = "🟢 정상 (예약 실행 대기)"
        elif state.startswith("sched:"):
            mark = f"🟢 정상 ({state.split(':')[1]}개 잡 예약)"
        else:
            mark = f"🟢 실행 중 (pid {state})"
        lines.append(f"- {label}: {mark}")
    return "\n".join(lines)


status = agent_status
