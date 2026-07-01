#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""소미 반자동 매매 보조 — 발굴/점수화 → 매수 제안(진입·손절·목표·이유·위험).

원칙: 자동매수 절대 금지. 제안만 하고, 사용자가 텔레그램에서 승인해야만 매수 실행.
승인/매수/포지션 기록은 영숙 봇(telegram_receiver) + kis_trader가 담당.
"""

from __future__ import annotations

import json
import os
import sys
import time
from datetime import datetime
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
from _shared import research  # noqa: E402
from _shared import growth  # noqa: E402
from _shared.process import ProcessLock  # noqa: E402
from somi_kis_reporter import KISClient, build_input_text, intraday_vwap, buy_pressure_ratio  # noqa: E402
from short_covering_analyzer import (  # noqa: E402
    parse_input_text, calculate_score, to_num,
    entry_score, risk_score, rr_score, data_quality_score,
)
from somi_screener import get_candidates, GOOD_SCORE  # noqa: E402

load_env(str(PROJECT_ROOT))

PROPOSALS_FILE = PROJECT_ROOT / "output" / "cache" / "somi_proposals.json"
POSITIONS_FILE = PROJECT_ROOT / "output" / "cache" / "somi_positions.json"
CLOSED_TRADES_FILE = PROJECT_ROOT / "output" / "cache" / "somi_closed_trades.json"  # 청산 거래 로그(성과추적)

STOP_PCT = 0.05    # 지지선 없을 때 기본 손절 -5%
TARGET_PCT = 0.10  # 저항선 없을 때 기본 목표 +10%


# ── 제안/포지션 저장소 ───────────────────────────────────────
def _load(path: Path) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8")) if path.exists() else {}
    except Exception:
        return {}


def _save(path: Path, data: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


def load_proposals() -> dict:
    return _load(PROPOSALS_FILE)


def get_proposal(key: str) -> dict | None:
    """종목명 또는 코드로 최근 제안 조회."""
    items = load_proposals().get("items", [])
    key = key.strip()
    for it in items:
        sym, nm = it.get("symbol", ""), it.get("name", "")
        if key and (key == sym or key == nm or key in nm):
            return it
    return None


def load_positions() -> dict:
    return _load(POSITIONS_FILE)


def record_position(symbol: str, name: str, entry: float, stop: float, target: float,
                    qty: int, score: int | None = None, extra: dict | None = None) -> None:
    pos = load_positions()
    rec = {
        "name": name, "entry": entry, "stop": stop, "target": target,
        "qty": qty, "ts": datetime.now().strftime("%Y-%m-%d %H:%M"), "score": score,
        # 최대상승/최대하락률 추적 시드 — 포지션 모니터가 매 틱 갱신
        "high_water": entry, "low_water": entry,
    }
    if extra:
        rec.update(extra)  # entry_score/risk_level/dq_state/reasons/익절1·2/trail/slot/regime/news 등
    pos[symbol] = rec
    _save(POSITIONS_FILE, pos)


def remove_position(symbol: str) -> None:
    pos = load_positions()
    if symbol in pos:
        del pos[symbol]
        _save(POSITIONS_FILE, pos)


def set_position_fields(symbol: str, fields: dict) -> None:
    """보유 포지션 메타 일부 갱신(고저점 추적·분할익절 상태·트레일링 등)."""
    pos = load_positions()
    if symbol in pos:
        pos[symbol].update(fields)
        _save(POSITIONS_FILE, pos)


def log_closed_trade(symbol: str, name: str, entry: float, exit_price: float, qty: int,
                     reason: str, ts_open: str = "", score=None, extra: dict | None = None) -> None:
    """청산된 모의 거래를 거래일지에 적재 — 성과추적·주간 조건조합 분석용.
    extra: 진입점수/리스크/데이터품질/최대상승·하락률/매수이유/시간대/시장상태/테마뉴스 등."""
    try:
        log = json.loads(CLOSED_TRADES_FILE.read_text(encoding="utf-8")) if CLOSED_TRADES_FILE.exists() else []
    except Exception:
        log = []
    ret_pct = ((exit_price - entry) / entry * 100) if entry else 0.0
    rec = {
        "symbol": symbol, "name": name, "entry": entry, "exit": exit_price, "qty": qty,
        "ret_pct": round(ret_pct, 2), "reason": reason, "score": score,
        "ts_open": ts_open, "ts_close": datetime.now().strftime("%Y-%m-%d %H:%M"),
    }
    if extra:
        rec.update(extra)
    log.append(rec)
    _save(CLOSED_TRADES_FILE, log)


# ── 매수 제안 생성 ───────────────────────────────────────────
def _levels(parsed: dict) -> tuple[float, float, float]:
    """진입가/손절가/목표가 산출 (지지·저항 우선, 없으면 % 기반)."""
    entry = to_num(parsed.get("close"))
    support = to_num(parsed.get("support_line"))
    resistance = to_num(parsed.get("resistance_line"))
    stop = support if (support and support < entry) else round(entry * (1 - STOP_PCT))
    target = resistance if (resistance and resistance > entry) else round(entry * (1 + TARGET_PCT))
    return entry, stop, target


def _ma5(kis: KISClient, code: str) -> float:
    """일봉 5일 단순이동평균(종가). 진입 품질(현재가 5일선 위) 판정용."""
    try:
        d = kis.daily_prices(code, 6)
        closes = [to_num(r.get("stck_clpr")) for r in d[:5] if r.get("stck_clpr")]
        return sum(closes) / len(closes) if closes else 0.0
    except Exception:
        return 0.0


def analyze_candidate(kis: KISClient, code: str, name: str, realtime: bool = False) -> dict | None:
    """탐지점수(1차) + (realtime=True면) 실시간 진입/리스크/손익비/데이터품질 점수.
    realtime은 API 부담이 커 매수 판단 슬롯의 상위 후보에만 켠다."""
    try:
        parsed = parse_input_text(build_input_text(kis, "제안", code, name))
    except Exception:
        return None
    score, grade, pos, neg = calculate_score(parsed)
    entry, stop, target = _levels(parsed)
    rr = (target - entry) / (entry - stop) if entry > stop else 0  # 손익비(저항 미반영 기본값)
    soomgeup_net = to_num(parsed.get("buy_foreigner_5d")) + to_num(parsed.get("buy_institution_5d"))
    dq = parsed.get("data_quality") or {}
    out = {
        "symbol": code, "name": name, "score": score, "grade": grade,
        "change": parsed.get("change_pct", ""),
        "entry": entry, "stop": stop, "target": target, "rr": round(rr, 2),
        "soomgeup_net": soomgeup_net,  # 기관+외국인 5일 누적 순매수(수급확인 게이트용)
        "score_mode": dq.get("score_mode", "regular"),
        "reasons": pos[:3], "risks": neg[:3] or ["뚜렷한 위험 신호 없음"],
        # realtime 미실행 기본값(보수적: 진입/손익비 미충족 처리)
        "entry_score": 0, "risk_level": 0, "risk_state": "ok",
        "rr_score": 0, "rr_ok": False, "dq_score": 0, "dq_state": "degraded",
        "vwap": 0.0, "buy_pressure": 0.0,
    }
    if not realtime:
        return out
    vwap = intraday_vwap(kis.minute_chart(code))
    bp = buy_pressure_ratio(kis.orderbook(code))
    feat = {
        "close": to_num(parsed.get("close")), "vwap": vwap, "ma5": _ma5(kis, code),
        "high": to_num(parsed.get("high")), "low": to_num(parsed.get("low")),
        "change_pct": parsed.get("change_pct"), "buy_pressure": bp,
        "trading_value": parsed.get("trading_value"), "resistance": parsed.get("resistance_line"),
        "entry": entry, "stop": stop, "target": target, "market_warning": parsed.get("market_warning"),
    }
    e_s, e_pos, e_neg = entry_score(feat)
    r_s, r_state, r_notes = risk_score(feat)
    rr_s, rr_val, rr_ok, rr_notes = rr_score(feat)
    dq_s, dq_state, dq_notes = data_quality_score(dq, vwap, bp)
    out.update({
        "rr": rr_val, "entry_score": e_s, "risk_level": r_s, "risk_state": r_state,
        "rr_score": rr_s, "rr_ok": rr_ok, "dq_score": dq_s, "dq_state": dq_state,
        "vwap": vwap, "buy_pressure": bp,
        "reasons": (pos[:2] + e_pos[:2]) or ["수급 양호"],
        "risks": (neg[:1] + e_neg[:1] + r_notes[:1] + rr_notes[:1]) or ["뚜렷한 위험 없음"],
    })
    return out


def _gate_thresholds() -> dict:
    """매수 게이트 문턱 — 모의(paper)는 공격적 완화, 실거래(live)는 보수값 유지.
    위험관리 축(dq_state·danger)은 모드 무관 차단. 수급미확정은 실거래만 차단(모의는 5일누적 보정 허용)."""
    if _is_paper():
        return {
            "score": int(os.getenv("SOMI_GATE_SCORE_PAPER", "48")),   # 탐지점수 (기본 60 → 48)
            "entry": int(os.getenv("SOMI_GATE_ENTRY_PAPER", "58")),   # 진입점수 (기본 70 → 58)
            "require_rr": os.getenv("SOMI_GATE_RR_PAPER", "false").lower() in {"1", "true", "yes"},
        }
    return {"score": 60, "entry": 70, "require_rr": True}


def _passes_buy_gate(c: dict) -> tuple[bool, str]:
    """기대값 기반 최종 매수 게이트. 모두 통과해야 실매수 허용.
    모의는 점수·진입 문턱↓·손익비 요구 해제·수급미확정 허용(공격적), 실거래는 보수값. 위험축은 공통 차단."""
    th = _gate_thresholds()
    if c.get("score", 0) < th["score"]:
        return False, f"탐지점수 {c.get('score')} < {th['score']}"
    # 수급 미확정: 실거래는 하드 차단(보수). 모의는 장중 당일수급이 원천 미공개라
    # 하드 차단하면 100% 매수 불가 → 5일 누적수급 보정점수로 매수 허용(공격적). 약한 종목은 점수·리스크 게이트가 거른다.
    if not _is_paper() and c.get("score_mode") == "morning_missing_investor_adjusted":
        return False, "당일 외국인/기관 수급 미확정 — 후보 저장만"
    if c.get("dq_state") == "degraded":
        return False, "데이터 품질 미흡(실시간 미확인)"
    if c.get("entry_score", 0) < th["entry"]:
        return False, f"진입점수 {c.get('entry_score')} < {th['entry']}"
    if th["require_rr"] and not c.get("rr_ok"):
        return False, f"손익비 {c.get('rr')} 미달/저항 차단"
    if c.get("risk_state") == "danger":
        return False, "리스크 위험 상태"
    return True, ""


def _decide(c: dict) -> tuple[str, str]:
    ok, why = _passes_buy_gate(c)
    if ok:
        return "매수", ""
    if c.get("score", 0) >= 45:
        return "관찰", why
    return "제외", why


def _format_decision(c: dict, next_check: str = "다음 슬롯") -> str:
    """헌장 의사결정 출력형식 — 종목별 결론/세부점수/가격/이유."""
    decision, why = _decide(c)
    entry = c.get("entry") or 0
    t1 = int(entry * 1.05) if entry else 0
    t2 = max(int(c.get("target") or 0), int(entry * 1.08) if entry else 0)
    return "\n".join([
        f"[{c['name']}({c['symbol']})]",
        f"- 결론: {decision}",
        f"- 탐지점수: {c.get('score', '-')}",
        f"- 진입점수: {c.get('entry_score', '-')}",
        f"- 리스크: {c.get('risk_state', '-')}({c.get('risk_level', '-')})",
        f"- 예상 손익비: {c.get('rr', '-')}",
        f"- 매수 가능 가격: {int(entry):,}",
        f"- 손절가: {int(c.get('stop') or 0):,}",
        f"- 1차 목표가: {t1:,}",
        f"- 2차 목표가: {t2:,}",
        f"- 핵심 이유: {', '.join(c.get('reasons', [])[:3]) or '-'}",
        f"- 제외 이유: {why or '-'}",
        f"- 데이터 품질: {c.get('dq_state', '-')}",
        f"- 다음 확인 시간: {next_check}",
    ])


def _news_candidates() -> list[tuple[str, str]]:
    """뉴스/공시 호재(impact≥+1) + 밸류체인 수혜 종목을 발굴 후보로. (마켓데스크가 채운 issue_impact 기반)"""
    try:
        impact = research.load_issue_impact()
    except Exception:
        return []
    out = []
    for code, v in (impact or {}).items():
        if isinstance(v, dict) and v.get("score", 0) >= 1:
            out.append((str(code), v.get("name", str(code))))
    return out


def make_proposals(candidate_limit: int = 20, min_score: int = GOOD_SCORE) -> list[dict]:
    min_score = int(os.getenv("SOMI_GOOD_SCORE", str(min_score)))  # 표본수집 등 한시적 완화용(기본=GOOD_SCORE)
    kis = KISClient()
    try:
        impact = research.load_issue_impact() or {}
    except Exception:
        impact = {}
    # 후보 = 거래대금 상위(유동성) ∪ 뉴스/공시 호재·밸류체인 종목 (중복 코드 제거)
    universe: dict[str, str] = {}
    for code, name in get_candidates(kis, candidate_limit):
        universe[code] = name
    for code, name in _news_candidates():
        universe.setdefault(code, name)
    # 투자하우스 픽 — Action1 '진입(소미게이트 통과)' 판정 종목을 후보로 승격(+가점, TTL).
    # 이미 마켓데스크 issue_impact가 더 높게 평가했으면 그 점수를 존중한다.
    try:
        for code, v in research.load_house_picks().items():
            universe.setdefault(code, v.get("name", code))
            hp = int(v.get("score", 1) or 1)
            if (impact.get(code) or {}).get("score", 0) < hp:
                impact[code] = {"score": hp, "reason": "투자하우스 진입판정: " + v.get("reason", ""),
                                "source": "house"}
    except Exception:
        pass
    proposals = []
    for code, name in universe.items():
        a = analyze_candidate(kis, code, name)
        if not a:
            time.sleep(0.2)
            continue
        # 뉴스 호재 가점 — 강한 호재(+2)면 수급 기준 10p, 호재(+1)면 5p 완화 (뉴스 주도 매매 반영)
        score_imp = (impact.get(code) or {}).get("score", 0) or 0
        bonus = 10 if score_imp >= 2 else (5 if score_imp >= 1 else 0)
        if a["score"] >= min_score - bonus:
            a["news_bonus"] = bonus
            proposals.append(a)
        time.sleep(0.2)
    proposals.sort(key=lambda x: x["score"], reverse=True)
    return proposals


# ── 뉴스·공시 영향도 반영 매수판단 (LLM) ────────────────────────
NEWS_JUDGE_SYSTEM = """당신은 국내주식 매수판단가 '소미'입니다.
판단은 두 축을 '반드시 함께' 사용합니다:
 (1) 수급 분석 — 세력·거래량·대차/공매도 기반 점수
 (2) 최근 N일(기본 5거래일) 뉴스·공시 누적 영향도 — 그날 하루가 아니라 '며칠간 흐름'으로 본다.

