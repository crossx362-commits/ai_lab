# AGENTS.md

이 저장소의 비-Claude AI 도구 공통 지침이다. **필요한 것만 읽고 필요한 것만 고친다.**

## AutoDev v2 예외
`projects/autodev-v2/` 또는 재와별 자율개발 작업은 이 파일보다 아래 두 파일을 우선한다.
- `projects/autodev-v2/CORE_RULES.md`
- `projects/autodev-v2/KNOWLEDGE.md`

AutoDev v2에서는 루트 `CLAUDE.md`, `HANDBOOK.md`, 과거 회의록, `ORDERS.md`, `GAME_WORKLOG.md`를 자동 정독하지 않는다. 필요성이 확인된 부분만 찾아 읽는다.

## 기본 원칙
- 요청과 직접 관련된 파일만 읽는다. 광범위 스캔은 명확한 필요가 있을 때만.
- 기존 패턴을 유지하고 최소 변경한다.
- 관련 없는 리팩터링·정리·문서 갱신을 하지 않는다.
- 사용자의 기존 미커밋 변경을 되돌리지 않는다.
- 완료 선언은 실제 테스트/실행/검증 후에만 한다.
- 같은 실패를 근거 없이 반복하지 않는다.

## 모델 정책
- AutoDev v2: Grok 기본 → Grok 재시도 → Codex 최대 1회 → STOP.
- Claude는 AutoDev v2에서 사용하지 않는다.
- 같은 작업을 여러 모델에 동시에 보내지 않는다.
- 파일 검색, Git 상태, 로그 필터, 컴파일/테스트는 가능한 한 로컬 도구로 처리한다.

## 저장소 핵심 사실
- 운영 기계: macOS
- 공용 모듈: `projects/ai-team/_shared/`
- 에이전트 스킬: `projects/ai-team/skills/<agent>/SKILL.md`
- 정시 잡 원천: `projects/ai-team/skills/영숙_비서/tools/schedules.json`
- 런타임 로그·QA 산출물: `output/`
- 시크릿: 루트 `.env`만 사용, 커밋 금지

## 안전
1. 시크릿/API 키/쿠키/개인키를 커밋·출력·외부 전송하지 않는다.
2. force-push 금지.
3. 자동 병합 실패 시 중간 상태를 방치하지 않는다.
4. 사용자 프로세스, 특히 오너가 연 Unity를 임의 종료하지 않는다.
5. 외부 게시·배포·결제·계정 변경은 명시적 지시 없이는 하지 않는다.
6. `_shared/` 변경은 영향 범위가 크므로 관련 회귀 검사를 수행한다.

## Git
- 필요한 파일만 add/commit한다. `git add -A`를 기본으로 쓰지 않는다.
- 커밋 전 diff가 예상 범위인지 확인한다.
- AutoDev v2 Worker는 commit/push를 하지 않는다. 상태 관리는 v2 코어가 맡는다.

## 검증
- 재와별 빠른 C# 검사: `projects/ai-team/skills/마루_게임개발/tools/game_compile_check.py`
- 재와별 전체 빌드/실행: `projects/ai-team/skills/마루_게임개발/tools/game_build_verify.py`
- 전체 빌드는 필요한 경우에만 사용한다.

상세 사고 이력은 Git history와 `HANDBOOK.md`에 남아 있다. 매 작업 컨텍스트로 자동 적재하지 않는다.
