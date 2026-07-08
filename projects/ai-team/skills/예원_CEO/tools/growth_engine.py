#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""예원 성장엔진 — 자동 학습·자동 개선·자동 에이전트 신설 (2026-07-02 사용자 '완전 자동' 승인).

매 거래일 마감 후(기본 16:10) 1사이클:
  1) 학습(learn)      : 청산일지(순수익)·growth 로그를 통계+LLM으로 종합 → insights.json
  2) 튜닝(tune)       : 모의 한정 파라미터(somi_tuning.json) 규칙 기반 조정 + 성과악화 시 자동 롤백
  3) 자기패치(patch)  : 개선제안 1건/일을 headless claude로 구현 → 컴파일 검증 → 커밋 → 데몬 재시작
                        (실패 시 백업 복원, 화이트리스트 밖 수정은 전체 롤백)
  4) 에이전트 신설    : 주 1회, 역량 공백 감지 시 agent_factory로 생성·등록·기동

안전선(불변):
  - 실거래 주문·실거래 보수값은 절대 건드리지 않는다(모의 _is_paper 범위만).
  - 킬스위치: output/growth/AUTOPILOT_OFF 파일이 있으면 변경 작업 전부 중단(학습·보고만).
  - 튜닝은 advisor의 _TUNING_BOUNDS로 이중 클램프.
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import time
from datetime import datetime, timedelta
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[4]
AI_TEAM_ROOT = PROJECT_ROOT / "projects" / "ai-team"
sys.path.insert(0, str(AI_TEAM_ROOT))
sys.path.insert(0, str(SCRIPT_DIR))

from _shared.env import load_env  # noqa: E402
from _shared.notify import send  # noqa: E402
from _shared.llm import text as llm_text  # noqa: E402
from _shared import growth  # noqa: E402
from _shared.process import ProcessLock  # noqa: E402

load_env(str(PROJECT_ROOT))

GROWTH_DIR = PROJECT_ROOT / "output" / "growth"
KILL_SWITCH = GROWTH_DIR / "AUTOPILOT_OFF"
INSIGHTS_FILE = GROWTH_DIR / "insights.json"
STATE_FILE = GROWTH_DIR / "engine_state.json"
TUNING_FILE = PROJECT_ROOT / "output" / "cache" / "somi_tuning.json"
TRADES_FILE = PROJECT_ROOT / "output" / "cache" / "somi_closed_trades.json"
# 미장(US) 전용 — 국내와 점수 눈금·파일 분리(파이프라인 검토 2026-07-06: 기존엔 학습 되먹임 없이
# 정적 env값(GATE_LO/HI)만 썼음 — 국내와 동일한 학습·롤백 패턴을 미장에도 연결).
TUNING_FILE_US = PROJECT_ROOT / "output" / "cache" / "somi_tuning_us.json"
TRADES_FILE_US = PROJECT_ROOT / "output" / "cache" / "somi_us_closed.json"
TUNING_DEFAULTS_US = {"gate_lo": 44, "gate_hi": 54}
# 하한 하드캡: 43 이하 완화 금지(백테스트 40~54 PF1.16, 35↓ 전멸). 상한: 55↑ 블로우오프 검증 차단 유지.
TUNING_BOUNDS_US = {"gate_lo": (44, 48), "gate_hi": (50, 54)}
CONTROLLER = AI_TEAM_ROOT / "skills" / "영숙_비서" / "tools" / "agent_controller.py"
RUN_AT = os.getenv("GROWTH_ENGINE_TIME", "16:10")
# 자기패치가 수정해도 되는 경로(이 밖을 건드리면 전체 롤백)
PATCH_WHITELIST = ("projects/ai-team/skills/소미_분석가/tools/",
                   "projects/ai-team/skills/예원_CEO/tools/")
# gate_score 기본 60·하한 58(2026-07-02 중소형 전이검증): 실사냥터(코스닥·중소형)에선 55는
# 수급확인 포함해도 손실(-60%), 60+수급확인부터 흑자(+45%) — advisor._TUNING_BOUNDS와 동일 유지.
TUNING_DEFAULTS = {"gate_score": 60, "gate_entry": 55, "observe_minutes": 2, "paper_auto_max": 8}
TUNING_BOUNDS = {"gate_score": (58, 70), "gate_entry": (52, 75),
                 "observe_minutes": (1, 20), "paper_auto_max": (2, 10)}


