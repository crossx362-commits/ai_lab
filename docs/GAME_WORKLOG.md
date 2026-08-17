# 재와 별 — 작업 인계 기록

> 오너 지시(2026-08-15): "세션 길어질 경우 해당 세션 작업 내용 잘 기록해서 계속 연결해서 작업할 수 있게"
>
> **세션이 끊기면 이 파일부터 읽는다.** 커밋 로그는 "무엇을 했나"만 남기고 "지금 어디까지
> 와 있고 다음이 뭔가"는 안 남는다. 그 간극을 여기서 메운다.
>
> **갱신 규칙**: 작업 마디(커밋 단위)마다 「진행 중」을 갱신한다. 끝난 것은 「완료」로 내리되
> **판정 근거(수치·커밋 해시)를 같이 적는다** — 근거 없는 완료는 다음 세션이 다시 검증해야 한다.

---

## 경매장 전용 그림 (2026-08-17)

큐 1번 화질은 재생성 금지. INBOX 09:57 남은 경매장 수레.
`DedicatedOf(Auction)`=`estate_auction_0`. SelfCheck PASS. PNG 목재 장.
`QA_NO`면 수레. `W3Party` 안 건드림.

## 명예 승리 방어력 비례 (2026-08-17)

큐 1번 화질은 재생성 금지. §18-13 ✅ 승리 +30 ±50%.
`Honor.WinForCut` Cut 0=15 · 40=45. SelfCheck PASS. PNG 월드맵 +45.
수비 성공 +20은 안 넣음. 코드 `b4934096`. `W3Party` 안 건드림.

## 긴급 탈출 수동 한정 (2026-08-17)

큐 1번 화질은 재생성 금지. §4 ✅ 자동에서 두루마리 금지.
`EscapeManual` 보스·침략만. SelfCheck PASS. PNG 필드 자막.
코드 `4d2d759d`. `W3Party` 안 건드림.

## 영지 마을 굴려 확대 (2026-08-17)

큐 1번 화질은 재생성 금지. INBOX 09:18 확대.
`EstateYard` 휠 1.50. SelfCheck PASS. PNG 집이 전면보다 크다.
코드 `3a9ff6aa`. `W3Party` 안 건드림.

## 영지 마을 끌어 보기 (2026-08-17)

큐 1번 화질은 재생성 금지. INBOX 09:18 카메라.
`EstateYard` 끌어 180. SelfCheck PASS. PNG 마름모가 오른쪽.
코드 `9d396c1f`. `W3Party` 안 건드림.

## 영지 현황 도크 (2026-08-17)

큐 1번 화질은 재생성 금지. 현황 2×2가 마을을 덮었다.
`EstateStatusHud` 5칸 88. SelfCheck PASS. PNG 마을+아래 도크.
코드 `e0802219`. `W3Party` 안 건드림.

## 개발 보드 운영 (2026-08-17, 오너 「항상 잘 관리해」)

8766 보드. 원장은 `loop/board.py` 머리말. 세션이 보드를 만지면 아래를 유지한다.

- 채팅 지시 → `python3 loop/board.py command "제목" "본문"` (`owner_commands.json`)
- 다음 할 일은 위. 프로토가 끝나면 지금 단계(마을·탑·장비)로 바꾼다.
- 글은 짧은 한국어. `humanize_title`/`humanize_detail`
- 외부 테스터는 아나만. V4 10명은 키트에서 뺀 상태
- 끝난 일 검은 화면 PNG 금지 (`shot_is_black`)
- 회귀: `python3 -m unittest loop.test_board loop.test_v4_playtest`

## 게임 전체 점검 (2026-08-17)

오너 「게임 전체 부분 체크」. `GameSweepSelfCheck` 20종 + 보드 회귀.
캐릭터창 경험치 표기 `EXP`→`경험`. 보드 검증 칸에 기록.

## 검증을 보드에 올림 (2026-08-17)

