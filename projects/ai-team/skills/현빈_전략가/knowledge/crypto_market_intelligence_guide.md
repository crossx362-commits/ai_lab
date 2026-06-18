# 암호화폐 시장 정보 수집 가이드

> **작성일**: 2026-06-16  
> **담당**: 현빈 (비즈니스 전략가)  
> **목적**: 데이브(자동매매봇)에게 실시간 시장 정보 제공

---

## 1. 개요

데이브가 정확한 매매 판단을 내리기 위해서는 다음과 같은 **외부 정보**가 필요합니다:

| 정보 유형 | 목적 | 업데이트 주기 |
|----------|------|--------------|
| 연준 이벤트 일정 | 고위험 관망 구간 판단 | 일 1회 |
| 공포탐욕지수 | 시장 심리 → 역발상 매수/매도 신호 | 5분마다 |
| 김치 프리미엄 | 국내 투기 과열/공포 감지 | 5분마다 |
| 고래 움직임 | 대형 자금 이동 추적 | 실시간 |
| 청산 맵 | 숏 스퀴즈 타점 포착 | 5분마다 |
| 주요 뉴스 | 급격한 시장 변동 원인 파악 | 5분마다 |

---

## 2. 수집 방법

### 2.1. 공포탐욕지수 (Fear & Greed Index)

**API**: Alternative.me (무료, 키 불필요)

```python
url = "https://api.alternative.me/fng/?limit=1"
response = requests.get(url)
data = response.json()
value = int(data["data"][0]["value"])
```

**해석 기준 (데이브 SKILL 기준)**:
- `value ≤ 20`: 극단적 공포 → **역발상 매수 준비**
  - 실증: 2019~2025년 이 구간 매수 시 90일 중간 수익률 +42%
- `value ≥ 75`: 극단적 탐욕 → **포모 경보, 신규 진입 금지**
- `20 < value < 75`: 중립 구간

---

### 2.2. 김치 프리미엄

**계산 방법**:
```
김치 프리미엄(%) = (업비트_가격 - 바이낸스_가격_KRW) / 바이낸스_가격_KRW × 100
```

**API**:
- 업비트: `https://api.upbit.com/v1/ticker?markets=KRW-BTC`
- 바이낸스: `https://api.binance.com/api/v3/ticker/price?symbol=BTCUSDT`
- 환율: 고정 1,300원 또는 실시간 API

**해석 기준 (데이브 SKILL 기준)**:
- `≥ +5%`: 국내 투기 과열 = **인간 지표 극대화** = 고점 경보 → 즉시 자산 대피
- `≤ -3%`: 국내 공포 극대화 = **역발상 저점 신호**
  - 실증: 72% 확률로 로컬 바닥 일치, 이후 45일 내 평균 +28% 반등

**동시 발생 최강 신호**:
```
공포탐욕지수 ≤ 20 + 김치 프리미엄 ≤ -3% + 기술적 합치 조건 충족
→ 최우선 롱 진입
```

---

### 2.3. 연준(Fed) 이벤트 일정

**주요 이벤트**:
- FOMC (금리 결정): 연 8회
- CPI (소비자물가지수): 매월 10~13일
- PPI (생산자물가지수): 매월 중순
- NFP (고용지표): 매월 첫째 주 금요일

**데이브 SKILL 규칙**:
- 발표 **전후 1~2시간**: 무조건 관망(HOLD) 강제
- **실증 데이터**: 2025년 FOMC 8회 중 비트코인 상승은 단 1회
- **"Sell the News"** 패턴 → 발표 직후 되돌림 빈번

**2026년 6월 고위험 구간**:
```
CPI(6월 10일) → FOMC(6월 17일)
7일 간격 연속 발표 = 이 7일 전체 구간을 고위험 관망 구간으로 지정
```

**수집 방법**:
- 자동: Fed Calendar API 또는 웹 스크래핑
- 수동: 매월 초 일정 업데이트

---

### 2.4. 고래 알림 (Whale Alert)

