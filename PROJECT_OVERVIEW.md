# 🏗️ Connect AI Lab — 전체 프로젝트 개요

> 마지막 업데이트: 2026-06-16

---

## 📁 워크스페이스 구조

```text
ai_lab/
├── projects/
│   ├── ai-team/          # 🤖 AI 에이전트 프레임워크
│   └── petnna/           # 🐾 펫과나 웹 앱
├── reports/              # 📊 리포트 및 로그
├── docs/                 # 📄 문서
├── output/               # 🎬 생성 결과물
└── .agent/               # ⚙️ 로컬 스킬·메모리·도구
```

---

## 🤖 AI 팀 구조

```
                   예원 (CEO)
              최고 총괄 오케스트레이터
                        |
              영숙 (Executive 비서)
           텔레그램 최우선 순위 독점
                        |
     ┌──────────────────┼──────────────────┬──────────────────┐
     |                  |                  |                  |
   케빈 (Kevin)      데이브 (Dave)      레오 (Leo)        현빈 (Hyunbin)
 DevOps 인프라      보수적 매매        공격적 단타       시장 정보 수집
```

---

## 👥 에이전트 상세 역할

### 1. 예원 (CEO) — 최고 총괄 오케스트레이터
**직급**: 최고 경영자 및 중앙 컨트롤러

#### 핵심 책임
1. **태스크 아키텍처링 & 플래닝**: 사장님 비즈니스 명령 분석 후 최소 동원 원칙(최대 2~3명 제한)에 기반한 최적의 에이전트 배치 및 실행 JSON 분배.
2. **원점 라우팅 제어**: 단건 메시지 유입 시 오직 가용 대상 에이전트 ID 1개만 JSON 포맷으로 정밀 반환.
3. **시스템 가용성 거버넌스**: 코다리 3시간 헬스 체크 로그 마스터 수령 및 인프라 안전성 스캔.
5. **에이전트 스킬 검토 및 자가 진화 감사**: `skill_auditor.py`로 전 에이전트 스킬 완성도 Ollama 다차원 채점 후 주간 리포팅.

**주요 도구**: `yewon_dispatcher.py`, `skill_auditor.py`  
**스케줄**: 매주 월요일 09:00 KST 전체 스킬 감사 | 상시 명령 분배

---

### 2. 영숙 (Youngsuk) — 사장님 전담 Executive 비서
**직급**: 수석 보좌 비서

#### 핵심 책임
1. **텔레그램 최우선 챗 인터셉트**: 사장님 메시지를 모든 에이전트 중 최우선 독점 수신. 단순 일상·일정은 직접 처리, 창작·분석 업무는 예원 CEO에게 dispatch 포워딩.
2. **구글 캘린더 연동**: 자연어 파싱 기반 캘린더 CRUD 제어 (표준 JSON 규격 단일 블록 응답).
3. **일일 정기 업로드 총괄**: 매일 새벽 03:00 `upload_manager.py` 트리거 → 누락 파이프라인 선제 자율 실행 후 통합 브리핑.
4. **AI 모델**: Gemini 2.5 Flash (Fallback: GPT-4o mini → Ollama)

**주요 도구**: `telegram_receiver.py`, `upload_manager.py`, `google_calendar.py`, `youtube_recommender.py`  
**스케줄**: 03:00 업로드 점검 | 04:00 리포트 정리 | 상시 텔레그램 모니터링

---

### 3. 데이브 (Dave) — 보수적 암호화폐 트레이더
**직급**: 보수적 매매 봇

#### 핵심 책임
- **감시 코인 (14개)**: SOL, XRP, DOGE, NEAR, SUI, SEI, STX, HBAR, ADA, AVAX, LINK, PEPE, BTC, ETH
- **진입 조건**: 퀀트 스코어 ≥3.0점 + LLM(GPT-4o mini) 최종 검증 + 연준 이벤트 HIGH 구간 진입 금지
- **실행 주기**: 30초마다 전체 코인 스캔

**퀀트 지표 (10개)**: EMA200(+4) · Supertrend(+3) · StochRSI(+3) · Heikin Ashi(+3) · Volume Spike(+2) · OBV 다이버전스(+2) · CVD 다이버전스(+2) · 세력 매집 패턴(+2) · 워시트레이딩 페널티(-3) · 4시간봉 모멘텀(+2)

**주요 도구**: `upbit_auto_trader.py`  
**상태**: ✅ 실행 중

---

### 4. 레오 (Leo) — 공격적 단타 트레이더
**직급**: 고변동성 알트코인 단타 봇

#### 핵심 책임
- **감시 코인 (7개)**: DOGE, PEPE, NEAR, SUI, SEI, HBAR, STX
- **진입 조건**: 퀀트 스코어 ≥2.0점 (데이브보다 낮음) + 데이브 보유 코인 제외
- **실행 주기**: 10초마다 스캔
- **위험 관리**: 연속 손절 3회 → 30분 휴식 | 일일 손실 -5% → 거래 중단 | 시간당 5회 거래 제한

