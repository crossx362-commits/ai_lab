# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

> 사람이 읽는 운영 매뉴얼·사고 런북·문서 색인은 [HANDBOOK.md](HANDBOOK.md)에 있다.

---

## 🏗️ Repository Structure

This is a monorepo containing:

1. **ai-team/** — Multi-agent AI automation framework (8 agents: 예원·영숙·봄이·수리·테오·백호·미오·나무)
2. **petnna/** — Pet healing platform (web/hybrid app)
3. **Root scripts/** — Daemon management

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

### Agent Roster

| Agent | Role | Key Tools |
|-------|------|-----------|
| 예원 (Yewon) | CEO — Task dispatcher & orchestrator, 펫나 긴급 회의 의장 | `yewon_dispatcher.py`, `upload_manager.py`, `petnna_council.py` |
| 영숙 (Youngsuk) | Secretary — Telegram bot & calendar | `telegram_receiver.py`, `calendar_manager.py` |
| 봄이 (Bomi) | QA — 펫나 상시 자동 순찰/검수 | `petnna_qa_patrol.py` (skills/봄이_QA, SKILL.md=QA 헌장) |
| 수리 (Suri) | Dev — 펫나 자동 개선 엔진 (QA 결과→수정→재검수→저위험 자동 병합, 백로그 과제는 항상 PR대기) | `petnna_dev_engine.py` (skills/수리_개발자, SKILL.md=개발 헌장) |
| 테오 (Teo) | Test — E2E 테스트 자동 작성·실행 (2회 연속 통과 시 채택, flaky 폐기) | `petnna_test_engineer.py` (skills/테오_테스트) |
| 백호 (Baekho) | Backend — Supabase 스키마·RLS·프론트 계약 감사 (매일, 읽기 전용) | `petnna_backend_guard.py` (skills/백호_백엔드) |
| 미오 (Mio) | Design — 주 1회 스크린샷 기반 디자인 리뷰 → 백로그 적재 | `petnna_design_review.py` (skills/미오_디자인) |
| 나무 (Namu) | PM — 주 1회 웹서치 트렌드·경쟁 조사 → 기능 백로그 적재 | `petnna_product_manager.py` (skills/나무_기획) |

### Shared Module System

All agents use centralized utilities in `projects/ai-team/_shared/`:

| Module | Purpose |
|--------|---------|
| `env_loader.py` | Load encrypted `.env` from project root |
| `gemini_client.py` | Gemini API wrapper (text/image/vision) |
| `ollama_client.py` | Local Ollama LLM with task-based model selection |
| `telegram_notifier.py` | Send notifications to Telegram |
| `process_lock.py` | Windows Named Mutex for preventing duplicate processes |
| `agent_status.py` | Get status reports for all agents |
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
- `ANTHROPIC_API_KEY` (Codex fallback)
- `INSTAGRAM_ACCESS_TOKEN`, `INSTAGRAM_ACCOUNT_ID`
- `NOTION_API_KEY`, `NOTION_DATABASE_ID`
- `SUPABASE_URL`, `SUPABASE_ANON_KEY`

---

## 📱 Telegram Bot (영숙)

### Natural Language Commands

The bot uses Gemini Function Calling to map natural language to tools:

- **"현황 보고해줘" / "다들 뭐해?"** → `get_agent_status()` (shows all agents)
- **"일정 알려줘" / "캘린더 확인해봐"** → `list_calendar()`
- **"에이전트 작업 요청"** → `dispatch()` → CEO orchestration

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

## Imported Claude Cowork project instructions

---

## 🏛️ Workspace Harness Architect — 구조 관리 지침

### 하네스(Harness) 운영 규칙

- `projects/ai-team/harness/`는 **읽기 전용 검증 엔진**입니다. 하네스 자체를 수정하지 마십시오.
- 폴더 정리, 마이그레이션, 파일 이동 등 구조적 변화 전후에는 반드시 실행:
  ```bash
  python projects/ai-team/harness/check_all.py
  ```
- 라이브 런타임 경로는 Producer + Consumer 에이전트가 함께 마이그레이션되기 전까지 **이동 금지**

### 파일 배치 정책

| 유형 | 경로 |
|------|------|
| 에이전트 전용 툴 | `projects/ai-team/skills/<agent>/tools/` |
| 공용 헬퍼 | `projects/ai-team/_shared/` |
| 일회성 진단 스크립트 | `projects/ai-team/scripts/agents/` |
| 정식 시스템 스크립트 | `projects/ai-team/scripts/` |
| 연구·분석 리포트 | `reports/` |
| 런타임 로그·미디어 | `output/` (Git 제외) |

> ❌ 루트에 새 스크립트 생성 금지 / ❌ 평문 `.env` 복사본 금지 / ❌ `__pycache__` Git 포함 금지

### 에이전트 도메인 경계

| 에이전트 | 전담 역할 | 진입 파일 |
|---------|----------|----------|
| **예원_CEO** | 오케스트레이션·라우팅·하네스 체크·워치독 | `yewon_dispatcher.py`, `harness_manager.py`, `harness_monitor.py` |
| **영숙_비서** | 텔레그램 봇·스케줄러·캘린더 | `telegram_receiver.py`, `schedule_manager.py` |
| **봄이_QA** | 펫나 QA 상시 순찰 | `petnna_qa_patrol.py` |
| **수리_개발자** | 펫나 자동 개선 엔진 (봄이 결과 소비, 격리 브랜치 수정·게이트 병합) | `petnna_dev_engine.py` |
| **테오_테스트** | 펫나 E2E 테스트 자동 작성·실행 | `petnna_test_engineer.py` |
| **백호_백엔드** | Supabase 계약 감사 (읽기 전용) | `petnna_backend_guard.py` |
| **미오_디자인** | 디자인 리뷰 → 공유 백로그(`output/qa/petnna/backlog.json`) | `petnna_design_review.py` |
| **나무_기획** | 기획 PM → 공유 백로그 | `petnna_product_manager.py` |

> 주식·코인 관련 에이전트(소미·한별·행크·유나·레온·마켓데스크 등)는 2026-07-08 오너 지시로 전부 삭제됨 (git 이력에서 복구 가능).

### 주요 런타임 파일 (이동 금지)

| 파일 | Producer → Consumer |
|------|---------------------|
| `skills/영숙_비서/tools/schedules.json` | 설정 → 영숙 스케줄러 |
| `_shared/calendar_cache.md` | 구글 캘린더 → 영숙 봇 |
