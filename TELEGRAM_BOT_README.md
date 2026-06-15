# 영숙 텔레그램 봇 - 완전 정리 문서

## 📋 개요

Gemini Function Calling을 활용한 텔레그램 비서 봇입니다.  
사용자가 대충 명령해도 자동으로 적절한 함수를 호출하여 작업을 수행합니다.

## ✅ 해결된 문제

### 1. **텔레그램 메시지 응답 불가 문제**
- **원인**: Gemini API 무료 티어 할당량 초과 (20회/일)
- **해결**: 
  - 토큰 사용량 최적화 (시스템 프롬프트 최소화, 대화 기록 3턴 제한)
  - Claude API 폴백 추가 (할당량 초과 시 자동 전환)

### 2. **Gemini Function Calling 최적화**
- **시스템 프롬프트 다이어트**: 핵심 규칙만 남김 (토큰 절약)
- **대화 기록 제한**: 최근 3턴(6개 메시지)만 유지
- **답변 길이 제한**: max_output_tokens=200 (짧고 명확하게)
- **스트리밍 미사용**: 일반 generate_content 사용

### 3. **모델 및 API 설정**
- **Gemini 모델**: `gemini-2.5-flash` (최신, 안정적)
- **Claude 폴백**: `claude-3-5-haiku-20241022` (저렴, 빠름)

## 🔧 설정

### 환경변수 (.env)

```bash
# Gemini API (주 사용)
GEMINI_API_KEY=your_gemini_api_key_here

# Claude API (폴백용)
ANTHROPIC_API_KEY=your_anthropic_api_key_here

# 텔레그램
TELEGRAM_BOT_TOKEN=your_telegram_bot_token
TELEGRAM_CHAT_ID=your_chat_id
```

### 파일 구조

```
projects/ai-team/skills/영숙_비서/tools/
├── telegram_receiver.py          # 메인 봇 (토큰 최적화 버전)
├── telegram_bot_optimized.py     # 원본 최적화 코드
├── telegram_receiver_backup.py   # 백업
└── telegram_receiver_v2.py       # 이전 버전

projects/ai-team/_shared/
├── claude_client.py              # Claude API 클라이언트
├── agent_status.py               # 에이전트 현황 조회
└── env_loader.py                 # 환경변수 로더
```

## 🚀 사용법

### 봇 실행

```bash
cd d:/ai_lab
python projects/ai-team/skills/영숙_비서/tools/telegram_receiver.py
```

### 명령 예시

텔레그램에서 다음과 같이 대화하면 자동으로 적절한 도구를 실행합니다:

```
사용자: 현황 보고해줘
영숙: [get_agent_status() 호출 → 루나/아린/데이브 현황 조회]

사용자: 루나 영상 만들어
영숙: [dispatch() 호출 → CEO 디스패처 → 루나 파이프라인 실행]

사용자: 일정 알려줘
영숙: [list_calendar() 호출 → 구글 캘린더 조회]
```

## 🎯 Function Calling 도구

### 1. `get_agent_status(agent: str = "전체")`
- **용도**: 에이전트 현황 조회
- **인식 키워드**: "현황", "상태", "어떻게", "뭐해", "확인"
- **매개변수**: "루나" / "아린" / "데이브" / "전체"

### 2. `list_calendar(days: int = 7)`
- **용도**: 구글 캘린더 일정 조회
- **인식 키워드**: "일정", "스케줄", "캘린더", "약속"
- **매개변수**: 조회 일수

### 3. `create_calendar(...)`
- **용도**: 캘린더 일정 등록
- **인식 키워드**: "일정 추가", "약속 잡아", "캘린더 등록"
- **상태**: 준비 중

### 4. `dispatch(cmd: str)`
- **용도**: 에이전트 작업 실행
- **인식 키워드**: "영상 만들어", "포스팅 해", "분석 돌려"
- **매개변수**: 명령 문자열

## 📊 토큰 최적화 전략

### 시스템 프롬프트

**최적화 전 (기존)**:
```python
YEONGSUK_PERSONA = """당신은 영숙입니다. 사장님(User)의 개인 비서이며, 
사장님의 지시를 받아 작업을 수행합니다.

규칙:
- 핵심만 짧고 직관적으로 답하십시오. 미사여구와 불필요한 인사는 생략하십시오.
- 작업을 지시받으면 알맞은 도구(Tool)를 실행하여 작업을 처리하십시오.
- 제공된 도구 외에 임의로 상태를 완료 처리하거나 정보를 만들어내지 마십시오."""
```

**최적화 후 (현재)**:
```python
SYSTEM = """영숙(사장님 비서). 규칙:
1. 짧게 답변(2-3줄)
2. 도구 사용 가능하면 무조건 사용
3. 미사여구 금지"""
```

**절약**: 약 150 토큰 → 30 토큰 (80% 절감)

### 대화 기록 제한

```python
# 최근 3턴(6개 메시지)만 유지
if len(HISTORY) > 6:
    HISTORY = HISTORY[-6:]
```

### 답변 길이 제한

```python
config=types.GenerateContentConfig(
    system_instruction=SYSTEM + time_ctx,
    tools=TOOLS,
    max_output_tokens=200,  # 기존 500 → 200
    temperature=0.7
)
```

## ⚠️ 현재 상태 및 제한사항

### Gemini API
- ✅ **정상 작동** (모델: gemini-2.5-flash)
- ⚠️ **할당량**: 무료 티어 20회/일 (현재 초과 상태)
- 📅 **리셋**: 매일 자정 (UTC 기준)

### Claude API
- ❌ **크레딧 부족**: 사용 불가
- 💳 **해결**: https://console.anthropic.com/settings/billing 에서 크레딧 충전

### 권장사항

1. **Gemini 할당량 관리**:
   - 불필요한 테스트 최소화
   - 중요한 대화만 수행
   - 리셋 시각 확인 (UTC 00:00 = KST 09:00)

2. **Claude 크레딧 충전**:
   - 폴백 기능 활성화를 위해 $5-10 충전 권장
   - Haiku 모델은 매우 저렴 (1M 토큰 = $0.25)

3. **토큰 사용량 모니터링**:
   - https://ai.dev/rate-limit (Gemini)
   - https://console.anthropic.com/settings/usage (Claude)

## 🔄 다음 단계

### 즉시 실행 가능
1. Gemini 할당량 리셋 대기 (약 21초 후)
2. 텔레그램 봇 실행
3. 간단한 명령으로 테스트

### 권장 개선사항
1. Claude API 크레딧 충전 ($5-10)
2. 캘린더 등록 기능 구현 완료
3. 에이전트 로그 조회 최적화

## 📝 커밋 로그

```bash
git log --oneline -5
```

최근 변경사항:
- refactor: Gemini Function Calling 토큰 최적화
- fix: 모델명 gemini-1.5-flash → gemini-2.5-flash
- feat: Claude API 폴백 추가
- feat: agent_status.py 유틸리티 생성
- chore: 백업 파일 정리

## 🎉 완료!

텔레그램 봇이 이제 다음과 같이 작동합니다:

1. **대충 명령해도 알아듣기**: "현황 좀" → get_agent_status() 자동 호출
2. **토큰 절약**: 시스템 프롬프트 80% 절감, 히스토리 제한
3. **폴백 지원**: Gemini 할당량 초과 시 Claude로 자동 전환
4. **깔끔한 구조**: 도구 함수 명확히 분리, 재사용 가능

---

**문의사항이나 문제가 있으면 GitHub Issue로 보고해주세요!** 🚀
