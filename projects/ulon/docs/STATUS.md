# 울온 현황

<!-- loop-stamp:start -->
마지막 바퀴: **#54** (성공) · 2026-09-13T00:14:37+0900
커밋: `b9200efc` 구현, `acd1168a` STATUS. 원작 합격은 아닙니다.

- 모델 `grok-4.6` · 경과 678s · 세션 rc `0`
- HEAD `acd1168a [loop#54] 방패 막기 판정 STATUS` · 브랜치 `master`
- INBOX 미처리 **0**건
- 이 바퀴 커밋:
- `acd1168a [loop#54] 방패 막기 판정 STATUS`
- `b9200efc [loop#54] 방패 막기 판정을 공격에 연결`

## 바퀴 기록

| 바퀴 | 결과 | 시각 | 모델 | 경과 |
|---|---|---|---|---|
| #23 | 성공 | 2026-09-12T17:52:56+0900 | grok-4.6 | 1027s |
| #24 | 성공 | 2026-09-12T18:09:16+0900 | grok-4.6 | 932s |
| #25 | 성공 | 2026-09-12T18:19:40+0900 | grok-4.6 | 577s |
| #26 | 성공 | 2026-09-12T18:47:38+0900 | grok-4.6 | 1630s |
| #27 | 성공 | 2026-09-12T19:01:17+0900 | grok-4.6 | 771s |
| #28 | 성공 | 2026-09-12T19:18:58+0900 | grok-4.6 | 1014s |
| #29 | 성공 | 2026-09-12T19:27:41+0900 | grok-4.6 | 476s |
| #30 | 성공 | 2026-09-12T19:47:38+0900 | grok-4.6 | 1149s |
| #31 | 실패 | 2026-09-12T19:48:27+0900 | gpt | 1s |
| #32 | 실패 | 2026-09-12T19:49:15+0900 | gpt | 1s |
| #34 | 성공 | 2026-09-12T20:00:10+0900 | grok-4.6 | 593s |
| #35 | 성공 | 2026-09-12T20:12:59+0900 | grok-4.6 | 722s |
| #36 | 성공 | 2026-09-12T20:28:33+0900 | grok-4.6 | 886s |
| #37 | 성공 | 2026-09-12T20:40:48+0900 | grok-4.6 | 688s |
| #38 | 성공 | 2026-09-12T20:51:20+0900 | grok-4.6 | 584s |
| #39 | 성공 | 2026-09-12T21:07:21+0900 | grok-4.6 | 914s |
| #40 | 성공 | 2026-09-12T21:19:18+0900 | grok-4.6 | 670s |
| #41 | 성공 | 2026-09-12T21:31:03+0900 | grok-4.6 | 658s |
| #42 | 성공 | 2026-09-12T21:40:24+0900 | grok-4.6 | 514s |
| #43 | 성공 | 2026-09-12T21:52:16+0900 | grok-4.6 | 665s |
| #45 | 성공 | 2026-09-12T22:16:13+0900 | grok-4.6 | 717s |
| #46 | 성공 | 2026-09-12T22:26:11+0900 | grok-4.6 | 551s |
| #47 | 성공 | 2026-09-12T22:39:38+0900 | grok-4.6 | 760s |
| #48 | 성공 | 2026-09-12T22:50:52+0900 | grok-4.6 | 626s |
| #49 | 성공 | 2026-09-12T23:03:25+0900 | grok-4.6 | 706s |
| #50 | 성공 | 2026-09-12T23:13:36+0900 | grok-4.6 | 563s |
| #51 | 성공 | 2026-09-12T23:26:26+0900 | grok-4.6 | 722s |
| #52 | 성공 | 2026-09-12T23:46:37+0900 | grok-4.6 | 1164s |
| #53 | 성공 | 2026-09-13T00:02:32+0900 | grok-4.6 | 907s |
| #54 | 성공 | 2026-09-13T00:14:37+0900 | grok-4.6 | 678s |
<!-- loop-stamp:end -->

## 아트 방식

**저폴리 3D 직접 사용** + 고정 3/4 쿼터뷰(줌만, 회전 없음).

