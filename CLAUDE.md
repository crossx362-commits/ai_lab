# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## 📖 시스템 이해 필수 문서

**작업 전 반드시 읽기**: [`docs/AI_LAB_SYSTEM_ARCHITECTURE.md`](docs/AI_LAB_SYSTEM_ARCHITECTURE.md)

- 전체 에이전트 구조와 데이터 플로우
- 각 컴포넌트 역할과 의존성
- 공유 모듈 설명 및 사용 패턴
- 실행 스케줄과 시스템 동작 원리

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

### 모델 선택

- **루틴 작업 → mini 모델**: 간단한 수정, 반복 작업, 명확한 패치
- **복잡한 작업 → 현재 모델**: 아키텍처 변경, 디버깅, 설계

---

## 🏗️ Repository Structure

```
ai_lab/
├── projects/
│   ├── ai-team/
│   │   ├── _shared/              # 공통 클라이언트 (from _shared.xxx로 임포트)
│   │   │   ├── env.py            # 환경변수 로드/암호화/검증
│   │   │   ├── llm.py            # LLM 통합 (Ollama → GPT-4o-mini → Gemini)
│   │   │   ├── notify.py         # 텔레그램 알림 + 에이전트 상태
│   │   │   ├── process.py        # 프로세스 락 + 중복 방지
│   │   │   └── utils.py          # 경로/리소스/ffmpeg 유틸
│   │   ├── skills/               # 에이전트별 도구 (한국어 폴더명)
│   │   │   ├── 예원_CEO/tools/   yewon_dispatcher.py, harness_manager.py, skill_auditor.py
│   │   │   ├── 영숙_비서/tools/  telegram_receiver.py (Flask webhook + GPT-4o-mini)
│   │   │   ├── 소미_분석가/tools/ somi_kis_reporter.py, short_covering_analyzer.py
│   │   │   └── 공용스킬/         공통 스킬 마크다운 문서
│   │   ├── scripts/              # 운영 스크립트 (대부분 각 에이전트 tools/로 재배치)
│   │   ├── harness/              # check_all.py — 시스템 점검
│   │   ├── security/            # ecc 보안 컴포넌트
│   │   ├── src/                  # VS Code 익스텐션 (TypeScript: extension.ts, agents.ts)
│   │   └── tests/                # 테스트
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

### macOS — 데몬 운영

에이전트 데몬은 `agent_controller.py`로 제어하고, 정기 서비스는 launchd(`com.ailab.*`)로 관리된다.

```bash
# 개별 에이전트 제어 (영숙 | 소미 | 예원 | 영숙스케줄)
python projects/ai-team/skills/영숙_비서/tools/agent_controller.py 영숙 start
python projects/ai-team/skills/영숙_비서/tools/agent_controller.py 소미 status

# launchd 정기 서비스 상태
launchctl list | grep com.ailab
```

서비스 목록:
- `com.ailab.youngsuk` — 영숙: Flask webhook 서버 (포트 5000)
- `com.ailab.somi` — 소미: 주식 분석 리포터 (정기 보고)
- `com.ailab.yewon_monitor` — 예원: 하네스 모니터

### 수동 재시작 (개별 서비스)
```bash
# agent_controller로 개별 제어 (start|stop|restart|status)
python projects/ai-team/skills/영숙_비서/tools/agent_controller.py 영숙 restart
python projects/ai-team/skills/영숙_비서/tools/agent_controller.py 소미 restart

