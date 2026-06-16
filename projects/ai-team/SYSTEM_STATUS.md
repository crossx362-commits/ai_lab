# AI 트레이딩 팀 시스템 현황

> 마지막 업데이트: 2026-06-16

## 🤖 실행 중인 에이전트

### 트레이딩 봇 (실거래 모드)

| 에이전트 | 역할 | 주기 | 스크립트 경로 | 상태 |
|---------|------|------|--------------|------|
| 현빈 | 시장 정보 수집 | 5분 | `skills/현빈_전략가/tools/crypto_market_intelligence.py` | ✅ 실행 중 |
| 데이브 | 보수적 매매 | 30초 | `skills/데이브_주식/tools/upbit_auto_trader.py` | ✅ 실행 중 |
| 레오 | 공격적 단타 | 10초 | `skills/레오_트레이더/tools/leo_aggressive_trader.py` | ✅ 실행 중 |
| 프로세스 모니터 | 프로세스 감시 | - | `monitor_processes.py` | ✅ 실행 중 |

### 텔레그램 봇

| 봇 | 역할 | AI 모델 | 스크립트 경로 | 상태 |
|----|------|---------|--------------|------|
| 영숙 | 비서/명령 수신 | Gemini 2.5 Flash | `skills/영숙_비서/tools/telegram_receiver.py` | ✅ 실행 중 |

---

## 📊 에이전트 상세 정보

### 1. 현빈 (시장 정보 수집 에이전트)

**역할**: 데이브와 레오에게 매크로/심리/온체인 지표 제공

**수집 데이터**:
- **연준 이벤트**: FOMC, CPI, NFP 일정 및 리스크 레벨
- **공포탐욕지수**: Alternative.me API 활용
  - 극공포 (≤20): 역발상 매수 신호
  - 극탐욕 (≥75): 신규 진입 금지
- **김치 프리미엄**: 업비트 vs 바이낸스 가격 차이
  - ≥5%: 국내 투기 과열, 자산 대피
  - ≤-3%: 국내 공포 극대화, 저점 신호
- **암호화폐 뉴스**: CryptoPanic API (최근 5개)
- **청산 맵**: Coinglass API (구현 예정)

**출력 파일**: `reports/research/crypto_market_intel.json`

**실행 주기**: 5분마다 자동 갱신

---

### 2. 데이브 (보수적 매매 봇)

**전략**: 퀀트 3점 이상 + LLM 검증 → 신중한 진입

**감시 코인 (14개)**:
```
SOL, XRP, DOGE, NEAR, SUI, SEI, STX, HBAR, 
ADA, AVAX, LINK, PEPE, BTC, ETH
```

**퀀트 분석 지표** (총 10개):

1. **EMA200 대추세** (+4점): 상승 국면 확인
2. **Supertrend** (+3점): 추세 방향
3. **StochRSI** (+3점): 과매도 골든크로스 최우선
4. **Heikin Ashi** (+3점): 아래꼬리 없는 장대양봉
5. **Volume Spike** (+2점): 거래량 급증
6. **OBV 다이버전스** (+2점): 세력 매집 신호
7. **CVD 다이버전스** (+2점): 고래 참여 여부
8. **세력 매집 패턴** (+2점): 바닥 매집, 손털기
9. **워시트레이딩 패널티** (-3점): 통정매매 의심
10. **4시간봉 모멘텀** (+2점): 단기 상승 신호

**진입 조건**:
- 퀀트 스코어 ≥3.0점
- LLM 최종 검증 통과 (5분 쿨다운)
- 현빈 정보 참조 (연준 이벤트 HIGH → 진입 금지)

**실행 주기**: 30초마다 전체 코인 스캔

**LLM 검증**:
- GPT-4o mini 사용
- 퀀트 데이터 + 차트 분석
- 최종 결정: BUY / SELL / HOLD

---

### 3. 레오 (공격적 단타 봇)

**전략**: 고변동성 알트코인 전문, 빠른 수익 실현

**감시 코인 (7개 - 고변동성 알트)**:
```
DOGE (밈코인 대장)
PEPE (밈코인 급등주)
NEAR (레이어1 고변동)
SUI (신규 레이어1)
SEI (신규 레이어1)
HBAR (엔터프라이즈)
STX (비트코인 L2)
```

**퀀트 분석 지표** (단순화):

