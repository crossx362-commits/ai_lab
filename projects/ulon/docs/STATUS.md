# 울온 현황

<!-- loop-stamp:start -->
마지막 바퀴: **#27** (성공) · 2026-09-12T19:01:17+0900
커밋: `34c25fcd`, `fed677fc`. 카드 `scene-kind-folders`는 **검증 중**입니다.

- 모델 `grok-4.6` · 경과 771s · 세션 rc `0`
- HEAD `fed677fc [loop#27] 씬 종류 폴더 검증 결과 STATUS·보드 반영` · 브랜치 `master`
- INBOX 미처리 **1**건
- 이 바퀴 커밋:
- `fed677fc [loop#27] 씬 종류 폴더 검증 결과 STATUS·보드 반영`
- `34c25fcd [loop#27] 씬 구역 소품을 Kind* 종류 폴더로 묶음`

## 바퀴 기록

| 바퀴 | 결과 | 시각 | 모델 | 경과 |
|---|---|---|---|---|
| #0 | 성공 | 2026-09-12T12:28:00+0900 | grok-4.6 | 187s |
| #1 | 성공 | 2026-09-12T13:04:00+0900 | grok-4.6 | 362s |
| #2 | 실패 | 2026-09-12T13:37:34+0900 | grok-4.6 | 1236s |
| #4 | 성공 | 2026-09-12T14:05:38+0900 | grok-4.6 | 705s |
| #5 | 성공 | 2026-09-12T14:19:23+0900 | grok-4.6 | 777s |
| #6 | 성공 | 2026-09-12T14:24:02+0900 | grok-4.6 | 231s |
| #7 | 성공 | 2026-09-12T14:29:57+0900 | grok-4.6 | 308s |
| #8 | 성공 | 2026-09-12T14:42:14+0900 | grok-4.6 | 690s |
| #9 | 성공 | 2026-09-12T14:59:24+0900 | grok-4.6 | 982s |
| #10 | 성공 | 2026-09-12T15:10:38+0900 | grok-4.6 | 626s |
| #11 | 성공 | 2026-09-12T15:25:06+0900 | grok-4.6 | 819s |
| #12 | 성공 | 2026-09-12T15:37:01+0900 | grok-4.6 | 666s |
| #13 | 성공 | 2026-09-12T15:49:27+0900 | grok-4.6 | 699s |
| #14 | 성공 | 2026-09-12T16:06:34+0900 | grok-4.6 | 980s |
| #15 | 성공 | 2026-09-12T16:27:03+0900 | grok-4.6 | 1181s |
| #16 | 성공 | 2026-09-12T16:43:52+0900 | grok-4.6 | 961s |
| #17 | 실패 | 2026-09-12T16:44:40+0900 | gpt | 1s |
| #18 | 실패 | 2026-09-12T16:45:29+0900 | gpt | 1s |
| #19 | 성공 | 2026-09-12T16:57:08+0900 | grok-4.6 | 651s |
| #20 | 성공 | 2026-09-12T17:07:56+0900 | grok-4.6 | 600s |
| #21 | 성공 | 2026-09-12T17:23:03+0900 | grok-4.6 | 859s |
| #22 | 성공 | 2026-09-12T17:35:01+0900 | grok-4.6 | 670s |
| #23 | 성공 | 2026-09-12T17:52:56+0900 | grok-4.6 | 1027s |
| #24 | 성공 | 2026-09-12T18:09:16+0900 | grok-4.6 | 932s |
| #25 | 성공 | 2026-09-12T18:19:40+0900 | grok-4.6 | 577s |
| #26 | 성공 | 2026-09-12T18:47:38+0900 | grok-4.6 | 1630s |
| #27 | 성공 | 2026-09-12T19:01:17+0900 | grok-4.6 | 771s |
<!-- loop-stamp:end -->

## 아트 방식

**저폴리 3D 직접 사용** + 고정 3/4 쿼터뷰(줌만, 회전 없음).

근거: `docs/GAME_DESIGN.md` 머리말·§1·§8.1, `docs/DESIGN.md` 「그래픽·카메라」. v0.2의 2D/LPC는 이력만.  
적용 에셋: KayKit/Kenney/OpenGameArt **3D 메시**를 `_ThirdParty/.../RAW`에서 읽어 `VisualSliceBuilder`가 씬에 조립. 2D 스프라이트 게임이 아니다.

