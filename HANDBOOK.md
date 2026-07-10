# 운영 매뉴얼 (Company Handbook)

> **대상 독자: 사람(오너).** AI 도구용 지침은 [CLAUDE.md](CLAUDE.md)(Claude Code)와 [AGENTS.md](AGENTS.md)(Codex)에 있다.
> 이 문서는 구조를 설명하지 않는다 — 링크로 위임한다. 여기엔 **다른 어디에도 없는 것**만 담는다:
> 회사 헌장, 사고 런북, 문서 지도.
>
> 마지막 검증: 2026-07-10 (9개 데몬 실행 확인, Windows 운영기)

---

## 1. 문서 지도 — 여기가 유일한 진입점

저장소에 문서가 **97개**다(`references/`·`connect-ai*/` 제외). 전부를 한 파일로 합칠 수는 없다 —
하네스(`check_all.py`)가 특정 경로의 존재를 계약으로 강제하고, 일부는 웹앱이 서빙한다(§1-4).
그래서 **물리적 병합 대신 이 색인 하나**로 모은다. 길을 잃으면 여기로 돌아온다.

> 개수 재확인: `git -c core.quotepath=false ls-files '*.md'`
> (`core.quotepath=false` 없이 세면 한글 경로가 따옴표로 감싸여 `^` 앵커가 빗나가 **과소 집계**된다)

### 1-1. 상황별 — 대부분 이 표에서 끝난다

| 상황 | 읽을 곳 |
|---|---|
| 사고가 났다, 뭔가 안 돈다 | **§5 런북** |
| 회사 원칙이 뭐였지 | **§2 헌장** |
| 봇을 켜고 끄고 싶다 | **§4 일상 운영** |
| 누가 뭘 하는지 | **§3 조직도** |
| 뭘 건드리면 안 되나 | **§6 금기** |
| 전체 구조가 궁금하다 | [docs/AI_LAB_SYSTEM_ARCHITECTURE.md](docs/AI_LAB_SYSTEM_ARCHITECTURE.md) |
| 특정 봇의 헌장 | `projects/ai-team/skills/<봇>/SKILL.md` |
| 코드를 고치려 한다 | [CLAUDE.md](CLAUDE.md) 「하네스 가드레일」 |
| 시크릿을 다뤄야 한다 | [docs/setup/ENV_SECURITY_RULES.md](docs/setup/ENV_SECURITY_RULES.md) |
| LLM 모델 전략 | [projects/ai-team/docs/AI_MODEL_STRATEGY.md](projects/ai-team/docs/AI_MODEL_STRATEGY.md) |

### 1-2. 권위 등급

문서가 서로 모순될 때 **위쪽이 이긴다.**

| 등급 | 문서 | 뜻 |
|---|---|---|
| **A. 헌장** | 이 문서, `CLAUDE.md`, 각 봇 `SKILL.md` | 현재 유효. 어기면 사고. |
| **B. 정식 참조** | `docs/*.md`, `docs/setup/*`, `projects/ai-team/docs/*` | 현재 유효하나 낡을 수 있음. 코드와 다르면 **코드가 옳다**. |
| **C. 기록** | `reports/*`, `docs/superpowers/*`, `*/CHANGELOG.md` | 그때의 사실. 지금의 지침이 아니다. |
| **D. 보관** | `docs/archive/*` | **운영 지침으로 쓰지 마라.** |

### 1-3. 전체 색인 (97개)

| 영역 | 개수 |
|---|---|
| `projects/ai-team/` | 38 |
| `docs/` | 27 |
| `reports/` | 14 |
| `projects/petnna/` | 10 |
| 루트 | 7 |
| `projects/bboggl/` | 1 |