1. **RSI 과매도** (+3점): RSI < 30
2. **Volume Spike** (+2점): 거래량 1.5배 이상
3. **1시간 모멘텀** (+2점): 1h 상승률 > 1%
4. **김치 프리미엄** (+1점): ≥3% (국내 관심)
5. **공포탐욕지수** (+2점/-2점): 극단 구간
6. **연준 이벤트** (+1점): HIGH 위험 → 변동성 기회

**진입 조건**:
- 퀀트 스코어 ≥2.0점 (데이브보다 낮음)
- 현빈 정보 적극 활용
- 데이브 보유 코인 제외 (충돌 방지)

**위험 관리 (자동 중단 조건)**:
- 연속 손절 3회 → 30분 휴식
- 일일 손실 -5% 도달 → 거래 중단
- 시간당 5회 거래 → 과열 방지
- 손실 후 30분 쿨다운

**실행 주기**: 10초마다 스캔 (데이브보다 3배 빠름)

---

### 4. 영숙 (텔레그램 비서 봇)

**역할**: 사용자 명령 처리 및 에이전트 상태 보고

**AI 모델**: Gemini 2.5 Flash (Fallback: GPT-4o mini → Ollama)

**주요 기능**:
- 에이전트 상태 조회 (`get_agent_status`)
- 일정 조회 (`list_calendar`)
- 스크립트 실행 (`dispatch`)
- 간결한 2줄 답변 (상태 보고 제외)

**도구 호출 규칙**:
- 일반 질문 → 도구 없이 즉시 답변
- 현황 보고 → 모든 에이전트 상세 출력
- 스크립트 실행 → dispatch 도구 호출

**API 재시도 로직**:
- 429 에러 시 5초 대기 후 자동 재시도 (최대 5회)
- Gemini Flash 실패 → Gemini Pro 전환
- 모든 Gemini 실패 → Ollama 로컬 LLM 폴백

## 📁 주요 스크립트

### 시작 스크립트

```
scripts/start_trading_team.py --live    # 트레이딩 팀 전체 시작
scripts/start_youngsuk_bot.cmd          # 텔레그램 봇 시작
```

### 데몬 래퍼

```
scripts/run_youngsuk_daemon.py          # 영숙 봇 데몬 래퍼 (로그 리다이렉션)
scripts/run_trader_daemon.py            # 트레이더 데몬 래퍼
```

## 🔧 환경 설정

### API 키 (.env)

```bash
# Gemini AI
GEMINI_API_KEY=AQ.Ab8RN6JdUpq-***************************

# Telegram
TELEGRAM_BOT_TOKEN=8615052743:AAF***************************
TELEGRAM_CHAT_ID=689****491

# Upbit (실거래)
UPBIT_ACCESS_KEY=EsA0SAod2Ssb8NCG*********************
UPBIT_SECRET_KEY=RSb5l24vNkICpxut0CKc***************

# Fallback AI APIs
ANTHROPIC_API_KEY=sk-ant-api03-*********************
OPENAI_API_KEY=sk-proj-*********************
```

### Python 인터프리터

**영숙 봇**: 시스템 Python 3.12 사용
```
C:\Program Files\WindowsApps\PythonSoftwareFoundation.Python.3.12_3.12.2800.0_x64__qbz5n2kfra8p0\python3.12.exe
```

**트레이딩 봇**: Windows Store Python 사용
```
C:\Users\User\AppData\Local\Microsoft\WindowsApps\python.exe
```

## 🏗️ 시스템 아키텍처

```
┌─────────────────────────────────────────────────────┐
│  start_trading_team.py --live                       │
│  (PID: 3616, 실행 시간: 140분+)                      │
└─────────────────┬───────────────────────────────────┘
                  │
        ┌─────────┼─────────┬─────────┐
        │         │         │         │
        ▼         ▼         ▼         ▼
    ┌─────┐  ┌──────┐  ┌─────┐  ┌─────────┐
    │현빈 │  │데이브│  │레오 │  │모니터   │
    │정보 │  │보수  │  │공격 │  │프로세스 │
    │수집 │  │매매  │  │단타 │  │감시     │
    └─────┘  └──────┘  └─────┘  └─────────┘
       │         │         │
       └─────────┴─────────┴──> 업비트 API
                  │
                  └──> 텔레그램 알림

┌─────────────────────────────────────────────────────┐
│  start_youngsuk_bot.cmd                             │
│  (영숙 텔레그램 봇)                                   │
└─────────────────┬───────────────────────────────────┘
                  │
        ┌─────────┴─────────┐
        ▼                   ▼
    ┌─────────┐        ┌──────────┐
    │ Gemini  │        │Telegram  │
    │ 2.5     │        │API       │
    │ Flash   │        │          │
    └─────────┘        └──────────┘
```