규격(기획서): 1 unit = 1 m, 인간 키 약 1.7~1.9 m (`BOOTSTRAP.md` 기록 1.8 m). 텍스처 128~256 px. 이동 속도(m/s)는 기획서에 **없음** → 원작 기준 합격은 해당 수치를 보강하기 전 부여하지 않는다.

## 시스템 상태

판정: **동작함** = 이 바퀴에서 실행해 눈으로/로그로 확인. **원작 기준 합격**은 동작함 + 기획서 원작 규격 대조까지. 코드가 있다는 이유로 동작함을 적지 않는다. `DESIGN_COVERAGE.md`의 ①은 2026-09-11 문서 판정이며 이번 실행 증거가 아니다.

| 시스템 | 상태 | 이번 근거 |
|---|---|---|
| 카메라 고정 3/4 쿼터뷰 | 동작함 | loop#7: 에디터 플레이 샷 `unity/Captures/loop7_play_minimap.png` — 고정 3/4, 줌만. 회전 없음 |
| 스킬 100/총합 700·↑↓Lock | 부분 | HUD·서버 코드. 플레이 미실행 |
| 숙련 칭호 | 부분 | `TitleOf`. 복합 직업명(마검사 등)은 **없음** |
| STR/DEX/INT·Stat Lock | 부분 | 코드. 플레이 미실행 |
| 클릭 이동 | 부분 | loop#22: 호스트 NT `_clientAuthoritative=0`. 목적지 RPC·섬 밖 거절. 광장 −1.2→+6.8m 보행. 샷 `unity/Captures/loop22_move_before.png`·`loop22_move_after.png`. 게이트 `AssertMoveAuthority`+NC. WASD 원격 클라는 모터 꺼짐(기획 보류). 2클라 미실행 |
| 타깃 RPG 전투(서버 판정) | 부분 | loop#8: 스켈레톤에서 타격 VFX 재생 확인. FishNet 클라 미기동이라 `RpcRequestAttack`은 skip·HP 30 유지. 서버 판정 자체는 이번 미실행 |
| 스킬/마법 퀵바 | 부분 | IMGUI 버튼. 단축키·사용성 미검증 |
| 장비·인벤·내구도·수리 | 부분 | 코드·게이트. 플레이 미실행 |
| 채집·제작·Maker Mark | 부분 | 제작법·스테이션 코드. 플레이 미실행 |
| 공급 비율 45/25/20/10 | 부분 | 드랍·제작은 있음. 비율 계측 **없음** |
| 안전 거래 | 부분 | loop#24: 골드 Offer+정산·중복 `duplicate`. 게이트 `AssertSecureTrade`+NC. 호스트 플레이 50→40(+철검)·40→35. 샷 `unity/Captures/loop24_trade_panel.png`(근처 창 「골드 5」). 2클라 미실행. 원작 합격 아님 |
| NPC 상점·은행·훈련 | 부분 | 스테이션 코드·QA 샷. 플레이 미실행 |
| 플레이어 벤더·하우징 | 부분 | 코드 있음(기획상 Phase 2인데 앞섬). 플레이 미실행 |
| 월드(마을1·필드·광산·던전3·테스트) | 부분 | loop#7 플레이: Terrain 600×600m, hres=1025. 미니맵·세계지도(M) 표시, 라벨 600m. 원작 절반(~3072m)은 아님 |
| 몬스터 | 부분 | loop#11: 원장 **20종**(사냥 8+추가 6+보스 4+조련 2). 샷 `unity/Captures/loop11_cutthroat_seer.png`·`loop11_skelmage_squire.png`·`loop11_bonekin_runt.png`. 사냥터 8종·게이트 유지. 비인간형 원형(늑대·거미)은 메시 없어 KayKit 색·크기 변형으로 채움. 배치 셀프체크는 에디터 점유라 미실행 |
| 마법·시약·명상·시전 중단 | 부분 | `RpcCast`가 성공 시 `RpcPlayEffect` 방송(소스 게이트). HUD 시전 버튼은 이번 플레이에서 안 누름 |
| 죽음→유령→부활→시체 회수 | 동작함 | loop#21 플레이어 시체 유지. loop#23: 보스 드랍은 시체·기여자/파티 창(§18.11). 호스트 플레이 헥사크 처치 때 가방에 봉인 없음·시체 items=1 vis, 룻 후 가방 「헥사크의 봉인」. 샷 `unity/Captures/loop23_hexarch_corpse.png`·`loop23_hexarch_loot_bag.png`. 게이트 `AssertBossLoot`+NC·던전 4보스 계약. 독/펫 기여자·2클라는 미실행. 원작 합격 아님 |
| 무게·과적·STR 요구 | 부분 | 코드. 중첩 컨테이너는 주머니 1단 |
| Fame/Karma·가드존·범죄 | 부분 | 코드. Open PvP 플레이 미실행 |
| 파티·길드·길드전·결투 | 부분 | HUD 패널 코드. 플레이 미실행 |
| 조련·마구간·Follower | 부분 | Phase 2가 앞섬. 플레이 미실행 |
| Moongate·Mark/Recall | 부분 | 코드. 플레이 미실행 |
| FishNet 호스트/클라 | 부분 | loop#22: 호스트 StartHost 후 NetPlayer Clone owner/srv/cli, NT 서버 권위. 전용 서버 빌드·외부 접속·2클라 **미확인** |
| PostgreSQL 영구 저장 | 동작함 | loop#6 /ready 200. loop#10: persist `saved_at` 왕복(운영 PG PUT/GET `loop10-latest-wins`). CharacterStore.Load가 JSON이 더 최신이면 골드99·iron_sword·검술40을 고르고 persist에 밀어 넣음. NC: 2000년 JSON(wood)은 못 덮음. 에디터 execute_code `OK gold=99 inv=iron_sword skill=40 savedAt=2099-01-01T00:00:00Z`. SQLite `test_persist_atomicity` 6/6. 클라 재접속 왕복은 **미실행**(UDP 7770 닫힘·클라 바이너리 없음) |
| 관심 영역(Interest Management) | 부분 | loop#20: `interest.json` 18m + FishNet `ObserverManager`/`DistanceCondition`. 호스트 플레이: 광장에서 몹 렌더러 0/21, 사냥터 옆에서 Skeleton 1.2m vis. 샷 `unity/Captures/loop20_interest_hunt_near.png`·`loop20_interest_plaza_far.png`. 게이트 `AssertInterest`+NC. 2클라 서로 보임은 바이너리 없어 **미실행**. 기획서에 미터값 없음 → 원작 합격 아님 |
| LOD·거리 비활성 | 없음 | 기획 §8.1 |
| UI 팩(Paperdoll 그림·DnD·우클릭) | 부분 | loop#14 우클릭 + loop#16 대상 지정 모드. 샷 `unity/Captures/loop16_target_heal_mark.png`(노란 십자·안내)·`loop16_target_spell.png`·`loop16_target_gather.png`·`loop16_target_interact.png`·`loop16_target_action_gather.png`(행동 탭 「채집」). 게이트 `AssertTargetCursor`+NC. Kenney 커서 스킨·외형 렌더·한글 폰트는 **없음** |
| VFX | 동작함 | loop#8 타격 불티 + loop#9 등불·횃불·분수 루프 파티클. 샷 `unity/Captures/loop9_fountain_spray.png`에 주황 불티. 분수 물줄기는 약함 |
| SFX | 동작함 | 같은 플레이에서 `ActionSfx.Played` 0→3. 공간 음원 3D. 귀로 들은 것은 MCP라 미확인(카운트만) |
| GM 도구 | 부분 | loop#19: 무인증 `GmGive` `fail=unauthorized`. 원장 계정 지급 성공. 에디터 바이패스 지급 성공. 게이트 `AssertGmAuth`+NC OK. 샷 `unity/Captures/loop19_gm_panel.png`(F1 패널·광장복구·지급 버튼). 전용 서버+미등록 클라 2클라 실측은 바이너리 없어 **미실행** |
| 백업/복구 | 부분 | loop#25: persist 스냅샷에 캐릭터·집·마구간. SQLite 11/11·PG 격리스키마 11/11 왕복·롤백·404 NC. 운영 POST `/backup` `db_20260912_091727` characters=19 stables=2 houses=0. GM `RpcGmRestore` 배선. 운영 DB 복원은 안 돌림(전량 덮음). Unity MCP 세션 없어 HUD 샷·게이트 플레이 없음. 원작 합격 아님 |
| 봇/부하 테스트 | 없음 | 기획 §14.1 |
| 알파 준비 판정 스크립트 | 동작함 | 이번 `python3 tools/test_alpha_readiness.py` → **OK** (1 test). 운영 서비스 기동은 아님 |

