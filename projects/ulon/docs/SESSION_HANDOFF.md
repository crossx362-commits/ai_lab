# SESSION HANDOFF — Ulon (자율개발 루프 세션)

> 지금까지 슬라이스에 들어간 기능 전량은 `SliceSelfCheck` PASS 로그 한 줄에 나열돼 있다
> (`unity/Logs/selfcheck_dev.log`의 "Slice self-check PASS —" 줄). 여기엔 **현재 상태와 다음 단계만** 둔다.

## 지금 위치
검수 세션(`local_7b28e464-020e-4be4-905a-270a96c891d7`)이 준 우선순위 큐를 따라 작업한다.
한 파트가 끝나면 검수에 보고 → QA 지적이 오면 그것을 큐 맨 앞에 넣는다(오너 지시).

## 실행 명령 (모두 `/Users/junholee/ai_lab/projects/ulon`에서, **유니티 에디터를 닫고**)
```bash
SELFCHECK_LOG=$PWD/unity/Logs/selfcheck_dev.log ./tools/slice_selfcheck.sh   # 배치모드 Assert 전량, EXIT=0이어야 함
./tools/qa_shots.sh                                                          # builds/qa/*.png 14장
```
검수 세션도 같은 스크립트를 쓰므로 로그 파일명을 `SELFCHECK_LOG`로 분리한다(덮어쓰기 방지).

## 작업 규칙 (검수가 반려하며 못박은 것 — 어기면 되돌려진다)
1. **수치 게이트는 양쪽 한계** — 하한만 두면 상한 초과를 통과시킨다.
2. **네거티브 컨트롤 먼저** — 고치기 전 상태에서 게이트가 FAIL(exit 1) 나는 로그 줄을 확보해 보고에 싣는다.
3. **화면 근거는 플레이 카메라 샷**(`QaShots.PlayCam` — 씬의 QuarterViewCamera pitch/yaw/distance를 읽고 런타임과 같은 `DungeonSightFade.Hide` 적용). 전용 카메라 샷은 증거가 아니다.
4. 씬을 바꾸는 작업은 `Assets/Game/Scenes/Bootstrap.unity`도 같이 커밋.
5. 보고엔 기획서 조항 번호(`docs/GAME_DESIGN.md` §4.2·§6.1·§8.1·§8.2·§10.2·§11·§12.2)를 인용.

## 최근 커밋 (오래된 것 → 최신)
… eb0961ce(흰 바위 잔재), b074dc52·cf8404c5·34033723(던전 지하화 + 게이트 2종),
8ec5cf4a(인수인계), cbf2341d·8646a0ca·ed1098ad·e89faccf·096261d0·eb88e531(월드 지형 산·바다·강·호수),
dc18aeb5(왕관 위치·보스 무기·입구 마감 반려 3건), 2ef3b6c4(던전 침수), 917f0f3f(발 높이).

## 최근에 끝난 것
1. **던전 지하화**(검수 P0) — 방 바닥 지면 −4.5m, 6×6 암반 뚜껑 + Terrain 홀.
   게이트: 화면 잔디 비율 45% 상한(§8.2) / `AssertDungeonLighting`(주광 차단·지하 점광 3개↑).
2. **월드 지형**(오너 지시) — `Shared/WorldTerrain.cs`가 높이 원장. 맵 300m·높이 60m,
   평지 → 산 띠(최고 52m) → 해안 → 바다, 호수(-70,10)·강(x −80~−140)이 같은 수면(3.0) 공유.
   도포 3종(풀·바위·모래). `AssertWorldTerrain`(기복·표준편차·수면 아래 비율·호수/강 개별·도포·콘텐츠 8곳 뭍).
3. **검수 반려 3건** — 왕관을 CharacterController 머리 기준으로(Assert: 바닥 ≥ 몸높이 0.8배),
   보스 무기를 실제로 렌더(가장 큰 장비 선택·조상 활성화·HideExtraGear 예외·EnsureBossDressing 멱등,
   Assert: 렌더러 + 최장변 ≥ 몸높이 0.4배), 입구 흰 바위 제거(EnsureEntranceClearance)·옆벽 정렬.
4. **지형 작업이 부른 회귀 2건** — 던전이 수면 아래로 잠김(LandBase 10.0 + 침수 Assert),
   방 안 몹·보스가 바닥에 파묻힘(StandOnRoomFloor + 발 높이 Assert).

## 다음 할 일 (검수 확정 순서 — 새 지시가 오면 그게 맨 앞)
1. 검수 회신 대기: 지형 2차(Blender로 실제 산·바위 메시를 얹는 것까지 범위인지) 여부.
2. 온라인 서버권한 2건(`NetAvatar` SyncVar 노출 / `RpcTame`·`RpcPet` 부재) — 검수가 실물 검수 후 순위 확정.
3. 테스트 공간(§6.1) 구현 + 문서의 "MVP 콘텐츠 상한 충족" 문구를 미구현으로 정정.
4. `DataLedger` 레코드 단위 검증(아이템 weight>0·buy≥0·uses≥0·strReq≥0 / 몹 hp>0·height>0·name 비어있지 않음·dmgMin≥0·dmgMax≥dmgMin),
   불량 레코드는 코드 폴백 + `Debug.LogError` + `loadError` 누적, 필드 없는 레코드로 코드 기본값 확인 Assert.
5. GM "원장 다시 읽기" 배선, "재빌드 없이" 문구 정정, 스택 결합 경고 문서, `TameCritter/TameBoar.DisplayName` 이중 원장,
   `MobCatalog.KindCount = 8` vs 14 모순, 클라 변조면 주석, Assert의 문자열 치환을 파싱/수정/기록으로 교체,
   `Assets/Game/Data/.gitkeep` 삭제.
6. 제작법(CraftRecipes) 외부화.

## 지형 작업이 남긴 교훈(반복 금지)
- `TerrainData`는 에셋이다 — `SaveAssets()` 없이는 씬을 다시 열 때 디스크의 옛 지형이 돌아온다.
- `Ensure*` 스폰 함수는 오브젝트가 있으면 일찍 반환한다 — 코드에서 고쳐도 **이미 만들어진 씬은 안 고쳐진다**.
  씬 상태를 바꾸는 수정에는 멱등한 `EnsureXxx` 보수 패스를 만들고 셀프체크에서 부를 것.
- 지형 높이를 만지면 던전 침수·발 높이 어긋남 같은 회귀가 난다 — 지형 상수를 바꾸면 QA 샷 전량을 눈으로 볼 것.
