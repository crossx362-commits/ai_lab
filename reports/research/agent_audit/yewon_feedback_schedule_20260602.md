# 📊 예원 CEO — 일일 피드백 자동화 가이드
**작성일**: 2026-06-02  
**에이전트**: 예원 (CEO)  
**기능**: Instagram·YouTube 콘텐츠 성과 자동 평가 및 보고

---

## 🎯 개요

예원 CEO가 매일 자동으로 Instagram(아린)과 YouTube(루나)의 콘텐츠 성과를 평가하고, 텔레그램으로 보고서를 전송합니다.

### 평가 기준
| 플랫폼 | 성공 기준 | 실패 기준 |
|--------|----------|----------|
| YouTube | 조회수 10,000+ | 조회수 10,000 미만 |
| Instagram | 모든 포스팅 (조회수 API 미지원) | — |

---

## 📅 스케줄

### 일일 평가 (매일 09:00 KST)
```python
# 실행 내용:
1. 어제~오늘 업로드된 콘텐츠 성과 수집
2. YouTube 조회수·좋아요·댓글 수 조회
3. 성공/실패 판정 → 보상/패널티 로그 기록
4. Ollama로 인사이트 분석
5. 텔레그램 일일 보고서 전송
```

### 주간 리포트 (매주 월요일 10:00 KST)
```python
# 실행 내용:
1. 지난 7일간 전체 성과 집계
2. 요일별 성공률 분석 → 최적 요일 도출
3. 평균 조회수 추이 분석
4. 텔레그램 주간 리포트 전송
```

---

## 🚀 실행 방법

### 1. 즉시 실행 (테스트)

#### 일일 평가
```bash
cd d:\ai_lab\projects\ai-team\skills\예원_CEO\tools
python daily_feedback_scheduler.py --daily
```

#### 주간 리포트
```bash
python daily_feedback_scheduler.py --weekly
```

### 2. 백그라운드 데몬 실행 (권장)

#### Windows
```powershell
# pythonw로 백그라운드 실행 (콘솔 숨김)
cd d:\ai_lab\projects\ai-team\skills\예원_CEO\tools
Start-Process pythonw -ArgumentList "daily_feedback_scheduler.py --daemon" -WindowStyle Hidden

# 또는 Task Scheduler 등록
```

#### Linux/macOS
```bash
cd /path/to/ai-team/skills/예원_CEO/tools
nohup python daily_feedback_scheduler.py --daemon > feedback_scheduler.log 2>&1 &

# 또는 systemd 서비스 등록
```

### 3. 프로세스 확인

#### Windows
```powershell
Get-Process python* | Where-Object { 
    (Get-WmiObject Win32_Process -Filter "ProcessId = $($_.Id)").CommandLine -match "daily_feedback_scheduler" 
}
```

#### Linux/macOS
```bash
pgrep -af daily_feedback_scheduler.py
```

---

## 📊 보고서 형식

### 일일 보고서 예시

```
📊 **예원 CEO — 일일 콘텐츠 성과 보고**
날짜: 2026-06-02

━━━━━━━━━━━━━━━━━━━━

📈 **전체 성과**
• 총 평가: 3개
• ✅ 성공: 2개 (67%)
• ❌ 실패: 1개 (33%)

━━━━━━━━━━━━━━━━━━━━

📺 **YouTube (루나)**
• 성공: 1개
• 상위 콘텐츠:
  1. Neon Bloom (12,345 조회수)

📸 **Instagram (아린)**
• 포스팅: 1개

⚠️ **개선 필요**
• 조회수 미달: 1개
  - City Dreams (8,500 조회수)

━━━━━━━━━━━━━━━━━━━━

💡 **Ollama 인사이트**
성공한 콘텐츠는 "Neon"처럼 시각적 키워드가 강함.
실패 콘텐츠는 제목이 다소 포괄적. 
내일은 구체적인 감성 키워드 강화 권장 (예: Golden Hour, Twilight).
```

### 주간 리포트 예시