**루트 7개 — `DESIGN.md` 뺀 6개는 하네스가 존재를 강제**
| 파일 | 대상 독자 | 상태 |
|---|---|---|
| `HANDBOOK.md` | **사람(오너)** | 이 문서. 진입점 |
| `CLAUDE.md` | Claude Code | A등급. 가드레일 사고 기록 |
| `AGENTS.md` | Codex | B등급 |
| `PROJECT_OVERVIEW.md` | 사람 | 구조 개요 |
| `README.md` | 외부/GitHub | 경량 진입점 |
| `SKILL.md` | AI | 스킬 시스템 포인터 |
| `DESIGN.md` | AI/디자인 | 디자인 시스템 |

**docs/ — 27개**
- 최상위 6: `AI_LAB_SYSTEM_ARCHITECTURE.md`(아키텍처) · `REPOSITORY_CLASSIFICATION.md`(배치 규칙, 이 색인과 다름) ·
  `TELEGRAM_BOT_README.md` · `AGENT_GROWTH_DOCTRINE.md` · `AGENT_ADVANCEMENT_DESIGN.md` · `CLEANUP_SUMMARY_2026-06-27.md`(C등급)
- `setup/` 8: `ENV_SECURITY_RULES.md` ⭐ · `ENCRYPTED_SECRETS_README.md` · `ENV_STATUS.md` ·
  `AI_TEAM_AUTOMATION_README.md` · `DAILY_AUTOMATION_SETUP.md` · `NOTION_SETUP.md` · `QUICK_START_NOTION.md` · `SETUP_INSTAGRAM.md`
- `archive/` 6 (D등급) · `superpowers/` 6 (C등급) · `plans/` 1 — **읽되 따르지 마라**

**projects/ai-team/ — 38개**
- `skills/` 18: 봇 헌장 8(`예원_CEO`·`영숙_비서`·`봄이_QA`·`수리_개발자`·`테오_테스트`·`백호_백엔드`·`미오_디자인`·`나무_기획`/`SKILL.md`) 🔒
  + `공용스킬/` 7(공통 지식·토큰 최적화·멀티에이전트 토론·리서치모드 등) + `README.md` + 영숙 tools 2
- `docs/` 6: `AI_MODEL_STRATEGY.md` ⭐ · `AI_QUALITY_OPTIMIZATION.md` · `AGENT_COMPATIBILITY.md` ·
  `GEMINI_TOKEN_OPTIMIZATION.md` · `AGENT_PIPELINE_REVIEW.md` · `progress.md`
- 최상위 3: `README.md` · `CLAUDE.md`(하위 스코프) · `CHANGELOG.md`
- `plugins/` 4 · `assets/` 4(brain-seeds 템플릿, 라이브러리 자산) · `harness/` 1 · `scripts/` 1 · `_shared/` 1

**projects/petnna/ — 10개**
`README.md` · `CHANGELOG.md` · `DEVELOPMENT_REPORT.md` · `SETUP_SUPABASE.md` ·
`PRIVACY_POLICY.md` 🔒 · `TERMS_OF_SERVICE.md` 🔒 (웹앱이 `/`에서 서빙 — 이동 금지) · `docs/` 4종

**projects/bboggl/README.md** — 1개

**reports/ — 14개, 전부 C등급 기록**
`README.md` · `channel_registration_status.md` · `research/` 2 · `history/` 5 · `meetings/` 4 · `inspection/` 1

### 1-4. 옮길 수 없는 것과 이유

| 대상 | 잠긴 이유 | 근거 (함수 기준 — 라인은 밀린다) |
|---|---|---|
| 루트 **6개** `.md`<br>`AGENTS`·`HANDBOOK`·`PROJECT_OVERVIEW`·`README`·`CLAUDE`·`SKILL` | 하네스가 존재 검사 → 없으면 FAIL | `check_all.py:233` `check_classification_layout()` |
| `docs/REPOSITORY_CLASSIFICATION.md`<br>`docs/TELEGRAM_BOT_README.md` | 같은 필수 목록 | 〃 |
| `ai-team/README.md`·`scripts/README.md`·`skills/README.md` | 같은 필수 목록 | 〃 |
| 루트 파일 전반 (`DESIGN.md` 포함) | 화이트리스트 밖이면 WARN | `check_all.py:358` `check_root_layout()` |
| 각 봇 `SKILL.md` 8개 | 하네스가 봇별 존재 검사 | `check_all.py:292` |
| petnna 법무문서 2개 | 앱이 `/PRIVACY_POLICY.md`로 링크 | `js/templates/settings.js:339` |