오너 「테스트는 철저하게, 결과를 보드에 작성」. `ChatWorkBatchSelfCheck` 7종 +
보드 회귀 74. 화면 「검증 결과」가 `last_test_report.json`을 읽는다.

## 사냥 중 강화 3택 (2026-08-17)

오너 「뱀서류처럼 사냥중 강화 목록 안뜨는데」→「니가 정해서」.
정함: 접속 중 사냥만, 처치 8마리부터 전투 정지 3택. 임시·나가면 삭제.
영구 레벨에 안 건다(판 끝난 뒤 정산·시드 만렙이면 영원히 안 뜸). 보스전·W1~W3·방치는 안 뜸.
`HuntBoon` + Overlay. `HuntBoonSelfCheck`.

## 캐릭터도 안 겹치게 (2026-08-17)

오너 「캐릭터도 안겹치게해」. 파티는 집 `Resolve`만 하고 `Around`가 없어 지붕을
가로질렀고, 겹침 해소는 70%만 받아 서로·몹 위에 남았다. 같은 역할은 스폰이
한 점. 고침: 이동·대시는 몹과 같이 비키고, 파티는 `Unstick`으로 몸 너비만큼
완전 분리. `UnitSeparationSelfCheck`.

## 몬스터·집·배경 안 겹치게 (2026-08-17)

오너 「몬스터 집 배경 오브젝트 안겹치게해」. `FieldDecor.Place`가 이미 선 프랍을
안 봐서 나무·바위가 지붕 위에 앉고, 집 옆 나무는 `asCover=false`라 몹이 뚫었다.
고침: 집을 먼저 세우고 `_placed` 원으로 겹치면 버린다. 집·나무·바위·건초는
엄폐 모드에서 `ArenaLayout` 장애물. `FieldDecorOverlapSelfCheck`.

## 크롬 글씨 여백 (2026-08-17)

오너 타이틀 스크린샷: 「게임 시작」이 금테에 겹침. 9-slice dest(패널 짧은 변 24%)와
CardLayout 8–18px가 어긋난 게 원인. `UiAtlas.SlicePad`/`ContentRect` 하나로 DrawSliced와
카드·탭·버튼·명부·편성·스타터·장비 스튜디오·배너·월드맵 캡션이 같은 안쪽 칸을 쓴다.
`UiAtlasSelfCheck` 타이틀 532×180 금테 안 단언. meas PASS. Play 재시작 필요.

## 보스 HP 프레임 소비처 (2026-08-16)

큐 UI 남은 것. `boss_hp_frame`은 아틀라스에만 있었다. `DrawBossHp` + BattleScreen Overlay.
SelfCheck PASS. PNG 탑 견본 3칸 + 실전 7500/7500 페이즈 4. W3Party 안 건드림.

## 올라마 분담 (2026-08-16)

`loop/ollama_split.py` — classify/copy. 실측 gemma4:12b JSON.
분류: 다음=UI 퀄리티(INBOX), owner=ollama. 귀환 카피 「피격 시 시전 취소」를 Overlay에 반영.
대장간 둘째는 루프 #8. 클라우드 올라마 금지.

## 긴급 탈출 6초 캐스트 (2026-08-16)

루프는 대장간 둘째. 이 세션은 §4 캐스트만. 피격 취소 시 두루마리 미소모.
SelfCheck PASS.

## 보드 내 선택 축소 (2026-08-16)

오너: 정말 중요한 것만 체크. `pending_choices`는 `human`만.
외부 테스터·V4 70%는 「내 선택」에서 제외. 관문은 되돌릴 수 없는 OUT.

## 루프+에이전트 — 영지 3건물 첫 슬라이스 (2026-08-16)

- 오너 보드 「이걸로 진행」. ashes-code/ashes-art가 소비처를 가름.
- **수비대**: `DefenseState` 최대 5, 배치=출전 제외. 침략 본게임 안 염.
  SelfCheck PASS. 월드맵 `수비대 0/5 — 침략 전투는 아직 없다`.