## 진행 중이던 작업 (최근 커밋)

게임 본편(화면)은 `loop-claude` 계열에서 물·언덕·샷을 닫고 master에 들어온 상태. 이 트리에서 그 다음:

- `801a60f0` 저장 원자성·준비 판정 (2026-09-11)
- `8a7ad1b5`·`dcba16e8` 시체 루팅 우선창 후 공개, 부활 뒤 회수 게이트
- `32f7cc4b` 이후 자율 루프(Grok 전용) 인프라

핸드오프(`docs/SESSION_HANDOFF.md`) 화면 쪽 **아직 연 것**: 물가 톱니(깊이 버퍼 계단), `_FoamLit` 원장 미연결, 강 곡류, `65` 거품 위 검은 얼룩, `63` 부두 자 없음. 처방은 검수 판정 대기인 항목이 있다.

## 발견한 문제

- **클라이언트 바이너리 없음.** 이 트리 `builds/client/`에 `UlonClient.app`이 없다. 실행·2클라 검사를 이번 바퀴에서 못 함.
- **persist/postgres는 이번 살아 있음.** loop#10에서 persist.py 반영 후 `start_persist.sh`로 재기동, pid 2822, 5432·8777 청취, `/ready` 200 driver=postgres.
- **셀프체크·QA샷 이번 미실행.** 마지막 `unity/Logs/selfcheck_dev.log`는 2026-09-11(배치 종료 `Exiting batchmode successfully` — 게이트 합격 문구는 로그에서 못 찾음). `two_client` 마지막은 2026-09-03.
- **울온 Unity 에디터 점유.** PID 85035가 `projects/ulon/unity`를 잠금. 배치 셀프체크·클라 빌드는 불가. HTTP MCP(127.0.0.1:8080, `unity@43694413dbe9b6f3`)는 이번 살아 있음 — `EnsureHouseRoofs`·플레이 샷에 씀. 배치 검사는 에디터를 닫은 뒤에.
- **이번 플레이 FishNet 클라 미기동.** `playmode_transition`이 길게 남음. `NetAvatar.IsClientInitialized=false`라 공격 RPC는 skip. VFX는 로컬 `Play`로 확인.
- **기획서–코드 불일치(문서):** 하우징·조련·길드/PvP가 MVP 후순위인데 코드가 앞섬(`DESIGN_COVERAGE`). 몬스터 원장 20종은 채웠으나 §10.1 사족·비행 원형은 메시 없음. 방어구 세트 얇음. UI가 원작 검프가 아님.
- **워크트리 분기:** `/Users/junholee/ai_lab-loop` (`loop-claude`, HEAD `2462cead`)는 이 트리보다 **뒤**다(물 깊이색 커밋에서 멈춤). 이 트리가 persist·루프·루팅을 더 갖고 있다. 반대로 **UlonClient.app은 그 워크트리에만** 있다. 병합·리베이스·그 트리 수정은 하지 않음.

