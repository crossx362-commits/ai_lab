# 소미 도구 연동 맵 (Plugin ↔ Python Toolchain)

스킬(판단 방법론)이 호출/참조하는 **실제 소미 Python 도구와 CLI**. 경로 기준:
`projects/ai-team/skills/소미_분석가/tools/`. 모든 명령은 repo 루트(`d:/ai_lab`)에서 실행.

## 도구별 진입점 (실측)

| 도구 | 주요 CLI / 함수 | 역할 |
|------|----------------|------|
| `somi_kis_reporter.py` | `--send` `--print` `--daemon` · `make_report()` `daily_summary()` `send_watchlist_reports()` | 관심종목 수급·점수 정기 보고 (KIS API) |
| `somi_screener.py` | `--top N` `--candidates N` `--send` `--daemon --times HH:MM,HH:MM` · `run()` `screen()` `get_candidates()` | 거래대금 상위 후보 채점·발굴 |
| `somi_trade_advisor.py` | `make_proposals()` `analyze_candidate()` `_passes_buy_gate()` `_decide()` | 매수구간·제안 판단·기대값 기반 매매 신호 실행(자동매수 단일 실행자) |
| `somi_position_monitor.py` | `--send` `--daemon` · `check_positions()` `run()` | 보유 포지션 익절/손절/트레일링/시간초과 점검 |
| `market_regime.py` | `market_regime()` `stable_regime()` `regime_label()` | KOSPI 프록시 HMM 레짐(공격/중립/방어) |
| `short_covering_analyzer.py` | `--file` `--text` `--output` · `entry_score()` `risk_score()` `rr_score()` `flow_short_analysis()` | 대차/공매도·수급 점수 |
| `watchlist_manager.py` | `add\|remove\|list --symbol --name` · `load_watchlist()` | 관심종목 등록/조회 (`output/cache/somi_watchlist.json`) |
| `market_trend_alert.py` | (데몬) | 시장 추세/뉴스 알림 |
| `kis_trader.py` | 명시 주문만 체결 (자동매매 아님) | KIS 주문 실행 |

## 거래 모드 판정 (중요 — [[paper-mode-autotrade]])

우선순위: `output/cache/trade_mode.json`(텔레그램 토글) → 없으면 `KIS_PAPER` 환경변수.
- **모의(paper)**: `somi_position_monitor.py`가 손절/목표/트레일링/시간초과 시 **자동 매도 체결**.
- **실거래(live)**: 동일 트리거에서 **수동 승인요청**(텔레그램 인라인) 후 사람이 체결.
- 1종목 예산: `SOMI_BUDGET_PER_TRADE`(기본 1,000,000원). 약세장이면 후보 수 2배(`SOMI_SIGNAL_CANDIDATES_BEAR`).

## 스킬 → 도구 매핑 요약

- `morning-brief` → reporter(daily_summary) + market_regime + position_monitor + price_monitor 알림
- `idea-screen` → screener.run() + signal_engine.scan() + short_covering_analyzer
- `sector-overview` → reporter(수급) + screener(후보)
- `earnings-analysis` → reporter(발표후 수급) + short_covering_analyzer
- `thesis-tracker` → position_monitor + trade_advisor(진입 시 기록된 EV/손절/목표)

## 원칙
- 스킬은 **판단 레이어**, 데이터·체결은 위 도구가 담당. 스킬이 도구 코드를 수정하지 않는다.
- 모든 수치는 KIS 실시간 — 하드코딩 금지([[no-hardcoding]]). 추정은 `[추정]`.
