# AI Lab System Architecture

**Last Updated**: 2026-07-08
**System Status**: 8 agents — orchestration/secretary (예원·영숙) + Petnna QA/dev team (봄이·수리·미오·나무·백호·테오)

---

## Executive Summary

AI Lab runs a multi-agent automation system centered on **Petnna** (a pet-care web app) plus general
orchestration/Telegram infrastructure. Stock/crypto trading agents (소미·한별·행크·유나·레온·마켓데스크,
and the earlier 데이브·레오·시그널·펄스 generation) were fully removed 2026-07-08 per owner instruction —
recoverable from git history if ever needed. The system now combines:

1. **Orchestration**: 예원 (CEO) dispatches tasks, runs the harness/watchdog, reviews skill docs
2. **Telegram Gateway**: 영숙 (secretary) handles all natural-language interaction, calendar, scheduled jobs
3. **Petnna QA loop**: 봄이 (QA patrol) finds issues → 수리 (dev engine) fixes low-risk ones in isolated branches
4. **Petnna growth loop**: 미오 (design review) + 나무 (product/roadmap) propose improvements → shared backlog → 수리 implements
5. **Petnna reliability**: 백호 (backend/schema guard) audits Supabase contract drift, 테오 (test engineer) writes/runs E2E tests

There is no automated trading or financial execution anywhere in this system.

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
    │  - Function calling → tools            │
    │  - Calendar, scheduled jobs, reports   │
    └────┬─────────────────────────┬────────┘
         │                         │
    ┌────▼────────────┐      ┌────▼──────────────────┐
    │ Direct tools     │      │ Task Dispatch         │
    │ (calendar/status)│      │ → yewon_dispatcher    │
    └──────────────────┘      └────┬──────────────────┘
                                    │
                                    ▼
    ┌────────────────────────────────────────┐
    │  예원 (Yewon) — CEO Orchestrator       │
    │  - Harness health check (check_all.py) │
    │  - Watchdog (harness_monitor.py)       │
    │  - Skill auditor, content feedback     │
    └─────────────────────────────────────────┘

┌─────────────────────────── Petnna QA/Dev Loop ───────────────────────────┐
│                                                                            │
│  봄이 (QA patrol) ──findings──┐                                          │
│  미오 (design review) ──ideas─┼──► output/qa/petnna/backlog.json          │
│  나무 (product/PM) ──ideas────┘              │                            │
│                                               ▼                           │
│                              수리 (dev engine): isolated git worktree,    │
│                              claude -p headless fix, re-run 봄이 gate,    │
│                              auto-merge only low-risk P2/P3               │
│                                               │                           │
│  백호 (backend guard) ──schema/RLS audit──────┤                           │
│  테오 (E2E tests) ──write+run Playwright──────┘                           │
│                                                                            │
└────────────────────────────────────────────────────────────────────────┘
```

---

## Agent Roster

### Core Orchestration (Always Running)

| Agent | Role | Primary Tool | Type |
|-------|------|--------------|------|
| **영숙** (Youngsuk) | Telegram secretary bot | `telegram_receiver.py` | Continuous daemon (24/7 polling) |
| **예원** (Yewon) | CEO — dispatch, harness, watchdog | `yewon_dispatcher.py`, `harness_manager.py`, `harness_monitor.py` | Continuous daemon + reactive |

### Petnna QA/Dev Team

| Agent | Role | Primary Tool | Cadence |
|-------|------|--------------|---------|
| **봄이** (Bomi) | QA patrol — console/JS errors, 404s, broken images, a11y, SEO basics | `petnna_qa_patrol.py` | Daily patrol + on file-change |
| **수리** (Suri) | Dev engine — reads 봄이/미오/나무 findings, fixes one low-risk issue per cycle in an isolated worktree branch via `claude -p`, re-gates with 봄이, auto-merges only if safe | `petnna_dev_engine.py` | Continuous daemon |
| **미오** (Mio) | Design review — weekly screenshot-based UX/visual review | `petnna_design_review.py` | Weekly (Mon 11:00) |
| **나무** (Namu) | Product/PM — weekly feature roadmap & competitor research, proposals only (no code) | `petnna_product_manager.py` | Weekly (Tue 11:00) |
| **백호** (Baekho) | Backend guard — Supabase schema/RLS vs. frontend contract audit | `petnna_backend_guard.py` | Daily |
| **테오** (Teo) | E2E test engineer — writes + runs Playwright tests for uncovered flows | `petnna_test_engineer.py` | Daily + on file-change |

**Safety rails on 수리** (the only agent that writes to `master`): always works in an isolated git
worktree/branch, never touches files outside `projects/petnna/`, refuses to merge if it touches
forbidden paths (supabase/api/migrations/payments/env/deploy config), refuses merge if 봄이's
re-check shows the issue unresolved or metrics regressed, gives up after 3 failures on the same
issue (escalates to a notification instead of looping). P0/P1 findings always go to a human, never
auto-merged.

---

## Data Flow: Petnna QA/Dev Loop

```
봄이 (daily patrol) ──┐
미오 (weekly design) ──┼──► output/qa/petnna/backlog.json (shared backlog)
나무 (weekly roadmap) ─┘
                                    │
                                    ▼
                    수리 picks highest-impact, lowest-risk item
                                    │
                    isolated git worktree branch
                    claude -p headless makes a minimal fix
                                    │
                    re-run 봄이 patrol against the branch (gate)
                                    │
              ┌─────────────────────┴─────────────────────┐
              ▼                                            ▼
       gate passes, low-risk P2/P3               gate fails OR P0/P1/high-risk
              │                                            │
       auto-merge to master                    leave branch, notify for review
