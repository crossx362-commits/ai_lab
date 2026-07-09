# 🏗️ Connect AI Lab — 전체 프로젝트 개요

> 마지막 업데이트: 2026-07-09 (2026-07-08 오너 지시로 주식·코인 트레이딩 도메인 전면 삭제, 펫나 자동개발팀으로 재편)

---

## 📁 워크스페이스 구조

```text
ai_lab/
├── projects/
│   ├── ai-team/          # 🤖 AI 에이전트 프레임워크
│   └── petnna/           # 🐾 펫과나 웹 앱
├── reports/              # 📊 리포트 및 로그
├── docs/                 # 📄 문서
├── output/                # 🎬 생성 결과물
└── .agent/               # ⚙️ 로컬 스킬·메모리·도구
```

---

## 🤖 AI 팀 구조

```
                   예원 (CEO)
              오케스트레이션·하네스·워치독
                        |
              영숙 (비서)
        텔레그램 게이트웨이·일정·정시 잡
                        |
     ┌───────┬──────────┼──────────┬───────┬───────┐
     |       |          |          |       |       |
   봄이     수리        테오       백호     미오     나무
   QA       Dev         Test       Backend Design   PM
```

## 👥 에이전트 상세 역할

### 1. 예원 (Yewon) — CEO
- 오케스트레이션·하네스 체크·워치독·콘텐츠 피드백. 펫나 긴급 회의(전 에이전트 소집) 의장.
- **주요 도구**: `yewon_dispatcher.py`, `harness_manager.py`, `harness_monitor.py`, `skill_auditor.py`, `petnna_council.py`

### 2. 영숙 (Youngsuk) — Secretary
- 텔레그램 게이트웨이(폴링·발신은 `_shared/telegram.py`), 일정, 정시 잡 실행자.
- **주요 도구**: `telegram_receiver.py`, `schedule_manager.py`, `agent_controller.py`, `calendar_manager.py`

### 3. 봄이 (Bomi) — QA
- 펫나 상시 순찰: 콘솔/JS 오류·404·깨진 이미지·접근성·가로스크롤·SEO 점검, P0/P1 즉시 텔레그램.
- **주요 도구**: `petnna_qa_patrol.py`

### 4. 수리 (Suri) — Dev
- 봄이 결과를 읽어 저위험 P2/P3를 격리 브랜치에서 자동 수정·재검수 후 게이트 통과 시만 master 병합. QA 이슈 없으면 미오·나무 백로그 과제 구현(항상 PR대기, 자동 병합 없음).
- **주요 도구**: `petnna_dev_engine.py`

### 5. 테오 (Teo) — Test
- Playwright E2E 테스트 자동 작성(하루 1개, 2회 연속 통과 시 채택·flaky 폐기), 매일 + 변경 시 전체 스위트 실행.
- **주요 도구**: `petnna_test_engineer.py`

### 6. 백호 (Baekho) — Backend
- Supabase 스키마·RLS vs 프론트 쿼리 계약 감사(매일, 읽기 전용).
- **주요 도구**: `petnna_backend_guard.py`

### 7. 미오 (Mio) — Design
- 주 1회(월) 스크린샷 기반 UX·시각 리뷰 → 공유 백로그 적재.
- **주요 도구**: `petnna_design_review.py`

### 8. 나무 (Namu) — PM
- 주 1회(화) 웹서치 트렌드·경쟁 조사 → 기능 백로그 적재.
- **주요 도구**: `petnna_product_manager.py`

> 과거 주식·코인 트레이딩 에이전트(소미·한별·행크·유나·레온·마켓데스크·지아)와 그 이전 세대 에이전트(데이브·레오·시그널·펄스·케빈·경수·코다리·티모·로율)는 전부 삭제됨(2026-07-08 및 그 이전 정리) — git 이력에서 복구 가능. 자세한 내용과 삭제 배경은 `CLAUDE.md`의 하네스 가드레일 섹션 참고.

---

## 🔄 펫나 자동 개발 루프

```
봄이(발견) · 백호(DB 계약) · 테오(회귀 테스트)
  ↓
수리(수정/구현, 격리 브랜치)
  ↓
봄이 재검수 → 저위험 P2/P3만 자동 병합, 나머지는 사람 검토 대기

미오(디자인) · 나무(기획) → output/qa/petnna/backlog.json 과제 적재
  → 수리가 QA 이슈 없을 때 브랜치 구현(자동 병합 없음)
```

큰 이슈(봄이 신규 P0/P1, 수리 3회 실패 보류, 백호 신규 P1 계약 위반)는 `petnna_council.py`가
전 에이전트 긴급 회의를 자동 소집한다.

### 실행 명령
```bash
# 텔레그램 봇 시작
python projects/ai-team/skills/영숙_비서/tools/telegram_receiver.py

# 개별 에이전트 제어 (start|stop|restart|status)
python projects/ai-team/skills/영숙_비서/tools/agent_controller.py 영숙 restart
```

---

## 🐾 펫과나 (Petnna) 프로젝트

**배포 URL**: https://petnna.vercel.app

| 기능 | 상태 |
|------|------|
| AI 건강 분석 (Gemini API) | ✅ |
| GPS 산책 트래킹 (Leaflet.js) | ✅ |
| 사주팔자 성향 분석 | ✅ |
| 디지털 일기장 (PDF 내보내기) | ✅ |
| 소셜 네트워크 (Supabase) | ✅ |
| 마이펫 관리 | ✅ |

**기술 스택**: HTML5 · Tailwind CSS · JavaScript ES6+ · Leaflet.js · Chart.js · Supabase (PostgreSQL) · Gemini API · Vercel

---

## 📁 주요 스크립트 경로

```
projects/ai-team/skills/
├── 예원_CEO/tools/        yewon_dispatcher.py · harness_manager.py · harness_monitor.py · petnna_council.py
├── 영숙_비서/tools/        telegram_receiver.py · schedule_manager.py · agent_controller.py
├── 봄이_QA/tools/          petnna_qa_patrol.py
├── 수리_개발자/tools/       petnna_dev_engine.py
├── 테오_테스트/tools/       petnna_test_engineer.py
├── 백호_백엔드/tools/       petnna_backend_guard.py
├── 미오_디자인/tools/       petnna_design_review.py
└── 나무_기획/tools/         petnna_product_manager.py
```

---

## 🔧 환경 설정

### 필수 API 키 (.env)
```
GEMINI_API_KEY          # Google AI
TELEGRAM_BOT_TOKEN      # 텔레그램 봇
TELEGRAM_CHAT_ID
NOTION_API_KEY          # 보고서 발행
NOTION_DATABASE_ID
SUPABASE_URL            # 펫과나 DB
SUPABASE_ANON_KEY
```

클라우드 LLM은 유료 API가 아니라 구독 CLI(`claude -p`/`codex exec`) + Gemini 무료 할당량을 쓴다
— 자세한 내용은 `CLAUDE.md`의 AI Model Strategy 섹션 참고.

### 보안
- 원본 `.env`는 Git 제외
- 암호화 저장: `.env.encrypted`
- 복호화: `python projects/ai-team/_shared/env.py decrypt .env.encrypted .env.decrypted`

---

## 📝 로그 위치

```
output/bot_logs/                            # 봇/데몬 stdout·stderr
output/qa/petnna/                           # 봄이·수리·테오·백호·미오·나무 산출물
reports/status/                             # 하네스 상태
```