# 소미 즉시 보고 (watchlist 등록 종목)
python projects/ai-team/skills/소미_분석가/tools/somi_kis_reporter.py --send
```

---

## 🤖 AI Agent System Architecture

### Agent Roster (8 Agents — 실존, 2026-07-02 갱신)

> 백엔드(스킬 폴더·실행 데몬)가 있는 에이전트는 아래 8명. 과거 가짜 에이전트(데이브·레오·시그널·코다리·케빈·경수·티모·로율 등)는 제거됨.

| Agent | Role | Key Tools |
|-------|------|-----------|
| 예원 (Yewon) | CEO — Task dispatcher & orchestrator | `yewon_dispatcher.py`, `harness_manager.py`, `skill_auditor.py`, `evaluate_feedback.py`, `daily_feedback_scheduler.py` |
| 영숙 (Youngsuk) | Secretary — Telegram bot (GPT-4o-mini, polling) | `telegram_receiver.py`, `schedule_manager.py`, `agent_controller.py` |
| 소미 (Somi) | Analyst — Stock analysis & scoring | `somi_kis_reporter.py`, `somi_trade_advisor.py`, `somi_screener.py`, `short_covering_analyzer.py`, `watchlist_manager.py` |
| 마켓데스크 | Market synthesis — 07:50/15:20 시장 종합·issue_impact | `market_desk.py` |
| 행크 (Hank) | US research desk | `us_research.py` |
| 유나 (Yuna) | Asia research desk | `asia_research.py` |
| 레온 (Leon) | Europe research desk | `eu_research.py` |
| 한별 (Hanbyul) | Quant analysis | `quant_analyzer.py` |

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

1. **ALL secrets live in `/Users/junholee/ai_lab/.env`** (encrypted)
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

## 📊 Stock Analysis (소미 — 국내주식)

> 과거 크립토 자동매매(데이브·레오·시그널/upbit 봇)는 모두 제거됨. 현재는 소미의 국내주식 수급 분석만 존재하며, **자동매매 아님** — `kis_trader.py`는 명시적 주문만 체결한다.

### 소미 컴포넌트

1. **정기 보고** (`somi_kis_reporter.py`): KIS API로 watchlist 등록 종목의 수급·점수를 정기(오전/정오/마감) 분석 보고. `--send` 즉시 전송, `--daemon --times` 데몬 모드.
2. **유망종목 발굴** (`somi_screener.py`): 거래대금 상위 후보를 소미 점수로 채점해 상위 종목 추천.
3. **매수 판단** (`somi_trade_advisor.py`): 매수 가능 구간·제안.
4. **실시간 감시** (`somi_price_monitor.py`): watchlist 종목 급등락·거래량 급증 실시간 감시(상시 데몬).
5. **포지션 관리** (`somi_position_monitor.py`): 보유 포지션 익절/손절 점검.
6. **수급/숏커버링** (`short_covering_analyzer.py`): 대차잔고·공매도 기반 분석.

### 관심종목 (watchlist)

보고 대상은 `output/cache/somi_watchlist.json`의 등록 종목으로 결정된다(고정 종목 없음). 텔레그램 "관심종목 추가 <코드> <종목명>"으로 등록/관리(`watchlist_manager.py`).

### 주요 명령
```bash
# watchlist 종목 즉시 보고
python projects/ai-team/skills/소미_분석가/tools/somi_kis_reporter.py --send

# 유망종목 스캔
python projects/ai-team/skills/소미_분석가/tools/somi_screener.py --send --top 5

