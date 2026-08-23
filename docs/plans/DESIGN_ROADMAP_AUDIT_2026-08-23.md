# 재와별 기획·로드맵 품질 감사 + 패치 초안

> 작성: 기획검증 (2026-08-23) · **제안만** — 아래 패치는 아직 원문에 미적용.
> 권위: `GAME_DESIGN_ASHES_TO_STARS.md` > SPEC > STATUS/INBOX > 코드.
> 범위: 문서 정렬. `EstateBuild.cs`·`W3Party`·보드 UI 코드는 건드리지 않음.

> **2026-08-23 追記:** 「네 개 전부 패치 초안」요청 → 완성본은 [`PATCHES_FOUR_2026-08-23.md`](./PATCHES_FOUR_2026-08-23.md).

---

## 한 줄 요약

필라(무가챠·3사망·잡몹자동/보스수동)는 탄탄하다.
품질 리스크는 **EstateBuild 소유권 교착 + V4/W2 미닫힘 + §22/STATUS/루프 우선순위 역전**.

---

## P0 / P1 발견 (증거)

### P0-1. EstateBuild — 규칙상 아무도 못 닫음
- `docs/STATUS.md` #1: 클로드만 `EstateBuild.cs`, **그록은 안 만진다**
- `docs/feedback/INBOX.md` 14:22: Claude 한도 → **Codex/Grok만**, 1순위 EstateBuild
- `docs/GAME_SPEC_ESTATE_BUILD.md` §4-B: `EstateBuild.cs` — **그록 미접촉**
→ 최우선 작업이 세 문서에 막혀 교착.

### P0-2. V4 미측정인데 범위·폴리싱 선행
- 원장 §21-6: V4 넘김·외부 표본 0
- STATUS는 Estate·소비처0·UI가 위, 사람 관문은 「루프가 닫지 않음」만

### P1-1. §22 「지금 당장」이 관문① PASS 미반영
- 표·§22-1b: 관문① PASS, 남은 건 관문②
- 「지금 당장」#1은 여전히 30층 종단 측정 톤

### P1-2. W2 FAIL 열린 채 UI 폴리싱 병행
- STATUS 보류: W2 FAIL, 기준 낮추지 말 것
- 필라(보스 수동·대시 손맛)의 기술 전제

### P1-3. 운영규칙 3 vs STATUS 실무 역전
- 원장: 소비처0 루프 ≠ 로드맵. 바닥나면 관문 측정
- STATUS #3~#7이 상시 본업처럼 동작

### 기타 문서 품질
- 원장 §3 기본직업 **5종** vs §23 「기본직업 **4종**」
- `GAME_DEV_HANDOFF.md` 완료표 기획서 **v0.6** (원장은 v0.8)
- 루트 `DESIGN.md` = Claude UI yaml (게임 기획 아님). `docs/DESIGN.md`만 포인터로 유효
- Trinity C5 탱 DR 20%: SPEC 오너 확정 vs WORKLOG 「E/A 이미 0.39 → 도입 보류」
- `SESSION_HANDOFF.md` 0바이트

---

## 패치 초안 (복붙용)

### Patch A — `docs/feedback/INBOX.md` (대기 중 보드·루프 블록에 한 줄 추가)

```markdown
- EstateBuild 소유권 (한시): Claude 주간 한도 동안 `EstateBuild.cs`·업그레이드 창은
  **Codex/Grok이 닫는다.** SPEC §4-B 「그록 미접촉」은 한도 해소 또는 이 지시 철회까지
  **정지.** HOLD: `touch loop/HOLD` 후 해당 파일만, 커밋 직후 해제. 클로드와 동시 수정 금지.
```

### Patch B — `docs/STATUS.md` #1 교체

```markdown
1. **영지 §4 (한시 Codex/Grok)** — `EstateBuild.cs` 건물별 업그레이드 창.
   INBOX 2026-08-23: Claude 한도 중이라 Codex/Grok이 닫음 (SPEC §4-B 한시 정지).
   StoreX 경로 소비처는 닫음(`EstateStore.Reached`). 남은 §5 드래그 UX·§6 아트는 그 다음.
   **사람 관문 부채(루프 밖):** 관문②(5h 지루함) · V4 · W2 — 아래 「막힌 것」 참고.
```

### Patch C — `docs/GAME_SPEC_ESTATE_BUILD.md` §4-B 표 아래 추가