근거: `docs/GAME_DESIGN.md` 머리말·§1·§8.1, `docs/DESIGN.md` 「그래픽·카메라」. v0.2의 2D/LPC는 이력만.  
적용 에셋: KayKit/Kenney/OpenGameArt **3D 메시**를 `_ThirdParty/.../RAW`에서 읽어 `VisualSliceBuilder`가 씬에 조립. 2D 스프라이트 게임이 아니다.

규격(기획서): 1 unit = 1 m, 인간 키 약 1.7~1.9 m (`BOOTSTRAP.md` 기록 1.8 m). 텍스처 128~256 px. 이동 리듬 §7.2.1 걷기 2.5 / 달리기 5.0 m/s (`move_speed.json`). 기마 10.0은 원장만 — 탑승 플레이 없음.

## 시스템 상태

판정: **동작함** = 이 바퀴에서 실행해 눈으로/로그로 확인. **원작 기준 합격**은 동작함 + 기획서 원작 규격 대조까지. 코드가 있다는 이유로 동작함을 적지 않는다. `DESIGN_COVERAGE.md`의 ①은 2026-09-11 문서 판정이며 이번 실행 증거가 아니다.

| 시스템 | 상태 | 이번 근거 |
|---|---|---|
| 카메라 고정 3/4 쿼터뷰 | 동작함 | loop#7: 에디터 플레이 샷 `unity/Captures/loop7_play_minimap.png` — 고정 3/4, 줌만. 회전 없음 |
| 스킬 100/총합 700·↑↓Lock | 동작함 | loop#36: HUD가 `TryCycleSkillLock`/`RpcCycleSkillLock`. 플레이 샷 `unity/Captures/loop36_skill_gump.png`(합계 1.0/700·능력 90/225·검술 ↓)·`loop36_skill_lock.png`(검술 x·STR ↓). 게이트 `AssertSkillLockAuth`+NC 에디터 OK. 오프라인 Fire 검술 ↑→↓→x. 호스트 켠 뒤 Try 채광 Down→Locked. 2클라 서명 왕복은 미실행. 원작 검프 아님 |
| 숙련 칭호 | 동작함 | loop#51: 구간 원장 `skill_titles.json`(30/40/50/60/70/80/90/100). 검프 `GumpLine`·페이퍼돌 `TitleOf`(최고 스킬). 플레이 샷 `unity/Captures/loop51_skill_gump.png`(달인 검술 80·초심자 치유 30·그랜드마스터 마법 100·채광 29.9 무칭호·상태 「그랜드마스터 마검사」)·`loop51_paperdoll.png`(인형 「달인 검사」)·`loop51_swordsman.png`(상태 「달인 검사」). 게이트 `AssertSkillTitleRanks`+NC 에디터 OK. 배치 셀프체크·2클라 미실행. 원작 합격 아님(복합 직업명 유지) |
| STR/DEX/INT·Stat Lock | 동작함 | loop#52: 주 잠금이면 부 스탯(`SecondaryOf`·`TryGainFromSkill`). 플레이 STR x 39 유지·DEX 25→26·검술 0.1→0.2·ST 26/36. 샷 `unity/Captures/loop52_skill_gump.png`(STR 39 x·DEX 26↑·능력 91/225·검술 0.2)·`loop52_str_locked.png`·`loop52_dex_gain.png`. 게이트 `AssertStatSecondaryGain`+NC 에디터 OK. 배치 셀프체크·2클라 미실행. 원작 합격 아님(75/25 없음, 마법저항/조련 Primary는 기존 코드) |
| 클릭 이동 | 동작함 | loop#53: 홀드를 떼면 멈춘다(`ClickHold.StopOnRelease`, 2프레임+). 짧은 클릭(1프레임)은 목적지 유지. 플레이 홀드 해제 x=-6.140 1s 유지 speed 0·짧은 클릭은 계속 2.50. 샷 `unity/Captures/loop53_click_hold_walk.png`·`loop53_click_hold_stop.png`. 게이트 `AssertClickHold`+NC 에디터 OK. 배치 셀프체크·2클라 미실행. 원작 합격 아님(커서 거리=달리기 수치 없음) |
| 타깃 RPG 전투(서버 판정) | 부분 | loop#55: 철 갑옷 물리 AR(`PhysicalArmor`, items.json armor=4). 마법 경로는 AR 안 탐. 플레이 무갑 400→388 dmg 12·철갑 400→392 dmg 8·HUD 「방 4」. 샷 `unity/Captures/loop55_armor_plate-1.png`. 게이트 `AssertPhysicalArmor`+NC 에디터 OK. 배치 셀프체크·2클라 미실행. 원작 Virtual Armor·방패 AR·투구 세트 없음 |
| 스킬/마법 퀵바 | 동작함 | loop#35: 원장 `QuickbarSlots` 1–9. 플레이 샷 `unity/Captures/loop35_quickbar.png`(1 붕대·2 물약·3 명상·4 불씨·5 봉합). `FireQuickbarAt(0)` 후 샷 `loop35_hotkey_bandage.png`(「치유 대상을 지정하세요」). 게이트+NC 에디터 OK. 물리 Alpha1 키는 MCP라 미실행. 커스텀 할당 없음. 원작 합격 아님 |
| 장비·인벤·내구도·수리 | 부분 | 코드·게이트. 플레이 미실행 |
| 채집·제작·Maker Mark | 부분 | 제작법·스테이션 코드. 플레이 미실행 |
| 공급 비율 45/25/20/10 | 부분 | 드랍·제작은 있음. 비율 계측 **없음** |
| 안전 거래 | 부분 | loop#24: 골드 Offer+정산·중복 `duplicate`. 게이트 `AssertSecureTrade`+NC. 호스트 플레이 50→40(+철검)·40→35. 샷 `unity/Captures/loop24_trade_panel.png`(근처 창 「골드 5」). 2클라 미실행. 원작 합격 아님 |
| NPC 상점·은행·훈련 | 부분 | 스테이션 코드·QA 샷. 플레이 미실행 |
| 플레이어 벤더·하우징 | 부분 | 코드 있음(기획상 Phase 2인데 앞섬). 플레이 미실행 |
| 월드(마을1·필드·광산·던전3·테스트) | 부분 | loop#7 플레이: Terrain 600×600m, hres=1025. 미니맵·세계지도(M) 표시, 라벨 600m. 원작 절반(~3072m)은 아님 |
| 몬스터 | 부분 | loop#11: 원장 **20종**(사냥 8+추가 6+보스 4+조련 2). 샷 `unity/Captures/loop11_cutthroat_seer.png`·`loop11_skelmage_squire.png`·`loop11_bonekin_runt.png`. 사냥터 8종·게이트 유지. 비인간형 원형(늑대·거미)은 메시 없어 KayKit 색·크기 변형으로 채움. 배치 셀프체크는 에디터 점유라 미실행 |
| 마법·시약·명상·시전 중단 | 동작함 | loop#39 시전 이동 취소. loop#41: 마나 자연 회복(`ManaRegen`, 명상+INT, 중갑×0.5). 플레이 SetMana(2)→저 MP 5/36 → ~2s 후 10/36. 중갑 철판 후 rate 0.76·샷 MP 중갑 7/36. 게이트 `AssertManaRegen`+NC 에디터 OK. 샷 `unity/Captures/loop41_mana_low.png`·`loop41_mana_regen.png`·`loop41_mana_heavy.png`. 클릭 명상은 숙련용 유지. 2클라 미실행. 원작 합격 아님. 1.0+명상/100+INT/50 은 기획서에 없는 프로젝트 값 |
| 죽음→유령→부활→시체 회수 | 동작함 | loop#21 플레이어 시체 유지. loop#23: 보스 드랍은 시체·기여자/파티 창(§18.11). 호스트 플레이 헥사크 처치 때 가방에 봉인 없음·시체 items=1 vis, 룻 후 가방 「헥사크의 봉인」. 샷 `unity/Captures/loop23_hexarch_corpse.png`·`loop23_hexarch_loot_bag.png`. 게이트 `AssertBossLoot`+NC·던전 4보스 계약. 독/펫 기여자·2클라는 미실행. 원작 합격 아님 |
| 무게·과적·STR 요구 | 동작함 | loop#38: 과적 시 달리기 불가(`CarryMove.CanRun`). 플레이 샷 `unity/Captures/loop38_overweight.png`(무게 180/156 과적·달림불가). LocalAvatar 끄고 SetRunning(true)여도 Running=false·PlanarSpeed=2.50. 가방 비우면 Running=true. 게이트 `AssertOverweightMove`+NC 에디터 OK. 집기/구매 과적 거절은 기존. 걷기 감속 배율은 기획서에 없어 안 넣음(옛 0.35 삭제). 2클라 미실행. 원작 합격 아님 |
| Fame/Karma·가드존·범죄 | 부분 | 코드. Open PvP 플레이 미실행 |
| 파티·길드·길드전·결투 | 부분 | HUD 패널 코드. 플레이 미실행 |
| 조련·마구간·Follower | 부분 | Phase 2가 앞섬. 플레이 미실행 |
| Moongate·Mark/Recall | 부분 | 코드. 플레이 미실행 |
| FishNet 호스트/클라 | 부분 | loop#46: 이 트리 `UlonClient.app` 재빌드(360M). 전용 서버 `-ulon-server` UDP 7770 기동, GUI 클라 `-ulon-client -ulon-host 127.0.0.1 -ulon-account ds-a` 접속(`Remote connection started for Id 0`). 클라/서버 로그 Exception·Error 0. 화면 샷은 OS 캡처 거부. 2클라 `two_client_check`는 다음 |
| PostgreSQL 영구 저장 | 동작함 | loop#6 /ready 200. loop#10: persist `saved_at` 왕복(운영 PG PUT/GET `loop10-latest-wins`). CharacterStore.Load가 JSON이 더 최신이면 골드99·iron_sword·검술40을 고르고 persist에 밀어 넣음. NC: 2000년 JSON(wood)은 못 덮음. 에디터 execute_code `OK gold=99 inv=iron_sword skill=40 savedAt=2099-01-01T00:00:00Z`. SQLite `test_persist_atomicity` 6/6. 클라 재접속 왕복은 **미실행**(loop#46에서 7770을 잠깐 열고 끔) |
| 관심 영역(Interest Management) | 부분 | loop#20: `interest.json` 18m + FishNet `ObserverManager`/`DistanceCondition`. 호스트 플레이: 광장에서 몹 렌더러 0/21, 사냥터 옆에서 Skeleton 1.2m vis. 샷 `unity/Captures/loop20_interest_hunt_near.png`·`loop20_interest_plaza_far.png`. 게이트 `AssertInterest`+NC. 2클라 서로 보임은 **미실행**. 기획서에 미터값 없음 → 원작 합격 아님 |
| LOD·거리 비활성 | 없음 | 기획 §8.1 |
| UI 팩(Paperdoll 그림·DnD·우클릭) | 부분 | loop#14 우클릭 + loop#16 대상 지정 모드. 샷 `unity/Captures/loop16_target_heal_mark.png`(노란 십자·안내)·`loop16_target_spell.png`·`loop16_target_gather.png`·`loop16_target_interact.png`·`loop16_target_action_gather.png`(행동 탭 「채집」). 게이트 `AssertTargetCursor`+NC. Kenney 커서 스킨·외형 렌더·한글 폰트는 **없음** |
| VFX | 동작함 | loop#8 타격 불티 + loop#9 등불·횃불·분수 루프 파티클. 샷 `unity/Captures/loop9_fountain_spray.png`에 주황 불티. 분수 물줄기는 약함 |
| SFX | 동작함 | 같은 플레이에서 `ActionSfx.Played` 0→3. 공간 음원 3D. 귀로 들은 것은 MCP라 미확인(카운트만) |
| GM 도구 | 부분 | loop#19: 무인증 `GmGive` `fail=unauthorized`. 원장 계정 지급 성공. 에디터 바이패스 지급 성공. 게이트 `AssertGmAuth`+NC OK. 샷 `unity/Captures/loop19_gm_panel.png`(F1 패널·광장복구·지급 버튼). 전용 서버+미등록 클라 2클라 실측은 바이너리 없어 **미실행** |
| 백업/복구 | 동작함 | loop#33/#34: 호스트 플레이 GM 패널에 「백업」「복구」. 샷 `unity/Captures/loop33_gm_backup.png`·`loop34_gm_restore.png`(서버 ON·클라 ON·접속 1·복구 버튼·백업 로그 `playloop-verify|-|backup`). GmBackup persist `db_20260912_105601.json` counts characters=19 stables=2 houses=0. SQLite 11/11·PG 격리 11/11. 운영 스냅샷을 임시 스키마에 복원→GET→변조→재복원 일치 후 DROP. NcSkipRestore NC. 운영 POST `/restore` 는 전량 덮음이라 안 돌림. 원작 합격 아님 |
| 봇/부하 테스트 | 없음 | 기획 §14.1 |
| 알파 준비 판정 스크립트 | 동작함 | loop#30: `alpha_ready.judge` 는 파일 ok:true 를 현재로 안 씀. 스모크 실패 시 `alpha_status.json` 삭제. 보드 `/api/state.alpha_ready` live `/ready` ok=true·stale_file=false. 테스트 7/7 + board 4/4. NC: 옛 성공 파일+실측 실패=준비 아님 |

