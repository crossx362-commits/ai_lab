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

매 바퀴 Claude·Codex·Grok CLI를 완전히 새 세션으로 실행합니다. 작업이 하나면 Claude(Fable, 사용량 소진 시에만 Opus 5)가 개발하고 Codex가 검토합니다. 독립 작업이 여러 개면 최대 세 개를 별도 Git worktree에서 병렬 개발하며, 다른 제공자가 정확한 후보 커밋을 검토합니다. 통과한 변경만 `autonomous/integration`에 합치므로 `master`와 사용자의 열린 작업트리는 자동 수정하지 않습니다.

### 주요 파일

- `loop/loop.sh`: 무한 반복, 새 coordinator 프로세스, 날짜별 로그, STOP 처리
- `loop/agent_runner.py`: 작업 분리, 겹침 차단, 격리 worktree, 교차 검토, 통합
- `loop/env.sh`: 모델·최대 턴·대기·최대 바퀴·병렬 수·PATH
- `loop/PROMPT.md`: 다섯 절 개발 지시서
- `loop/TASKS.example.json`: planner 호출을 아끼는 구조화 작업 예시
- `loop/board.py`, `loop/board.html`: 상태·로그·오너 지시 보드
- `loop/com.ailab.autonomous_loop.plist`: macOS launchd 정의
- `docs/DESIGN.md`, `docs/STATUS.md`, `docs/feedback/INBOX.md`: 파일 기억
- `logs/YYYY-MM-DD/<run-id>/`: planner/worker/reviewer/run JSON 증거

### 작업 수와 토큰 사용 조절

```bash
# 자동: 독립 작업이면 최대 3개, 겹치면 다음 바퀴로 미룸
LOOP_MODE=auto ./loop/loop.sh

# 한 번에 하나만 개발
LOOP_MODE=single ./loop/loop.sh

# 독립 작업을 최대 3개 병렬 개발
LOOP_MODE=parallel LOOP_MAX_PARALLEL=3 ./loop/loop.sh
```

기본적으로 INBOX/STATUS를 읽는 짧은 Claude planner가 작업 경로를 나눕니다. planner 토큰도 아끼려면 `loop/TASKS.example.json`을 복사해 gitignored `loop/TASKS.json`을 채우면 planner 세션을 생략합니다. 완료된 작업 hash는 `output/cache/autonomous_loop/completed.json`에 기록되어 같은 지시를 반복하지 않습니다. Ollama는 기본 `off`이며 판단이 필요 없는 형식 정리에만 선택적으로 허용됩니다.

### 터미널에서 켜기

```bash
rm -f loop/STOP
./loop/loop.sh /Users/junholee/ai_lab

# 확인용 두 바퀴만
LOOP_MAX_LOOPS=2 ./loop/loop.sh /Users/junholee/ai_lab
```

### launchd 설치와 켜기

설치본은 worktree 삭제와 무관한 고정 경로를 사용합니다. 아래 복사는 등록만 하며 즉시 켜지 않습니다.

```bash
mkdir -p "/Users/junholee/Library/Application Support/AI Lab Autonomous Loop"
install -m 755 loop/loop.sh loop/agent_runner.py "/Users/junholee/Library/Application Support/AI Lab Autonomous Loop/"
install -m 644 loop/env.sh loop/PROMPT.md "/Users/junholee/Library/Application Support/AI Lab Autonomous Loop/"
install -m 644 loop/com.ailab.autonomous_loop.plist /Users/junholee/Library/LaunchAgents/com.ailab.autonomous_loop.plist
```

검증 후 실제로 켤 때만 실행합니다.

```bash
launchctl bootstrap "gui/$(id -u)" /Users/junholee/Library/LaunchAgents/com.ailab.autonomous_loop.plist
```

plist는 로그인 시 시작하고(`RunAtLoad`), 비정상 종료만 재시작하며 정상 STOP 종료는 그대로 둡니다. `ThrottleInterval=60`과 절대 PATH가 설정돼 있습니다.

### 끄기·재개·등록 해제

```bash
# 현재 바퀴를 끝낸 뒤 정상 정지
touch /Users/junholee/ai_lab/loop/STOP

# 같은 로그인 세션에서 다시 시작
rm -f /Users/junholee/ai_lab/loop/STOP
launchctl kickstart "gui/$(id -u)/com.ailab.autonomous_loop"

# launchd 등록도 해제
launchctl bootout "gui/$(id -u)" /Users/junholee/Library/LaunchAgents/com.ailab.autonomous_loop.plist
```

### 상태와 개발 결과 확인

```bash
launchctl print "gui/$(id -u)/com.ailab.autonomous_loop"
tail -f /Users/junholee/ai_lab/logs/loop_main.log
git log --oneline autonomous/integration -20
git branch --list 'autonomous/loop-*'
```

보드는 `python3 loop/board.py`로 열 수 있습니다. 최신 STATUS는 `autonomous/integration:docs/STATUS.md`가 정본이고 루트 STATUS는 최초 틀입니다. 불합격 후보도 고유 branch와 세션 로그가 남아 Claude·GPT·Grok이 서로 개발한 부분을 다시 확인할 수 있습니다.


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