`DESIGN.md`는 **필수가 아니라 허용**이다 — 지워도 FAIL은 안 나고, 옮기면 WARN만 난다.

> 라인 번호는 코드가 바뀌면 밀린다. **함수명으로 찾아라.** 이 표의 번호는 2026-07-10 실측값이다.

### 1-5. 알려진 부채

- `AGENTS.md` — "3 agents"로 낡음 (실제 9개 데몬)
- `README.md` · `PROJECT_OVERVIEW.md` · `docs/AI_LAB_SYSTEM_ARCHITECTURE.md` — 구조 설명 3중 중복
- `reports/history/kevin_*.md` · `kodari_ollama_log.md` — 삭제된 에이전트의 로그 (C등급으로 보존)
- `projects/ai-team/CLAUDE.md` — 루트 `CLAUDE.md`와 스코프 경계가 불분명

---

## 2. 회사 헌장 — 절대 원칙

> 어겼을 때 되돌리기 가장 비싼 순서로 배열했다.

### 2-1. 거짓말 금지 (최상위)
검증하지 않은 것을 "됐다"고 말하지 않는다. 모든 보고는 증거 기반이다.
`rc=0`은 성공의 증거가 아니다 — **"응답 성공 ≠ 지시 전달 성공"** (2026-07-10 수리 사고).
"프로세스 생존 ≠ 일하는 중" — PID가 살아 있어도 산출물이 안 나오면 죽은 것이다.

### 2-2. 승인 게이트를 코드로 덮지 않는다
오너가 완화한 설정(`.env` 값)을 코드 하한으로 무력화하는 것은 **절대 금지**.
분석 지시는 게이트 도입 승인이 아니다. 새 발견이 기존 원칙과 충돌하면
게이트로 넣지 말고 **충돌을 명시하고 오너에게 먼저 묻는다**.

### 2-3. 유료 API 금지
LLM은 **구독 CLI(`claude -p`, `codex exec`) + Gemini + 로컬(Ollama)** 만 쓴다.
체인: 로컬 → 구독 클로드(Max) → 구독 GPT(Plus) → Gemini.
점검할 땐 `_shared.llm` 임포트만 보지 말고 `api.anthropic` / `api.openai` **직접 HTTP 호출까지 grep** 한다.

### 2-4. 하드코딩 금지
데이터는 동적으로 — API·LLM으로 가져온다. 표에 박아 넣지 않는다.

### 2-5. 자율 수정의 경계
- **묻지 말고 고쳐라**: 운영 결함(데몬·게이트·LLM·설정), 되돌리기 쉬운 코드 수정
- **반드시 물어라**: 실거래, 시크릿, 파괴적 작업, 승인 게이트 변경

### 2-6. 알림 규율
정보성 알림은 텔레그램 금지. **행동의 결과**만 보낸다. 단 P0/P1급 위기 경보는 예외.

### 2-7. 단일 기계 운영
펫나 데몬 6종은 **Windows(이 기계)에서만** 돈다. 맥에서 같은 데몬을 켜면
두 기계가 각자 master에 병합하는 참사가 난다.

---

## 3. 조직도

**사람**: 오너 1인 (최종 승인권자)

**봇 9종** — 전원 이 Windows 기계에서 상시 가동:

