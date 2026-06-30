# 투자논리 추적 — 기대값·청산 (도구 연동)

상위 도구표: [`../../references/TOOLCHAIN.md`](../../references/TOOLCHAIN.md).
기대값 엔진 배경: [[somi-expected-value-engine]].

## 1. 포지션·진입값 로드
```python
from somi_trade_advisor import load_positions
pos = load_positions()        # 종목별 entry/stop/target/qty 등 진입 시 기록값
```
진입 시 `record_position(symbol, name, entry, stop, target, ...)`로 저장된
**손절(stop)·목표(target)·기대값**을 테제의 기준선으로 쓴다.

## 2. 현재 상태·청산 신호
```python
from somi_position_monitor import check_positions, run
signals = check_positions()   # stop / trailing / target / early_exit / tp1_partial / 시간초과
```
- 트리거 종류(실측): `stop`(손절), `trailing`(트레일링), `target`(목표),
  `early_exit`(조기청산), `tp1_partial`(1차 분할익절), `MAX_HOLD_DAYS` 초과.
- **모의(_is_paper)**: `_paper_sell()`로 자동 체결.
- **실거래**: 동일 신호를 수동 승인요청으로 전환([[paper-mode-autotrade]]).

## 3. 기대값 재평가
현재가 기준 EV 재계산 → 진입 EV 대비 개선/악화. 악화 + 무효화 조건 충족이면
확신도와 무관하게 청산 후보. 손절은 규칙(stop 트리거)으로 집행.

## 4. 스코어카드 갱신
근거 기둥별 상태(외인 순매수 지속·영업이익 턴어라운드·숏커버링 등)를
`somi_kis_reporter`/`short_covering_analyzer` 최신 수급으로 갱신.

## 5. 출력
- 텔레그램: 종목별 "논리 유효/손상 + 액션(홀딩·트림·손절·익절)" 한 줄.
- 노션: 스코어카드·업데이트 로그 전체.

## 원칙
- 테제는 반증 가능해야 한다. 반증 증거를 확증만큼 엄격히 추적.
- 손절은 감정 아닌 진입 시 정한 트리거. 분기 1회 강제 점검.