## 📊 협업 구조

1. **현빈** → 시장 정보 수집 (5분마다)
   - 연준 일정 (FOMC, CPI)
   - 공포탐욕지수
   - 김치프리미엄
   - 결과 저장: `reports/research/crypto_market_intel.json`

2. **데이브** → 현빈 정보 참조하여 보수적 매매 (30초마다)
   - 퀀트 3점 이상 + LLM 검증
   - 실거래 모드

3. **레오** → 현빈 정보 참조하여 공격적 단타 (10초마다)
   - 퀀트 2점 이상 + 위험 필터
   - 실거래 모드

4. **영숙** → 텔레그램 명령 수신 및 처리
   - 사용자 명령 처리
   - 에이전트 상태 보고
   - 일정 조회

## 🔍 로그 위치

```
output/bot_logs/youngsuk_daemon.out.log    # 영숙 봇 stdout
output/bot_logs/youngsuk_daemon.err.log    # 영숙 봇 stderr
output/bot_logs/youngsuk_cmd.log           # 영숙 봇 래퍼 로그
```

## ⚠️ 알려진 이슈 & 해결 방법

### 1. Gemini API 연결 실패

**증상**: `Gemini: ❌` 로그 표시

**원인**: 
- Python 인터프리터에 `google-genai` 패키지 미설치
- API 키 미설정

**해결**:
```bash
pip install google-genai
```

### 2. 중복 실행 방지 오류

**증상**: `No module named 'psutil'` 경고

**해결**:
```bash
pip install psutil
```

### 3. 텔레그램 봇 재시작

```bash
# 기존 프로세스 종료
taskkill /F /IM python.exe /FI "WINDOWTITLE eq telegram*"

# 재시작
cmd /c projects\ai-team\scripts\start_youngsuk_bot.cmd
```

## 📝 프로세스 확인 명령어

### PowerShell

```powershell
# 모든 Python 프로세스 확인
Get-Process python* | Select-Object Id, ProcessName, StartTime

# 특정 프로세스 상세 확인
Get-CimInstance Win32_Process -Filter "Name = 'python.exe'" | 
  Select-Object ProcessId, CommandLine | Format-List
```

### Bash

```bash
# 프로세스 목록
ps aux | grep python

# 로그 확인
tail -f output/bot_logs/youngsuk_daemon.out.log
```

## 🚀 시스템 시작 순서

1. 트레이딩 팀 시작:
   ```bash
   python projects/ai-team/scripts/start_trading_team.py --live
   ```

2. 텔레그램 봇 시작:
   ```bash
   cmd /c projects\ai-team\scripts\start_youngsuk_bot.cmd
   ```

3. 상태 확인:
   ```bash
   # 트레이딩 봇
   python -c "import psutil; [print(p.info) for p in psutil.process_iter(['pid', 'name', 'cmdline']) if 'trader' in str(p.info.get('cmdline', ''))]"
   
   # 텔레그램 봇
   tail -20 output/bot_logs/youngsuk_daemon.out.log
   ```

## 📌 주의사항

1. **실거래 모드**: `--live` 플래그 사용 시 실제 거래 발생
2. **API 키 보안**: .env 파일은 git에 커밋하지 않음
3. **로그 용량**: 주기적으로 로그 파일 정리 필요
4. **프로세스 모니터링**: 10초마다 상태 체크, 비정상 종료 시 텔레그램 알림

---

---

## 🐾 펫나나 (Petnna) 프로젝트

### 개요
AI 기반 올인원 반려동물 케어 플랫폼

**배포 URL**: https://petnna.vercel.app  
**프로젝트 경로**: `projects/petnna/`  
**버전**: v1.3.0  
**상태**: 프로덕션 운영 중

### 핵심 기능