def _load(path: Path, default):
    try:
        return json.loads(path.read_text(encoding="utf-8")) if path.exists() else default
    except Exception:
        return default


def _save(path: Path, data) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


def _autopilot_on() -> bool:
    return not KILL_SWITCH.exists()


# ── 1) 학습 ──────────────────────────────────────────────
def _recent_trades(days: int = 14, file: Path = TRADES_FILE) -> list[dict]:
    cutoff = (datetime.now() - timedelta(days=days)).strftime("%Y-%m-%d")
    return [t for t in _load(file, [])
            if t.get("source") != "backtest_seed" and str(t.get("ts_close", "")) >= cutoff]


def _stats(trades: list[dict]) -> dict:
    if not trades:
        return {"n": 0}
    rets = [t.get("ret_pct", 0) for t in trades]
    wins = [r for r in rets if r > 0]
    by_reason: dict[str, list[float]] = {}
    for t in trades:
        by_reason.setdefault(t.get("reason", "?"), []).append(t.get("ret_pct", 0))
    winners = [t for t in trades if t.get("ret_pct", 0) > 0 and t.get("max_up_pct") is not None]
    runner_gap = (sum(t["max_up_pct"] - t["ret_pct"] for t in winners) / len(winners)) if winners else 0.0
    return {
        "n": len(trades), "winrate": round(len(wins) / len(rets), 2),
        "avg_net": round(sum(rets) / len(rets), 2), "sum_net": round(sum(rets), 1),
        "stop_share": round(sum(1 for t in trades if t.get("reason") == "stop") / len(trades), 2),
        "early_share": round(sum(1 for t in trades if t.get("reason") == "early_exit") / len(trades), 2),
        "runner_gap": round(runner_gap, 2),  # 승리거래 최고가 대비 못 먹은 수익(%p)
        "by_reason": {k: {"n": len(v), "avg": round(sum(v) / len(v), 2)} for k, v in by_reason.items()},
    }


def learn() -> dict:
    trades = _recent_trades()
    st = _stats(trades)
    logs = growth.collect(limit=120)
    improves = [f'{r.get("agent")}: {r["self_eval"]["improve"]}' for r in logs
                if r.get("self_eval", {}).get("improve")][:15]
    bads = [f'{r.get("agent")}: {r["self_eval"]["bad"]}' for r in logs
            if r.get("self_eval", {}).get("bad")][:15]
    prompt = (
        "너는 자동매매 시스템의 수석 개선책임자다. 아래 최근 14일 데이터로\n"
        "(1) 순수익을 가장 깎는 요인 1~3개, (2) 오늘 적용할 만한 구체적 코드개선 후보 1~3개를 제시하라.\n"
        "개선 후보는 '모의(_is_paper) 범위에서 파일 1~2개 수정으로 끝나는 것'만. JSON으로만 답:\n"
        '{"insights": ["..."], "patch_candidates": [{"title": "...", "instruction": "구현 지시문(파일·함수 지목)", "expected": "..."}]}\n\n'
        f"[거래 통계] {json.dumps(st, ensure_ascii=False)}\n"
        f"[에이전트 자기평가-개선] {improves}\n[자기평가-문제] {bads}"
    )
    # max_tokens 1500(2026-07-02): 로컬(gemma) 폴백 시 700이면 finish=length로 JSON이 잘려
    # 파싱 실패 → 'LLM 종합 실패'. 1500부터 완결(finish=stop) 확인.
    raw = llm_text(prompt, json_mode=True, max_tokens=1500, temperature=0.2, lm_first=False)
    try:
        parsed = json.loads(raw[raw.find("{"):raw.rfind("}") + 1])
    except Exception:
        parsed = {"insights": ["LLM 종합 실패 — 통계만 기록"], "patch_candidates": []}
    out = {"ts": datetime.now().strftime("%Y-%m-%d %H:%M"), "stats": st,
           "insights": parsed.get("insights", []), "patch_candidates": parsed.get("patch_candidates", [])}
    _save(INSIGHTS_FILE, out)
    return out


