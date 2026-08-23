# 재와별 문서 패치 4종 (복붙용 · 2026-08-23)

> 요청: 「네 개 전부 패치 초안」
> 작성: 기획검증 · **2026-08-23 원문 반영 완료**
> 적용 시: 고친 파일만 stage. `git add -A` 금지.

---

## 적용 상태 (이 문서를 쓸 시점)

| # | 패치 | 상태 |
|---|---|---|
| 1 | STATUS 「관문 부채」칸 | ✅ 원문 반영 (2026-08-23) |
| 2 | EstateBuild 소유권 (STATUS·INBOX·SPEC) | ✅ 원문 반영 (2026-08-23) |
| 3 | 원장 §22 「지금 당장 할 일」 | ✅ 원문 반영 (2026-08-23) |
| 4 | 루트 `DESIGN.md` 경고/격리 | ✅ 원문 반영 (2026-08-23) |

---

## 패치 1 — `docs/STATUS.md` 상단 「관문 부채」

### 위치
헤더(폴리싱 다음)와 `## 다음 할 일` 사이에 삽입.

### 넣을 블록 (그대로)

```markdown
## 관문 부채 (루프 밖 · 사람/대화 세션)

> 원장 §22 운영규칙 3: 소비처0 루프는 로드맵이 아님. 아래는 루프가 닫지 않는 관문.

| 관문 | 상태 | 담당 | 재개 트리거 |
|---|---|---|---|
| 단계1 관문② (5h 지루함) | 미측정 · 규격 미확정 | 사람 세션 | 관문① PASS 이후(이미 PASS) — 규격 확정 후 1회 |
| V4 영구삭제 수용성 | §21-6 넘김 · 외부 표본 0 | 사람 관문 | 데모·EA 전 / 사망 규칙 변경 시 |
| V2 손맛 · V3 | 사람 관문 | 사람 | 단계4 진입 전 §21-6과 함께 |
| W2 FAIL (회피 기회) | FAIL 유지 · **기준 낮추지 말 것** | 대화 세션 | 위협밀도·대시 손맛 손대기 직전 |
```

### 「막힌 것 · 보류」교체 (파일 하단)

```markdown
## 막힌 것 · 보류
- 위 **관문 부채** 표가 권위. 여기에는 한 줄 요약만.
- W2 FAIL — 기준 낮추지 말 것 · 담당 대화 세션
- V2/V3/V4 — 루프가 닫지 않음 · 데모·EA 전 재측정
- 전체 이력: `docs/archive/legacy_loop_docs_20260823/STATUS.md` · `docs/GAME_WORKLOG.md`
```

---

## 패치 2 — EstateBuild 소유권 정렬 (INBOX ↔ STATUS ↔ SPEC)

### 2-A. `docs/feedback/INBOX.md` — 「대기 중 · 보드·루프」블록에 추가

```markdown
- EstateBuild 소유권 (한시): Claude 주간 한도(~2026-08-24 23:00 KST) 동안
  `EstateBuild.cs`·업그레이드 창(`EstateScreen`)은 **Codex/Grok**이 닫는다.
  SPEC §4-B 「그록 미접촉」은 한도 해소 또는 이 지시 철회까지 **정지**.
  HOLD: `touch loop/HOLD` 후 해당 파일만, 커밋 직후 해제. 클로드와 동시 수정 금지.
```

### 2-B. `docs/STATUS.md` #1 (이미 반영된 문장 — 유지용 최종본)

```markdown
1. **영지 §4** — `EstateBuild.cs` 건물별 업그레이드 창. INBOX(2026-08-23): Claude 한도 중 **Codex/Grok**이 닫음 (SPEC §4-B 한시 정지). StoreX 경로 소비처는 닫음(`EstateStore.Reached`). 남은 §5는 마우스 드래그 UX(TryMove는 있음). §6 아트는 그 다음.
```

### 2-C. `docs/GAME_SPEC_ESTATE_BUILD.md` §4-B (이미 한시 예외 삽입됨 — 유지용 문구)

```markdown
> **한시 예외 (2026-08-23 INBOX):** Claude 주간 한도(~08-24 23:00 KST) 동안
> `EstateBuild.cs`(§2-3)와 이어지는 업그레이드 창(`EstateScreen`)은 **Codex/Grok**이 닫을 수 있다.
> 한도 해소 또는 오너 철회 시 아래 표(클로드 전용)로 복귀.
> 착수 시 `loop/HOLD` + 커밋 메시지에 `estate-build:codex-temp` 한 줄.
```

---

