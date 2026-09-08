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
- **씬은 커밋된 산출물이다** — 배치 코드만 고치면 화면은 안 바뀐다. `Ensure*` 멱등 패스로 수렴시켜라.
  재드레싱 사슬은 닫혔다(`Dress Village` 두 번 + 셀프체크 EXIT=0).
- 유니티가 오염시킨 자산을 되돌릴 땐 **`git stash` 먼저, 자산 경로만** 지정
  — `git checkout -- Assets`는 내 편집도 지운다(실제로 한 번 잃었다).

## 커밋 꼬리
`74023bb6`(랩 A) → `b997e5ee`(박공 자리) → `e1e07bed`(지붕 규칙·수리 패스·겹침 게이트) →
`2fc8a4b3`(인수인계) → `19572358`(재드레싱 수리) → `bde45de6`(용마루 한 줄·목표 수렴) →
`8842f295`(껍데기 몹) → `25194af3`(랩 ② 뼈 고르기)

## 닫힌 것
보스 그립 NC · mono_crash · 은행 지붕 · 배우 전수 스윕(③) · 장비를 자리로 찾기(①) · 랩 A 그림↔캡슐 ·
**민가 지붕 판때기**(용마루 한 줄, 7채) · **재드레싱 사슬**(랜드마크 소실·프리팹 GUID·`EnsureHousingPlot`) ·
**껍데기 몹**(묻힌 몸에 그림 물려주기) · **랩 ② 뼈 고르기**(머리 장식·방패를 자리·성질로)

## 진행 중 — 랩 B (킷 배율)
`VisualSliceBuilder.Rooms.cs`에 **`KitScale = 2.1`** 원장을 넣었다(미적용, 아직 씬은 원래 크기).
`SliceSelfCheck.KitScale.cs`(새 파일)에 **문/사람 자 + 양방향 NC**를 굳혔다 — **아직 배선 안 했다**
(세계를 안 키운 상태로 배선하면 상시 빨간불).

실측(민가 벽 조각 72개 전수, 이름 아닌 성질로): `wall-door` **0.74m** · 창 0.14~0.18m · 벽 0.00m,
벽 모듈 1.00×1.00×0.10m. 필요 `PlayerHeight 1.8 × 0.85 = 1.53m` → **2.0배는 1.48m로 5cm 모자란다**.
유도값 `1.53/0.74 = 2.07` → **2.1**. 이 숫자와 함께 검수에 **경로**를 되물어 둔 상태다:
민가가 **1m 모듈 정수 좌표**로 조립되므로 「조각만 제자리 확대」는 배치가 깨진다 —
묶음 뿌리 하나의 `localScale`로 가면 배우 패스가 안 커진 좌표에 사람을 놓고,
빌더 좌표에서 유도해 **다시 굽는** 편이 자를 덜 만든다. **검수 판정 오면 그대로 간다.**

남은 조건(검수): ㉮`*=` 금지·현재 값을 재서 목표 수렴 ㉯두 판 연속 값 불변 로그
㉰킷은 **출처**(`/Kenney/`)로 가리되 지형·KayKit 제외 — 출처는 프리팹 뿌리가 아니라 **자식 `Visual`**에 있다.
화면 두 장: 문/사람 근접 · 나무·산 원경.

**사냥터 간격은 닫혔다**(`eb8c779d`) — 월드 거리만으로는 초록인데 화면이 겹쳤다. QA 샷 카메라를
원장(`VisualSliceBuilder.HuntViewEye`)으로 빼고 **그 눈에서 본 방위각 차 ≥ 몸이 차지하는 각**으로
잰다(양방향 NC 포함, 실측 여유 +3.5°). 화면으로 확인 완료.

## 다음 (순서)
1. 랩 B 마무리(위).
2. **절대 미터를 비율로** — `SliceSelfCheck.BossTraits.cs`의 `WeaponGripDistMax = 0.25f`·
   `WeaponTipDistMin = 0.50f`는 **몸 높이 대비**, 왕관 축 `0.15f`는 **머리 폭 대비**로.
   발-지표 `0.10`·장비 관통 `0.05`는 절대값 유지하되 **매 판 실측 여유를 로그로**.
   같은 랩에서 **물림 자 셋이 보스에만 걸린 것**을 일반 배우로 넓힐지 판단(검수 경증).
3. MegaKit(모루·화덕/절구통)은 오너가 `_ThirdParty/Quaternius/`에 파일을 넣어야 진행(다운로드는 오너 직행).

## 손대지 말 것
`OfflineWorld*`, `NetAvatar.cs`, 루트 `docs/SESSION_HANDOFF.md`(검수 것),
`.worktrees/herdr-*`·`autodev-*`, `OfflineWorld.Player`.

## 랩 규칙
읽기 → 하나 만들기 → **내 눈으로 검증**(셀프체크 두 판 EXIT=0 + QA PNG 실제로 보기) →
커밋·푸시(`git add -A` 금지, 경로 명시, add+commit 한 호흡) → 검수 보고 → 이 파일 갱신.
보고 끝에 **「구조 한 줄」**(잔재·비대 목록 + 원칙 한 줄).
