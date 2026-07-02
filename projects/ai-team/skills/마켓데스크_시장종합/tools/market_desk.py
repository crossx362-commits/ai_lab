#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""마켓데스크 — 지역 브리프 종합.

행크(미국)·유나(아시아)·레온(유럽)이 저장한 region_*.json 을 모아
하나의 시장 종합 브리프(market_brief.md/json)를 만들고, LLM으로 한국 증시
관점의 코멘트를 덧붙인다. --send 시 텔레그램 전송.
"""

from __future__ import annotations

import argparse
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

from _shared.env import load_env  # noqa: E402
from _shared.notify import publish_report, send  # noqa: E402
from _shared.llm import text, gpt, gemini, claude  # noqa: E402
from _shared import research  # noqa: E402
from _shared import growth  # noqa: E402
from _shared.process import ProcessLock  # noqa: E402

load_env(str(PROJECT_ROOT))


def _idx_line(region: dict) -> str:
    return " / ".join(f"{k} {v['close']}" for k, v in (region.get("indices") or {}).items() if v)


def _short(s: str, n: int = 120) -> str:
    s = (s or "").strip().replace("\n", " ")
    return (s[:n] + "…") if len(s) > n else s


def _somi_candidates(limit: int = 20) -> list[tuple[str, str]]:
    """소미 매매 후보(거래대금 상위) 종목 — 뉴스 평가 대상에 포함시키기 위해."""
    try:
        sys.path.insert(0, str(AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools"))
        from somi_screener import get_candidates
        from somi_kis_reporter import KISClient
        return get_candidates(KISClient(), limit)
    except Exception as exc:
        print(f"[market_desk] 소미 후보 조회 실패: {exc}", file=sys.stderr)
        return []


def _build_issue_impact(disclosures: list, us: dict, asia: dict, eu: dict) -> None:
    """소미 후보 ∪ 공시 종목에 대해 시장 맥락 기반 호재/악재(-2~+2) 평가 → issue_impact.json.
    실제 뉴스 근거가 있는(0이 아닌) 종목만 저장한다(0=뉴스없음 취급)."""
    import json as _json

    # 평가 대상 종목 (코드→이름)
    targets: dict[str, str] = {}
    for code, name in _somi_candidates(20):
        targets[code] = name
    by_code: dict[str, dict] = {}
    for d in disclosures or []:
        by_code.setdefault(d["code"], {"name": d["name"], "reports": []})["reports"].append(d["report"])
        targets[d["code"]] = d["name"]
    if not targets:
        return

    # LLM에 줄 시장 맥락 (지역 웹이슈 + 증권뉴스 + 공시 상세 + 심리)
    ctx = []
    for label, reg in (("미국", us), ("아시아/한국", asia), ("유럽", eu)):
        w = reg.get("web_issues")
        if w:
            ctx.append(f"[{label} 웹이슈] {w}")
    if asia.get("news"):
        ctx.append("[증권뉴스] " + " | ".join(asia["news"][:8]))
    if by_code:
        ctx.append("[공시] " + "; ".join(f"{v['name']}: {', '.join(v['reports'])}" for v in by_code.values()))
    fg = research.fear_greed()
    if fg.get("score") is not None:
        ctx.append(f"[공포탐욕] {fg['score']}({fg.get('rating', '')})")
    ctx_str = "\n".join(ctx) or "특이 뉴스 없음"
    target_list = "\n".join(f"{c} {n}" for c, n in targets.items())

    prompt = (
        "아래 '시장 맥락'(뉴스·공시·웹이슈)을 근거로, '평가 대상 종목' 각각의 향후 주가 영향도를 "
        "정수 -2(강한 악재)~+2(강한 호재)로 평가하라. 해당 종목에 대한 구체적 뉴스/공시 근거가 없으면 0으로 둬라. "
        "반드시 JSON만 출력: {\"종목코드\":{\"score\":정수,\"reason\":\"한 줄(근거 없으면 빈 문자열)\"}}\n\n"
        f"[시장 맥락]\n{ctx_str}\n\n[평가 대상 종목]\n{target_list}"
    )
    impact = {}
    evaluated = False   # LLM 평가가 실제 수행됐는지 — 실패 시 빈 값으로 덮어쓰지 않기 위한 구분
    try:
        # issue_impact는 소미 매매 판단의 핵심 입력 → JSON 신뢰성 우선. 로컬 모델(json_mode 미적용)은
        # 파싱 실패가 잦아 GPT(json_mode)→Gemini→Claude→로컬 순으로 고정. (로컬은 최후 폴백)
        resp = (gpt(prompt, max_tokens=900, temperature=0.2, json_mode=True)
                or gemini(prompt, max_tokens=900, temperature=0.2, json_mode=True)
                or claude(prompt, max_tokens=900, json_mode=True)
                or text(prompt, json_mode=True, max_tokens=900, temperature=0.2, task="blog"))
        if resp:
            try:
                raw = _json.loads(resp)
            except Exception:
                # 폴백 모델이 JSON 앞뒤에 잡문/펜스를 붙이는 실패("Extra data" 등) 구제 —
                # 첫 '{'부터 균형 잡힌 첫 JSON 객체만 추출(2026-07-03 뉴스신호 유실 사고)
                raw, _ = _json.JSONDecoder().raw_decode(resp[resp.index("{"):])
            evaluated = isinstance(raw, dict)
            # 실제 영향(0이 아닌) 종목만 저장 — 0/근거없음은 '뉴스없음'으로 둠. 종목명도 함께.
            for code, v in (raw.items() if isinstance(raw, dict) else []):
                if isinstance(v, dict) and isinstance(v.get("score"), int) and v["score"] != 0:
                    code = str(code)
                    impact[code] = {"score": v["score"], "reason": v.get("reason", ""),
                                    "name": targets.get(code, code)}
    except Exception as exc:
        print(f"[market_desk] issue_impact 평가 실패: {exc}", file=sys.stderr)

    if not evaluated:
        # 평가 자체가 실패(모델 무응답·JSON 복구 불능) — 빈 값 덮어쓰기로 직전 뉴스신호를
        # 유실하지 않는다(가드레일 '비파괴' 원칙을 원본에도 적용). 직전값 없을 때만 그대로 진행.
        prev = research.load_issue_impact()
        if prev:
            print(f"[market_desk] issue_impact 평가 실패 — 직전 {len(prev)}종목 유지(빈 값 덮어쓰기 방지)")
            return

    # 밸류체인 확장: 강한 호재(+2) 종목의 공급망 수혜주를 도출해 후보에 추가
    _expand_value_chain(impact)
    research.save_issue_impact(impact)
    print(f"[market_desk] issue_impact 저장: {len(impact)}종목 (대상 {len(targets)})")


def _expand_value_chain(impact: dict, max_add: int = 6) -> None:
    """강한 호재(+2) 종목의 밸류체인/공급망 수혜 상장사를 LLM으로 도출해 impact에 추가(+1).
    LLM이 준 종목명은 네이버 검색으로 코드 검증 후, 실재 종목만 반영(허위 티커 방지)."""
    import json as _json

    strong = [(c, v) for c, v in list(impact.items()) if v.get("score", 0) >= 2]
    if not strong:
        return
    try:
        sys.path.insert(0, str(AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools"))
        from stock_search import resolve_symbol
    except Exception:
        return
    added: dict[str, dict] = {}
    for code, v in strong[:3]:
        nm = v.get("name", code)
        names = []
        try:
            resp = text(
                f"'{nm}'의 호재: {v.get('reason', '')}\n"
                "이 호재로 직접 수혜받는 한국 상장사(밸류체인·공급망)를 3개까지 골라라. 확실한 것만, 대형주 우선.\n"
                'JSON 객체만: {"beneficiaries": ["종목명", ...]}',
                json_mode=True, max_tokens=150, temperature=0.2, lm_first=False,
            )
            if resp:
                a, b = resp.find("{"), resp.rfind("}")
                obj = _json.loads(resp[a:b + 1]) if a >= 0 and b > a else {}
                names = obj.get("beneficiaries", []) if isinstance(obj, dict) else []
        except Exception:
            names = []
        for name in (names if isinstance(names, list) else []):
            hit = resolve_symbol(str(name))
            if not hit:
                continue
            rc, rn = hit
            if rc in impact or rc in added:
                continue
            added[rc] = {"score": 1, "reason": f"밸류체인 수혜({nm})", "name": rn}
            if len(added) >= max_add:
                break
        if len(added) >= max_add:
            break
    if added:
        impact.update(added)
        print(f"[market_desk] 밸류체인 수혜주 {len(added)}종목 추가")


def final_judgment() -> str:
    """헌장 [마켓데스크 최종 판단] — 매수제안·보유·개선안 신호를 종합해 단일 결론.
    충돌 조정: 국내 수급·거래대금 우선, 급등이라도 수급 약하면 추격 경고."""
    import json
    cache = growth._root() / "output" / "cache"

    def _load(name):
        try:
            return json.loads((cache / name).read_text(encoding="utf-8"))
        except Exception:
            return None

    proposals = (_load("somi_proposals.json") or {}).get("items", [])
    buys = [p for p in proposals if p.get("verdict") == "buy"]
    positions = _load("somi_positions.json") or {}
    pending = growth.list_proposals()

    if buys:
        conclusion, approval = "매수 후보", "예"
        top = max(buys, key=lambda x: x.get("score", 0))
        ev1 = f"매수 후보 {len(buys)}종목 (최고 {top.get('name')} {top.get('score')}점)"
        risk = "급등 후보의 추격 위험 — 거래대금·수급 약하면 진입 보류"
        choice = "매수 승인 여부 결정"
    elif positions:
        conclusion, approval = "보유", "아니오"
        ev1 = f"신규 매수 후보 없음 · 보유 {len(positions)}종목 관리 국면"
        risk = "보유분 손절선·수급 이탈 점검 필요"
        choice = "보유 유지 / 청산 후보 확인"
    else:
        conclusion, approval = "관망", "아니오"
        ev1 = "매수 후보·보유 모두 없음 — 현금 보유"
        risk = "신규 진입 신호 부족(국면·점수 미달)"
        choice = "추가 관망"

    lines = [
        "[마켓데스크 최종 판단]",
        f"- 결론: {conclusion}",
        "- 핵심 근거 3개:",
        f"  1. {ev1}",
        f"  2. 보유 포지션 {len(positions)}종목",
        "  3. 판단 우선순위: 국내 수급·거래대금 > 해외 조사(배경 변수)",
        f"- 가장 큰 리스크: {risk}",
        f"- 사용자에게 필요한 선택: {choice}",
        f"- 승인 필요 여부: {approval}",
    ]
    if pending:
        lines.append(f"- 대기 개선안: {len(pending)}건 (예원 수집·승인 대기)")
    return "\n".join(lines)


def build() -> dict:
    us = research.load_region("us")
    asia = research.load_region("asia")
    eu = research.load_region("eu")
    disclosures = asia.get("disclosures", []) or []
    fx = asia.get("fx", {}) or {}

    # 종목별 이슈 영향도 → issue_impact.json (소미가 소비).
    # 평가 대상 = 소미 매매 후보(거래대금 상위) ∪ 공시 종목 — 소미가 실제 보는 종목에 뉴스가 붙도록 맞춤.
    _build_issue_impact(disclosures, us, asia, eu)

    # 데이터 요약 (LLM 입력 + 본문)
    facts = []
    if fx.get("KRW"):
        facts.append(f"USD/KRW {fx['KRW']:.1f}")
    for label, reg in (("미국", us), ("아시아", asia), ("유럽", eu)):
        line = _idx_line(reg)
        if line:
            facts.append(f"{label} 지수: {line}")
        w = reg.get("web_issues")
        if w:
            facts.append(f"{label} 웹이슈: {w}")
    if disclosures:
        facts.append("watchlist 공시: " + "; ".join(f"{d['name']} {d['report']}" for d in disclosures[:8]))
    fg = research.fear_greed()
    if fg.get("score") is not None:
        facts.append(f"미국 공포탐욕지수 {fg['score']} ({fg.get('rating', '')})")
    news = asia.get("news") or []
    if news:
        facts.append("증권 뉴스: " + " | ".join(news[:5]))
    facts_str = "\n".join(facts) if facts else "수집된 데이터가 적음"

    # LLM 종합 코멘트 (실패 시 폴백)
    comment = ""
    try:
        prompt = (
            "다음은 오늘의 시장 데이터다. 한국 증시 관점에서 주목할 점을 3줄 이내로 "
            "간결히 요약하라. 과장 없이 사실 위주로.\n\n" + facts_str
        )
        comment = (text(prompt, max_tokens=300, temperature=0.4, task="blog") or "").strip()
    except Exception:
        comment = ""

    # 향후 전망 + 비트코인 전망 (웹검색)
    outlook = research.web_brief(
        "다음 두 가지를 각각 2~3줄로 간결히 정리하라. 근거 위주, 단정적 표현은 피하라: "
        "(1) 향후 1~2주 한국·미국 증시 단기 전망 (2) 비트코인 가격 동향과 단기 전망.",
        max_tokens=900,
    )

    # 거시 · 지정학 · 트렌드 (시장을 움직이는 큰 줄기)
    geo_macro = research.web_brief(
        "다음을 각각 2줄 이내로 간결히. 근거·출처 위주: "
        "(1) 지정학·전쟁 상황(중동·이란·러시아-우크라이나 등 시장 영향) "
        "(2) 거시 경제 흐름(미국·한국 금리·인플레·유가·달러 추세) "
        "(3) 핵심 트렌드 이슈(AI·반도체 등 시장 주도 테마)",
        max_tokens=1100,
    )

    # 신기술·혁신 트렌드 핫이슈 (AI·우주에 한정하지 말고 폭넓게, 관련 상장사까지)
    hot_issues = research.web_brief(
        "오늘 가장 화제인 '신기술·혁신 트렌드' 이슈를 폭넓게 4~6개 골라 각각 1~2줄로 정리하라. "
        "특정 주제 2개에 한정하지 말 것. 예: AI/반도체, 우주·SpaceX·위성, 로봇·휴머노이드, 양자컴퓨팅, "
        "자율주행·전기차, 바이오·헬스테크, 에너지·원전·배터리, AR/VR 등 첨단기술 전반. "
        "각 이슈에 관련된 한국/미국 상장사(수혜주)가 있으면 함께. 근거·출처 위주, 단정 표현 회피.",
        max_tokens=1000,
    )

    now = datetime.now().strftime("%Y-%m-%d %H:%M")
    md_lines = [f"# 📋 시장 종합 브리프 — {now}", ""]
    if fx.get("KRW"):
        krw = f"USD/KRW {fx['KRW']:.1f}"
        jpy = f" · USD/JPY {fx['JPY']:.1f}" if fx.get("JPY") else ""
        md_lines += ["## 💱 환율", krw + jpy, ""]
    md_lines.append("## 📈 지수")
    for label, reg in (("미국", us), ("아시아", asia), ("유럽", eu)):
        line = _idx_line(reg)
        md_lines.append(f"- {label}: {line}" if line else f"- {label}: (조회 실패)")
    md_lines.append("")
    md_lines.append(f"## 📑 watchlist 공시 ({len(disclosures)}건)")
    if disclosures:
        for d in disclosures[:15]:
            md_lines.append(f"- {d['name']}({d['code']}) {d['report']} [{d['date']}]")
    else:
        md_lines.append("- 신규 공시 없음")
    md_lines.append("")
    web_sections = []
    for label, reg in (("🇺🇸 미국", us), ("🌏 아시아", asia), ("🇪🇺 유럽", eu)):
        w = reg.get("web_issues")
        if w:
            web_sections.append(f"**{label}**\n{w}")
    if web_sections:
        md_lines.append("## 🔎 지역 웹이슈")
        md_lines += web_sections
        md_lines.append("")
    if fg.get("score") is not None:
        md_lines += ["## 😨 시장 심리", f"미국 공포탐욕지수 {fg['score']} ({fg.get('rating', '')})", ""]
    if news:
        md_lines.append("## 📰 증권 뉴스")
        md_lines += [f"- {n}" for n in news[:6]]
        md_lines.append("")
    if comment:
        md_lines += ["## 🧭 데스크 코멘트", comment, ""]
    if geo_macro:
        md_lines += ["## 🌐 거시 · 지정학 · 트렌드", geo_macro, ""]
    if hot_issues:
        md_lines += ["## 🚀 신기술 핫이슈", hot_issues, ""]
    if outlook:
        md_lines += ["## 📈 전망 · 비트코인", outlook, ""]

    md = "\n".join(md_lines)
    research.save_market_brief(md, {
        "fx": fx,
        "indices": {"us": us.get("indices"), "asia": asia.get("indices"), "eu": eu.get("indices")},
        "disclosures": disclosures,
        "comment": comment,
    })

    # 노션에 간결 기록 (제목 + 핵심 불릿 몇 줄)
    bullets = []
    if fx.get("KRW"):
        bullets.append(f"💱 USD/KRW {fx['KRW']:.1f}")
    for label, reg in (("🇺🇸 미국", us), ("🌏 한국", asia), ("🇪🇺 유럽", eu)):
        w = reg.get("web_issues")
        if w:
            bullets.append(f"{label} {_short(w, 110)}")
    if disclosures:
        bullets.append("📑 공시 " + str(len(disclosures)) + "건: "
                       + ", ".join(f"{d['name']} {d['report']}" for d in disclosures[:3]))
    news = asia.get("news") or []
    if news:
        bullets.append("📰 뉴스: " + " · ".join(news[:2]))
    if comment:
        bullets.append("🧭 " + _short(comment, 160))
    if geo_macro:
        bullets.append("🌐 거시·지정학: " + _short(geo_macro, 180))
    if outlook:
        bullets.append("📈 전망·BTC: " + _short(outlook, 170))
    research.notion_page(f"📊 시장 브리프 {now}", bullets)

    md += "\n\n" + final_judgment()  # 헌장 [마켓데스크 최종 판단] 통합
    return {"md": md}


def main() -> None:
    ap = argparse.ArgumentParser(description="마켓데스크 시장 종합")
    ap.add_argument("--send", action="store_true")
    ap.add_argument("--print", action="store_true")
    ap.add_argument("--daemon", action="store_true", help="정기 실행 데몬 모드 (매 1시간)")
    args = ap.parse_args()

    if args.daemon:
        from _shared.utils import due_slot
        slots = os.getenv("MARKETDESK_SLOTS", "07:50,15:20").split(",")
        state = PROJECT_ROOT / "output" / "cache" / "marketdesk_slots.json"
        with ProcessLock("market_desk"):
            print(f"[{datetime.now()}] 마켓데스크 데몬 시작 (정해진 시각만: {','.join(slots)})")
            while True:
                if due_slot(slots, state):
                    try:
                        publish_report("마켓데스크 시장 종합 브리프", build()["md"])
                        growth.record("marketdesk", role="시장 종합 최종판단", data="행크·유나·레온 통합",
                                      judgment="단일 시장판단", result="전송", good="정해진 시각 보고",
                                      bad="중복·충돌 정리 로직 정식화 여지",
                                      scores={"fit": 21, "evidence": 19, "efficiency": 18, "risk": 18, "brevity": 8})
                    except Exception as e:
                        send(f"⚠️ 마켓데스크 오류: {e}")
                time.sleep(300)
        return

    md = build()["md"]
    if args.print or not args.send:
        print(md)
    if args.send:
        publish_report("마켓데스크 시장 종합 브리프", md)


if __name__ == "__main__":
    main()