## 패치 3 — 원장 `docs/GAME_DESIGN_ASHES_TO_STARS.md` §22 「지금 당장 할 일」

### 지울 구간
`### 지금 당장 할 일 (우선순위 순)` 부터 `## 23.` 직전까지.

### 넣을 블록 (그대로)

```markdown
### 지금 당장 할 일 (우선순위 순)

> 2026-08-23 갱신. 관문①은 §22-1b PASS(2026-08-19). 옛 #1·#2(30층 종단·곡선 판정)는 닫힘.

1. **단계 1 관문 ② — 5시간 연속 플레이 지루함** — 표본·척도·CSV 규격을 오너가 확정한 뒤 1회 측정.
   합격 초안(미확정): 중도 포기 없이 5h 완주 + 「의무 숙제감」자가진단 ≤2/5.
   난이도 평탄 상태로 재지 말 것(관문① PASS 전제 — 이미 충족).
2. **EstateBuild §2-3 업그레이드 창** — INBOX 소유권(한시 Codex/Grok) 따름. 한 동 end-to-end + SelfCheck.
3. **원장 ✅ 소비처 0곳 메우기** — 자동 루프 상시 작업. 1·2번을 대신하지 않음(운영규칙 3).
4. **로컬 온라인 기능 권위 분리 유지** — 경매·침략·랭킹 (운영규칙 4).
5. **외부 표본 재측정 준비** — 데모/EA 전 V2·V3·V4를 §21-1 규격으로 (§21-6).
```

### (선택) §23 캐릭터 획득 한 줄 — 같은 커밋에 넣어도 됨

- 찾기: `기본직업 4종 중 1명 선택`
- 바꾸기: `기본직업 5종 중 1명 선택` (§3과 통일)

---

## 패치 4 — 루트 `DESIGN.md` 격리

루트 파일은 Claude UI 테마 YAML(게임 기획 아님). 루프·에이전트가 원장으로 오인함.

### 옵션 A (권장) — 파일명 변경 + 포인터 stub

1) 이동:

```bash
mv DESIGN.md docs/archive/DESIGN.claude-ui-theme.yaml
```

2) 새 루트 `DESIGN.md` 전체 내용:

```markdown
# ⛔ 이 파일은 게임 기획이 아니다

> **재와 별 기획 원장:** `docs/GAME_DESIGN_ASHES_TO_STARS.md`
> **실무 큐:** `docs/STATUS.md` · `docs/GAME_WORKLOG.md` · `docs/feedback/INBOX.md`
> **요약 틀:** `docs/DESIGN.md`

루트에 있던 동명 파일은 Anthropic Claude UI 테마 YAML이었다.
보관 위치: `docs/archive/DESIGN.claude-ui-theme.yaml`

루프·에이전트는 이 파일을 기획 원장으로 읽지 마라. (`loop/PROMPT.md` ②-4)
```

### 옵션 B (이름 유지) — 파일 맨 위 BIG WARNING만

기존 YAML **앞에** 삽입 (YAML frontmatter를 깨지 않으려면 옵션 A 권장):

```markdown
<!--
⛔ GAME DESIGN? NO.
This root DESIGN.md is a Claude product UI theme (YAML), NOT Ashes to Stars.
Canon: docs/GAME_DESIGN_ASHES_TO_STARS.md
Pointer: docs/DESIGN.md
-->
```

옵션 B는 파서가 주석을 무시하지 않으면 테마 파일을 망가뜨릴 수 있으니 **옵션 A만 권장**.

---

## 적용 순서

1. 패치 2 잔여(INBOX 한 줄) — 루프가 이미 Estate를 도는 중이라 지시 명문화
2. 패치 1 — STATUS 관문 부채 (보드가 읽음)
3. 패치 3 — 원장 §22 (권위 문서, 신중히)
4. 패치 4 — 루트 DESIGN 격리 (오인 방지)

커밋 메시지 예:

```
문서: 로드맵·소유권·관문부채 정렬 (기획검증 패치 1~4)

STATUS 관문 부채 · EstateBuild 한시 Codex/Grok · §22 지금당장 · 루트 DESIGN 격리
```

---

## 검증

```bash
grep -n '관문 부채\|한시 예외\|지금 당장 할 일\|게임 기획이 아니다' \
  docs/STATUS.md docs/feedback/INBOX.md docs/GAME_SPEC_ESTATE_BUILD.md \
  docs/GAME_DESIGN_ASHES_TO_STARS.md DESIGN.md docs/DESIGN.md
grep -n '기본직업 4종\|기본직업 5종' docs/GAME_DESIGN_ASHES_TO_STARS.md
```
