# CLAUDE.md — 현재 운영 요약

이 파일은 과거 Claude 중심 운영 문서의 축약판이다. 상세 사고 이력과 오래된 런북은 Git history와 `HANDBOOK.md`에 보존되어 있다.

## 현재 사실
- 운영 기계: macOS
- 시크릿: 루트 `.env` 단일 사용, 커밋 금지
- 공용 모듈: `projects/ai-team/_shared/`
- 에이전트 스킬: `projects/ai-team/skills/<agent>/SKILL.md`
- 정시 잡: `projects/ai-team/skills/영숙_비서/tools/schedules.json`
- 공통 규칙: `DIRECTIVES.md`, `AGENTS.md`

## Claude 사용 정책
현재 사용자는 Claude 구독을 운영하지 않는다.

- AutoDev v2에서 Claude를 호출하지 않는다.
- 공용 LLM fallback에서도 Claude를 기본 경로로 사용하지 않는다.
- 과거 `run_claude`, `claude -p`, Claude Max를 전제로 한 기록은 레거시다.
- 특정 오래된 도구가 Claude를 직접 요구하면 자동 반복하지 말고 다른 지원 경로를 사용하거나 명시적으로 실패시킨다.

## 재와별 AutoDev v2
재와별 자율개발의 현재 원천은 다음이다.
- `projects/autodev-v2/CORE_RULES.md`
- `projects/autodev-v2/KNOWLEDGE.md`
- `projects/autodev-v2/config.json`
- `output/autodev_v2/ashes-to-stars/state.json`

모델 흐름:
`Grok → Grok 재시도 → Codex 최대 1회 → STOP`

다음 작업은 Director가 큐가 빌 때만 묶음으로 만들고, 그 뒤 선택은 로컬 Task Queue가 한다. `game_council.py`, 역할별 `game_agents.py`, `ORDERS.md`, `GAME_WORKLOG.md`, `autopilot_stop_hook.py`는 AutoDev v2 기본 루프가 아니다.

## 기본 개발 원칙
- 실제 기능 완성 우선
- 필요한 파일만 읽기
- 최소 변경
- AI가 필요 없는 상태 확인은 로컬 코드 사용
- 완료는 실행/테스트 결과로 판정
- 같은 실패 반복 금지
- 관련 없는 리팩터링·문서 갱신 금지

## 안전
- force-push 금지
- 기존 미커밋 변경 되돌리기 금지
- 사용자 프로세스 강제 종료 금지
- 외부 게시·배포·구매는 명시적 지시 없이는 금지
- 시크릿 출력·커밋·전송 금지

세부 운영 사고를 조사해야 할 때만 `HANDBOOK.md` 또는 Git history를 필요한 범위로 검색한다. 매 작업 전에 전체를 읽지 않는다.
