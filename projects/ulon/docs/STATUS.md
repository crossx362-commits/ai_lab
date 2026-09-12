# 울온 현황

<!-- loop-stamp:start -->
마지막 바퀴: **#3** (실패) · 2026-09-12T13:53:02+0900
STATUS 자동 갱신 훅을 넣기 위해 #3을 중단함. 이후 매 바퀴 스크립트가 헤더를 찍는다.

- 모델 `grok-4.6` · 경과 0s · 세션 rc `15`
- HEAD `605ffaf9 docs(ulon): loop#2 맵 확대는 턴 한도에 실패, 미커밋 코드 원복` · 브랜치 `master`
- INBOX 미처리 **2**건
- 이 바퀴 커밋:
- (이 바퀴 커밋 없음)

## 바퀴 기록

| 바퀴 | 결과 | 시각 | 모델 | 경과 |
|---|---|---|---|---|
| #0 | 성공 | 2026-09-12T12:28:00+0900 | grok-4.6 | 187s |
| #1 | 성공 | 2026-09-12T13:04:00+0900 | grok-4.6 | 362s |
| #2 | 실패 | 2026-09-12T13:37:34+0900 | grok-4.6 | 1236s |
| #3 | 실패 | 2026-09-12T13:53:02+0900 | grok-4.6 | 0s |
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
| 카메라 고정 3/4 쿼터뷰 | 부분 | `QuarterViewCamera` 코드·과거 QA 샷 존재. 이번 클라 실행 없음 |
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
| 월드(마을1·필드·광산·던전3·테스트) | 부분 | 빌더·과거 `builds/qa/` PNG. 이번 렌더 없음 |
| 몬스터 | 부분 | 원장 14종(보스 포함). 기획 20종 내외 미달 |
| 마법·시약·명상·시전 중단 | 부분 | Spellbook·슬라이스 주문 코드. 플레이 미실행 |
| 죽음→유령→부활→시체 회수 | 부분 | 루팅 우선창 커밋 `8a7ad1b5`/`dcba16e8`. 플레이 미실행 |
| 무게·과적·STR 요구 | 부분 | 코드. 중첩 컨테이너는 주머니 1단 |
| Fame/Karma·가드존·범죄 | 부분 | 코드. Open PvP 플레이 미실행 |
| 파티·길드·길드전·결투 | 부분 | HUD 패널 코드. 플레이 미실행 |
| 조련·마구간·Follower | 부분 | Phase 2가 앞섬. 플레이 미실행 |
| Moongate·Mark/Recall | 부분 | 코드. 플레이 미실행 |
| FishNet 호스트/클라 | 부분 | `AutoStartNetwork`. 전용 서버 빌드·외부 접속 **미확인** |
| PostgreSQL 영구 저장 | 부분 | `server/persist.py`·`schema.sql`. **이번: persist pid 사망, 5432 미청취.** `/ready` 실측 없음 |
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
- **persist/postgres 꺼짐.** `data/persist.pid`(12877)는 죽은 프로세스. 5432 미청취. `data/alpha_status.json`은 2026-09-04 성공 스냅샷이라 **현재 성공이 아님**.
- **셀프체크·QA샷 이번 미실행.** 마지막 `unity/Logs/selfcheck_dev.log`는 2026-09-11(배치 종료 `Exiting batchmode successfully` — 게이트 합격 문구는 로그에서 못 찾음). `two_client` 마지막은 2026-09-03.
- **울온 Unity 에디터는 안 떠 있음.** Hub만 살아 있고 Hub 시작 프로젝트는 재와별. 배치 잠금은 이번 해당 없음. 다만 재와별 에디터가 나중에 같은 에디터 바이너리를 잡을 수 있음.
- **기획서–코드 불일치(문서):** 하우징·조련·길드/PvP가 MVP 후순위인데 코드가 앞섬(`DESIGN_COVERAGE`). 몬스터 14/20. 방어구 세트 얇음. UI가 원작 검프가 아님.
- **워크트리 분기:** `/Users/junholee/ai_lab-loop` (`loop-claude`, HEAD `2462cead`)는 이 트리보다 **뒤**다(물 깊이색 커밋에서 멈춤). 이 트리가 persist·루프·루팅을 더 갖고 있다. 반대로 **UlonClient.app은 그 워크트리에만** 있다. 병합·리베이스·그 트리 수정은 하지 않음.

## 완료한 것 (이 바퀴)

- 기획서·커버리지·코드·에셋·git·프로세스 현황 파악
- `docs/STATUS.md`·`docs/ASSETS.md`·`docs/CREDITS.md`·`docs/board.json` 초판
- `tools/test_alpha_readiness.py` 통과

## 지금 하는 것

없음 (0번째 바퀴 종료).

## 다음 할 것 (우선순위 → board.json)

1. 운영 PostgreSQL + persist 기동, `/ready` 200 실측 (P0)
2. 이 트리에서 `UlonClient.app` 재빌드
3. `tools/two_client_check.sh`로 §13.1 최소 동기화 재검증
4. JSON 폴백 vs DB 복구 후 최신 저장 선택 (P0)
5. 핸드오프 물가 톱니 — 사람 판정 없이 진행 가능한 범위만
6. 기획 UI(Paperdoll·DnD) / 몹 20종 / 관심 영역은 그 다음

## 막힌 것 (사람 결정)

- **에셋 다운로드:** `ASSET_WANTLIST.md`의 모루·대장간 건물·마구간 건물·목공소·갱도 입구·Quaternius MegaKit. 받기는 오너 직행. 조사만 되어 있음.
- **Quaternius Universal Base / Modular Outfits:** 기획 1차 필수인데 `_ThirdParty/Quaternius/`는 빈 폴더. 반입 여부 사람 결정.
- **Noto Sans KR:** 기획서 SIL OFL 1.1로 적혀 있으나 폰트 파일 없음. IMGUI 기본 글꼴 사용 중.
- **Kenney Retro Fantasy Kit / Kenney UI Pack:** 레지스터·기획에만 있고 파일 없음.
- **이동 속도 등 체감 수치:** 기획서 없음 → 「기획서 보강 필요」. 출처 없는 수치로 구현하지 않음.
- **ai_lab-loop 워크트리:** 앞선 작업이 아니라 뒤처짐. 그곳 빌드만 쓰지 말고 이 트리에서 다시 빌드할 것. 병합 금지.
- **물 2·3단계(프레넬·정점 흔들림), 강 곡류:** 핸드오프상 검수 판정 뒤.
