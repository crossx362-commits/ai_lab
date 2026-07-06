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

### Agent Roster (7 Agents — 실존, 2026-07-04 역할 재편)

> 백엔드(스킬 폴더·실행 데몬)가 있는 에이전트는 아래 7명. 과거 가짜 에이전트(데이브·레오·시그널·코다리·케빈·경수·티모·로율 등)는 제거됨. 마켓데스크는 예원에 흡수(2026-07-04).
>
> **소유권 재배치(2026-07-04)**: 소미 8역할 부담을 분산. **도구 파일은 물리적으로 그대로**(대부분 `소미_분석가/tools/`) 두고, `notify.py`의 `_AGENT_LABELS`에서 **소유(명찰)만 이동** — launchd·워치독·import·데몬 키(`somi_advisor` 등) 전부 불변(안전). "왜 한별 도구가 소미 폴더에?" = 이 재배치 때문(파일 이동 안 함).

| Agent | Role | Key Tools (파일 위치 불변, 소유만 표기) |
|-------|------|-----------|
| 예원 (Yewon) | CEO — 통합관리자: 오케스트레이션·시장종합·브리핑·추세·하네스·정시잡 | `yewon_dispatcher.py`, `harness_manager.py`, `market_desk.py`, `morning_note.py`, `somi_kis_reporter.py`(정기리포트), `market_trend_alert.py` |
| 영숙 (Youngsuk) | Secretary — 텔레그램 게이트웨이 | `telegram_receiver.py`(+`bot_common.py`·`bot_tools_info.py`), `schedule_manager.py`, `agent_controller.py` |
| 소미 (Somi) | Analyst — 실시간 감시/집행 코어 | `somi_price_monitor.py`, `somi_position_monitor.py`, `somi_us_trader.py`, `short_covering_analyzer.py`, `watchlist_manager.py` |
| 한별 (Hanbyul) | Quant — 정량 매매 두뇌: 전략 검증·백테스트·매수판단·발굴·신호·튜닝 | `quant_analyzer.py`, `backtest.py`(전략·게이트 검증), `somi_trade_advisor.py`, `somi_screener.py`, `somi_signal_engine.py` |
| 행크 (Hank) | US research desk | `us_research.py` |
| 유나 (Yuna) | Asia research desk | `asia_research.py` |
| 레온 (Leon) | Europe research desk | `eu_research.py` |

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
- **52주 신고가 전략 = 모의 전용 실험(웹 연구 2026-07-05, 한별 소유)** — `advisor.analyze_candidate`가 `_is_paper() and _is_52w_high(kis,code)`이면 +8점 가점(250일 일봉 고가 98%+ 돌파). 기관 상승추세 편승(웹 'best-performing'). **실거래 미적용**(`_is_paper` 가드 — 250일 조회·가점 전부 모의 후보만, 실거래 부하·영향 0). backtest `--compare`의 `+52주신고가+국면`으로 기존 전략 대비 우위 검증 후에만 실거래 도입 판단. 담당: 한별(전략·백테스트). 어닝 갭 홀드는 실적 데이터 필요라 후순위.
- **모의 매수는 고정 슬롯 아님 — 발굴 주기 + 고속감시 동적 진입(2026-07-02)** — 발굴(스크리너·뉴스·공시)은 10분 주기(`SOMI_DISCOVERY_MIN`), 사이엔 60초 고속감시(`_fast_watch`)가 발굴 후보만 실시간 재평가해 즉시 매수(`slot=buy_fast`). 마감권(15:00~)은 고속감시 중단·buy_close 규율. 실거래는 기존 보수 슬롯 유지. **실행 주체는 소미제안 상시 데몬**(`somi_trade_advisor --daemon`) — launchd 정시 propose(09:30/15:50)만으론 동적 진입이 안 돈다(데몬 가동 시 one-shot은 자동 스킵). "매수가 아예 없다" = 소미제안 데몬 생존부터 확인.
- **당일 외국인/기관 수급: 확정치는 마감 후, 장중엔 잠정치(가집계)로 채점** — KIS `investor-trend-estimate`(HHPTJ04160200)가 장중 외인·기관 추정 순매수 제공(`somi_kis_reporter.investor_estimate`). 잠정 수급은 `score_mode=intraday_estimated`로 정상 채점(가점 8→6, dq -5). 가집계 미가용 시에만 구 방식 폴백(5일 누적 보정 `morning_missing_investor_adjusted` — 실거래 차단·모의 허용). "수급 미확정이라 거래 안 됨" 재발 시 가집계 API 응답부터 확인.
- **뉴스는 스케줄 아니라 매매 직전 반영** — 마켓데스크 정시(07:50/15:20)만 믿지 마라. 매수 슬롯 직전 거래대상 관련 지역(국내주식=아시아/한국)만 재수집→`issue_impact` 재평가(`_refresh_news_for_trade`). 미국/유럽은 KR 장중 미개장이라 아침 스냅샷 유지.
- **"학습 인사이트 실패" = 크레딧부터 확인(2026-07-02)** — OpenAI·Gemini 둘 다 429 `insufficient_quota`(크레딧 소진)면 전 에이전트가 로컬로 강등된다(영숙 GPT 함수호출 포함). 로컬 최후선: `OLLAMA_MODEL` 소형 강제(e2b)는 JSON 프롬프트에 빈 응답/깨진 JSON을 뱉으므로 llm.py가 json_mode 검증 실패 시 최대 모델(12b)로 자동 승급. `lm_first=False`는 이제 실제 클라우드 우선(기존엔 무시됨). json_mode 호출은 max_tokens 1500↑ — 700이면 finish=length로 JSON이 잘린다.
- **클라우드 클로드는 구독(`claude -p`) 1선 — API 크레딧 막힘 대응(2026-07-05 오너 지시)** — `llm._claude_code`가 Claude Code headless(`claude -p --append-system-prompt`)를 subprocess로 호출해 **구독 사용량으로 클로드**를 쓴다(API 크레딧 불필요). 체인: Ollama→**ClaudeCode(구독)**→Gemini→Claude API(크레딧 백업). 품질은 로컬 gemma 압도(전문 퀀트 수준 JSON). **주의 ①`--bare` 금지**(API키 인증만 읽어 구독 OAuth 무시) **②구독은 Max 플랜**(2026-07-05 확인)이라 rate limit 여유 — 구독 클로드를 적극 활용해도 된다(Pro였다면 빠듯). 단 Max도 무한은 아니니 로컬 우선(`AI_TEAM_LLM_PRIMARY=ollama`) 기조는 유지해 균형. `lm_first=False`(issue_impact 등) 작업은 구독이 1선. ③subprocess라 API보다 느림. **로컬 모델은 `OLLAMA_MODEL` 핀 없이 설치 모델 자동감지**(2026-07-05) — 올라마에 켜둔 모델을 `_pick_ollama`가 자동 선택(현재 gemma4:12b), 모델 교체 시 자동 반영.
- **GPT도 구독으로 — Codex CLI headless(`codex exec`)로 ChatGPT Plus 사용(2026-07-05 오너 지시)** — 클로드(`claude -p`)와 동형. `llm._gpt_codex`가 `codex exec --skip-git-repo-check -o <파일> <프롬프트>`로 호출, **`-o`(output-last-message)가 순수 응답만 파일에 뽑아** 훅 로그(`hook: SessionStart` 등)와 분리한다. json_mode는 프롬프트 지시 + `_json_ok` 검증. **주의: ChatGPT는 Plus 플랜이라 Claude Max보다 rate limit 빠듯** → 체인에서 구독 클로드 다음 2선에만 두고 로컬+클로드가 대부분 커버하게 해 Plus 한도 소진 방지. 전체 클라우드 체인: **구독 클로드(Max) → 구독 GPT(Plus) → Gemini → GPT API → 클로드 API**. 구독 둘이 무료(크레딧 불필요) 1·2선, API 넷은 크레딧 백업.
- **`issue_impact` json_mode는 로컬도 가능 — `format:"json"` 강제(2026-07-05 해결)** — 과거 "로컬은 json_mode 미적용이라 잡문(`JSON 드릴게요…`)을 뱉어 파싱 실패 → 클라우드 고정"이 문제였다. `_ollama`가 json_mode일 때 body에 `"format":"json"`을 넣으면 **로컬(gemma)도 파싱 가능한 JSON을 강제 출력**한다(검증됨: `{"verdict":"buy",...}`). 이제 클라우드 크레딧 0이어도 issue_impact가 로컬로 작동. 체인은 여전히 Claude→Gemini→로컬(`lm_first=False`, 품질순)이나 **로컬이 최후 보루로 실제 기능**한다. GPT 퇴출(오너 지시) — 영숙 함수호출도 클로드 tool use(`ANTHROPIC_BOT_MODEL`, haiku). 매 슬롯 재평가 비파괴(비면 직전값 복원). json_mode는 여전히 max_tokens 1500↑.
- **클라우드 모델은 최저가 haiku 고정 — 비용 최소화(오너 지시 2026-07-05)** — `ANTHROPIC_MODEL` 기본 `claude-haiku-4-5`(opus 대비 ~5배 저렴). 고품질이 꼭 필요한 특정 작업만 `.env ANTHROPIC_MODEL`로 상향. **주의: Opus만 temperature 미지원(400)** — `_claude`는 `"opus" not in model`일 때만 temperature 전송(haiku/sonnet은 분류 temperature=0 반영, opus는 생략). max_tokens는 output 기준 과금이라 낮추지 마라(절감 미미 + json 잘림 위험, 가드레일 1500↑ 유지). 토큰 절감의 핵심은 모델(haiku)+로컬 우선이지 max_tokens가 아니다.
- **조기청산은 여유·유예·반등대기** — 매수 직후 소폭 눌림에 즉시 컷하면 휘프소. VWAP -2% 여유 + 매수후 15분 유예(`SOMI_EARLY_GRACE_MIN`) + 호가 매수세 우위(반등 예측)면 손실이라도 대기. 하방은 상위 하드손절(-3%/ATR)이 우선 컷하므로 완화해도 안전.
- **로컬 최후선은 '모델 존재'가 아니라 '챗 응답 성공'으로 판정(2026-07-03)** — 로컬 주력 gemma4:e2b가 두 겹으로 죽어 있었다: ①Ollama 자동 업그레이드 중단이 매니페스트를 깨 전 호출 400(list엔 보이나 show는 404 — `ollama pull` 재수복), ②e2b는 **thinking 모델**이라 OpenAI 호환(/v1) 경로에선 추론이 reasoning 필드로 새며 max_tokens를 소진해 content가 빈다(7/2 'e2b 빈 응답' 가드레일의 진범). 수리: `_ollama`를 네이티브 `/api/chat`+`think:false`로 전환(정상+고속), 후보별 실패 시 다음 설치 모델 승계, `market_desk._build_issue_impact` 평가 실패 시 직전값 유지(비파괴)+잡문 JSON 구제. 클라우드 429/크레딧0과 겹치면 LLM 전멸 — "issue_impact 비어 있음" 경보 = ①클라우드 크레딧 ②Ollama 생존·매니페스트(show로 확인) 순 점검.
- **영숙 새 기능은 4곳 등록** — 함수 정의 + `AVAILABLE_FUNCTIONS` + `TOOLS`(GPT 스키마) + 시스템 프롬프트 규칙. 하나라도 빠지면 봇이 함수 못 부르고 일반 회피 답변. 종목 뉴스는 `get_stock_news`(`research.web_brief`).
- **OS 이관/인프라 교체는 두 플랫폼 모두 확인** — 6/28 launchd 이관이 Windows 정시 잡 실행자(`schedule_manager --daemon`)를 차단해 14개 잡이 나흘간 조용히 정지(예원 다이제스트·속보감시 등). 실행자 교체는 `sys.platform` 분기 필수 + 하네스 체크(check_all)도 같은 분기로 검증. "정기 보고가 안 온다" = 정시 잡 실행자 생존부터 확인.
- **게이트 하한은 백테스트 근거 — 유니버스별로 다르다(2026-07-02)** — 대형주 40종목: 55↑ 흑자, 60↑ 손익비 1.2+. 그러나 **소미 실사냥터(코스닥·중소형 30종목) 전이검증에선 55는 수급확인을 더해도 손실(-60%), 60+수급확인부터 흑자(+45%·PF 1.39), 65+수급확인 승률 92%·PF 12(표본 12건)**. 모의 gate_score 기본 60·하한 58. 하락 국면은 차단 대신 +10 선별(`SOMI_BEAR_GATE_BUMP`, 대형주 분기점 65) — **단 모의는 오너 지시(2026-07-03)로 BUMP=0(.env)**: 데이터 수집이 목적이라 하락장에도 기본 60으로 거래(무차별 bear 매수도 백테스트 +23.6% 흑자). 수급확인·뉴스판정 게이트는 유지 — 하락장 무체결의 주범은 국면 가산이 아니라 '수급 음수 + 무재료 급등' 조합이며 이건 계속 걸러야 한다. RSI·상대강도 추가 필터는 과필터 — 추가 금지. 주의: 실전 점수엔 수급 가점(6~8)이 포함돼 백테스트 눈금보다 부풀려짐 — 최종 눈금은 한별 점수버킷(실데이터)이 보정한다. 재검증 없이 58 밑으로 내리지 마(`backtest.py --grid` + 중소형 유니버스 오버라이드).
- **수급확인 게이트는 최대 엣지원 — 끄지 마라(2026-07-02 백테스트)** — 모멘텀 단독 +62%(샤프 2.3) vs 수급확인 추가 +154%(승률 71%·PF 2.06·MDD 절반·샤프 5.0). `SOMI_SOOMGEUP_GATE` 기본 true 유지. **예외(오너 지시 2026-07-03): 모의 데이터 수집기는 게이트 4종 완화** — ①수급확인 .env off ②뉴스판정 watch 허용(avoid는 차단) ③MC 기대값은 차단→기록 전환(_is_paper 분기 — 폭락장 90일 분포는 전종목 기대수익 음수라 전면 무체결) ④진입점수 55→40(SOMI_GATE_ENTRY_PAPER) ⑤탐지점수 58(검증 하한) ⑥LLM 뉴스판정은 상위 16후보만(클라우드 전멸 시 로컬 판정이 사이클을 20분+로 늘림). 폭락주간에 이 게이트들이 겹겹이 전면 무체결을 만들어 발굴이 무의미해지던 문제(7/3 코스닥 -5%일에 58로 첫 체결 확인). 공시악재·리스크·관찰시간은 유지. 12개월 그리드 재검증(7/3): 55=PF1.21·+41% 흑자(추가 완화 여지), **50=PF0.9·-67% 전멸(금지)**. 완화 배포 시 주의: 커밋마다 워치독이 데몬을 재배포해 진행 중 발굴 사이클(5~20분)을 죽인다 — 연속 커밋은 무체결을 스스로 연장시킴. MC 수치는 매수 기록에 저장돼 한별 튜닝 학습 데이터가 된다. **실거래 전환 시 반드시 복원**(env 2줄 삭제 + 코드는 _is_paper라 자동). 눌림목(PF 1.06)·평균회귀(-94%)는 데이터로 기각 — 재도입 금지. 조용한매집(선취)은 **유니버스 의존이 극단적** — 대형주 문턱 65·보유 5일은 승률 67%·PF 2.77·+120%·MDD -21%(채택: `somi_accum_scanner`, 평일 16:40 관찰 편입 전용), 같은 신호가 중소형에선 -99%/749건 참사(중소형 적용 절대 금지). 신호 산식은 `backtest._accum_levels` 단일 소스 — 스캐너와 백테스트 정의 분리 금지. 학습 데이터 배관: 청산→`somi_closed_trades.json`→한별 tune(평일 15:45, 성장엔진 16:10 선행)·성과추적(금 16:30)·성장엔진 learn.
- **미장 모의는 게이트가 '창'이다 — 국내와 역방향(2026-07-02)** — 미국 대형주 전이검증(24개월): 문턱 48~52만 흑자(PF 1.5·+73%·MDD -34%), **55↑는 블로우오프 전패** — 상한 차단 필수(`SOMI_US_GATE_LO/HI`, 모의 기본 44~54 — 아래 2026-07-03 완화 검증). 수급확인이 없어 국내 대비 약한 엣지 감안. 실행: `somi_us_trader`(야간 22:30~05:00 KST=맥 담당, 야후시세+USD 내부원장, 실거래 경로 없음, SPY>MA20 국면 전용, 슬롯 3·손절 -3%·목표 +8%·시간청산 7일). 청산기록 `somi_us_closed.json` — 점수 눈금이 달라 국내 학습(somi_closed_trades)과 절대 혼합 금지.
- **미장 모의 무체결은 대부분 '신호 희소'가 원인 — 창 하한 44로 완화(2026-07-03)** — 원창 48~54는 24개월 35건(최근 3개월 히트 4건)이라 밤새 체결 0이 정상처럼 보였다. 창 그리드 재검증: 44~54=130건·PF 1.25·+66%·샤프 1.46 채택, 43 이하는 PF 붕괴(35↓ 전멸)·55↑ 금지 유지. 무체결 의심 시 ①그 밤 실행 기계에 데몬 생존(7/2 윈도우 함대 전멸이 병행 원인) ②스캔 주기 로그의 후보 수 ③창 그리드 순으로 확인. 부수 결함: regime 조회 실패 시 `max({})` 크래시로 매수가 조용히 전면 정지 — 빈 국면 가드 추가.
- **분할매수는 '돌파 증액'만 유효 — 눌림 분할은 역선택(2026-07-03 연구, 모의 적용됨)** — 40종목 12/24개월: 돌파분할(50%+50%@+2%, 3일 유효)×현행청산 = PF 2.43·24mo +568%·MDD -19%·샤프 5.26 (일괄 대비 샤프 2.2→5.3). 구현: `_auto_buy_paper` 50% 진입 + `_addon_scale_in`(고속감시 주기) 돌파 증액, 평단·tp1/tp2 재산정, 손절은 원신호 유지. 전부 `_is_paper()` 한정 — 실거래 도입은 모의 실데이터 검증 후. 눌림 분할(-2% 추가)은 약한 종목일수록 풀포지션이 되는 역선택으로 성과 반토막 — 도입 금지. 분할매도는 현행(+5% 절반+트레일)이 최적 근처, 3단 세분화는 승자 이익만 깎음. 트레일온리는 누적 최고나 승률 37%로 기각. 상세: `docs/SPLIT_ENTRY_EXIT_STUDY_2026-07-03.md`.
- **급등주 진입비용이 엣지를 절반 먹는다(2026-07-02)** — 중소형 60+수급확인 기준 슬리피지 0.1→0.3%에서 누적 +45%→+23%, 0.5%면 +4.6%로 소멸. 모의 시장가 체결에 편도 0.1% 슬리피지 반영(`SOMI_PAPER_SLIP`, 지정가 미적용) — 모의 성과의 실전 과대평가 방지. 참고: 보유기간 20일(`SOMI_MAX_HOLD_DAYS`)은 다기간(12/24/30mo) 재검증 근거 — 단일기간 백테스트 결과로 낮추지 마라(7일이 좋아 보여도 승자를 일찍 놔줌).
- **ATR 변동성 손절은 중소형에서 해로워 미채택(웹연구+백테스트 2026-07-06)** — 웹 근거(1.5~2.0×ATR가 모멘텀 표준·고정% 대비 우위)로 `_levels`에 ATR 손절을 시험. 대형주 40종목 12mo에선 ATR2.0이 개선처럼 보였으나(누적 44→77%·샤프 0.85→0.94, 단 MDD -55→-67% 악화), **중소형 20종목(실사냥터) 재검증에선 전 배수(0.75/1.0/1.5/2.0) 모두 현행보다 손익비 열등**(PF 0.39~0.52 vs 현행 0.67). 한국 중소형주는 일간 ATR이 5~11%로 커서 2×ATR이 실용 손절선을 넘어 캡에 박히고, rr만 붕괴. 대형주→중소형 전이 실패의 교과서 사례. **`_levels`의 -3% 캡 유지, ATR 손절 재도입 금지**(재시도 전 중소형 백테스트 우위부터). 검증 도구는 `backtest.py`에 상주: `SOMI_BT_SMALL=1 python backtest.py --compare`로 중소형 유니버스(`SMALL_UNIVERSE`) + ATR 변형(`_score_atrNN_levels`) 재현 가능. 부차 확인: RSI·상대강도·52주신고가 필터는 중소형 하락구간에서 거래수를 줄여 MDD를 낮추나(상대강도 PF 0.98·MDD -24%), 이는 '덜 거래해 덜 잃음'이라 엣지와 구분 불가 — 과필터 금지 기조 유지.
- **운영 기계 분담: 장중=Windows PC, 맥은 꺼지는 날 있음(2026-07-02)** — 맥은 아침(~06:45) 종료→저녁(19:00) 부팅하는 날이 있어 장중 매매(소미제안 데몬·발굴·데스크)는 Windows 프로세스가 주인. 맥 launchd 장중 잡은 맥이 켜진 날의 안전망(데몬 가동 시 one-shot 자동 스킵). 코드 배포는 **git pull만 하면 됨** — 워치독이 HEAD 변화를 감지해 변경 폴더의 데몬을 새 코드로 자동 교체(`harness_monitor.restart_on_code_update`, `_shared` 변경 시 전 데몬·자신은 자가교체). "장중에 안 돌았다" = 그 시간 어느 기계가 켜져 있었는지부터 확인.
- **재부팅 복구는 워치독 launchd 상주가 전제(2026-07-02)** — 7/2 재부팅 후 launchd 비관리 상시 데몬(예원모니터·추세알림·모닝노트·성장엔진)이 반나절 전멸. 원인: 워치독 `_restart_bot`이 macOS에서 `com.ailab.<이름>` kickstart만 시도 → 라벨 없는 데몬은 조용히 실패. 수정: 라벨은 `_LAUNCHD_FALLBACK`으로 해석 + kickstart 실패 시 agent_controller 폴백, 워치독 자신은 `com.ailab.yewon_monitor`(KeepAlive) 상주(설치: `deploy/install_yewon_monitor.command`), 자가복구(yewon_self_heal)도 상시 데몬을 실제 재시작. "재부팅 후 데몬 전멸" = 워치독 launchd 적재부터 확인.

---

## 📚 Documentation

- **Agent details**: `AGENTS.md`
- **AI model strategy**: `projects/ai-team/docs/AI_MODEL_STRATEGY.md`
- **Security rules**: `docs/setup/ENV_SECURITY_RULES.md`
- **Telegram bot**: `TELEGRAM_BOT_README.md`
- **Petnna setup**: `projects/petnna/README.md`
