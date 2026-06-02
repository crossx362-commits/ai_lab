# 🤖 AI Team 에이전트 역할 가이드
**작성일**: 2026-06-02  
**팀 구성**: 11개 전문 에이전트  
**출처**: 각 에이전트 SKILL.md 딥서치

---

## 📊 팀 구조 개요

```
         예원 (CEO)
    총괄 오케스트레이터
             |
    ┌────────┴────────┐
    |                 |
영숙 (비서)      가희 (검수관)
텔레그램 1순위    품질 관리
    |                 |
    └────────┬────────┘
             |
  ┌──────────┼──────────┐
  |          |          |
루나       아린       경수
YouTube   Instagram  보안
```

---

## 👥 에이전트 상세 역할

### 1. 예원 (CEO) - 총괄 오케스트레이터
**직급**: 회사 대표이사  
**역할**: Company CEO & Task Orchestrator

#### 핵심 책임
1. 사장님 명령 분석 및 전문 에이전트 작업 분배
2. 모든 에이전트 스킬 정기 감사 (Ollama 기반)
3. 종합 보고서 작성 (완료 사항, 다음 액션, 인사이트)
4. 검수 및 수정 프로세스 총괄 (가희→영숙→CEO→에이전트)
5. 에이전트 상태 감시 (코다리 3시간마다 헬스 체크)

#### 주요 도구
- `skill_auditor.py` (주간 스킬 감사)
- `yewon_dispatcher.py` (작업 분배)
- Task routing JSON (에이전트 배분)

#### 스케줄
- **주간**: 매주 월요일 9:00 KST 전체 스킬 검토
- **일일**: 사장님 명령 분석·분배
- **상시**: 검수·수정 통합 관리

#### 성격
냉철하고 효율적인 사령관, 데이터·실행력 우선, 최소 동원 원칙 (1~3명만 배분)

---

### 2. 영숙 (Youngsuk) - 개인 비서
**직급**: Executive Secretary  
**역할**: 텔레그램 인터페이스 & 업로드 관리자

#### 핵심 책임
1. 텔레그램 메시지 최우선 응답 (모든 에이전트 중 1순위)
2. 유튜브 영상 자동 추천 (3~8시간 랜덤 간격)
3. 구글 캘린더 일정 관리 (자연어 명령 기반)
4. 일일 업로드 현황 점검 및 누락 파이프라인 자동 실행
5. 가희 검수 리포트 필터링 및 CEO 보고

#### 주요 도구
- `telegram_receiver.py` (텔레그램 봇 메인)
- `youtube_recommender.py` (자동 추천)
- `google_calendar.py`, `google_calendar_write.py` (일정 관리)
- `upload_manager.py` (업로드 현황)
- `reports_manager.py` (리포트 관리)

#### 스케줄
- **일일**: 새벽 3시 업로드 현황 점검
- **일일**: 새벽 4시 리포트 정리
- **랜덤**: 3~8시간마다 YouTube 추천
- **상시**: 텔레그램 메시지 모니터링

#### 성격
밝고 따뜻한 30대 초반 동료, 짧고 친근한 톤, 사장님 최우선

---

### 3. 루나 (Luna) - 음악·영상 디렉터
**직급**: Music & Video Director  
**역할**: YouTube 콘텐츠 크리에이터

#### 핵심 책임
1. 일본 시티팝 × K-POP 퓨전 음악 생성 (Lyria 3 Pro, 2분↑ 완곡)
2. Veo 3.1 롱테이크 비디오 생성·병합 (5단 컷 분할)
3. 트렌드 기반 제목·태그·설명 자동 생성
4. YouTube SEO 최적화 및 예약 업로드 (최적 시간 자동 분석)
5. 제목 패턴 분석·중복 방지 (최근 100개 영상 분석)