- **대장간**: 루프 이터 #7이 가죽→흉갑→전투 HP (`ec234975`). 수비대를 덮지 않음.
- **경매장**: 거래 서버 없어 잠금. V4 70%는 보류 유지.

## 개발 현황 그래프 (2026-08-16)

- 보드 `http://127.0.0.1:8766`에 관문·로드맵·큐·Resources 그래프. 숫자는
  STATUS 큐 표·DESIGN 관문·§21-4·Resources PNG·git log에서만 집계.
- V4 70%는 0%로 둔다. 자동 경계를 통과로 읽지 않는다.

## Resources 소비 대조·미사용 검사 (2026-08-16)

- 검토: `unity/Assets/Resources` 267png / 45.8MB. 코드 `Resources.Load`·`GetPropNames`·
  `FxPool`/`JobVfx`/`StatusVfx`·아틀라스·`BackgroundArt`와 1:1 — 고아 0, 누락 0.
- 남긴 것: ash/던전 프랍(GetPropNames에 있음), SpriteBank 폴백 10장, mob01 22장,
  FX 22장(전부 로드). 생성 원본 `art/out_*`는 게임이 안 읽으므로 그대로.
- 잔재: `unity_meas`에 이전 정리분 42장이 남아 있었음 → `sync_meas.sh`로 원본과 맞춤
  (`unity_meas`는 git 추적 밖).
- 재발 방지: `game_asset_names.unused_resource_problems()` + 회귀
  `tests/test_game_asset_unused_resources.py`(고의 미사용 PNG 주입 시 FAIL).

## UI 프레임 소비처 — 버튼 3상태·9-slice·체력바 (2026-08-16)

- 클로드 주간 한도 세션이 배경 6장 반입 직후 「UI 프레임이 다음」에서 끊김.
- 아틀라스 조각은 이미 있었는데 `button_hover`/`button_pressed`/`hp_frame` 소비처가 0곳.
- `UiAtlas`에 ButtonKey·DrawSliced·DrawMeter를 두고 GameScreen·W3Party가 읽는다.
- 검증: 컴파일 84소스 0오류, `unity_meas` `UiAtlasSelfCheck` PASS.
- 다음: 아이콘(화면에 실제로 뜨는 것) 또는 V3 한 판 종단.

## V3 보스 HP — 파티 공격 배선, 실행 QA 대기 (2026-08-16)

- 짧은 설계: `BossBattle`의 보스별 HP를 단일 권위로 유지하고, W3에는 화면을 중복해서 그리지 않는
  공격 타깃 슬롯만 둔다. 보스전 일반 웨이브를 멈추고 실제 보스 슬롯 피해만 HP·페이즈·처치로 전달한다.
- TDD: 신규 계약 부재 10건 RED. SelfCheck는 실제 `W3Party.DamageMob` 경계로
  `9000→4500→0`, 페이즈1·처치1과 `BOSS_NO_DPS=1` Boss/W3 `9000→9000`을 단언한다.
- 리뷰 수리: 처음 구현의 쫄/잡몹 피해 복제, 처치 시 페이즈 누락, 차단 모드 프록시 교착,
  프록시 AI·보상 혼입, 실패 시 테스트 오브젝트 누수를 제거했다. 최종 Critical/Important 0.
- 검증: 정적 컴파일 82소스 오류0, 검사기 고의 오류1 탐지, diff check 통과. 코드 `e48aee09`.
- 미완: 원본 manifest의 `com.unity.modules.physicscore2d@1.0.0` 미해결 의존성 때문에 Unity 실행
  SelfCheck·보스 PNG는 미확인. `output/qa/ashes-to-stars/boss_dps_*`와
  `mob_family_hunt_attempt_20260816.log`에 증거를 남겼다. 장판·힐체크는 다음 독립 슬라이스다.

## 2차 각성 초필살기 — 실전 배선·계측 완료 (2026-08-16)