| 봇 | 역할 | 자율성 |
|---|---|---|
| **예원** | CEO — 오케스트레이션·워치독·긴급회의 의장 | 자동 재시작 권한 |
| **영숙** | 비서 — 텔레그램 게이트웨이 | 오너와 대화 |
| **영숙스케줄** | 정시 잡 실행자 (Windows 유일 실행자) | — |
| **봄이** | QA — 펫나 상시 순찰, 함대 신선도 감사 | P0/P1 즉시 경보 |
| **수리** | 개발 — QA 결과 자동 수정 | **저위험 P2/P3만 자동 병합** |
| **테오** | 테스트 — E2E 자동 작성 (2회 연속 통과 시 채택) | master 자동 커밋 |
| **백호** | 백엔드 — Supabase 계약 감사 | **읽기 전용** |
| **미오** | 디자인 — 주 1회(월) UX 리뷰 | 백로그 적재만 |
| **나무** | 기획 — 주 1회(화) 트렌드 조사 | 백로그 적재만 |

**자동 개발 루프**:
발견(봄이·백호·테오) → 수정(수리) → 재검수(봄이) → 저위험만 자동 병합.
미오·나무는 `output/qa/petnna/backlog.json`에 과제를 쌓고, 수리가 QA 이슈 없을 때 구현한다(**자동 병합 없음 — 사람 검토**).

**브레이크**: PR대기 브랜치 ≥5(`SURI_MAX_PENDING`)면 수리가 신규 착수를 멈춘다. 사람 검토가 병목일 때 무한 브랜치 생성을 막는 장치다.

---

## 4. 일상 운영

### 4-0. 운영 인터프리터 (가장 흔한 함정)
PATH의 `python`은 hermes venv(3.11)라 **playwright가 없어** 봄이·미오가 즉사한다.
컨트롤러를 켜는 파이썬이 함대 전체의 파이썬이 되므로, 반드시:

```
C:\Users\User\AppData\Local\Programs\Python\Python313\python.exe
```

### 4-1. 상태 확인
```bash
python projects/ai-team/skills/영숙_비서/tools/agent_controller.py status
```
텔레그램에서는 **"현황 보고해줘"** / **"다들 뭐해?"**

### 4-2. 봇 제어
```bash
# <에이전트명> <시작|종료|재시작|상태>
python .../agent_controller.py 봄이 재시작
python .../agent_controller.py 수리 상태

# 전체
python .../agent_controller.py 봇다꺼
python .../agent_controller.py 봇다켜
```
에이전트명: `영숙` `영숙스케줄` `예원` `봄이` `수리` `테오` `백호` `미오` `나무`
(영문 별칭도 됨: `bomi` `suri` `teo` `baekho` `mio` `namu` …)

### 4-3. 코드 배포
`git pull`만 하면 된다 — 워치독 예원이 HEAD 변화를 감지해 변경 폴더의 데몬을 자동 교체한다
(`harness_monitor.restart_on_code_update`, `_shared` 변경 시 전 데몬 + 자기 자신).
**플랫폼 분기 없음 — 맥·Windows 모두 동작한다**(2026-07-10 실측: 커밋마다 예원 PID 자동 교체됨).

전제는 **예원이 살아 있을 것**. 죽어 있으면 아무것도 갱신되지 않는다.
"고쳤는데 왜 그대로냐" = ①예원 생존 ②데몬 재기동 여부 순으로 확인.
함대가 통째로 꺼진 상태에서 pull로 되살리는 건 §4-5(post-merge 훅)가 담당한다.

### 4-4. 설정 변경 (⚠️ 함정)
`load_env()`는 `.env.encrypted`가 있으면 **그것만 읽고 즉시 반환**한다.
평문 `.env`를 고쳐도 **아무 효과 없다.**

```bash
python projects/ai-team/_shared/env.py decrypt .env.encrypted .env.decrypted
# 편집 후
python projects/ai-team/_shared/env.py encrypt .env .env.encrypted
```
적용 확인은 `load_env()` 후 `os.getenv()`로.

### 4-5. 기계 전환 — git pull로 자동 기동

새 기계에서 **딱 한 번**만 실행하면, 그 뒤로는 `git pull`만으로 함대가 뜬다.
(`core.hooksPath`는 `.git/config`에 있어 커밋으로 전파되지 않는다 — 그래서 한 번은 필요하다.)