뉴스는 누적·추세로 해석한다:
- 단발성(하루만 뜨고 식은) 뉴스는 노이즈로 보고 비중을 낮춘다.
- 여러 날 반복·강화되는 악재(누적 음수)는 강하게 반영한다 — 수급이 좋아도 판단을 낮춘다.
- 여러 날 지속되는 호재(누적 양수, 후속 보도/실적/수주 등)는 확신을 강화한다.
- 최근일에 가중치를 더 준다(오래된 이슈는 약화). 단, 방향이 바뀌면(악재→해소, 호재→소멸) 최신 흐름을 따른다.
- 상충하는 날들이 섞이면 '반복 빈도'와 '최근성'을 우선한다.

판단 규칙:
- N일 누적 영향도 <= -2(지속/강한 악재): 수급과 무관하게 'avoid'.
- 누적 -1 수준(약한·간헐 악재): 'watch'로 강등하거나 비중·목표를 보수적으로 축소.
- 누적 >= +1(지속 호재): 확신 강화, 목표 상향·손절 여유 검토(과도한 상향 금지).
- 뉴스 데이터가 없으면 수급만으로 판단하되 reason에 "뉴스 정보 없음" 명시.
- 시장 브리프(지수 급락·환율 급등·공포탐욕 극단)가 위험이면 전반적으로 보수적으로 조정.
- 손익비(목표-진입)/(진입-손절)가 1.5 미만이면 'watch' 이하.
- 너는 제안만 한다. 실제 체결은 사용자 승인으로만. 과감한 매수 유도·근거 없는 낙관 금지.

