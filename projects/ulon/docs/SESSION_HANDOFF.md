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
… 던전 지하화·월드 지형·지형 반려 1~3·왕관/무기/입구 반려 3건·침수/발높이 회귀,
f55878bb(실내 줌 §4.2 + 실내 화면 비율 하한 게이트), d7609f63(§6.1 지역 배치 + AssertWorldRegions).

## 최근에 끝난 것
1. **던전 지하화**(P0) — 방 바닥 −4.5m, 6×6 암반 뚜껑 + Terrain 홀. 게이트: 화면 잔디 45% 상한·지하 점광 3개↑.
2. **월드 지형** — `Shared/WorldTerrain.cs`가 높이 원장. 맵 300m·산 띠·바다·호수·강, 도포 3종. `AssertWorldTerrain`.
3. **검수 반려 3건** — 왕관 위치(CharacterController 머리 기준)·보스 무기 실렌더·입구 바위 제거/옆벽 정렬.
4. **회귀 2건** — 던전 침수(LandBase 10.0 + 침수 Assert)·방 안 발 높이(StandOnRoomFloor + Assert).
5. **§7.2 서버 권한 배선** — 조련/펫/키워드를 NetAvatar Rpc로. 게이트는 런타임이 아니라 **소스 정적 스캔**
   (`SliceSelfCheck.ServerAuthority.cs`) — 메서드 경계에서 멈추게 고치자 결함 2건이 더 드러났다.
6. **실내 줌(§4.2) + 실내 화면 비율 하한** — `QuarterViewCamera.IndoorDistanceMeters` 상수, `InteriorShareMin=0.90`
   (실측 실내 0.98~1.00 / 결함 0.67~0.78).
8. **도달 불가 기능 5건 배선 + 도달 가능 게이트**(626745dc) — 착용/해제·주머니 넣기/꺼내기·펫 놓아주기가
   서버에만 있고 클라 호출부가 없어 플레이어가 못 쓰던 상태였다. `AssertReachableFeatures`(전이 포함, 허용목록 1건).
   PASS 로그의 「내부 스텁」·하드코딩 보스 HP도 정정(MobCatalog에서 읽는다).
9. **§6.1 테스트 공간**(79b13d9e) — (-68,-68) 울타리 마당·표적 3·등불 4 + GM 패널 워프. `AssertTestChamber`.
7. **§6.1 지역 배치**(반려 4, d7609f63) — `Shared/WorldRegions.cs` 원장 + 농경지·숲·광산 실물 + 평지 산포 259개
   + 던전 뚜껑 위 장식 66개. `AssertWorldRegions`: 지역 소품 하한·사분면 4개↑·평지 12m 안 소품 75%↑
   (실측 91.0%, 네거티브 36.6% FAIL).

## 다음 할 일 (검수 확정 순서 — 새 지시가 오면 그게 맨 앞)
1. **산 도포 재반려** — 중턱 풀 하한 35%↑(현재 실측 0.19), 바위 텍스처를 풀과 다른 패턴/타일링으로,
   바위 색 분산 게이트. **무기 손 부착 판정**(무기 앵커가 손 본 0.3m 안). **왕관 가시성**(머리 축에서 수평 0.15m 안).
   보스 클로즈업 샷(17)은 상시 유지.
2. **실내 카메라 거리 재조정**(검수 관찰) — 5.5m는 방(12m)의 절반도 안 담아 전투 시야가 좁다.
   6.5~8m를 실측해 「실내 비율 90%↑를 지키는 가장 먼 거리」로 잡을 것.
3. `DataLedger` 레코드 단위 검증 → 정리 묶음(GM 원장 다시 읽기, "재빌드 없이" 문구, `TameCritter/TameBoar.DisplayName`
   이중 원장, `MobCatalog.KindCount = 8` vs 14, Assert 문자열 치환 → 파싱/수정/기록, `Assets/Game/Data/.gitkeep` 삭제)
   → 제작법(CraftRecipes) 외부화.

## 교훈(반복 금지)
- `TerrainData`는 에셋이다 — `SaveAssets()` 없이는 디스크의 옛 지형이 돌아온다.
- `Ensure*`는 오브젝트가 있으면 일찍 반환한다 — **이미 만들어진 씬은 코드 수정만으로 안 고쳐진다**. 멱등 보수 패스를 만들 것.
- `[SerializeField]` 기본값 변경은 씬에 저장된 컴포넌트에 반영되지 않는다 — 판정에 쓰는 값은 상수로.
- 씬을 만드는 `Ensure*`의 **호출 순서**를 확인할 것 — 던전 뚜껑 장식이 뚜껑 생성 전에 돌아 한 번 통째로 사라졌다.
- 게이트 임계값은 **고친 상태와 결함 상태를 둘 다 실측해** 그 사이로 잡는다. 새 게이트는 게이트 자체를 네거티브 컨트롤로 검사.
- **씬에 저장된 것은 빌더를 꺼도 남는다** — 네거티브 컨트롤은 빌더 호출을 지우는 게 아니라 **오브젝트를 지워야** 빨간불이 난다.
- 게이트는 존재가 아니라 **화면에서 읽히는 성질**(위치·정렬·부착·보여야 할 것의 하한)을 잰다.