- 짧은 설계: 기존 4스킬/조작 2슬롯은 유지하고, `Second`에만 직업 고유 Gauge와 별개인
  초필 게이지 100%·180초 쿨·E/별도 버튼을 연다. 역할 별 실전 효과를 계측한다.
- TDD: 신규 계약 API 부재 7건 RED, 리뷰 회귀 계약 9건 RED → 최종 80소스 오류 0.
- 실행: 광전사400·궁수632·드루이드150·주술사205, 모두 효과>0·쿨180초. 단계/게이지/쿨
  차단은 각각 효과0·PASS. `unity_meas` SelfCheck 실행 PASS.
- 시각: `output/qa/ashes-to-stars/shots/qa_second_광전사_normal.png` — 선택 카드,
  기존 2슬롯, 별도 `초필 2% / 178s` 버튼과 파티 보호 연출 확인.
- 코드 커밋 `31a50057`. 다음: 1차 6종 슬롯1/2 실행·PNG·`QA_ADV_NO_SLOT` 차단 증거 종결.

## 1차 6종 실전 계측 하네스 — 실행 증거 완료 (2026-08-16)

- 짧은 설계: 기존 BattleScreen/W3 전투를 그대로 쓰고, QA만 직업 하나를 신규 로스터에
  주입해 1초·3초에 슬롯 1/2를 강제한다. 스킬 분기 안에서 실제 피해·회복·보호막
  수치를 모아 계약 존재가 아니라 소비 성립을 판정한다.
- TDD: 생산 API 전 SelfCheck 컴파일 오류 1건 RED → 78소스 오류 0 GREEN. 검사기 고의 오류
  1건 탐지. 코드 커밋 `ab5fe3d0`.
- 실행: 광전사18/1·궁수62.4/4·소환사93.6/1·드루이드12/28·주술사5/2.5·정령사5/100,
  6종 모두 두 수치 `>0` PASS. 정상 PNG 6장과 Player.log를
  `output/qa/ashes-to-stars/first_advancement/`에 보존하고 직접 열어 실제 전투 렌더를 확인했다.
- 네거티브: `QA_ADV_NO_SLOT=1`은 0/1 FAIL, `=2`는 18/0 FAIL. 일반 프레임 캡처가 전용
  계측을 선점하던 결함과 광전사 주 대상 명중/드루이드 만피 픽스처 결함을 수리했다.
- 코드 커밋 `b35fc3ac`. 원본의 타 세션 미커밋 manifest는 건드리지 않고 측정 사본만 정상화했다.

## 전직 단계별 전투 스킬 — 4차 슬라이스 완료 (2026-08-16)

- 설계: 조작 슬롯 2개(§5)는 유지하고, 전투원 계약에 전직 단계를 보존해 기본직업은 역할 학습
  스킬 2개, 1차는 고유 메커니즘을 포함한 4개 능력을 활성화한다.
- 구현: `PartyState.SortieCombatants` → `BattleScreen.ApplyGameParty` → `W3Party.Member.Advancement`.
  기본 4역할 전투 분기와 1차 전용 최후의 보루 게이트를 추가했다.
- 검증: TDD RED 2건, 최종 75소스 오류 0, 검사기 오류 주입 1건 탐지. 커밋 `42315c7b`.
- 미완: W3 미지원 1차 6종은 검사 폴백. 오너 Unity 락으로 SelfCheck/스크린샷 미실행.


## 몹 chaser 계열 재생성 — 완료 (2026-08-15 17:1x, 이터3, INBOX⭐)

**핵심: 이터1·2의 "백엔드 죽음"은 오진이었다.** higgsfield는 살아 있고 느릴 뿐(INBOX 정정이 옳음).
인내심을 갖고 돌리니 chaser가 정상 생성됐다.

- **생성**: `python3 aigen.py --spec spec_p2_chaser2.json --out-dir out_p2` (백엔드 higgsfield 기본).
  sheet A(21:9, 10셀)는 10분 타임아웃 2회 후 3번째 시도 성공, sheet B(4:3, 12셀) 1회 성공. 총 ~30분.
  판정 근거: 크레딧 1044→1030, `pgrep -f higgsfield` 자식 프로세스 생존.