# ── 2) 모의 파라미터 자동 튜닝 + 롤백 ─────────────────────
def tune(st: dict) -> list[str]:
    data = _load(TUNING_FILE, {"params": dict(TUNING_DEFAULTS), "history": []})
    params = {**TUNING_DEFAULTS, **data.get("params", {})}
    history = data.get("history", [])
    notes: list[str] = []

    # 롤백 점검: 직전 변경 후 표본 5건↑인데 평균 순수익이 기준 대비 1%p↓ → 이전 값 복원
    if history:
        last = history[-1]
        after = [t for t in _recent_trades(7) if str(t.get("ts_close", "")) > last["ts"]]
        if len(after) >= 5 and last.get("kind") != "rollback":
            avg_after = sum(t.get("ret_pct", 0) for t in after) / len(after)
            base = last.get("baseline_avg", 0)
            if avg_after < base - 1.0:
                params = dict(last.get("before", params))
                history.append({"ts": datetime.now().strftime("%Y-%m-%d %H:%M"), "kind": "rollback",
                                "reason": f"변경 후 평균 {avg_after:+.2f}% < 기준 {base:+.2f}%-1.0", "params": params})
                notes.append(f"↩️ 튜닝 롤백 — 변경 후 성과 악화({avg_after:+.2f}%)")

    before = dict(params)
    n = st.get("n", 0)
    recent3 = _recent_trades(3)
    if not notes:  # 롤백한 날은 추가 조정 금지(한 번에 한 손잡이)
        if n and not recent3:
            params["gate_score"] -= 2
            params["gate_entry"] -= 2
            notes.append("3일간 체결 0건 → 문턱 완화(-2)")
        elif n >= 10 and st.get("winrate", 1) < 0.45:
            params["gate_score"] += 2
            params["gate_entry"] += 2
            notes.append(f"승률 {st['winrate']:.0%} < 45% → 문턱 강화(+2)")
        if n >= 8 and st.get("stop_share", 0) > 0.4:
            params["observe_minutes"] += 2
            notes.append(f"손절 비중 {st['stop_share']:.0%} → 관찰시간 +2분")
        if n >= 10 and st.get("avg_net", 0) < 0:
            params["gate_entry"] += 3
            notes.append(f"평균 순수익 {st['avg_net']:+.2f}% < 0 → 진입문턱 +3")

    for k, (lo, hi) in TUNING_BOUNDS.items():
        params[k] = max(lo, min(hi, int(params[k])))

    if params != before:
        history.append({"ts": datetime.now().strftime("%Y-%m-%d %H:%M"), "kind": "tune",
                        "before": before, "params": params,
                        "baseline_avg": st.get("avg_net", 0), "notes": notes})
    # 병합 저장 — somi_tuning.json은 한별(퀀트)의 recommend_min_score 등과 공유하는 파일.
    # 전체 덮어쓰기 금지: 성장엔진 키(params/history)만 갱신하고 나머지 키는 보존한다.
    data.update({"params": params, "history": history[-30:]})
    _save(TUNING_FILE, data)
    return notes


