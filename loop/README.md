# 자율 개발 루프 — 운영 안내

매 바퀴마다 헤드리스 AI 세션을 새로 열고 `loop/PROMPT.md`에 따라 작업한다. 대화 세션은
이어 붙이지 않으며, 기억은 문서와 Git에 남긴다.

## 핵심 원칙

- 시작·중단·상태 확인은 모든 사람과 AI가 `loop/control.sh` 하나만 사용한다.
- `STOP`은 오너가 `control.sh stop`을 명시했을 때만 만든다. 루프·감시자·오류 처리기는 만들지 않는다.
- 사용량 한도에서는 다른 AI로 넘기지 않는다. 무료 사용량 조회만 반복하고 같은 AI가 회복되면 계속한다.
- 일반 오류는 지문당 복구 AI를 최대 한 번 호출한다. 같은 오류·같은 Git 상태에서는 AI를 다시 부르지 않는다.
- 정상 개발은 한 바퀴에 AI 세션 하나다. 자동 council, speed lane, 건강한 보드의 정기 AI 개선은 꺼져 있다.

## 시작·중단·상태

어떤 AI 대화에서든 셸을 사용할 수 있으면 아래 명령을 바로 실행하면 된다.

```bash
# 직전 실행기 또는 기본 실행기로 시작
bash loop/control.sh start

# 실행기 지정
bash loop/control.sh start claude
bash loop/control.sh start codex
bash loop/control.sh start grok
bash loop/control.sh start opencode

# 상태 확인과 오너 중단
bash loop/control.sh status
bash loop/control.sh stop
```

`start`는 `STOP`·`HOLD` 해제, 실행기 기록, launchd plist 등록, 중복 프로세스 방지,
PID 또는 heartbeat 확인을 한 번에 수행한다. 확인하지 못하면 성공으로 보고하지 않고
`recovering`으로 기록한다. 같은 `start`를 반복해도 프로세스를 중복 생성하지 않는다.

`stop`은 현재 바퀴가 끝난 뒤 정상 종료하도록 `STOP`을 만들고 상태를 `owner_stopped`로
기록한다. 직접 `touch loop/STOP`하거나 `loop.sh`를 백그라운드로 띄우지 않는다.

실행 상태를 바꾸지 않고 plist 파일만 설치하려면 다음을 사용한다.

```bash
bash loop/deploy_launchd.sh --register-only
# 이전 명령 호환: bash loop/deploy_launchd.sh --no-start
```

launchd는 Application Support의 복사본이 아니라 저장소의
`/Users/junholee/ai_lab/loop/loop.sh`를 직접 실행한다.

## 상태 의미

상태는 git에서 제외된 `loop/runtime_state.json`에 원자적으로 기록된다.

| 상태 | 의미 | 감시자 동작 |
|---|---|---|
| `running` | 정상 바퀴 실행 또는 다음 바퀴 대기 | PID와 heartbeat가 정상이면 유지 |
| `quota_wait` | 선택한 AI 사용량 회복 대기 | heartbeat가 살아 있으면 재시작하지 않음 |
| `recovering` | 오류 진단·수정 또는 같은 오류의 새 정보 대기 | 중복 복구 세션을 만들지 않음 |
| `owner_stopped` | 오너가 명시적으로 중단 | 아무것도 시작하지 않음 |

대기 중에도 heartbeat를 갱신한다. 감시자는 오래된 lap 로그 대신 상태·heartbeat·launchd PID를
보고, 서비스 소실이나 heartbeat 정체가 확인된 경우에만 `control.sh start`로 원본을 재기동한다.

## 비용 정책

- 한도 확인은 `board.py usage` 결과만 사용하며 시험 프롬프트를 보내지 않는다.
- 한도 대기 중 AI 호출은 0회다. 회복 후 선택했던 같은 AI로 개발 세션 하나를 연다.
- 오류 복구 입력은 종료 코드와 로그 끝 80줄로 제한한다.
- 같은 오류 지문은 Git HEAD나 실패 내용이 달라질 때까지 복구 AI를 다시 호출하지 않는다.
- `com.ailab.speedlane`은 `RunAtLoad=false`이고 KeepAlive가 없다. 감시자도 다시 켜지 않는다.
- council은 바퀴 수로 자동 소집하지 않는다. 큰 이정표나 오너의 명시 신호 `COUNCIL_NOW`에서만 연다.
- 보드 지킴이는 건강하면 로컬 검사만 수행한다. 실제 실패도 동일 지문에는 복구 AI 한 번만 호출한다.

## 주요 파일

| 파일 | 역할 |
|---|---|
| `control.sh` | 멱등 `start\|stop\|status` 단일 진입점 |
| `runtime_state.py` | 상태·heartbeat·오류 지문·복구 claim 원자 관리 |
| `loop.sh` | 정상 lap, 무료 한도 대기, 오류 복구 상태 머신 |
| `loop_watch.sh` | 상태·heartbeat·PID 기반 감시와 단일 경로 재개 |
| `env.sh` | 실행기·모델·대기 간격·바퀴 설정 |
| `PROMPT.md` | 정상 개발 세션 지시서 |
| `board.py` / `board.html` | 개발 보드(http://127.0.0.1:8766), 계속/끄기도 control 사용 |
| `board_keeper.sh` | 무료 보드 검사와 새 오류 지문 1회 수리 |
| `deploy_launchd.sh` | 저장소 원본을 가리키는 plist 등록 |
| `com.ailab.autonomous_loop.plist` | 메인 루프 비정상 종료 재기동 |
| `com.ailab.speedlane.plist` | 상시 병렬 레인 비활성 설정 |
| `logs/YYYY-MM-DD/lap-*.log` | 정상 바퀴·오류 로그 |

개발 기억의 원천은 `docs/DESIGN.md`, `docs/STATUS.md`, `docs/feedback/INBOX.md`다.

## 검증

아래 테스트는 임시 저장소와 가짜 AI/launchd만 사용하므로 실제 루프를 시작하지 않는다.

```bash
bash loop/test_control.sh
bash loop/test_loop_agent.sh
bash loop/test_loop_continuity.sh
bash loop/test_infra_detect.sh
bash loop/test_loop_watch.sh
bash loop/test_board_keeper.sh
python3 -m unittest loop/test_runtime_state.py -v
python3 loop/test_board.py
```

## 인수 기록

- **2026-08-28**: 단일 제어 명령, 상태·heartbeat, 같은 AI 한도 대기, 오류 지문당 1회 복구,
  저장소 원본 직접 실행을 도입하고 자동 council·speed lane·건강한 보드 AI 개선을 중단했다.
- **2026-08-24**: 오너 원 명세를 받아 그림 생성·실행기·커밋 순서와 `--no-start`를 정리했다.
- **2026-08-23**: 그래픽 직접 생성이 기본이었던 한시 규칙을 원 명세로 되돌렸다.
