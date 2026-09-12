# 울온 현황

<!-- loop-stamp:start -->
마지막 바퀴: **#12** (성공) · 2026-09-12T15:37:01+0900
커밋: `69bbcdf9` 구현, `92d5cc3f` STATUS·보드. 다음 우선은 에디터 닫은 뒤 `UlonClient.app` 재빌드, 그다음 `ui-container-dnd`.

- 모델 `grok-4.6` · 경과 666s · 세션 rc `0`
- HEAD `92d5cc3f [loop#12] ui-paperdoll 검증 결과 STATUS·보드 반영` · 브랜치 `master`
- INBOX 미처리 **0**건
- 이 바퀴 커밋:
- `92d5cc3f [loop#12] ui-paperdoll 검증 결과 STATUS·보드 반영`
- `69bbcdf9 [loop#12] 가방 패널에 UO식 장비 인형 10칸`

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
| 클릭 이동 | 부분 | `ClickMotor`. WASD는 기획 보류·**없음** |
| 타깃 RPG 전투(서버 판정) | 부분 | loop#8: 스켈레톤에서 타격 VFX 재생 확인. FishNet 클라 미기동이라 `RpcRequestAttack`은 skip·HP 30 유지. 서버 판정 자체는 이번 미실행 |
| 스킬/마법 퀵바 | 부분 | IMGUI 버튼. 단축키·사용성 미검증 |
| 장비·인벤·내구도·수리 | 부분 | 코드·게이트. 플레이 미실행 |
| 채집·제작·Maker Mark | 부분 | 제작법·스테이션 코드. 플레이 미실행 |
| 공급 비율 45/25/20/10 | 부분 | 드랍·제작은 있음. 비율 계측 **없음** |
| 안전 거래 | 부분 | `TradeView`. 2클라 이번 미실행 |
| NPC 상점·은행·훈련 | 부분 | 스테이션 코드·QA 샷. 플레이 미실행 |
| 플레이어 벤더·하우징 | 부분 | 코드 있음(기획상 Phase 2인데 앞섬). 플레이 미실행 |
| 월드(마을1·필드·광산·던전3·테스트) | 부분 | loop#7 플레이: Terrain 600×600m, hres=1025. 미니맵·세계지도(M) 표시, 라벨 600m. 원작 절반(~3072m)은 아님 |
| 몬스터 | 부분 | loop#11: 원장 **20종**(사냥 8+추가 6+보스 4+조련 2). 샷 `unity/Captures/loop11_cutthroat_seer.png`·`loop11_skelmage_squire.png`·`loop11_bonekin_runt.png`. 사냥터 8종·게이트 유지. 비인간형 원형(늑대·거미)은 메시 없어 KayKit 색·크기 변형으로 채움. 배치 셀프체크는 에디터 점유라 미실행 |
| 마법·시약·명상·시전 중단 | 부분 | `RpcCast`가 성공 시 `RpcPlayEffect` 방송(소스 게이트). HUD 시전 버튼은 이번 플레이에서 안 누름 |
| 죽음→유령→부활→시체 회수 | 부분 | 루팅 우선창 커밋 `8a7ad1b5`/`dcba16e8`. 플레이 미실행 |
| 무게·과적·STR 요구 | 부분 | 코드. 중첩 컨테이너는 주머니 1단 |
| Fame/Karma·가드존·범죄 | 부분 | 코드. Open PvP 플레이 미실행 |
| 파티·길드·길드전·결투 | 부분 | HUD 패널 코드. 플레이 미실행 |
| 조련·마구간·Follower | 부분 | Phase 2가 앞섬. 플레이 미실행 |
| Moongate·Mark/Recall | 부분 | 코드. 플레이 미실행 |
| FishNet 호스트/클라 | 부분 | `AutoStartNetwork`. 전용 서버 빌드·외부 접속 **미확인** |
| PostgreSQL 영구 저장 | 동작함 | loop#6 /ready 200. loop#10: persist `saved_at` 왕복(운영 PG PUT/GET `loop10-latest-wins`). CharacterStore.Load가 JSON이 더 최신이면 골드99·iron_sword·검술40을 고르고 persist에 밀어 넣음. NC: 2000년 JSON(wood)은 못 덮음. 에디터 execute_code `OK gold=99 inv=iron_sword skill=40 savedAt=2099-01-01T00:00:00Z`. SQLite `test_persist_atomicity` 6/6. 클라 재접속 왕복은 **미실행**(UDP 7770 닫힘·클라 바이너리 없음) |
| 관심 영역(Interest Management) | 없음 | 기획 §7.3. 코드 없음 |
| LOD·거리 비활성 | 없음 | 기획 §8.1 |
| UI 팩(Paperdoll 그림·DnD·우클릭) | 부분 | loop#13: 가방·은행 칸 + 한 칸 입출금. 샷 `unity/Captures/loop13_bag_grid.png`(가방 주머니·천·철검, 은행 4칸), `loop13_bank_after_deposit.png`(천이 은행으로). 게이트 `AssertContainerDnD`+NC. 아이콘·외형 렌더·우클릭·Kenney UI는 **없음**. 마우스 드래그 제스처는 MCP에서 미실행(서버 `TryDepositOne`은 플레이에서 적용) |
| VFX | 동작함 | loop#8 타격 불티 + loop#9 등불·횃불·분수 루프 파티클. 샷 `unity/Captures/loop9_fountain_spray.png`에 주황 불티. 분수 물줄기는 약함 |
| SFX | 동작함 | 같은 플레이에서 `ActionSfx.Played` 0→3. 공간 음원 3D. 귀로 들은 것은 MCP라 미확인(카운트만) |
| GM 도구 | 부분 | GM 패널 코드. 실행 없음 |
| 백업/복구 | 부분 | 백업 버튼·`data/backups/`. 화면 복구 경로 없음. E2E 미실행 |
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
- **울온 Unity 에디터 점유.** PID 85035가 `projects/ulon/unity`를 잠금. 배치 셀프체크·클라 빌드는 불가. HTTP MCP(127.0.0.1:8080, `unity@43694413dbe9b6f3`)로 플레이·샷. Grok stdio MCP는 Start Session 없음.
- **이번 플레이 FishNet 클라 미기동.** `playmode_transition`이 길게 남음. `NetAvatar.IsClientInitialized=false`라 공격 RPC는 skip. VFX는 로컬 `Play`로 확인.
- **기획서–코드 불일치(문서):** 하우징·조련·길드/PvP가 MVP 후순위인데 코드가 앞섬(`DESIGN_COVERAGE`). 몬스터 원장 20종은 채웠으나 §10.1 사족·비행 원형은 메시 없음. 방어구 세트 얇음. UI가 원작 검프가 아님.
- **워크트리 분기:** `/Users/junholee/ai_lab-loop` (`loop-claude`, HEAD `2462cead`)는 이 트리보다 **뒤**다(물 깊이색 커밋에서 멈춤). 이 트리가 persist·루프·루팅을 더 갖고 있다. 반대로 **UlonClient.app은 그 워크트리에만** 있다. 병합·리베이스·그 트리 수정은 하지 않음.

