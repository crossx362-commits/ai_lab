# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## 📖 시스템 이해 필수 문서

**⚡ 통합 작업 지침**: [`DIRECTIVES.md`](DIRECTIVES.md) — **모든 작업(대화형·헤드리스·자동 루프) 전 필수 준수.** 오너 원칙 6개·작업 절차·안전 규칙·검증 체크리스트·진단 순서의 단일 요약. 아래 「하네스 가드레일」은 그 원장(개별 사고 세부)이다.

**전체 문서 색인·권위 등급**: [`HANDBOOK.md`](HANDBOOK.md) — 문서끼리 모순되면 §1-2 등급이 판정한다(위쪽이 이김).

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

**틀린 것·지적받은 것은 근본 수리 + 학습 (오너 지시 2026-08-06)**: 오너에게 지적당했거나 스스로 틀렸다고 확인된 것은 그 자리만 고치고 넘어가지 마라. 근본 원인을 찾아 재발이 구조적으로 불가능하게 만든 뒤 지침·가드레일에 1줄 남긴다. **시스템 사고뿐 아니라 오너의 지적·나의 오답·내가 어긴 지침도 전부 이 사이클의 대상이다.** "다음엔 조심하겠다"는 수리가 아니다. 절차는 [DIRECTIVES.md](DIRECTIVES.md) §5.

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
│   │   │   ├── 영숙_비서/tools/  telegram_receiver.py (Flask webhook)
│   │   │   ├── 봄이_QA/tools/    petnna_qa_patrol.py
│   │   │   └── 공용스킬/         공통 스킬 마크다운 문서
│   │   ├── scripts/              # 운영 스크립트 (대부분 각 에이전트 tools/로 재배치)
│   │   ├── harness/              # check_all.py — 시스템 점검
│   │   ├── security/            # ecc 보안 컴포넌트
│   │   ├── src/                  # VS Code 익스텐션 (TypeScript: extension.ts, agents.ts)
│   │   └── tests/                # 테스트
│   └── petnna/                   # Pet 플랫폼 웹앱 (index.html + js/css)
├── output/
│   ├── bot_logs/                 # 봇 로그
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
# 개별 에이전트 제어 (영숙 | 예원 | 영숙스케줄 | 봄이)
python projects/ai-team/skills/영숙_비서/tools/agent_controller.py 영숙 start
python projects/ai-team/skills/영숙_비서/tools/agent_controller.py 봄이 status