```

백호 and 테오 run independently (schema audit, E2E suite) and file their own findings into the
same backlog/notification channels; they do not gate 수리's merges directly today.

---

## Shared Module System

All agents inherit from core modules in `projects/ai-team/_shared/`:

### env.py — Environment & Secrets
- `load_env()`: Decrypt & load `.env` at startup
- `encrypt(plaintext, ciphertext)`: Symmetric encryption for secrets
- Validates required API keys (Telegram, Gemini, Notion, Supabase, etc.)

**Never hardcode credentials.** Always call `load_env()` first.

### llm.py — Unified LLM Client
Priority fallback chain (all subscription-based — no paid API calls by default, owner instruction):
1. **Ollama** (local, free) — auto-detects installed model
2. **Claude Code CLI** (`claude -p`, subscription) — primary cloud fallback
3. **Codex CLI** (`codex exec`, subscription) — secondary cloud fallback
4. **Gemini** (free tier) — final fallback

```python
from _shared.llm import text

# Local-first (Ollama → Claude subscription → Codex subscription → Gemini)
response = text("프롬프트", lm_first=True, task="coding")

# Cloud-first
response = text("프롬프트", lm_first=False)
```

### notify.py — Telegram & Daemon Status
- `send(msg)`: Post to Telegram chat
- `agent_status()`: Health check for all agents
  - `CONTINUOUS_DAEMONS`: `youngsuk`, `yewon`, `bomi_qa`, `scheduler`
  - `SCHEDULED_SERVICES`: `yewon_selfheal`, `harness`
  - Returns e.g. `{"youngsuk": "up,12345", "harness": "scheduled"}`

### process.py — Mutex & Duplicate Prevention
```python
from _shared.process import ProcessLock

with ProcessLock("some_daemon_name"):
    # Only one instance can execute here at a time
    # Prevents zombie processes, race conditions