## 완료한 것 (이 바퀴)

- `village-house-corner`: INBOX 18:47의 풀 수 있는 구멍. Kenney `wall-corner`(L자, 실측 −X/+Z, yaw 90/180/270/0 = SW/SE/NE/NW). 직선 창 벽은 유지하고 모서리에만 L자를 얹음 — 벽을 L자로 바꾸면 2×2 2층이 창 없는 탑이 됨(샷 초안). `EnsureHouseCorners` 멱등. 게이트 `AssertHouseCorners`+NC(끄면 FAIL). 집 7채 · L자 29 · 직선 벽 72. 호스트 플레이 샷 `unity/Captures/loop28_house_corners.png`·`loop28_house_corner_close.png`. 콘솔 Metal memoryless 깊이 경고 2줄. `python3 tools/test_alpha_readiness.py` OK. 배치 셀프체크는 에디터 점유라 미실행. 문 칸(1층 남쪽 x=1) L자는 문을 덮어 건너뜀.
- 원작 대비: 같은 점 — 마을 집이 네 모서리가 막힌 상자. 나은 점 — 모듈 킷 모서리 조각. 부족한 점 — 문 칸 바깥 직각은 아직 직선+문, 배치 셀프체크 미실행. 원작 합격 아님(모듈 조립은 원작 규격이 아님).
- INBOX 18:47 사람 결정(에셋 수령·절반맵·관심영역 미터·폰트·Quaternius)은 그대로 막힘.