**API**: Whale Alert (유료, API 키 필요)
- 웹사이트: https://whale-alert.io/
- 문서: https://docs.whale-alert.io/

**추적 대상**:
- 1,000 BTC 이상 대형 거래소 입출금
- 고래 지갑 → 거래소: 매도 압력 (하락 신호)
- 거래소 → 고래 지갑: 매집 신호 (상승 신호)

**상태**: 구현 예정 (API 키 발급 필요)

---

### 2.5. 청산 맵 (Liquidation Map)

**API**: Coinglass (일부 무료, 상세 데이터는 유료)
- 웹사이트: https://www.coinglass.com/
- API: `https://open-api.coinglass.com/public/v2/liquidation`

**활용법**:
- 현재가 상방에 대형 숏 청산 구간 집중 → **숏 스퀴즈 가능성** → 강력 롱 트리거
- 현재가 하방에 대형 롱 청산 구간 집중 → 하락 압력 경고

**상태**: 구현 예정 (API 키 확인 필요)

---

### 2.6. 암호화폐 뉴스

**API**: CryptoPanic (무료)
- 엔드포인트: `https://cryptopanic.com/api/v1/posts/?auth_token=free&currencies=BTC,ETH`

**필터링 기준**:
- BTC/ETH 관련만
- 최근 1시간 이내 뉴스만
- 중요도 높은 뉴스 우선

---

## 3. 데이터 저장 구조

**파일 경로**: `reports/research/crypto_market_intel.json`

**JSON 구조**:
```json
{
  "timestamp": "2026-06-16T09:30:00",
  "fed_events": {
    "next_fomc": "2026-06-17",
    "next_cpi": "2026-07-10",
    "current_status": "FOMC 발표 1일 전 - 고위험 관망 구간",
    "risk_level": "HIGH"
  },
  "fear_greed_index": {
    "value": 35,
    "classification": "Fear",
    "signal": "중립 구간",
    "action": "NEUTRAL"
  },
  "kimchi_premium": {
    "upbit_price": 130000000,
    "binance_price_krw": 129500000,
    "premium_pct": 0.39,
    "signal": "정상 범위",
    "action": "NEUTRAL"
  },
  "whale_alerts": {
    "status": "not_implemented"
  },
  "liquidation_map": {
    "status": "not_implemented"
  },
  "crypto_news": {
    "status": "success",
    "count": 5,
    "news": [...]
  }
}
```

---

## 4. 데이브 연동 방법

### 4.1. 데이브가 정보를 읽는 방식

```python
# upbit_auto_trader.py 또는 upbit_analyzer.py에 추가

import json

def load_market_intel():
    """현빈이 수집한 시장 정보 로드"""
    workspace_root = os.path.abspath(os.path.join(AI_TEAM_ROOT, "..", ".."))
    intel_path = os.path.join(workspace_root, "reports", "research", "crypto_market_intel.json")
    
    if not os.path.exists(intel_path):
        return None
    
    with open(intel_path, "r", encoding="utf-8") as f:
        return json.load(f)

def check_fed_event_risk():
    """연준 이벤트 고위험 구간 체크"""
    intel = load_market_intel()
    
    if intel and intel.get("fed_events", {}).get("risk_level") == "HIGH":
        return True  # 관망 강제
    
    return False

def get_sentiment_signals():
    """공포탐욕 + 김치프리미엄 신호 종합"""
    intel = load_market_intel()
    
    if not intel:
        return "NEUTRAL"
    
    fg_action = intel.get("fear_greed_index", {}).get("action", "NEUTRAL")
    kp_action = intel.get("kimchi_premium", {}).get("action", "NEUTRAL")
    
    # 둘 다 BUY_SIGNAL이면 최강 매수 신호
    if fg_action == "BUY_SIGNAL" and kp_action == "BUY_SIGNAL":
        return "STRONG_BUY"
    
    # 둘 다 SELL_SIGNAL이면 위험 신호
    if fg_action == "SELL_SIGNAL" or kp_action == "SELL_SIGNAL":
        return "STRONG_SELL"
    
    return "NEUTRAL"
```