def tune_us(st: dict) -> list[str]:
    """미장(US) 게이트창(gate_lo/gate_hi) 자동 튜닝 — 국내 tune()과 동일한 롤백·클램프 안전장치.
    표본이 국내보다 훨씬 희소(24개월 35~130건 수준)해 신호 발동 최소표본을 국내보다 낮게 잡는다.
    somi_us_trader._tuning_us()가 이 파일(TUNING_FILE_US)을 읽어 다음 스캔부터 즉시 반영(재시작 불요)."""
    data = _load(TUNING_FILE_US, {"params": dict(TUNING_DEFAULTS_US), "history": []})
    params = {**TUNING_DEFAULTS_US, **data.get("params", {})}
    history = data.get("history", [])
    notes: list[str] = []

    if history:
        last = history[-1]
        after = [t for t in _recent_trades(7, TRADES_FILE_US) if str(t.get("ts_close", "")) > last["ts"]]
        if len(after) >= 3 and last.get("kind") != "rollback":
            avg_after = sum(t.get("ret_pct", 0) for t in after) / len(after)
            base = last.get("baseline_avg", 0)
            if avg_after < base - 1.0:
                params = dict(last.get("before", params))
                history.append({"ts": datetime.now().strftime("%Y-%m-%d %H:%M"), "kind": "rollback",
                                "reason": f"변경 후 평균 {avg_after:+.2f}% < 기준 {base:+.2f}%-1.0", "params": params})
                notes.append(f"↩️ 미장 튜닝 롤백 — 변경 후 성과 악화({avg_after:+.2f}%)")

    before = dict(params)
    n = st.get("n", 0)
    recent3 = _recent_trades(3, TRADES_FILE_US)
    if not notes:  # 롤백한 날은 추가 조정 금지(한 번에 한 손잡이)
        if n and not recent3:
            params["gate_lo"] -= 1
            notes.append("미장 3일간 체결 0건 → 하한 완화(-1)")
        elif n >= 5 and st.get("winrate", 1) < 0.4:
            params["gate_lo"] += 1
            notes.append(f"미장 승률 {st['winrate']:.0%} < 40% → 하한 강화(+1)")
        if n >= 5 and st.get("avg_net", 0) < 0:
            params["gate_lo"] += 1
            notes.append(f"미장 평균 순수익 {st['avg_net']:+.2f}% < 0 → 하한 강화(+1)")

    for k, (lo, hi) in TUNING_BOUNDS_US.items():
        params[k] = max(lo, min(hi, int(params[k])))
    if params["gate_lo"] > params["gate_hi"] - 2:   # 최소 창 폭 2 보장(과튜닝으로 창 소멸 방지)
        params["gate_lo"] = params["gate_hi"] - 2

    if params != before:
        history.append({"ts": datetime.now().strftime("%Y-%m-%d %H:%M"), "kind": "tune",
                        "before": before, "params": params,
                        "baseline_avg": st.get("avg_net", 0), "notes": notes})
    data.update({"params": params, "history": history[-30:]})
    _save(TUNING_FILE_US, data)
    return notes


# ── 3) 자기패치 (headless claude) ────────────────────────
def _git(*args: str) -> str:
    # core.quotepath=false: 한글 경로가 8진수 이스케이프로 나오면 화이트리스트 검사·복원이 깨진다
    r = subprocess.run(["git", "-c", "core.quotepath=false", *args], cwd=PROJECT_ROOT,
                       capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=60)
    return r.stdout.strip()


def _changed_files(before: set[str]) -> list[str]:
    now = set(_git("diff", "--name-only").splitlines()) | set(_git("ls-files", "--others", "--exclude-standard").splitlines())
    return sorted(now - before)