- **후처리 함정**: sheet A는 셀 경계에 **검은 격자선**을 그려서 나온다(chroma_key는 마젠타만 지움).
  → `wipe_gridlines.py`로 시트 청소 후 재분할. sheet B는 격자선 없음.
- **파이프라인**: split_ai_sheet(고정격자 5×2·4×3, 22프레임) → wipe → align_frames(공통 159x124,
  바닥정렬) → `Resources/sprites/mob_chaser`(png만 덮음, meta 유지) → `game_asset_names.py` ✅통과.
- **화면 확인**: `qa_shot.sh hunt` → `output/qa/ashes-to-stars/shots/qa_hunt.png`. 늑대 실루엣이
  FamilyColor 색조별(주황·청·분홍)로 표시, 캐릭터와 같은 픽셀아트 세계관. 무채색+런타임틴트 파이프라인 실증.
- **네거티브 컨트롤**: 같은 화면에 미재생성 3계열(charger·ranged·swarmer 옛 아트)이 함께 나오는데
  톤이 매끈/3D톤으로 갈려 보인다 — 옛 아트 되돌림과 동등한 증거.

**남은 일**: charger·ranged·swarmer 3계열(spec 준비됨). 다음 세션이 **1계열씩** 위 파이프라인 그대로.
백엔드는 살아 있으니 인내심만 있으면 된다.

---

## 지금 진행 중 (2026-08-15 13:5x)

### 캐릭터 4직업 재생성 — 착수 완료, 생성 진행 중

**왜**: 오너 "현재 캐릭터는 임시본이라 품질 낮음, 재생성 대상". 캐릭터가 최우선
(P2 몹보다 먼저 — 캐릭터는 `SpriteBank.JOB_FRAMES` 슬롯이 이미 다 있어 **뽑는 즉시
화면에 반영되는 유일한 트랙**이다. 몹은 `MOB_DIRS` 확장이 선행돼야 했다).

**착수 전 3대 판정 완료** → `art/CHARACTER_PLAN.md`
1. 앵커 정합성 ✅ — 몬스터 오염 제거(`anchor_characters_clean.png`), 직업별 참조 4장 생성
2. 크레딧 ✅ — **1생성 2크레딧**, 3직업×3시트=18크레딧. 잔액 1092로 여유 충분
3. 파생 설계 ✅ — 전직 11종을 파츠 교체로 파생시키기 위한 제작 규칙 4가지 확정

**진행 상황**

| 직업 | 스펙 | 생성 | 분할·정렬 | 반입 |
|---|---|---|---|---|
| tank | `spec_char_tank.json` | 미실행 | — | — |
| **dps** | `spec_char_dps.json` | ✅ A·B·C (프롬프트 3차 확정본) | — | — |
| healer | `spec_char_healer.json` | 미실행 | — | — |
| buffer | `spec_char_buffer.json` | 미실행 | — | — |

**파이프라인 확립 완료** (`05a62c48`) — `./art/build_chars.sh [직업]` 하나로
생성→격자선제거→분할→정렬이 돈다. 프롬프트는 3차 시행착오로 확정:

| 차수 | 문제 | 해결 |
|---|---|---|
| 1차 | 프레임 라벨이 이미지에 그려짐 | 웹서치 → **스펙시트형 프롬프트가 원인**. 서술형으로 재작성 |
| 2차 | 6셀 요청에 9셀 + 바닥 그림자 | 룰셋에 `EXACTLY SIX CELLS`·`NO SHADOW OF ANY KIND` |
| 3차 | ✅ 전부 해결 | 인접 프레임 차이 28~50%로 동작도 잘 읽힘(실측) |

