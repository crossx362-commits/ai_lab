# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## 🎯 에이전트 작업 지침

**목표**: 이 파일 하나만 수정해.  
**범위**: [파일명] 외에는 열지 마.  
**금지**: 전체 리팩터링, 전체 검색, 의존성 추가, 전체 테스트 실행 금지.

**진행 절차**:
1. 먼저 원인과 수정 계획을 5줄 이내로 말해.
2. 내가 승인하면 수정해.
3. 수정 후 diff만 보여줘.
4. 테스트는 내가 지정한 명령 하나만 실행해.

**출력 규칙**: 짧게. 전체 파일 내용 붙여넣지 마.

### Usage Control

- **토큰 최소화**: 요청과 직접 관련된 파일만 읽기
- **타겟 검색**: 전체 저장소 스캔 금지 (명시 요청 시 제외)
- **리팩터링 제한**: 광범위한 리팩터링 금지 (명시 요청 시 제외)
- **5개 파일 이상 읽기 전 승인 요청**
- **타겟 패치 우선**: 전체 재작성 금지
- **전체 파일 출력 금지**: 간결한 diff만 표시
- **전체 테스트 금지**: 최소한의 관련 테스트만 (명시 요청 시 제외)
- **긴 로그 금지**: 핵심 줄만 요약 표시
- **의존성/아키텍처 변경 전 승인 요청**

---

## 🏗️ Repository Structure

```
ai_lab/
├── projects/
│   ├── ai-team/
│   │   ├── _shared/              # 공통 클라이언트 (from _shared.xxx로 임포트)
│   │   │   ├── env_loader.py
│   │   │   ├── gemini_client.py  # LLM: Ollama → GPT-4o-mini → Gemini 폴백
│   │   │   ├── ollama_client.py
│   │   │   ├── telegram_notifier.py
│   │   │   ├── process_lock.py   # fcntl 파일 락 (중복 실행 방지)
│   │   │   └── agent_registry.py
│   │   ├── skills/               # 에이전트별 도구 (한국어 폴더명)
│   │   │   ├── 예원_CEO/tools/   yewon_dispatcher.py, upload_manager.py
│   │   │   ├── 영숙_비서/tools/  telegram_receiver.py (봇 + 감시 스레드)
│   │   │   ├── 코다리_개발자/tools/ web_preview.py, ollama_health_check.py
│   │   │   ├── 케빈_인프라/tools/ vercel_manager.py, supabase_manager.py
│   │   │   ├── 티모_디자이너/tools/ petnna_reviewer.py
│   │   │   ├── 펄스_전략가/tools/ crypto_market_intelligence.py
│   │   │   ├── 데이브_주식/tools/ upbit_auto_trader.py, upbit_analyzer.py
│   │   │   ├── 레오_트레이더/tools/ leo_aggressive_trader.py
│   │   │   ├── 경수_수사관/tools/ comment_forensics.py
│   │   │   ├── 로율_변호사/tools/ tax_simulator.py
│   │   │   └── 공용스킬/         공통 스킬 마크다운 문서
│   │   ├── scripts/              # 시스템 운영 스크립트
│   │   │   ├── launchd/          # macOS LaunchAgent plist + install.sh
│   │   │   ├── agents/           # 에이전트 API 연결 테스트
│   │   │   ├── security/         # 시크릿 암호화/복호화
│   │   │   ├── start_trading_team.py
│   │   │   ├── cleanup_duplicate_processes.py
│   │   │   ├── check_holdings.py
│   │   │   ├── daily_balance_check.py
│   │   │   └── monitor_processes.py
│   │   └── reports/research/     # 펄스이 생성한 시장 인텔 JSON
│   └── petnna/                   # Pet 플랫폼 웹앱 (index.html + js/css)
├── output/
│   ├── trading_logs/             # 봇별 stdout/stderr 로그
│   ├── bot_logs/                 # 영숙 로그
│   └── media/                    # 생성된 영상/음악 파일
├── docs/                         # 설계 문서
├── connect-ai/                   # LLM fine-tuning 데이터 (별도 프로젝트)
├── connect-ai-packs/             # 스킬 팩 템플릿
├── .env                          # 암호화된 시크릿 (절대 커밋 금지)
└── CLAUDE.md                     # 이 파일
```

---

## 🚀 Running the System

### macOS — launchd (정식 운영 방식)

봇 4개는 macOS LaunchAgent로 관리됩니다. OS 부팅 시 자동 시작, 충돌 시 자동 재시작.