```markdown
> **한시 예외 (2026-08-23 INBOX):** Claude 주간 한도 동안 `EstateBuild.cs`(§2-3)는
> Codex/Grok이 닫을 수 있다. 한도 해소 또는 오너 철회 시 본 표(클로드 전용)로 복귀.
> 그록이 착수하면 `loop/HOLD` + 커밋 메시지에 `estate-build:grok-temp` 한 줄.
```

### Patch D — 원장 §22 「지금 당장 할 일」교체

```markdown
### 지금 당장 할 일 (우선순위 순)

> 2026-08-23 갱신. 관문①은 §22-1b PASS. 곡선 재측정은 STATUS 큐에 「닫음」.

1. **단계 1 관문 ② — 5시간 연속 플레이 지루함** — 표본·척도·CSV 규격을 정한 뒤 1회 측정.
   (관문① PASS 전 측정 금지였던 이유가 해소됨.)
2. **EstateBuild §2-3 업그레이드 창** — INBOX 소유권(한시 Codex/Grok) 따름. 한 동 end-to-end.
3. **원장 ✅ 소비처 0곳 메우기** — 자동 루프 상시 작업. 1·2번을 대신하지 않음(운영규칙 3).
4. **로컬 온라인 기능 권위 분리 유지** — 경매·침략·랭킹 (운영규칙 4).
5. **외부 표본 재측정 준비** — 데모/EA 전 V2·V3·V4를 §21-1 규격으로 (§21-6).
```

### Patch E — 원장 §23 캐릭터 획득 한 줄

- 변경: `기본직업 4종 중 1명 선택` → `기본직업 5종 중 1명 선택` (§3과 통일)

### Patch F — `docs/STATUS.md` 「막힌 것·보류」보강

```markdown
## 막힌 것 · 보류
- W2 FAIL (회피 기회 부족) — 기준 낮추지 말 것. **담당: 대화 세션.** 재개: 위협밀도/대시 손맛 손대기 직전.
- V4 영구삭제 수용성 — §21-6 미측정. **담당: 사람 관문.** 재개: 데모·EA 전 또는 사망 규칙 변경 시.
- V2/V3 — 루프가 닫지 않음. 단계4 진입 전 §21-6과 함께.
- 전체 이력: `docs/archive/legacy_loop_docs_20260823/STATUS.md` · `docs/GAME_WORKLOG.md`
```

### Patch G — 소소
- `GAME_DEV_HANDOFF.md`: 기획서 v0.6 → **v0.8**, 「다음에 할 것」은 STATUS/INBOX 포인터로 축소
- Trinity C5: WORKLOG 보류(E/A 0.39)를 SPEC에 「도입 보류·근거 소멸」로 역기록하거나 원장에 미도입 명시
- 루트 `DESIGN.md`: 상단 BIG WARNING 유지 확인 (게임 원장 아님). 가능하면 `DESIGN.claude-ui.yaml`로 rename

---

## 관문② · V4 검증 체크리스트 (초안)

### 관문② (5h 지루함) — 아직 규격 없음, 제안
- [ ] 세션: 한 계정, 필드+탑 혼합, 목표 5시간 벽시계
- [ ] 기록 CSV: 10분 단위 행동(사냥/탑/영지/상점), 사망·부활초 소비, 층·Lv
- [ ] 이탈 사유 자유기술 1회 (끝 또는 중도 포기 시)
- [ ] 합격 초안: 중도 포기 없이 5h 완주 + 「의무 숙제감」자가진단 ≤2/5 (오너 확정 필요)
- [ ] 난이도 평탄 상태로 재지 말 것 (관문① PASS 전제 — 이미 충족)

### V4 (§21-1)
- [ ] `docs/V4_EXTERNAL_PLAYTEST.md` + `loop/v4_*.py`가 표(표본·70%·24h)와 일치하는지
- [ ] 외부 표본 일정 / 삭제 1회 강제 경험 포함 여부
- [ ] 실패 시 연쇄(사망·환생·부활초·골드=목숨) 롤백 범위 메모

### W2
- [ ] HANDOFF 수치 재현 (포위·접촉·흡수) — **기준 하향 금지**
- [ ] 변경 전후 absorb≥3 확인

### 소비처 루프 감사
- [ ] 원장 ✅ 잔여 vs STATUS 「닫음」교차 — 증거(커밋·SelfCheck) 없는 닫힘 탐지

---

## 적용 순서 제안

1. Patch A+B+C (교착 해소) — 루프가 다시 움직일 수 있게
2. Patch D+F (원장·STATUS 관문 부채 정렬)
3. Patch E+G (문서 품질)
4. 관문② 규격 오너 확정 후 측정 1회

