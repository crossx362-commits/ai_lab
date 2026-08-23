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

대화 맥락에 의존하지 않고 **매 바퀴마다 새 헤드리스 세션**을 연다. 기억은 `docs/` 파일과 Git에만 둔다.

실제 launchd가 실행하는 복사본은 아래에 있다 (레포가 dirty여도 루프 본체가 안 깨지게):
`~/Library/Application Support/AI Lab Autonomous Loop/`
레포 `loop/`가 원본이고, `loop/deploy_launchd.sh`로 배포한다.

### 1. 주요 파일
| 파일 | 역할 |
|---|---|
| `loop/loop.sh` | 무한 루프 · STOP · 날짜별 로그 · agent_runner 호출 |
| `loop/agent_runner.py` | 한 바퀴 코디네이터 (planner/worker/reviewer, worktree 격리) |
| `loop/env.sh` | 모델·턴·쿨다운·최대 바퀴·PATH |
| `loop/PROMPT.md` | 5절 지시서 (합격 / 읽을 문서 / 규칙 / 순서 / 커밋) |
| `loop/com.ailab.autonomous_loop.plist` | macOS launchd (로그인 시작, 비정상만 재시작, PATH 명시) |
| `loop/deploy_launchd.sh` | 레포 → Application Support 배포 + 재등록 |
| `docs/DESIGN.md` | 기획 요약 틀 (원장은 `docs/GAME_DESIGN_ASHES_TO_STARS.md`) |
| `docs/STATUS.md` | 진행·다음 큐 (보드·루프가 읽음) |
| `docs/feedback/INBOX.md` | 오너 최우선 지시 |
| `logs/YYYY-MM-DD/lap-*.log` | 날짜별 바퀴 로그 (+ worktree 하위 role 로그) |

### 2. 켜는 법
```bash
# 배포 후 launchd 시작 (권장)
./loop/deploy_launchd.sh

# 또는 터미널에서 직접 (최대 2바퀴 시험)
LOOP_MAX_LOOPS=2 ./loop/loop.sh /Users/junholee/ai_lab
```

### 3. 끄는 법
```bash
touch loop/STOP          # 현재 바퀴 끝난 뒤 정상 종료 (권장)
rm loop/STOP             # 다시 켤 때
launchctl bootout gui/$(id -u)/com.ailab.autonomous_loop   # 서비스 내리기
```

### 4. 상태 보는 법
```bash
launchctl print gui/$(id -u)/com.ailab.autonomous_loop | head
tail -f logs/loop_main.log
ls -lt logs/$(date +%Y-%m-%d)/ | head
python3 loop/board.py    # 개발보드 http://127.0.0.1:8766
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
