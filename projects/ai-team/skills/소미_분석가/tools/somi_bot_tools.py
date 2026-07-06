#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""소미 분석가 — 텔레그램 봇 툴. 영숙 게이트웨이가 BOT_TOOLS를 수집해 등록한다.

종목/투자 성격 툴(감시목록·조회·뉴스·공시·투자하우스·모닝노트)의 주인은 소미다.
영숙(telegram_receiver)은 이 모듈을 import해 자기 툴 레지스트리에 병합만 한다."""

from __future__ import annotations

import re
import sys
from pathlib import Path

_HERE = Path(__file__).resolve()
_SOMI_TOOLS = _HERE.parent
_AI_TEAM_ROOT = _HERE.parents[3]
_YS_TOOLS = _AI_TEAM_ROOT / "skills" / "영숙_비서" / "tools"
for _p in (str(_AI_TEAM_ROOT), str(_SOMI_TOOLS), str(_YS_TOOLS)):
    if _p not in sys.path:
        sys.path.insert(0, _p)

import bot_common as bc

_INVEST_HOUSE = _HERE.with_name("investment_house.py")


# ── 소미 위임/발굴 ───────────────────────────────────────────────────────────
def dispatch_to_somi(text: str) -> str:
    """소미 분석가에게 종목 분석을 요청합니다."""
    script = _HERE.with_name("somi_kis_reporter.py")
    stock = bc.stock_from_text(text)
    if not stock:
        return "어떤 종목을 분석할까요? 종목명이나 6자리 코드를 알려줘요. 예: SK스퀘어 분석, 005930 리포트"
    symbol, name = stock
    return bc.run_python(script, "--print", "--symbol", symbol, "--name", name, timeout=90)


def dispatch_screener(_: str = "") -> str:
    """소미 매수 제안 (발굴+점수화 → 진입/손절/목표/이유/위험, 승인형)."""
    script = _HERE.with_name("somi_trade_advisor.py")
    return bc.run_python(script, "--propose", "--candidates", "20", timeout=300)


# ── 관심종목 ─────────────────────────────────────────────────────────────────
def add_watchlist(symbol: str, name: str) -> str:
    """소미 감시 목록에 종목 추가"""
    return bc.run_python(_HERE.with_name("watchlist_manager.py"), "add", "--symbol", symbol, "--name", name, timeout=30)


def remove_watchlist(symbol: str) -> str:
    """소미 감시 목록에서 종목 제거"""
    return bc.run_python(_HERE.with_name("watchlist_manager.py"), "remove", "--symbol", symbol, timeout=30)


def list_watchlist() -> str:
    """소미 감시 종목 목록 조회"""
    return bc.run_python(_HERE.with_name("watchlist_manager.py"), "list", timeout=30)


def search_stock(query: str) -> str:
    """종목명으로 종목코드 검색"""
    return bc.run_python(_HERE.with_name("stock_search.py"), query, timeout=30)


# ── 뉴스·공시 ────────────────────────────────────────────────────────────────
def get_stock_news(query: str) -> str:
    """종목/키워드 관련 최신 뉴스·공시를 웹에서 찾아 요약."""
    from _shared import research
    try:
        brief = research.web_brief(
            f"'{query}' 종목·기업의 최근 뉴스·공시·이슈를 3~5줄로 요약하라. "
            f"호재/악재와 근거(날짜·핵심 내용)를 간결히. 확실치 않으면 추측하지 말고 "
            f"'뚜렷한 최신 뉴스 없음'이라고 답하라.",
            max_tokens=700,
        )
        return f"📰 {query} 관련 뉴스\n\n{(brief or '').strip() or '뚜렷한 최신 뉴스를 못 찾았어.'}"
    except Exception as e:
        return f"{query} 뉴스 조회 중 오류가 났어: {e}"


def get_disclosures(query: str = "") -> str:
    """최근 공시 조회 — 유나(아시아조사)의 DART 수집분 우선, 특정 종목은 웹으로 보강."""
    import os
    from _shared import research
    try:
        asia = research.load_region("asia") or {}
        items = asia.get("disclosures") or []
        if query:
            hit = [d for d in items if query in (d.get("name", "") + d.get("report", ""))]
            if hit:
                return "\n".join([f"📑 '{query}' 최근 공시 {len(hit)}건"] +
                                 [f"- {d.get('date', '')} {d.get('name', '')}: {d.get('report', '')}" for d in hit[:8]])
            brief = research.web_brief(
                f"'{query}' 종목의 최근 1주일 공시(DART)·주요 발표를 3~5줄로 요약. "
                f"날짜·공시명 중심, 확실치 않으면 '확인된 공시 없음'이라고 답하라.", max_tokens=300)
            return f"📑 '{query}' — 자동수집분엔 없어 웹에서 확인:\n{(brief or '').strip() or '확인 실패'}"
        if items:
            return "\n".join([f"📑 관심종목 최근 공시 {len(items)}건 (수집 {asia.get('updated', '?')})"] +
                             [f"- {d.get('date', '')} {d.get('name', '')}: {d.get('report', '')}" for d in items[:10]])
        if not os.getenv("DART_API_KEY", "").strip():
            return ("📑 공시 자동수집이 꺼져 있어요 — DART_API_KEY 미설정.\n"
                    "opendart.fss.or.kr에서 무료 API 키 발급 후 .env에 DART_API_KEY로 넣으면 "
                    "유나가 관심종목 공시를 매 사이클 수집해요. 특정 종목은 '삼성전자 공시'처럼 물으면 웹으로 찾아볼게요.")
        return "📑 최근 2일간 관심종목 신규 공시가 없어요."
    except Exception as e:
        return f"공시 조회 중 오류가 났어: {e}"


# ── 1인 투자하우스 ───────────────────────────────────────────────────────────
def invest_scout(text: str) -> str:
    """Action 1: 종목탐색 (섹터 포지션 → 스크리닝 → 어닝스 프리뷰 → 투자 메모)"""
    stock = bc.stock_from_text(text)
    if stock:
        symbol, name = stock
        return bc.run_python(_INVEST_HOUSE, "action1", symbol, name, timeout=300)
    return "종목을 찾지 못했어요. 예: '삼성전자 투자분석해줘'"


def invest_track(text: str) -> str:
    """Action 2: 추적관찰 (Thesis 업데이트 → 카탈리스트 → 어닝스 리뷰)"""
    stock = bc.stock_from_text(text)
    if stock:
        symbol, name = stock
        return bc.run_python(_INVEST_HOUSE, "action2", symbol, name, timeout=300)
    return "종목을 찾지 못했어요. 예: 'SK하이닉스 추적관찰해줘'"


def invest_track_all() -> str:
    """소미 보유 전 종목 일괄 추적관찰 (매수→추적 루프 자동화)"""
    return bc.run_python(_INVEST_HOUSE, "track-all", timeout=300)


def invest_sector(text: str = "") -> str:
    """섹터 초감 분석 — 메시지에서 섹터명 동적 추출"""
    t = (text or "").strip()
    m = re.search(r"([가-힣A-Za-z0-9·/&]+)\s*섹터", t)
    if m:
        sector = m.group(1)
    else:
        for stop in ["분석해줘", "분석", "초감", "어때", "현황", "전망",
                     "해줘", "알려줘", "관련", "에 대해", "에대해", "좀"]:
            t = t.replace(stop, " ")
        parts = t.split()
        sector = parts[0] if parts else "AI반도체"
    return bc.run_python(_INVEST_HOUSE, "sector", sector, timeout=120)


def morning_note_now() -> str:
    """모닝노트 즉시 전송"""
    return bc.run_python(_HERE.with_name("morning_note.py"), timeout=120)


# ── 조회 헬퍼 (게이트웨이가 직접 호출 — LLM 등록 아님) ────────────────────────
def get_stock_price(text: str) -> str:
    stock = bc.stock_from_text(text)
    if not stock:
        return "종목을 찾지 못했어요. 예: 삼전 주가, 하이닉스 현재가"
    symbol, name = stock
    try:
        from somi_kis_reporter import KISClient, fmt_pct, fmt_int, pick
        quote = KISClient().quote(symbol)
        price = fmt_int(pick(quote, "stck_prpr"))
        change = fmt_int(pick(quote, "prdy_vrss"))
        rate = fmt_pct(pick(quote, "prdy_ctrt"))
        return f"{name} 현재가: {price or '확인 필요'}원, 전일대비 {change or '0'}원 ({rate or '0.00%'})"
    except Exception as exc:
        return f"{name} 현재가 조회 실패: {exc}"


def position_verdict(pnl: float, cur: float, stop: float, target: float, tp1: float) -> str:
    """보유 종목 다음 행동 판단(앞으로 어쩔지)."""
    if target and cur >= target:
        return "🟢 목표 도달 — 익절 검토"
    if stop and cur <= stop:
        return "🔴 손절선 도달 — 청산 검토"
    if tp1 and cur >= tp1:
        return "🟢 +5% 돌파 — 분할익절/트레일링"
    if pnl <= -3:
        return "🟠 손절선 근접 — 주의"
    if target and cur:
        return f"⚪ 보유 (목표까지 {((target - cur) / cur * 100):+.1f}%)"
    return "⚪ 보유"


def _today_realized() -> str:
    """오늘 청산된 거래의 실현손익 요약 — 보유 포지션이 없어도 '수익현황'에 답을 준다.
    금액은 ret_pct(실비용 반영 순수익률) 기준으로 재구성해 표시. 청산 없으면 ''."""
    try:
        import json
        from datetime import datetime
        from somi_trade_advisor import CLOSED_TRADES_FILE
        from somi_kis_reporter import num
        log = json.loads(CLOSED_TRADES_FILE.read_text(encoding="utf-8"))
    except Exception:
        return ""
    today = datetime.now().strftime("%Y-%m-%d")
    tr = [t for t in log if str(t.get("ts_close", "")).startswith(today)]
    if not tr:
        return ""
    amt = sum(num(t.get("qty")) * num(t.get("entry")) * num(t.get("ret_pct")) / 100 for t in tr)
    wins = sum(1 for t in tr if num(t.get("ret_pct")) > 0)
    avg = sum(num(t.get("ret_pct")) for t in tr) / len(tr)
    srt = sorted(tr, key=lambda t: num(t.get("ret_pct")), reverse=True)
    best, worst = srt[0], srt[-1]
    return (
        f"[오늘 실현손익] 청산 {len(tr)}건 · {amt:+,.0f}원 (평균 {avg:+.1f}% · 승 {wins}/{len(tr)})\n"
        f"  최고 {best.get('name')} {num(best.get('ret_pct')):+.1f}% · 최저 {worst.get('name')} {num(worst.get('ret_pct')):+.1f}%"
    )


def _account_header() -> str:
    """계좌 종합 — 예수금·보유평가·총자산·누적손익(초기자본 대비). 원장(모의) 또는 KIS(실거래) 기준."""
    try:
        import os
        from kis_trader import KISTrader
        from somi_kis_reporter import num
        t = KISTrader()
        bal = t.balance()
        cash = num(bal.get("cash"))
        hval = sum(num(h.get("qty")) * num(h.get("avg")) * (1 + num(h.get("pnl")) / 100)
                   for h in (bal.get("holdings") or []))
        total = cash + hval
        line = f"💰 예수금 {cash:,.0f}원 · 보유평가 {hval:,.0f}원 · 총자산 {total:,.0f}원"
        if t.paper:   # 모의는 초기자본 대비 누적손익 표시(실거래는 입출금 있어 생략)
            start = float(os.getenv("SOMI_PAPER_CASH", "10000000"))
            cum = total - start
            line += f"\n📈 누적손익 {cum:+,.0f}원 ({cum / start * 100:+.1f}% · 초기 {start / 10000:,.0f}만원)"
        return line
    except Exception as exc:
        return f"(계좌 조회 실패: {exc})"


def get_trading_status(is_live: bool = False) -> str:
    """거래 현황 — 계좌 종합(예수금·총자산·누적손익) + 보유 평가손익 + 오늘 실현손익.

    거래모드(모의/실거래) 판정은 봇 상태이므로 게이트웨이가 is_live로 넘긴다."""
    try:
        from somi_trade_advisor import load_positions
        from somi_kis_reporter import KISClient, num
    except Exception:
        return "거래 현황 조회 모듈을 불러오지 못했어요."
    mode = "🔴 실거래(실제 돈)" if is_live else "🧪 모의(페이퍼)"
    acct = _account_header()
    realized = _today_realized()
    pos = load_positions()
    if not pos:
        base = f"[손익 현황] {mode}\n{acct}"
        base += f"\n\n{realized}" if realized else "\n오늘 청산 거래 없음."
        return base + "\n보유 포지션 없음 — 현재 거래 중인 종목이 없어요."
    kis = KISClient()
    lines = []
    take, cut = 0, 0
    tot_pnl_amt, tot_cost = 0.0, 0.0   # 자본가중 손익 — 종목 크기 반영(단순평균 아님)
    for sym, p in pos.items():
        try:
            cur = num(kis.quote(sym).get("stck_prpr"))
        except Exception:
            cur = 0.0
        entry = num(p.get("entry"))
        stop = num(p.get("stop"))
        target = num(p.get("target"))
        tp1 = num(p.get("tp1"))
        qty = int(p.get("qty") or 0)
        pnl = ((cur - entry) / entry * 100) if (cur and entry) else 0.0
        pnl_amt = qty * (cur - entry) if (cur and entry) else 0.0   # 평가손익(원)
        tot_pnl_amt += pnl_amt
        tot_cost += qty * entry
        verdict = position_verdict(pnl, cur, stop, target, tp1)
        if "익절" in verdict:
            take += 1
        elif "손절" in verdict:
            cut += 1
        lines.append(
            f"• {p.get('name', sym)}({sym}) {qty}주 @ {int(entry):,} → {int(cur):,} ({pnl:+.1f}%, {pnl_amt:+,.0f}원)\n"
            f"   손절 {int(stop):,} / 목표 {int(target):,} · {verdict}"
        )
    wpnl = (tot_pnl_amt / tot_cost * 100) if tot_cost else 0.0   # 자본가중 수익률
    head = (f"[손익 현황] {mode}\n{acct}\n"
            f"보유 {len(pos)}종목 · 평가손익 {tot_pnl_amt:+,.0f}원 ({wpnl:+.1f}%)")
    tail = f"\n요약: 익절검토 {take} · 손절주의 {cut} · 그 외 보유 {len(pos) - take - cut}"
    return head + "\n" + "\n".join(lines) + tail + (f"\n\n{realized}" if realized else "")


def balance() -> str:
    """계좌 잔고 조회."""
    try:
        from kis_trader import KISTrader, _fmt_balance
        t = KISTrader()
        mode = "실거래" if t.real else "모의투자"
        return f"[{mode} 계좌 {t.cano}-{t.prod}]\n" + _fmt_balance(t.balance())
    except Exception as exc:
        return f"잔고 조회 실패: {exc}"


BOT_TOOLS = [
    {"handler": dispatch_to_somi, "schema": {"type": "function", "function": {
        "name": "dispatch_to_somi",
        "description": "종목의 수급·세력·매수 타이밍 즉시 분석(소미 수급 점수). '수급 어때', '세력 붙었나', "
                       "'지금 사도 돼', '살만해', '들어가도 될까', '점수 몇' 등 단기 매매 판단·매수 타이밍 질문. "
                       "종합·장기 투자 판단은 invest_scout.",
        "parameters": {"type": "object", "properties": {"text": {"type": "string", "description": "분석 요청 메시지"}}, "required": ["text"]}}}},
    {"handler": dispatch_screener, "schema": {"type": "function", "function": {
        "name": "dispatch_screener",
        "description": "유망 종목 발굴. 사용자가 특정 종목을 지정하지 않고 '유망종목/추천종목/뭐 살까/발굴' 등을 물으면 호출. 거래량 상위 종목을 소미 점수로 채점해 상위 종목을 추천",
        "parameters": {"type": "object", "properties": {}, "required": []}}}},
    {"handler": search_stock, "schema": {"type": "function", "function": {
        "name": "search_stock",
        "description": "종목명으로 종목코드 검색. 사용자가 '삼전 감시' 같이 종목명만 말하면 먼저 이 함수로 검색",
        "parameters": {"type": "object", "properties": {"query": {"type": "string", "description": "검색어 (종목명)"}}, "required": ["query"]}}}},
    {"handler": get_disclosures, "schema": {"type": "function", "function": {
        "name": "get_disclosures",
        "description": "최근 공시(DART) 조회. '공시 알려줘', '오늘 공시 뭐 있어?', 'OO 공시 떴어?' 등 공시 관련 질문이면 호출. query 비우면 관심종목 전체, 종목명 주면 해당 종목만",
        "parameters": {"type": "object", "properties": {"query": {"type": "string", "description": "종목명(선택, 예: 삼성전자). 전체 공시는 빈 문자열"}}, "required": []}}}},
    {"handler": get_stock_news, "schema": {"type": "function", "function": {
        "name": "get_stock_news",
        "description": "종목·기업 관련 최신 뉴스/이슈를 웹에서 찾아 요약. 사용자가 '우리기술 뉴스', 'OO 관련뉴스', 'OO 무슨 일 있어?' 처럼 특정 종목의 뉴스·소식을 물으면 호출",
        "parameters": {"type": "object", "properties": {"query": {"type": "string", "description": "종목명 또는 키워드 (예: 우리기술)"}}, "required": ["query"]}}}},
    {"handler": add_watchlist, "schema": {"type": "function", "function": {
        "name": "add_watchlist", "description": "소미 감시 목록에 종목 추가 (search_stock으로 종목코드 확인 후 사용)",
        "parameters": {"type": "object", "properties": {"symbol": {"type": "string", "description": "종목코드"}, "name": {"type": "string", "description": "종목명"}}, "required": ["symbol", "name"]}}}},
    {"handler": remove_watchlist, "schema": {"type": "function", "function": {
        "name": "remove_watchlist", "description": "소미 감시 목록에서 종목 제거",
        "parameters": {"type": "object", "properties": {"symbol": {"type": "string", "description": "종목코드"}}, "required": ["symbol"]}}}},
    {"handler": list_watchlist, "schema": {"type": "function", "function": {
        "name": "list_watchlist", "description": "소미가 감시 중인 종목 목록 조회",
        "parameters": {"type": "object", "properties": {}, "required": []}}}},
    {"handler": invest_scout, "schema": {"type": "function", "function": {
        "name": "invest_scout",
        "description": "종목 심층 투자 탐색 메모(Action 1): 섹터 포지션·밸류에이션·어닝스 프리뷰. "
                       "'투자 메모', '깊이 분석해줘', 'initiate', '중장기 투자 관점' 등 종합·장기 투자 판단. "
                       "단기 수급·매수 타이밍('지금 사도 돼', '살만해')은 dispatch_to_somi.",
        "parameters": {"type": "object", "properties": {"text": {"type": "string", "description": "종목명이 포함된 요청 메시지"}}, "required": ["text"]}}}},
    {"handler": invest_track, "schema": {"type": "function", "function": {
        "name": "invest_track",
        "description": "보유 종목 추적관찰 (Action 2): Thesis 업데이트·카탈리스트 체크·어닝스 리뷰. 사용자가 '추적', '관찰', '업데이트', '보유 중인데', '계속 봐줘' 등을 말하면 호출",
        "parameters": {"type": "object", "properties": {"text": {"type": "string", "description": "종목명이 포함된 요청 메시지"}}, "required": ["text"]}}}},
    {"handler": invest_sector, "schema": {"type": "function", "function": {
        "name": "invest_sector",
        "description": "섹터 초감 분석: AI, 반도체, 바이오, 금융 등 섹터 전반 이슈 요약. 사용자가 '섹터 분석', '반도체 어때', 'AI 섹터' 등 섹터 전반을 물으면 호출",
        "parameters": {"type": "object", "properties": {"text": {"type": "string", "description": "섹터명이 포함된 요청 메시지"}}, "required": ["text"]}}}},
    {"handler": invest_track_all, "schema": {"type": "function", "function": {
        "name": "invest_track_all",
        "description": "소미가 실제 보유 중인 모든 종목을 일괄 추적관찰(Action 2). 사용자가 '보유종목 다 추적', '내 종목 전부 점검', '포지션 추적관찰' 등을 말하면 호출",
        "parameters": {"type": "object", "properties": {}, "required": []}}}},
    {"handler": morning_note_now, "schema": {"type": "function", "function": {
        "name": "morning_note_now",
        "description": "모닝노트 즉시 생성 및 전송. 사용자가 '모닝노트', '아침 브리핑', '오늘 시장 요약' 등을 요청하면 호출",
        "parameters": {"type": "object", "properties": {}, "required": []}}}},
]
