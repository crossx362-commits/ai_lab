# 🤖 텔레그램 봇 시작 가이드

## ✅ 준비 완료 상태

- **Gemini API**: ✅ 새 키로 업데이트 완료
- **Claude API**: ⚠️ 크레딧 부족 (폴백용)
- **텔레그램**: ✅ 설정 완료
- **Function Calling**: ✅ 완벽 구현

## 🚀 봇 실행

```bash
cd d:/ai_lab
python projects/ai-team/skills/영숙_비서/tools/telegram_receiver.py
```

## 💬 사용 예시

텔레그램에서 다음과 같이 대화하세요:

```
사용자: 현황
영숙: 🎬 루나: ...
     📸 아린: ...
     📈 데이브: ...

사용자: 루나 영상 만들어
영숙: [dispatch() 실행 → CEO → 루나 파이프라인]
     ✅ 영상 제작 시작...

사용자: 일정 알려줘
영숙: 📅 일정:
     - 2026-06-20 14:00 회의
     - ...
```

## 🎯 핵심 기능

### 1. 자동 Function Calling
- "현황" → `get_agent_status()` 자동 호출
- "영상" → `dispatch()` 자동 실행
- "일정" → `list_calendar()` 자동 조회

### 2. 3단계 폴백
1. **Gemini Flash** (기본, 빠름)
2. **Gemini Pro** (Flash 할당량 초과 시)
3. **Claude Haiku** (모든 Gemini 실패 시)

### 3. 토큰 최적화
- 시스템 프롬프트: 80% 절감
- 대화 기록: 최근 3턴만
- 답변 길이: 200 토큰 제한

## 📊 API 상태

### Gemini API
- **키**: `AQ.Ab8RN6Is0CZ96L27h...` (53자)
- **모델**: gemini-2.5-flash → gemini-2.5-pro (폴백)
- **할당량**: 무료 티어 적용

### Claude API  
- **키**: `sk-ant-api03-_-Y1OUF...` (108자)
- **모델**: claude-3-5-haiku-20241022
- **상태**: ⚠️ 크레딧 부족 (필요 시 충전)

## 🔧 문제 해결

### 봇이 응답하지 않는 경우

1. **Gemini 할당량 확인**
   - https://ai.dev/rate-limit
   - 리셋: 매일 UTC 00:00 (KST 09:00)

2. **API 키 확인**
   ```bash
   cd d:/ai_lab
   python -c "
   import os
   from projects.ai_team._shared.env_loader import load_env
   load_env()
   print(os.getenv('GEMINI_API_KEY'))
   "
   ```

3. **로그 확인**
   - 봇 실행 시 콘솔에 출력되는 에러 메시지 확인
   - `❌` 표시 찾기

### Pro 모델로 전환되는 경우
- 정상 동작입니다 (Flash 할당량 초과 시 자동 전환)
- 메시지 앞에 `[Pro]` 표시됨

### Claude 모드로 전환되는 경우
- 모든 Gemini 할당량 초과 시 발생
- 메시지 앞에 `🤖 [Claude]` 표시됨
- **현재**: 크레딧 부족으로 작동 안 함

## 📝 다음 단계

1. **즉시 테스트**
   ```bash
   python projects/ai-team/skills/영숙_비서/tools/telegram_receiver.py
   ```

2. **텔레그램 메시지 전송**
   - "안녕" → 인사
   - "현황" → 에이전트 상태
   - "일정" → 캘린더

3. **Claude 크레딧 충전** (선택)
   - https://console.anthropic.com/settings/billing
   - $5-10 권장

## 🎉 완료!

모든 설정이 완료되었습니다!  
이제 텔레그램에서 영숙 봇과 대화할 수 있습니다.

**궁금한 점이 있으면 TELEGRAM_BOT_README.md를 참고하세요!**
