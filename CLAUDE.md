# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## 🏗️ Repository Structure

This is a monorepo containing:

1. **ai-team/** — Multi-agent AI automation framework with 13+ specialized agents
2. **petnna/** — Pet healing platform (web/hybrid app)
3. **Root scripts/** — Trading bots and daemon management

### Key Directories

```
ai_lab/
├── projects/ai-team/
│   ├── _shared/           # Shared API clients and utilities (import as: from _shared.module_name)
│   ├── skills/            # Agent-specific tools organized by agent name (Korean folders)
│   ├── scripts/           # System-wide automation and daemon launchers
│   └── reports/           # Generated reports and research outputs
├── projects/petnna/       # Pet platform web app
├── .env                   # Encrypted central environment variables (NEVER commit plaintext)
└── output/                # Runtime logs and artifacts
```

---

## 🚀 Running the System

### Start All Trading Bots (Live Mode)
```bash
python projects/ai-team/scripts/start_trading_team.py --live
```

This launches:
- **현빈 (Hyunbin)**: Crypto market intelligence collector
- **데이브 (Dave)**: Conservative Upbit auto-trader
- **레오 (Leo)**: Aggressive day-trader
- **Monitor**: Process health checker

### Start Telegram Bot (영숙/Youngsuk)
```powershell
powershell -ExecutionPolicy Bypass .\projects\ai-team\skills\영숙_비서\tools\start_telegram_bot.ps1
```

Or manually:
```bash
python projects/ai-team/skills/영숙_비서/tools/telegram_receiver.py
```

### Check Running Bots
```powershell
Get-Process python* | Select-Object Id, ProcessName, StartTime
```

### Kill Duplicate Processes
```bash
python projects/ai-team/scripts/cleanup_duplicate_processes.py
```

---

## 🤖 AI Agent System Architecture

### Agent Roster (13 Agents)

| Agent | Role | Key Tools |
|-------|------|-----------|
| 예원 (Yewon) | CEO — Task dispatcher & orchestrator | `yewon_dispatcher.py`, `upload_manager.py` |
| 영숙 (Youngsuk) | Secretary — Telegram bot & calendar | `telegram_receiver.py`, `calendar_manager.py` |
| 루나 (Luna) | Director — YouTube music video production | `youtube_research.py`, `video_automation.py` |
| 아린 (Arin) | Manager — Instagram content uploader | `upload_insta_post.py`, `auto_pipeline.py` |
| 코다리 (Kodari) | Developer — Web dev & health checks | `web_preview.py`, `ollama_health_check.py` |
| 케빈 (Kevin) | Infra — Vercel & Supabase management | `setup_vercel.py`, `deploy_*.py` |
| 티모 (Timo) | Designer — UI/UX review | `petnna_reviewer.py` |
| 현빈 (Hyunbin) | Strategist — Crypto market research | `crypto_market_intelligence.py` |
| 데이브 (Dave) | Trader — Conservative crypto trading | `upbit_auto_trader.py` |
| 레오 (Leo) | Trader — Aggressive day trading | `leo_aggressive_trader.py` |
| 경수 (Kyungsu) | Investigator — Malicious comment detection | security tools |
| 로율 (Royul) | Lawyer — Legal/tax/compliance | compliance tools |

### Shared Module System

All agents use centralized utilities in `projects/ai-team/_shared/`:

| Module | Purpose |
|--------|---------|
| `env_loader.py` | Load encrypted `.env` from project root |
| `gemini_client.py` | Gemini API wrapper (text/image/vision) |
| `ollama_client.py` | Local Ollama LLM with task-based model selection |
| `telegram_notifier.py` | Send notifications to Telegram |
| `process_lock.py` | Windows Named Mutex for preventing duplicate processes |
| `agent_status.py` | Get status reports for all 13 agents |
| `duplicate_guard.py` | Prevent duplicate content uploads |

**Import pattern** used by all agents:
```python
import os, sys
_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, "reports")):  # or .agent, ENV_MANIFEST.json
        break
    _root = os.path.dirname(_root)
sys.path.insert(0, _root)

from _shared.env_loader import load_env
from _shared.telegram_notifier import send_telegram_message
load_env()
```

---

## 🧠 AI Model Strategy (2-Tier Fallback)

Priority: **Ollama (local, free) → Gemini API (cloud, paid) → GPT-4o-mini (fallback)**

### Model Selection Logic (`_shared/ollama_client.py`)

- **Coding tasks**: Prefers `deepseek-coder`, `codestral`
- **Blog/caption writing**: Prefers `qwen2.5` (excludes deepseek)
- **General**: Uses first loaded model in Ollama

Force a specific model:
```bash
export OLLAMA_MODEL=deepseek-coder:latest
```

### Fallback Chain

```python
from _shared.gemini_client import text

# Automatically tries: Ollama → Gemini → GPT-4o-mini
response = text("Your prompt here", lm_first=True)
```

---

## 🔐 Environment Variable Security

### Critical Rules

1. **ALL secrets live in `D:\ai_lab\.env`** (encrypted)
2. **NEVER create project-specific `.env` files**
3. **NEVER hardcode API keys**
4. **Always use `load_env()` before accessing secrets**

### Encryption/Decryption

Encrypt all secrets:
```bash
python projects/ai-team/scripts/security/encrypt_all_secrets.py
```

Decrypt for editing:
```bash
python projects/ai-team/scripts/security/decrypt_all_secrets.py
```

### Required Environment Variables

See `.env` for full list. Key variables:
- `TELEGRAM_BOT_TOKEN`, `TELEGRAM_CHAT_ID`
- `GEMINI_API_KEY`
- `ANTHROPIC_API_KEY` (Claude fallback)
- `UPBIT_ACCESS_KEY`, `UPBIT_SECRET_KEY`
- `INSTAGRAM_ACCESS_TOKEN`, `INSTAGRAM_ACCOUNT_ID`
- `NOTION_API_KEY`, `NOTION_DATABASE_ID`
- `SUPABASE_URL`, `SUPABASE_ANON_KEY`

---

## 📊 Trading System

### Trading Team Components

1. **Market Intelligence** (`crypto_market_intelligence.py`):
   - Fetches Upbit market data
   - Analyzes price trends, order book depth
   - Publishes to `projects/ai-team/reports/research/crypto_market_intel.json`

2. **Conservative Trader** (`upbit_auto_trader.py`):
   - Reads market intelligence
   - Executes safe trades with stop-loss
   - Logs all trades

3. **Aggressive Trader** (`leo_aggressive_trader.py`):
   - High-frequency day trading
   - Tighter risk tolerance
   - More volatile strategy

### Public API Fallback

If `pyupbit` module is unavailable, `upbit_public.py` provides fallback REST API access.

### Trading Commands

Start live trading:
```bash
cd projects/ai-team/scripts
python start_trading_team.py --live
```

Check holdings:
```bash
python check_holdings.py
```

Daily balance check:
```bash
python daily_balance_check.py
```

---

## 📱 Telegram Bot (영숙)

### Natural Language Commands

The bot uses Gemini Function Calling to map natural language to tools:

- **"현황 보고해줘" / "다들 뭐해?"** → `get_agent_status()` (shows all 13 agents)
- **"일정 알려줘" / "캘린더 확인해봐"** → `list_calendar()`
- **"루나 영상 만들어" / "인스타 올려"** → `dispatch()` → CEO orchestration

### Bot Architecture

`telegram_receiver.py` consolidates:
- Gemini Function Calling integration
- Calendar manager (`calendar_manager.py`)
- Posting scheduler (`posting_scheduler.py`)
- Reports manager (`reports_manager.py`)
- Upload approval flow (`upload_approval_flow.py`)

Logs: `projects/ai-team/skills/영숙_비서/tools/telegram_receiver.log`

---

## 🌐 Petnna Project

### Local Development

Start web preview server:
```bash
python projects/ai-team/skills/코다리_개발자/tools/web_preview.py
# → http://localhost:8000
```

### UI/UX Review

Run automated design review:
```bash
python projects/ai-team/skills/티모_디자이너/tools/petnna_reviewer.py
```

### Structure

```
projects/petnna/
├── index.html        # Main entry
├── js/               # Controllers and views
├── css/              # Tailwind CSS, Leaflet
├── images/           # Assets
├── api/              # Backend API (if applicable)
└── docs/             # Planning and research reports
```

---

## 🛠️ Development Guidelines

### When Editing Agent Tools

1. **Preserve import paths** — All agents use the 6-level root-finding pattern
2. **Use UTF-8 encoding** — Set `PYTHONUTF8=1` or `sys.stdout.reconfigure(encoding="utf-8")`
3. **Test with Ollama first** — Most agents default to local LLM
4. **Check for mutex locks** — Use `process_lock.py` for daemon scripts to prevent duplicates

### When Adding New Agents

1. Create folder: `projects/ai-team/skills/<에이전트명>/`
2. Add tools to: `projects/ai-team/skills/<에이전트명>/tools/*.py`
3. Register in: `projects/ai-team/_shared/agent_status.py`
4. Update: `projects/ai-team/AGENT_AUDIT_REPORT.md`

### Process Management

- **Daemons use Windows Named Mutex** (`process_lock.py`) to prevent duplicates
- **Cleanup zombies**: `cleanup_duplicate_processes.py`
- **Monitor processes**: `monitor_processes.py --daemon`

### Logging

- Agent logs: `output/bot_logs/`
- System logs: `.logs/`
- Trading logs: Check respective trader scripts

---

## 🎯 Common Tasks

### Daily Automation

```bash
python projects/ai-team/scripts/start_daily_automation.py
```

This runs:
- Upload status checks
- Calendar sync
- Report generation

### Agent Health Check

```bash
python projects/ai-team/scripts/agents/test_agent_api_connections.py
```

Verifies:
- Ollama server running
- Gemini API key valid
- Telegram bot token working

### Scan Environment Variable Usage

```bash
python projects/ai-team/scripts/scan_env_usage.py
```

Shows which `.py` files use which env vars.

---

## 📝 Coding Conventions

- **Korean folder names** are normal (에이전트명) — all OS paths handle UTF-8
- **Match existing patterns** — Don't refactor agent import logic
- **No premature abstractions** — Agents prefer explicit over DRY
- **Surgical changes only** — Don't "improve" adjacent code
- **Test on Windows** — This repo runs primarily on Windows 11

### Error Handling

Agents use lenient error handling with Telegram fallback:
```python
try:
    # risky operation
except Exception as e:
    send_telegram_message(f"⚠️ Error in {AGENT_NAME}: {e}")
```

### Encoding Issues

Always use:
```python
import sys
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
```

---

## 🚨 IMPORTANT: Do Not Break

1. **Never modify `_shared/` without testing ALL agents**
2. **Never commit `.env` in plaintext**
3. **Never force-push to master**
4. **Never remove mutex locks from daemons** (causes zombie processes)
5. **Never skip `load_env()`** — all agents depend on central `.env`

---

## 📚 Documentation

- **Agent details**: `projects/ai-team/AGENT_AUDIT_REPORT.md`
- **AI model strategy**: `projects/ai-team/docs/AI_MODEL_STRATEGY.md`
- **Security rules**: `docs/ENV_SECURITY_RULES.md`
- **Telegram bot**: `TELEGRAM_BOT_README.md`
- **Petnna setup**: `projects/petnna/README.md`
