# Connect AI Lab

AI agent automation and Petnna web app monorepo.

**👉 운영 매뉴얼·사고 런북·전체 문서 색인: [HANDBOOK.md](HANDBOOK.md)**

Last reviewed: 2026-07-10

## Current Layout

```text
D:\ai_lab
├── projects\ai-team\      # multi-agent automation framework
├── projects\petnna\       # Petnna web/hybrid app
├── docs\                  # setup and operating documentation
├── reports\               # generated reports, meeting notes, research, logs
├── output\                # generated runtime artifacts
├── connect-ai-packs\      # reusable packs, skills, templates
├── PROJECT_OVERVIEW.md    # high-level system overview
└── AGENTS.md              # repository rules for coding agents
```

The historical flat `ai-team/` and `petnna/` paths are no longer the active project layout. Use `projects/ai-team/` and `projects/petnna/`.

## Main Entry Points

| Task | Command |
| --- | --- |
| Start trading team live | `python projects/ai-team/scripts/start_trading_team.py --live` |
| Start trading team in background | `python projects/ai-team/scripts/run_trading_team_background.py --live` |
| Restart trading team from Windows | `restart_trading.bat` |
| Start Youngsuk Telegram bot | `cmd /c projects\ai-team\scripts\start_youngsuk_bot.cmd` |
| Start Telegram bot directly | `python projects/ai-team/skills/영숙_비서/tools/telegram_receiver.py` |
| Check agent API connections | `python projects/ai-team/scripts/agents/test_agent_api_connections.py` |
| Scan environment variable usage | `python projects/ai-team/scripts/scan_env_usage.py` |
| Preview Petnna locally | `python projects/ai-team/skills/코다리_개발자/tools/web_preview.py` |
| Review Petnna UI/UX | `python projects/ai-team/skills/티모_디자이너/tools/petnna_reviewer.py` |

## Agent System

The active agent tools live under `projects/ai-team/skills/<agent>/tools/`.

| Area | Agents / Files |
| --- | --- |
| Orchestration | 예원 CEO: `yewon_dispatcher.py`, `upload_manager.py`, `skill_auditor.py` |
| Telegram and scheduling | 영숙 비서: `telegram_receiver.py`, `calendar_manager.py`, `posting_scheduler.py` |
| Trading | 펄스 market intelligence, 데이브 conservative trader, 레오 aggressive trader |
| Development and design | 코다리 developer tools, 티모 UI/UX reviewer |
| Infra and security | 케빈 Vercel/Supabase/PWA monitor, 경수 investigation/security tools |
| Legal/tax | 로율 tax and legal simulation tools |

Shared Python modules are in `projects/ai-team/_shared/`. Be careful with this folder because many agents import it directly.

## Documentation Map

- [PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md): current high-level architecture and operating picture.
- [docs/REPOSITORY_CLASSIFICATION.md](docs/REPOSITORY_CLASSIFICATION.md): categorized Markdown/script/bot inventory and cleanup decisions.
- [docs/TELEGRAM_BOT_README.md](docs/TELEGRAM_BOT_README.md): Telegram bot operations.
- [docs/setup/ENV_SECURITY_RULES.md](docs/setup/ENV_SECURITY_RULES.md): secret handling rules.
- [projects/ai-team/scripts/README.md](projects/ai-team/scripts/README.md): operational scripts index.
- [projects/petnna/README.md](projects/petnna/README.md): Petnna app documentation.

## Security Rules

- Keep secrets in `D:\ai_lab\.env` and encrypted copies only.
- Do not create project-specific plaintext `.env` files.
- Do not commit plaintext credentials, `client_secret.json`, logs, generated media, or cache folders.
- Use `load_env()` from `projects/ai-team/_shared/env_loader.py` before accessing secrets.

## Generated and Disposable Areas

These paths are operational artifacts, caches, or local backups and should not be treated as source:

- `.archive/`
- `.backups/`
- `.logs/`
- `__pycache__/`
- `output/`
- `reports/uploads/`
- `projects/ai-team/node_modules/`
- `projects/ai-team/out/`

Use Git status before cleanup so tracked reports or deliverables are not removed accidentally.
