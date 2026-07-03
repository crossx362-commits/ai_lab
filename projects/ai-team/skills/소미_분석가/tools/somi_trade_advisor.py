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
    # 원자적 쓰기(2026-07-03): write_text는 파일을 0으로 자른 뒤 기록 → 데몬이 쓰는 순간
    # 텔레그램 봇 등 동시 읽기가 빈/깨진 파일을 만나 '보유 없음' 오답. tmp+replace로 봉인.
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_name(path.name + ".tmp")
    tmp.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    os.replace(tmp, path)


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
    gross_pct = ((exit_price - entry) / entry * 100) if entry else 0.0
    # 실비용 반영 — 왕복 수수료(0.015%×2) + 매도 거래세(0.18%) 차감(backtest.py와 동일 상수).
    # VTS 체결가엔 비용이 없어 성과가 ~0.21%p 과대평가되던 문제. gross는 별도 보존.
    cost_pct = (0.00015 * 2 + 0.0018) * 100 if entry else 0.0
    ret_pct = gross_pct - cost_pct
    rec = {
        "symbol": symbol, "name": name, "entry": entry, "exit": exit_price, "qty": qty,
        "ret_pct": round(ret_pct, 2), "gross_ret_pct": round(gross_pct, 2), "reason": reason, "score": score,
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
    # rr 일원화: 실제 청산은 포지션 모니터 하드손절(-3%/ATR 보수)이 우선 컷하므로, 지지선 기반
    # 넓은 손절(-10~30%)로 계산한 rr이 뉴스판정·MC 게이트를 일괄 왜곡하던 문제 — -3%를 바닥으로 캡.
    stop = max(stop, round(entry * 0.97))
    target = resistance if (resistance and resistance > entry) else round(entry * (1 + TARGET_PCT))
    return entry, stop, target


def _ma_cross(kis: KISClient, code: str, lookback: int = 3) -> tuple[bool, int]:
    """골든크로스(MA5가 MA20 상향돌파) 최근 lookback거래일 내 발생 여부 → (발생, 경과일).
    일봉 26개로 각 시점 MA5/MA20을 계산해 교차일 탐지. 데이터 부족/조회 실패 시 (False, -1)."""
    try:
        d = kis.daily_prices(code, 26)
        closes = [to_num(r.get("stck_clpr")) for r in d if r.get("stck_clpr")]
    except Exception:
        return False, -1
    if len(closes) < 21 + lookback:
        return False, -1

    def ma(n: int, i: int) -> float:  # closes[0]=최신, i일 전 시점의 n일 이동평균
        return sum(closes[i:i + n]) / n

    for i in range(lookback):
        if ma(5, i) > ma(20, i) and ma(5, i + 1) <= ma(20, i + 1):
            return True, i
    return False, -1


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
    # 골든크로스(5/20일선) 가점 +6점(추세 전환 초입 포착) — 모의·실거래 공통(실거래 적용 사용자 승인 2026-07-02).
    gc, gc_days = _ma_cross(kis, code)
    if gc:
        score = min(100, score + 6)
        pos = [f"골든크로스 발생({'당일' if gc_days == 0 else f'{gc_days}일 전'}, 5/20일선)"] + pos
    entry, stop, target = _levels(parsed)
    rr = (target - entry) / (entry - stop) if entry > stop else 0  # 손익비(저항 미반영 기본값)
    soomgeup_net = to_num(parsed.get("buy_foreigner_5d")) + to_num(parsed.get("buy_institution_5d"))
    dq = parsed.get("data_quality") or {}
    out = {
        "symbol": code, "name": name, "score": score, "grade": grade,
        "change": parsed.get("change_pct", ""),
        "entry": entry, "stop": stop, "target": target, "rr": round(rr, 2),
        "soomgeup_net": soomgeup_net,  # 기관+외국인 5일 누적 순매수(수급확인 게이트용)
        "market_warning": parsed.get("market_warning", "") or "",  # 시장경보(투자경고/위험/정리매매 등) — 발굴 제외용
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


TUNING_FILE = PROJECT_ROOT / "output" / "cache" / "somi_tuning.json"
# 성장엔진 자동 튜닝 허용범위 — 모의 한정. 엔진 버그로 극단값이 와도 여기서 클램프(실거래 무관).
# gate_score 하한 58(2026-07-02 중소형 전이검증): 소미 실제 사냥터(코스닥·중소형 30종목)에선
# 55는 수급확인을 더해도 손실(-60%), 60+수급확인부터 흑자(+45%·PF 1.39) — 대형주 결론(55↑ 흑자)이
# 전이되지 않음. 최종 눈금은 한별 점수버킷(실데이터)이 보정. gate_entry는 별개 척도라 52 유지.
_TUNING_BOUNDS = {"gate_score": (58, 70), "gate_entry": (52, 75),
                  "observe_minutes": (1, 20), "paper_auto_max": (2, 10)}


def _tuning(key: str, default: int) -> int:
    """성장엔진(예원)이 모의 한정 자동 튜닝하는 파라미터 — 파일 우선, 없으면 default. 호출 시점 로드(재시작 불요)."""
    try:
        v = int(json.loads(TUNING_FILE.read_text(encoding="utf-8")).get("params", {}).get(key, default))
    except Exception:
        return default
    lo, hi = _TUNING_BOUNDS.get(key, (v, v))
    return max(lo, min(hi, v))


def _gate_thresholds() -> dict:
    """매수 게이트 문턱 — 모의(paper)는 공격적 완화 + 성장엔진 자동 튜닝, 실거래(live)는 보수값 고정.
    위험관리 축(dq_state·danger)은 모드 무관 차단. 수급미확정은 실거래만 차단(모의는 5일누적 보정 허용)."""
    if _is_paper():
        return {
            "score": _tuning("gate_score", int(os.getenv("SOMI_GATE_SCORE_PAPER", "60"))),   # 탐지점수 (중소형 전이검증: 60+수급확인부터 흑자)
            "entry": _tuning("gate_entry", int(os.getenv("SOMI_GATE_ENTRY_PAPER", "55"))),   # 진입점수 (별개 척도 — 실데이터로 보정 예정)
            "require_rr": os.getenv("SOMI_GATE_RR_PAPER", "false").lower() in {"1", "true", "yes"},
        }
    return {"score": 60, "entry": 70, "require_rr": True}


def _passes_buy_gate(c: dict, regime: str = "unknown") -> tuple[bool, str]:
    """기대값 기반 최종 매수 게이트. 모두 통과해야 실매수 허용.
    모의는 점수·진입 문턱↓·손익비 요구 해제·수급미확정 허용(공격적), 실거래는 보수값. 위험축은 공통 차단."""
    th = _gate_thresholds()
    # 하락 국면 선별(백테스트 12개월·40종목: 하락장 무차별 매수 누적 +23.6% vs 선별통과 +62.3%):
    # 매수를 막진 않되 문턱만 올려 역행 강세만 통과 — 모의 공격 원칙과 수익률의 절충. 모의 한정.
    score_th = th["score"] + (int(os.getenv("SOMI_BEAR_GATE_BUMP", "10"))
                              if _is_paper() and regime == "bear" else 0)
    if c.get("score", 0) < score_th:
        return False, f"탐지점수 {c.get('score')} < {score_th}"
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


def _decide(c: dict, regime: str = "unknown") -> tuple[str, str]:
    ok, why = _passes_buy_gate(c, regime)
    if ok:
        return "매수", ""
    if c.get("score", 0) >= 45:
        return "관찰", why
    return "제외", why


def _format_decision(c: dict, next_check: str = "다음 슬롯", regime: str = "unknown") -> str:
    """헌장 의사결정 출력형식 — 종목별 결론/세부점수/가격/이유."""
    decision, why = _decide(c, regime)
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
        # 시장경보(투자경고·투자위험·정리매매·관리종목·단기과열)는 매매 부적격 — 발굴/제안에서 제외.
        if any(w in a.get("market_warning", "") for w in ("경고", "위험", "정리매매", "관리종목", "과열")):
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

# 모의(paper) 전용 판단 규칙 — 수익 기회 포착 우선(하네스: 완화는 _is_paper 분기로만, 실거래 프롬프트는 그대로).
# 급등주는 손절폭(ATR/저가)이 넓어 손익비가 구조적으로 낮게 계산됨 → 1.5 하드룰이 후보 전원을 watch로 강등하던 문제.
# 하방 방어는 하드손절(-3%/ATR)·트레일링·MC 게이트가 별도로 맡으므로 뉴스판정은 악재 회피만 담당한다.
NEWS_JUDGE_SYSTEM_PAPER = NEWS_JUDGE_SYSTEM.replace(
    "- 손익비(목표-진입)/(진입-손절)가 1.5 미만이면 'watch' 이하.",
    "- 손익비는 참고만 한다 — 급등주는 손절폭이 넓어 손익비가 낮게 나오므로 손익비만으로 강등하지 않는다.",
).replace(
    "- 너는 제안만 한다. 실제 체결은 사용자 승인으로만. 과감한 매수 유도·근거 없는 낙관 금지.",
    "- 지금은 모의(검증) 운용: 누적 악재·시장 급변이 없고 수급이 유망하면 'buy'를 준다. "
    "'뉴스 없음'·'확신 부족'만으로 buy를 watch로 낮추지 마라(뉴스 없음=중립, 수급 우선).",
)

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
    raw = llm_text(user, system=NEWS_JUDGE_SYSTEM_PAPER if _is_paper() else NEWS_JUDGE_SYSTEM,
                   json_mode=True, max_tokens=300, temperature=0.3, lm_first=False)
    j = _parse_json(raw)
    if not j or "verdict" not in j:
        # LLM 미응답 → 수급만으로 판단. 모의는 유망 수급이면 buy(판정 실패가 매수 전면차단이 되지 않게), 실거래는 watch.
        fb = "buy" if _is_paper() and p.get("score", 0) >= GOOD_SCORE and p.get("soomgeup_net", 0) > 0 else "watch"
        return {**p, "verdict": fb, "news_trend": "none" if n == 0 else "stable",
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
    # 모의: LLM 판정은 매수권 상위 후보만 — 클라우드 전멸 시 로컬 LLM 판정(건당 10~20초)이
    # 후보 40개 × 사이클을 20분+로 늘려 매수 판단 자체가 지연되던 문제(2026-07-03).
    # 자동매수 상한(8)의 2배만 판정하면 체결 후보는 전부 커버. 나머지는 watch(악재 후보는
    # 공시 게이트·리스크 게이트가 별도 차단). 실거래는 전 후보 판정 유지.
    if _is_paper():
        n = max(8, _paper_auto_max() * 2)
        top_ids = {id(x) for x in sorted(proposals, key=lambda x: x.get("score", 0), reverse=True)[:n]}
        return [judge_with_news(p, hist, brief) if id(p) in top_ids
                else {**p, "verdict": "watch", "news_trend": "none", "news_reflected": False,
                      "news_reason": "모의 속도 우선 — 상위 후보만 LLM 판정"}
                for p in proposals]
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
def _paper_auto_max() -> int:
    """모의 1회 실행 자동매수 상한 — 성장엔진 튜닝 대상(호출 시점 로드)."""
    return _tuning("paper_auto_max", int(os.getenv("SOMI_PAPER_AUTO_MAX", "8")))


def _observe_minutes() -> int:
    """매수 전 관찰 시간(분) — 모의는 성장엔진 튜닝 대상, 실거래는 30분 고정."""
    if _is_paper():
        return _tuning("observe_minutes", int(os.getenv("SOMI_OBSERVE_MINUTES", "2")))
    return int(os.getenv("SOMI_OBSERVE_MINUTES", "30"))


PAPER_AUTO_MAX = int(os.getenv("SOMI_PAPER_AUTO_MAX", "8"))
SOMI_BUDGET = int(os.getenv("SOMI_BUDGET_PER_TRADE", "1000000"))
# 매수 전 관찰 시간(분): 'buy' 신호가 이 시간 이상 유지돼야 실제 매수. 모의=2분(고공격)/실거래=30분.
OBSERVE_MINUTES = int(os.getenv("SOMI_OBSERVE_MINUTES", "2" if _is_paper() else "30"))
WATCHING_FILE = PROJECT_ROOT / "output" / "cache" / "somi_watching.json"


def _observation_gate(buys: list[dict]) -> tuple[list[dict], list[str]]:
    """'buy' 신호를 바로 매수하지 않고 _observe_minutes()만큼 관찰.
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
            msgs.append(f"👀 관찰 시작 — {p['name']}({sym}) · 최소 {_observe_minutes()}분 지켜본 뒤 매수")
            continue
        w["count"] = w.get("count", 1) + 1
        try:
            elapsed = (now - datetime.fromisoformat(w["first_ts"])).total_seconds() / 60
        except Exception:
            elapsed = _observe_minutes()
        if elapsed >= _observe_minutes():  # 관찰 통과 → 매수
            to_buy.append(p)
            del watching[sym]
            msgs.append(f"✅ 관찰 완료 {int(elapsed)}분({w['count']}회 유지) — {p['name']} 매수 진행")
        else:
            msgs.append(f"⏳ 관찰 중 — {p['name']} ({int(elapsed)}/{_observe_minutes()}분, {w['count']}회 확인)")

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
    # 자금배분(사용자 지시 2026-07-02): 예수금 200만원(고정)만 남기고 전액 투자.
    # 배분량 = (현금 - 고정유보금)을 후보 확신 가중으로 분배. 유보 도달 시 신규매수 보류.
    reserve = float(os.getenv("SOMI_CASH_RESERVE_KRW", "2000000"))
    _conv = lambda p: min(3.0, max(0.5, 1.0 + (p.get("score", 65) - 65) / 40.0))  # noqa: E731
    try:
        bal = trader.balance()
        cash = float(bal.get("cash", 0))
        eligible = [p for p in proposals if p["symbol"] not in held][: _paper_auto_max()]
        total_conv = sum(_conv(p) for p in eligible) or 1.0
        deployable = max(0.0, cash - reserve)
    except Exception as exc:               # 잔고 조회 실패 → 기존 고정예산으로 폴백(매수 중단 방지)
        print(f"[소미제안] 잔고 조회 실패 — 고정예산 폴백: {exc}")
        cash, reserve, deployable, total_conv = float("inf"), 0.0, 0.0, 0.0
    if deployable <= 0 and cash != float("inf"):
        return [f"💤 신규매수 보류 — 예수금 고정유보 {int(reserve):,}원 도달(현금 {int(cash):,})"]
    for p in proposals:
        if bought >= _paper_auto_max():   # 실제 체결분만 상한 계산(스킵 메시지는 미포함)
            break
        if p["symbol"] in held:
            continue
        entry = p.get("entry") or 0
        if entry <= 0:                 # F2: 유효 진입가 없으면 스킵 — 수량 폭주(예산//1) 방지
            done.append(f"⏭️ {p['name']} 매수 건너뜀: 유효 진입가 없음")
            continue
        # 확신 기반 사이징(백테스트 검증): 점수 높을수록 크게. 평균점수(~65) 기준 1.0배 재중심.
        conv = _conv(p)
        if deployable > 0:             # 투자여력 비례 배분 + 남은 현금-유보 한도 캡
            budget = int(min(deployable * conv / total_conv, cash - reserve))
        else:
            budget = int(SOMI_BUDGET * conv)   # 잔고 조회 실패 시 기존 고정예산
        if budget <= 0:
            done.append(f"💤 {p['name']} 매수 보류 — 예수금 고정유보 {int(reserve):,}원 도달")
            break
        if entry > budget:             # F3: 1주가 배정예산 초과(고가주) — 포트폴리오 배분 왜곡 방지
            done.append(f"⏭️ {p['name']} 매수 건너뜀: 1주 {int(entry):,}원 > 배정예산 {budget:,}원")
            continue
        # 돌파 분할매수(연구 2026-07-03, docs/SPLIT_ENTRY_EXIT_STUDY): 예산 50% 진입 →
        # 진입가 +2% 돌파 시 잔여 50% 증액(3거래일 유효, 미돌파 시 미투입).
        # 24개월 검증: 손익비 1.40→2.43, MDD -32→-19%, 샤프 2.24→5.26. 1주가 절반예산 초과면 일괄.
        half_budget = budget // 2
        split = half_budget >= entry
        qty = max(1, (half_budget if split else budget) // entry)
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
        if split:
            extra.update({"addon_trigger": round(fill * 1.02), "addon_budget": int(budget - qty * fill),
                          "addon_until": _n_trading_days_later(3), "addon_done": False})
        record_position(p["symbol"], p["name"], fill, p["stop"], p["target"], qty, p.get("score"), extra=extra)
        bought += 1
        cash -= qty * fill   # 후속 후보 배분 캡에 반영(유보율 침범 방지)
        split_note = f" · 분할 1차 50%(+2% 돌파 시 증액)" if split else ""
        done.append(
            f"🧪 자동 매수(모의) — {p['name']}({p['symbol']}) {qty}주 @ {int(fill):,}원 "
            f"(확신 {conv:.1f}배·탐지 {p.get('score', '?')}·진입 {p.get('entry_score', '?')}){split_note}\n"
            f"   손절 {int(p['stop']):,} / 1차 {tp1:,} / 2차 {tp2:,} 감시 시작"
        )
    return done


def _deployable_cash() -> float:
    """투자 가능 현금(예수금 - 고정유보). 모의는 원장 읽기라 무비용.
    발굴 게이트용 — 자금 없으면(전액 투자·유보 도달) 무거운 발굴을 건너뛴다."""
    try:
        from kis_trader import KISTrader
        trader = KISTrader()
        cash = float(trader.balance().get("cash", 0))
        reserve = float(os.getenv("SOMI_CASH_RESERVE_KRW", "2000000"))
        return max(0.0, cash - reserve)
    except Exception:
        return 1.0   # 조회 실패 시 발굴 허용(기존 흐름 유지 — 무체결 방지)


def _n_trading_days_later(n: int) -> str:
    """주말 제외 n거래일 뒤 날짜(YYYY-MM-DD) — 공휴일 미반영(근사)."""
    d = datetime.now()
    added = 0
    while added < n:
        d += timedelta(days=1)
        if d.weekday() < 5:
            added += 1
    return d.strftime("%Y-%m-%d")


def _addon_scale_in() -> None:
    """돌파 분할매수 2차 증액(모의 전용) — 보유 포지션의 +2% 돌파 트리거를 고속감시 주기로 확인.
    연구(2026-07-03): '강함 확인 후 증액'이 손익비 1.40→2.43·샤프 2.24→5.26 (docs/SPLIT_ENTRY_EXIT_STUDY).
    기한(3거래일) 내 미돌파면 미투입 종료 — 눌림 추가매수는 역선택이라 금지."""
    if not _is_paper():
        return
    pos = load_positions()
    pending = {s: p for s, p in pos.items() if p.get("addon_trigger") and not p.get("addon_done")}
    if not pending:
        return
    from kis_trader import KISTrader
    trader = KISTrader()
    if not trader.paper:
        return
    kis = KISClient()
    today = datetime.now().strftime("%Y-%m-%d")
    for sym, p in pending.items():
        if today > str(p.get("addon_until", "")):
            set_position_fields(sym, {"addon_done": True})   # 기한 만료 — 잔여 미투입 종료
            continue
        try:
            cur = to_num((kis.quote(sym) or {}).get("stck_prpr"))
        except Exception:
            continue
        if not cur or cur < p["addon_trigger"]:
            continue
        qty2 = int(p.get("addon_budget", 0) // cur)
        if qty2 < 1:
            set_position_fields(sym, {"addon_done": True})   # 잔여 예산으로 1주 불가
            continue
        try:
            res = trader.order(sym, qty2, "buy", 0)
        except Exception as exc:
            print(f"[분할증액] {p.get('name', sym)} 주문 실패: {exc}")
            continue
        fill2 = res.get("price") or cur
        q1, e1 = int(p.get("qty", 0)), float(p.get("entry") or fill2)
        new_qty = q1 + qty2
        new_avg = round((q1 * e1 + qty2 * fill2) / new_qty, 2)
        # 평단 갱신에 맞춰 분할익절 단계 재산정(손절·트레일은 원신호 유지 — 백테스트 조건 동일)
        set_position_fields(sym, {
            "qty": new_qty, "entry": new_avg, "addon_done": True,
            "addon_fill": fill2, "addon_qty": qty2,
            "tp1": round(new_avg * 1.05), "tp2": max(int(p.get("target") or 0), round(new_avg * 1.08)),
        })
        send(f"🧪 [소미 분할증액] {p.get('name', sym)}({sym}) +2% 돌파 확인 — {qty2}주 @ {int(fill2):,}원 추가\n"
             f"   평단 {int(new_avg):,}원 · 총 {new_qty}주 · 1차 {round(new_avg * 1.05):,} 재설정")


CANDIDATES_FILE = PROJECT_ROOT / "output" / "cache" / "somi_candidates.json"
TRIGGER_FILE = PROJECT_ROOT / "output" / "cache" / "somi_trigger.json"
_last_trigger_run: datetime | None = None


def _consume_trigger(max_age_sec: int = 600, cooldown_sec: int = 600) -> bool:
    """가격모니터의 '강한 신호' 이벤트 소비(1회성) — 신선하고 쿨다운 지났고 장중이면 즉시 매수검토.
    슬롯 스케줄과 별개인 이벤트 경로: 급변동 감지→매수검토 지연을 최대 15분→1분으로 단축.
    신선도 10분: 슬롯 실행(5~8분)과 겹쳐 대기해도 실행 직후 소비되게 — 급변동 후 데이터로 재검토는 유효."""
    global _last_trigger_run
    if not TRIGGER_FILE.exists():
        return False
    try:
        t = json.loads(TRIGGER_FILE.read_text(encoding="utf-8"))
        ts = datetime.fromisoformat(str(t.get("ts", "")))
    except Exception:
        TRIGGER_FILE.unlink(missing_ok=True)
        return False
    TRIGGER_FILE.unlink(missing_ok=True)  # 읽는 즉시 소비 — 중복 실행 방지
    now = datetime.now()
    if (now - ts).total_seconds() > max_age_sec:
        return False
    if _last_trigger_run and (now - _last_trigger_run).total_seconds() < cooldown_sec:
        return False
    if now.weekday() >= 5 or not ("09:05" <= now.strftime("%H:%M") <= "15:00"):
        return False
    _last_trigger_run = now
    print(f"[{now}] 급변동 트리거 소비 — {t.get('name')}({t.get('symbol')}) {t.get('change')}")
    return True
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
            # 모의는 MC를 차단이 아니라 '기록'으로만 사용(오너 지시 2026-07-03) — 폭락장 90일 분포에선
            # 전 종목 기대수익 음수라 전면 무체결이 됨. 매수 기록에 mc가 남아 한별 튜닝의 학습 데이터가 된다.
            # 실거래는 기존 fail-closed 유지(시뮬 실패·기대수익 음수 차단).
            if _is_paper():
                gated.append(p)
                continue
            if mc is not None and mc["edge"] >= min_edge and mc["exp_ret_pct"] > 0:
                gated.append(p)
        return gated
    except Exception:
        return buys


STOCK_NEWS_CACHE = PROJECT_ROOT / "output" / "cache" / "somi_stock_news.json"


def _enrich_mover_news(top_n: int = 6) -> None:
    """급등 후보 중 issue_impact에 뉴스 없는 종목을 종목별 웹검색으로 개별 보강(일 1회 캐시).
    소형 급등주는 마켓데스크 매크로 뉴스에 안 잡혀 news_bonus=0 되던 문제 해소.
    비용 제한: 상위 후보 top_n개, 종목당 하루 1회만 조회(캐시)."""
    try:
        from somi_screener import get_candidates
        impact = research.load_issue_impact() or {}
        today = datetime.now().strftime("%Y-%m-%d")
        cache = _load(STOCK_NEWS_CACHE)
        if cache.get("date") != today:
            cache = {"date": today, "items": {}}
        changed = False
        for code, name in get_candidates(KISClient(), top_n):
            if code in impact or code in cache["items"]:
                continue  # 이미 매크로 뉴스 있거나 오늘 조회함 → 스킵
            brief = research.web_brief(
                f"'{name}'({code}) 오늘 주가 관련 구체적 재료(뉴스·공시·수주 등)가 있으면 "
                f"첫 줄에 호재 강도 숫자만(1=약호재 2=강호재 -1=악재), 없으면 0. "
                f"둘째 줄에 한 줄 근거(없으면 빈 줄).", max_tokens=180)
            score, reason = 0, ""
            if brief:
                lines = [l.strip() for l in brief.strip().splitlines() if l.strip()]
                try:
                    score = max(-2, min(2, int(lines[0].split()[0].replace("+", ""))))
                except (ValueError, IndexError):
                    score = 0
                reason = (lines[1] if len(lines) > 1 else lines[0] if lines else "")[:60]
            cache["items"][code] = {"score": score, "reason": reason, "name": name}
            if score:
                impact[code] = {"score": score, "reason": reason, "name": name, "source": "stock_news"}
                changed = True
        _save(STOCK_NEWS_CACHE, cache)
        if changed:
            research.save_issue_impact(impact)
            print(f"[소미제안] 종목별 뉴스 보강 — {sum(1 for v in cache['items'].values() if v['score'])}종목 재료 확인")
    except Exception as exc:
        print(f"[소미제안] 종목별 뉴스 보강 실패(스킵): {exc}")


# 공시 키워드 게이트(결정적) — LLM 평가와 별개로 치명 공시는 하드 차단, 명확한 호재는 가점.
_BAD_DISCLOSURE = ("유상증자", "전환사채", "신주인수권", "감자", "관리종목", "거래정지", "상장폐지",
                   "불성실공시", "횡령", "배임", "감사의견", "회생절차", "투자주의환기")
_GOOD_DISCLOSURE = ("단일판매", "공급계약", "수주", "무상증자", "자기주식", "자사주")


def _disclosure_check(codes: list[str]) -> dict[str, dict]:
    """매수 후보의 최근 3일 DART 공시 직접 조회 — 워치리스트 밖 후보까지 커버(매매 직전 반영 원칙).
    반환 {code: {"block": bool, "bonus": int, "why": str}}. 키 미설정/실패 시 빈 dict(기존 흐름 유지)."""
    out: dict[str, dict] = {}
    try:
        for d in research.dart_recent(set(codes), days=3):
            rep, code = d.get("report", "") or "", d.get("code", "") or ""
            if not code:
                continue
            cur = out.setdefault(code, {"block": False, "bonus": 0, "why": ""})
            if any(k in rep for k in _BAD_DISCLOSURE):
                cur.update({"block": True, "why": rep[:50]})
            elif any(k in rep for k in _GOOD_DISCLOSURE) and not cur["block"]:
                cur["bonus"], cur["why"] = 5, rep[:50]
    except Exception as exc:
        print(f"[소미제안] 공시 게이트 조회 실패(스킵): {exc}")
    return {c: v for c, v in out.items() if v["block"] or v["bonus"]}


def _refresh_news_for_trade() -> None:
    """뉴스는 스케줄이 아니라 매매 직전에 반영 — 거래 대상(국내주식) 관련 지역만 재수집.
    아시아/한국 뉴스·공시를 새로 fetch → 마켓데스크 issue_impact 재평가. 미국/유럽은
    KR 장중 미개장이라 아침 스냅샷 유지. 실패해도 매매는 진행(기존 캐시로 폴백)."""
    try:
        sys.path.insert(0, str(AI_TEAM_ROOT / "skills" / "유나_아시아조사" / "tools"))
        sys.path.insert(0, str(AI_TEAM_ROOT / "skills" / "마켓데스크_시장종합" / "tools"))
        import asia_research
        import market_desk
        prev = research.load_issue_impact()                 # 재평가 실패 대비 스냅샷(뉴스 유실 방지)
        asia = asia_research.collect()                      # KR/아시아 뉴스·공시 재수집
        us, eu = research.load_region("us"), research.load_region("eu")
        market_desk._build_issue_impact(asia.get("disclosures", []) or [], us, asia, eu)
        # _build_issue_impact는 LLM JSON 파싱 실패 시 빈 결과로 덮어쓴다 → 직전 데이터가 있었으면 복원.
        if not research.load_issue_impact() and prev:
            research.save_issue_impact(prev)
            print("[소미제안] 뉴스 재평가 결과 비어 있음 — 직전 issue_impact 복원(유실 방지)")
        else:
            print("[소미제안] 거래 전 뉴스 갱신 완료 — 아시아/한국 재수집·issue_impact 재평가")
        _enrich_mover_news()   # 소형 급등주 종목별 뉴스 개별 보강(매크로 뉴스 사각지대)
    except Exception as exc:
        print(f"[소미제안] 거래 전 뉴스 갱신 실패 — 기존 캐시 사용: {exc}")


def _fast_watch(regime: str = "unknown") -> None:
    """발굴 사이 고속 감시(모의 전용): 직전 발굴 후보(PROPOSALS_FILE)만 실시간 재평가해
    게이트·관찰 통과 즉시 매수. 무거운 발굴(스크리너·뉴스·공시 조회)은 하지 않는다 —
    공시 차단은 발굴 시점에 표시된 disc_block을 재사용."""
    now = datetime.now()
    if now.strftime("%H:%M") >= "15:00":
        return  # 마감권(15:00~)은 발굴(buy_close) 제한 규율로만 매수 — 고속감시 중단
    store = _load(PROPOSALS_FILE)
    if str(store.get("ts", ""))[:10] != now.strftime("%Y-%m-%d"):
        return  # 전일 후보로 매수 금지
    held = set(load_positions().keys())
    _ok_verdicts = ("buy", "watch") if _is_paper() else ("buy",)   # 모의는 watch 허용(오너 지시 2026-07-03)
    cands = [p for p in (store.get("items") or [])
             if p.get("verdict") in _ok_verdicts and not p.get("disc_block") and p["symbol"] not in held]
    if not cands and not _load(WATCHING_FILE):
        return
    kis = KISClient()
    top = sorted(_apply_buy_gates(cands), key=lambda x: x["score"], reverse=True)[: _paper_auto_max()]
    for p in top:
        rt = analyze_candidate(kis, p["symbol"], p["name"], realtime=True)
        if rt:
            p.update({k: rt[k] for k in (
                "entry_score", "risk_level", "risk_state", "rr_score", "rr_ok",
                "dq_score", "dq_state", "score_mode", "vwap", "buy_pressure", "rr", "reasons", "risks")})
    passed = [p for p in top if _passes_buy_gate(p, regime)[0]]
    to_buy, msgs = _observation_gate(passed)
    executed = _auto_buy_paper(to_buy, slot_kind="buy_fast", regime=regime)
    # 체결·관찰 상태변화(시작·완료·해제)만 알림 — 매 틱 '관찰 중' 스팸 방지
    if executed or any(k in m for m in msgs for k in ("관찰 시작", "관찰 완료", "관찰 해제")):
        parts = [f"[소미 고속감시 / {now.strftime('%H:%M')}] ⚡ 동적 진입"]
        if msgs:
            parts.append("\n".join(msgs))
        if executed:
            parts.append("[매수 체결]\n" + "\n\n".join(executed))
        send("\n\n".join(parts))


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
    # 뉴스는 스케줄이 아니라 매매 직전에 반영 — 매수 슬롯에서만 관련 지역(아시아/한국) 재수집·재평가.
    if slot_kind in ("buy", "buy_close"):
        _refresh_news_for_trade()
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

    # 공시 게이트(2026-07-02): 후보 종목 최근 3일 공시 — 악재는 하드 제외, 호재는 +5 가점.
    # 마켓데스크 LLM 평가와 별개의 결정적 안전축(유증·CB 등 치명 공시가 LLM 누락돼도 차단).
    disc = _disclosure_check([p["symbol"] for p in candidates])
    if disc:
        dropped = [f"⛔ 공시 악재 제외 — {p['name']}: {disc[p['symbol']]['why']}"
                   for p in candidates if disc.get(p["symbol"], {}).get("block")]
        for p in candidates:
            d = disc.get(p["symbol"])
            if d and d["bonus"] and not d["block"]:
                p["score"] = min(100, p["score"] + d["bonus"])
                p["reasons"] = [f"호재 공시: {d['why']}"] + (p.get("reasons") or [])
        candidates = [p for p in candidates if not disc.get(p["symbol"], {}).get("block")]
        if dropped:
            header += "\n" + "\n".join(dropped[:5])
        # 고속감시(_fast_watch)가 공시 재조회 없이 차단을 재사용하도록 표시 후 재저장
        for p in proposals:
            if disc.get(p["symbol"], {}).get("block"):
                p["disc_block"] = True
        _save(PROPOSALS_FILE, {"ts": now_str, "items": proposals})

    # 발굴 후보 자동 관심등록 — 유망(GOOD_SCORE↑)만. 가격감시·정기보고가 즉시 추적 시작.
    try:
        from watchlist_manager import auto_register
        added = auto_register(candidates, min_score=GOOD_SCORE)
        if added:
            header += f"\n📌 관심종목 자동 등록: {', '.join(added)}"
    except Exception as exc:
        print(f"[watchlist] 자동 등록 실패: {exc}")

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
    # 모의는 'watch'(확신부족)도 통과 — 오너 지시(2026-07-03): 폭락주간 전종목 수급음수→watch로
    # 모의가 전면 무체결이 되던 문제. 'avoid'(누적 악재)는 모의에서도 차단. 실거래는 buy만.
    _ok_verdicts = ("buy", "watch") if _is_paper() else ("buy",)
    buys = _apply_buy_gates([p for p in candidates if p.get("verdict") in _ok_verdicts])
    # 2차: 상위 후보를 실시간 다중점수로 보강 → 기대값 기반 최종 게이트
    kis = KISClient()
    top = sorted(buys, key=lambda x: x["score"], reverse=True)[: max(4, _paper_auto_max() * 2)]
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
    passed = [p for p in top if _passes_buy_gate(p, regime)[0]]
    decisions = "\n\n".join(_format_decision(p, next_check=("보유 관리" if _passes_buy_gate(p, regime)[0] else "다음 슬롯"), regime=regime) for p in shown)

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

    # 이중실행 가드(2026-07-02): launchd 정시 one-shot(--propose)이 상시 매수 데몬과 겹치면 스킵
    # — 데몬이 발굴 주기로 같은 일을 하므로 이중 수집(LLM 비용·중복 보고) 방지.
    # 데몬 미가동이면 그대로 실행(안전망 유지). schedule_manager 가드(Windows)와 동일 취지.
    if args.propose and not args.daemon and sys.platform != "win32":
        import subprocess
        try:
            out = subprocess.run(["pgrep", "-f", r"somi_trade_advisor\.py.*--daemon"],
                                 capture_output=True, text=True, timeout=5).stdout
            if any(p.isdigit() for p in out.split()):
                print("[소미] one-shot 스킵 — 매수 데몬 가동 중(이중실행 방지)")
                return
        except Exception:
            pass  # 판정 실패 시 실행 — 잡 누락이 이중 수집보다 해롭다

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
        if _is_paper():
            # 모의: 고정 매수 슬롯 폐지(사용자 지시 2026-07-02) — 발굴(무거움)은 주기 실행,
            # 사이엔 고속감시(_fast_watch)로 발굴 후보를 실시간 재평가해 조건 충족 즉시 매수.
            # 가격모니터 강신호(_consume_trigger)는 발굴 주기를 기다리지 않고 즉시 발굴.
            fast_sec = int(os.getenv("SOMI_FAST_WATCH_SEC", "60"))
            disc_min = int(os.getenv("SOMI_DISCOVERY_MIN", "10"))
            last_disc, regime = None, "unknown"
            with ProcessLock("somi_trade_advisor"):
                print(f"[{datetime.now()}] 소미 매수 데몬 시작 (동적 모드: 발굴 {disc_min}분 주기 + 고속감시 {fast_sec}초)")
                while True:
                    now = datetime.now()
                    hm = now.strftime("%H:%M")
                    if now.weekday() >= 5 or not ("09:00" <= hm <= "15:20"):
                        time.sleep(60)   # 장외 대기
                        continue
                    disc_due = (not last_disc or (now - last_disc).total_seconds() >= disc_min * 60
                                or _consume_trigger())
                    # 발굴은 '자금 있고 매수 시작 전에만'(사용자 지시 2026-07-03) — 전액 투자/유보 도달 시
                    # 무거운 발굴(스크리너·뉴스·40종목 분석)을 건너뛴다. 보유 관리(청산·분할증액)는 계속.
                    if disc_due and _deployable_cash() <= 0:
                        disc_due = False
                        if not last_disc or (now - last_disc).total_seconds() >= disc_min * 60:
                            last_disc = now   # 재검사 주기 유지(매 틱 스킵로그 방지)
                            print(f"[{datetime.now()}] 발굴 스킵 — 투자가능 현금 없음(전액 투자/유보 도달)")
                    if disc_due:
                        last_disc = now
                        kind = "buy_close" if hm >= "15:00" else "buy"  # 마감권 제한 규율 유지
                        try:
                            print(f"[{datetime.now()}] 발굴 실행({kind})")
                            run(args.candidates, do_send=True, slot_kind=kind)
                            regime = _market_regime_now()[0]
                        except Exception as e:
                            send(f"⚠️ 소미 매수 제안 오류: {e}")
                    else:
                        try:
                            _fast_watch(regime)
                        except Exception as e:
                            print(f"[{datetime.now()}] 고속감시 오류: {e}")
                    try:
                        _addon_scale_in()   # 돌파 분할매수 2차 증액 감시(모의 전용)
                    except Exception as e:
                        print(f"[{datetime.now()}] 분할증액 오류: {e}")
                    time.sleep(fast_sec)
            return
        from _shared.utils import due_slot
        # 실거래: 보수 스케줄(승인형) 유지 — 동적 고속감시는 모의 전용.
        slots = os.getenv("SOMI_ADVISOR_SLOTS", "09:00,11:00,12:30,14:00,15:10,15:25").split(",")
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
                elif _consume_trigger():  # 가격모니터 강한 신호 — 정시 슬롯 대기 없이 즉시 매수검토
                    try:
                        run(args.candidates, do_send=True, slot_kind="buy")
                    except Exception as e:
                        print(f"[{datetime.now()}] 트리거 실행 오류: {e}")
                time.sleep(60)  # 1분마다 슬롯/트리거 확인(보고는 슬롯당 1회, 트리거는 쿨다운 10분)
        return

    print(run(args.candidates, args.send, slot_kind=args.slot))


if __name__ == "__main__":
    main()