```
📊 **예원 CEO — 주간 콘텐츠 리포트**
기간: 2026-05-26 ~ 2026-06-02

━━━━━━━━━━━━━━━━━━━━

📈 **주간 성과**
• 총 콘텐츠: 14개
• YouTube: 7개
• Instagram: 7개
• 성공률: 71%

📺 **YouTube 분석**
• 총 조회수: 105,234
• 평균 조회수: 15,033
• 상위 콘텐츠: 23,456 조회

📅 **최적 업로드 요일**
• 금요일 (성공률: 100%)

━━━━━━━━━━━━━━━━━━━━

💡 **다음 주 권장 사항**
1. 금요일 집중 업로드
2. 평균 조회수 18,000 목표 설정
3. 성공 패턴 분석 후 재활용
```

---

## 💾 파일 구조

### 평가 로그
```
reports/
├── learning/
│   └── optimal_time_cache.json          # 최적 시간 캐시
└── history/
    └── upload_history.json              # 업로드 히스토리

.agent/memory/
├── reward/
│   └── success_log.jsonl                # 성공 로그 (조회수 1만+)
└── punishment/
    └── fail_log.jsonl                   # 실패 로그 (조회수 1만 미만)
```

### 로그 형식 (JSONL)

#### success_log.jsonl
```json
{
  "agent": "루나",
  "platform": "youtube",
  "title": "Neon Bloom",
  "video_id": "abc123xyz",
  "video_url": "https://youtu.be/abc123xyz",
  "prompt_used": "1980s Retro K-Pop & City Pop Fusion...",
  "views": 12345,
  "likes": 234,
  "comments": 56,
  "feedback_date": "2026-06-02 09:00",
  "conclusion": "성공. 해당 프롬프트/테마를 다음 기획에 재활용할 것."
}
```

#### fail_log.jsonl
```json
{
  "agent": "루나",
  "platform": "youtube",
  "title": "City Dreams",
  "video_id": "xyz789abc",
  "video_url": "https://youtu.be/xyz789abc",
  "prompt_used": "City Pop...",
  "views": 8500,
  "likes": 123,
  "comments": 34,
  "feedback_date": "2026-06-02 09:00",
  "conclusion": "조회수 기준 미달. 다음에는 시티팝 멜로디의 템포감 및 비주얼의 복고 감성을 더 보강하여 교정할 것."
}
```

---

## 🧠 Ollama 인사이트 분석

### 분석 항목
1. **성공 패턴**: 제목 스타일, 키워드, 테마 공통점
2. **실패 원인**: 개선 필요 요소
3. **내일 권장 사항**: 구체적 개선 방향 (1~2가지)

### 예시 프롬프트
```python
prompt = """
다음은 오늘의 콘텐츠 성과입니다:

**전체**: 3개 | **성공률**: 67%

**성공한 콘텐츠** (조회수 1만+ 또는 Instagram):
- Neon Bloom
- Golden Hour Memories

**실패한 콘텐츠** (조회수 1만 미만):
- City Dreams

**분석 요청**:
1. 성공한 콘텐츠의 공통 패턴 (제목 스타일, 키워드 등)
2. 실패한 콘텐츠의 개선 방향
3. 내일 콘텐츠 제작 시 주의사항 (1~2가지)

짧고 구체적으로 3~5줄 이내로 답변해줘.
"""
```

### Ollama 응답 예시
```
성공한 콘텐츠는 "Neon", "Golden Hour"처럼 시각적 키워드가 강함.
실패 콘텐츠는 "City Dreams"처럼 다소 포괄적.
내일은 구체적인 시간대/색상 키워드 강화 권장 (예: Twilight, Sunset).
```

---

## 🔄 자동화 워크플로우