### 4.2. 진입 로직에 통합

```python
# run_auto_trade_cycle() 함수 수정

def run_auto_trade_cycle(sim_mode=False):
    # 1. 연준 이벤트 체크
    if check_fed_event_risk():
        print("[AutoTrader] 연준 고위험 구간 - 신규 진입 금지")
        return
    
    # 2. 시장 심리 신호 확인
    sentiment = get_sentiment_signals()
    
    # 3. 퀀트 스코어 계산
    best = calculate_confluence_score(ticker)
    
    # 4. 복합 판단
    if sentiment == "STRONG_BUY" and best["score"] >= 2:
        # 공포탐욕 + 김치프리미엄 모두 매수 신호 → 진입 점수 낮춰도 OK
        execute_buy(...)
    elif sentiment == "STRONG_SELL":
        # 위험 신호 → 기존 포지션 부분 청산
        execute_sell(...)
```

---

## 5. 실행 방법

### 5.1. 단발 실행 (테스트용)

```bash
cd d:\ai_lab\projects\ai-team\skills\현빈_전략가\tools
python crypto_market_intelligence.py
```

### 5.2. 데몬 모드 (상시 실행)

```bash
python crypto_market_intelligence.py --daemon
```

- 5분마다 자동 수집
- 텔레그램 알림 자동 발송
- `crypto_market_intel.json` 자동 업데이트

### 5.3. 백그라운드 실행 (PowerShell)

```powershell
cd "d:\ai_lab\projects\ai-team\skills\현빈_전략가\tools"
Start-Process python -ArgumentList "crypto_market_intelligence.py --daemon" -NoNewWindow
```

---

## 6. 개선 계획

### Phase 1 (현재)
- ✅ 공포탐욕지수 수집
- ✅ 김치 프리미엄 계산
- ✅ 연준 일정 (수동 입력)
- ✅ 뉴스 수집

### Phase 2 (단기)
- ⏳ 연준 일정 자동 업데이트 (Fed Calendar API)
- ⏳ 실시간 환율 API 연동
- ⏳ Whale Alert API 연동
- ⏳ Coinglass 청산 맵 연동

### Phase 3 (중기)
- 📋 온체인 지표 (거래소 유입/유출)
- 📋 소셜 미디어 센티먼트 분석
- 📋 주요 거래소 호가창 실시간 모니터링

---

## 7. 트러블슈팅

### 7.1. API 오류

**증상**: "No data" 또는 HTTP 오류

**해결**:
1. 인터넷 연결 확인
2. API 엔드포인트 변경 여부 확인
3. API 키 만료 여부 확인 (유료 API의 경우)

### 7.2. JSON 파싱 오류

**증상**: `JSONDecodeError`

**해결**:
```python
try:
    data = response.json()
except json.JSONDecodeError:
    print(f"API 응답 원문: {response.text}")
```

### 7.3. 데이브가 정보를 읽지 못함

**원인**: 파일 경로 불일치

**해결**:
```python
# 절대 경로 확인
print(f"Intel path: {intel_path}")
print(f"File exists: {os.path.exists(intel_path)}")
```

---

## 8. 체크리스트

**현빈 실행 전**:
- [ ] `reports/research/` 디렉토리 존재 확인
- [ ] 인터넷 연결 확인
- [ ] 텔레그램 봇 토큰 유효성 확인

**데이브 연동 전**:
- [ ] `crypto_market_intel.json` 파일 생성 확인
- [ ] JSON 구조 유효성 검증
- [ ] 데이브 코드에 `load_market_intel()` 함수 추가

**운영 중**:
- [ ] 5분마다 JSON 업데이트 확인
- [ ] 텔레그램 알림 정상 수신 확인
- [ ] 데이브 로그에서 정보 활용 여부 확인

---

**마지막 업데이트**: 2026-06-16  
**다음 검토**: 연준 이벤트 자동 수집 구현 후
