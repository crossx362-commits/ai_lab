# 울온 현황

<!-- loop-stamp:start -->
마지막 바퀴: **#6** (성공) · 2026-09-12T14:24:02+0900
다음: Unity에서 MCP 세션을 켠 뒤 600m·월드맵 셀프체크 → 행동 체감 → 프리팹+이펙트.

- 모델 `grok-4.6` · 경과 231s · 세션 rc `0`
- HEAD `a9d7e22c [loop#6] 보드에 persist-ready 커밋 해시` · 브랜치 `master`
- INBOX 미처리 **4**건
- 이 바퀴 커밋:
- `a9d7e22c [loop#6] 보드에 persist-ready 커밋 해시`
- `3e136972 [loop#6] persist /ready 200 실측 (postgres)`

## 바퀴 기록

| 바퀴 | 결과 | 시각 | 모델 | 경과 |
|---|---|---|---|---|
| #0 | 성공 | 2026-09-12T12:28:00+0900 | grok-4.6 | 187s |
| #1 | 성공 | 2026-09-12T13:04:00+0900 | grok-4.6 | 362s |
| #2 | 실패 | 2026-09-12T13:37:34+0900 | grok-4.6 | 1236s |
| #4 | 성공 | 2026-09-12T14:05:38+0900 | grok-4.6 | 705s |
| #5 | 성공 | 2026-09-12T14:19:23+0900 | grok-4.6 | 777s |
| #6 | 성공 | 2026-09-12T14:24:02+0900 | grok-4.6 | 231s |
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
| 타깃 RPG 전투(서버 판정) | 부분 | `TryAttack`/`AttackResolve`. 이번 전투 미실행. 2026-09-03 two_client 로그는 몹 HP 30→19(과거) |
| 스킬/마법 퀵바 | 부분 | IMGUI 버튼. 단축키·사용성 미검증 |
| 장비·인벤·내구도·수리 | 부분 | 코드·게이트. 플레이 미실행 |
| 채집·제작·Maker Mark | 부분 | 제작법·스테이션 코드. 플레이 미실행 |
| 공급 비율 45/25/20/10 | 부분 | 드랍·제작은 있음. 비율 계측 **없음** |
| 안전 거래 | 부분 | `TradeView`. 2클라 이번 미실행 |
| NPC 상점·은행·훈련 | 부분 | 스테이션 코드·QA 샷. 플레이 미실행 |
| 플레이어 벤더·하우징 | 부분 | 코드 있음(기획상 Phase 2인데 앞섬). 플레이 미실행 |
| 월드(마을1·필드·광산·던전3·테스트) | 부분 | loop#7 플레이: Terrain 600×600m, hres=1025. 미니맵·세계지도(M) 표시, 라벨 600m. 원작 절반(~3072m)은 아님 |
| 몬스터 | 부분 | 원장 14종(보스 포함). 기획 20종 내외 미달 |
| 마법·시약·명상·시전 중단 | 부분 | Spellbook·슬라이스 주문 코드. 플레이 미실행 |
| 죽음→유령→부활→시체 회수 | 부분 | 루팅 우선창 커밋 `8a7ad1b5`/`dcba16e8`. 플레이 미실행 |
| 무게·과적·STR 요구 | 부분 | 코드. 중첩 컨테이너는 주머니 1단 |
| Fame/Karma·가드존·범죄 | 부분 | 코드. Open PvP 플레이 미실행 |
| 파티·길드·길드전·결투 | 부분 | HUD 패널 코드. 플레이 미실행 |
| 조련·마구간·Follower | 부분 | Phase 2가 앞섬. 플레이 미실행 |
| Moongate·Mark/Recall | 부분 | 코드. 플레이 미실행 |
| FishNet 호스트/클라 | 부분 | `AutoStartNetwork`. 전용 서버 빌드·외부 접속 **미확인** |
| PostgreSQL 영구 저장 | 동작함 | loop#6: `pg_isready` 수락, `characters` 17행, persist pid 68384·8777 청취, `GET /ready` HTTP 200 `ok:true` `driver:postgres` (sha `e020037e…`=persist.py). `GET /character/selfcheck` 200. `closed_alpha_smoke.sh` ok. 클라 재접속 저장 왕복은 **미실행**(UDP 7770 닫힘·클라 바이너리 없음) |
| 관심 영역(Interest Management) | 없음 | 기획 §7.3. 코드 없음 |
| LOD·거리 비활성 | 없음 | 기획 §8.1 |
| UI 팩(Paperdoll 그림·DnD·우클릭) | 없음 | HUD는 IMGUI 기본 버튼. Kenney UI 팩 미반입 |
| VFX | 부분 | `ActionVfx` + Kenney 파티클 일부. 이번 재생 없음 |
| SFX | 부분 | Kenney RPG Audio 3클립. 이번 청취 없음 |
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
- **persist/postgres는 이번 살아 있음.** pid 68384, 5432·8777 청취, `/ready` 200. 옛 STATUS의 pid 사망은 낡은 기록이었다.
- **셀프체크·QA샷 이번 미실행.** 마지막 `unity/Logs/selfcheck_dev.log`는 2026-09-11(배치 종료 `Exiting batchmode successfully` — 게이트 합격 문구는 로그에서 못 찾음). `two_client` 마지막은 2026-09-03.
- **울온 Unity 에디터 점유.** PID 85035가 `projects/ulon/unity`를 잠금. 배치 셀프체크·클라 빌드는 불가. HTTP MCP(127.0.0.1:8080)로는 연결됨 — loop#7은 이 경로로 플레이·샷. Grok stdio MCP는 여전히 Start Session 없음.
- **기획서–코드 불일치(문서):** 하우징·조련·길드/PvP가 MVP 후순위인데 코드가 앞섬(`DESIGN_COVERAGE`). 몬스터 14/20. 방어구 세트 얇음. UI가 원작 검프가 아님.
- **워크트리 분기:** `/Users/junholee/ai_lab-loop` (`loop-claude`, HEAD `2462cead`)는 이 트리보다 **뒤**다(물 깊이색 커밋에서 멈춤). 이 트리가 persist·루프·루팅을 더 갖고 있다. 반대로 **UlonClient.app은 그 워크트리에만** 있다. 병합·리베이스·그 트리 수정은 하지 않음.

