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
85f66512, 5b955804, ae7c70c3, bada4df6, 221c72d0, 6b6fbcde, 737bd088, 37c9fbcb,
9f1557ba(입구 문틀), 6e9f19f3, 808155b6, eb0961ce(흰 바위 잔재),
b074dc52(던전 지하화 + 게이트 2종), cf8404c5(페이드가 바닥을 지우던 버그·방 안 잔디),
34033723(방 가장자리 하늘 비침·실내 알베도).

## 방금 끝난 것 — 던전 지하화(검수 P0)
- 방 바닥 = 지면 −`VisualSliceBuilder.DungeonDepth`(4.5m), 벽은 지면까지. 몹·보스·출구·등불·잔해는 `SinkIntoDungeon`으로 함께 내림.
- 천장 한 장 → 6×6 `DungeonCap` 타일 + `PunchTerrainHole`(여백 2.5m)로 Terrain에 구멍. 페이드가 작은 창만 연다.
- 게이트 2종(`Editor/SliceSelfCheck.Interior.cs`):
  - **화면 채움**(§8.2): 플레이 카메라 시야 21×21 샘플 레이 중 잔디 비율 상한 0.45. 실측 0.07/0.13/0.23,
    네거티브 컨트롤(뚜껑을 방 크기로 축소) 0.60 → FAIL. 하늘은 계측만(3/4 시점에선 늘 조금 들어온다).
  - **실내 조명**(§8.2, `AssertDungeonLighting`): 주광이 방 바닥에 직접 못 닿음(암반 뚜껑 그림자) + 지면 아래 등불 점광 3개↑.
    네거티브 컨트롤(받침 광원 `RoomFill` 제거) 2개 → FAIL.
- 버그 2건 수리: 페이드가 대상보다 아래 렌더러(바닥)까지 지워 하늘이 비침 → `DungeonSightFade.Hide`에서 `bounds.max.y < look.y - 0.2` 제외.
  바닥 판이 Terrain 홀보다 좁아 가장자리로 하늘이 보임 → `span + 8`.
- 화면 근거: `builds/qa/08_d1_interior_playcam.png`(암반 단면 아래 등불로 밝힌 석실, 보스·잡몹 식별됨).

## 다음 할 일 (검수 확정 순서 — 새 지시가 오면 그게 맨 앞)
1. 검수에 지하화 완료 보고 후 **다음 과제를 검수에 질문**(오너 지시).
2. 온라인 서버권한 2건(`NetAvatar` SyncVar 노출 / `RpcTame`·`RpcPet` 부재) — 검수 검증 후 순위 재조정 중.
3. 테스트 공간(§6.1) 구현 + 문서의 "MVP 콘텐츠 상한 충족" 문구를 미구현으로 정정.
4. `DataLedger` 레코드 단위 검증(아이템 weight>0·buy≥0·uses≥0·strReq≥0 / 몹 hp>0·height>0·name 비어있지 않음·dmgMin≥0·dmgMax≥dmgMin),
   불량 레코드는 코드 폴백 + `Debug.LogError` + `loadError` 누적, 필드 없는 레코드로 코드 기본값 확인 Assert. **검수가 먼저 시작하지 말라고 함.**
5. GM "원장 다시 읽기" 배선, "재빌드 없이" 문구를 "(서버 재시작 또는 GM 리로드)"로 정정, 스택 결합 경고 문서,
   `TameCritter/TameBoar.DisplayName` 이중 원장, `MobCatalog.KindCount = 8` vs 14 모순, 클라 변조면 주석,
   Assert의 문자열 치환을 파싱/수정/기록으로 교체, `Assets/Game/Data/.gitkeep` 삭제.
6. 제작법(CraftRecipes) 외부화.
7. 오너의 산·바다·강·호수 지형 작업 — 검수가 오너 확인 중이라 **대기**. 착수 전 §11 리소스 정책·§8.1 폴리 예산 확인.