출력은 아래 JSON만. 설명·마크다운 금지:
{
  "verdict": "buy" | "watch" | "avoid",
  "entry": 정수, "stop": 정수, "target": 정수,
  "news_trend": "improving" | "stable" | "deteriorating" | "none",
  "news_reflected": true | false,
  "reason": "수급과 '며칠간 뉴스 흐름'을 함께 언급한 한국어 1~2문장"
}"""

# 뉴스 누적 일수 + 일자별 영향도 히스토리 저장소 (issue_impact가 매번 덮어써져 자체 누적)
NEWS_DAYS = int(os.getenv("SOMI_NEWS_DAYS", "5"))
NEWS_HISTORY_FILE = PROJECT_ROOT / "output" / "cache" / "somi_news_history.json"


def _record_news_snapshot(impact_map: dict) -> dict:
    """오늘자 issue_impact를 종목별 일자 히스토리에 누적(날짜당 1건). 최근 N*2일 보관."""
    hist = _load(NEWS_HISTORY_FILE)
    today = datetime.now().strftime("%Y-%m-%d")
    for sym, v in (impact_map or {}).items():
        if isinstance(v, dict) and v.get("score") is not None:
            hist.setdefault(sym, {})[today] = {"score": v.get("score"), "reason": v.get("reason", "")}
    keep = max(NEWS_DAYS * 2, NEWS_DAYS)
    for sym in list(hist):
        dates = sorted(hist[sym], reverse=True)[:keep]
        hist[sym] = {d: hist[sym][d] for d in dates}
    _save(NEWS_HISTORY_FILE, hist)
    return hist


def _news_block(sym: str, hist: dict) -> tuple[str, float, float, int]:
    """종목의 최근 N일 뉴스 라인(최신순) + 누적합/평균/건수."""
    entries = hist.get(sym, {})
    items = sorted(entries.items(), reverse=True)[:NEWS_DAYS]  # 날짜 내림차순=최신순
    lines, scores = [], []
    for date, v in items:
        s = v.get("score")
        if s is None:
            continue
        scores.append(s)
        lines.append(f"  {date}: impact {s:+d}  — {v.get('reason', '') or '-'}")
    if not scores:
        return "없음", 0.0, 0.0, 0
    return "\n".join(lines), float(sum(scores)), sum(scores) / len(scores), len(scores)


def _parse_json(raw: str | None) -> dict | None:
    if not raw:
        return None
    a, b = raw.find("{"), raw.rfind("}")
    try:
        return json.loads(raw[a:b + 1]) if a >= 0 and b > a else None
    except Exception:
        return None


def _market_brief_summary() -> str:
    try:
        mb = research.load_market_brief()
    except Exception:
        mb = {}
    parts = []
    fx = mb.get("fx") or {}
    if fx.get("USD"):
        parts.append(f"USD/KRW {fx['USD']}")
    try:
        fg = research.fear_greed()
        if fg.get("score") is not None:
            parts.append(f"공포탐욕 {fg['score']}({fg.get('rating', '')})")
    except Exception:
        pass
    if mb.get("comment"):
        parts.append(str(mb["comment"])[:160])
    return " / ".join(parts) or "정보 없음"


def judge_with_news(p: dict, hist: dict, brief: str) -> dict:
    """수급 제안 p에 최근 N일 뉴스 누적 흐름 + 시장 브리프를 LLM으로 종합 → verdict/조정."""
    block, imp_sum, imp_avg, n = _news_block(p["symbol"], hist)
    user = (
        f"[종목] {p['name']}({p['symbol']})  현재가 {int(p['entry'])}  등락 {p['change']}\n"
        f"[수급 분석] 점수 {p['score']}/{p['grade']} · 손익비 {p['rr']}\n"
        f"  강점: {', '.join(p['reasons']) or '-'}\n"
        f"  위험: {', '.join(p['risks']) or '-'}\n"
        f"  잠정 손절 {int(p['stop'])} / 목표 {int(p['target'])}\n\n"
        f"[최근 {NEWS_DAYS}일 뉴스·공시 누적]  (최신순, 날짜별 영향도 -2~+2)\n"
        f"{block if n else '  없음'}\n"
        f"  누적 합계: {imp_sum:+.0f} / 평균: {imp_avg:+.2f}\n\n"
        f"[시장 브리프] {brief}\n\n"
        "위 수급과 '며칠간 뉴스 흐름'을 종합해 매수 판단을 내리고,\n"
        "필요한 만큼만 진입/손절/목표를 조정해 JSON으로만 답하라."
    )
    raw = llm_text(user, system=NEWS_JUDGE_SYSTEM, json_mode=True, max_tokens=300,
                   temperature=0.3, lm_first=False)
    j = _parse_json(raw)
    if not j or "verdict" not in j:
        # LLM 미응답 → 수급만으로 판단, 안전하게 watch 강등
        return {**p, "verdict": "watch", "news_trend": "none" if n == 0 else "stable",
                "news_reflected": n > 0, "news_reason": "뉴스 판단 LLM 미응답 — 수급만 반영"}
    entry = int(j.get("entry") or p["entry"])
    stop = int(j.get("stop") or p["stop"])
    target = int(j.get("target") or p["target"])
    if not (stop < entry < target):  # LLM이 비합리적 레벨 반환 시 수급 기반 원래 레벨 유지
        entry, stop, target = int(p["entry"]), int(p["stop"]), int(p["target"])
    return {
        **p,
        "verdict": str(j.get("verdict", "watch")).lower(),
        "entry": entry, "stop": stop, "target": target,
        "news_trend": str(j.get("news_trend", "none")),
        "news_reflected": bool(j.get("news_reflected", n > 0)),
        "news_reason": str(j.get("reason", "")),
    }


def apply_news_judgment(proposals: list[dict]) -> list[dict]:
    """make_proposals 결과에 최근 N일 뉴스 누적 흐름 반영(후처리). 오늘 영향도를 히스토리에 누적 후 판단."""
    if not proposals:
        return proposals
    try:
        impact_map = research.load_issue_impact()
    except Exception:
        impact_map = {}
    hist = _record_news_snapshot(impact_map)  # 오늘자 누적 후 N일 히스토리 사용
    brief = _market_brief_summary()
    return [judge_with_news(p, hist, brief) for p in proposals]


_VERDICT_ICON = {"buy": "🟢 매수", "watch": "👀 관찰", "avoid": "🔴 회피"}


def _fmt(p: dict) -> str:
    """헌장 [매수 제안] 형식 — 종목/등급/진입이유/진입불가이유/손절/익절/승인필요."""
    won = lambda v: f"{int(v):,}원"
    score = p.get("score", 0)
    verdict = p.get("verdict")
    grade = {"buy": "강함" if score >= 70 else "보통", "watch": "약함", "avoid": "보류"}.get(verdict, "보류")
    entry_reason = p.get("news_reason") or (", ".join(p.get("reasons", [])) or "수급 양호")
    no_reason = ", ".join(p.get("risks", [])) or "뚜렷한 위험 신호 없음"
    lines = [
        "[매수 제안]",
        f"- 종목: {p['name']}({p['symbol']}) · 점수 {score}",
        f"- 제안 등급: {grade}",
        f"- 진입 이유: {entry_reason}",
        f"- 진입하면 안 되는 이유: {no_reason}",
        f"- 손절 기준: {won(p['stop'])}",
        f"- 익절 후보: {won(p['target'])} (손익비 {p['rr']})",
    ]
    mc = p.get("mc")
    if mc:
        lines.append(f"- MC확률: 목표 {mc['p_target']*100:.0f}% vs 손절 {mc['p_stop']*100:.0f}% "
                     f"(우위 {mc['edge']*100:+.0f}%p)")
    lines.append("- 사용자 승인 필요: 예")
    return "\n".join(lines)


def _is_paper() -> bool:
    return os.getenv("KIS_PAPER", "false").strip().lower() in {"1", "true", "yes", "y"}


# 모의 모드 1회 실행 시 자동 매수할 최대 종목 수 (예산 소진 시 자동 중단). 모의=공격적(8).
PAPER_AUTO_MAX = int(os.getenv("SOMI_PAPER_AUTO_MAX", "8"))
SOMI_BUDGET = int(os.getenv("SOMI_BUDGET_PER_TRADE", "1000000"))
# 매수 전 관찰 시간(분): 'buy' 신호가 이 시간 이상 유지돼야 실제 매수. 모의=2분(고공격)/실거래=30분.
OBSERVE_MINUTES = int(os.getenv("SOMI_OBSERVE_MINUTES", "2" if _is_paper() else "30"))
WATCHING_FILE = PROJECT_ROOT / "output" / "cache" / "somi_watching.json"


def _observation_gate(buys: list[dict]) -> tuple[list[dict], list[str]]:
    """'buy' 신호를 바로 매수하지 않고 OBSERVE_MINUTES 만큼 관찰.
    신호가 유지된 종목만 매수 대상으로 반환. (신호 소멸 시 관찰 해제)"""
    watching = _load(WATCHING_FILE)
    now = datetime.now()
    buy_syms = {p["symbol"] for p in buys}
    msgs: list[str] = []
    to_buy: list[dict] = []

    # 더 이상 'buy'가 아닌 관찰 종목은 해제 (신호 소멸)
    for sym in list(watching):
        if sym not in buy_syms:
            msgs.append(f"👋 관찰 해제 — {watching[sym].get('name', sym)} (매수 신호 사라짐)")
            del watching[sym]

    for p in buys:
        sym = p["symbol"]
        w = watching.get(sym)
        if not w:  # 처음 발견 → 관찰만 시작, 매수 안 함
            watching[sym] = {"name": p["name"], "first_ts": now.isoformat(timespec="seconds"), "count": 1}
            msgs.append(f"👀 관찰 시작 — {p['name']}({sym}) · 최소 {OBSERVE_MINUTES}분 지켜본 뒤 매수")
            continue
        w["count"] = w.get("count", 1) + 1
        try:
            elapsed = (now - datetime.fromisoformat(w["first_ts"])).total_seconds() / 60
        except Exception:
            elapsed = OBSERVE_MINUTES
        if elapsed >= OBSERVE_MINUTES:  # 관찰 통과 → 매수
            to_buy.append(p)
            del watching[sym]
            msgs.append(f"✅ 관찰 완료 {int(elapsed)}분({w['count']}회 유지) — {p['name']} 매수 진행")
        else:
            msgs.append(f"⏳ 관찰 중 — {p['name']} ({int(elapsed)}/{OBSERVE_MINUTES}분, {w['count']}회 확인)")

    _save(WATCHING_FILE, watching)
    return to_buy, msgs


def _auto_buy_paper(proposals: list[dict], slot_kind: str = "buy", regime: str = "unknown") -> list[str]:
    """모의 모드: 상위 미보유 종목을 승인 없이 자동 매수. (실거래에서는 호출 안 함)
    매수 시 진입 근거·세부점수·분할익절 단계·시간대·시장상태를 포지션 메타에 저장(거래일지 마감용)."""
    from kis_trader import KISTrader

    trader = KISTrader()
    if not trader.paper:  # 안전장치: 실거래면 자동매수 금지
        return []
    held = load_positions()
    done, bought = [], 0
    for p in proposals:
        if bought >= PAPER_AUTO_MAX:   # 실제 체결분만 상한 계산(스킵 메시지는 미포함)
            break
        if p["symbol"] in held:
            continue
        entry = p.get("entry") or 0
        if entry <= 0:                 # F2: 유효 진입가 없으면 스킵 — 수량 폭주(예산//1) 방지
            done.append(f"⏭️ {p['name']} 매수 건너뜀: 유효 진입가 없음")
            continue
        # 확신 기반 사이징(백테스트 검증): 점수 높을수록 크게. 평균점수(~65) 기준 1.0배로 재중심해
        # 평균 투입자본은 그대로 두고 고점수에만 더 배분 → 공정비교서 24mo +264%→+298%,
        # 30mo +348%→+385%, MDD 동등/개선. (위험기반·균등보다 우월)
        conv = min(3.0, max(0.5, 1.0 + (p.get("score", 65) - 65) / 40.0))
        budget = int(SOMI_BUDGET * conv)
        if entry > budget:             # F3: 1주가 배정예산 초과(고가주) — 포트폴리오 배분 왜곡 방지
            done.append(f"⏭️ {p['name']} 매수 건너뜀: 1주 {int(entry):,}원 > 배정예산 {budget:,}원")
            continue
        qty = max(1, budget // entry)
        try:
            res = trader.order(p["symbol"], qty, "buy", 0)
        except Exception as exc:
            done.append(f"⏭️ {p['name']} 매수 건너뜀: {exc}")
            continue
        fill = res.get("price") or entry  # 실제 페이퍼 체결가 — 성과·손익 계산 기준(제안가 아님)
        # 분할익절 단계(헌장): 1차 +5%, 2차 max(저항/목표, +8%). 트레일링은 고점 대비 -3%.
        tp1 = round(fill * 1.05)
        tp2 = max(int(p.get("target") or 0), round(fill * 1.08))
        extra = {
            "entry_score": p.get("entry_score"), "risk_level": p.get("risk_level"),
            "risk_state": p.get("risk_state"), "dq_state": p.get("dq_state"),
            "score_mode": p.get("score_mode"), "buy_reason": ", ".join(p.get("reasons", [])[:3]),
            "tp1": tp1, "tp2": tp2, "trail_pct": 3.0, "partial_taken": False,
            "slot": slot_kind, "regime": regime, "news": bool(p.get("news_bonus")),
            "rr": p.get("rr"), "vwap_at_buy": p.get("vwap"),
        }
        record_position(p["symbol"], p["name"], fill, p["stop"], p["target"], qty, p.get("score"), extra=extra)
        bought += 1
        done.append(
            f"🧪 자동 매수(모의) — {p['name']}({p['symbol']}) {qty}주 @ {int(fill):,}원 "
            f"(확신 {conv:.1f}배·탐지 {p.get('score', '?')}·진입 {p.get('entry_score', '?')})\n"
            f"   손절 {int(p['stop']):,} / 1차 {tp1:,} / 2차 {tp2:,} 감시 시작"
        )
    return done


CANDIDATES_FILE = PROJECT_ROOT / "output" / "cache" / "somi_candidates.json"
# 슬롯 종류: 시간대별 운영 규율(헌장). collect/observe=실매수 금지·후보 저장, buy=매수검토, buy_close=마감권 제한, manage=청산 중심
SLOT_LABEL = {
    "collect": "후보 편입(09시)", "observe": "후보 관찰(오전)",
    "buy": "매수 검토(오후)", "buy_close": "마감권 제한검토", "manage": "마감 관리",
}


def _save_candidates(cands: list[dict], slot_kind: str) -> str:
    """오전 후보군을 당일 후보 저장소에 적재(실매수 금지). 12:30 이후 재평가 대상."""
    today = datetime.now().strftime("%Y-%m-%d")
    store = _load(CANDIDATES_FILE)
    if store.get("date") != today:
        store = {"date": today, "items": {}}
    lines = []
    for c in cands:
        store["items"][c["symbol"]] = {
            "name": c["name"], "score": c["score"], "score_mode": c.get("score_mode"),
            "change": c.get("change"), "ts": datetime.now().strftime("%H:%M"), "slot": slot_kind,
        }
        tag = " ⚠️수급미확정" if c.get("score_mode") == "morning_missing_investor_adjusted" else ""
        lines.append(f"• {c['name']}({c['symbol']}) 탐지 {c['score']}{tag}")
    _save(CANDIDATES_FILE, store)
    return "\n".join(lines)


def _market_regime_now():
    """KOSPI·KOSDAQ 국면을 함께 추정. 반환: (효과국면, {지수:국면}, regime_label).
    소미 후보는 대부분 코스닥이라 둘 중 하나라도 'bear'면 위험회피(bear)로 본다."""
    try:
        from market_regime import stable_regime, regime_label, KOSPI_PROXY, KOSDAQ_PROXY
        kospi = stable_regime(KOSPI_PROXY).get("regime", "unknown")
        kosdaq = stable_regime(KOSDAQ_PROXY).get("regime", "unknown")
        if "bear" in (kospi, kosdaq):
            eff = "bear"
        elif kospi == "bull" and kosdaq == "bull":
            eff = "bull"
        else:
            eff = "sideways"
        return eff, {"KOSPI": kospi, "KOSDAQ": kosdaq}, regime_label
    except Exception:
        return "unknown", {}, (lambda r: r)


def _apply_buy_gates(buys: list[dict]) -> list[dict]:
    """통계 게이트(수급확인·몬테카를로) — 탐지 단계 품질 필터.
    하락 국면이라도 매수를 막지 않는다(하락장 기회 종목 발굴). 국면은 후보 검색폭에만 반영."""
    if os.getenv("SOMI_SOOMGEUP_GATE", "true").lower() in {"1", "true", "yes"}:
        buys = [p for p in buys if p.get("soomgeup_net", 0) > 0]
    try:
        from mc_simulator import simulate as _mc
        min_edge = float(os.getenv("SOMI_MC_MIN_EDGE", "0.05"))
        gated = []
        for p in buys:
            mc = _mc(p["symbol"], p["entry"], p["stop"], p["target"])
            p["mc"] = mc
            if mc is not None and mc["edge"] >= min_edge and mc["exp_ret_pct"] > 0:
                gated.append(p)
        return gated
    except Exception:
        return buys


def run(candidate_limit: int = 20, do_send: bool = False, slot_kind: str = "buy") -> str:
    now_str = datetime.now().strftime("%Y-%m-%d %H:%M")
    label = SLOT_LABEL.get(slot_kind, "매수 검토")
    regime, regime_detail, regime_label = _market_regime_now()
    # 하락 국면이면 후보 풀을 넓혀 더 많은 종목을 검색(기회 종목 발굴). 매수는 막지 않음.
    if regime == "bear":
        candidate_limit = int(os.getenv("SOMI_CANDIDATES_BEAR", str(candidate_limit * 2)))
    # 시간대별 탐지 문턱: 오전 후보편입 45, 오후 매수검토는 매수게이트 점수와 정렬(모의=48/실거래=60).
    # 게이트보다 floor가 높으면 완화한 게이트가 무의미해지므로 일치시킨다.
    detect_floor = 45 if slot_kind in ("collect", "observe") else int(os.getenv("SOMI_GOOD_SCORE", str(_gate_thresholds()["score"])))
    proposals = make_proposals(candidate_limit, min_score=detect_floor)
    proposals = apply_news_judgment(proposals)
    _save(PROPOSALS_FILE, {"ts": now_str, "items": proposals})
    detail_txt = ""
    if regime_detail:
        detail_txt = (f" (KOSPI {regime_label(regime_detail.get('KOSPI', '?'))}"
                      f" · KOSDAQ {regime_label(regime_detail.get('KOSDAQ', '?'))})")
    header = f"[소미 {label} / {now_str}]\n시장 국면(HMM): {regime_label(regime)}{detail_txt}"
    held_syms = set(load_positions().keys())
    candidates = [p for p in proposals if p["symbol"] not in held_syms]

    # ── 오전: 실매수 금지, 후보 저장만 ──
    if slot_kind in ("collect", "observe"):
        saved = _save_candidates(candidates, slot_kind)
        miss = sum(1 for c in candidates if c.get("score_mode") == "morning_missing_investor_adjusted")
        note = f"\n(당일 수급 미확정 {miss}종목 — 실매수 보류, 오후 재평가)" if miss else ""
        report = f"{header} 🕘 신규매수 금지·후보 편입\n\n[후보 {len(candidates)}종목]\n" + (saved or "조건 충족 후보 없음") + note
        if do_send:
            send(report)
        growth.record("somi_advisor", role=f"{label}", data=f"후보 {len(candidates)}",
                      judgment="실매수 금지·후보 저장", result="후보 편입",
                      good="오전 수급공백 회피", bad="", scores={"fit": 22, "evidence": 18, "efficiency": 18, "risk": 20, "brevity": 8})
        return report

    # ── 15:20 이후: 신규매수 최소화, 청산/관리 중심(포지션 모니터 담당) ──
    if slot_kind == "manage":
        report = f"{header}\n15:20 이후 — 신규매수 최소화. 보유 청산/익절/손절은 포지션 모니터가 관리."
        if do_send:
            send(report)
        return report

    # ── 오후 매수 슬롯(buy / buy_close) ──
    # 1차: 뉴스판정 buy + 통계 게이트(수급·MC). 하락 국면은 차단이 아니라 후보 확대로 반영.
    buys = _apply_buy_gates([p for p in candidates if p.get("verdict") == "buy"])
    # 2차: 상위 후보를 실시간 다중점수로 보강 → 기대값 기반 최종 게이트
    kis = KISClient()
    top = sorted(buys, key=lambda x: x["score"], reverse=True)[: max(4, PAPER_AUTO_MAX * 2)]
    # 표시용: 하락장엔 'buy' 판정이 적어 매수 메시지가 대형주 한 종목만 반복돼 보인다.
    # 발굴 다양성이 메시지에도 드러나도록 점수 상위 후보를 함께 노출(체결은 buys 게이트 경로만).
    shown = sorted(candidates, key=lambda x: x["score"], reverse=True)[:5]
    for p in {id(x): x for x in (top + shown)}.values():   # 공유 dict는 id로 중복 보강 방지
        rt = analyze_candidate(kis, p["symbol"], p["name"], realtime=True)
        if rt:
            p.update({k: rt[k] for k in (
                "entry_score", "risk_level", "risk_state", "rr_score", "rr_ok",
                "dq_score", "dq_state", "score_mode", "vwap", "buy_pressure", "rr", "reasons", "risks")})
    if slot_kind == "buy_close":  # 마감권: 종가 고가권 유지(매수세·VWAP 위) 후보만 제한 검토
        top = [p for p in top if p.get("buy_pressure", 0) >= 1.0 and p.get("entry_score", 0) >= 75]
    passed = [p for p in top if _passes_buy_gate(p)[0]]
    decisions = "\n\n".join(_format_decision(p, next_check=("보유 관리" if _passes_buy_gate(p)[0] else "다음 슬롯")) for p in shown)

    notable = True   # 스팸 감소: paper 슬롯은 '실제 이벤트' 있을 때만 전송(빈 슬롯 자제)
    if not proposals:
        report = f"{header}\n오늘은 소미 기준({detect_floor}점↑) 탐지 후보가 없습니다. 계속 감시 중."
        notable = not _is_paper()
    elif _is_paper():
        to_buy, watch_msgs = _observation_gate(passed)  # 관찰시간 통과분만 실매수
        executed = _auto_buy_paper(to_buy, slot_kind=slot_kind, regime=regime)
        parts = []
        if decisions:
            parts.append("[종목별 판단]\n" + decisions)
        if watch_msgs:
            parts.append("[관찰 현황]\n" + "\n".join(watch_msgs))
        parts.append("[매수 체결]\n" + ("\n\n".join(executed) if executed else "이번엔 매수 없음 (게이트 미통과·관찰 중)"))
        report = f"{header} 🧪 모의 자동매매(기대값 게이트)\n\n" + "\n\n".join(parts)
        # 체결 or 관찰 상태변화(시작·완료·해제)가 있을 때만 알림. 단순 '관찰 중'/'매수 없음'은 자제.
        notable = bool(executed) or any(k in m for m in watch_msgs for k in ("관찰 시작", "관찰 완료", "관찰 해제"))
    else:
        report = f"{header}\n기대값 기반 종합 판단(승인형):\n\n{decisions or '현재 매수 게이트 통과 후보 없음 — 계속 감시.'}"
    if do_send and notable:
        send(report)
    growth.record(
        "somi_advisor", role=f"{label}(기대값 게이트)",
        data=f"후보 {len(proposals)} / 국면 {regime}", judgment=f"게이트통과 {len(passed)}",
        result=("모의 자동매수" if _is_paper() else "승인형 제안"),
        good="탐지·진입·손익비·리스크·데이터품질 동시 통과분만", bad=("후보 0" if not proposals else ""),
        scores={"fit": 23, "evidence": 21, "efficiency": 18, "risk": 20, "brevity": 8},
    )
    return report


def main() -> None:
    import argparse

    parser = argparse.ArgumentParser(description="소미 매수 제안 (승인형, 자동매수 없음)")
    parser.add_argument("--propose", action="store_true", help="후보 분석 후 제안 생성")
    parser.add_argument("--candidates", type=int, default=20)
    parser.add_argument("--send", action="store_true", help="텔레그램 전송")
    parser.add_argument("--daemon", action="store_true", help="데몬 모드 (시간대별 슬롯)")
    parser.add_argument("--slot", default="buy", help="단발 실행 슬롯종류: collect|observe|buy|buy_close|manage")
    args = parser.parse_args()

    # 슬롯 시각 → 시간대 운영 종류(헌장): 오전 후보편입·관찰, 오후 매수, 마감권 제한, 마감 관리
    slot_kinds = {
        "09:00": "collect",   # 09:00~10:00 신규매수 금지·후보 편입
        "11:00": "observe",   # 10:00~11:30 후보 관찰
        "12:30": "buy",       # 12:30~14:30 매수 검토
        "14:00": "buy",
        "15:10": "buy_close", # 15:00~15:20 종가 고가권 제한 검토
        "15:25": "manage",    # 15:20 이후 신규매수 최소화·청산 관리
    }

    if args.daemon:
        from _shared.utils import due_slot
        # 모의(paper)는 오전 포함 30분 간격 고빈도 매수 슬롯(헌장 시각 외는 buy로 폴백) — 실거래는 보수 스케줄 유지.
        # 모의는 15분 간격(관찰 2분이 슬롯 간격에 묻히지 않게) — 실거래는 보수 스케줄 유지.
        default_slots = (
            "09:15,09:30,09:45,10:00,10:15,10:30,10:45,11:00,11:15,11:30,11:45,12:00,"
            "12:15,12:30,12:45,13:00,13:15,13:30,13:45,14:00,14:15,14:30,14:45,15:00"
            if _is_paper() else "09:00,11:00,12:30,14:00,15:10,15:25")
        slots = os.getenv("SOMI_ADVISOR_SLOTS", default_slots).split(",")
        state = PROJECT_ROOT / "output" / "cache" / "somi_advisor_slots.json"
        with ProcessLock("somi_trade_advisor"):
            print(f"[{datetime.now()}] 소미 매수 제안 데몬 시작 (시간대 슬롯: {','.join(slots)})")
            while True:
                slot = due_slot(slots, state)   # 정해진 시각에만, 재시작·매틱 보고 방지
                if slot:
                    kind = slot_kinds.get(slot, "buy")
                    try:
                        print(f"[{datetime.now()}] 슬롯 {slot} 실행({kind})")
                        run(args.candidates, do_send=True, slot_kind=kind)
                    except Exception as e:
                        send(f"⚠️ 소미 매수 제안 오류: {e}")
                time.sleep(300)  # 5분마다 슬롯 도래만 확인(보고는 슬롯당 1회)
        return

    print(run(args.candidates, args.send, slot_kind=args.slot))


if __name__ == "__main__":
    main()