```

### research.py — HTTP + Notion Helpers
Trimmed 2026-07-08 to only the functions with live consumers: `_get`/`get_json` (HTTP),
`load_market_brief()` (always returns `{}` now — no producer since 마켓데스크 was removed;
kept for `morning_brief.py`'s fallback path), `notion_page()`/`notion_report()` (used by
`reports_manager.py`, `notion_publish.py`, `notify.py`). Region/watchlist/DART/fx/index-quote
functions were removed — no consumers remain; restore from git history if a research agent
returns.

### registry.py — Agent Registry (SSOT)
- Loads from `output/cache/agent_registry.json`, merged with dynamic discovery of
  `skills/<agent>/SKILL.md` folders
- New agents are added by `agent_factory.py` as `status: quarantined` until approved

---

## Process Management

### Continuous Daemons

| Daemon | Script | Role |
|--------|--------|------|
| `youngsuk` | `telegram_receiver.py` | Telegram polling |
| `yewon` | `harness_monitor.py` | Watchdog — auto-restarts down daemons, redeploys on `git pull` |
| `bomi_qa` | `petnna_qa_patrol.py` | Petnna QA patrol |
| `scheduler` | `schedule_manager.py` | Windows-only scheduled-job executor (macOS uses launchd instead) |

Restart via:
```bash
python projects/ai-team/skills/영숙_비서/tools/agent_controller.py 영숙 restart
python projects/ai-team/skills/영숙_비서/tools/agent_controller.py 봄이 restart
```

### Scheduled Services (launchd on macOS / Task Scheduler on Windows)

```
com.ailab.yewon_selfheal      08:00 KST (system self-heal)
com.ailab.harness             09:00, 21:00 KST (health check)
```

정시 잡의 단일 진실원천은 `영숙_비서/tools/schedules.json`이며
`schedule_sync.py sync`가 잡별 `com.ailab.sched.*` launchd 에이전트로 materialize 한다.

### Ops Hygiene Automation

| Job | Schedule | Script (영숙_비서/tools/) | Role |
|-----|----------|--------------------------|------|
| `llm_probe` | 평일 07:25 | `llm_probe.py` | 로컬(Ollama)·구독 클로드·Gemini 실제 챗 응답 점검 — 실패 시에만 텔레그램 경보 |
| `log_janitor` | 매일 04:10 | `log_janitor.py` | output 로그 회전(5MB+, copytruncate)·45일 사어 로그 삭제 |
| `state_backup` | 매일 20:00 | `state_backup.py` | output/cache + .env → `~/ai_lab_backups` 스냅샷(14일 보관) |

`check_all.py`의 `ops_hygiene` 검사가 디스크 여유·백업 신선도를 감시한다.

---

## Directory Structure

```
ai_lab/
├── projects/ai-team/
│   ├── _shared/                 # Shared modules
│   │   ├── env.py              # Secrets management
│   │   ├── llm.py              # Unified LLM client (Ollama → subscription CLIs → Gemini)
│   │   ├── notify.py           # Telegram + daemon status
│   │   ├── process.py          # Mutex/lock
│   │   ├── registry.py         # Agent metadata (SSOT)
│   │   ├── research.py         # HTTP + Notion helpers (trimmed 2026-07-08)
│   │   ├── growth.py           # Agent self-learning records/proposals
│   │   ├── utils.py            # Path/resource utilities
│   │   ├── calendar_client.py  # Google Calendar API
│   │   └── agent_loop.py       # Agent polling loop
│   │
│   ├── skills/
│   │   ├── 예원_CEO/tools/
│   │   │   ├── yewon_dispatcher.py     # Main orchestrator
│   │   │   ├── yewon_orchestrator.py   # Task routing
│   │   │   ├── yewon_self_heal.py      # Auto-recovery
│   │   │   ├── harness_manager.py      # Health check wrapper
│   │   │   ├── harness_monitor.py      # Watchdog daemon
│   │   │   ├── skill_auditor.py        # Doc validation
│   │   │   ├── daily_feedback_scheduler.py  # Content feedback
│   │   │   └── agent_factory.py        # Agent creation
│   │   │
│   │   ├── 영숙_비서/tools/
│   │   │   ├── telegram_receiver.py    # Main bot
│   │   │   ├── agent_controller.py     # Process mgmt
│   │   │   ├── schedule_manager.py     # Scheduled-job executor
│   │   │   ├── morning_brief.py        # Daily summary
│   │   │   ├── reports_manager.py      # Report archival
│   │   │   ├── calendar_manager.py     # Google Calendar
│   │   │   └── llm_probe.py            # LLM chain health probe
│   │   │
│   │   ├── 봄이_QA/tools/petnna_qa_patrol.py
│   │   ├── 수리_개발자/tools/petnna_dev_engine.py
│   │   ├── 미오_디자인/tools/petnna_design_review.py
│   │   ├── 나무_기획/tools/petnna_product_manager.py
│   │   ├── 백호_백엔드/tools/petnna_backend_guard.py
│   │   ├── 테오_테스트/tools/petnna_test_engineer.py
│   │   │
│   │   └── 공용스킬/
│   │       └── SKILL.md documents
│   │
│   ├── harness/
│   │   └── check_all.py                # System health check
│   │
│   ├── plugins/                        # Claude Code plugin marketplace (youngsuk-briefing only)
│   │
│   ├── src/
│   │   ├── extension.ts                # VS Code extension
│   │   └── agents.ts                   # Agent registry (TS)
│   │
│   └── tests/
│
├── output/
│   ├── qa/petnna/                      # QA backlog, dev-engine reports, test results
│   ├── cache/                          # Runtime cache (agent registry, watchdog state)
│   └── bot_logs/                       # Agent logs
│
├── .env                                # Encrypted secrets (NEVER commit)
└── CLAUDE.md                           # This repo's guidelines
```

---

## Communication & Notification

### Telegram Flow

1. **User sends message** → `telegram_receiver.py` polls
2. Function calling parses intent: `get_agent_status()`, `list_calendar()`, `dispatch()` → CEO orchestration
3. **Response** sent back to user with result/status

### Error Handling

All agents use **lenient error handling** with Telegram fallback:

```python
try:
    # risky operation
except Exception as e:
    send(f"⚠️ {AGENT_NAME} error: {e}")
```

---

## Key Implementation Patterns

### 1. Import Pattern (root-finding)

```python
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "..", ".."))

from _shared.llm import text
from _shared.notify import send
from _shared.env import load_env

load_env()
```

### 2. Mutex Lock (Daemon Protection)

```python
from _shared.process import ProcessLock

with ProcessLock("daemon_name"):
    # Prevents duplicate execution, auto-cleans on exit
```

---

## Security

- **All secrets encrypted** in `.env`, never committed
- **Loaded at startup** via `load_env()`, auto-decrypted on read
- **No hardcoded credentials**, mutex prevents race conditions, Telegram fallback for all errors
- **JSON atomic writes** (`.tmp` + `os.replace()`)

---

## Troubleshooting

### Check Agent Status
```bash
python -c "from _shared.notify import status_report; print(status_report())"
```

### Restart a Daemon
```bash
python projects/ai-team/skills/영숙_비서/tools/agent_controller.py 영숙 restart
```

### View Logs / System Check
```bash
python projects/ai-team/harness/check_all.py
```

---

## Limitations & Known Issues

1. **Ollama Required**: For local LLM fallback (optional but recommended)
2. **launchd macOS Only**: Windows uses the `scheduler` daemon (`schedule_manager.py --daemon`) instead
3. **수리's auto-merge is petnna-only**: any change outside `projects/petnna/` requires a human
