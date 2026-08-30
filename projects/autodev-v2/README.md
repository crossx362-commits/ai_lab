# AutoDev v2 — 재와별 자율개발 코어

기존 v1의 **회의 → 상시 감사 → 여러 모델 중복 호출 → 턴 강제연장 → 문서 반복 로드** 구조를 사용하지 않습니다.

## 정본 실행 구조

`대시보드 → engine.py 단일 PID → Grok Director → 로컬 Task Queue → Grok Worker → 로컬/Unity 검증 → 필요할 때만 Grok 재시도 → 마지막 Codex 1회`

- 계획은 Grok Director만 담당합니다. Ollama/Claude는 계획 루프에 없습니다.
- 대시보드는 `engine.py` 하나만 시작합니다.
- `start.py`는 예전 호출 호환용이며 즉시 `engine.py`로 위임합니다.
- `autodev.py run`도 예전 자체 루프를 실행하지 않고 현재 프로세스를 `engine.py`로 교체합니다.
- 상태/큐 관리 명령(`status`, `unblock`, `reset-queue`)은 `autodev.py`에 남습니다.

## 지침·지식·상태 분리

- 행동 규칙: `projects/autodev-v2/CORE_RULES.md`
- 안정 지식: `projects/autodev-v2/KNOWLEDGE.md`
- 진행상태/작업 큐: `output/autodev_v2/ashes-to-stars/state.json`
- 게임 기획 권위: `docs/GAME_DESIGN_ASHES_TO_STARS.md`

과거 WORKLOG/회의록은 자동 컨텍스트가 아닙니다.

## 안전장치

- 큐가 필요할 때만 Grok Director 호출
- 매 작업은 새 Worker 세션
- 관련 파일 후보 최대 5개, 컨텍스트 최대 12K 문자
- 중복 작업/같은 영역 반복을 로컬 코드로 차단
- 실패 작업의 변경만 rollback, 오너의 기존 미커밋 변경은 보존
- provider 한도/로그인/CLI 오류는 구현 실패 횟수로 계산하지 않음
- 한 배치 최대 6작업/10 cloud calls, 시간당 최대 12 calls
- Codex는 Grok 실패 뒤 작업당 최대 1회
- 완료는 컴파일 + 필요한 작업의 Unity Acceptance 통과가 기준

## Unity가 열려 있을 때

기본값 `implement_while_unity_locked=true`입니다. 오너가 Unity를 Play/편집 중이어도 구현은 진행할 수 있지만 **완료 판정은 보류**합니다.

검증 대기 구현은 기본 최대 2개(`max_waiting_verification_tasks=2`)만 쌓습니다. Unity 검증이 가능해지면 Grok/Codex를 다시 호출하기 전에 **검증만 먼저 재실행**합니다. 통과하면 완료, 실제 검증 실패일 때만 수리 Worker로 돌아갑니다.

## 실행

평소에는 AutoDev 대시보드에서 `개발 시작`을 사용합니다. CLI가 필요하면 정본은 다음입니다.

```bash
python3 projects/autodev-v2/engine.py
```

예전 명령도 호환됩니다. 둘 다 `engine.py`로 위임됩니다.

```bash
python3 projects/autodev-v2/start.py
python3 projects/autodev-v2/autodev.py run --continuous
```

상태와 큐 관리:

```bash
python3 projects/autodev-v2/autodev.py status
python3 projects/autodev-v2/autodev.py unblock T0001
python3 projects/autodev-v2/autodev.py reset-queue
```

## 테스트 원칙

CI와 회귀테스트는 실제 Grok/Codex 요청을 보내지 않습니다. provider 동작은 mock/fixture로 검증하고, 실제 AI 호출은 AutoDev 런타임에서만 일어납니다.