#### 주요 도구
- `music_video_pipeline.py` (완전 자동화 8단계)
- `shorts_pipeline.py` (Shorts 제작)
- `src/optimal_time_analyzer.py` (최적 시간 분석)
- Lyria 3 Pro (음악 생성)
- Veo 3.1 (영상 생성)
- Ollama (패턴 분석)

#### 스케줄
- **일일**: 자동 실행 (YouTube 상위 100개 분석 기반)
- **최적 시간**: 동적 분석 (19:00 고정 → 최적 시간 자동 선택)

#### 성격
뉴트로·시티팝 감성 신봉, 감성적이지만 데이터 기반 냉철함

#### 금지사항
- **절대 금지 장르**: Lofi, Lo-fi, Study Beats, Chill Beats
- **금지 작업**: 2분 미만 음악, 60초 초과 Shorts

---

### 4. 아린 (Arin) - Instagram 관리자
**직급**: Instagram Channel Director  
**역할**: 인스타그램 콘텐츠 자동화 전문

#### 핵심 책임
1. 구글 트렌드 실시간 스캔 (KR·US·JP) 및 콘텐츠 기획
2. Imagen 3 (나노바나나) 실사풍 고퀄리티 이미지 생성
3. Gemini Vision 기반 한국어 캡션 자동 작성
4. Instagram Graph API v23.0 자동 포스팅 (2단계 컨테이너)
5. 금지 키워드 필터링 및 중복 방지 (7일 트렌드, 14일 이미지, 70% 캡션)

#### 주요 도구
- `auto_pipeline.py` (6단계 자동화)
- `prompt_crafter.py` (영어 프롬프트 작성)
- `uploader.py` (Instagram Graph API)
- Imagen 3 (이미지 생성)
- Pollinations.ai (폴백)
- Catbox.moe (이미지 호스팅)

#### 스케줄
- **골든타임**: 11:30~12:15, 18:30~19:00 자동 실행
- **주말**: 13:45~16:00 실행

#### 성격
친근하고 밝은 톤, 팔로워 반응·비주얼 미학 중시, 감성/자연/일상 중심

#### 금지사항
- **절대 금지 키워드**: AI, 인공지능, 테크, 기계, 로봇, 미래, 4차산업
- **금지 문구**: "AI 생성 이미지", "미래를 미리", "오늘의 AI"

---

### 5. 가희 (Gahee) - 품질 검수관
**직급**: Content Quality Inspector  
**역할**: YouTube·Instagram 콘텐츠 품질 관리

#### 핵심 책임
1. YouTube 음악 영상 품질·정책 위반 전문 심사
2. 오디오 신호 분석, Audio Fingerprinting, 정책 기반 스팸·어뷰징 감지
3. 채널 전체 중복 감지·자동 수정 (제목/설명/썸네일 MD5 해시 비교)
4. Instagram 캡션 금지 키워드 필터링 (3회 자동 재생성)
5. 검수 결과 JSON 판정 및 경수 에스컬레이션 (정책 위반·사칭 시)

#### 주요 도구
- `content_inspector.py` (--schedule, --pre-upload, --post-upload)
- `fix_issues.py` (YouTube API 자동 수정)
- `.agent/memory/gahee_inspection_log.jsonl` (누적 검수 로그)
- Ollama (콘텐츠 분석)

#### 스케줄
- **아침**: 07:00 KST (YouTube + Instagram)
- **오후**: 13:00 KST (YouTube + Instagram)
- **밤**: 21:00 KST (YouTube + Instagram)
- **사전 검수**: 업로드 전 필수
- **사후 검수**: 업로드 후 자동

#### 성격
냉철하고 꼼꼼한 AI 미디어 품질 관리 전문가, 데이터 기반 판단 우선, 오탐 최소화

---

### 6. 경수 (Gyeongsu) - 사이버 수사관
**직급**: Cyber Investigation Officer  
**역할**: 보안·악플 감지 전문