```bash
# 맥에서 (playwright 있는 인터프리터로!)
git pull
python3 projects/ai-team/scripts/fleet_bootstrap.py --setup
git add .env.encrypted && git commit -m "chore: 운영기를 맥으로 지정" && git push
```

`--setup` = post-merge 훅 활성화 + 이 기계를 운영기로 지정 + `BOTS_OFF` 해제 + 즉시 기동.

**훅은 기동보다 거부가 본업이다.** 세 관문을 다 통과해야만 뜬다:

| 관문 | 막는 사고 |
|---|---|
| `BOTS_OFF` 플래그 없을 것 | 오너가 내려둔 기계를 pull이 멋대로 깨우는 것 |
| 이 기계가 지정 운영기일 것 | **두 기계가 각자 master에 병합하는 이중 가동** |
| playwright 있는 인터프리터일 것 | 봄이·미오 기동 즉시 사망 |

운영기 지정자는 `TELEGRAM_POLL_HOST`를 **재사용**한다 — 개념을 하나로 유지해야
텔레그램 폴링 소유자와 함대 운영기가 어긋나지 않는다.
`output/cache/*`는 gitignore라 `BOTS_OFF`·인터프리터 핀은 기계마다 독립이다.

주의: post-merge 훅은 **실제 병합이 일어날 때만** 실행된다.
`Already up to date`면 안 불리고, `git pull --rebase`에서도 안 불린다.

### 4-6. 외부 하트비트 (함대 밖 감시선)
함대와 **같이 죽지 않는** 유일한 장치. Windows 작업 스케줄러가 5분마다 새 프로세스로 띄운다.
`down`/`misconfig`를 감지하면 텔레그램(30분 쿨다운), 워치독 예원이 죽어 있으면 **직접 되살린다**
(예원은 스스로를 재시작하지 못한다 — `harness_monitor.check_and_restart_bots`가 `yewon`을 제외).
```bash
python projects/ai-team/skills/예원_CEO/tools/fleet_heartbeat.py            # 1회 점검
python projects/ai-team/skills/예원_CEO/tools/fleet_heartbeat.py --status   # 마지막 결과
python projects/ai-team/skills/예원_CEO/tools/fleet_heartbeat.py --install  # 5분 주기 등록
```
등록 확인: `Get-ScheduledTask -TaskName AiLabFleetHeartbeat` → `Ready` / `PT5M`

### 4-7. 시스템 점검 / 긴급회의
```bash
python projects/ai-team/harness/check_all.py
python projects/ai-team/skills/예원_CEO/tools/petnna_council.py --topic "안건"
```
긴급회의 자동 트리거: 봄이 신규 P0/P1, 수리 3회 실패 보류, 백호 신규 P1 계약 위반.
(동일 안건 24h 중복 소집 방지)

---

## 5. 사고 런북 — 증상으로 찾기

> 각 항목은 실제로 일어난 사고다. 진범은 대개 의심한 곳에 없었다.

### 「정기 보고가 안 온다」
1. **어느 기계가 그 시간에 켜져 있었나** 확인
2. Windows면 `영숙스케줄`(정시 잡 실행자) 생존 확인 — 이게 죽으면 잡 전체가 조용히 정지
3. macOS면 launchd 적재 확인

### 「함대가 통째로 죽었는데 하네스는 조용하다」 (2026-07-10, 수정됨)
**진범: `.env.encrypted`에서 `PETNNA_AGENTS_ON_WINDOWS`가 사라짐.**
플래그가 없으면 펫나 6종은 기동 즉시 자진 종료하는데, 옛 코드는 이를 `down`이 아닌 **`disabled`**로
보고했다. `disabled`는 "이 기계에선 안 돌리는 게 정상"이라 **워치독이 재시작을 시도조차 하지 않는다.**
안전장치가 침묵장치로 뒤집힌다.