## 완료한 것 (이 바퀴)

- `map-land-scale` 검증: HTTP MCP로 에디터 세션 연결. 월드맵 베이크 통과(256² 물 14828·뭍 50708, 물비율 0.226, 호수=물, 원점=뭍). Terrain 600×600, hres=1025, ares=1024.
- 에디터 플레이로 화면 확인: 미니맵 + M 세계지도, 범례 「600m」, 섬·바다·호수·숲·초원·광산 구분. 샷 `unity/Captures/loop7_play_minimap.png`(커밋 안 함).
- `python3 tools/test_alpha_readiness.py` OK. 배치 `slice_selfcheck.sh`는 에디터 점유라 안 돌림(같은 게이트 `AssertWorldMapBake`는 execute_code로 통과).
- 원작 대비: 같은 점 — 종이 지도처럼 뭍/물/마을이 읽힘, 고정 3/4. 나은 점 — 상시 미니맵·3D. 부족한 점 — 한 변 600m는 원작 절반(~3072m)에 못 미침(카드 `uo-half-span`). 핵심 동작(600m+월드맵)은 이 카드 범위에서 통과.

## 지금 하는 것

없음. loop#7 카드 `map-land-scale` 닫음.

## 다음 할 것 (우선순위 → board.json)

1. INBOX 「행동 느낌 없음」 — 타격/시전 VFX·SFX (`action-feel`). 플레이로 확인
2. INBOX 「FBX 말고 프리팹+이펙트」 — `scene-prefab-vfx`. `VisualSliceBuilder` 분할 금지
3. 이 트리에서 `UlonClient.app` 재빌드 (에디터 점유 해제 후)
4. 원작 지도 절반(~3072m): 청크 또는 셀 확대. 사람 선택 (`uo-half-span`)
5. `tools/two_client_check.sh`

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
