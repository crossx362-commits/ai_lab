# AutoDev v2 — 재와별 자율개발 코어

기존 AutoDev의 문제였던 **회의 → 상시 감사 → 여러 모델 폴백 → 최대 20턴 강제연장** 구조를 사용하지 않습니다.

## 구조

`Director(Grok, 가끔)` → `Task Queue(로컬)` → `Worker(Grok)` → `Unity 검증(로컬)` → 실패 시 `Grok 재시도 1회` → 그래도 실패할 때만 `Codex 1회`.

Claude는 AutoDev v2에서 전혀 호출하지 않습니다.

## 토큰 절약 장치

- Director는 큐가 빌 때만 호출하고 한 번에 4~6개 작업을 생성합니다.
- 매 작업은 새 Grok headless 세션입니다. 이전 대화를 이어붙이지 않습니다.
- `--no-memory`, `--no-subagents`, `--disable-web-search`를 기본 적용합니다.
- 작업 파일 후보는 로컬 `rg`가 최대 5개로 좁힙니다.
- Unity 컴파일 검증은 기존 로컬 `game_compile_check.py`를 직접 실행합니다.
- 같은 검증 실패가 반복되면 Grok 재시도를 조기 종료합니다.
- 한 실행당 기본 최대 6작업 / 클라우드 12호출에서 강제 종료합니다.
- 문서/회의록/STATUS 작성은 Worker가 하지 않습니다.

## 가장 간단한 시작

Grok Build CLI에 한 번 로그인된 상태에서 아래 한 줄만 실행하면 됩니다.

```bash
python3 projects/autodev-v2/start.py
```

`start.py`가 먼저 기존 게임 회의/상시 감사 스케줄을 백업 후 비활성화하고, macOS launchd를 동기화한 다음 AutoDev v2 연속 루프를 시작합니다.

## 수동 전환

전환만 따로 하려면:

```bash
python3 projects/autodev-v2/migrate_v1.py --apply
```

이 명령은 기존 파일을 삭제하지 않고 `schedules.json`을 백업한 다음 게임 회의/상시 감사 잡만 비활성화합니다. macOS에서는 `schedule_sync.py sync`까지 실행해 기존 launchd 등록도 정리합니다.

## 개별 실행

Grok 확인:

```bash
grok --version
grok login
```

상태 확인:

```bash
python3 projects/autodev-v2/autodev.py status
```

작업 하나만:

```bash
python3 projects/autodev-v2/autodev.py run
```

예산 상한까지 자율 연속 개발:

```bash
python3 projects/autodev-v2/autodev.py run --continuous
```

막힌 작업을 다시 시도하려면:

```bash
python3 projects/autodev-v2/autodev.py unblock T0001
```

## 중요한 운영 원칙

AutoDev v2의 자율성은 "AI가 계속 생각한다"가 아니라 **로컬 프로그램이 계속 진행하고 판단이 필요한 순간에만 AI를 부른다**는 뜻입니다.

Director는 다음 작업을 한 번에 묶어서 정합니다. 그 뒤 다음 작업 선택, Git 상태, 파일 후보 검색, Unity 컴파일 판정은 모두 로컬에서 처리됩니다.

기본 모델 흐름은 `Grok → Grok 재시도 → Codex 1회 → STOP`입니다. 여러 모델에게 같은 문제를 동시에 묻지 않습니다.

## 설정

`projects/autodev-v2/config.json`

기본값:
- Grok 재시도: 작업당 최대 2회
- Codex: Grok 실패 후 최대 1회
- 관련 파일 후보: 최대 5개
- 한 실행: 최대 6개 작업
- 한 실행 클라우드 호출: 최대 12회
- milestone 전체 Unity 빌드: 기본 OFF

`full_verify_on_milestone=true`로 바꾸면 milestone 작업에서 `game_build_verify.py`까지 실행합니다. 전체 빌드는 느리고 Unity 에디터 락 영향이 있으므로 기본은 꺼져 있습니다.