**지금은 상태가 분리돼 있다** — 플래그 **부재**는 `misconfig`(🟠, 하네스 FAIL + 하트비트 경보),
명시적 `false`만 `disabled`(⚪, 경보 제외). 상태가 전부 `misconfig`면 아래로 간다.
```bash
# 키 손실 확인 (값은 보지 말고 개수만)
git show HEAD:.env.encrypted > /tmp/h.enc
python projects/ai-team/_shared/env.py decrypt /tmp/h.enc /tmp/h.txt
grep -c "PETNNA_AGENTS_ON_WINDOWS" /tmp/h.txt   # 커밋본엔 1
# 복구: 다른 의도적 변경이 없는지 먼저 키 diff → 없으면
git checkout HEAD -- .env.encrypted
```
**교훈**: `.env.encrypted`가 `M`으로 떠 있으면 그게 "오너의 편집"이 아니라 **회귀**일 수 있다.
되돌리기 전에 키 이름·값 해시를 비교해 다른 변경이 섞였는지 반드시 확인하라.

### 「봇다켜를 했는데 영숙·예원만 안 뜬다」 (2026-07-10, 수정됨)
`_KEEP_ON_SHUTDOWN = {"영숙","예원"}`을 `start_all_bots`가 **기동 제외 목록으로 오용**했다.
"정지 제외"와 "기동 제외"는 다른 정책이다. 둘이 다른 이유로 죽으면 `봇다켜`가 영원히 못 살렸고,
출력의 "워치독이 자동 복구합니다"는 **예원이 그 워치독이라 거짓**이었다.
→ [agent_controller.py:265](projects/ai-team/skills/영숙_비서/tools/agent_controller.py:265) `start_all_bots()` 수정 완료.
**복구 책임자를 복구 대상에서 빼지 마라.**

### 「봇은 떠 있는데 결과물이 없다」
산출물 신선도를 본다. 36h(주간 에이전트는 8일) 무갱신이면 **죽은 데몬**이다.
`output/qa/petnna/` 하위 보고서 타임스탬프 확인. 봄이가 매일 자동 감사한다.

### 「LLM이 이상한 답을 한다 / 빈 응답」
1. **크레딧부터 확인** — 클라우드 429면 전 에이전트가 로컬로 조용히 강등된다
2. Ollama 생존 + **매니페스트** 확인 (`ollama show` — `list`엔 보여도 `show`가 404일 수 있음)
3. 로그에 `⚠️/❌ [ClaudeCode]` 줄이 **아예 없으면** = CLI 미발견(호출 전 탈락)
   있으면 = 호출 후 실패

### 「수리가 과제를 못 알아듣는다」
헤드리스 CLI에 **여러 줄 프롬프트를 argv로 넘겼는지** 확인.
Windows `claude.CMD`는 개행에서 인자를 잘라 **첫 줄만** 전달한다.
→ `input=`(stdin)으로 넘기고 `encoding="utf-8"` 명시(기본 cp949).

### 「봇이 계획만 쓰고 끝난다」
헤드리스 클로드도 워크트리의 `CLAUDE.md`를 읽는다 → "승인받고 고쳐라"를 따라
**승인자가 없는데 대기**한다. 자동 실행 프롬프트에
"비대화형이니 승인 묻지 말고 즉시 편집, 계획만 쓰면 실패 처리"를 명시하라.

### 「수리가 멀쩡한 자기 패치를 거부한다」
게이트 diff 기준이 `master`면, 사이클 도중 테오가 master에 커밋해 남의 변경이 섞여 보인다.
→ `git merge-base master HEAD` 기준으로 비교해야 한다.

### 「재부팅 후 데몬 전멸」(macOS)
워치독(`com.ailab.yewon_monitor`)의 launchd 적재부터 확인.

### 「콘솔 창이 계속 깜빡인다」(Windows)
데몬의 자식 subprocess에 `creationflags=CREATE_NO_WINDOW`가 빠졌다.

---

## 6. 절대 건드리지 마라

1. `_shared/`를 **전 에이전트 테스트 없이** 수정 금지
2. 평문 `.env` 커밋 금지
3. master **force-push** 금지
4. 데몬에서 **mutex 락 제거** 금지 (좀비 프로세스)
5. `load_env()` **생략** 금지
6. 동시 세션 저장소다 — `git add`와 `git commit`을 **한 호흡으로**.
   스테이징을 방치하면 다른 세션의 자동 커밋이 쓸어 담아간다.

