# AI Team Scripts

This folder contains operational scripts that are shared across agents or used to
start and inspect the ai-team runtime.

## Before Running

- Scripts can touch live services, trading state, Telegram messages, or cloud
  resources. Prefer dry-run or diagnostic scripts first when available.
- Use `agents/test_agent_api_connections.py` for real API connectivity checks.
- Use the harness before and after structure changes:

```bash
python projects/ai-team/harness/check_all.py
```

## Folders

| Path | Purpose |
| --- | --- |
| `agents/` | One-off diagnostics, env checks, API checks, and path validation. |
| `security/` | Secret encryption/decryption and credential hygiene helpers. |
| `youtube/` | YouTube OAuth, public conversion, and metadata update helpers. |

## Main Scripts

| File | Purpose |
| --- | --- |
| `start_trading_team.py` | Canonical launcher for signal, pulse, Dave, Leo, and monitor. |
| `run_trading_team_background.py` | Background trading-team launcher. |
| `monitor_processes.py` | Process monitor for important background daemons. |
| `cleanup_duplicate_processes.py` | Cleans duplicate Python daemon processes. |
| `start_daily_automation.py` | Starts daily AI-team automation jobs. |
| `daily_balance_check.py` | Daily balance and holdings check. |
| `check_holdings.py` | On-demand Upbit holdings check. |
| `daily_trading_learning.py` | Trading-result learning and reflection job. |
| `scan_env_usage.py` | Scans Python files for environment variable usage. |
| `kodari_ollama.py` | Ollama health and recovery helper. |
| `cycle.js` | VS Code extension automation support script. |

## Placement Rules

- Agent-specific tools belong in `projects/ai-team/skills/<agent>/tools/`.
- Shared Python helpers belong in `projects/ai-team/_shared/`.
- One-off diagnostics belong in `projects/ai-team/scripts/agents/`.
- Official operating pipelines belong directly in `projects/ai-team/scripts/`.
- Generated reports, logs, caches, and media belong under `reports/`, `.logs/`,
  `output/`, or ignored runtime-state paths.
