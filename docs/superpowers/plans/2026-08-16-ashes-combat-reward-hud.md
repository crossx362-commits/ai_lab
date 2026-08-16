# 전투 보상 루프 HUD Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 전장과 겹치지 않는 보상 루프형 전투 HUD를 구축한다.

**Architecture:** `W3Party`가 보상 큐와 안전 영역 계산을 보유하고 기존 IMGUI를 상단 요약·우측 보상·하단 지휘 렌더러로 나눈다. 보스 상태는 읽기 전용으로 연결하며 전투 규칙과 저장 상태는 변경하지 않는다.

**Tech Stack:** Unity 6000.5, C#, IMGUI, 기존 SpriteBank/PortraitAtlas/CombatIconAtlas

**Spec:** `docs/superpowers/specs/2026-08-16-ashes-combat-reward-hud-design.md`

## Global Constraints

- UI는 상단 72px, 우측 170px, 하단 148px 안전 영역 밖으로 그리지 않는다.
- 보상 큐는 최대 3개이며 각 항목은 2.2초 후 사라진다.
- 전투 수치·밸런스·저장 데이터·FxPool 정렬값을 바꾸지 않는다.
- 1280×720과 1440px 폭에서 캡처로 간섭을 검증한다.

---

### Task 1: HUD 안전 영역과 보상 큐의 실패 검증 추가

**Files:**
- Create: `projects/ashes-to-stars/unity/Assets/_Game/Scripts/Editor/CombatHudSelfCheck.cs`
- Modify: `projects/ashes-to-stars/unity/Assets/Scripts/W3Party.cs`

**Interfaces:**
- Produces: `W3Party.CombatHudTopHeight`, `CombatHudBottomHeight`, `CombatHudRewardMaxEntries`, `CombatHudRewardLifetime`

- [ ] **Step 1: 실패 자가검사를 작성한다.** `CombatHudSelfCheck.Run`에서 다음을 검사한다.

```csharp
Debug.Assert(W3Party.CombatHudTopHeight <= 72f);
Debug.Assert(W3Party.CombatHudBottomHeight >= 148f);
Debug.Assert(W3Party.CombatHudRewardMaxEntries == 3);
Debug.Assert(Mathf.Approximately(W3Party.CombatHudRewardLifetime, 2.2f));
```

- [ ] **Step 2: Unity 배치 실행으로 `W3Party` 상수 부재에 따른 컴파일 실패를 확인한다.**
- [ ] **Step 3: `W3Party`에 상수와 최대 3개 보상 항목의 내부 큐를 추가한다.**
- [ ] **Step 4: `CombatHudSelfCheck.Run`이 통과하는지 확인한다.**
- [ ] **Step 5: 상수·큐·자가검사를 커밋한다.**

### Task 2: 보상 루프형 렌더링으로 전투 로그 교체

**Files:**
- Modify: `projects/ashes-to-stars/unity/Assets/Scripts/W3Party.cs:2783-2948`
- Test: `projects/ashes-to-stars/unity/Assets/_Game/Scripts/Editor/CombatHudSelfCheck.cs`

**Interfaces:**
- Consumes: Task 1 상수와 보상 큐
- Produces: `DrawCombatSummary`, `DrawRewardRail`, `DrawCommandBar`

- [ ] **Step 1: 보상 최대 개수가 3개라는 실패 자가검사를 추가한다.**
- [ ] **Step 2: 새 렌더러 부재로 실패하는 컴파일을 확인한다.**
- [ ] **Step 3: 기존 64px 텍스트 로그를 제거한다. `DrawCombatSummary`는 상단 72px, `DrawRewardRail`은 우측 170px, `DrawCommandBar`는 하단 148px만 사용한다. `KillMob`에서 `PushReward`를 호출한다.**
- [ ] **Step 4: 자가검사와 Unity 컴파일을 통과시킨다.**
- [ ] **Step 5: HUD 교체를 커밋한다.**

### Task 3: 빌드와 화면 간섭 검증

**Files:**
- Test: `projects/ashes-to-stars/unity/Assets/_Game/Scripts/Editor/CombatHudSelfCheck.cs`

**Interfaces:**
- Consumes: Task 2 HUD 렌더러
- Produces: 전투·보스·VFX 테스트 스크린샷

- [ ] **Step 1: `GAME_START=hunt GAME_SHOT_SEC=12`로 1280×720 전투 캡처를 만든다.**
- [ ] **Step 2: `GAME_START=boss GAME_SHOT_SEC=12`로 보스 캡처를 만든다.**
- [ ] **Step 3: 1440×810 전투 캡처를 만든다.**
- [ ] **Step 4: `PlayableScenesBuilder.BuildGame`으로 스탠드얼론 빌드를 생성한다.**
- [ ] **Step 5: 최종 변경과 검증을 커밋한다.**