⚠️ **내 도구가 낸 사고**: `wipe_gridlines.py`가 단독 프레임(invuln)의 캐릭터를 관통하는
줄무늬를 그어 이미지를 훼손했다. "캐릭터는 한 줄을 가득 채우지 않는다"는 가정이 6셀
시트에서만 참이었다. 수정 후 네거티브 컨트롤로 확인함.

**구조**: 13프레임 = 6셀 시트 A(idle·walk2·attack2·special) + 6셀 시트 B(dash4·hurt·death)
+ 단독 C(invuln). 무적 색은 §0-A 확정대로 직업별로 다르다(탱=금 구체 / 근접딜=보라 오라 /
힐·버퍼=금+초록 링). 이동기도 다르다(탱=방패돌진 / 딜=구르기 / 힐·버퍼=짧은스텝).

**다음 할 일**
1. dps 생성물 육안 확인 → 6셀 분할(`split_ai_sheet.py`) → 13프레임 이름 매핑
2. 통과하면 healer·buffer·tank 생성
3. `align_frames.py` → `game_asset_names.py` → 대조 이미지 → Play 스크린샷
4. 통과 후 기존 임시본을 `art/ref_old_chars/`로 이동(삭제 아님)

---

## 완료 (근거 포함)

| 항목 | 근거 | 커밋 |
|---|---|---|
| W3 측정 신뢰성 복구 | 시드 교락(`cfg*1000`) 제거. 5구성 동일 시드 확인 | `956650fa` |
| 도발 하드 락 | **D/A 0.66** (목표 0.75 이하 통과). 5회 중앙값 | `e5c5c6e1` |
| §3 전투 스타일 SO 배선 | 소비처 0곳 → `W3Party.cs:52`가 `Resources.LoadAll`로 실제 소비 | `067b8d6e` |
| 밸런스 앵커 재판정 | **오진 종결** — 26.9%는 게임 수치가 아니라 리텐션 가정이 지배(네거티브 컨트롤로 확정) | `fbc5bc69` |
| P1 프랍 32종 반입 | 검사기 통과 | `da80d6f8` |
| P2 몹 4계열 연결 | 88장 반입 + `MOB_DIRS` 1→5종 + 스폰/애니 규칙 통일 | `5caf3841` |
| 몹 걷기 튐 수리 | 캔버스 4종 혼재 → 1종 통일, 발 여백 0 | `c7d66a04` |
| 화면 3종 실태 확정 | 파티편성·캐릭터 ✅구현 / 영지 ⚠️껍데기. 핸드오프 모순 해소 | `fbc5bc69` |
| 단일 세션 통합 개발 | 세션 분리 자동화 전부 중단 | `b440e341` |

## 아직 안 한 것 (우선순위 순)

1. **캐릭터 재생성 완주** — 위 진행 중
2. **영지 하위 건물 4종** — `EstateScreen.cs`가 껍데기. 대장간·경매장·영묘·수비대가
   전부 "아직 내용이 없다"로 간다
3. **전투 스타일 UI 부재** — 데이터는 살아났는데 플레이어가 바꿀 화면이 없다.
   `CharacterScreen`에 스타일 선택이 없어 실질적으로 기본값 고정
4. **RaceDef 배선** — §3 종족 4종이 전투에 영향 0. `CombatStyleDef`와 같은 계열 빈 배선
5. **보스 쫄 소환** — `BossBattle.cs:317`이 빈 GameObject만 만든다
6. **W2 회피** — 포위율 97%인데 접촉 60초 3회. ⚠️흡수·대시 기준을 낮춰 통과시키지 말 것
7. **P2 몹 Play 육안 확인** — 검사기·컴파일은 통과했으나 화면은 아직 안 봤다

## 보류 — 근거 소멸

**탱 상시 DR 20%**: 도입 근거가 "E/A 0.67 → 0.60 이하"였는데, 최신 5회 중앙값에서
**E/A가 이미 0.39**다(`w3_reps5.csv`). 힐러 가치가 이미 충분히 나오고 있어 지금 넣으면
근거 없이 수치를 만지는 것이 된다. 재개하려면 새 근거가 필요하다.
