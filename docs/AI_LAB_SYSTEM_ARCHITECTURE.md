# AI Lab System Architecture

**Last Updated**: 2026-06-29  
**System Status**: 7 Agents (3 active daemons + 4 research agents)

---

## Executive Summary

AI Lab is a multi-agent stock market analysis and orchestration system running on Windows 11. It combines:

1. **Data Collection**: 4 geopolitical research agents (US/EU/Asia/Quant)
2. **Data Integration**: Market Desk consolidates research into global briefs
3. **Stock Analysis**: Somi scores watchlist securities with supply/demand metrics
4. **Decision Support**: Trade advisor provides buy signals and position management
5. **Orchestration**: Yewon CEO dispatches tasks; Youngsuk secretary handles Telegram interface

The system is **NOT an automated trader** — all trades require explicit approval via Telegram.

---

## System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                      USER (Telegram: 준호)                          │
└────────────────────────┬────────────────────────────────────────────┘
                         │ Natural language requests
                         ▼
    ┌────────────────────────────────────────┐
    │  영숙 (Youngsuk) — Secretary Bot       │
    │  - Polling Telegram messages           │
    │  - GPT-4o-mini function calling        │
    │  - Route to agents/tools               │
    └────┬─────────────────────────┬────────┘
         │                         │
    ┌────▼────────────┐      ┌────▼──────────────────┐
    │ Keyword Match   │      │ Task Dispatch         │
    │ (영숙_비서)      │      │ → yewon_dispatcher    │
    └────┬────────────┘      └────┬──────────────────┘
         │                         │
         ▼                         ▼
    ┌────────────────────────────────────────┐
    │  예원 (Yewon) — CEO Orchestrator      │
    │  - Harness health check (check_all.py) │
    │  - Skill auditor                       │
    │  - Agent coordination                  │
    └────┬─────────────────────────┬────────┘
         │                         │
         ├─────────────────────────┼─────────────────────────┐
         │                         │                         │
         ▼                         ▼                         ▼
    ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
    │ 소미 (Somi)      │  │ 조사팀 (Research)│  │ 마켓데스크       │
    │ Stock Analyst    │  │ Hank/Yuna/Leon  │  │ (Market Desk)    │
    └──────────────────┘  └──────────────────┘  └──────────────────┘
         │                         │                         │
         ▼                         ▼                         ▼
    ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
    │ KIS API          │  │ Web Scraping     │  │ Consolidation    │
    │ Stock Data       │  │ + Research.py    │  │ → market_brief   │
    │ + Scoring        │  │ Region JSONs     │  │ + Commentary     │
    └──────────────────┘  └──────────────────┘  └──────────────────┘
         │                                           │
         └───────────────────────┬───────────────────┘
                                 │
                         ┌───────▼────────┐
                         │ Output Storage │
                         │ /output/       │
                         │ - cache/       │
                         │ - research/    │
                         │ - trading_logs │
                         └────────────────┘
```

---

## Agent Roster

### Tier 1: Core Orchestration (Always Running)

| Agent | Role | Primary Tool | Type | Schedule |
|-------|------|--------------|------|----------|
| **영숙** (Youngsuk) | Telegram secretary bot | `telegram_receiver.py` | Continuous Daemon | 24/7 polling |
| **예원** (Yewon) | CEO task dispatcher | `yewon_dispatcher.py` | On-demand via Telegram | Reactive |

### Tier 2: Stock Analysis (Market Hours)

| Agent | Role | Primary Tools | Type | Schedule |
|-------|------|---------------|------|----------|
| **소미** (Somi) | Domestic stock analyst | `somi_kis_reporter.py`, `somi_trade_advisor.py` | Scheduled + Daemon | 09:30/12:00/15:40 (reports); 24/7 (price monitor) |

### Tier 3: Research (Geopolitical)

| Agent | Role | Region | Primary Tool | Schedule |
|-------|------|--------|--------------|----------|
| **행크** (Hank) | US market researcher | America | `us_research.py` | Every 30 min (daemon) |
| **유나** (Yuna) | Asia market researcher | Asia/Korea | `asia_research.py` | Every 30 min (daemon) |
| **레온** (Leon) | EU market researcher | Europe | `eu_research.py` | Every 30 min (daemon) |
| **한별** (Hanbyul) | Quantitative analyst | Global | `quant_analyzer.py` | On-demand |

### Tier 4: Integration

| Agent | Role | Primary Tool | Schedule |
|-------|------|--------------|----------|
| **마켓데스크** (Market Desk) | Global brief consolidator | `market_desk.py` | Every hour (daemon) |

---

## Data Flow

### 1. News & Market Data Collection

```
Web Sources (Yahoo Finance, DART, etc.)
    ↓