def self_patch(insights: dict, state: dict) -> str:
    today = datetime.now().strftime("%Y-%m-%d")
    if state.get("last_patch_date") == today:
        return "오늘 패치 완료분 있음(1건/일 상한)"
    cands = insights.get("patch_candidates", []) + [
        {"title": p["problem"][:60], "instruction": p["fix"], "expected": p.get("effect", "")}
        for p in growth.list_proposals()[:5]
    ]
    if not cands:
        return "패치 후보 없음"
    pick = cands[0]  # learn()이 이미 영향도순 생성, 제안은 후순위

    pre = set(_git("diff", "--name-only").splitlines()) | set(_git("ls-files", "--others", "--exclude-standard").splitlines())
    backup = GROWTH_DIR / "patch_backup"
    if backup.exists():
        shutil.rmtree(backup, ignore_errors=True)
    backup.mkdir(parents=True, exist_ok=True)

    prompt = (
        "다음 개선을 이 저장소에 구현하라. 규칙:\n"
        f"- 수정 허용 경로: {', '.join(PATCH_WHITELIST)} 만. 그 밖 절대 수정 금지.\n"
        "- 실거래 보수값·실거래 경로는 금지. 완화·공격화는 반드시 _is_paper() 분기 안에서만.\n"
        "- 최소 수정(타겟 패치). 리팩터링·의존성 추가 금지. 수정 후 python -m py_compile로 검증하라.\n\n"
        f"[개선 제목] {pick.get('title', '')}\n[지시] {pick.get('instruction', '')}\n[기대효과] {pick.get('expected', '')}"
    )
    try:
        r = subprocess.run(
            'claude -p --allowedTools "Read,Grep,Glob,Edit,Bash(python -m py_compile*)" --max-turns 60',
            input=prompt, cwd=PROJECT_ROOT, capture_output=True, text=True,
            encoding="utf-8", errors="replace", timeout=1500, shell=True,
        )
    except Exception as exc:
        return f"패치 실행 실패(스킵): {exc}"

    changed = _changed_files(pre)
    if not changed:
        # 가시화(2026-07-08 감사): stdout/returncode를 버리고 있어 "claude가 정말 무변경 판단했는지"와
        # "claude 자체가 크래시/타임아웃/거부했는지"를 구분할 수 없었다("auto(growth) 0건 의심"의 원인).
        detail = f"rc={r.returncode}"
        if r.returncode != 0 or not (r.stdout or "").strip():
            tail = (r.stderr or r.stdout or "").strip()[-300:]
            detail += f" stderr/out꼬리: {tail}" if tail else " (출력 없음)"
        return f"패치 무변경 종료({detail}) — {pick.get('title', '')}"

    # 화이트리스트·컴파일 검증, 위반/실패 시 git 복원
    def _revert():
        for f in changed:
            subprocess.run(["git", "checkout", "--", f], cwd=PROJECT_ROOT, capture_output=True)

    if any(not f.replace("\\", "/").startswith(PATCH_WHITELIST) for f in changed):
        _revert()
        return f"⛔ 화이트리스트 밖 수정 감지 → 전체 롤백: {changed}"
    for f in [c for c in changed if c.endswith(".py")]:
        c = subprocess.run([sys.executable, "-m", "py_compile", str(PROJECT_ROOT / f)],
                           capture_output=True, text=True)
        if c.returncode != 0:
            _revert()
            return f"⛔ 컴파일 실패 → 롤백: {f}"

    subprocess.run(["git", "add", *changed], cwd=PROJECT_ROOT, capture_output=True)
    subprocess.run(["git", "commit", "-m", f"auto(growth): {pick.get('title', 'self patch')}\n\n성장엔진 자동 적용",
                    "--", *changed], cwd=PROJECT_ROOT, capture_output=True)
    # 수정 파일이 속한 데몬 재시작(소미 계열만 매핑)
    restart_map = {"somi_trade_advisor.py": "소미제안", "somi_position_monitor.py": "소미포지션",
                   "somi_screener.py": "소미발굴", "somi_price_monitor.py": "소미",
                   "somi_signal_engine.py": "소미신호", "market_trend_alert.py": "추세알림",
                   "morning_note.py": "모닝노트"}
    for f in changed:
        agent = restart_map.get(Path(f).name)
        if agent:
            subprocess.run([sys.executable, str(CONTROLLER), agent, "재시작"],
                           capture_output=True, timeout=60)
    state["last_patch_date"] = today
    return f"✅ 자기패치 적용 — {pick.get('title', '')} ({len(changed)}파일: {', '.join(Path(f).name for f in changed)})"


