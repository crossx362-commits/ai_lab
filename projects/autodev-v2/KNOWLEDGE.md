# AutoDev v2 안정 지식

이 파일에는 **자주 바뀌지 않는 사실만** 둔다. 진행상태·다음 작업·실패 횟수는 여기에 쓰지 않고 `state.json`이 관리한다.

## 프로젝트
- 게임: 재와 별(Ashes to Stars)
- Unity 프로젝트: `projects/ashes-to-stars/unity`
- 운영 기계: macOS
- 기획 권위: `docs/GAME_DESIGN_ASHES_TO_STARS.md`
- 상세 스펙: `docs/GAME_SPEC_*.md`

## 개발 방식
- 핵심 플레이 루프와 실제 동작을 우선한다.
- 작은 문서 정리나 미관 개선보다 플레이 가능한 기능을 먼저 완성한다.
- 관련 파일만 읽고 최소 변경한다.
- 작업마다 새 모델 세션을 사용한다.

## 모델 정책
- 기본 개발자: Grok
- Grok 동일 작업 최대 2회
- 그 뒤 Codex 최대 1회
- Claude: 사용하지 않음
- 여러 모델에게 같은 작업을 동시에 보내지 않음

## 로컬 검증
- 빠른 C# 검사: `projects/ai-team/skills/마루_게임개발/tools/game_compile_check.py`
- 전체 빌드·실행 검증: `projects/ai-team/skills/마루_게임개발/tools/game_build_verify.py`
- 전체 빌드는 느리므로 milestone 또는 꼭 필요한 작업에서만 사용한다.
- 오너가 직접 연 Unity 프로젝트를 강제 종료하지 않는다.

## 컨텍스트 정책
- 프로젝트 전체 자동 첨부 금지
- 관련 파일 후보 최대 5개
- 오류 로그는 필요한 부분만
- `GAME_WORKLOG.md`, `ORDERS.md`, 회의록은 AutoDev v2의 상태 저장소가 아니다.
- 현재 작업 상태의 단일 진실원천은 `output/autodev_v2/ashes-to-stars/state.json`이다.

## 과거 시스템
`game_council.py`, 역할별 `game_agents.py`, `autopilot_stop_hook.py`는 v1 유산이다. 코드와 기록은 보존할 수 있지만 AutoDev v2의 다음 작업 결정이나 자동 연속개발에 사용하지 않는다.