```bash
# 전체 설치 및 시작
bash projects/ai-team/scripts/launchd/install.sh

# 상태 확인
launchctl list | grep com.ailab

# 전체 중지 및 제거
bash projects/ai-team/scripts/launchd/uninstall.sh
```

서비스 목록:
- `com.ailab.pulse` — 펄스: 시장 인텔 수집
- `com.ailab.dave` — 데이브: 보수적 업비트 자동매매
- `com.ailab.leo` — 레오: 공격적 데이트레이딩
- `com.ailab.youngsuk` — 영숙: 텔레그램 봇 + 중복 프로세스 감시

### 수동 재시작 (개별 봇)
```bash
launchctl kickstart -k gui/$(id -u)/com.ailab.dave
```

### 보유 현황 / 잔고 확인
```bash
python3 projects/ai-team/scripts/check_holdings.py
python3 projects/ai-team/scripts/daily_balance_check.py
```

---

## 🤖 AI Agent System Architecture

### Agent Roster (13 Agents)

| Agent | Role | Key Tools |
|-------|------|-----------|
| 예원 (Yewon) | CEO — Task dispatcher & orchestrator | `yewon_dispatcher.py`, `upload_manager.py` |
| 영숙 (Youngsuk) | Secretary — Telegram bot & calendar | `telegram_receiver.py`, `calendar_manager.py` |
| 코다리 (Kodari) | Developer — Web dev & health checks | `web_preview.py`, `ollama_health_check.py` |
| 케빈 (Kevin) | Infra — Vercel & Supabase management | `setup_vercel.py`, `deploy_*.py` |
| 티모 (Timo) | Designer — UI/UX review | `petnna_reviewer.py` |
| 펄스 (pulse) | Strategist — Crypto market research | `crypto_market_intelligence.py` |
| 데이브 (Dave) | Trader — Conservative crypto trading | `upbit_auto_trader.py` |
| 레오 (Leo) | Trader — Aggressive day trading | `leo_aggressive_trader.py` |
| 경수 (Kyungsu) | Investigator — Malicious comment detection | security tools |
| 로율 (Royul) | Lawyer — Legal/tax/compliance | compliance tools |

### Shared Module System (Unified, 5 Files)

All agents use **5 centralized modules** in `projects/ai-team/_shared/`:

| Module | Purpose |
|--------|---------|
| **`env.py`** | Load/encrypt/validate environment variables |
| **`llm.py`** | Unified LLM client (Ollama → GPT → Gemini fallback) |
| **`notify.py`** | Telegram notifications + agent status |
| **`process.py`** | Process lock + duplicate content guard |
| **`utils.py`** | Path/resource/ffmpeg/image upload utilities |

**Standard import pattern** for all agents:
```python
#!/usr/bin/env python3
import os, sys
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from _shared.env import load_env
from _shared.llm import text
from _shared.notify import send
from _shared.process import ProcessLock
from _shared.utils import find_root

load_env()
```

---

## 🧠 AI Model Strategy (Unified LLM Client)

Priority: **Ollama (local, free) → GPT-4o-mini → Gemini (cloud, paid)**

### Unified LLM Client (`_shared/llm.py`)

- **Coding tasks**: Prefers `deepseek-coder`, `codestral` (Ollama)
- **Blog/caption writing**: Prefers `qwen2.5` (excludes deepseek)
- **Cloud fallback**: GPT-4o-mini → Gemini

Force a specific model:
```bash
export OLLAMA_MODEL=deepseek-coder:latest
```

### Usage

```python
from _shared.llm import text

# Local-first (Ollama → GPT → Gemini)
response = text("프롬프트", lm_first=True, task="coding")

# Cloud-first (GPT → Gemini → Ollama)
response = text("프롬프트", lm_first=False)

# Direct access
from _shared.llm import ollama, gpt, gemini
result = ollama("프롬프트", task="blog")
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
python projects/ai-team/_shared/env.py encrypt .env .env.encrypted
```

Decrypt for editing:
```bash
python projects/ai-team/_shared/env.py decrypt .env.encrypted .env.decrypted
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
3. Register in: `projects/ai-team/_shared/agent_registry.py`
4. Update: `AGENTS.md`

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

- **Agent details**: `AGENTS.md`
- **AI model strategy**: `projects/ai-team/docs/AI_MODEL_STRATEGY.md`
- **Security rules**: `docs/setup/ENV_SECURITY_RULES.md`
- **Telegram bot**: `TELEGRAM_BOT_README.md`
- **Petnna setup**: `projects/petnna/README.md`