# ── 4) 에이전트 신설 판단 ─────────────────────────────────
def agent_check(insights: dict, state: dict) -> str:
    last = state.get("last_agent_check", "")
    if last and (datetime.now() - datetime.strptime(last, "%Y-%m-%d")).days < 7:
        return ""
    state["last_agent_check"] = datetime.now().strftime("%Y-%m-%d")
    try:
        from agent_factory import auto_create  # 기존 팩토리(중복검사·일일상한·시범실행 내장)
        from _shared.registry import active_agents
    except Exception as exc:
        return f"팩토리 로드 실패: {exc}"
    roster = [f'{m.get("display", k)}: {m.get("role", "")}' for k, m in active_agents().items()]
    prompt = (
        "국내주식 자동매매 시스템의 상시 에이전트 구성이다. 최근 인사이트를 볼 때 '지금 없어서 순수익을 놓치는'\n"
        "새 역할이 있으면 1개만 제안하라. 기존 역할과 조금이라도 겹치면 needed=false. JSON만:\n"
        '{"needed": true|false, "need": "그 에이전트가 맡을 미해결 작업 설명 1~2문장"}\n\n'
        f"[기존 에이전트] {roster}\n[인사이트] {insights.get('insights', [])}"
    )
    raw = llm_text(prompt, json_mode=True, max_tokens=250, temperature=0.2, lm_first=False)
    try:
        j = json.loads(raw[raw.find("{"):raw.rfind("}") + 1])
    except Exception:
        return "신설 판단 LLM 실패(스킵)"
    if not j.get("needed") or not j.get("need"):
        return "신설 불필요 판단"
    res = auto_create(str(j["need"])[:200])
    return res.get("message", str(res))


# ── 사이클 ────────────────────────────────────────────────
def run_cycle() -> str:
    state = _load(STATE_FILE, {})
    insights = learn()
    st = insights["stats"]
    lines = [f"[성장엔진 / {insights['ts']}]",
             f"14일 순성과: {st.get('n', 0)}건 · 승률 {st.get('winrate', 0):.0%} · 평균 {st.get('avg_net', 0):+.2f}%"
             if st.get("n") else "14일 순성과: 표본 없음"]
    lines += [f"• {i}" for i in insights.get("insights", [])[:3]]

    if _autopilot_on():
        notes = tune(st)
        lines += [f"🔧 {n}" for n in notes] or ["🔧 튜닝: 조정 없음"]
        # 미장(US) 튜닝 — 표본 희소하므로 실제 신호(notes) 있거나 거래가 있었던 날만 보고(매일 '0건' 스팸 방지).
        # 신호 판정 자체는 n=0인 날도 항상 실행(무체결 연속→하한 완화 로직이 조용히 계속 작동해야 함).
        st_us = _stats(_recent_trades(14, TRADES_FILE_US))
        notes_us = tune_us(st_us)
        if notes_us or st_us.get("n"):
            lines += [f"🇺🇸 {n}" for n in notes_us] or [f"🇺🇸 미장 14일 {st_us.get('n', 0)}건 학습(조정 없음)"]
        lines.append(self_patch(insights, state))
        ac = agent_check(insights, state)
        if ac:
            lines.append(ac)
    else:
        lines.append("⏸️ AUTOPILOT_OFF — 학습만 수행(변경 중단)")

    state["last_run_date"] = datetime.now().strftime("%Y-%m-%d")
    _save(STATE_FILE, state)
    report = "\n".join(lines)
    send(report)
    growth.record("yewon_growth", role="성장엔진", data=f"거래 {st.get('n', 0)}건 학습",
                  judgment="; ".join(insights.get("insights", [])[:1]), result=lines[-1],
                  scores={"fit": 22, "evidence": 20, "efficiency": 18, "risk": 18, "brevity": 8})
    return report


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--once", action="store_true", help="즉시 1사이클 실행")
    ap.add_argument("--daemon", action="store_true", help=f"매일 {RUN_AT} 자동 실행")
    args = ap.parse_args()
    if args.once:
        print(run_cycle())
        return
    with ProcessLock("yewon_growth_engine"):
        print(f"[{datetime.now()}] 성장엔진 데몬 시작 (매일 {RUN_AT})")
        while True:
            now = datetime.now()
            state = _load(STATE_FILE, {})
            if now.strftime("%H:%M") >= RUN_AT and state.get("last_run_date") != now.strftime("%Y-%m-%d") \
                    and now.weekday() < 5:
                try:
                    run_cycle()
                except Exception as exc:
                    print(f"[{now}] 사이클 오류: {exc}")
                    send(f"⚠️ 성장엔진 사이클 오류: {exc}")
            time.sleep(60)


if __name__ == "__main__":
    main()
