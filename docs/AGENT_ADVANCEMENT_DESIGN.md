# 에이전트 고도화 설계 — 분담 · 오케스트레이션 · 자동 생성

> 작성일: 2026-06-27 · 대상: ai-team (예원/영숙/소미)
> 목적: 에이전트를 더 포괄적으로 동작시키고, 업무를 자동 분담하며, 필요 시 신규 에이전트를 자동 생성하는 구조 설계.

---

## 0. 현재 구조 진단 (코드 근거)

| 구성요소 | 현재 동작 | 한계 |
|---|---|---|
| `yewon_dispatcher.py` | 키워드 매칭 → 실패 시 LLM 1회 호출 → **단일 에이전트 · 단일 스크립트** 실행 | 멀티스텝 불가, 작업 분할/취합 없음, agent 후보 `somi/youngsuk/ceo` 하드코딩 |
| `skill_auditor.py` | `_build_agent_folder_map()`이 `skills/` 폴더를 **동적 스캔**해 SKILL.md 보유 폴더 자동 발견 + `_detect_overlaps()` 역할 중복 감지 | 분석만 함. 발견 결과가 dispatcher/실행계층과 연결 안 됨 |
| `notify.py` | `CONTINUOUS_DAEMONS` / `SCHEDULED_SERVICES` / `_AGENT_LABELS` **하드코딩 dict** | 에이전트 추가 시 수동 편집 |
| `agents.ts` | `AGENTS` 맵 **하드코딩** | 동상 |
| `harness_manager.py` | `analyze_structure()`가 3개 SKILL.md 경로 **하드코딩** 체크 | 신규 에이전트 미반영 |

**핵심 모순**: 감사 계층은 이미 동적인데, 라우팅·실행·등록 계층은 정적. 고도화의 90%는 이 둘을 **단일 진실 소스(SSOT)**로 잇는 일이다.

---

## 1. 토대 — Agent Registry (단일 진실 소스)

흩어진 하드코딩(dispatcher 키워드, notify dict, agents.ts, harness 경로)을 **하나의 레지스트리**로 통합한다.

`projects/ai-team/_shared/registry.py` + `output/cache/agent_registry.json`

```jsonc
{
  "somi": {
    "display": "소미",
    "role": "국내주식 수급·매수판단 분석가",
    "keywords": ["종목", "수급", "공매도", "매수", "..."],
    "tools": [
      {"name": "report",   "script": "skills/소미_분석가/tools/somi_kis_reporter.py", "args": ["--send"]},
      {"name": "screen",   "script": "skills/소미_분석가/tools/somi_screener.py"},
      {"name": "short",    "script": "skills/소미_분석가/tools/short_covering_analyzer.py"}
    ],
    "daemons":   {"somi_monitor": "somi_price_monitor.py"},
    "scheduled": {"somi": "com.ailab.somi"},
    "status": "active",        // active | quarantined | retired
    "created_by": "human",     // human | yewon_factory
    "created_at": "2025-..."
  }
}
```

- `skill_auditor`의 동적 발견 로직을 여기로 흡수해, **레지스트리가 기존 SKILL.md 스캔 + JSON 메타를 병합**해 산출.
- `notify.py`·`agents.ts`(빌드 시 생성)·`harness_manager`·`dispatcher`가 전부 이 레지스트리를 읽게 바꾼다 → 등록 지점이 1곳으로 수렴.
- **이 단계만으로도** 신규 에이전트 추가가 "JSON 한 항목 + 폴더"로 끝남 (자동 생성의 전제조건).

---

## 2. 단계 A — 에이전트 고도화 (각자가 더 포괄적으로)

현재 소미·예원은 고정 스크립트 1개를 실행한다. 영숙만 Gemini Function Calling으로 상황별 tool을 고른다. **이 패턴을 소미·예원으로 확장**한다.

각 에이전트에 얇은 "판단 루프"를 추가:

```
입력 → (LLM이 registry의 tools 목록을 보고) 어떤 tool들을 어떤 순서로 쓸지 결정
     → 실행 → 결과를 LLM이 요약/판단 → (추가 tool 필요 시 반복, 최대 N회) → 최종 보고
```

- 구현 위치: `_shared/agent_loop.py` (공용). 각 에이전트는 자기 registry tools만 주입받아 호출.
- `_shared/llm.py`의 기존 fallback(Ollama→GPT→Gemini)·`json_mode` 재사용 → 신규 의존성 없음.
- 가드: 루프 상한(`max_steps=4`), tool 화이트리스트(자기 것만), 타임아웃 — 폭주 방지.

효과: "소미야 우리기술 어때?" → 수급 조회 + 숏커버링 + 스크리너를 **스스로 조합**해 종합 답변. 지금은 한 스크립트만 실행.

---

## 3. 단계 B — 업무 분담 (오케스트레이션)

`yewon_dispatcher`를 **단일 라우팅 → 플랜 기반 멀티 에이전트**로 격상.

