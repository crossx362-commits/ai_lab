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
| Start Youngsuk Telegram bot | `python projects/ai-team/skills/영숙_비서/tools/telegram_receiver.py` |
| Control an individual agent daemon | `python projects/ai-team/skills/영숙_비서/tools/agent_controller.py <영숙\|예원\|영숙스케줄\|봄이> <start\|stop\|restart\|status>` |
| Run the harness check | `python projects/ai-team/harness/check_all.py` |

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
