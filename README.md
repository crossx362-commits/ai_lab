# Connect AI Lab

AI agent automation and Petnna web app monorepo.

**👉 운영 매뉴얼·사고 런북·전체 문서 색인: [HANDBOOK.md](HANDBOOK.md)**

Last reviewed: 2026-07-22

## Current Layout

```text
ai_lab/
├── projects/ai-team/      # multi-agent automation framework
├── projects/petnna/       # Petnna web/hybrid app
├── docs/                  # setup and operating documentation
├── reports/               # generated reports, meeting notes, research, logs
├── output/                # generated runtime artifacts
├── PROJECT_OVERVIEW.md    # high-level system overview
└── AGENTS.md              # repository rules for coding agents
```

## Main Entry Points

| Task | Command |
| --- | --- |
| Start Autonomous Dev Loop (Foreground) | `./loop/loop.sh` |
| Stop Autonomous Dev Loop | `touch loop/STOP` (재개 시 `rm loop/STOP`) |
| Start Youngsuk Telegram bot | `python projects/ai-team/skills/영숙_비서/tools/telegram_receiver.py` |
| Control an individual agent daemon | `python projects/ai-team/skills/영숙_비서/tools/agent_controller.py <영숙\|예원\|영숙스케줄\|봄이> <start\|stop\|restart\|status>` |
| Run the harness check | `python projects/ai-team/harness/check_all.py` |

## 자율 개발 루프 (Autonomous Development Loop)

대화 맥락에 의존하지 않고 매 이터레이션마다 **새로운 헤드리스 세션**으로 자율 개발을 진행하는 루프 시스템입니다. 상태와 작업 큐는 파일(`docs/`)로 영속 관리됩니다.

### 1. 주요 파일 구성
- [loop/loop.sh](file:///Users/junholee/ai_lab/loop/loop.sh): 루프 본체 스크립트 (독립 세션 실행, 지시서 전달, 로그 기록, STOP 감지)
- [loop/env.sh](file:///Users/junholee/ai_lab/loop/env.sh): 루프 환경설정 (실행기/모델, 최대 턴 수, 쿨다운 대기시간, 최대 바퀴 수, PATH)
- [loop/PROMPT.md](file:///Users/junholee/ai_lab/loop/PROMPT.md): 5개 절 지시서 (합격 기준, 읽을 문서 순서, 아트 규칙, 도는 순서, 커밋 규칙)
- [loop/com.ailab.autonomous_loop.plist](file:///Users/junholee/ai_lab/loop/com.ailab.autonomous_loop.plist): macOS 자동 실행 (launchd) 등록 정의
- [docs/DESIGN.md](file:///Users/junholee/ai_lab/docs/DESIGN.md): 무엇을 만드는가 (초기 기획서, 기준 헌법)
- [docs/STATUS.md](file:///Users/junholee/ai_lab/docs/STATUS.md): 어디까지 했고 다음은 뭔가 (매 바퀴 갱신 상태 및 큐)
- [docs/feedback/INBOX.md](file:///Users/junholee/ai_lab/docs/feedback/INBOX.md): 오너 직접 지시함 (최우선 처리)
- `logs/`: 매 바퀴별 실행 상세 로그 디렉토리 (`logs/loop_YYYYMMDD_HHMMSS_iterN.log`)

### 2. 켜는 법 (Start)
- **터미널에서 직접 실행**:
  ```bash
  ./loop/loop.sh
  ```
- **특정 에이전트/바퀴 수 지정 실행**:
  ```bash
  LOOP_AGENT=codex LOOP_MAX_LOOPS=5 ./loop/loop.sh
  ```
- **macOS launchd 백그라운드 서비스 시작**:
  ```bash
  launchctl load ~/Library/LaunchAgents/com.ailab.autonomous_loop.plist
  launchctl start com.ailab.autonomous_loop
  ```

### 3. 끄는 법 (Stop)
- **현재 바퀴 완료 후 안전하게 정지 (권장)**:
  ```bash
  touch loop/STOP
  ```
  *(다시 시작할 때는 `rm loop/STOP`)*
- **macOS launchd 서비스 정지 및 비활성화**:
  ```bash
  launchctl stop com.ailab.autonomous_loop
  launchctl unload ~/Library/LaunchAgents/com.ailab.autonomous_loop.plist
  ```
- **강제 즉시 종료**:
  ```bash
  pkill -f loop/loop.sh
  ```

### 4. 상태 보는 법 (Status)
- **현재 작업 및 다음 할 일 확인**: `docs/STATUS.md` 및 `docs/feedback/INBOX.md` 열람
- **실시간 실행 로그 확인**:
  ```bash
  tail -f logs/loop_main.log
  ```
- **launchd 서비스 상태 확인**:
  ```bash
  launchctl list | grep com.ailab.autonomous_loop
  ```


## Agent System

The active agent tools live under `projects/ai-team/skills/<agent>/tools/`. Full roster and responsibilities: [PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md).

| Area | Agents / Files |
| --- | --- |
| Orchestration | 예원 CEO: `yewon_dispatcher.py`, `harness_manager.py`, `skill_auditor.py`, `petnna_council.py` |
| Telegram and scheduling | 영숙 비서: `telegram_receiver.py`, `schedule_manager.py`, `agent_controller.py`, `calendar_manager.py` |
| Petnna QA / Dev / Test | 봄이 `petnna_qa_patrol.py`, 수리 `petnna_dev_engine.py`, 테오 `petnna_test_engineer.py` |
| Petnna Backend / Design / PM | 백호 `petnna_backend_guard.py`, 미오 `petnna_design_review.py`, 나무 `petnna_product_manager.py` |

Shared Python modules are in `projects/ai-team/_shared/`. Be careful with this folder because many agents import it directly.

## Documentation Map

- [PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md): current high-level architecture and operating picture.
- [docs/REPOSITORY_CLASSIFICATION.md](docs/REPOSITORY_CLASSIFICATION.md): categorized Markdown/script/bot inventory and cleanup decisions.
- [docs/TELEGRAM_BOT_README.md](docs/TELEGRAM_BOT_README.md): Telegram bot operations.
- [docs/setup/ENV_SECURITY_RULES.md](docs/setup/ENV_SECURITY_RULES.md): secret handling rules.
- [projects/ai-team/scripts/README.md](projects/ai-team/scripts/README.md): operational scripts index.
- [projects/petnna/README.md](projects/petnna/README.md): Petnna app documentation.

## Security Rules

- Keep secrets in `ai_lab/.env` (local, not committed plaintext) and encrypted copies only.
- Do not create project-specific plaintext `.env` files.
- Do not commit plaintext credentials, `client_secret.json`, logs, generated media, or cache folders.
- Use `load_env()` from `projects/ai-team/_shared/env.py` before accessing secrets.

## Generated and Disposable Areas

These paths are operational artifacts, caches, or local backups and should not be treated as source:

- `output/`
- `reports/uploads/`
- `projects/ai-team/node_modules/`
- `projects/ai-team/out/`

Use Git status before cleanup so tracked reports or deliverables are not removed accidentally.