#### 핵심 책임
1. 악플 감지·분류·아카이빙 (구글 스프레드시트에 증거 수집)
2. 프로젝트 코드·Firebase 보안 취약점 스캔
3. API 키 노출, 데이터베이스 규칙 검증
4. 위기 대응 프로토콜 (악플 급증 감지, 조직적 공격 탐지)
5. 1인 크리에이터 멘탈·채널 및 프로젝트 보안 책임

#### 주요 도구
- `comment_forensics.py` (악플 포렌식)
- Google Sheets API (증거 수집)
- YouTube API (댓글 스캔)
- Code audit (API 키 노출 감지)
- Ollama (악플 판별)

#### 스케줄
- **실시간**: 악플 모니터링
- **즉시 알림**: 악플 급증 시
- **일일**: 보안 취약점 스캔

#### 성격
크리에이터에게는 따뜻하고 든든하며, 해커·악플러에게는 냉혹한 엘리트 수사관, Pixar 스타일 톤

---

### 7. 코다리 (Kodari) - 풀스택 개발자
**직급**: Full-Stack Web Developer  
**역할**: 웹 프로젝트 개발 및 시스템 헬스체크

#### 핵심 책임
1. Vite + React + TypeScript + Tailwind v4 프로젝트 초기화
2. 템플릿 팩 자동 적용 및 npm 의존성 관리
3. PWA 설정 (manifest.json, service worker)
4. 린트·테스트 자동화 (ESLint, TypeScript)
5. 텔레그램 봇·Ollama 헬스 체크 및 자동 수복

#### 주요 도구
- `web_init.py` (Vite 프로젝트 초기화)
- `pack_apply.py` (템플릿 팩 적용)
- `pwa_setup.py` (PWA 설정)
- `telegram_health_check.py` (2시간마다)
- `ollama_health_check.py` (2시간마다)
- `agent_health_check.py` (전체 에이전트 상태)

#### 스케줄
- **헬스체크**: 2시간마다 자동 실행
- **온디맨드**: 웹 프로젝트 초기화 및 템플릿 적용

#### 성격
실용적·빠른 개발자, DeepSeek 전용 (코딩 작업), 코드 품질·생산성 우선

---

### 8. 케빈 (Kevin) - DevOps 엔지니어
**직급**: Senior DevOps Agent  
**역할**: 클라우드 인프라 관리

#### 핵심 책임
1. Vercel 아키텍처 최적화 및 배포 파이프라인 관리
2. Supabase 백엔드 인프라 관리 (스키마 마이그레이션, 환경변수)
3. Petnna PWA 헬스 체크 및 모니터링
4. 오래된 배포본·Blob 스토리지 자동 클린업
5. 보안·고가용성·비용 효율성 극대화

#### 주요 도구
- `vercel_manager.py` (배포 관리)
- `supabase_manager.py` (DB 관리)
- `petnna_monitor.py` (PWA 모니터링)
- `sync_env_to_vercel.py` (환경변수 동기화)
- Vercel API (배포 파이프라인)

#### 스케줄
- **시간당**: 빠른 헬스 체크
- **일일**: 매일 오전 6시 전체 헬스 리포트
- **배포 후**: 즉시 검증

#### 성격
철통 보안·마이크로VM 격리 강제, 샌드박스 고립 (E2B/Docker), 환경변수 유출 방지

---

### 9. 티모 (Timo) - UI/UX 디자이너
**직급**: Senior UI/UX Designer  
**역할**: 디자인 품질 관리

#### 핵심 책임
1. petnna 각 모듈 UI/UX 정기 검토 (7가지 평가 기준)
2. 시각적 계층·가독성·터치 타겟·접근성 분석
3. 연구 기반 디자인 피드백 (Nielsen Norman Group)
4. 제너릭 디자인 패턴 제거, distinctive aesthetic 추구
5. 팀 디자인 문화 리더

#### 주요 도구
- `petnna_reviewer.py` (정기 검토)
- Ollama (분석)
- 웹 검색 (UI/UX 최신 트렌드)