# launchd 정기 서비스 상태
launchctl list | grep com.ailab
```

서비스 목록:
- `com.ailab.youngsuk` — 영숙: Flask webhook 서버 (포트 5000)
- `com.ailab.yewon_monitor` — 예원: 하네스 모니터 (워치독)

### 수동 재시작 (개별 서비스)
```bash
# agent_controller로 개별 제어 (start|stop|restart|status)
python projects/ai-team/skills/영숙_비서/tools/agent_controller.py 영숙 restart
```

---

## 🤖 AI Agent System Architecture

### Agent Roster (10 Agents — 2026-07-08 주식·코인 전면 삭제, 펫나 개발팀 6명 신설, 2026-08-13 게임 개발팀 1명, 2026-08-16 이미지품질 1명)

> 오너 지시(2026-07-08)로 주식·코인 관련 에이전트(소미·한별·행크·유나·레온·마켓데스크·지아)와 도구·스케줄·데몬 전부 삭제.
> 남은 펫나 개발팀 8명 + 게임 개발팀 2명(마루·별이).

| Agent | Role | Key Tools |
|-------|------|-----------|
| 예원 (Yewon) | CEO — 오케스트레이션·하네스·워치독·콘텐츠 피드백 | `yewon_dispatcher.py`, `harness_manager.py`, `harness_monitor.py`, `skill_auditor.py`, `daily_feedback_scheduler.py`, `petnna_pipeline_audit.py`(주 1회 파이프라인 딥 로직 감사) |
| 영숙 (Youngsuk) | Secretary — 텔레그램 게이트웨이·일정·정시 잡 | `telegram_receiver.py`, `schedule_manager.py`, `agent_controller.py`, `calendar_manager.py` |
| 봄이 (Bomi) | QA — 펫나 상시 순찰 | `petnna_qa_patrol.py` |
| 수리 (Suri) | Dev — 펫나 자동 개선 엔진: QA 결과→격리 브랜치 수정→재검수→저위험만 자동 병합. QA 이슈 없으면 백로그(미오·나무 과제) 구현(항상 PR대기) | `petnna_dev_engine.py` (헌장: `skills/수리_개발자/SKILL.md`, 산출물: `output/qa/petnna/dev/`) |
| 테오 (Teo) | Test — E2E 테스트 자동 작성(하루 1개, 2회 연속 통과 시 채택·flaky 폐기)·매일+변경 시 실행 | `petnna_test_engineer.py` (테스트: `projects/petnna/tests/e2e/`, 결과: `output/qa/petnna/tests/`) |
| 백호 (Baekho) | Backend — Supabase 스키마·RLS vs 프론트 쿼리 계약 감사(매일 10:30, 읽기 전용) | `petnna_backend_guard.py` (보고서: `output/qa/petnna/backend/`) |
| 미오 (Mio) | Design — 주 1회(월) 스크린샷 기반 UX·시각 리뷰 → 공유 백로그 적재 | `petnna_design_review.py` (보고서: `output/qa/petnna/design/`) |
| 나무 (Namu) | PM — 주 1회(화) 웹서치 트렌드·경쟁 조사 → 기능 백로그 적재 | `petnna_product_manager.py` (보고서: `output/qa/petnna/product/`) |
| 마루 (Maru) | Game Dev — 유니티 빌드·성능·렌더링 검증·밸런스 시뮬. 배치 빌드→플레이어 실행→FPS·화면·수치 검증→리포트·스크린샷 생성 | `game_build_verify.py`, `game_balance_sim.py` (헌장: `skills/마루_게임개발/SKILL.md`, 산출물: `output/qa/ashes-to-stars/`) |
| 별이 (Byeol) | Game Art QA — 매 실행 딥서치로 기준 확인 → 검수 → 크로마·반투명 수정. 4직업 재생성 없음 | `game_image_quality.py` (헌장: `skills/별이_이미지품질/SKILL.md`, 산출물: `output/qa/ashes-to-stars/image_quality/`) |


**⚡ 전원 재와 별 투입 (오너 지시 2026-08-18)** — 펫나 6인이 게임 개발 역할로 이동했다.
배정 단일 소스는 `_shared/fleet_assignment.json`(평문·git 추적)이고, `_shared/assignment.py`의
`assignment_guard()`가 **코드로 강제**한다 — 배정이 `ashes-to-stars`인 동안 펫나 도구는
`--once`든 `--daemon`이든 정시 잡이든 첫 줄에서 사유를 찍고 종료한다(문서 규칙이 아니라 가드).

| 사람 | 게임 역할 | 무엇을 보나 | 주기 |
|---|---|---|---|
| 봄이 | 정합성 | 기획서 ✅ 확정 중 코드에서 성립 안 하는 것 | 07:10 · 19:10 |
| 수리 | 구현 | 죽은 데이터·읽는 코드 0곳인 설정/에셋 | 07:40 · 19:40 |
| 백호 | 밸런스 | 수치가 §18 앵커에서 유도됐는지 코드와 대조 | 08:10 · 20:10 |
| 미오 | 연출 | 500체 화면에서 규칙이 눈에 읽히는지 | 08:40 · 20:40 |
| 테오 | 검증 | 네거티브 컨트롤 없는 통과 찾기 | 10:10 · 22:10 |
| 나무 | 우선순위 | ORDERS·백로그 순서 역전 (로컬 모델 1순위) | 20분 |

전원 **읽기 전용**(Read/Grep/Glob·plan 모드)이라 유니티 락을 잡지 않는다 — 개발 세션 빌드를
죽이지 않기 위한 원칙이며 되돌리지 마라(`game_agents.py` 머리말). 발견은 보고서
`output/qa/ashes-to-stars/agents/<역할>_<ts>.md` + 백로그(`owner_agent`에 사람 이름).
펫나로 되돌리려면 `fleet_assignment.json`의 `project`를 `petnna`로 고치면 된다 — 코드는 그대로다.

**펫나 자동 개발 루프**: 봄이(발견)·백호(DB 계약)·테오(회귀 테스트) → 수리(수정/구현) → 봄이 재검수 → 저위험 P2/P3만 자동 병합. 미오(디자인)·나무(기획)가 `output/qa/petnna/backlog.json`에 과제 적재 → 수리가 QA 이슈 없을 때 브랜치 구현(자동 병합 없음, 사람 검토). 봄이는 순찰 중 앱 자체 오류수집기(AppLogger→localStorage)도 흡수(global_error=P1). 전 에이전트 클로드 세션에 웹서치 허용(모르는 건 검색). 공용 헬퍼: `_shared/cc.py`(claude -p 헤드리스). **반려 피드백 환류(크리틱 루프, 2026-07-15)**: 예원 PR 리뷰의 품질 반려는 시도 한도(MAX_ATTEMPTS) 내라면 `보류`가 아니라 `대기`로 되돌리며 반려 사유를 `review_feedback`에 적재 — 수리가 재시도 프롬프트에 그 사유를 주입받아 같은 실수를 반복하지 않는다(하드 게이트 반려·한도 소진만 보류, 회귀 테스트 `tests/test_review_feedback_loop.py`).

**펫나 가드레일 (주식 모의거래 교훈 이식, 2026-07-08)**:
- **산출물 감사**: 예원이 매일(11:00) 함대 신선도 감사(`petnna_fleet_health.py`, launchd `com.ailab.sched.petnna_fleet_health`) — 데몬이 떠 있어도 산출물(보고서/루프/결과)이 30h(주간 에이전트 8일) 무갱신이면 죽은 잡 의심 경보. "프로세스 생존 ≠ 일하는 중". (2026-07-09 발견: 스크립트만 있고 정시 잡 미등록으로 한 번도 자동 실행된 적 없던 공백 — schedules.json 등록 완료.)
- **검토 적체 상한**: PR대기 브랜치 ≥5(`SURI_MAX_PENDING`)면 수리가 신규 백로그 착수 중단(QA 버그 수정은 계속) + 하루 1회 알림. 사람 검토가 병목일 때 무한 브랜치 생성 방지.
- **인프라 실패 ≠ 이슈 실패**: 클로드 CLI 부재/타임아웃/과부하로 실패한 사이클은 시도 미차감 — 크레딧·PATH 장애를 "3회 실패 보류"로 오판 금지.
- **단일 기계 운영**: 펫나 데몬 6종은 지정된 기계가 아니면 자동 종료 — 두 기계가 각자 master 병합하는 이중 가동 참사 방지. **2026-07-11 오너 지시로 맥이 운영 기계**(`projects/ai-team/_shared/fleet_machine_policy.json`의 `primary_platform: "darwin"` — git 추적 평문 파일이라 두 기계가 항상 같은 값을 봄, 과거 `PETNNA_AGENTS_ON_WINDOWS` 기계별 암호화 플래그 방식은 폐기 아닌 폴백으로만 유지). Windows에서 같은 데몬을 켜지 마라 — 기계를 바꾸려면 이 파일의 `primary_platform`만 수정(git pull 전파 필요).
- **컨트롤러 오폭 방지**: `--daemon` 에이전트의 stop/restart는 "스크립트명 --daemon"만 매칭 — 진행 중 수동 사이클(--once)·회의를 죽이지 않는다.
- **브랜치 위생**: 병합/해결된 이슈의 브랜치는 즉시 삭제, 수리 데몬 기동 시 잔재 워크트리 정리 + 독트린(게이트 구성) 1줄 로그.
- **헤드리스 클로드에 프롬프트는 stdin으로(2026-07-10 사고)** — Windows의 `claude.CMD`(npm 셔임)는 argv에 담긴 개행에서 인자를 잘라 **첫 줄만** CLI에 넘긴다. 수리는 과제 본문을 통째로 잃은 채 지시문만 보냈고 클로드는 "과제가 안 보인다"고 되물었다. `rc=0`이라 엔진은 성공으로 기록 → diff가 비어서야 실패 인지. `_shared/cc.run_claude`를 쓰는 나무·미오·백호·테오·예원도 응답 파싱 실패 후 **로컬 모델로 조용히 폴백**해 함대 전체가 강등 운영됐다. 교훈: **"응답 성공 ≠ 지시 전달 성공"**. 새 CLI를 subprocess로 부를 땐 여러 줄 인자를 argv에 싣지 말고 `input=`으로, `encoding="utf-8"` 명시(Windows 기본 cp949).
- **무인 세션에 '승인받고 고쳐라'는 데드락(2026-07-10 사고)** — 헤드리스 클로드도 워크트리의 `CLAUDE.md`를 읽는다. "계획 → 오너 승인 → 수정" 절차를 그대로 따라 **승인자가 없는데 계획만 쓰고 종료**(과제가 추상적일수록 심함). 자동 실행 프롬프트엔 "비대화형이니 승인 묻지 말고 즉시 편집, 계획만 쓰면 실패 처리"를 명시하라.
- **게이트 diff는 `master`가 아니라 분기점(merge-base) 기준(2026-07-10)** — `git diff master`는 사이클 도중 master가 앞서면 남의 커밋을 뒤집어 브랜치 변경처럼 보여준다. 테오가 E2E를 master에 자동 커밋하므로 상시 충돌 → 수리가 멀쩡한 자기 패치를 "petnna 밖 파일 수정"으로 자폭 거부. `merge-base master HEAD`와 비교할 것.
- **평문 `.env`는 무시된다 — `.env.encrypted`가 우선(2026-07-10 함정)** — `load_env()`는 `.env.encrypted`가 있으면 그것만 읽고 즉시 반환한다. 평문 `.env`를 고쳐도 **아무 효과 없다**. 설정 변경은 반드시 `env.py encrypt`로 재암호화까지. 적용 확인은 `load_env()` 후 `os.getenv()`로.
- **운영 인터프리터는 Python 3.13(2026-07-10)** — PATH의 `python`은 hermes venv(3.11)라 **playwright가 없어** 봄이·미오가 즉사한다. `agent_controller`는 자식을 `sys.executable`로 띄우므로 컨트롤러를 켜는 파이썬이 함대 전체의 파이썬이 된다. `C:\Users\User\AppData\Local\Programs\Python\Python313\python.exe`로 실행할 것.

**긴급 회의(큰 이슈 = 전 에이전트 소집)**: `예원_CEO/tools/petnna_council.py` — 트리거: 봄이 신규 P0/P1, 수리 3회 실패 보류, 백호 신규 P1 계약 위반 (각 에이전트가 비차단 자동 소집), 수동 `--topic`. 6인이 각자 헌장+실데이터 기반 독립 의견(plan 모드) → 의장 예원 종합 결정 → 액션아이템 백로그 적재([승인필요]/owner=사람은 보류 상태로 수리가 안 집음) → 회의록 `output/qa/petnna/council/` + 텔레그램. 동일 안건 24h 중복 소집 방지.


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

`.env.encrypted`는 `getpass.getuser()@platform.node()`로 파생한 **기계+계정 전용** 키라
git으로 공유할 수 없다(2026-07-11 발견: Windows가 마지막으로 재암호화한 뒤로 맥에서
복호화가 계속 조용히 실패해 이 맥이 며칠간 평문 `.env`로만 강등 운영됐다 — `load_env()`가
이제 실패 시 stderr 경보를 남기도록 수정됨). 그래서 `.env.encrypted`는 git 추적에서
제거하고 `.gitignore`에 추가했다 — **로컬 전용 파일**로만 쓸 것, 커밋하지 마라.

로컬에서 암호화(선택, 안 해도 평문 `.env`로 정상 동작):
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
- `NOTION_API_KEY`, `NOTION_DATABASE_ID`
- `SUPABASE_URL`, `SUPABASE_ANON_KEY`

---


## 📱 Telegram Bot (영숙)

### Natural Language Commands

The bot uses Gemini Function Calling to map natural language to tools:

- **"현황 보고해줘" / "다들 뭐해?"** → `get_agent_status()` (실존 에이전트 현황)
- **"일정 알려줘" / "캘린더 확인해봐"** → `list_calendar()`
- **"에이전트 작업 요청"** → `dispatch()` → CEO orchestration

### Bot Architecture

`telegram_receiver.py` consolidates:
- Claude tool use function calling integration
- Calendar manager (`calendar_manager.py`)
- Reports manager (`reports_manager.py`)

(구 유튜브 업로드 파이프라인 `posting_scheduler.py`·`upload_approval_flow.py`·
`upload_manager.py`·`youtube_recommender.py`는 2026-08-04 삭제 — 루나/아린 세대 잔재)

Logs: `projects/ai-team/skills/영숙_비서/tools/telegram_receiver.log`

---

## 🌐 Petnna Project

### QA & Auto-Improvement (봄이·수리)

- **이웃 활동** (`skills/예원_CEO/tools/petnna_social_agent.py`, schedules.json `petnna_social` 하루 3회 10:30·15:30·20:30) — 앱의 AI 이웃 페르소나(social.js `AI_AGENT_FRIENDS`)가 실제로 활동: 반려동물 게시글 작성(Claude 생성, 실패 시 템플릿) + 실 유저 글에 댓글·좋아요. **비스팸**(회당 1글, 최근 3h 2글 상한), **정직성**(봇 아바타·이모지 이름으로 AI 이웃임 노출), 쓰기는 `posts`만(스키마 변경 없음, anon insert). 오너 지시(2026-07-11) — 정체된 피드 활성화.
- **봄이** (`skills/봄이_QA/tools/petnna_qa_patrol.py`) — 상시 순찰: 콘솔/JS 오류·404·깨진 이미지·접근성·가로스크롤·SEO + **로그인 후 클릭 인터랙션**(더미 계정 우회로 전 탭 전환·주요 모달 열기가 오류/빈 화면 없이 되는지, `interactive_checks`) 점검, P0/P1 즉시 텔레그램 알림, 보고서 `output/qa/petnna/`. 인터랙션 점검은 **비파괴**(탭 전환·모달 open/close만, 저장/삭제/전송 등 쓰기는 안 함 — 앱이 실 Supabase 연결이라 오염 방지).
- **수리** (`skills/수리_개발자/tools/petnna_dev_engine.py`) — 봄이 결과를 읽어 저위험 P2/P3를 격리 브랜치에서 자동 수정·재검수 후 게이트 통과 시만 master 병합. master 직접 수정 없음, 금지 경로(supabase·api·결제 등) 접촉 시 병합 거부.

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
4. **Check for mutex locks** — Use `_shared/process.py`(`ProcessLock`)로 daemon 스크립트 중복 방지

### When Adding New Agents

1. Create folder: `projects/ai-team/skills/<에이전트명>/`
2. Add tools to: `projects/ai-team/skills/<에이전트명>/tools/*.py`
3. Register in: `src/agents.ts` (AGENTS) + `_shared/notify.py` (CONTINUOUS_DAEMONS/SCHEDULED_SERVICES) + `agent_controller.py` (실행 대상)
4. Update: `AGENTS.md`

### Process Management

- **Daemons use `_shared/process.py`'s `ProcessLock`** to prevent duplicates(맥은 `fcntl.flock`, Windows는 Named Mutex — 이미 크로스플랫폼 구현)
- **Cleanup zombies**: `python projects/ai-team/scripts/cleanup_duplicate_processes.py`

### Logging

- Agent logs: `output/bot_logs/`
- System logs: `.logs/`

---

## 📝 Coding Conventions

- **Korean folder names** are normal (에이전트명) — all OS paths handle UTF-8
- **Match existing patterns** — Don't refactor agent import logic
- **No premature abstractions** — Agents prefer explicit over DRY
- **Surgical changes only** — Don't "improve" adjacent code
- **Test on macOS** — 2026-07-11 오너 지시로 맥이 메인 운영 기계로 확정(`_shared/fleet_machine_policy.json`의 `primary_platform: "darwin"`). Windows 전용으로만 동작하고 맥에서 검증 안 되는 기능은 추가하지 마라.

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

## 💾 토큰·세션 운영 수칙 (2026-08-20 오너 지시)

- **세션 인수인계**: 긴 작업을 중단하거나 컨텍스트가 길어지면 `docs/SESSION_HANDOFF.md`에 현재 상태·완료 항목·다음 단계·핵심 파일 경로를 자족적으로 기록한다. 새 세션 시작 시 SessionStart 훅이 이 파일을 자동 주입하므로, **이어받은 세션은 완료한 항목을 파일에서 지우고, 전부 끝나면 파일을 비운다**(비워야 다음 세션에 주입 안 됨).
- **넓은 탐색은 서브에이전트로**: 전 저장소 검색·다수 파일 훑기는 메인 컨텍스트에서 직접 하지 말고 Explore 등 서브에이전트에 위임해 결론만 받는다.
- **작업 단위가 바뀌면 새 세션 권장**: 누적 대화가 매 요청 재전송되므로 긴 세션은 요청당 비용이 커진다.
- **사고 이력은 원장 파일에만**: CLAUDE.md에 상세를 다시 쌓지 마라(아래 섹션 참조).

---

## 🔧 하네스 가드레일

> 반복된 실패를 규칙으로 박아 에이전트가 같은 실수를 안 하게 한다.
> **상세 원장(사고별 전말·수리 내역)은 [`docs/HARNESS_GUARDRAILS_LEDGER.md`](docs/HARNESS_GUARDRAILS_LEDGER.md)** — 새 사고는 그 파일에 추가하라(이 파일에 다시 쌓지 마라, 토큰 폭증의 원인이었다).
> 핵심 절차 요약은 [`DIRECTIVES.md`](DIRECTIVES.md). 진단이 막히거나 과거 유사 사고가 의심되면 원장을 열어 검색할 것.

**상시 적용 핵심 규칙 (원장 전체의 최소 요약)**:
- 응답 성공 ≠ 지시 전달 성공, Popen 성공 ≠ 스크립트 실행 성공 — 새 검증 절차는 네거티브 컨트롤로 빨간불부터 확인.
- 원인 단정 전 판별 테스트(정상/가짜/변조 입력 나란히 호출). 에러 메시지·형태(접두사·길이)로 추측 금지 — 실제로 호출해봐라.
- 헤드리스 클로드: 프롬프트는 stdin, 시크릿 env 스크럽(`_shared/cc.scrub_secrets`), "승인 묻지 말고 즉시 편집" 명시.
- 같은 로직이 여러 곳에 살면 재발한다 — 안전장치·판정 사유는 공용 함수로 만들고 호출부는 재사용만.
- 상태 전환·보류에는 사유를 코드가 강제로 남긴다. 같은 상태로 보내는 모든 경로를 grep으로 확인.
- "커밋했다 ≠ 배포됐다", "저장됐다 ≠ 반영됐다" — 배포본 버전 대조·비동기 동기화 루틴부터 의심.
- 동시 세션 저장소: `git add`+`commit`은 한 호흡. 자동 병합 실패 시 `merge --abort` 즉시.
- 게이트·독트린 무단 강화 금지, 오너 env를 코드 하한으로 덮지 마라. 오너 결정은 `resolution`에 마커로.
- 서브에이전트 프롬프트에 외부 계정 부작용 금지(`vercel dev` 등) 상시 문구.
- 경보는 예원이 못 고친 것만 — 고칠 수 있는 건 조용히 고친다.

---

## 📚 Documentation

- **Agent details**: `AGENTS.md`
- **AI model strategy**: `projects/ai-team/docs/AI_MODEL_STRATEGY.md`
- **Security rules**: `docs/setup/ENV_SECURITY_RULES.md`
- **Telegram bot**: `TELEGRAM_BOT_README.md`
- **Petnna setup**: `projects/petnna/README.md`
- **DESIGN.md 참고 자료(2026-07-06)**: `references/awesome-design-md/design-md/<사이트>/DESIGN.md` — 73개 실사이트 디자인 시스템(색상·타이포·컴포넌트) 추출본. 적용 대상 미정(petnna/bboggl/대시보드 후보) — 사용 시 해당 프로젝트 루트에 원하는 `DESIGN.md`를 복사해 붙여넣고 AI에게 "이 디자인처럼 만들어줘" 요청.
