# SESSION HANDOFF — Ulon 자율 개발 루프

## 자리
- 작업 트리 **`/Users/junholee/ai_lab-loop`**, 브랜치 `loop-claude`, 푸시는 `git push origin HEAD:master`.
- `/Users/junholee/ai_lab`는 **남의 트리**(검수·Grok·Codex `autodev`) — 건드리지 않는다.
- 보고는 검수 세션 `local_7b28e464-020e-4be4-905a-270a96c891d7`. 안건 판정은 대장
  `local_07be93d8-1ac7-44a4-8d67-c82f238176a1`. 오너 직행은 셋뿐 — 다운로드·되돌릴 수 없는 삭제·돈.

## 도구 (모두 `projects/ulon`에서)
```bash
SELFCHECK_LOG=$PWD/unity/Logs/selfcheck_dev.log ./tools/slice_selfcheck.sh   # 편집 뒤 두 판 EXIT=0
./tools/qa_shots.sh            # builds/qa/*.png — 찍고 **눈으로 본다**
```
- 유니티는 프로젝트당 한 인스턴스(`pgrep -f "Unity.*-batchmode"` 먼저).
- `-nographics`로는 카메라 렌더가 죽는다(SIGSEGV) — 렌더가 필요하면 빼라.
- 셀프체크는 매 판 `Bootstrap.unity`를 **커밋 상태로 되돌리고** 시작한다(백업 한 벌).
- **씬은 커밋된 산출물이다** — 배치 코드만 고치면 화면은 안 바뀐다. 재드레싱(`Dress Village`)은
  랜드마크를 잃고, 전체 재빌드(`Build Visual Slice`)는 쌓인 것을 잃는다. **`Ensure*` 멱등 패스로
  수렴시켜라**(예: `VisualSliceBuilder.EnsureHouseRoofs`).
- 유니티가 오염시킨 자산을 되돌릴 땐 **자산 경로만** 지정 — `git checkout -- Assets`는 내 편집도 지운다.

## 커밋 꼬리
`74023bb6`(랩 A 그림/캡슐) → `b997e5ee`(박공 자리) → `e1e07bed`(지붕 규칙 한 자리·수리 패스·겹침 게이트)

## 닫힌 것
보스 그립 양방향 NC · mono_crash 정리 · 은행 지붕 높이/타일링 · 배우 전수 스윕(③) ·
장비를 자리로 찾기(①) · 랩 A 그림↔캡슐 맞춤 · **민가 지붕 판때기**(검수 발견, 7채 중 7채 수리)

## 다음 (순서)
1. **랩 ② 뼈 고르기** — `PickShield`는 정확 이름 `"Round_Shield"`, 모자는 substring `"Hat"`
   (`FitVillagerHat`·`IsHeadgear`). **이름이 아니라 자리·성질**로 바꾸고 양방향 NC + 화면 한 장.
2. **랩 B — `KitScale = 2.0` 단일 원장**(검수/대장 판정). Game Variant 프리팹 층(§12.2)에서
   **Kenney 킷 전부**(건물·울타리·가로등·간판·나무·덤불·소품). 지형 제외(실 미터), KayKit 던전 소품 제외.
   배치 간격·마을 반경·가드존은 상수에서 유도(눈대중 재배치 금지). 게이트는 **문/사람 ≥ 0.85 비율**,
   NC는 `KitScale=1`이면 빨간불. 같은 랩에서 나무·덤불이 언덕 대비 과대한지도 본다.
3. **절대 미터 한도를 비율로** — 플레이어가 작아지면서 상대적으로 헐거워졌다.
   특히 `SliceSelfCheck.BossTraits.cs`의 `WeaponGripDistMax = 0.25f`·`WeaponTipDistMin = 0.50f`.
4. MegaKit(대장간 모루·화덕 / 절구통)은 오너가 `_ThirdParty/Quaternius/`에 파일을 넣어야 진행
   (다운로드는 오너 직행).

## 손대지 말 것
`OfflineWorld*`, `NetAvatar.cs`, 루트 `docs/SESSION_HANDOFF.md`(검수 것),
`.worktrees/herdr-*`·`autodev-*`, `OfflineWorld.Player`.

## 랩 규칙
읽기 → 하나 만들기 → **내 눈으로 검증**(셀프체크 두 판 EXIT=0 + QA PNG 실제로 보기) →
커밋·푸시(`git add -A` 금지, 경로 명시, add+commit 한 호흡) → 검수 보고 → 이 파일 갱신.
보고 끝에 **「구조 한 줄」**(잔재·비대 목록 + 원칙 한 줄).
