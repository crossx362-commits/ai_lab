# 종목 발굴 — 채점·기대값 (도구 연동)

상위 도구표: [`../../references/TOOLCHAIN.md`](../../references/TOOLCHAIN.md).

## 1. 후보 풀
```python
from somi_screener import get_candidates, screen, run
# 거래대금 상위 candidate_limit개 → 소미 채점 상위 top_n개
report = run(top_n=5, candidate_limit=20, do_send=False)
```
CLI: `python ... /somi_screener.py --top 5 --candidates 20`.

## 2. 점수 구성요소 (short_covering_analyzer)
`screen()` 내부 채점은 다음 부분점수를 합산한다(실측 함수):
- `entry_score(feat)` — 진입 적합도(수급·가격위치)
- `risk_score(feat)` — 리스크(변동성·관리종목 등)
- `rr_score(feat)` — 손익비(R/R), 양호 여부 플래그
- `data_quality_score(dq, vwap, buy_pressure)` — 데이터 신뢰도
- `grade_of(score)` — 등급화

## 3. 기대값(EV) 정렬 ([[somi-expected-value-engine]])
```
EV = (상승확률 × 목표수익률) − (하락확률 × 손절폭)
```
- 목표/손절은 `somi_trade_advisor._levels()`/`analyze_candidate()`가 산출.
- 신호화·예산 배분은 `somi_signal_engine.scan(budget, top)` (기본 예산 `SOMI_BUDGET_PER_TRADE`).
- 약세장(`_is_bear()`)이면 후보 수 2배로 넓혀 보수적으로 선별.

## 4. 매수 게이트
`somi_trade_advisor._passes_buy_gate(c)` 통과 + `_decide(c)`가 "매수/관망/회피" 결정.
숏리스트엔 **트리거 가격·무효화 조건**을 반드시 병기.

## 5. 출력
- 텔레그램 요약(`_shared.notify.send`), 상세는 노션.
- 미등록 종목: `watchlist_manager.add_watch(code, name)` 등록 제안.

## 주의
- 0건이면 "조건 충족 없음 — 관망"이 정답.
- 코스닥 소형·테마주 변동성/유동성 경고 병기.
- 실매수는 모의=자동/실거래=승인([[paper-mode-autotrade]]).