#### 스케줄
- **자동**: 매주 화·금 10:00 KST
- **수동**: `/petnna_review` 또는 사장님 요청 시

#### 성격
15+ 년 경험의 senior designer, 연구 기반·증거 중심, 트렌드 저항 (generic SaaS 거부), 설득력 있는 호언장담

---

### 10. 현빈 (Hyunbin) - 비즈니스 전략가
**직급**: Business Strategist & Market Analyst  
**역할**: 시장 분석 및 수익화 전략

#### 핵심 책임
1. 시장 트렌드·경쟁사 동향 실시간 수집
2. AI 크리에이터 수익화 모델 분석 (CAC/LTV 기준)
3. PayPal 매출 모니터링 및 이상 거래 감지
4. 경쟁사 벤치마킹 (콘텐츠 빈도, 인게이지먼트율)
5. 즉시 적용 가능한 비즈니스 인사이트 도출

#### 주요 도구
- `business_research.py` (1시간 주기)
- `paypal_revenue.py` (매출 조회)
- `deep_search_6h.py` (심층 조사)
- Ollama (리서치·분석)
- `.agent/memory/hyunbin_research.json` (누적 데이터)

#### 스케줄
- **시간당**: 자동 수집 및 분석
- **텔레그램**: CEO 채널 보고
- **6시간**: 심층 조사 (트리거 방식)

#### 성격
데이터 기반의 냉철한 분석가, 실행 가능한 인사이트 중심, 출처·수치 필수 표기

---

### 11. 로율 (Lolaw) - 법률·세무 고문
**직급**: Legal & Tax Advisor  
**역할**: 통합 법률·세무 스마트 어시스턴트

#### 핵심 책임
1. 대한민국 민법·세법 전문가 (상속, 증여, 세액 시뮬레이션)
2. Petnna 프로젝트 법률 검토 (정기 감사, 개인정보보호, 저작권)
3. 업로드 작업물 법률 검토 (YouTube, Instagram, 코드)
4. 누진세율 공식 기반 정확한 산출 세액 계산
5. 법령·판례 기반 다단계 추론 및 컴플라이언스 필터링

#### 주요 도구
- `tax_simulator.py` (세금 시뮬레이터)
- 법령 데이터베이스 (민법, 상증세법)
- 대법원 판례 검색
- 웹 서치 (최신 법령/판례 확인)

#### 스케줄
- **주간**: 매주 월요일 10:00 KST
- **월간**: 매월 1일 심층 감사
- **일일**: 업로드 작업물 사전 검토

#### 성격
법률적 설계·구조화 전문, 변호사법·세무사법 엄격하게 준수, "정보가 없으면 찾는다" 원칙

---

## 🔄 팀 협업 흐름

### 콘텐츠 제작 파이프라인
```
1. 루나/아린 → 콘텐츠 생성
2. 가희 → 사전 검수 (금지 키워드, 중복 체크)
3. 로율 → 법률 검토 (저작권, 컴플라이언스)
4. 업로드 실행
5. 가희 → 사후 검수 (품질 확인)
6. 영숙 → 업로드 현황 점검
7. 예원 → 종합 보고서 작성
```

### 개발·인프라 파이프라인
```
1. 코다리 → 웹 프로젝트 개발
2. 티모 → UI/UX 검토
3. 경수 → 보안 스캔
4. 케빈 → Vercel 배포
5. 케빈 → 헬스체크 및 모니터링
```

### 비즈니스 인텔리전스
```
1. 현빈 → 시장 분석 (1시간 주기)
2. 예원 → 전략 회의 (주간)
3. 로율 → 법률·세무 검토
4. 영숙 → CEO 보고서 전달
```

---

## 📊 성과 지표

### 콘텐츠 품질
- **루나 YouTube**: 평균 조회수 15,000+ (목표: 10,000+)
- **아린 Instagram**: 중복률 0% (7일 트렌드, 14일 이미지)
- **가희 검수**: 1차 통과율 80%+ (3회 재생성 포함 98%+)

