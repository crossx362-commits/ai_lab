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
| Start Autonomous Dev Loop | `bash loop/control.sh start <claude\|codex\|grok\|opencode>` |
| Stop Autonomous Dev Loop | `bash loop/control.sh stop` |
| Start Youngsuk Telegram bot | `python projects/ai-team/skills/영숙_비서/tools/telegram_receiver.py` |
| Control an individual agent daemon | `python projects/ai-team/skills/영숙_비서/tools/agent_controller.py <영숙\|예원\|영숙스케줄\|봄이> <start\|stop\|restart\|status>` |
| Run the harness check | `python projects/ai-team/harness/check_all.py` |

## 자율 개발 루프 (Autonomous Development Loop)

대화 맥락에 의존하지 않고 **매 바퀴마다 새 헤드리스 세션**을 연다 (`resume`/`continue` 없음).
기억은 `docs/` 파일과 Git에만 둔다. 시작할 때 고른 실행기를 유지하며, launchd도
`/Users/junholee/ai_lab/loop/loop.sh` 원본을 직접 실행한다. 사용량 한도나 공급자 외부 장애에는
다른 AI로 전환하거나 복구 AI를 부르지 않고, 프롬프트 없는 공식 사용량 조회만 반복한다.

### 만든 파일
| 파일 | 역할 |
|---|---|
| `loop/control.sh` | start/stop/status 단일 제어 진입점 · 실행 확인 · STOP 소유 |
| `loop/loop.sh` | 새 세션 · 한도/오류 대기·복구 · 날짜별 로그 · STATUS.md 갱신 검사 |
| `loop/runtime_state.py` | running/quota_wait/recovering/owner_stopped 원자적 상태 기록 |
| `loop/PROMPT.md` | 5절 지시서 (합격 / 읽을 문서 / 규칙 / 순서 / 커밋) |
| `loop/com.ailab.autonomous_loop.plist` | macOS launchd (로그인 시작, 비정상만 재시작, PATH 명시) |
| `loop/deploy_launchd.sh` | launchd plist 등록 후 단일 제어 경로로 기동 |
| `docs/DESIGN.md` | 무엇을 만드는가 (요약 틀. 원장은 `docs/GAME_DESIGN_ASHES_TO_STARS.md`) |
| `docs/STATUS.md` | 어디까지 했고 다음은 뭔가 (한 바퀴마다 갱신) |
| `docs/feedback/INBOX.md` | 오너 지시 (가장 먼저 처리) |
| `logs/YYYY-MM-DD/lap-*.log` | 날짜별 바퀴 로그 |

### 켜는 법
```bash
bash loop/control.sh start claude   # 원하는 AI를 지정해 즉시 시작
bash loop/control.sh status
```

### 끄는 법
```bash
bash loop/control.sh stop            # 현재 바퀴 끝난 뒤 정상 종료
```

### 상태 보는 법
```bash
launchctl print gui/$(id -u)/com.ailab.autonomous_loop | head
tail -f logs/loop_main.log
ls -lt logs/$(date +%Y-%m-%d)/ | head
# 개발보드 (8766) — launchd KeepAlive. 터미널에서 켜면 세션 종료와 같이 죽는다.
launchctl bootstrap gui/$(id -u) ~/Library/LaunchAgents/com.ailab.board.plist
# 끄기: launchctl bootout gui/$(id -u)/com.ailab.board
# 주소: http://127.0.0.1:8766
```

### 사용량·오류 연속성

- 사용량 한도: 같은 AI를 유지하고 `PROVIDER_RETRY_SECONDS` 뒤 무료 조회한다. 조회가 잠시
  불명확해도 이미 확인한 한도 상태를 유지한다.
- 503·로그인·조직 접근 차단: 복구 AI를 쓰지 않고 무료 상태 조회가 회복될 때까지 기다린다.
- 코드·테스트 오류: 동일 오류 지문당 복구 세션은 한 번만 허용하고, 고유 표식이 든 복구
  커밋을 확인한 뒤 새 원본으로 재기동한다.
- speed lane은 자동 기동하지 않는다. 명시적인 별도 운영 결정이 있을 때만 사용한다.

```bash
python3 loop/board.py usage claude    # 클로드 사용량 확인 (JSON)
python3 loop/board.py usage grok      # 그록 사용량 확인 (JSON)
cat loop/agent                         # 현재 고정 실행기
```

### 한 바퀴 순서 (PROMPT.md)
읽기(INBOX→STATUS) → 하나만 만들기 → 자동검사 → **화면 보기 전 커밋** → 눈으로 확인 → STATUS.md 갱신.

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