```
매일 09:00 KST
    ↓
① evaluate_feedback.py 실행
   - upload_history.json 읽기
   - YouTube API로 조회수 조회
   - 성공/실패 판정
   - success_log.jsonl / fail_log.jsonl 기록
    ↓
② 일일 보고서 생성
   - 최근 24시간 평가 결과 집계
   - 플랫폼별 성과 요약
   - Ollama 인사이트 생성
    ↓
③ 텔레그램 전송
   - 예원 → 사장님께 보고

매주 월요일 10:00 KST
    ↓
④ 주간 리포트 생성
   - 지난 7일 전체 집계
   - 요일별 성과 분석
   - 최적 업로드 요일 도출
    ↓
⑤ 텔레그램 전송
```

---

## 🛠️ 의존성

### Python 패키지
```bash
# YouTube API (선택)
pip install google-auth google-auth-oauthlib google-api-python-client

# 기본 (이미 설치됨)
# - _shared.telegram_notifier
# - _shared.ollama_client
```

### 환경변수
```bash
# .env
TELEGRAM_BOT_TOKEN=your_telegram_bot_token
TELEGRAM_CHAT_ID=your_chat_id
```

### OAuth 토큰 (YouTube API)
```
.agent/credentials/youtube_token.pickle
```

**없을 시**: YouTube 조회수는 0으로 처리 (경고만 출력)

---

## 🚨 에러 처리

### YouTube API 실패
```python
# 토큰 없음 또는 API 오류
→ 조회수 0으로 기록 (Warning 출력)
→ 평가는 계속 진행
```

### Ollama 미연결
```python
# lm_available() == False
→ 기본 인사이트 메시지: "Ollama 미연결로 자동 분석 불가"
→ 보고서는 생성됨 (통계만)
```

### 텔레그램 전송 실패
```python
# 토큰 오류 또는 네트워크 실패
→ 콘솔에 에러 출력
→ 로그는 정상 기록됨
```

---

## 📈 활용 방안

### 1. 콘텐츠 전략 개선
- 성공 패턴 분석 → 다음 콘텐츠에 재활용
- 실패 패턴 회피 → 개선 방향 반영

### 2. 최적 시간 결정
- 요일별 성공률 분석 → 집중 업로드 요일 선정
- 주간 트렌드 파악 → 시즌별 전략 수립

### 3. 에이전트 성과 추적
- 루나/아린 각각 성과 비교
- 장기 성장 추이 모니터링

### 4. 학습 데이터 축적
- success_log.jsonl → 성공 프롬프트 DB
- fail_log.jsonl → 회피 패턴 DB

---

## 🎯 향후 개선 계획

### 단기 (1~2주)
1. **Instagram 인사이트 연동**: Instagram Graph API로 도달률·인게이지먼트 수집
2. **실시간 알림**: 조회수 1만 돌파 시 즉시 텔레그램 알림
3. **A/B 테스트 지원**: 동일 테마 다른 제목 성과 비교

### 중기 (1개월)
1. **예측 모델**: 제목·키워드 입력 시 예상 조회수 예측
2. **자동 개선 제안**: 실패 콘텐츠 제목·프롬프트 자동 수정안 생성
3. **경쟁 분석**: 유사 채널 성과 비교

### 장기 (3개월)
1. **강화학습 적용**: 성공/실패 피드백으로 자동 최적화
2. **다중 플랫폼 확장**: TikTok, Twitter 등 추가
3. **대시보드 구축**: 웹 기반 실시간 성과 대시보드

---

## 📚 관련 파일

| 파일 | 용도 |
|------|------|
| `evaluate_feedback.py` | 기존 평가 스크립트 (단발 실행) |
| `daily_feedback_scheduler.py` | 신규 스케줄러 (자동화) |
| `upload_manager.py` | 업로드 현황 점검 (영숙이 사용) |

---

## 🔗 참고 문서

- [에이전트 검수 보고서](./agent_audit_20260602.md)
- [루나 워크플로우](./arin_workflow_20260602.md) (루나 문서는 별도 작성 필요)
- [아린 워크플로우](./arin_workflow_20260602.md)
- [시스템 상태](./system_status_20260602.md)

---

**마지막 업데이트**: 2026-06-02  
**다음 단계**: 백그라운드 데몬 실행 및 Task Scheduler 등록 권장
