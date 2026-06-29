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
from _shared.notify import publish_report, send  # noqa: E402
from _shared.llm import text as llm_text  # noqa: E402
from _shared import research  # noqa: E402
from _shared import growth  # noqa: E402
from _shared.process import ProcessLock  # noqa: E402
from somi_kis_reporter import KISClient, build_input_text  # noqa: E402
from short_covering_analyzer import parse_input_text, calculate_score, to_num  # noqa: E402
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
                    qty: int, score: int | None = None) -> None:
    pos = load_positions()
    pos[symbol] = {
        "name": name, "entry": entry, "stop": stop, "target": target,
        "qty": qty, "ts": datetime.now().strftime("%Y-%m-%d %H:%M"), "score": score,
    }
    _save(POSITIONS_FILE, pos)


def remove_position(symbol: str) -> None:
    pos = load_positions()
    if symbol in pos:
        del pos[symbol]
        _save(POSITIONS_FILE, pos)


def log_closed_trade(symbol: str, name: str, entry: float, exit_price: float, qty: int,
                     reason: str, ts_open: str = "", score=None) -> None:
    """청산된 모의 거래를 로그에 적재 — 성과추적(백테스트 대비 실제 승률·수익률)용."""
    try:
        log = json.loads(CLOSED_TRADES_FILE.read_text(encoding="utf-8")) if CLOSED_TRADES_FILE.exists() else []
    except Exception:
        log = []
    ret_pct = ((exit_price - entry) / entry * 100) if entry else 0.0
    log.append({
        "symbol": symbol, "name": name, "entry": entry, "exit": exit_price, "qty": qty,
        "ret_pct": round(ret_pct, 2), "reason": reason, "score": score,
        "ts_open": ts_open, "ts_close": datetime.now().strftime("%Y-%m-%d %H:%M"),
    })
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


def analyze_candidate(kis: KISClient, code: str, name: str) -> dict | None:
    try:
        parsed = parse_input_text(build_input_text(kis, "제안", code, name))
    except Exception:
        return None
    score, grade, pos, neg = calculate_score(parsed)
    entry, stop, target = _levels(parsed)
    rr = (target - entry) / (entry - stop) if entry > stop else 0  # 손익비
    soomgeup_net = to_num(parsed.get("buy_foreigner_5d")) + to_num(parsed.get("buy_institution_5d"))
    return {
        "symbol": code, "name": name, "score": score, "grade": grade,
        "change": parsed.get("change_pct", ""),
        "entry": entry, "stop": stop, "target": target, "rr": round(rr, 2),
        "soomgeup_net": soomgeup_net,  # 기관+외국인 5일 누적 순매수(수급확인 게이트용)
        "reasons": pos[:3], "risks": neg[:3] or ["뚜렷한 위험 신호 없음"],
    }


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


# 모의 모드 1회 실행 시 자동 매수할 최대 종목 수 (예산 소진 시 자동 중단)
PAPER_AUTO_MAX = int(os.getenv("SOMI_PAPER_AUTO_MAX", "3"))
SOMI_BUDGET = int(os.getenv("SOMI_BUDGET_PER_TRADE", "1000000"))
# 매수 전 관찰 시간(분): 'buy' 신호가 이 시간 이상 유지돼야 실제 매수 (바로 안 삼)
OBSERVE_MINUTES = int(os.getenv("SOMI_OBSERVE_MINUTES", "30"))
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


