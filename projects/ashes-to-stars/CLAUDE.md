# 재와 별 — AutoDev v2 프로젝트 지침

이 파일은 `projects/ashes-to-stars/` 아래 작업의 **짧은 프로젝트 규칙**이다. 과거의 회의/WORKLOG/ORDERS/상시감사 운영 방식은 더 이상 기본 경로가 아니다.

## 권위 순서
1. 사용자의 현재 직접 지시
2. `docs/GAME_DESIGN_ASHES_TO_STARS.md`의 확정 사항
3. 관련 `docs/GAME_SPEC_*.md`
4. `projects/autodev-v2/CORE_RULES.md`
5. 현재 코드와 실제 검증 결과

문서와 코드가 다르면 현재 작업과 직접 관련된 차이만 확인한다. 전체 정합성 감사를 시작하지 않는다.

## 현재 개발 구조
- 자율개발 코어: `projects/autodev-v2/`
- 다음 작업 결정: Grok Director가 큐가 비었을 때만 4~6개 생성
- 다음 작업 선택: 로컬 Task Queue
- 구현: Grok Worker
- Grok 동일 작업 최대 2회
- 실패 시 Codex 최대 1회
- Claude 사용 안 함
- 상태 단일 진실원천: `output/autodev_v2/ashes-to-stars/state.json`

`output/qa/ashes-to-stars/ORDERS.md`, `docs/GAME_WORKLOG.md`, 과거 회의록은 AutoDev v2의 실행 큐나 진행상태 원천이 아니다. 필요할 때 사람이 참고하는 역사 자료다.

## 작업 원칙
- 한 번에 플레이 가능한 기능 하나를 앞으로 보낸다.
- 관련 파일만 읽고 최소 변경한다.
- 프로젝트 전체 스캔, 주변 리팩터링, 상태문서 갱신, 전체 아트 통일을 자동으로 시작하지 않는다.
- 제공된 작업의 완료 조건을 만족하면 더 개선하지 않고 종료한다.
- 이미 있는 기능을 다시 만들지 않는다.
- 사용자의 기존 미커밋 변경을 되돌리지 않는다.

## Unity
- 기본 프로젝트: `projects/ashes-to-stars/unity`
- 빠른 C# 검사: `projects/ai-team/skills/마루_게임개발/tools/game_compile_check.py`
- 전체 빌드/실행 검증: `projects/ai-team/skills/마루_게임개발/tools/game_build_verify.py`
- 전체 빌드는 milestone 또는 실제 런타임 확인이 필요한 경우에만 한다.
- 오너가 직접 열어둔 Unity를 강제 종료하지 않는다.
- 에디터 락 때문에 검증이 불가능하면 강제 종료 대신 검증을 보류하거나 안전한 사본/로컬 검사 경로를 쓴다.

## Git
- AutoDev Worker는 commit/push를 하지 않는다.
- force-push 금지.
- 다른 작업의 변경을 되돌리거나 한꺼번에 정리하지 않는다.
- 완료 기록은 AutoDev v2 state가 담당한다.

## 아트
- 실제 코드 소비처가 있는 리소스만 만든다.
- 기존 리소스가 있으면 중복 생성하지 않는다.
- 모델/백엔드 이름은 `art/aigen.py` 현재 설정을 단일 진실원천으로 삼고 문서에 오래된 별칭을 복제하지 않는다.
- 대량 재생성이나 전역 스타일 통일은 현재 작업이 명시적으로 요구할 때만 한다.

## 금지된 v1 기본 루프
다음은 코드/역사 기록으로 남아 있어도 AutoDev v2가 자동 호출하지 않는다.
- `game_council.py` 정기 회의
- 역할별 `game_agents.py` 상시 감사
- `autopilot_stop_hook.py` 턴 강제연장
- `ORDERS.md` 기반 무한 진행
- 작업마다 WORKLOG/STATUS 갱신

필요한 사고 이력이나 과거 세부 규칙은 Git history와 기존 문서에서 찾아볼 수 있지만, 매 작업의 기본 컨텍스트로 싣지 않는다.
