# AI Team

Multi-agent automation framework for Telegram operations, trading bots, Petnna support tooling, infrastructure checks, and business research.

Last reviewed: 2026-06-17

## Active Structure

```text
projects/ai-team/
├── _shared/          # common Python clients and utilities
├── scripts/          # operational launchers and diagnostics
├── skills/           # agent-specific SKILL.md files and tools
├── docs/             # architecture and optimization docs
├── src/              # VS Code extension source
├── assets/           # templates and static assets
└── reports/          # local project reports
```

## Agent Tool Map

| Agent | Folder | Main Tools |
| --- | --- | --- |
| 예원 CEO | `skills/예원_CEO/` | `yewon_dispatcher.py`, `upload_manager.py`, `skill_auditor.py` |
| 영숙 비서 | `skills/영숙_비서/` | `telegram_receiver.py`, `calendar_manager.py`, `posting_scheduler.py` |
| 현빈 전략가 | `skills/현빈_전략가/` | `crypto_market_intelligence.py`, `business_research.py`, `paypal_revenue.py` |
| 데이브 주식 | `skills/데이브_주식/` | `upbit_auto_trader.py`, `upbit_analyzer.py`, `upbit_public.py` |
| 레오 트레이더 | `skills/레오_트레이더/` | `leo_aggressive_trader.py`, `leo_learning_system.py` |
| 코다리 개발자 | `skills/코다리_개발자/` | `web_preview.py`, `agent_health_check.py`, `ollama_health_check.py` |
| 티모 디자이너 | `skills/티모_디자이너/` | `petnna_reviewer.py` |
| 케빈 인프라 | `skills/케빈_인프라/` | `vercel_manager.py`, `supabase_manager.py`, `petnna_monitor.py` |
| 경수 수사관 | `skills/경수_수사관/` | `comment_forensics.py`, `content_inspector.py` |
| 로율 변호사 | `skills/로율_변호사/` | `tax_simulator.py` |

## Common Commands

Run from `D:\ai_lab`.

```powershell
# Start all trading processes in live mode
python projects/ai-team/scripts/start_trading_team.py --live

# Start trading team in background
python projects/ai-team/scripts/run_trading_team_background.py --live

# Start Youngsuk Telegram bot
cmd /c projects\ai-team\scripts\start_youngsuk_bot.cmd

# Direct Telegram bot debug run
python projects/ai-team/skills/영숙_비서/tools/telegram_receiver.py

# Check API/env connectivity
python projects/ai-team/scripts/agents/test_agent_api_connections.py

# Scan environment variable usage
python projects/ai-team/scripts/scan_env_usage.py
```

## Shared Imports

Agent tools commonly climb to the `projects/ai-team/` root and import from `_shared`:

```python
from _shared.env_loader import load_env
from _shared.telegram_notifier import send_telegram_message

load_env()
```

Avoid changing `_shared/` casually. It is imported by many agents and should be tested across agent commands after edits.

## Script Index

Use `scripts/README.md` for the operations script inventory. Broad repository classification lives in `D:\ai_lab\docs\REPOSITORY_CLASSIFICATION.md`.

## Security

- Secrets are loaded from the central `D:\ai_lab\.env`.
- Do not add project-local plaintext `.env` files.
- Keep encrypted copies such as `.env.encrypted`; do not keep plaintext backups.
- Daemon and trading scripts should preserve process locks and duplicate-process guards.
