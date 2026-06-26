import json
import os
import subprocess
import sys

_here = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(_here, "..", "..", "..", "..", ".."))
AI_TEAM_ROOT = os.path.join(PROJECT_ROOT, "projects", "ai-team")
sys.path.insert(0, PROJECT_ROOT)
sys.path.insert(0, AI_TEAM_ROOT)

from _shared.llm import text as llm_text
from _shared.notify import report

ACTIVE_AGENT_KEYWORDS = {
    "somi": ["소미", "somi", "국내주식", "종목", "수급", "세력", "큰 수익", "매수", "매수판단", "숏커버링", "공매도", "대차잔고", "우리기술"],
    "youngsuk": ["영숙", "youngsuk", "비서", "일정", "캘린더", "스케줄", "리포트 정리", "상태", "현황"],
    "ceo": ["예원", "ceo", "하네스", "harness", "check_all", "스킬", "감사", "분배", "라우팅"],
}

DISPATCH_SYSTEM = """당신은 CEO 예원입니다.
현재 활성 에이전트는 세 명뿐입니다.
- somi: 국내주식 수급, 세력상황, 큰 수익 가능성, 매수판단 리포트
- youngsuk: 텔레그램 비서, 일정, 스케줄, 상태 조회, 리포트 정리
- ceo: 하네스 체크, 스킬 감사, 작업 분배, 시스템 점검

JSON 객체만 반환하세요.
{"agent":"somi|youngsuk|ceo","action":"짧은 작업 요약"}
"""


def _parse_dispatch_decision(raw: str | None) -> dict | None:
    if not raw:
        return None
    text = raw.strip()
    candidates = [text]
    start = text.find("{")
    end = text.rfind("}")
    if start >= 0 and end > start:
        candidates.append(text[start:end + 1])
    for candidate in candidates:
        try:
            parsed = json.loads(candidate)
            return parsed if isinstance(parsed, dict) else None
        except Exception:
            continue
    return None


def _keyword_dispatch_decision(message: str) -> dict | None:
    lower = message.lower()
    for agent, keywords in ACTIVE_AGENT_KEYWORDS.items():
        if any(keyword.lower() in lower for keyword in keywords):
            return {"agent": agent, "action": message[:120]}
    return None


def _run_script(args: list[str], timeout: int = 120, extra_env: dict[str, str] | None = None) -> tuple[int, str]:
    result = subprocess.run(
        args,
        cwd=PROJECT_ROOT,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=timeout,
        env={**os.environ, "PYTHONUTF8": "1", **(extra_env or {})},
    )
    return result.returncode, (result.stdout or result.stderr or "").strip()


def _run_harness() -> str:
    script = os.path.join(AI_TEAM_ROOT, "harness", "check_all.py")
    code, output = _run_script(
        [sys.executable, script],
        timeout=60,
        extra_env={"SUPPRESS_TELEGRAM": "true"},
    )
    has_warn = "WARN" in output or "FAIL" in output or code != 0
    note = "WARN/FAIL 감지, 구조 점검 필요" if has_warn else "모든 구조 정상"
    icon = "⚠️" if has_warn else "✅"
    return f"{icon} [예원 CEO] 하네스 체크 완료\n\n{output}\n\n{note}"


def _run_skill_audit() -> str:
    script = os.path.join(AI_TEAM_ROOT, "skills", "예원_CEO", "tools", "skill_auditor.py")
    code, output = _run_script([sys.executable, script, "--check"], timeout=180)
    icon = "✅" if code == 0 else "❌"
    return f"{icon} [예원 CEO] 스킬 문서 감사 완료\n\n{output[:3500]}"


def _run_somi(message: str) -> str:
    script = os.path.join(AI_TEAM_ROOT, "skills", "소미_분석가", "tools", "short_covering_analyzer.py")
    code, output = _run_script([sys.executable, script, "--text", message], timeout=90)
    icon = "✅" if code == 0 else "❌"
    return f"{icon} [소미] 국내주식 수급·매수판단 리포트 완료\n\n{output[:3500]}"


def _run_youngsuk(message: str) -> str:
    if any(k in message for k in ["스케줄", "일정", "schedule"]):
        script = os.path.join(AI_TEAM_ROOT, "skills", "영숙_비서", "tools", "schedule_manager.py")
        code, output = _run_script([sys.executable, script, "--list"], timeout=30)
        icon = "✅" if code == 0 else "❌"
        return f"{icon} [영숙] 스케줄 확인 완료\n\n{output[:2000]}"
    if any(k in message for k in ["리포트 정리", "cleanup"]):
        script = os.path.join(AI_TEAM_ROOT, "skills", "영숙_비서", "tools", "reports_manager.py")
        code, output = _run_script([sys.executable, script, "cleanup"], timeout=60)
        icon = "✅" if code == 0 else "❌"
        return f"{icon} [영숙] 리포트 정리 완료\n\n{output[:2000]}"
    from _shared.notify import status_report
    return status_report()


def dispatch_and_execute(ceo_message: str) -> str:
    report("예원", "업무 지시 처리 시작", ceo_message[:80])
    print(f"  [예원 CEO] 수신된 업무 지시: {ceo_message}")

    direct = _keyword_dispatch_decision(ceo_message)
    if direct:
        decision = direct
    else:
        raw = llm_text(ceo_message, system=DISPATCH_SYSTEM, json_mode=True, max_tokens=200, lm_first=False)
        decision = _parse_dispatch_decision(raw) or _keyword_dispatch_decision(ceo_message)

    if not decision:
        return "예원 CEO가 지시를 해석하지 못했어요. 소미/영숙/예원 중 누구에게 맡길지 한 번만 더 적어주세요."

    agent = str(decision.get("agent", "")).lower()
    print(f"  [예원 CEO] 분배 결정: {agent}")

    if agent in {"somi", "소미"}:
        return _run_somi(ceo_message)
    if agent in {"youngsuk", "영숙", "secretary"}:
        return _run_youngsuk(ceo_message)
    if agent in {"ceo", "예원", "yewon"}:
        if any(k in ceo_message.lower() for k in ["스킬", "감사", "skill"]):
            return _run_skill_audit()
        return _run_harness()

    return f"⚠️ 현재 활성 에이전트가 아닙니다: {decision.get('agent')}. 사용 가능: 소미, 영숙, 예원"