```
지시 수신
  → 예원 LLM이 "플랜(작업 그래프)" 생성:
      [{step:1, agent:"somi",     task:"...", depends_on:[]},
       {step:2, agent:"youngsuk", task:"...", depends_on:[1]}]
  → 의존성 순서대로 각 에이전트(단계 A 루프) 호출
  → 결과 취합 → 예원이 최종 종합 보고
```

- 신규 파일: `skills/예원_CEO/tools/yewon_orchestrator.py` (기존 dispatcher는 단순/단일 경로용으로 유지, fallback).
- 작업 큐: `output/cache/task_queue.json` — 진행/완료/실패 상태 추적, 재시작 복원력.
- 라우팅 후보는 **registry에서 동적 로드** (하드코딩 `somi/youngsuk/ceo` 제거).
- 병렬화는 2단계: 우선 순차(의존성만), 이후 독립 step 동시 실행.

효과: "이번 주 시장 정리하고 일정에 리뷰 잡아줘" → 소미(분석) → 영숙(캘린더 등록)으로 **자동 분담**.

---

## 4. 단계 C — 신규 에이전트 자동 생성 (메타 에이전트)

가장 야심찬 부분. **반드시 승인 게이트 뒤에 둔다.**

`skills/예원_CEO/tools/agent_factory.py`

```
트리거: 오케스트레이터가 "현재 registry 어느 에이전트도 못 맡는 작업" 감지
  → 예원이 신규 에이전트 명세 제안(JSON): {역할, 키워드, 필요 tools 초안}
  → ⛔ 텔레그램 승인 요청 ("새 에이전트 '○○' 생성할까요? [승인/거절]")
  → (승인 시) 템플릿으로 생성:
        skills/<이름>/SKILL.md            (표준 양식)
        skills/<이름>/tools/<이름>_main.py (표준 import 패턴 스캐폴드)
        registry.json 항목 추가 (status:"quarantined", created_by:"yewon_factory")
  → 격리 상태로 1회 시범 실행 → 정상이면 status:"active" 승격
```

### 자동 생성 리스크와 대응 (과거 가짜 에이전트 사태 재발 방지)

| 리스크 | 대응 |
|---|---|
| 무한 증식 | `factory`는 **사람 승인 없이 active화 불가**. 일일 생성 상한(예: 1개). |
| 좀비 프로세스 | 신규 데몬은 기존 `process.py` 뮤텍스 락 의무 주입. `agent_controller` 등록 후에만 상시 실행. |
| 가짜/유령 에이전트 | `created_by:"yewon_factory"` + `quarantined` 태그. **백엔드(tools 폴더+실행 검증) 없으면 자동 retire**. harness가 매 점검 시 검사. |
| 역할 중복 | 생성 전 `skill_auditor._detect_overlaps()` 통과 필수 — 기존 에이전트와 겹치면 거부. |
| 권한 오남용 | 신규 에이전트는 tool 화이트리스트 비어서 시작. 권한은 명시적으로만 부여. |

---

## 5. 권장 진행 순서 (점진적, 각 단계 독립 가치)

```
1. Registry(SSOT) 도입        ← 토대. 위험 낮음. 즉시 가치(추가가 1곳)
2. 단계 A: 에이전트 루프       ← 소미/예원에 Function-Calling 패턴
3. 단계 B: 오케스트레이터      ← 멀티 에이전트 분담
4. harness/감사를 registry 연동 ← 자동 검증 체계
5. 단계 C: agent_factory      ← 승인 게이트 + 격리. 마지막.
```

각 단계는 다음 단계 없이도 독립적으로 동작·가치 제공한다. 1·2번만 해도 체감 고도화가 크고, 5번은 1~4가 안정된 뒤 진입.

---

## 6. CLAUDE.md 운영 원칙과의 정합성

- **타겟 패치 우선**: registry 도입도 기존 dict를 한 번에 갈아엎지 말고, 어댑터로 양립시킨 뒤 점진 이행.
- **`_shared/` 변경 주의**: registry/agent_loop는 신규 파일로 추가, 기존 5개 모듈은 읽기만(병합 소스).
- **승인 절차**: 단계 C는 본질적으로 사람 승인 루프 — CLAUDE.md의 "의존성/아키텍처 변경 전 승인" 원칙과 일치.
- **뮤텍스/좀비 금지** 원칙을 factory가 강제 주입하도록 설계.

---

## 부록: 변경 영향 파일 (착수 시 수정 대상)

| 단계 | 신규 | 수정 |
|---|---|---|
| Registry | `_shared/registry.py`, `output/cache/agent_registry.json` | — |
| 단계 A | `_shared/agent_loop.py` | `소미`/`예원` tools 진입점 |
| 단계 B | `예원_CEO/tools/yewon_orchestrator.py`, `task_queue.json` | `yewon_dispatcher.py`(fallback화) |
| 연동 | — | `notify.py`, `agents.ts`, `harness_manager.py` (registry 읽기로 전환) |
| 단계 C | `예원_CEO/tools/agent_factory.py`, 에이전트 템플릿 | `agent_controller.py`(동적 등록) |