research.py (shared module)
    ├── fx() — USD rates vs. KRW/EUR/JPY
    ├── index_quote() — S&P500, KOSPI, etc.
    ├── dart_recent() — Corporate disclosures for watchlist
    └── web_brief() — LLM-powered web scraping summary
    ↓
Stored as: output/research/region_{us|asia|eu}.json
```

### 2. Regional Intelligence Integration

```
행크 (US)        유나 (Asia)       레온 (Europe)
  ↓                ↓                 ↓
region_us.json   region_asia.json  region_eu.json
  │                │                 │
  └────────────────┼─────────────────┘
                   ↓
            마켓데스크 (Market Desk)
                   ↓
         1. Aggregate indices/FX/news
         2. Match against Somi candidates
         3. LLM impact scoring (-2 to +2)
         4. Generate market_brief.md
                   ↓
         Telegram notification to user
```

### 3. Stock Analysis Pipeline

```
KIS API (Korea Investment & Securities)
    ↓
Watchlist (somi_watchlist.json)
    ↓
┌─ somi_kis_reporter.py (정기 보고)
│     • Supply/demand heatmap
│     • Volume, large trades
│     • Somi confidence score
│
├─ somi_screener.py (유망 종목 발굴)
│     • Top 100 stocks by daily turnover
│     • Score by Somi metrics
│     • Return top N candidates
│
├─ somi_trade_advisor.py (매수 판단)
│     • Buy signals based on regimes
│     • Entry price suggestions
│     • Position sizing by conviction
│
├─ somi_price_monitor.py (실시간 감시)
│     • Watch for ±5% moves
│     • Volume surges (>150% avg)
│     • Alert via Telegram
│
└─ somi_position_monitor.py (포지션 관리)
      • Check take-profit targets
      • Monitor stop-loss levels
      • Trail stops if enabled
```

### 4. Decision-to-Execution

```
User (Telegram)
    ↓ "종목 ABC 매수할까?"
    ↓
영숙 (Youngsuk) recognizes stock query
    ↓ Calls trade_advisor.analyze()
    ↓
somi_trade_advisor.py evaluates:
  • Regime (bull/bear/sideways)
  • Entry price
  • Risk/reward
  • Conviction score
    ↓
예원 (Yewon) routes to kis_trader.py
    ↓
kis_trader.place_order()
    ↓
Confirmation + order status back to Telegram
```

---

## Shared Module System

All agents inherit from **5 core modules** in `projects/ai-team/_shared/`:

### env.py — Environment & Secrets
- `load_env()`: Decrypt & load `.env` at startup
- `encrypt(plaintext, ciphertext)`: Symmetric encryption for secrets
- Validates API keys: Gemini, OpenAI, UPBIT, TELEGRAM, KIS, NOTION, SUPABASE, etc.

**Never hardcode credentials.** Always call `load_env()` first.

### llm.py — Unified LLM Client
Priority fallback chain:
1. **Ollama** (local, free): `deepseek-coder`, `qwen2.5` for coding/blog tasks
2. **GPT-4o-mini** (paid): Cloud fallback, function calling
3. **Gemini** (paid): Final fallback

```python
from _shared.llm import text

# Local-first (Ollama → GPT → Gemini)
response = text("프롬프트", lm_first=True, task="coding")

# Cloud-first
response = text("프롬프트", lm_first=False)

# JSON mode
response = text(prompt, json_mode=True)
```

### notify.py — Telegram & Daemon Status
- `send(msg)`: Post to Telegram chat
- `agent_status()`: Health check for all agents
  - Continuous daemons: `youngsuk`, `somi_monitor`
  - Scheduled services: `somi`, `somi_screener`, `somi_position`, `yewon_selfheal`, `harness`
  - Returns: `{"youngsuk": "up,12345", "somi": "scheduled", "harness": "down"}`

### process.py — Mutex & Duplicate Prevention
```python
from _shared.process import ProcessLock

with ProcessLock("somi_kis_reporter"):
    # Only one instance can execute here at a time
    # Prevents zombie processes, race conditions