**주요 도구**: `leo_aggressive_trader.py`, `leo_learning_system.py`  
**상태**: ✅ 실행 중

---

### 5. 현빈 (Hyunbin) — 시장 정보 수집 + 비즈니스 전략가
**직급**: 시장 정보 수집 에이전트 & Business Strategist

#### 핵심 책임
- **수집 데이터**: 연준 이벤트(FOMC·CPI·NFP) | 공포탐욕지수(Alternative.me) | 김치 프리미엄(업비트 vs 바이낸스) | 암호화폐 뉴스(CryptoPanic)
- **출력**: `reports/research/crypto_market_intel.json`
- **비즈니스 리서치**: 1시간 주기 크리에이터 이코노미 시장 트렌드 파악, CAC/LTV 기반 수익화 파이프라인 설계, PayPal 결제 이상 징후 추적

**주요 도구**: `crypto_market_intelligence.py`, `business_research.py`, `paypal_revenue.py`  
**스케줄**: 5분마다 시장 정보 갱신 | 1시간 주기 리서치 | 6시간 심층 조사

---

### 6. 케빈 (Kevin) — DevOps & 클라우드 인프라 아키텍트
**직급**: 수석 인프라 엔지니어

#### 핵심 책임
1. **Vercel 인프라 최적화**: `ignoredBuildStep` 스크립트로 불필요한 빌드 방지, Vercel Cron Job(`0 6,18 * * *`)으로 임시 자원 배치 정리.
2. **Supabase 백엔드 관리**: PostgreSQL 마이그레이션 형상 제어 및 환경 변수 동기화.
3. **Petnna PWA 모니터링**: `petnna_monitor.py`로 도메인 응답·PWA 파일 무결성·DB 로그인 실시간 스캔. 이상 감지 시 텔레그램 즉시 알림.

**주요 도구**: `vercel_manager.py`, `supabase_manager.py`, `petnna_monitor.py`  
**스케줄**: 매 시간 헬스 체크 | 매일 06:00 KST 전체 가용성 리포트 | 06시/18시 가비지 컬렉션

---

### 7. 경수 (Gyeongsu) — 사이버 수사관
**직급**: Cyber Investigation Officer

- **핵심 책임**: 유튜브·SNS 댓글 악플 및 조직적 어뷰징 Ollama 판별·분류, Google Sheets 증거 아카이빙. 소스 코드 API Key 탈취 시도 및 보안 취약점 스캔.
- **주요 도구**: `comment_forensics.py`, Google Sheets API, YouTube API 커스텀 스캐너
- **스케줄**: 실시간 모니터링 | 악플 급증 감지 시 텔레그램 즉시 알림

---

### 10. 코다리 (Kodari) — 풀스택 개발자
**직급**: Full-Stack Web Developer

- **핵심 책임**: Vite + React + TypeScript + Tailwind v4 기반 petnna 프로젝트 아키텍처 설계 및 빌드. 2시간 주기 Ollama·텔레그램 API·구글 캘린더 토큰 헬스 체크 후 예원 CEO 상태 보고.
- **주요 도구**: `web_init.py`, `pack_apply.py`, `agent_health_check.py`, `ollama_health_check.py`
- **스케줄**: 2시간 간격 헬스 체크 | 온디맨드 웹 개발 (DeepSeek 전용 연동)

---

### 11. 티모 (Timo) — 수석 UI/UX 디자이너
**직급**: Senior UI/UX Designer

- **핵심 책임**: NN Group·F-Pattern 등 사용성 논문 팩트 기반 인터페이스 설계. `petnna_reviewer.py`로 petnna 모듈 자율 웹 리뷰 및 UI 결함 텔레그램 보정 리포트 전송. 7대 검수 기준(시각계층·가독성·터치타겟·빈상태·반응형·일관성·WCAG) 준수 CSS/JS 스니펫 구현 출력.
- **주요 도구**: `petnna_reviewer.py`
- **스케줄**: 매주 화·금 10:00 KST 소스코드 UI 자율 점검

---

### 12. 로율 (Lolaw) — 법률·세무 고문
**직급**: Legal & Tax Advisor

- **핵심 책임**: 대한민국 민법·상속세·증여세법 기반 세액 산출 시뮬레이터 운영. petnna 개인정보처리방침·약관·저작권 법률 검토 관할.
- **주요 도구**: `tax_simulator.py`, 대한민국 법령·판례 연동 크롤러
- **스케줄**: 매주 월요일 10:00 KST 주간 컴플라이언스 감사 | 매월 1일 법률 오딧

---

