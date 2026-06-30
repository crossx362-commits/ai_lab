# 장전 브리핑 — 실행 워크플로 (도구 명령)

repo 루트(`d:/ai_lab`)에서 실행. 상위 도구표는 [`../../references/TOOLCHAIN.md`](../../references/TOOLCHAIN.md).

## 순서

### 1. 시장 레짐
```python
from market_regime import stable_regime, regime_label
r = stable_regime()           # 공격/중립/방어 안정화 레짐
```
→ 브리핑 "오늘의 한 줄"에 레짐 반영(방어면 후보·비중 축소).

### 2. 관심종목 수급 요약
```bash
python projects/ai-team/skills/소미_분석가/tools/somi_kis_reporter.py --print
```
또는 `daily_summary()` / `send_watchlist_reports("장전")`. 대상은
`output/cache/somi_watchlist.json`(고정 종목 없음).

### 3. 보유 포지션 액션
```bash
python projects/ai-team/skills/소미_분석가/tools/somi_position_monitor.py --print 2>/dev/null || \
python -c "from somi_position_monitor import run; print(run(do_send=False))"
```
→ 익절/손절/트레일링/시간초과 신호를 "보유 포지션 액션"에 요약.
모의면 자동청산 사실, 실거래면 승인 대상으로 표기([[paper-mode-autotrade]]).

### 4. 야간·해외·뉴스
`market_trend_alert.py` 최근 알림 + `_shared/llm.py`로 미국장·환율·반도체 한 줄 요약.

### 5. 전송
```python
from _shared.notify import send
send(brief_text)              # 텔레그램 압축본 (1페이지)
```
길면 풀버전은 영숙 `report-writer`로 노션([[report-to-notion]]).

## 체크리스트
- [ ] 레짐 한 줄 포함
- [ ] 관심종목 표(소미점수·수급·등락)
- [ ] 보유 포지션 액션 (모의/실거래 구분)
- [ ] "오늘의 한 줄" = PM이 알아야 할 단 하나
- [ ] 무재료면 "무재료, 포지션 유지" 명시
- [ ] 모든 수치 KIS 실데이터, 추정 `[추정]`