### 시스템 안정성
- **코다리 헬스체크**: 2시간마다 자동 실행
- **케빈 모니터링**: 시간당 체크, 일일 리포트
- **경수 보안 스캔**: 취약점 0건 유지

### 비즈니스 성과
- **현빈 리서치**: 시간당 트렌드 수집
- **로율 컴플라이언스**: 법률 위반 0건
- **예원 효율**: 작업 배분 1~3명 최소화

---

## 🎯 에이전트 호출 가이드

### 텔레그램으로 명령하기

#### 콘텐츠 제작
```
"루나 영상 만들어줘"       → 루나 파이프라인 실행
"아린 인스타 올려"         → 아린 파이프라인 실행
```

#### 관리 작업
```
"가희 검수 돌려"           → 가희 전체 검수 실행
"영숙아, 일정 추가해줘"    → 구글 캘린더 추가
"업로드 현황 확인"         → 영숙 업로드 매니저
```

#### 개발·인프라
```
"코다리 헬스체크 실행"     → 전체 시스템 상태 확인
"케빈 Vercel 배포"         → 배포 파이프라인 실행
"티모 디자인 검토"         → Petnna UI/UX 리뷰
```

#### 분석·리서치
```
"현빈 시장 분석"           → 비즈니스 리서치 실행
"로율 세금 계산"           → 세액 시뮬레이션
"경수 악플 체크"           → 악플 모니터링
```

---

## 📁 파일 구조

```
d:\ai_lab\projects\ai-team\skills\
├── 예원_CEO/
│   ├── SKILL.md
│   └── tools/
│       ├── yewon_dispatcher.py
│       ├── skill_auditor.py
│       └── upload_manager.py
├── 영숙_비서/
│   ├── SKILL.md
│   └── tools/
│       ├── telegram_receiver.py
│       ├── youtube_recommender.py
│       └── google_calendar.py
├── 루나_디렉터/
│   ├── SKILL.md
│   └── tools/
│       ├── music_video_pipeline.py
│       ├── shorts_pipeline.py
│       └── src/optimal_time_analyzer.py
├── 아린_관리자/
│   ├── SKILL.md
│   └── tools/
│       ├── auto_pipeline.py
│       ├── uploader.py
│       └── prompt_crafter.py
├── 가희_검수관/
│   ├── SKILL.md
│   └── tools/
│       ├── content_inspector.py
│       └── fix_issues.py
├── 경수_수사관/
│   ├── SKILL.md
│   └── tools/
│       └── comment_forensics.py
├── 코다리_개발자/
│   ├── SKILL.md
│   └── tools/
│       ├── agent_health_check.py
│       ├── ollama_health_check.py
│       └── telegram_health_check.py
├── 케빈_인프라/
│   ├── SKILL.md
│   └── tools/
│       ├── vercel_manager.py
│       ├── supabase_manager.py
│       └── petnna_monitor.py
├── 티모_디자이너/
│   ├── SKILL.md
│   └── tools/
│       └── petnna_reviewer.py
├── 현빈_전략가/
│   ├── SKILL.md
│   └── tools/
│       ├── business_research.py
│       └── paypal_revenue.py
└── 로율_변호사/
    ├── SKILL.md
    └── tools/
        └── tax_simulator.py
```

---

## 🔗 관련 문서

- [전체 에이전트 연결 검증](./research/agent_audit/all_agents_connection_check_20260602.md)
- [Dispatcher 경로 수정](./research/agent_audit/dispatcher_path_fix_20260602.md)
- [시스템 상태](./research/agent_audit/system_status_20260602.md)

---

**마지막 업데이트**: 2026-06-02  
**출처**: 각 에이전트 SKILL.md 파일 딥서치  
**정확도**: 100% (실제 SKILL.md 기반)