## 진행 중이던 작업 (최근 커밋)

게임 본편(화면)은 `loop-claude` 계열에서 물·언덕·샷을 닫고 master에 들어온 상태. 이 트리에서 그 다음:

- `801a60f0` 저장 원자성·준비 판정 (2026-09-11)
- `8a7ad1b5`·`dcba16e8` 시체 루팅 우선창 후 공개, 부활 뒤 회수 게이트
- `32f7cc4b` 이후 자율 루프(Grok 전용) 인프라

핸드오프(`docs/SESSION_HANDOFF.md`) 화면 쪽 **아직 연 것**: 물가 톱니(깊이 버퍼 계단), `_FoamLit` 원장 미연결, 강 곡류, `65` 거품 위 검은 얼룩, `63` 부두 자 없음. 처방은 검수 판정 대기인 항목이 있다.

## 발견한 문제

- **클라이언트 바이너리 있음 (loop#46).** `builds/client/UlonClient.app` 360M, Mach-O x86_64+arm64, 시각 2026-09-12 22:19. 전용 서버·1클라 접속 확인. 2클라는 아직.
- **persist/postgres는 이번 살아 있음.** pid 84115, 5432·8777 청취, `/ready` 200 driver=postgres.
- **셀프체크·QA샷 이번 미실행.** 마지막 `unity/Logs/selfcheck_dev.log`는 2026-09-11(배치 종료 `Exiting batchmode successfully` — 게이트 합격 문구는 로그에서 못 찾음). `two_client` 마지막은 2026-09-03.
- **울온 Unity 에디터 점유.** PID 85035가 `projects/ulon/unity`를 잠금. 배치 셀프체크는 불가. HTTP MCP로 종류 묶음·페이드 NC·플레이 확인. 배치 검사는 에디터를 닫은 뒤에.
- **이번 플레이.** 에디터 플레이: 콘솔 error/warning 0(깊이 버퍼 memoryless 무시 로그만). 같은 플레이어 무갑 더미 400→388 dmg 12. 철갑 더미 400→392 dmg 8·HUD 「방 4」. 샷 `unity/Captures/loop55_armor_plate-1.png`. 2클라 미실행.
- **기획서–코드 불일치(문서):** 하우징·조련·길드/PvP가 MVP 후순위인데 코드가 앞섬(`DESIGN_COVERAGE`). 몬스터 원장 20종은 채웠으나 §10.1 사족·비행 원형은 메시 없음. 방어구 세트 얇음. UI가 원작 검프가 아님.
- **워크트리 분기:** `/Users/junholee/ai_lab-loop` (`loop-claude`, HEAD `2462cead`)는 이 트리보다 **뒤**다(물 깊이색 커밋에서 멈춤). 이 트리가 persist·루프·루팅을 더 갖고 있다. 이 트리에도 loop#46로 `UlonClient.app`을 다시 넣었다. 병합·리베이스·그 트리 수정은 하지 않음.

## 완료한 것 (이 바퀴)

- 기획 §18.3 물리 방어. 철 갑옷 AR 4가 근접·반격 피해만 깎음. 마법은 `PhysicalArmor`를 안 탐. 게이트 `AssertPhysicalArmor`+NC 에디터 OK. 플레이 무갑 12·철갑 8·HUD 「방 4」. 샷 `unity/Captures/loop55_armor_plate-1.png`. 배치 셀프체크는 에디터 점유라 생략. `test_alpha_readiness` 7/7.
- 원작 대비: 같은 점 — 갑옷이 물리 피해를 줄인다. 나은 점 — HUD에 AR과 「방 4」가 바로 보인다. 부족한 점 — 원작 Virtual Armor 공식·부위 세트 AR은 기획서에 없어 안 넣음. 방패는 막기만(AR 0).

## 지금 하는 것

없음. loop#55 카드는 철 갑옷 물리 방어까지 닫음.

## 다음 할 것 (우선순위 → board.json)

1. `tools/two_client_check.sh` — 관심 영역·GM 무인증·시체 동기화 재실측
2. 운영 persist 복원(전량 덮음)은 사람 확인 뒤
3. 원작 지도 절반·동화풍 그래픽·월드맵은 Codex 차선
4. 그래픽·UI 폴리시·물가 셰이더는 GPT/Codex 차선 (그록 안 함)
5. 문 칸 모서리 기둥은 사람/다음 바퀴 — 문을 가리지 않는 조각이 필요

## 막힌 것 (사람 결정)

- **그래픽·UI 개선 = GPT 차선 (2026-09-12 16:38):** 셰이더·머티리얼·HUD 외형·아이콘·페이퍼돌 그림·커서 스킨·VFX 폴리시는 그록 루프가 새로 열지 않음. 서버·저장·2클라·관심영역·클라 빌드는 계속. `water-shore-jag`·이후 UI 폴리시는 GPT.
- **INBOX vs PROMPT 그래픽 막힘 (2026-09-12 19:41):** PROMPT는 진행 중 그래픽 카드를 막힘으로 넘기라고 함. INBOX는 그록 제외용 막힘을 대기로 되돌리고 담당 카드 상태를 그록이 바꾸지 말 것. HUD만 대기 복구. 이후 그록은 owner=codex/model=gpt 카드를 필터로만 건너뛴다.

- **에셋 다운로드 (일부 해제 2026-09-12):** Kenney Modular Cave Kit(갱도 게이트) · Kenney UI Pack Grey · Noto Sans KR 반입. 남은 것: 모루·대장간/마구간/목공소 건물(Quaternius poly.pizza, 직링크 없음) · Quaternius MegaKit · Kenney Retro Fantasy.
- **Quaternius Universal Base / Modular Outfits:** `_ThirdParty/Quaternius/` 빈 폴더. 사이트/itch 직링크 없음 — 사람 다운로드.
- **Noto Sans KR:** 반입함 (`Resources/Fonts/NotoSansKR-Regular.ttf`, SIL OFL). HUD `EnsureUiFont`. 에디터 임포트·화면 확인은 다음.
- **Kenney UI Pack:** Grey 버튼/패널 42장 반입. HUD 스킨 적용은 GPT UI 차선.
- **이동 속도:** 기획 §7.2.1 적용함(loop#37). 기마 10.0 m/s는 원장만 — 탑승 플레이가 없어 모터에 안 묶음. Always Run 옵션 UI는 GPT HUD 차선.
- **커서 거리 달리기:** 원작은 커서와 캐릭터 거리가 멀면 달린다(uoguide Moving, uo.com Movement and Travel). 걷기 2.5/달리기 5.0은 기획에 있으나 전환 문턱(타일·픽셀)은 없어 안 넣음. Shift 달리기 유지. 「기획서 보강 필요」.
- **과적 걷기 감속:** 원작 UO는 과적 시 걷기도 느려진다(uoguide Weight). 기획서에 배율이 없어 이번엔 달리기 금지와 걷기 2.5 m/s만. 감속·이동불가 문턱은 「기획서 보강 필요」.
- **달리기 스태미나 수치:** Max=10+DEX는 기획 §18.2 「단순 공식」. 소모 4/s·회복 2/s는 기획서에 없어 프로젝트 값(`run_stamina.json`). 원작 피격 스태미나 감소는 안 넣음.
- **마나 자연 회복 수치:** 기획 §18.6은 Meditation이 회복을 담당·중갑 패널티만. 공식 숫자는 없어 `mana_regen.json` 프로젝트 값(1 + 명상/100 + INT/50, 중갑×0.5). 원작 명상 틱 공식은 안 넣음.
- **HP 자연 회복 수치:** 기획 §18.2는 STR→최대 HP·단순 공식만. 자연 회복 숫자는 없어 `hp_regen.json` 프로젝트 값(1 + STR/50). 원작 기본은 약 10초에 1(+아이템 HPR). 기획서 보강 필요.
- **휘두름 간격 수치:** 기획 §18.2는 DEX=공격 속도 보정만. 무기 속도·SSI·틱은 없어 `attack_speed.json` 프로젝트 값(max(0.5, 1.8−스태미나/50), 기본 만땅=옛 1.1s). 원작 하한은 1.25s. 기획서 보강 필요.
- **명중 공식 수치:** 기획 §18.3은 「명중은 무기 스킬」만. 숫자는 없어 Stratics 공식 `(atk+20)/((def+20)*2)` (`hit_chance.json`). HCI/DCI·레슬링 대체·하한 2%는 기획서에 없어 안 넣음. 기획서 보강 필요.
- **방패 막기 수치:** 기획 §18.3은 「방패 막기/방어 판정」만. 숫자는 없어 Classic 방패 공식 방패술×0.3 (`parry_chance.json`, https://uo.stratics.com/content/skills/parrying.shtml). 무기만 막기·Bushido·DEX<80 보정은 기획서에 없어 안 넣음. 기획서 보강 필요.
- **물리 방어 수치:** 기획 §18.3은 「마법은 단순 방어력으로 안 막음」만. 철갑 AR 4는 프로젝트 값(`items.json` armor, `physical_armor.json`). 원작 Virtual Armor·부위 세트는 안 넣음. 방패 AR은 막기와 겹쳐서 0. 기획서 보강 필요.
- **부 스탯 확률:** 원작 uo.com(Publish 45)은 주 75%/부 25%·스킬 상승 시 1/20. 기획 §18.2는 「Primary/Secondary에 Stat Gain 시도」만. 이번엔 주가 안 오를 때만 부. 75/25는 「기획서 보강 필요」.
- **주 스탯 표 충돌:** 기존 `PrimaryOf`를 안 바꿈. 마법저항은 코드 INT / uo.com STR·DEX, 조련은 코드 DEX / uo.com STR·INT. 부는 표의 나머지. 출처 https://uo.com/wiki/ultima-online-wiki/player/stats/skills-stats-and-attributes/
- **ai_lab-loop 워크트리:** 앞선 작업이 아니라 뒤처짐. 그곳 빌드만 쓰지 말고 이 트리에서 다시 빌드할 것. 병합 금지.
- **물 2·3단계(프레넬·정점 흔들림), 강 곡류:** 핸드오프상 검수 판정 뒤.
- **에디터 점유:** PID 85035가 잠금. 배치 셀프체크는 불가. 클라 빌드는 loop#46에서 HTTP MCP로 완료. 배치 검사는 에디터를 닫은 뒤에.
- **관심 영역:** 기획 §7.3을 18타일=18m로 보강함. 구현 `InterestRange.FallbackMeters = 18`과 일치. 원작 합격은 2클라 실측 후.
- **원작 울온 절반 크기:** map0 6144×4096 타일(Stratics, 1타일≈1m → 절반 ≈3072m). 출처 https://community.stratics.com/threads/land-in-uo.221830/ . Unity Terrain 하이트맵 최대 4097. 지금 셀(300/512 m)이면 LandScale 최대 8(2400m). 3072m는 청크·더 큰 셀 중 사람 선택이 필요.
- **INBOX vs 기획 §6.1:** 기획은 「작은 하나의 살아 있는 월드」. 오너 INBOX(절반 크기)를 우선하되, 한 지형으로 3072m는 엔진 상한과 충돌.
- **INBOX 16:04 병렬 vs PROMPT 한 작업:** INBOX 우선. #15는 Unity와 안 겹치는 보고서만. 클라 빌드는 #46에서 닫음. 남은 2클라는 바이너리로 돌릴 수 있음. 물가·절반맵은 Codex.
- **그록/그록봇 공존:** 이 트리는 `projects/ulon`만. `ai_lab-loop`(loop-claude) 병합·수정 없음.
- **운영 persist 전량 복원:** POST `/restore` 는 테이블 DELETE 후 삽입. 사람 확인 전 운영에 안 돌림. 스테이징(임시 스키마)만 이번 확인.
- **원점 중복 소품 삭제 (2026-09-12):** `KindLight` 가로등 4·`KindArch` 아치 4·`KindSeat` 벤치 2·`KindPlant` 덤불 4가 월드 (0, 10, 0)에 겹침. VillageDecor 광장 귀퉁이 가로등 8은 별개. 게이트가 개수를 지킴. 지울지·옮길지는 사람 결정. 임의 삭제 안 함.