## 🔄 트레이딩 팀 협업 흐름

```
현빈 (5분마다 시장 정보 수집)
  ↓ 연준 이벤트 · 공포탐욕지수 · 김치프리미엄
  ├→ 데이브 (30초 주기, 퀀트 3점 + LLM 검증 → 보수적 매매)
  └→ 레오  (10초 주기, 퀀트 2점 → 공격적 단타)
  
매일 자정: 거래 기록 분석 → Ollama 인사이트 → 전략 개선
```

### 실행 명령
```bash
# 트레이딩 팀 시작 (실거래)
python projects/ai-team/scripts/start_trading_team.py --live

# 텔레그램 봇 시작
cmd /c projects\ai-team\scripts\start_youngsuk_bot.cmd
```

---

## 🔄 웹/인프라 협업 흐름

```
코다리 (Vite + React + TS 개발)
  ↓
티모 (7대 사용성 지표 리뷰 + CSS/JS 스니펫 출력)
  ↓
경수 (소스 코드 취약점 · API Key 노출 스캔)
  ↓
케빈 (Vercel 프로덕션 배포 + PWA 시간별 모니터링)
  ↓
코다리 (2시간 주기 에이전트 API 헬스 체크 → 예원 CEO 보고)
```

---

## 🐾 펫과나 (Petnna) 프로젝트

**배포 URL**: https://petnna.vercel.app  
**버전**: v1.3.0 | **상태**: 프로덕션 운영 중

| 기능 | 상태 |
|------|------|
| AI 건강 분석 (Gemini API) | ✅ 완료 |
| GPS 산책 트래킹 (Leaflet.js) | ✅ 완료 |
| 사주팔자 성향 분석 | ✅ 완료 |
| 디지털 일기장 (PDF 내보내기) | ✅ 완료 |
| 소셜 네트워크 (Supabase) | ✅ 완료 |
| 마이펫 관리 | ✅ 완료 |

**기술 스택**: HTML5 · Tailwind CSS · JavaScript ES6+ · Leaflet.js · Chart.js · Supabase (PostgreSQL) · Gemini API · Vercel

---

## 📁 주요 스크립트 경로

```
projects/ai-team/skills/
├── 예원_CEO/tools/          yewon_dispatcher.py · skill_auditor.py
├── 영숙_비서/tools/          telegram_receiver.py · upload_manager.py
├── 데이브_주식/tools/        upbit_auto_trader.py
├── 레오_트레이더/tools/       leo_aggressive_trader.py
├── 현빈_전략가/tools/        crypto_market_intelligence.py · business_research.py
├── 케빈_인프라/tools/        vercel_manager.py · petnna_monitor.py
├── 경수_수사관/tools/        comment_forensics.py
├── 코다리_개발자/tools/       agent_health_check.py · web_init.py
├── 티모_디자이너/tools/       petnna_reviewer.py
└── 로율_변호사/tools/        tax_simulator.py
```

---

## 🔧 환경 설정

### 필수 API 키 (.env)
```
GEMINI_API_KEY          # Google AI
TELEGRAM_BOT_TOKEN      # 텔레그램 봇
TELEGRAM_CHAT_ID
UPBIT_ACCESS_KEY        # 업비트 실거래
UPBIT_SECRET_KEY
OPENAI_API_KEY          # GPT-4o mini 폴백
ANTHROPIC_API_KEY       # Claude 폴백
SUPABASE_URL            # 펫과나 DB
SUPABASE_ANON_KEY
VERCEL_TOKEN            # 배포
```

### 보안
- 원본 `.env`, `client_secret.json`은 Git 제외
- 암호화 저장: `.env.encrypted`, `client_secret.json.encrypted`
- 복호화: `python decrypt_all_secrets.py` (비밀번호: 별도 관리)

---

## 📊 실행 중인 에이전트 현황 (2026-06-16 기준)

| 에이전트 | 역할 | 주기 | 상태 |
|---------|------|------|------|
| 현빈 | 시장 정보 수집 | 5분 | ✅ 실행 중 |
| 데이브 | 보수적 매매 | 30초 | ✅ 실행 중 |
| 레오 | 공격적 단타 | 10초 | ✅ 실행 중 |
| 영숙 | 텔레그램 비서 | 상시 | ✅ 실행 중 |
| 케빈 | PWA 모니터링 | 1시간 | ✅ 실행 중 |
| 코다리 | 헬스 체크 | 2시간 | ✅ 실행 중 |

---

## 📝 로그 위치

```
output/bot_logs/youngsuk_daemon.out.log    # 영숙 봇
output/bot_logs/youngsuk_daemon.err.log
reports/history/                            # 에이전트별 히스토리
reports/inspection/petnna_inspection_report.md
reports/research/crypto_market_intel.json  # 현빈 시장 정보
```