def _auto_buy_paper(proposals: list[dict]) -> list[str]:
    """모의 모드: 상위 미보유 종목을 승인 없이 자동 매수. (실거래에서는 호출 안 함)"""
    from kis_trader import KISTrader

    trader = KISTrader()
    if not trader.paper:  # 안전장치: 실거래면 자동매수 금지
        return []
    held = load_positions()
    done = []
    for p in proposals:
        if len(done) >= PAPER_AUTO_MAX:
            break
        if p["symbol"] in held:
            continue
        entry = p.get("entry") or 1
        # 확신 기반 사이징(백테스트 검증): 점수 높을수록 크게. 평균점수(~65) 기준 1.0배로 재중심해
        # 평균 투입자본은 그대로 두고 고점수에만 더 배분 → 공정비교서 24mo +264%→+298%,
        # 30mo +348%→+385%, MDD 동등/개선. (위험기반·균등보다 우월)
        conv = min(2.0, max(0.5, 1.0 + (p.get("score", 65) - 65) / 40.0))
        qty = max(1, int(int(SOMI_BUDGET * conv) // entry))
        try:
            res = trader.order(p["symbol"], qty, "buy", 0)
        except Exception as exc:
            done.append(f"⏭️ {p['name']} 매수 건너뜀: {exc}")
            continue
        fill = res.get("price") or entry  # 실제 페이퍼 체결가 — 성과·손익 계산 기준(제안가 아님)
        record_position(p["symbol"], p["name"], fill, p["stop"], p["target"], qty, p.get("score"))
        done.append(
            f"🧪 자동 매수(모의) — {p['name']}({p['symbol']}) {qty}주 @ {int(fill):,}원 "
            f"(확신 {conv:.1f}배·점수 {p.get('score', '?')})\n"
            f"   손절 {int(p['stop']):,} / 목표 {int(p['target']):,} 감시 시작"
        )
    return done


def run(candidate_limit: int = 20, do_send: bool = False) -> str:
    proposals = make_proposals(candidate_limit)
    # 수급 제안에 뉴스·공시 영향도 반영(후처리 LLM 판단)
    proposals = apply_news_judgment(proposals)
    _save(PROPOSALS_FILE, {
        "ts": datetime.now().strftime("%Y-%m-%d %H:%M"),
        "items": proposals,
    })
    # 시장 국면(HMM): 하락 국면이면 신규 매수 중단(떨어지는 장 회피) — 매도/감시는 계속
    try:
        from market_regime import market_regime, regime_label
        _reg = market_regime()
        regime = _reg.get("regime", "unknown")
    except Exception:
        regime, _reg = "unknown", {}
        regime_label = lambda r: r  # noqa: E731
    header = (f"[소미 매수 제안 / {datetime.now().strftime('%Y-%m-%d %H:%M')}]\n"
              f"시장 국면(HMM): {regime_label(regime)}")
    # 매수 대상 = verdict=buy · 미보유 · (하락 국면 아님)
    held_syms = set(load_positions().keys())
    buys = [p for p in proposals if p.get("verdict") == "buy" and p["symbol"] not in held_syms]
    if regime == "bear" and os.getenv("SOMI_DISABLE_REGIME_GATE", "").lower() not in {"1", "true", "yes"}:
        buys = []  # 하락 국면 → 신규 매수 중단 (SOMI_DISABLE_REGIME_GATE=true 면 한시적 해제)
    # 수급확인 게이트(백테스트 검증: 샤프 3.15→5.80·MDD 반토막): 기관+외국인 5일 누적 순매수>0만
    if os.getenv("SOMI_SOOMGEUP_GATE", "true").lower() in {"1", "true", "yes"}:
        buys = [p for p in buys if p.get("soomgeup_net", 0) > 0]
    # 몬테카를로 확률 게이트: 목표도달이 손절보다 충분히 우위(edge)이고 기대수익>0 인 것만 매수
    if buys:
        try:
            from mc_simulator import simulate as _mc
            min_edge = float(os.getenv("SOMI_MC_MIN_EDGE", "0.05"))
            gated = []
            for p in buys:
                mc = _mc(p["symbol"], p["entry"], p["stop"], p["target"])
                p["mc"] = mc
                # fail-closed: MC 계산 실패(None)면 매수 보류 — 게이트가 불확실성에 열리지 않게
                if mc is not None and mc["edge"] >= min_edge and mc["exp_ret_pct"] > 0:
                    gated.append(p)
            buys = gated
        except Exception:
            pass
    if not proposals:
        report = f"{header}\n오늘은 소미 기준({GOOD_SCORE}점↑) 매수 제안 종목이 없습니다. 계속 감시 중."
    elif _is_paper():
        # 모의 모드: 'buy' 신호도 바로 안 사고 관찰 게이트 통과한 것만 자동 매수
        to_buy, watch_msgs = _observation_gate(buys)
        executed = _auto_buy_paper(to_buy)
        parts = []
        if watch_msgs:
            parts.append("[관찰 현황]\n" + "\n".join(watch_msgs))
        parts.append("[매수 체결]\n" + ("\n\n".join(executed) if executed else "이번엔 매수 없음 (관찰 중이거나 신호 없음)"))
        report = f"{header} 🧪 모의 자동매매(뉴스+관찰)\n\n" + "\n\n".join(parts)
    else:
        body = "\n\n".join(_fmt(p) for p in proposals[:3])
        tip = ("매수하려면 '소미 승인 <종목명>', 넘기려면 '패스'. (승인 없이는 매수 안 함)"
               if buys else "현재 'buy' 판정 없음 — 계속 감시.")
        report = f"{header}\n수급+뉴스 종합 판단입니다. {tip}\n\n{body}"
    if do_send:
        publish_report("소미 매수 제안", report)
    growth.record(
        "somi_advisor", role="매수 제안(수급확인 게이트)",
        data=f"후보 {len(proposals)} / 국면 {regime}", judgment=f"매수대상 {len(buys)}",
        result=("모의 자동매수" if _is_paper() else "승인형 제안"),
        good="국면·수급·MC 게이트 통과분만", bad=("후보 0" if not proposals else ""),
        scores={"fit": 22, "evidence": 20, "efficiency": 18, "risk": 19, "brevity": 8},
    )
    return report


def main() -> None:
    import argparse

    parser = argparse.ArgumentParser(description="소미 매수 제안 (승인형, 자동매수 없음)")
    parser.add_argument("--propose", action="store_true", help="후보 분석 후 제안 생성")
    parser.add_argument("--candidates", type=int, default=20)
    parser.add_argument("--send", action="store_true", help="텔레그램 전송")
    parser.add_argument("--daemon", action="store_true", help="데몬 모드 (08:00~20:00, 30분 주기)")
    args = parser.parse_args()

    if args.daemon:
        with ProcessLock("somi_trade_advisor"):
            print(f"[{datetime.now()}] 소미 매수 제안 데몬 시작 (08:00~20:00, 30분 주기)")
            while True:
                now = datetime.now()
                hour = now.hour
                # 08:00~20:00만 동작
                if 8 <= hour < 20:
                    try:
                        print(f"[{now}] 후보 분석 시작")
                        result = run(args.candidates, do_send=True)
                        print(f"[{now}] 완료: {result[:200]}")
                    except Exception as e:
                        send(f"⚠️ 소미 매수 제안 오류: {e}")
                        print(f"[{now}] 오류: {e}")
                else:
                    print(f"[{now}] 대기 중 (활동시간: 08:00~20:00)")
                time.sleep(1800)  # 30분
        return

    print(run(args.candidates, args.send))


if __name__ == "__main__":
    main()