```

### registry.py — Agent Registry (SSOT)
Single source of truth for agent metadata:
- Loads from `output/cache/agent_registry.json`
- Auto-discovers agents with `SKILL.md`
- Provides: `active_agents()`, `get_agent(id)`, `route_by_keyword(msg)`, `tools_for(agent_id)`

### research.py — Market Data Collection
Shared utilities for all research agents:
- `fx(codes)`: USD rates
- `index_quote(symbol)`: Yahoo Finance quotes
- `dart_recent(codes, days)`: Corporate filings
- `web_brief(prompt)`: LLM-powered web summaries
- `save_region()` / `load_region()`: JSON persistence
- `fear_greed()`: Market psychology index

---

## Process Management

### Continuous Daemons

Run 24/7 with process-level mutex to prevent duplicates:

| Daemon | Script | Role | Restarts |
|--------|--------|------|----------|
| `youngsuk` | `telegram_receiver.py` | Telegram polling | auto-restart if crash |
| `somi_monitor` | `somi_price_monitor.py` | Real-time alerts | auto-restart if crash |

Restart via:
```bash
# Windows
python projects/ai-team/skills/영숙_비서/tools/agent_controller.py 영숙 restart
python projects/ai-team/skills/소미_분석가/tools/somi_price_monitor.py --daemon
```

### Scheduled Services (launchd on macOS / Task Scheduler on Windows)

```
com.ailab.somi                15:40 KST (close-of-day report)
com.ailab.somi_screener       09:30, 15:50 KST (top candidates)
com.ailab.somi_position       장중 (position check)
com.ailab.yewon_selfheal      08:00 KST (system self-heal)
com.ailab.harness             09:00, 21:00 KST (health check)
```

정시 잡의 단일 진실원천은 `영숙_비서/tools/schedules.json`이며
`schedule_sync.py sync`가 잡별 `com.ailab.sched.*` launchd 에이전트로 materialize 한다.

### Ops Hygiene Automation (2026-07-04)

| Job | Schedule | Script (영숙_비서/tools/) | Role |
|-----|----------|--------------------------|------|
| `llm_probe` | 평일 07:25 | `llm_probe.py` | 로컬(Ollama)·클라우드(GPT/Gemini) 실제 챗 응답 점검 — 실패 시에만 텔레그램 경보 |
| `log_janitor` | 매일 04:10 | `log_janitor.py` | output 로그 회전(5MB+, copytruncate)·45일 사어 로그 삭제 |
| `state_backup` | 매일 20:00 | `state_backup.py` | output/cache(청산기록·watchlist)+.env → `~/ai_lab_backups` 스냅샷(14일 보관) |

`check_all.py`의 `ops_hygiene` 검사가 디스크 여유·백업 신선도(50h)를 감시한다.
데몬 생성 스냅샷 리포트(`reports/research/*_latest.md`)는 git 미추적(풀 배포 차단 방지).

---

## Directory Structure

```
ai_lab/
├── projects/ai-team/
│   ├── _shared/                 # Shared modules (5 files + research/registry)
│   │   ├── env.py              # Secrets management
│   │   ├── llm.py              # Unified LLM client
│   │   ├── notify.py           # Telegram + daemon status
│   │   ├── process.py          # Mutex/lock
│   │   ├── registry.py         # Agent metadata (SSOT)
│   │   ├── research.py         # Market data collection
│   │   ├── utils.py            # Path/resource utilities
│   │   ├── calendar_client.py  # Google Calendar API
│   │   └── agent_loop.py       # Agent polling loop
│   │
│   ├── skills/
│   │   ├── 예원_CEO/tools/
│   │   │   ├── yewon_dispatcher.py     # Main orchestrator
│   │   │   ├── yewon_orchestrator.py   # Task routing
│   │   │   ├── yewon_self_heal.py      # Auto-recovery
│   │   │   ├── harness_manager.py      # Health check
│   │   │   ├── skill_auditor.py        # Doc validation
│   │   │   ├── breaking_monitor.py     # News alerts
│   │   │   └── agent_factory.py        # Agent creation
│   │   │
│   │   ├── 영숙_비서/tools/
│   │   │   ├── telegram_receiver.py    # Main bot
│   │   │   ├── agent_controller.py     # Process mgmt
│   │   │   ├── schedule_manager.py     # Calendar sync
│   │   │   ├── schedule_sync.py        # iCal integration
│   │   │   ├── morning_brief.py        # Daily summary
│   │   │   ├── reports_manager.py      # Report archival
│   │   │   ├── calendar_manager.py     # Google Calendar
│   │   │   └── youtube_recommender.py  # Content feed
│   │   │
│   │   ├── 소미_분석가/tools/
│   │   │   ├── somi_kis_reporter.py    # Periodic reports
│   │   │   ├── somi_screener.py        # Find candidates
│   │   │   ├── somi_trade_advisor.py   # Buy signals
│   │   │   ├── somi_price_monitor.py   # Real-time watch
│   │   │   ├── somi_position_monitor.py│ # Position mgmt
│   │   │   ├── short_covering_analyzer.py │ # Short supply
│   │   │   ├── kis_trader.py           # Order execution
│   │   │   ├── stock_search.py         # Ticker lookup
│   │   │   ├── watchlist_manager.py    # Watchlist CRUD
│   │   │   ├── somi_signal_engine.py   # Entry/exit signals
│   │   │   ├── backtest.py             # Strategy backtesting
│   │   │   └── market_regime.py        # Regime detection
│   │   │
│   │   ├── 행크_미국조사/tools/
│   │   │   └── us_research.py          # US market intel
│   │   │
│   │   ├── 유나_아시아조사/tools/
│   │   │   └── asia_research.py        # Asia/Korea intel
│   │   │
│   │   ├── 레온_유럽조사/tools/
│   │   │   └── eu_research.py          # EU market intel
│   │   │
│   │   ├── 한별_퀀트/tools/
│   │   │   └── quant_analyzer.py       # Quantitative analysis
│   │   │
│   │   ├── 마켓데스크_시장종합/tools/
│   │   │   └── market_desk.py          # Global integration
│   │   │
│   │   └── 공용스킬/
│   │       └── SKILL.md documents
│   │
│   ├── harness/
│   │   └── check_all.py                # System health check
│   │
│   ├── src/
│   │   ├── extension.ts                # VS Code extension
│   │   ├── agents.ts                   # Agent registry (TS)
│   │   └── webview/                    # Frontend
│   │
│   └── tests/
│       └── (test files)
│
├── output/
│   ├── research/                       # Regional briefs
│   │   ├── region_us.json
│   │   ├── region_asia.json
│   │   ├── region_eu.json
│   │   ├── market_brief.md
│   │   └── market_brief.json
│   │
│   ├── cache/                          # Runtime cache
│   │   ├── somi_watchlist.json
│   │   ├── agent_registry.json
│   │   └── ...
│   │
│   ├── bot_logs/                       # Agent logs
│   │   ├── youngsuk.log
│   │   ├── somi.log
│   │   └── ...
│   │
│   └── trading_logs/                   # Trade history
│       └── kis_orders_*.json
│
├── .env                                # Encrypted secrets (NEVER commit)
└── CLAUDE.md                           # This repo's guidelines
```

---

## Key Data Structures

### Watchlist (somi_watchlist.json)

```json
{
  "종목코드": "종목명",
  "005930": "삼성전자",
  "000660": "SK하이닉스",
  "035720": "카카오"
}
```

Used by `somi_kis_reporter`, research agents, and market desk for scope.

### Regional Brief (region_{us|asia|eu}.json)

```json
{
  "region": "us",
  "indices": {
    "S&P500": {"close": 5800.5, "chg_pct": +1.2},
    "VIX": {"close": 14.2, "chg_pct": -2.1}
  },
  "fx": {"EUR": 1.09, "JPY": 147.5, "KRW": 1318},
  "macro": {"연준기금금리": 5.5},
  "web_issues": "어제 AI 호재로 나스닥 +2%, VIX -3%...",
  "updated": "2026-06-29T15:40:00"
}
```

### Market Brief (market_brief.json)

```json
{
  "indices": {
    "KOSPI": {"score": 0, "reason": "특이 뉴스 없음"},
    "005930": {"score": +1, "reason": "AI 칩 수급 호재"}
  },
  "compiled_at": "2026-06-29T16:00:00",
  "regions": {
    "us": {...},
    "asia": {...},
    "eu": {...}
  }
}
```

### Agent Registry (agent_registry.json)

```json
{
  "agents": {
    "somi": {
      "display": "소미",
      "folder": "소미_분석가",
      "role": "Domestic stock analyst",
      "keywords": ["소미", "국내주식", "수급"],
      "tools": [...],
      "daemons": {...},
      "scheduled": {...},
      "status": "active"
    },
    ...
  }
}
```

---

## Execution Schedules

### Market Hours (09:00~16:00 KST)

```
09:00  ├─ 예원 harness: Full system health check
       └─ 예원 yewon_selfheal: Auto-repair if needed

09:30  ├─ 소미 screener: Top 50 candidates by turnover
       ├─ 행크/유나/레온: Regional data refresh
       └─ 마켓데스크: Consolidated brief generation

12:00  ├─ 소미 kis_reporter: Noon market snapshot
       └─ Research agents: Continuous refresh (30-min intervals)

15:40  ├─ 소미 kis_reporter: Close-of-day report (watchlist)
       ├─ 소미 screener: Final candidates for next day
       ├─ 마켓데스크: Final brief before market close
       └─ Telegram notification to user

15:50  └─ 소미 screener: Last refresh before market close

Post-Market (16:00~)

18:00  └─ 예원 yewon_selfheal: Post-close health check
```

### Off-Market (16:00~09:00 KST)

```
24/7   ├─ 영숙 telegram_receiver: Polling for user commands (every 1 sec)
       ├─ 소미 somi_price_monitor: Alert on ±5% or +150% volume
       ├─ 행크/유나/레온: Continuous regional data (every 30 min)
       └─ 마켓데스크: Hourly brief generation
```

---

## Communication & Notification

### Telegram Flow

1. **User sends message** → `telegram_receiver.py` polls
2. **GPT-4o-mini function calling** parses intent:
   - `get_agent_status()` → Check daemon status
   - `dispatch()` → Route to CEO
   - `stock_search()` → Look up ticker
   - `trade()` → Place order via KIS
   - `fetch_brief()` → Get market brief
3. **Response** sent back to user with result/status

### Error Handling

All agents use **lenient error handling** with Telegram fallback:

```python
try:
    # risky operation
except Exception as e:
    send(f"⚠️ {AGENT_NAME} error: {e}")
    raise  # or swallow if non-critical
```

---

## Key Implementation Patterns

### 1. Import Pattern (6-level path finding)

```python
import os, sys
from pathlib import Path

_here = Path(__file__).resolve().parent
PROJECT_ROOT = _here.parents[4]  # 4 levels up to ai_lab/
AI_TEAM_ROOT = PROJECT_ROOT / "projects" / "ai-team"
sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.llm import text
from _shared.notify import send
from _shared.env import load_env

load_env(str(PROJECT_ROOT))
```

### 2. Mutex Lock (Daemon Protection)

```python
from _shared.process import ProcessLock

with ProcessLock("somi_kis_reporter"):
    # Prevents duplicate execution
    # Auto-cleans on exit
```

### 3. Scheduled Service (launchd plist)

```xml
<key>StartCalendarInterval</key>
<array>
  <dict>
    <key>Hour</key><integer>15</integer>
    <key>Minute</key><integer>40</integer>
  </dict>
</array>
```

---

## Security

### Environment Variables

- **All secrets encrypted** in `.env` (AES-256 via `env.py`)
- **Never committed** to git
- **Loaded at startup** via `load_env()`
- **Auto-decrypted** on read

Required keys:
```
TELEGRAM_BOT_TOKEN, TELEGRAM_CHAT_ID
OPENAI_API_KEY, GEMINI_API_KEY
KIS_ACCOUNT, KIS_PASSWORD
UPBIT_ACCESS_KEY, UPBIT_SECRET_KEY
DART_API_KEY
NOTION_API_KEY, NOTION_DATABASE_ID
```

### Code Protection

1. **No hardcoded credentials**
2. **Mutex prevents race conditions**
3. **Subprocess isolation** (env vars not leaked)
4. **Telegram fallback** for all errors
5. **JSON atomic writes** (`.tmp` + `os.replace()`)

---

## Troubleshooting

### Check Agent Status

```bash
python -c "from _shared.notify import status_report; print(status_report())"
```

### Restart a Daemon

```bash
# Telegram command
"영숙 재시작해"  → triggers agent_controller.py

# Manual restart (Windows)
python projects/ai-team/skills/영숙_비서/tools/agent_controller.py 영숙 restart
```

### View Logs

```bash
# Telegram logs
output/bot_logs/youngsuk.log

# System check
python projects/ai-team/harness/check_all.py

# Agent registry
python -m _shared.registry --all --json
```

### Force Data Refresh

```bash
# Somi report
python projects/ai-team/skills/소미_분석가/tools/somi_kis_reporter.py --send

# Market desk
python projects/ai-team/skills/마켓데스크_시장종합/tools/market_desk.py --send
```

---

## Limitations & Known Issues

1. **KIS API Rate Limits**: Max 40 requests/min (buffered)
2. **Ollama Required**: For local LLM fallback (optional but recommended)
3. **Process Lock Across FS**: Windows mutex names are case-insensitive
4. **launchd macOS Only**: Windows uses Task Scheduler (future work)
5. **Python 3.9+**: Required for walrus operator, type hints

---

## Future Enhancements

1. **Windows Task Scheduler Support**: Replace launchd for Windows 11
2. **Reinforcement Learning**: Trade signal optimization
3. **Multi-Account Support**: Separate watchlists per strategy
4. **Real-time Streaming**: WebSocket instead of polling
5. **Advanced Backtesting**: Walk-forward analysis, regime testing
