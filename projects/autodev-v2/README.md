# AutoDev v2 — 재와별 자율개발 코어

기존 AutoDev의 **회의 → 상시 감사 → 여러 모델 중복 호출 → 턴 강제연장 → 문서 반복 로드** 구조를 사용하지 않습니다.

## 현재 구조
`Director(Grok, 가끔)` → `Task Queue(로컬)` → `Worker(Grok)` → `Unity 검증(로컬)` → 실패 시 `Grok 재시도` → 그래도 실패할 때만 `Codex 1회` → STOP.

Claude는 AutoDev v2에서 사용하지 않습니다.

## 지침·지식·상태를 분리
- 행동 규칙: `projects/autodev-v2/CORE_RULES.md`
- 안정 지식: `projects/autodev-v2/KNOWLEDGE.md`
- 동적 진행상태/작업 큐: `output/autodev_v2/ashes-to-stars/state.json`
- 게임 기획 권위: `docs/GAME_DESIGN_ASHES_TO_STARS.md`

과거 `GAME_WORKLOG.md`, `GAME_DEV_HANDOFF.md`, `ORDERS.md`, 회의록은 자동 컨텍스트가 아닙니다. 필요한 과거 사실을 조사할 때만 부분적으로 참고합니다.

## 토큰 절약 장치
- Director는 큐가 빌 때만 4~6개 작업을 생성
- 매 작업은 새 Grok 세션
- memory/subagent/자동 웹검색 비활성
- 관련 파일 후보 최대 5개
- 컨텍스트 기본 최대 12K 문자
- Unity 컴파일은 로컬 도구로 검증
- 같은 실패 반복 시 조기 중단
- 한 실행 최대 6작업 / 클라우드 최대 10호출
- Worker의 STATUS/WORKLOG/회의록 작성 금지
- 프로젝트 전체 자동 첨부 금지

## 원클릭 실행
Grok Build CLI가 로그인된 상태에서:

```bash
python3 projects/autodev-v2/start.py
```

`start.py`는 먼저 v1의 게임 회의/상시 감사 스케줄을 백업 후 비활성화하고 macOS launchd를 동기화한 뒤 v2 연속 루프를 시작합니다.

## 수동 명령
상태 확인:

```bash
python3 projects/autodev-v2/autodev.py status
```

작업 하나:

```bash
python3 projects/autodev-v2/autodev.py run
```

예산 상한까지 연속 개발:

```bash
python3 projects/autodev-v2/autodev.py run --continuous
```

막힌 작업 재개:

```bash
python3 projects/autodev-v2/autodev.py unblock T0001
```

v1 전환만 별도로:

```bash
python3 projects/autodev-v2/migrate_v1.py --apply
```

## 핵심 운영 철학
AutoDev v2의 자율성은 **AI를 계속 돌리는 것**이 아닙니다.

프로그램이 다음 작업 선택, 파일 후보 검색, Git 상태, 컴파일/테스트를 계속 처리하고, **새로운 판단이나 코드 작성이 필요한 순간에만 AI를 호출**합니다.

기본 모델 흐름은 `Grok → Grok 재시도 → Codex 1회 → STOP`이며 여러 모델에게 같은 문제를 동시에 묻지 않습니다.