# 관심종목 목록
python projects/ai-team/skills/소미_분석가/tools/watchlist_manager.py list
```

---

## 📱 Telegram Bot (영숙)

### Natural Language Commands

The bot uses Gemini Function Calling to map natural language to tools:

- **"현황 보고해줘" / "다들 뭐해?"** → `get_agent_status()` (실존 에이전트 현황)
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
3. Register in: `src/agents.ts` (AGENTS) + `_shared/notify.py` (CONTINUOUS_DAEMONS/SCHEDULED_SERVICES) + `agent_controller.py` (실행 대상)
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

## 🔧 하네스 가드레일 (세션 학습 — 실패를 하네스에 반영)

> 반복된 실패를 규칙으로 박아 에이전트가 같은 실수를 안 하게 한다. 새 사고 발생 시 여기 1줄 추가.

- **지수 등락률은 `chartPreviousClose`로 계산 금지** — `range=5d`에선 5거래일 전 종가라 일간 등락이 5일 누적으로 부풀려진다. 반드시 일봉 종가 시리즈의 직전 거래일 종가로 계산(`research.index_quote`). ±25% 초과 등락률은 산출오류로 보고 자동 폐기.
- **지수/종목 심볼 맵은 누락 점검** — 코스닥(`^KQ11`)처럼 빠지면 조용히 리포트에서 사라진다. 심볼 맵 수정 시 실제 조회로 개수 검증.
- **발굴 유니버스는 단일 축 금지** — 거래대금순 한 축만 보면 매번 초대형주만 나온다. 거래대금·거래증가율·회전율 다축 병합(`somi_screener.get_candidates`). "왜 같은 종목만" = 축 편향 의심.
- **모의(paper) 운영 중 `somi_signal_engine`(소미신호) 중지** — 실거래 승인푸시 전용이라 모의 중엔 혼선. 자동매수 단일 실행자는 `somi_trade_advisor`. (코드 강제: `scan()` 최상단 paper 가드가 푸시 차단·대기신호 비움 — 하네스가 자동재시작해도 무해)
- **모의 원칙: 종일 공격적 매수, 실거래는 보수** — 완화(문턱↓·수급미확정 허용·조기청산 여유)는 전부 `_is_paper()` 분기로만. 실거래 보수값은 절대 건드리지 마. "모의인데 왜 매수 안 되냐"는 대부분 이 분기 누락.
- **모의 매수는 고정 슬롯 아님 — 발굴 주기 + 고속감시 동적 진입(2026-07-02)** — 발굴(스크리너·뉴스·공시)은 10분 주기(`SOMI_DISCOVERY_MIN`), 사이엔 60초 고속감시(`_fast_watch`)가 발굴 후보만 실시간 재평가해 즉시 매수(`slot=buy_fast`). 마감권(15:00~)은 고속감시 중단·buy_close 규율. 실거래는 기존 보수 슬롯 유지.
- **당일 외국인/기관 수급: 확정치는 마감 후, 장중엔 잠정치(가집계)로 채점** — KIS `investor-trend-estimate`(HHPTJ04160200)가 장중 외인·기관 추정 순매수 제공(`somi_kis_reporter.investor_estimate`). 잠정 수급은 `score_mode=intraday_estimated`로 정상 채점(가점 8→6, dq -5). 가집계 미가용 시에만 구 방식 폴백(5일 누적 보정 `morning_missing_investor_adjusted` — 실거래 차단·모의 허용). "수급 미확정이라 거래 안 됨" 재발 시 가집계 API 응답부터 확인.
- **뉴스는 스케줄 아니라 매매 직전 반영** — 마켓데스크 정시(07:50/15:20)만 믿지 마라. 매수 슬롯 직전 거래대상 관련 지역(국내주식=아시아/한국)만 재수집→`issue_impact` 재평가(`_refresh_news_for_trade`). 미국/유럽은 KR 장중 미개장이라 아침 스냅샷 유지.
- **`issue_impact` 평가는 GPT(json_mode) 고정** — 로컬 모델은 json_mode 미적용이라 JSON 파싱 실패가 잦고, 실패 시 빈 결과로 덮어써 뉴스 신호가 통째로 유실된다. `market_desk._build_issue_impact`는 GPT→Gemini→로컬 순. 매 슬롯 재평가는 비파괴(비면 직전값 복원).
- **조기청산은 여유·유예·반등대기** — 매수 직후 소폭 눌림에 즉시 컷하면 휘프소. VWAP -2% 여유 + 매수후 15분 유예(`SOMI_EARLY_GRACE_MIN`) + 호가 매수세 우위(반등 예측)면 손실이라도 대기. 하방은 상위 하드손절(-3%/ATR)이 우선 컷하므로 완화해도 안전.
- **영숙 새 기능은 4곳 등록** — 함수 정의 + `AVAILABLE_FUNCTIONS` + `TOOLS`(GPT 스키마) + 시스템 프롬프트 규칙. 하나라도 빠지면 봇이 함수 못 부르고 일반 회피 답변. 종목 뉴스는 `get_stock_news`(`research.web_brief`).
- **OS 이관/인프라 교체는 두 플랫폼 모두 확인** — 6/28 launchd 이관이 Windows 정시 잡 실행자(`schedule_manager --daemon`)를 차단해 14개 잡이 나흘간 조용히 정지(예원 다이제스트·속보감시 등). 실행자 교체는 `sys.platform` 분기 필수 + 하네스 체크(check_all)도 같은 분기로 검증. "정기 보고가 안 온다" = 정시 잡 실행자 생존부터 확인.
- **재부팅 복구는 워치독 launchd 상주가 전제(2026-07-02)** — 7/2 재부팅 후 launchd 비관리 상시 데몬(예원모니터·추세알림·모닝노트·성장엔진)이 반나절 전멸. 원인: 워치독 `_restart_bot`이 macOS에서 `com.ailab.<이름>` kickstart만 시도 → 라벨 없는 데몬은 조용히 실패. 수정: 라벨은 `_LAUNCHD_FALLBACK`으로 해석 + kickstart 실패 시 agent_controller 폴백, 워치독 자신은 `com.ailab.yewon_monitor`(KeepAlive) 상주(설치: `deploy/install_yewon_monitor.command`), 자가복구(yewon_self_heal)도 상시 데몬을 실제 재시작. "재부팅 후 데몬 전멸" = 워치독 launchd 적재부터 확인.

---

## 📚 Documentation

- **Agent details**: `AGENTS.md`
- **AI model strategy**: `projects/ai-team/docs/AI_MODEL_STRATEGY.md`
- **Security rules**: `docs/setup/ENV_SECURITY_RULES.md`
- **Telegram bot**: `TELEGRAM_BOT_README.md`
- **Petnna setup**: `projects/petnna/README.md`