---

## 7. 에스컬레이션 정책 — 언제 봇이 오너를 부르는가

### 7-1. 판단 축은 심각도가 아니라 **자가복구 가능성**이다

"얼마나 심각한가"로 나누면 실패한다. 심각해도 워치독이 5분 내 되살리는 일은
오너가 **할 수 있는 게 없다** — 순수 소음이다. 반대로 `misconfig`는 재시작으로
낫지 않아 **오직 사람만** 고칠 수 있다. 그래서 기준은 이렇다:

> **자동으로 낫는가?** 낫는다 → 조용히. 안 낫는다 → 즉시.
> 낫긴 하는데 사람이 결정을 내려야 쌓인 게 풀리는가? → 하루 1회.

경보 피로는 실재하는 위험이다. 진짜 P0을 흘려보내게 만든다.
**텔레그램은 행동이 필요할 때만 울린다.**

### 7-2. 정책 표

| 상황 | 즉시 깨움 | 하루 1회 요약 | 조용히 기록만 | 이유 |
|---|:---:|:---:|:---:|---|
| 펫나 사이트 다운 (P0) | ✅ | | | 자동복구 불가, 사용자 영향 |
| 게이트 플래그 유실 (`misconfig`) | ✅ | | | 재시작으로 안 나음. 사람만 고침 |
| 워치독(예원) 다운 | ✅ | | | 복구 책임자가 죽음 → 나머지도 못 살아남 |
| 함대 동시 다운 (≥3종) | ✅ | | | 단일 봇 사고가 아님. 공통 원인 의심 |
| 산출물 36h 무갱신 | ✅ | | | 프로세스는 살았는데 일을 안 함 = 침묵 실패 |
| 봇 1개 다운 → **재시작 실패** | ✅ | | | 자동복구가 졌다 |
| 봇 1개 다운 → **재시작 성공** | | | ✅ | 오너가 할 게 없다 |
| PR대기 브랜치 ≥5 (`SURI_MAX_PENDING`) | | ✅ | | 사람 검토가 병목. 하루 단위면 충분 |
| 자동 병합된 커밋 | | ✅ | | 이미 5중 게이트를 통과함 |
| 백로그 적재 (미오·나무) | | | ✅ | 제안일 뿐, 아직 아무 일도 안 일어남 |

### 7-3. 코드 반영 지점

| 정책 | 강제하는 곳 |
|---|---|
| 즉시 4종(misconfig·예원·≥3종·재시작실패) | `fleet_heartbeat.check()` |
| 재시작 성공은 조용히 | `harness_monitor.check_and_restart_bots()` — 재시작 후 **재확인**해 실패한 것만 알림 |
| 단일 봇 다운은 1주기(5분) 유예 | `fleet_heartbeat` 연속 2회 감지 시에만 경보 (워치독에게 살릴 기회를 준다) |
| 같은 문제 30분 쿨다운 | `fleet_heartbeat.ALERT_COOLDOWN_SEC` |
| 산출물 36h / 주간 8일 | `petnna_qa_patrol.fleet_freshness_audit()` |
| PR대기 하루 1회 | `petnna_dev_engine` (`SURI_MAX_PENDING`, 기본 5) |

**바꾸려면 표를 먼저 고치고 코드를 맞춰라.** 반대로 하지 마라 —
코드에 흩어진 임계값이 정책이 되면, 오늘처럼 아무도 그 정책을 설명하지 못한다.

---

## 8. 이 문서의 갱신 규칙

- **사고가 나면 §5에 1줄 추가한다.** 증상 → 진범 → 조치 순서로.
- 구조 설명을 여기 복사하지 마라. 링크만 걸어라. (사본은 반드시 낡는다)
- 헌장(§2)에 항목을 추가하려면 **오너 승인**이 필요하다.