## 지금 하는 것

없음. loop#28 카드 `village-house-corner`는 검증 중(배치 셀프체크).

## 다음 할 것 (우선순위 → board.json)

1. 이 트리에서 `UlonClient.app` 재빌드 (에디터 점유 해제 후)
2. `tools/two_client_check.sh` — 관심 영역·GM 무인증·시체 동기화 재실측
3. persist 복구: Unity 게이트 플레이 + 스테이징 복원 후 재접속
4. 원작 지도 절반·동화풍 그래픽·월드맵은 Codex 차선
5. 그래픽·UI 폴리시·물가 셰이더는 GPT/Codex 차선 (그록 안 함)
6. 씬 루트 배우·시설 종류 묶음은 페이드 자가 루트를 보도록 고친 뒤
7. 문 칸 모서리 기둥은 사람/다음 바퀴 — 문을 가리지 않는 조각이 필요

## 막힌 것 (사람 결정)

- **그래픽·UI 개선 = GPT 차선 (2026-09-12 16:38):** 셰이더·머티리얼·HUD 외형·아이콘·페이퍼돌 그림·커서 스킨·VFX 폴리시는 그록 루프가 새로 열지 않음. 서버·저장·2클라·관심영역·클라 빌드는 계속. `water-shore-jag`·이후 UI 폴리시는 GPT.

- **에셋 다운로드:** `ASSET_WANTLIST.md`의 모루·대장간 건물·마구간 건물·목공소·갱도 입구·Quaternius MegaKit. 받기는 오너 직행. 조사만 되어 있음.
- **Quaternius Universal Base / Modular Outfits:** 기획 1차 필수인데 `_ThirdParty/Quaternius/`는 빈 폴더. 반입 여부 사람 결정.
- **Noto Sans KR:** 기획서 SIL OFL 1.1로 적혀 있으나 폰트 파일 없음. IMGUI 기본 글꼴 사용 중.
- **Kenney Retro Fantasy Kit / Kenney UI Pack:** 레지스터·기획에만 있고 파일 없음.
- **이동 속도 등 체감 수치:** 기획서 없음 → 「기획서 보강 필요」. 출처 없는 수치로 구현하지 않음.
- **ai_lab-loop 워크트리:** 앞선 작업이 아니라 뒤처짐. 그곳 빌드만 쓰지 말고 이 트리에서 다시 빌드할 것. 병합 금지.
- **물 2·3단계(프레넬·정점 흔들림), 강 곡류:** 핸드오프상 검수 판정 뒤.
- **에디터 점유:** PID 85035가 잠금. 배치 빌드/셀프체크는 불가. HTTP MCP로는 플레이 가능(loop#7 사용). 배치 검사는 에디터를 닫은 뒤에.
- **관심 영역 미터값 기획서 보강 필요:** §7.3은 「주변 일정 거리」만. 구현은 ServUO 기본 18타일. 기획서가 18(또는 다른 값)을 확정하기 전 원작 합격 없음.
- **원작 울온 절반 크기:** map0 6144×4096 타일(Stratics, 1타일≈1m → 절반 ≈3072m). 출처 https://community.stratics.com/threads/land-in-uo.221830/ . Unity Terrain 하이트맵 최대 4097. 지금 셀(300/512 m)이면 LandScale 최대 8(2400m). 3072m는 청크·더 큰 셀 중 사람 선택이 필요.
- **INBOX vs 기획 §6.1:** 기획은 「작은 하나의 살아 있는 월드」. 오너 INBOX(절반 크기)를 우선하되, 한 지형으로 3072m는 엔진 상한과 충돌.
- **INBOX 16:04 병렬 vs PROMPT 한 작업:** INBOX 우선. #15는 Unity와 안 겹치는 보고서만. 남은 대기 카드(클라 빌드·2클라·물가·관심영역·절반맵)는 전부 에디터/클라라 에디터 점유 중엔 나란히 못 연다.
- **그록/그록봇 공존:** 이 트리는 `projects/ulon`만. `ai_lab-loop`(loop-claude) 병합·수정 없음.