#### 1. AI 건강 분석
- **AI 모델**: Gemini API
- **기능**: 사진 한 장으로 건강 상태 즉시 분석
- **출력**: 건강 점수 + 맞춤형 조언

```javascript
// Gemini API 건강 분석
const analyzeHealth = async (imageData) => {
  const result = await geminiAPI.analyze({
    image: imageData,
    prompt: "반려동물의 건강 상태를 분석해주세요"
  });
  return {
    score: result.healthScore,
    recommendations: result.suggestions
  };
};
```

#### 2. 스마트 산책 GPS 트래킹
- **지도 라이브러리**: Leaflet.js
- **기능**:
  - 실시간 위치 추적
  - 거리 및 칼로리 계산
  - 산책 경로 저장/공유
  - 마킹 위치 기록 (💩💦👃)
- **최적화**: GPS 알고리즘 개선 (포인트 압축)

#### 3. 사주팔자 성향 분석
- **입력**: 반려동물 생년월일
- **출력**:
  - 성격 특성 분석
  - 다른 반려동물과 궁합도
  - 맞춤형 케어 조언
- **구현**: `debug_saju.py` - 사주 계산 로직

#### 4. 디지털 일기장
- **기능**:
  - 📸 사진/영상 업로드
  - 🎨 스티커 및 필터 적용
  - 💬 말풍선 추가 (캔버스 렌더링)
  - 📄 PDF 내보내기 (jsPDF)
  - 👥 친구와 공유 (Supabase)

#### 5. 소셜 네트워크
- **기능**:
  - 이웃 집사 프로필 보기
  - 1:1 대화
  - 피드 공유 (좋아요/댓글)
- **DB**: Supabase (PostgreSQL)

#### 6. 마이펫 관리
- 반려동물 프로필 관리
- 건강 대시보드
- 주간/월간 리포트
- 성장 기록 추적

### 기술 스택

**Frontend**:
- HTML5, CSS3, Tailwind CSS
- JavaScript (ES6+)
- Leaflet.js (지도)
- Chart.js (데이터 시각화)

**Libraries**:
- jsPDF v2.5.1 (PDF 생성)
- html2canvas v1.4.1 (스크린샷)

**Backend**:
- Supabase (실시간 DB, 인증)
- PostgreSQL

**AI**:
- Gemini API (건강 분석)

### 주요 파일

```
projects/petnna/
├── README.md                    # 프로젝트 문서
├── DEVELOPMENT_REPORT.md        # 개발 보고서
├── seed_demo_data.py            # 데모 데이터 생성
├── debug_saju.py                # 사주 계산 로직
├── fix_mypet.py                 # 마이펫 버그 수정
├── .env                         # 환경 변수 (Supabase, Gemini)
├── docs/
│   └── progress.md              # 진행 상황
├── TERMS_OF_SERVICE.md          # 이용약관
├── PRIVACY_POLICY.md            # 개인정보 처리방침
└── CHANGELOG.md                 # 변경 이력
```

### 환경 변수 (.env)

```bash
SUPABASE_URL=https://nlgjsdffgkygaylbjooc.supabase.co
SUPABASE_ANON_KEY=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9***
GEMINI_API_KEY=AQ.Ab8RN6JdUpq-***************************
```

### 배포

**플랫폼**: Vercel  
**자동 배포**: main 브랜치 푸시 시  
**도메인**: petnna.vercel.app

### 개발 완료 상태

| 기능 | 상태 | 비고 |
|------|------|------|
| AI 건강 분석 | ✅ 완료 | Gemini API 연동 |
| GPS 산책 트래킹 | ✅ 완료 | 포인트 최적화 적용 |
| 사주팔자 분석 | ✅ 완료 | 궁합도 계산 포함 |
| 디지털 일기장 | ✅ 완료 | PDF 내보내기, 말풍선 |
| 소셜 기능 | ✅ 완료 | 프로필, 1:1 대화 |
| 마이펫 관리 | ✅ 완료 | 건강 대시보드 |

---

**참고 문서**:
- `CLAUDE.md` - 프로젝트 가이드라인
- `ENV_README.md` - 환경 변수 설정 가이드
- `projects/ai-team/skills/*/README.md` - 각 에이전트 상세 문서
- `projects/petnna/README.md` - 펫나나 프로젝트 문서
- `projects/petnna/DEVELOPMENT_REPORT.md` - 펫나나 개발 보고서