## 완료한 것 (이 바퀴)

- `ui-container-dnd`: 가방·은행 IMGUI 칸, 끌어다 은행/주머니/가방. 서버 `TryDepositOne`/`TryWithdrawOne`(사거리·유령·과적, 주머니는 내용물 묶음). 버튼 맡기기/찾기는 같은 경로. 게이트+NC 에디터 실행 OK. 플레이 샷 2장에서 천이 가방→은행. 배치 셀프체크·클라 빌드는 에디터 점유라 못 돌림.
- 원작 대비: 같은 점 — 가방과 은행을 열어 물건을 옮김, 주머니 1단. 나은 점 — 3D 월드가 뒤에 보임. 부족한 점 — 아이콘·자유 배치·바닥에 버리기·우클릭 없음. 카드(컨테이너 DnD)는 통과, UI 시스템 원작 합격은 아님.

## 지금 하는 것

없음. loop#13 카드 `ui-container-dnd` 닫음.

## 다음 할 것 (우선순위 → board.json)

1. 이 트리에서 `UlonClient.app` 재빌드 (에디터 점유 해제 후)
2. 원작 지도 절반(~3072m) 사람 선택 (`uo-half-span`)
3. `tools/two_client_check.sh`

## 막힌 것 (사람 결정)

- **에셋 다운로드:** `ASSET_WANTLIST.md`의 모루·대장간 건물·마구간 건물·목공소·갱도 입구·Quaternius MegaKit. 받기는 오너 직행. 조사만 되어 있음.
- **Quaternius Universal Base / Modular Outfits:** 기획 1차 필수인데 `_ThirdParty/Quaternius/`는 빈 폴더. 반입 여부 사람 결정.
- **Noto Sans KR:** 기획서 SIL OFL 1.1로 적혀 있으나 폰트 파일 없음. IMGUI 기본 글꼴 사용 중.
- **Kenney Retro Fantasy Kit / Kenney UI Pack:** 레지스터·기획에만 있고 파일 없음.
- **이동 속도 등 체감 수치:** 기획서 없음 → 「기획서 보강 필요」. 출처 없는 수치로 구현하지 않음.
- **ai_lab-loop 워크트리:** 앞선 작업이 아니라 뒤처짐. 그곳 빌드만 쓰지 말고 이 트리에서 다시 빌드할 것. 병합 금지.
- **물 2·3단계(프레넬·정점 흔들림), 강 곡류:** 핸드오프상 검수 판정 뒤.
- **에디터 점유:** PID 85035가 잠금. 배치 빌드/셀프체크는 불가. HTTP MCP로는 플레이 가능(loop#7 사용). 배치 검사는 에디터를 닫은 뒤에.
- **원작 울온 절반 크기:** map0 6144×4096 타일(Stratics, 1타일≈1m → 절반 ≈3072m). 출처 https://community.stratics.com/threads/land-in-uo.221830/ . Unity Terrain 하이트맵 최대 4097. 지금 셀(300/512 m)이면 LandScale 최대 8(2400m). 3072m는 청크·더 큰 셀 중 사람 선택이 필요.
- **INBOX vs 기획 §6.1:** 기획은 「작은 하나의 살아 있는 월드」. 오너 INBOX(절반 크기)를 우선하되, 한 지형으로 3072m는 엔진 상한과 충돌.
