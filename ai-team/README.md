# AI Team — 1인 AI 기업 자동화 에이전트

반려동물 콘텐츠 채널 운영부터 인스타그램 마케팅, 비즈니스 전략, 개발·인프라까지  
**9명의 전문 AI 에이전트**가 역할 분담하여 자동 운영합니다.

---

## 🤖 에이전트 팀

| 이름 | 역할 | 핵심 담당 |
|------|------|-----------|
| **예원** (CEO) | 총괄 오케스트레이터 | 작업 분배·에이전트 라우팅·종합 보고·스킬 감사 |
| **루나** (디렉터) | AI 음악·영상 디렉터 | 시티팝 BGM(Lyria 3) 생성·Veo 영상 합성·YouTube 예약 업로드 |
| **아린** (관리자) | 인스타그램 총괄 | 구글 트렌드 분석·Gemini 이미지 생성·Graph API 자동 포스팅 |
| **가희** (검수관) | 콘텐츠 품질 검수 | YouTube MV 사전/사후 심사·금지 키워드 차단·중복 제목 수정 |
| **영숙** (비서) | 개인 비서 | 텔레그램 보고·구글 캘린더 일정·YouTube 추천·업로드 현황 점검 |
| **현빈** (전략가) | 비즈니스 전략 | 시장 트렌드 분석·경쟁사 리서치·PayPal 매출 모니터링 |
| **코다리** (개발자) | 풀스택 개발 | Vite·React·TypeScript 프로젝트 초기화·PWA·Ollama 진단·자동 수복 |
| **경수** (수사관) | 사이버 보안 | 악성 댓글 탐지·Google Sheets 증거 아카이브·코드 보안 감사 |
| **티모** (디자이너) | UI/UX 디자인 | 인터페이스 품질 검수·리서치 기반 디자인 방향·썸네일 전략 |

---

## 🔄 자동화 파이프라인

```
[매일 자동 실행]

루나  → YouTube 트렌드 수집 → BGM 생성(Lyria) → 영상 합성(Veo) → 가희 검수 → 19:00 예약 업로드
아린  → 구글 트렌드 분석 → 이미지 생성(Gemini) → 인스타그램 자동 포스팅
영숙  → 업로드 현황 점검 → 누락 파이프라인 실행 → 텔레그램 보고
코다리 → 텔레그램 봇·Ollama 상태 진단 → 이상 시 자동 수복
경수  → 댓글 모니터링 → 악성 탐지 시 아카이브·보고
```

---

## 🏗 프로젝트 구조

```
ai-team/
├── .agent/
│   ├── skills/          # 에이전트별 SKILL.md
│   │   ├── 예원/
│   │   ├── 루나/
│   │   ├── 아린/
│   │   ├── 가희/
│   │   ├── 영숙/
│   │   ├── 현빈/
│   │   ├── 코다리/
│   │   ├── 경수/
│   │   └── 티모/
│   ├── memory/          # 에이전트 학습 메모리
│   └── tools/           # 공용 실행 도구
├── _shared/             # 공유 모듈
│   ├── env_loader.py    # 환경변수 중앙화
│   ├── telegram_notifier.py
│   ├── ollama_client.py
│   ├── gemini_client.py
│   ├── 공통_스킬_지식.md
│   ├── 멀티에이전트_토론_스킬.md
│   └── skill-creator.md
└── assets/tool-seeds/   # 에이전트별 실행 스크립트
    ├── 루나_디렉터/
    ├── 아린_관리자/
    ├── 가희_검수관/
    ├── 영숙_비서/
    ├── 현빈_전략가/
    ├── 코다리_개발자/
    ├── 경수_수사관/
    └── 예원_CEO/
```

---

## 🛠 기술 스택

| 분류 | 기술 |
|------|------|
| AI 모델 (로컬) | Ollama · DeepSeek (코딩) · Gemma (일반) |
| AI 모델 (클라우드) | Google Gemini API · Lyria 3 Pro · Veo 3.1 |
| 알림 | Telegram Bot API |
| 콘텐츠 | YouTube Data API v3 · Instagram Graph API v23.0 |
| 결제 모니터링 | PayPal API |
| 일정 | Google Calendar API |
| 인프라 | Python 3.12 · macOS |

---

## ⚙️ 환경 설정

`.env` 파일을 프로젝트 루트에 생성:

```env
GEMINI_API_KEY=...
YOUTUBE_API_KEY=...
INSTAGRAM_ACCESS_TOKEN=...
INSTAGRAM_ACCOUNT_ID=...
TELEGRAM_BOT_TOKEN=...
TELEGRAM_CHAT_ID=...
```

---

## 📡 보고 체계

```
모든 에이전트 → 영숙(필터링) → 예원 CEO → 사장님
긴급 이슈    → 예원 즉시 보고 → 사장님
```

---

## 🧠 공유 스킬

모든 에이전트가 공통으로 보유한 스킬:

- **멀티에이전트 토론** — 역할 분담 협업으로 복잡한 문제 해결
- **Mermaid 다이어그램** — 업무 흐름·시스템 구조 시각화
- **Communication Coach** — 텍스트 초안 검토·톤 조정
- **Skill Creator** — 새 스킬 제작·기존 스킬 개선 (`_shared/skill-creator.md`)
- **Game-Changing Features** — 10x 성장 기회 발굴 전략 (일부 에이전트)
