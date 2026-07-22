# 🤖 영숙 텔레그램 봇 가이드 (TELEGRAM_BOT_README.md)

Gemini Function Calling 및 최적화된 우회 로직을 적용한 전담 비서 **영숙** 텔레그램 봇 문서입니다.

---

## 🚀 봇 실행 방법

### 데몬 제어 (권장)
```bash
python projects/ai-team/skills/영숙_비서/tools/agent_controller.py 영숙 start
```

### 수동 실행 (터미널 디버깅용)
콘솔 로그를 보며 실시간으로 작동을 테스트할 때 사용합니다:
```bash
python projects/ai-team/skills/영숙_비서/tools/telegram_receiver.py
```

---

## 💬 텔레그램 명령어 예시
텔레그램에서 다음과 같이 자연스럽게 입력하면 봇이 알아서 도구를 실행합니다.

* **"현황 보고해줘" / "다들 뭐해?"**
  * `get_agent_status()`가 호출됩니다.
  * 요약 필터를 거치지 않고 **실존 에이전트 전체(예원·영숙 + 펫나 QA/개발팀 6명)의 현황(HTML 서식)**을 잘림 없이 상세하게 전송합니다.
* **"일정 알려줘" / "캘린더 확인해봐"**
  * `list_calendar()`를 통해 연동된 구글 캘린더 캐시 일정을 조회합니다.
* **"에이전트 작업 요청"**
  * `dispatch()` -> CEO `yewon_dispatcher`를 호출하여 작업을 할당합니다.

---

## ⚙️ 파일 구성 및 아키텍처
기존에 산재해 있던 중복 및 이전 테스트용 스크립트들을 모두 통합하여 단일 파일 관리 구조로 간소화하였습니다.

```
projects/ai-team/skills/영숙_비서/tools/
├── telegram_receiver.py      # 통합 텔레그램 봇 메인 코드
├── start_telegram_bot.ps1    # 봇 자동 백그라운드 구동 및 프로세스 관리 스크립트
├── calendar_manager.py       # [통합] 구글 캘린더 읽기(iCal)/쓰기 상태 검사
├── posting_scheduler.py      # [통합] 일일 포스팅 일정 / 매일 반복 업로드 일정 등록
├── reports_manager.py        # [통합] 로그 정리 및 Notion 리서치 리포트 발행
└── upload_approval_flow.py   # 콘텐츠 업로드 승인 및 확인 워크플로우
```

---

## 🔧 자율 실행 제어 및 수동 조치
* **시스템 부하 방지**:
  * 백그라운드 헬스체크 프로세스들이 좀비 프로세스로 남지 않도록 모두 강제 정리 완료했습니다.
