# 재와 별 — 현재 위치 · 다음 할 일

> **인수인계서.** 매 이터레이션은 기억 없는 새 세션이다. 끝낼 때 이 파일을 갱신하지 않으면
> 다음 세션이 처음부터 다시 판단한다.
>
> 갱신 규칙: 완료로 내릴 때 **판정 근거(수치·커밋 해시)를 반드시 같이 적는다.**
> 근거 없는 완료는 다음 세션이 재검증해야 하므로 완료가 아니다.

최종 갱신: 2026-08-20 · 파티 폴리싱 전수 육안 = 결함 0 클린. 코드칸 시도했으나 유일한 미구현 ✅ 갭(정예우선타겟)이 W3Party 전투 AI 가드레일로 막혀 폴리싱으로 대체
마지막 트랙: 폴리싱
폴리싱 다음: **월드맵**. 이어 던전 → 결과 → 전투HUD → 타이틀 → 영지 → 필드 → 캐릭터 → 파티.
  (영지 폴리싱은 `docs/GAME_SPEC_ESTATE_BUILD.md` 문서 있으므로 허용 — 문서 전 금지 풀림, INBOX 처리됨 참조)

코드칸 시도 결과 — 유일한 미구현 ✅ 갭이 가드레일로 막힘 (루프, 2026-08-20): 직전 트랙 폴리싱이라
이번은 코드칸(기획서 ✅ · 소비처 0곳)이어야 했다. 서브에이전트 딥서치로 원장 전체를 훑어 미구현 ✅를
하나 찾았다: **§요약표(원장 line 1906) `전투 스타일 | 타겟 우선순위 토글` = CombatStyleDef.cs:24
`bool 정예우선타겟=false` 정의만 있고 소비처 0곳**(grep `정예우선타겟\|EliteFirst` → 정의 1줄뿐, W3Party
StyleSpec은 이 필드를 캐시조차 안 함, 타겟팅은 전부 NearestMob). 배선하려면 **W3Party 근접 딜러 타겟
선정부(W3Party.cs:1886 계열)에서 `_mKind[i]>=3` 정예를 우선 고르도록 전투 AI를 바꿔야** 한다. 그런데
이번 루프 명시 가드레일은 「W3Party는 전투 HUD 크롬(글씨·여백)만, 전투 수치·AI는 안 건드린다」다 —
정면 충돌. UI 토글만 배선하면 효과 0인 죽은 컨트롤이라 「정의만 있고 소비처 0」 안티패턴을 오히려
재생산한다. **막힘으로 기록**하고(오너/미래 세션이 「전투 AI 타겟 우선순위」를 열지 결정 필요) 대신
안전한 진행인 폴리싱 파티를 돌렸다. 코드 갭은 이 항목 하나만 남았고 가드레일로 봉인 상태다.

폴리싱(파티) 이번 이터 결과 — 결함 0 클린 (루프, 2026-08-20): `폴리싱 다음`이 「파티」라 `go:Party`를
meas 배치 빌드(오너가 `unity/` 에디터 6000.5.6f1 점유 중 — `sync_meas.sh` 후 `GAME_PROJ=unity_meas`·
`UNITY_BIN=6000.5.6f1`로 우회)로 프레임 200에 캡처. 전수 육안(헤더 부제·탭 편성/출전·5장 카드·하단
5칸 나브·크롭 6종·포트레이트 확대 3종): 헤더 「파티 편성 … 1번 자리가 탱 자리다(§10-4) … 부활초 0/3」
오른쪽 끝 안 잘림, 5장 카드(탱커·물리딜러 삭제·마법딜러·힐러·서포터) 모두 골드 9-slice 프레임 정상
(축소 크롭에서 가운데 열이 얇아 보였으나 힐러·서포터 native 확대에서 동일 프레임 확정 — 프레임 결함
아님), 삭제 카드 흐림(alpha 0.45)·2줄 캡션(`환생석` 중간 줄바꿈)은 앞선 세션이 의도/§21-3 비결함으로
이미 판정(STATUS 이전 필드 클린 기록과 정합), 역할 배지(탱커=방패)는 포트레이트 하단 중앙 의도 배치·
겹침 없음, 나브 라벨(영지·필드·탑·월드맵·캐릭터) 칸 안. 잘림/겹침/흰 종이/낡은 텍스처/안 읽히는 글씨
**없음**. 증거 `output/qa/ashes-to-stars/party_polish_shots/qa_go:Party.png`. 코드 변경 없음(폴리싱=한 화면
한 결함, 파티는 클린). 폴리싱 다음을 월드맵으로 넘김. `W3Party`/Resources/`PartyScreen.cs`는 안 건드림.

폴리싱(캐릭터) 이번 이터 결과: 직전 트랙 폴리싱이라 폴리싱 칸(코드 ✅ 소비처 0곳 갭은 감사 `a0065493`에서
소진 확인, 재감사 말고 폴리싱 돌리라는 인계). `폴리싱 다음`이 「캐릭터」라 `go:Character`를 meas 배치 빌드
(오너가 `unity/` 에디터 6000.5.6f1 점유 중 — `sync_meas.sh` 후 `unity_meas`로 우회, `UNITY_BIN=6000.5.6f1`)로
프레임 200에 캡처. **결함 1건 발견·수정**: 장비 패널 헤더가 제목(「탱커·탱」)과 「전투력 N」 **두 줄**이었는데,
둘째 줄 「전투력」이 초상 위 6칸 링의 **상단 슬롯(투구) 라벨과 같은 좁은 상단 밴드를 다퉈 겹쳤다**(투구 글씨가
전투력 글씨 위에 올라타 둘 다 안 읽힘 — 원해상도 크롭 `qa_before_helm_over_power.png`에서 직접 확인). **수리**:
「전투력」을 헤더에서 떼어 **오른쪽 정보 패널(DrawInspectInfo) 첫 스탯 줄**로 이동(전투력은 스탯이라 논리적
위치, Lv·경험 바로 아래). 헤더는 제목+목숨 **한 줄**만 남아 투구 라벨이 빈 배경 위에 읽힌다. 링 지오메트리·
`EquipRingDegrees`·`LargeLook`·`RosterSplit`은 불변이라 `CharacterRosterSelfCheck`와 무관(RED 없음).
**통과 기준**: 상단 밴드에 「투구」만 있고 「전투력」 텍스트 겹침 없음 + 정보 패널에 「전투력 1,200」 표시 +
컴파일 error CS 0(qa_shot 빌드 게이트 통과·화면 렌더=코드 반영). **네거티브**: 전투력 Hint를 헤더 `chrome.y+28`
둘째 줄로 되돌리면 투구-전투력 겹침 재발(**편집 전** 동일 env 캡처에서 직접 확인). 증거
`output/qa/ashes-to-stars/char_polish_shots/qa_go:Character.png`(after 전체) + `qa_before_helm_over_power.png`
(before 겹침) + `qa_after_helm_clear.png`(after 상단 밴드). `CharacterScreen.cs` 한 파일. `W3Party`/Resources/
`EstateBuild.cs`/링 지오메트리는 안 건드림. 코드 `24adc88f`. 폴리싱 다음을 파티로 넘김.

폴리싱(필드) 이번 이터 결과: 직전 트랙 폴리싱이라 폴리싱 칸(코드 ✅ 소비처 0곳 갭은 감사 `a0065493`에서
소진 확인, 재감사 말고 폴리싱 돌리라는 인계). `폴리싱 다음`이 「필드」라 필드 화면을 봤다. `go:Field`를
meas 배치 빌드(오너가 `unity/` 에디터 점유 중 — `-useHub` Unity 6000.5.6f1 + 임포트 워커 2종 확인,
`sync_meas.sh` 후 `unity_meas`로 우회, `UNITY_BIN=6000.5.6f1`)로 프레임 200에 캡처. **결함 0**: 헤더
제목·부제(`비살상 훈련 … 보유 2211골드 … 사냥 가죽 7`, 오른쪽 끝 안 잘림), 6장 카드(사냥 시작·던전
입장·지갑 잠김 플레이스홀더·저체력 귀환·일정 사냥·사망 없음), 하단 5칸 나브 도크, `ESC — 영지로`
힌트(필드는 영지로 가는 허브라 정상 유지) 모두 정상 렌더. 잘림/겹침/흰 종이/낡은 텍스처/안 읽히는
글씨 **없음**. 두 의심 지점을 소스로 교차 확인해 결함 아님을 확정: ① 헤더 꼬리 「사냥 가족 7」은
`GameState.BagText()`의 `CraftHide="사냥 가죽"` 7개(픽셀 폰트 죽→족 오독). ② 우상단 카드는
`FieldScreen.cs:188`의 지갑/소지품 잠김 플레이스홀더(`WalletText` 제목 + `building_auction` 창고
아이콘) — 지갑값이 헤더 부제와 중복이나 콘텐츠 판단이라 §21-3(임의 재튜닝 금지)로 손대지 않음
(캐릭터 6칸 링 중복·파티 삭제 캡션 줄바꿈과 동일 판정, STATUS 이전 필드 클린 기록과 정합). 증거
`output/qa/ashes-to-stars/field_polish_shots/qa_go:Field.png`. 코드 변경 없음(폴리싱=한 화면 한 결함,
필드는 클린). 폴리싱 다음을 캐릭터로 넘김. `W3Party`/Resources/`FieldScreen.cs`/`EstateBuild.cs`는 안 건드림.

폴리싱(영지) 이번 이터 결과: 직전 트랙 폴리싱이라 폴리싱 칸(코드 ✅ 소비처 0곳 갭은 감사 `a0065493`에서
소진 확인, 재감사 말고 폴리싱 돌리라는 인계). `폴리싱 다음`이 「영지」라 영지 화면을 봤다(문서
`GAME_SPEC_ESTATE_BUILD.md` 있어 영지 폴리싱 허용). `go:Estate`를 meas 배치 빌드(오너가 `unity/` 에디터
점유 중 — 6000.5.6f1 + 임포트 워커가 원본 잡음 확인, `sync_meas.sh` 후 `unity_meas`로 우회,
`UNITY_BIN=6000.5.6f1`)로 프레임 200에 캡처. 전수 육안 검사(헤더·탭·건물 8동 접지·팔레트·나브·크롭 6종):
건물은 그록의 자리·앵커 커밋 `6a1f3bdb` 이후 마름모에 접지·이웃 안 넘음(정상), 텍스처는 현행 다크판타지
전용 아트(재생성은 그록 §4-B, 루프 재생성 금지라 손 안 댐), 헤더 「영지」·「건물…압류」·「침략 북 3칸」의
자모 압축은 전 화면 공용 픽셀 폰트(§21-3, 안 건드림). 헤더 부제의 파산 압류 문구는 이 기계 PlayerPrefs에
남은 과거 `QA_BANKRUPT_SEIZE=1` 시드 잔재(`BankruptcySeize._applied` 영속)라 실제 플레이어가 보는 값이
아님 — 코드 결함 아님, 손 안 댐. **결함 1건 발견·수정**: 방어 건설 팔레트 4타일(화살탑·마법탑·성벽·함정)의
아이콘 중 **마법탑이 `field`(풀밭)**, **함정이 `building_barracks`(수비대 건물, 중복)** 로 오배정돼 던전 노드
등대 결함(`fb7cd614`)과 같은 계열의 부적절 아이콘이었다. **수리**: `UiAtlas.BuildingKey`에서 마법탑→`buffer`
(마법 지팡이, 화살탑 `tower`와 구분)·함정→`damage`(위해 교차검, 수비대 건물과 중복 해소). 화살탑·성벽·나머지
건물 키는 불변. 소비처는 팔레트 3곳(`EstateScreen.cs:444·584·672`)뿐이고 `UiAtlasSelfCheck`는 대장간·경매장·
영묘·수비대·없는건물만 단언해 마법탑·함정 변경과 무관(RED 없음). **통과 기준**: 마법탑 타일이 지팡이 아이콘
(화살탑 탑과 다름)·함정 타일이 붉은 교차검(수비대 건물 아님) + 컴파일 error CS 0(qa_shot 게이트 통과).
**네거티브**: 두 case를 `field`/`building_barracks`로 되돌리면 마법탑에 풀밭·함정에 수비대 건물이 재등장
(**편집 전** 동일 env 캡처에서 직접 확인 — 첫 캡처 크롭에서 풀밭·건물 아이콘). 증거
`output/qa/ashes-to-stars/estate_polish_shots/qa_go:Estate.png`(after 전체) + `qa_palette_icons_after.png`
(4타일: 탑·지팡이·성벽·붉은검). `W3Party`/Resources/`EstateYard.cs`/`EstateBuild.cs`/아트는 안 건드림 —
`UiAtlas.cs`(공용 아틀라스 헬퍼) 한 파일. 코드 `e5aa6b05`. 폴리싱 다음을 필드로 넘김.

폴리싱(타이틀) 이번 이터 결과: 직전 트랙 폴리싱이라 폴리싱 칸(코드 ✅ 소비처 0곳 갭은 감사 `a0065493`에서
소진 확인, 재감사 말고 폴리싱 돌리라는 인계에 따름). `GAME_START=go:Title`을 unity_meas 배치 빌드
(오너가 `unity/` 에디터 점유 중 — Unity 6000.5.6f1 + 임포트 워커가 원본 잡음 확인, `sync_meas.sh` 후
`unity_meas`로 우회, `UNITY_BIN=6000.5.6f1`)로 프레임 200에 캡처. **결함 1건 발견·수정**: 세 메뉴 카드 중
「게임 시작」·「이어하기」는 아이콘(영지 성·파티)+좌측 글씨인데 「종료」만 아이콘 인자 없이 `DrawCard`를 불러
글씨가 카드 좌측 프레임에 붙고 **아이콘 자리만큼 왼쪽으로 어긋나** 두 카드와 정렬이 깨졌다(육안: 종료 글씨가
위 두 카드보다 ~110px 왼쪽). **수리**: `DrawCard`에 옵트인 `center` 플래그 추가 — `center && 아이콘없음 && !slim`이면
새 중앙 정렬 스타일(`_cardTitleC`·`_h2C`, MiddleCenter)로 그린다. 아이콘 있는 형제 카드는 그대로(좌측). 다른
호출부는 `center` 기본값 false라 무변화(블라스트 0). `TitleScreen`의 「종료」만 `center:true`. **통과 기준**:
「종료」 카드 글씨가 카드 중앙에 위치 + 위 두 카드는 아이콘-좌측 유지 + 컴파일 error CS 0(qa_shot 게이트 통과).
**네거티브**: `TitleScreen`의 `center:true`를 빼면 「종료」 글씨가 다시 좌측 프레임에 붙어 어긋난다(이번 이터
**편집 전** 동일 env 캡처에서 직접 확인 — `qa_go:Title.png` before가 좌측 붙음). 증거
`output/qa/ashes-to-stars/title_polish_shots/qa_title_quit_center_after.png`(after, 종료 중앙 정렬). 헤더 「재와 별」
「와」자 픽셀 압축은 전 화면 공용 폰트 특성(§21-3, 안 건드림). `W3Party`/Resources/`EstateBuild.cs`는 안 건드림 —
`GameScreen.cs`(공용 카드 헬퍼)·`TitleScreen.cs` 두 파일. 코드 `0aa3673c`. 폴리싱 다음을 영지로 넘김.

폴리싱(전투HUD) 이번 이터 결과: 직전 트랙 폴리싱이라 폴리싱 칸(코드 ✅ 소비처 0곳 갭은 직전 감사 `a0065493`에서
소진 확인, 재감사 말고 폴리싱 돌리라는 인계에 따름). `GAME_START=hunt` 필드 전투 HUD를 meas 배치 빌드
(오너가 `unity/` 에디터 점유 중 — Unity 6000.5.6f1 + 임포트 워커가 원본 잡음 확인, `sync_meas.sh` 후
`unity_meas`로 우회)로 프레임 600에 캡처. **결함 1건 발견·수정**: 상단-좌 전투 요약 패널(`{시작웨이브}-{wave}·파티명`,
`처치·초`)과 상단-우 보상 레일(스킬 자동 토글 + 골드/경험 칩 + 연속·분당 칩)이 반투명 프레임만 그려
들판의 나무·집 배경이 안쪽으로 비쳐 글씨가 안 읽혔다(크롭 육안 확인: 나무 줄기가 패널 안까지 관통).
중앙 스킬 콜아웃·하단 4파티 카드는 어두운 영역/불투명이라 정상 → 원인은 배경 비침. **수리**: 신규 공용
헬퍼 `W3Party.DrawHudBacking(rect, alpha)`가 `UiAtlas.ContentRect`로 프레임 안쪽 사각을 구해 불투명 어둠
(`.03,.05,.09`, α=`0.88*alpha`)을 **프레임 그리기 전에** 깐다. `DrawCombatSummary`의 left·right 패널과
`DrawHudChip`(보상 칩) 세 곳이 재사용(같은 로직 3곳 흩어짐 방지, 가드레일). 프레임 α(0.55/0.50)는
그대로라 장식 테두리 유지. **통과 기준**: 상단 두 패널 안쪽이 배경과 무관하게 어둡고 글씨가 바탕 위로 읽힘
+ 컴파일 error CS 0(qa_shot 게이트 통과). **네거티브**: `DrawHudBacking` 호출 3곳을 빼면 나무·집 위에서
글씨가 다시 묻힌다(이번 이터 **편집 전** 동일 env 캡처에서 직접 확인 — 크롭에서 줄기 관통·저대비). 증거
`output/qa/ashes-to-stars/combat_hud_polish_shots/qa_hunt_backing_after.png`(after 전체) +
`crop_left_after.png`·`crop_right_after.png`(패널 안쪽 어두워짐). 남은 자모 압축은 전 화면 공용 픽셀 폰트
특성(§21-3, 안 건드림). 전투 수치·AI는 안 건드림 — `W3Party.cs` HUD 크롬만. 코드 `da802283`.
폴리싱 다음을 타이틀로 넘김.

폴리싱(결과) 이번 이터 결과: 직전 트랙 폴리싱이라 폴리싱 칸. `폴리싱 다음`이 「결과」라 결과 화면을 봤다.
`go:Result`(빈 상태 참고)와 `QA_GEAR_DROP=1 QA_HUNT_GOLD=1 go:Result`(보상 채운 상태) 두 장을 meas 배치
빌드(오너가 `unity/` 에디터 점유 중 — `Temp/UnityLockfile` 존재, `unity_meas`로 우회)로 캡처. **결함 1건
발견·수정**: 보상 줄(Info/RewardInfo)이 많아지면 하단 두 장 선택 카드(DrawChoice, `r.yMax` 앵커)를 침범해
「획득: 고급 가죽…」 줄이 「계속」 버튼 **뒤에 겹쳐** 보였고, 텍스트만 잘려도 `RewardInfo`가 아이콘을
무조건 그려 선택 밴드 위에 **고아 armor 아이콘**이 떴다. `ResultScreen.Body`가 이른 반환 뒤 DrawChoice와
**같은 공식**(`Min(168,Max(100,h*0.42))`)으로 선택 밴드 높이를 본문 `r`에서 예약 → Info의 하단 가드가
겹침을 막는다. 최종 DrawChoice는 원래 전체 높이 `full`로 앵커 유지. `RewardInfo` 아이콘도 같은 가드를 태워
고아 아이콘 제거. **통과 기준**: 보상 채운 결과 화면에서 보상 줄·아이콘이 「계속/영지로」 카드와 겹치지 않음
+ 컴파일 error CS 0(qa_shot 게이트 통과). **네거티브**: 예약(`r.height - choiceH - 16f`)을 원래 `r`로 되돌리면
「획득: 고급 가죽」 줄+armor 아이콘이 「계속」 버튼과 재겹침(before 샷과 동일). 증거
`output/qa/ashes-to-stars/result_polish_shots/qa_go:Result.png`(after, 겹침 없음) — before는 커밋 `176316ac`
직후 동일 env 캡처에서 겹침 확인. `W3Party`/Resources/`EstateBuild.cs`는 안 건드림 — `ResultScreen.cs` 한 파일.
폴리싱 다음을 전투HUD로 넘김. 코드 `PENDING_HASH`.

폴리싱(던전) 이번 이터 결과: 직전 세션이 던전 TemplateKo 폴리싱을 닫았으나 `go:Dungeon` 샷이
빈 상태("진행 중인 던전이 없다")라 노드 맵을 못 봐서 육안 확인이 미완이었다(STATUS 이전 기록).
이번에 **`GAME_START=dungeon`**(DebugAutoPilot 폴백이 `DungeonRun.Begin` 후 노드 맵을 연다)로
재캡처해 노드 맵을 육안 확인. **① TemplateKo 확정**: "1. 전투" 부제 `동시 105체 · 원거리 20% · 병목` —
`choke` enum이 아니라 한글 "병목"으로 렌더, enum 누출 0(직전 폴리싱 육안 확인 완료). **② 결함 1건 발견·수정**:
`DungeonScreen.cs:112`가 모든 노드 카드에 하드코딩 `"tower"` 아이콘을 써서 **전투 노드에 등대(탑)**가 떴다
(낡은/부적절 텍스처 계열). 신규 `NodeIconKey(NodeKind)` — 전투·정예=`damage`(교차 검)·강화=`buffer`·
보상분기=`rarity_rare`·보스=`tower`(종점 랜드마크 §5). 육안 확인: 재캡처에서 전투 노드가 교차 검으로 바뀜
(등대 사라짐), 던전 포기 카드는 `heart_broken` 유지. 잘림/겹침/흰 종이/안 읽히는 글씨 **없음**. 헤더 부제
"드"자 픽셀 압축은 전 화면 공용 폰트 특성이라 §21-3로 안 건드림.
**통과 기준**: 전투 노드 아이콘이 교전(damage)으로 렌더 + 컴파일 error CS 0(qa_shot 게이트 통과).
**네거티브**: `NodeIconKey(...)`를 `"tower"`로 되돌리면 전투 노드에 등대가 재등장(TemplateKo 이전 상태).
증거 `output/qa/ashes-to-stars/dungeon_polish_shots/qa_dungeon_node_icon_fix_20260819.png`(after, 교차 검) +
`qa_go:Dungeon.png`(빈 상태 참고). `W3Party`/Resources/`EstateBuild.cs`는 안 건드림 — `DungeonScreen.cs` 한 파일.
폴리싱 다음을 결과로 넘김. 코드 `fb7cd614`.

폴리싱(월드맵) 이번 이터 결과: `go:WorldMap` meas 배치 빌드(sync_meas 후, 오너가 unity/ 에디터 점유 중 —
`Temp/UnityLockfile` 존재 확인)로 재캡처. **결함 0**: 헤더 제목 `월드맵`+부제(`내 별 30층 · 별 ×1.60 ·
영공 4.0 · 침략은 탑 30층(§14·§15)`, `(§14·§15)`까지 안 잘림), 별 배너 카드, 4칸 도크(성계 이동/잠김—로컬
허브만 · 침략/북 3칸·출정 · 랭킹/잠김—서버 없음 · 수비대 0/5/잠김—침략 없음, 전부 한 줄·읽힘), 하단 나브
도크(영지·필드·탑·월드맵·캐릭터), `ESC — 영지로` 힌트(월드맵은 영지로 가는 허브라 정상 유지 — ESC 힌트
씬 조건부 폴리싱과 정합) 모두 정상 렌더. 잘림/겹침/흰 종이/낡은 텍스처/안 읽히는 글씨 **없음**. 헤더 제목
`월드맵`의 `드` 글자가 픽셀 압축에서 ㅡ 모음이 얇아 `월ㄷ맵`처럼 보이나, 이는 프로젝트 공용 기본 GUI 폰트
특성(모든 화면 도크 라벨에도 동일)이고 `UiPages.LabelClip`은 왜곡을 안 준다(소스 확인) — 월드맵 고유 결함이
아니고 폰트 재튜닝은 전 화면 영향이라 §21-3로 손대지 않음. 헤더 부제와 별 배너 카드의 내용 중복은 콘텐츠
판단(필드 지갑 카드 제목 중복과 동일 판정)이라 §21-3로 손대지 않음. 증거
`output/qa/ashes-to-stars/worldmap_polish_shots/qa_go:WorldMap.png`. 코드 변경 없음(폴리싱=한 화면 한 결함,
월드맵은 클린). 폴리싱 다음을 던전으로 넘김. `W3Party`/Resources/`WorldMapScreen.cs`·`EstateBuild.cs`는 안 건드림.

폴리싱(파티) 이번 이터 결과: 코드 트랙 소비처 0곳 갭이 소진돼 있어(직전 세션 전수 감사 `a0065493`,
"다음 코드 슬롯은 감사 재수행 말고 폴리싱 칸을 돌려라") 폴리싱 칸으로 진행. `go:Party` meas 배치 빌드
(sync_meas 후, 오너가 unity/ 에디터 점유 중 — `Temp/UnityLockfile` 존재 확인) 재캡처. **결함 0**:
파티 편성 5카드(탱커·물리딜러[삭제됨, 0.45 틴트+깨진 하트]·마법딜러·힐러·서포터) 전신 스프라이트·
프레임·직업 엠블럼·하트 3칸, 편성/출전 탭, 헤더 부제(`최대 5인(§9)·편성 4명·1번 자리가 탱 자리다
(§10-4 진형)·부활초 0/3`), 하단 나브 도크(영지·필드·탑·월드맵·캐릭터), `ESC — 영지로` 힌트 모두 정상
렌더. 잘림/겹침/흰 종이/낡은 텍스처/안 읽히는 글씨 **없음**. 삭제된 물리딜러 카드의 상태 문구
(`삭제됨 — 환생석으로만 복구(§4)`)가 3열 카드 폭 제약으로 `환생/석으로만` 줄바꿈되나, 완전히 읽히고
하트가 오른쪽 markW(80)를 점유해 nameR을 넓힐 수 없으므로 §21-3(임의 재튜닝 금지)로 손대지 않음
(캐릭터 폴리싱의 빈 슬롯 캡션 dimming·필드 지갑 제목 중복과 동일 판정). 헤더 부제 소스 확인:
`PartyScreen.cs:22`이 `§10-4 진형`(정자, "진혈" 아님) — 픽셀 육안 오독 배제. 증거
`output/qa/ashes-to-stars/party_polish_shots/qa_go:Party.png`. 코드 변경 없음(폴리싱=한 화면 한 결함,
파티는 클린 — STATUS 이전 기록 「필드·캐릭터·파티…결함 0」과 정합). 폴리싱 다음을 월드맵으로 넘김.
`W3Party`/Resources/`PartyScreen.cs`·`EstateBuild.cs`는 안 건드림.

이번 이터 결과(직전 트랙 폴리싱 → 코드 칸): **클린 비전투 ✅ 소비처 0곳 갭이 소진됐다.** 원장 246개 ✅를
전수 대조(직접 grep 15개 시스템 + Explore 서브에이전트 전수)한 결과, gr록이 손댈 수 있는 비전투 ✅는
**전부 소비처가 있다**(전직·합성·경험치 분배·티어 선택·목숨 상한 3/5·드랍률 8/15/10/1·대출 이자·건설 단축·
오프라인 정산·침략 비용·별 크기/디버프·보스 스킬/다중·명예·경매·가방·장비 드랍/옵션·시세 전부 배선됨).
**남은 0-소비처 오펀은 전부 손댈 수 없는 것뿐**: ①`RaceDef.방어배율`·`이속배율`(전투 스탯 → W3Party, 루프 금지)
②G16 로컬라이제이션·G17 접근성(전 화면 다중 서브시스템, 단일 소비처 칸 아님 · G17은 프로토 OUT §21-3)
③환생 스킬 선택·탐험 +30%·건물 내구·수비 명예·착용 레벨(선행 부재 오펀, `fd2287fc`에 기록, 오너 결정 대기)
④동맹·랭킹·별 모양 10층 연출(서버/💡). **다음 코드 슬롯 세션은 이 감사를 재수행하지 말고 폴리싱 칸을 돌려라** —
새 ✅가 원장에 추가되거나 위 오펀의 선행(안개·건물HP·인바운드PvP·착용레벨 확정치)이 열릴 때까지 코드 갭은 없다.

폴리싱(캐릭터) 이번 이터 결과: `go:Character` meas 배치 빌드(sync_meas 후, 오너가 unity/ 원본 점유 중이라 unity_meas
사본) 재캡처. **결함 0**: 명부 5카드·하트, 오른쪽 장비 6칸 리스트(무기·투구·갑옷·장갑·신발·장신구 전부 읽힘)·
전투력·경험·가방 4칸·전신 스프라이트·파티 4/5·자동 장착·지갑·나브 도크 모두 정상 렌더. 잘림/겹침/흰 종이/
낡은 텍스처/안 읽히는 글씨 **없음**. 바닥 왼쪽 「ESC — 영지로」 힌트 유지(캐릭터는 영지로 가는 허브라 정상,
ESC 힌트 씬 조건부 폴리싱과 정합). 초상 둘레 6칸 링 캡션이 흐린 것은 빈 슬롯 스타일(alpha 0.28)이고
오른쪽 리스트에 같은 정보가 선명히 있어 중복이라 §21-3(임의 재튜닝 금지)로 손대지 않음(필드 지갑 카드
제목 중복과 동일 판정). 증거 `output/qa/ashes-to-stars/char_polish_shots/qa_go:Character.png`. 코드 변경 없음
(폴리싱=한 화면 한 결함, 캐릭터는 클린). 폴리싱 다음을 파티로 넘김. `W3Party`/Resources/`EstateBuild.cs`는 안 건드림.


오너 INBOX 지시(막힌 것 풀기, 2026-08-19 20:24·20:20) 이번 이터 결과: INBOX가 큐보다 앞서므로 먼저 잡음.
**① 환생·탐험·내구·수비명예·착용레벨** — 5종 코드로 검증, 넷은 선행 시스템 부재로 오펀 확정·하나는 이미 배선. 조용히 안 넘김.
  - 환생: 핵심 배선 있음(`UseRebornStone`→`Rebirth.Apply`, `EstateScreen.cs:1002`). 남은 「환생 스킬 1개 선택」은 선택 스킬 슬롯 부재(직업 스킬 Job+단계 고정).
  - 탐험 +30%: `WorldStar.cs:12` — 전장의 안개 + 원격 별(서버) 부재. 선행: 안개 가시성 + 멀티플레이.
  - 내구(건물 +20%): 건물 HP 시스템 0곳. 선행: 건물 HP·파괴.
  - 수비 명예 +20: `Honor.cs:9` — 들어오는 침략 없음(서버 없음). 선행: 인바운드 침략(PvP/서버).
  - 착용레벨: 확정 요구 수치 없음, §1840 「Lv1이 30층」이라 레벨 게이트가 확정 설계와 상충. 선행: 오너 요구 레벨 확정치 또는 OUT.
  상세·선행은 `docs/feedback/INBOX.md` 「대기 중」 각 항목 진행 노트. 넷은 큰 서브시스템이라 한 이터 범위 밖 — 오너 결정/설계 확장 대기.
**② 루프 꺼짐** — 가동 확인: `loop/loop.sh` PID 10072 실행 중·STOP/HOLD 마커 없음. 이 이터가 실행 중 = 루프 온(20:20 과거 상태).
**③ 필드 폴리싱(직전 트랙 코드→폴리싱 한 칸)** — `go:Field` 재캡처. 결함 0 재확인: 잘림/겹침/흰 종이/낡은 텍스처/안 읽히는 글씨 **없음**.
  6장 카드·헤더·도크·ESC 힌트 모두 정상 렌더. 지갑 카드 제목이 헤더와 값 중복(§18 layout_waste 계열)이나 이는 콘텐츠 판단이지
  시각 결함이 아니라 §21-3(임의 재튜닝 금지)로 손대지 않음. 증거 `output/qa/ashes-to-stars/field_polish_shots/qa_go:Field.png`
  (unity_meas 배치, 오너가 unity/ 에디터 점유 중). 폴리싱 다음을 캐릭터로 넘김. 코드 변경 없음(디렉티브=문서만, 필드=클린).

코드(§18-14 오프라인 정산 감쇠) 이번 이터 결과: 직전 트랙이 폴리싱이라 코드 칸. 원장 §18-14 안전장치·§22 상시관측 #5가
「오프라인 정산은 기본 기능」이라 명시하는데, `EstateMine.Tick`이 `_lastUnix` 타임스탬프로 오프라인 경과를 **전 구간 100%로**
정산해 방치 채굴 농장 억제(§19)가 안 되던 소비처 0곳 갭. 신규 `Runtime/OfflineSettle.cs` — `EffectiveSeconds(elapsed)`가
8h까지 100%·8~12h 50%·12h 초과 버림(실효 상한 10h=36000초). 온라인 틱은 경과가 작아 항등(100%)이라 회귀 없음.
소비처: `EstateMine.Tick`의 `_owed += rate * OfflineSettle.EffectiveSeconds(elapsed)`. §18-14 값 그대로(수치 튜닝 금지 §21-3).
**통과 기준**: `GameFullCheck` 전수(unity_meas 배치, 오너가 unity/ 에디터 점유 중이라 사본)에서 `OfflineSettleSelfCheck` PASS —
순수 곡선(1h→1h·8h→8h·10h→9h·12h→10h·24h→상한 10h) + 광산 Tick 실소비(24h 오프라인→실효 10h=25000쿠퍼) + 소비처 grep.
error CS 0. 광산 관련 SelfCheck 전부 PASS(EstateMine·MineSeize·EstateRaceMine·NetWorth·SoftCap). 증거
`output/qa/ashes-to-stars/full_check/offline_settle_selfcheck_20260819.log`.
**네거티브(SelfCheck 내장·실측 PASS)**: `QA_NO_OFFLINE_DECAY=1`이면 24h를 그대로 정산(60000쿠퍼=옛 100%), `감쇠가 실제로
정산량을 줄인다`(25000<60000), EstateMine.cs에서 `OfflineSettle.EffectiveSeconds` 배선을 지우면 grep 단언 FAIL.
**주의**: 이번 전수에서 내 변경과 무관한 기존 FAIL이 있었다 — `AdvLookSelfCheck`(10, 전직 스프라이트 look, meas 사본 에셋 의존),
`HuntBoonSelfCheck`(1, 가로 카드 금테), `BossBattleDpsSelfCheck`(NRE, W3Party), `ChatWorkBatchSelfCheck`(1). 전부 OfflineSettle·광산과
무관(다른 서브시스템). `W3Party`/Resources/`EstateScreen.cs`는 안 건드림 — `EstateMine.cs` 한 줄 + 신규 2파일. 코드 `e8cb5d78`.
⚠️ 영지 폴리싱은 `docs/GAME_SPEC_ESTATE_BUILD.md`대로만 — 문서 없이 영지 재설계 금지(오너 2026-08-18). 문서는 있으니 그걸 읽고 한 결함만.

폴리싱(영지) 이번 이터 결과: `qa_go:Estate` 샷에서 바닥 왼쪽 힌트가 「ESC — 영지로」였는데, 영지는 허브 루트라
`GameScreen.Update`의 ESC가 (도크 있는 화면이면) `GameFlow.Go(GameFlow.Estate)` = **제자리 재로드**다(갈 곳이 없다).
그 화면에서 「ESC — 영지로」라 적으면 눌러서 어딘가 이동한다고 오해한다(타이틀 「ESC — 종료」 폴리싱 313892f2와 동형).
`GameScreen.BottomBar`의 힌트 그리기를 `SceneManager.GetActiveScene().name != GameFlow.Estate`일 때로 조건부:
영지에선 숨기고, 실제로 영지로 가는 다른 허브(필드·탑·월드맵·캐릭터)에선 유지. ESC 키 입력·다른 화면 크롬은 안 건드림.
**육안 확인**: meas 배치 빌드 후 재캡처. 영지 `qa_go:Estate.png` 바닥 왼쪽 힌트 사라짐(증거
`output/qa/ashes-to-stars/hud_shots/qa_estate_esc_hint_removed_20260819.png`), 필드 `qa_go:Field.png`엔 「ESC — 영지로」 유지
(증거 `qa_field_esc_hint_kept_20260819.png`). 네거티브: 씬 조건 제거하면 영지에도 힌트 재등장 = 제자리 이동을 「간다」로 오표기.
두 빌드 모두 error CS 0(qa_shot 컴파일 게이트 통과). `W3Party`/Resources/`EstateScreen.cs`·`EstateBuild.cs`는 안 건드림 —
`GameScreen.cs` 한 파일. 커밋 대기.

폴리싱(타이틀) 이번 이터 결과: `qa_go:Title` 샷에서 바닥 왼쪽 힌트가 「ESC — 뒤로」인데, 타이틀은 루트라
`GameScreen.Update`의 ESC가 `!ShowBottomBar && Title=="재와 별"`이면 **`GameFlow.Quit()`**(뒤가 없어 종료)이다.
「뒤로」라 적으면 ESC로 게임이 닫히는 걸 「돌아간다」로 오해한다. `GameScreen.OnGUI` ESC 힌트 그리는 곳
한 줄만 조건부로: `Title=="재와 별" ? "ESC — 종료" : "ESC — 뒤로"`. 다른 화면(도크 없는 것)은 여전히 「뒤로」(=영지로) 유지.
**육안 확인**: unity_meas 배치 빌드 후 재캡처 `qa_go:Title.png` 바닥 왼쪽 「ESC — 종료」. 증거
`output/qa/ashes-to-stars/hud_shots/qa_title_esc_quit_label_20260819.png`. before(직전 build) 샷은 「ESC — 뒤로」였다(네거티브:
`Title=="재와 별"` 분기 제거하면 타이틀도 「뒤로」로 회귀 = ESC=종료를 「뒤로」로 잘못 표기). meas 빌드 error CS 0(qa_shot 게이트 통과).
`W3Party`/Resources/`TitleScreen.cs`는 안 건드림 — `GameScreen.cs` 한 파일. 커밋 `313892f2`.

전투HUD 직전 트랙 결과: `BattleScreen`은 `ShowBottomBar=false`라 베이스 `GameScreen.OnGUI`가 바닥 왼쪽에
「ESC — 뒤로」+scrim(Rect(0,REF_H-34,360,34))을 그렸는데, W3Party 파티 카드가 바닥을 소유해 **1번 카드
체력바와 겹쳐 회색 글씨가 흰 수치("278/347")를 뭉갰다**(qa_boss.png 실측). `GameScreen`에 `ShowEscHint`
virtual(기본 true) 추가 → `BattleScreen`만 false로 override. ESC 키 입력은 그대로. **육안 확인**: 재캡처
qa_boss.png에서 회색 힌트 사라지고 1번 카드 "378/378" 깨끗. 증거 `output/qa/ashes-to-stars/hud_shots/
qa_boss_esc_hint_fix_20260819.png`. meas 빌드 error CS 0(qa_shot 컴파일 게이트 통과). 네거티브: `ShowEscHint`
override 제거하면 겹침 재발(before 크롭이 회색 오버랩 보임). W3Party 전투 수치·AI는 안 건드림. 코드 대기 커밋.
폴리싱 다음(과거 기록): 이어 타이틀 → 영지. 던전은 이전 이터가 닫음 —
노드 부제 `동시 64체 · 원거리 0% · pockets`처럼 `ArenaTemplate` enum(open_ring/pillars/choke/
pockets/arena_wide)이 한글 UI에 그대로 새던 것을 `TemplateKo`로 매핑. **⚠️ 이 세션 환경에서는
`ScreenCapture`가 저장 실패해(앱이 foreground 아님, Player.log `Failed to store screen shot` 3회)
확인 샷을 못 찍음** — 다음 세션이 `go:Dungeon` 샷으로 최종 육안 확인할 것.
필드·캐릭터·파티·월드맵·결과는 직전 이터가 훑어 결함 0. 영지는 `docs/GAME_SPEC_ESTATE_BUILD.md`대로
(클로드 EstateBuild와 겹치지 말 것).

## 다음 할 일 (원장 §22 — 위에서부터 하나만)
1. **영지 §4** — 클로드가 `EstateBuild.cs` 건물별 업그레이드 창. 그록은 그 파일을 안 만진다. StoreX 경로 소비처는 닫음(`EstateStore.Reached`). 남은 §5는 마우스 드래그 UX(TryMove는 있음). §6 아트는 그 다음.
2. **INBOX 22:03·22:04** — 몹 알파·서포터 반쪽·던전 입장 부제는 닫음. 남은 한 결함: 이펙트 위치/알파·생성. FX PNG는 다른 세션이 이미 수정 중이라 겹치지 말 것. 한 결함만.
3. **기획서 ✅ · 소비처 0곳** — 원장 `GAME_DESIGN_ASHES_TO_STARS.md`를 훑어 ✅인데 grep 소비처가 0인 칸 **하나만** 닫는다. §10-5 보스 스킬 수(중간 2→3·대보스 2→3→4·50층+ 2→3→4→5)·§10-3 계열 상성(×1.3/×0.7)·§18-13 별 인식(`1 + 층/10`)·§18-13 별 크기(`1 + 층×0.02`)·§18-11 대보스 개체 HP(2체 65·3체 45)·§10-2 정예 유형 1~2종(지도 Caption)·§10-7 탑 대보스 마릿수(60/30/10)·§18-10 레이드 벽(5층 ×1.5·10층 ×2.2)·§18-4 목숨 시세 상한(부활초 8·두루마리 4·환생석 300)·증표 시세 상한 400·§11 드랍 옵션 체력(`GearOpt.HpMul`→`EffectiveHpMul`)·경매 복원 등급·옵션·§13-3 창고 현재 칸 경로·§11 드랍 옵션 1~4·§10-8 정예 일반·보스 고급 장비·가방 60칸·무기 직업 계열·진입 면 선택은 닫음. 시각 UI「다른 게임만큼」·V2 손맛·V4 70%는 사람 관문이라 닫지 않는다. `W3Party`·오너 Unity는 건드리지 않는다. **중복 리소스 재생성 금지.** 통과 기준: 소비처 ≥1 + SelfCheck + 네거티브. 증거 없는 완료 금지. 직전 트랙이 UI면 이번은 이 칸. 직전 트랙이 코드면 **UI·아트 상시 폴리싱**.
3. **UI·아트 상시 폴리싱** — 코드 구멍 다음 이터에 화면 하나·결함 하나. 「다른 게임만큼」완료 금지. 할로우 화풍 강제 취소. 이미 있는 장을 쓰고, 있는 대상은 다시 뽑지 않는다. 샷+한 결함이 완료. 필드 지갑 `2147483647`·탑 2×2 전폭·월드맵 2×2 전폭·아틀라스 UV(heart/tower 이웃)·지갑 부제 줄바꿈·일정/저체력 도크 부제·탑 하위 레이드 도크 부제·탑 레이드(5층 단위) 도크 부제·월드맵 침략 카드 부제·월드맵 성계·랭킹 카드 부제·월드맵 수비대 카드 부제·배회 보스 도크 부제·레이드급 카드 부제는 닫음. HuntBoon 도크 `8a7e6b93`·글씨 `67664c3a`·던전 입장 부제는 닫음. 다음 화면: 사냥 시작 카드 부제가 길면 그 한 결함. 영지는 `docs/GAME_SPEC_ESTATE_BUILD.md`대로.
4. **INBOX 09:57 전체 그래픽 남은 것** — 필드 허브 HUD·전용 7건물·경매장 전용 그림·제목판 52·경매 전폭 막대·캐릭터창 3열·장비 라벨·현황 도크·도크 부제·마을 끌어 보기·굴려 확대는 닫음. 중복 리소스 재생성 금지. 캐릭터/몹 화질은 사람 육안. 새 생성 전 `ARTIFACT_INDEX`·대기 작업 확인.
5. **INBOX 08:47 지금 문제점** — 캐릭터·몹 움직임, 맵 전투에서 캐릭터/몹/배경 비율. 겹침은 대화 세션이 `fe2eb9c8`(필드 프랍)·`95886088`(파티 겹침)로 닫음. 움직임·비율은 `W3Party`라 대화 세션. UI 퀄리티 전체는 사람 육안. 금테·글씨 여백은 대화 세션 `0d8e50da`. 루프는 전투 밖만. 영지 전면 마을·영지 마을 HUD·필드 허브 HUD·전용 7건물·경매장 전용 그림·제목판 52·경매 전폭 막대·캐릭터창 3열·장비 라벨·현황 도크·도크 부제·마을 끌어 보기·굴려 확대는 닫음.
6. **UI 퀄리티 남은 것**(INBOX 16:46 · 21:45 · 08:37 글씨 위치는 닫음 · 09:18 영지 전면·끌어 보기·굴려 확대는 닫음 · 09:45 마을 HUD는 닫음 · 필드 6장 카드는 닫음 · 제목판 52는 닫음 · 경매 전폭 막대는 닫음 · 캐릭터창 3열·장비 라벨은 닫음 · 현황 도크는 닫음 · 도크 부제는 닫음) — 하단 도크·격자 8×8·시작 2명·침략 보호막·수비대 회복·인간 PvE 18h·경매 수수료 7%·영지생산·드랍률·전직재료배율·약탈량·엘프 인식·영공 적 디버프·드워프 골드 소모·약탈 상한·사냥 시작 두 단계·시간당 수익 소프트캡·승자 최소 0.5 G/h·창고 20% 약탈·명예 +30·반복 침략 −80%·신규 계정 7일 구매 잠금·5층 전 비살상 훈련·하위 레이드 스케일 0.65·하위 레이드 보스 풀 10종·재입장 누진 ×1·×2·×4·×8·경매 등록 24시간 유찰·연체 2회 생산 압류·파산 건물 −1·비장착 30%·환생 Lv1·필드 사냥 골드·마지막 목숨 장착 6부위·영묘 추모(층·출전·원인·장착·마지막 동료)·수비대 30층 해금·영묘 첫 삭제 해금·대장간 1차 전직 해금·경매 드랍·제작만 거래·필드 자동사냥 일정·10층 대보스 0.15 G/h·대출 순자산(장비·영지)·필드 배회 보스·긴급 탈출 보상 포기·넓은 카드 글씨 가운데·목숨 시세 하한·누적 출전·영지 전면 마을·증표 시세 200 G/h·영지 마을 HUD·필드 허브 HUD·전용 7건물·경매장 전용 그림·제목판 52·경매 전폭 막대·캐릭터창 3열·장비 라벨·현황 도크·도크 부제·마을 끌어 보기·굴려 확대·긴급 탈출 수동 한정·명예 승리 방어력 비례·침략 진입 면 선택·무기 직업 계열·가방 60칸은 닫음. 허브 마을 전경은 `9f4336f8`. 금테 여백은 `0d8e50da`. 전체 「다른 게임만큼」은 사람 육안.
7. **이미 닫은 소비처 목록** — 대기하지 말고 원장을 훑어 다음 구멍을 큐에 올린다. `BossSkills.PhaseCount`/`SkillsAt`/`Line`/`OldPhaseCount`를 `CreateBosses`·탑 자막·`UiAtlas.PhaseCountForFloor`가 읽음. 중간 2→3 · 대보스 2→3→4 · 50층+ 2→3→4→5. `QA_NO`면 옛 ≤5/≤10(15층 4페이즈). `FieldDockCap.Raid`/`OldRaid`/`RaidLine`을 `FieldScreen` 레이드급 카드가 읽음. `5인 · 환생석 없음`. `QA_NO`면 옛 `5인 전제 · 비용 · 환생석·증표 없음`. `FieldDockCap.Dungeon`/`OldDungeon`/`DungeonLine`을 `FieldScreen` 던전 입장 카드가 읽음. `랜덤 · 종점 보스`. `QA_NO`면 옛 `랜덤 생성 + 종점 보스 · 비용`. `FamilyAdv.Mul`/`Title`/`Line`/`OldTitle`을 `DungeonScreen` 제목·부제가 읽음. 야수+마법사 1.3 · 야수+궁수 0.7. `QA_NO`면 옛 `야수 계열`. 전투 배율은 W3Party라 안 넣음. `WorldStar.SenseMul`/`SenseBase`/`SenseLine`/`OldSenseBase`를 `WorldMapScreen` 자막·SizeLabel이 읽음. 1층 1.10 · 100층 11. `QA_NO`면 옛 4~16 선형. `WorldStar.SizeMul`/`SizePx`/`SizeLine`/`OldSizePx`를 `WorldMapScreen` 자막·별 아이콘이 읽음. 1층 ×1.02 · 100층 ×3. `QA_NO`면 옛 40~112 선형. `WorldMapDockCap.Defense`/`OldDefense`를 `WorldMapScreen` 수비대 카드가 읽음. 잠김=`침략 없음`. `QA_NO`면 옛 `침략 전투는 아직 없다(§13-5)`. `WorldMapDockCap.Star`/`Rank`/`OldStar`/`OldRank`를 `WorldMapScreen` 성계·랭킹 카드가 읽음. 잠김=`로컬 허브만` · `서버 없음`. `QA_NO`면 옛 미구현 설명. `BossHp.CountMul`/`CountLine`을 `CreateBosses`·탑 자막이 읽음. 2체 65 · 3체 45. `QA_NO`면 옛 DPS 100·자막 없음. `WorldMapDockCap.Caption`/`Open`/`Lock`/`Line`을 `WorldMapScreen` 침략 카드가 읽음. 잠김=`30층 해금` · 열림=`북 3칸 · 출정`. `QA_NO`면 옛 41·58자. `EliteKinds.Caption`/`Format`/`Line`을 `DungeonScreen` 정예 노드가 읽음. `QA_NO`면 옛 퍼센트 줄. 전투 기믹은 W3Party라 안 넣음. `TowerDockCap.Raid`/`OldRaid`를 `TowerScreen` 레이드(5층 단위) 도크가 읽음. 훈련=`비살상 · HP 1 귀환` · 5층=`5층 ×1.5` · 10층=`대보스 ×2.2`. `QA_NO`면 옛 24~42자. `BossCount.Begin`/`Fight`/`Of`/`FromRoll`을 `BattleScreen` 탑 대보스·드랍이 읽음. 10·20·…=60/30/10 · 5층·배회·던전은 1(던전은 Plan). `QA_NO`면 옛 1. `TowerDockCap.Lower`/`OldLower`/`CaptionFits`를 `TowerScreen` 하위 레이드 도크가 읽음. `QA_NO`면 옛 101자. `BossHp.WallMul`을 `Hp`가 읽고 `CreateBosses`가 Hp를 읽음. 5·15·…=×1.5 · 10·20·…=×2.2. 던전·배회는 1. `QA_NO`면 옛 1. `FieldDockCap.LowHp`/`Schedule`/`Death`를 `FieldScreen` 도크가 읽음. `QA_NO`면 옛 긴 줄. `LifePrice.Ceil`/`AboveCeil`/`CeilHoursOf`를 `TryListItem`·경매 자막이 읽음. T1 환생석 상한 300골드·부활초 8·두루마리 4. `QA_NO`면 옛 상한 없음. `GearOpt.Pack`/`Parse`/`ListLine`을 `TryListGear`·`RestoreListed`·캐릭터창이 읽음. `QA_NO`면 옛 `recipe|enhance`(일반·옵션 0). `GearOpt.Apply`/`CountOf`/`Format`/`Line`을 `TryGrantDrop`·캐릭터창이 읽음. `QA_NO`면 옛 0. 제작품은 0. 일반1·고급2·희귀3·영웅3·전설4. 전투 수치는 안 넣음. `EliteDrop.Apply`/`Applies`/`Format`/`Line`을 `DungeonRun.Complete`·전투·던전 지도·캐릭터창이 읽음. `QA_NO`면 옛 0. 필드 정예는 W3Party라 안 넣음. `GearDrop.Apply`/`GradeOf`/`Format`/`Line`을 `CalculateVictoryReward`·결과·캐릭터창이 읽음. `QA_NO`면 옛 0. 제작품은 일반. 정예 일반·랜덤 옵션은 안 넣음. `BagSlots.Used`/`CanGain`/`CanAddGear`/`Line`을 `Gain`·제작·벗기기·복원·캐릭터창·대장간이 읽음. `QA_NO`면 옛 무한. 목숨 아이템은 종류당 1칸, 비장착 장비는 1개 1칸. 골드 확장은 안 넣음. `EquipJob.CanWear`/`WhyNot`/`Line`을 `TryEquip`·캐릭터창·대장간이 읽음. `QA_NO`면 옛 항상 허용. 송곳니 검은 물리(탱·딜). 레벨 제한·다른 무기 레시피는 안 넣음. `InvasionApproach.Pick`/`Side`/`Path`/`Line`을 `TryBegin`·월드맵이 읽음. `QA_NO`면 옛 최단 자동. 경로 전투 시뮬은 💡라 안 넣음. `EstateBuildings.DedicatedOf`가 경매장=`estate_auction_0`을 읽고 `PropOf`가 마을에 그린다. `Honor.WinForCut`/`WinNow`를 `ApplyInvasion`·정산·월드맵이 읽음. `EscapeManual.Allowed`/`WhyNot`/`Line`을 `TryBegin`·전투·필드가 읽음. `EstateYard.SetZoom`/`HandleZoom`/`Zoom`/`Line`을 마을이 읽음. `EstateYard.TileOrigin`/`SetPan`/`HandlePan`/`Line`을 마을이 읽음. `EstateStatusHud.AuraCaption`/`KeepCaption`/`WorldCaption`/`MineCaption`/`StoreCaption`을 현황 도크가 읽음. `UiPages.IsSlimCard`/`TitleHOf`/`CardChrome`을 `CardLayout`·`DrawCard`가 읽음. `EstateStatusHud.Cards`/`OverlayH`/`OpenH`/`Line`을 영지 현황이 읽음. `CharHud.RosterSplit`/`RosterCell`/`EquipLabel`/`EquipRingFit`/`SlotLabel`/`Line`을 캐릭터창이 읽음. `AuctionHud.BarRect`/`LotsBody`/`StatusLine`/`Line`을 경매장이 읽음. `HubHeader.H`/`BodyTop`/`IconRect`/`TitleRect`/`Line`을 `GameScreen`이 읽음. `EstateBuildings.DedicatedOf`/`PropOf`/`Line`이 본성·광산·창고·수비대·대장간·영묘·탑·경매장을 읽음. `TowerHud.Cards`/`OverlayH`/`OpenH`/`Line`을 탑이 읽음. `FieldHud.Cards`/`OverlayH`/`OpenH`/`Line`을 필드 허브가 읽음. `EstateHud.OverlayH`/`PaletteTiles`/`ShowInspectBar`/`Line`을 영지 마을이 읽음. `TokenPrice.Floor`/`Ceil`/`BelowFloor`/`AboveCeil`/`Line`을 `ListPrice`·`TryListItem`이 읽음. 상한 400 G/h. `QA_NO`면 옛 25골드·상한 없음. `SortieTime.Apply`/`AddToIndexes`/`Line`을 전투·일정·영묘가 읽음. `LifePrice.Floor`/`BelowFloor`/`Copper`를 `ListPrice`·`TryListItem`·NPC가 읽음. `EscapeForfeit.Apply`/`Line`/`Body`가 긴급 탈출 포기를 읽음. `InvasionState.AbortPending`이 패배 추가 소모 없이 대기를 취소. `FieldBoss.Tick`/`BeginFight`/`DropSource`가 필드 배회 보스를 읽음. `NetWorth.Assets`/`KeepCopper`/`GearCopper`가 대출 한도의 장비·영지를 읽음. `Memorial.FormatParty`/`PartyLine`이 마지막 출전 동료를 읽음. `RaidCost.ActionKey`/`Copper`/`Line`이 10층 대보스 0.15 G/h를 읽음. `HuntSchedule.TryStart`/`Tick`/`Stop`/`PendingGold`가 필드 일정을 읽음. `AuctionTrade.CanList`/`CanListBound`/`TryFirstBag`가 드랍·제작을 읽고 칭호·스킨·명예는 거절. `TryListItem`이 `CanList`를 읽음. `Equipment.LockReason`/`LockLine`/`SeedUnlockQaIfRequested`가 1차 전직을 읽음. `Memorial.Unlocked`/`LockReason`/`Open`이 첫 삭제를 읽음. `DefenseState.Unlocked`/`LockReason`이 탑 30층을 읽음. `Memorial.Stamp`/`Line`/`GearLine`/`PartyLine`/`TimeLine`이 최고 층·마지막 출전·사망 원인·장착·동료·누적 출전을 읽음. `LastLifeWarn.GearLine`/`GearRest`가 장착 6부위를 읽음. `BattleScreen`이 `WaveHuntGold`를 Earn한다(T1 3600초=1골드). `UseRebornStone`이 `Rebirth.Apply`를 읽음. `ApplyBankruptcy`가 `BankruptcySeize`를 읽음. `EstateMine.Tick`이 `RepayFromIncome`을 읽음. `AuctionState.SweepExpired`가 `ListHours`를 읽음. `Honor.WinForCut`은 Cut 0=15·20=30·40=45. 수비 성공 +20은 들어오는 침략이 없어 안 넣음. `RaidReroll`/`RaidBossPool`/`RaidScale`/`DeathTraining`/`Earn`/`LootCopper`/`Honor.ApplyInvasion`은 닫힘. `RaceDef` 비전투 칸은 골드 소모까지 닫힘. 환생 스킬 1개 선택·생전 스킬 목록은 안 넣음(직업 스킬이 Job+단계라 칸이 없음). 누적 출전은 `SortieTime`이 닫음. 16-6 별자리 카드는 💡. 이속·체력은 W3Party가 이미 읽고, 방어배율·불굴·야성·이동회피·소환수 재소환은 W3Party라 대화 세션. 탐험 범위 +30%는 안개 시스템이 없어 안 넣음. 드워프 방어 내구는 건물 HP가 없어 안 넣음. 강화 성공 +10%p는 `SuccessPercent`가 이미 읽음. 오프라인 정산 60%·일과표 타임라인·조건부 지시는 💡라 안 넣음. 매칭 ±5층·디버프 중첩 2별은 로컬 별이 1개라 안 넣음. 수비 성공 명예 +20은 들어오는 침략이 없어 안 넣음. 16×16 부지·배치 프리셋·경로 전투 시뮬·무료 영입 3회·동시 건설 2슬롯·본성 유료 영입·명예 상점은 💡라 안 연다. 변종 패턴 1개·다중 3체·필드 보스 배회 스프라이트는 💡/W3Party. 잡몹 1마리 3~10쿠퍼는 시간당 공식으로 흡수했다. 16-10 전투 오디오·G17 접근성은 프로토 OUT(§21-3 사운드). 생존형 HP 50% 이탈은 W3Party. 다음 비전투 구멍은 원장 ✅를 다시 훑어 소비처 0곳인 것을 올린다.
8. **단계 1 관문 ① — 재측정 PASS(2026-08-18)** — `BossHp`가 §18-10 권장 전투력(1층=100, +5.5%/층)을 DPS로 읽고 `BossBattle`이 HP를 만든다. 재측정 G1 100→472 · 변화 29회 · G2 5h Lv36≥요구 30. 대화 세션 독립 재실행(2026-08-19)도 PASS — 벽 배율 병합 후 30층 1039(=472×2.2), 오너 승인 §18-11에 기록. CSV `output/qa/ashes-to-stars/curve/tower_climb_30.csv`. `QA_NO`면 옛 100. 장비 칸은 권장 전투력에 흡수(별도 시뮬 없음). 관문 ②(5시간 지루함)는 아직.

## 다음 할 일 큐 (루프가 못 닫은 것)

| # | 항목 | 이유 |
|---|---|---|
| 1 | **필드 정예·전투 보정** | 킬 카운트가 `W3Party` 안에만 있다. 루프는 W3Party를 안 만진다. 선행: 대화 세션이 필드 정예 처치 1회를 `EliteDrop`/`GameState`로 넘기는 훅 |
| 2 | **30층 성장 곡선** | 닫음. `BossHp` + 재측정 PASS |
| 3 | **영지 §5 드래그 UX** | `EstateStore.TryMove`·경로 재계산은 닫음. 마우스 드래그 미리보기는 EstateScreen(클로드 창 훅과 겹침) |
| 4 | **환생·탐험·내구·수비명예·착용레벨** | 선행이 없어 지금 넣으면 오펀 |
7. **전체 점검(2026-08-18 22:5x) SelfCheck 전수 129개 중 5개 FAIL — 하나씩 수리** — 실행기 `GameFullCheck`(전수 러너, unity_meas 배치). 증거 `output/qa/ashes-to-stars/full_check/full_check_20260819_0758.log`. ①`BankruptcySeizeSelfCheck` — **닫음(2026-08-19)**: `DrawEstateStatus`가 `BankruptcySeize.KeepLine()`(강등)·`ItemLine()`(압류) 두 줄을 `ShowOnHub`일 때 현황 도크 상단에 그린다. EnvShow·SeedQaIfRequested는 Body()가 이미 켜므로 grep 대상 파일은 EstateScreen 그대로(검사만 고친 게 아니라 실제 소비 추가). ②`MineSeizeSelfCheck` — **닫음(2026-08-19, `ae8f4056`)**: `DrawEstateStatus`가 `statusRow` 스택으로 파산 두 줄 아래에 `EstateMine.Seized`일 때 `EstateMine.SeizeLine()`(「광산 생산 압류 100%(§18-5)」)을 그린다. 도크 5칸은 DockH(하단)이라 3줄과 안 겹침(QA_MINE_SEIZE=1 화면 확인). SelfCheck `영지 현황이 압류 문구·시드를 읽는다` PASS, SeizeLine 소비 제거 시 FAIL. ③`NetWorthSelfCheck` — **닫음(2026-08-19)**: `DrawEstateStatus`가 파산·광산 압류 두 줄 아래 `statusRow` 스택에 `NetWorth.ShowOnHub`일 때 `NetWorth.Line()`(순자산·한도 §18-5)을 그린다(①②와 동형, 검사만 고친 게 아니라 실제 소비 추가). SelfCheck 33항 전부 PASS(「영지 현황이 Line·Seed를 읽는다」 포함), error CS 0. 네거티브 실측: meas 사본에서 그 줄+코멘트를 지우면 FAIL 1건 exit 1. 증거 `output/qa/ashes-to-stars/full_check/net_worth_selfcheck_20260819.log`. ④`RaceDropSelfCheck` — **닫음(2026-08-19)**: 전수 스윕에서만 FAIL·단독 실행은 PASS였다(전형적 전역 상태 오염). `BattleScreen.SeedRaceDropRewardQaIfRequested`의 조기반환 가드가 `DroppedItems.Count>0`만 봐서, 앞선 SelfCheck가 `_reward`에 가죽 아닌 드랍을 남긴 채 승리·수인 상태로 두면 재시드를 건너뛰어 「시드 가죽 1장」이 FAIL이었다. 형제 `SeedRaceAdvMatRewardQaIfRequested`처럼 `DroppedItems.Contains(CraftHide)`를 특정 확인하도록 수정(근본 수리 — 형제와 불일치하던 가드를 맞춤). 통과: 전수 스윕 fail 3→2, RaceDrop 3항 PASS(`racedrop_after_20260819.log`). 네거티브: 옛 가드(스윕)는 「시드 가죽 1장」 FAIL(`racedrop_before_20260819.log`). ⑤`BossBattleDpsSelfCheck` — NRE(W3Party 의존, 대화 세션). **신규 FAIL: `EstateStatusHudSelfCheck` 「도크가 긴 순자산 줄을 안 붙인다」** — **닫음(2026-08-19)**: NetWorthSelfCheck는 배선을 요구하고 이 검사는 부재를 요구하던 모순. 단언을 `if (NetWorth.ShowOnHub)` 게이트 확인으로 교체(형제 파산·광산 패턴과 일치). 전수 스윕 FAIL 1/129(BossBattleDps NRE만) PASS. ⑤`BossBattleDpsSelfCheck`(NRE·W3Party)만 남음 — 대화 세션.

V4 외부 테스터 70% → 넘김. 사람 70% 계속·24h 재실행은 측정하지 않았다. 테스터 통과가 아니다.

> **이번 이터 결과(코드): full_check 회귀 — EstateStatusHudSelfCheck의 모순 단언을 게이트 확인으로.**
> - 직전 트랙이 폴리싱이라 코드 칸. STATUS가 지목한 신규 FAIL `EstateStatusHudSelfCheck`
>   「도크가 긴 순자산 줄을 안 붙인다」를 잡음.
> - **근본 원인(두 SelfCheck 모순)**: `NetWorthSelfCheck:132`는 `EstateScreen.cs`가
>   `NetWorth.Line`을 **포함**하라고 요구(530cf54b 배선), 반면 `EstateStatusHudSelfCheck:104`는
>   `!estate.Contains("NetWorth.Line()")`로 **부재**를 요구 — 둘이 동시 통과 불가. NetWorth 배선은
>   검증된 의도 기능이므로 후자의 `!contains` 단언이 낡은 것.
> - 실제 레이아웃 확인: `NetWorth.Line()`은 `if (NetWorth.ShowOnHub)` 게이트 뒤 statusRow
>   스택에서만 그림(파산 `KeepLine`/`ItemLine`·광산 `SeizeLine`과 동형·동일 게이트 패턴).
>   도크 5칸은 DockH(하단)이라 상시 슬림 — 단언의 원래 의도(도크에 긴 줄을 상시 안 붙임)는 유지됨.
> - **수정**(`EstateStatusHudSelfCheck.cs:104`): 단언을 `estate.Contains("if (NetWorth.ShowOnHub)")`로
>   교체 — 긴 순자산 줄이 게이트 뒤에서만 그려지는지(=도크 상시 슬림) 확인. 검사만 고친 게 아니라
>   기존 형제(파산·광산) 패턴과 일치시킨 것. `W3Party`/Resources/`EstateScreen.cs`는 안 건드림.
> - **통과 기준**: `GameFullCheck` 전수 4165 PASS · FAIL 1/129(=`BossBattleDpsSelfCheck` NRE만),
>   `EstateStatusHudSelfCheck`·`NetWorthSelfCheck` 둘 다 PASS, error CS 0.
>   증거 `output/qa/ashes-to-stars/full_check/full_check_estatehud_fix_20260819.log`.
> - **네거티브(실측)**: meas 사본에서 옛 `!estate.Contains("NetWorth.Line()")`로 되돌리면
>   `EstateStatusHudSelfCheck` FAIL 1건 exit 1 —
>   `output/qa/ashes-to-stars/full_check/estatehud_negctrl_20260819.log`.
> - **남은 full_check FAIL**: `BossBattleDpsSelfCheck`(NRE·W3Party 의존)만 — 대화 세션 몫.
>   full_check FAIL 목록이 이제 하나로 줄었다. 코드 `e52b9d8e`.
>
> **직전 이터 결과(코드): full_check ④ RaceDropSelfCheck — 시드 가드를 CraftHide 특정 확인으로.**
> - 직전 트랙이 폴리싱이라 코드 칸. 남은 full_check FAIL ④를 잡음(⑤ BossBattleDps는 W3Party 의존 NRE라 대화 세션).
> - **근본 원인**: RaceDrop은 **단독 실행은 PASS인데 전수 스윕(`GameFullCheck`)에서만 FAIL**이었다
>   — 전형적 전역 상태 오염. `SeedRaceDropRewardQaIfRequested`의 조기반환 가드가
>   `_reward.DroppedItems.Count>0`만 확인해서, 알파벳 앞선 SelfCheck가 `_reward`를 승리·수인·
>   비가죽 드랍 상태로 남기면 재시드를 건너뛰어 「시드 가죽 1장」이 FAIL. 형제
>   `SeedRaceAdvMatRewardQaIfRequested`는 `.Contains(AdvancementMaterial)`로 특정 아이템을
>   보는데 RaceDrop만 어긋나 있었다.
> - **수정**(`BattleScreen.cs`): 가드를 `_reward.DroppedItems.Contains(Economy.LifeItem.CraftHide)`로
>   교체 — 남은 상태와 무관하게 가죽이 없으면 항상 재시드. `W3Party`/Resources는 안 건드림.
> - **통과 기준**: 전수 스윕 fail 3→2, RaceDrop 3항(시드 가죽·시드 화면 문구·시드 요약) PASS.
>   증거 `output/qa/ashes-to-stars/full_check/racedrop_after_20260819.log`.
> - **네거티브(실측)**: 옛 가드로 돌린 스윕은 「시드 가죽 1장」 FAIL —
>   `output/qa/ashes-to-stars/full_check/racedrop_before_20260819.log`. 단독 실행은 옛 가드로도 PASS라
>   반드시 **전수 스윕**으로 재현·검증할 것(파일 단위 검증은 이 오염을 못 본다).
> - **남은 full_check FAIL**: ⑤`BossBattleDpsSelfCheck`(NRE·W3Party) + 신규 `EstateStatusHudSelfCheck`
>   (③ NetWorth 배선의 회귀로 보임). 다음 코드 이터가 하나씩. 코드 대기 커밋.
>
> **직전 이터 결과(코드): full_check ③ 순자산 문구를 영지 현황 도크에 배선.**
> - 직전 트랙이 폴리싱이라 코드 칸. full_check FAIL 5개 중 ①②는 이미 닫힘 → 같은 「현황 도크
>   문구 배선」 계열인 ③ NetWorth를 잡음. 소비처: `NetWorth.Line()`이 TowerScreen만 읽고
>   EstateScreen은 Seed·EnvShow만 읽어 SelfCheck의 「영지 현황이 Line·Seed를 읽는다」가 FAIL이었다.
> - **생산 소비처**: `EstateScreen.DrawEstateStatus`가 파산 강등·광산 압류 두 줄 아래 statusRow
>   스택에 `NetWorth.ShowOnHub`(QA_NET_WORTH·파산과 동형 게이트)일 때 `NetWorth.Line()`
>   =「순자산 … · 한도 …(§18-5)」를 그린다. 도크 5칸은 DockH(하단)이라 스택과 안 겹침.
>   `W3Party`/`EstateBuild`/`EstateStatusHud`/`TowerScreen`/`Resources`는 안 건드림.
> - **통과 기준**: `NetWorthSelfCheck.Run` 33항 전부 PASS(이전 FAIL이던 「영지 현황이 Line·Seed를
>   읽는다」 포함), `error CS` 0. meas 사본 배치(`unity_meas`, 원본 Unity는 오너가 -useHub로 열어둠).
> - **네거티브(실측)**: meas 사본에서 `NetWorth.Line()` 줄+코멘트를 지우면 `NetWorthSelfCheck`
>   FAIL 1건 「영지 현황이 Line·Seed를 읽는다」 exit 1. 코멘트만 남기면 grep 기반 검사가 오탐하므로
>   코멘트까지 제거해야 진짜 FAIL이 뜬다(검사가 소스 텍스트 grep임을 확인).
> - **증거** `output/qa/ashes-to-stars/full_check/net_worth_selfcheck_20260819.log`. 코드 `530cf54b`.
> - 남은 full_check FAIL: ④`RaceDropSelfCheck`(QA 시드 보상에 가죽 없음) ⑤`BossBattleDpsSelfCheck`(NRE).
>   다음 코드 이터가 하나씩.
>
> **직전 이터 결과(폴리싱): 던전 노드 부제 배치 템플릿 enum 누출을 한글로.**
> - 직전 트랙이 코드라 폴리싱 칸. `폴리싱 다음`이 「던전 → 전투HUD → 타이틀」이라 던전을 봤다.
>   기존 최신 샷 `family_adv_shots/qa_go:Dungeon.png`(08-19 02:33)을 열어 결함 확인:
>   「1. 전투」노드 부제가 `동시 64체 · 원거리 0% · pockets` — **`ArenaTemplate` enum 이름
>   (pockets)이 한글 UI에 그대로 노출**. 안 읽히는 글씨(폴리싱 대상).
> - **생산 소비처**: `DungeonScreen.Desc`가 보상분기·일반 노드 두 곳에서 `{n.Template}`를 그대로
>   찍던 것을 `TemplateKo(n.Template)`로 교체 — 엄폐·열린 고리·병목·기둥·넓은 무대.
>   내부 서명 `DungeonPlan.Signature`(96행)의 enum은 UI가 아니라 그대로 둠.
>   `W3Party`/`EstateBuild`/`EliteKinds`/`Resources`는 안 건드림.
> - **컴파일 검증**: `unity_meas` 사본 배치 빌드(`-batchmode -nographics`) 성공 —
>   `/tmp/meas_build.log` 「Exiting batchmode successfully now!」, `error CS` 0건.
>   빌더가 출력 경로를 하드코딩(`build_game`)해 실행본도 새 코드로 갱신됨.
> - **⚠️ 확인 샷 미완(정직한 미완)**: 이 헤드리스 세션 환경에서 `ScreenCapture`가 저장 실패한다 —
>   앱은 실제로 렌더됐고(Player.log Metal 1280×720, `[던전] 계획: …open_ring/choke/pillars/
>   arena_wide` 확인) 스모크 캡처 요청까지 갔으나 `Failed to store screen shot`(foreground 앱이
>   아니라 drawable 없음, 3회 재시도 동일). qa_shot.sh·앱 직접 실행은 승인 게이트로 막힘.
>   **다음 세션이 GUI 가능 환경에서 `go:Dungeon` 샷으로 최종 확인할 것.**
> - **네거티브(구성상)**: `TemplateKo(n.Template)`를 `n.Template`로 되돌리면 옛 enum 원문이 다시 샌다.
> - **코드** `8b6912f2`. `W3Party`는 안 건드렸다.
>
> **직전 이터 결과(폴리싱→코드/실행): 광산 연체 압류 문구를 영지 현황에 배선(full_check ②).**
> - 직전 트랙이 코드라 폴리싱 칸으로 시작. `폴리싱 다음`(필드/사냥 시작 부제)을 샷으로 확인 →
>   **부제 안 잘림**(field 허브 샷 `field_dungeon_cap_shots/qa_go:Field.png`, 새 샷도 동일). 규정대로
>   회전을 이어 **필드·캐릭터·파티·월드맵·결과를 전부 샷으로 훑음 — 결함 0**(전부 이미 폴리시됨).
>   파티 삭제 카드가 「환생석」을 줄바꿈으로 쪼개지만 전문이 다 보여 결함 아님. 한 줄 강제(단일라인
>   rect)를 시도했다가 오히려 「환생석으」로 **잘려서** 정보 손실 → **되돌림**(PartyScreen 무변경).
> - 깨끗한 UI를 억지로 바꾸면 회귀라, 오너 「대기하지 마라 / 잡을 것 없으면 코드 구멍」 지침대로
>   full_check ②(문서화된 다음 코드 과제)로 전환. ①과 같은 「현황 도크에 문구 배선」 계열이다.
> - **생산 소비처**: `EstateScreen.DrawEstateStatus`가 `statusRow` 스택으로 파산 강등·압류 두 줄
>   아래에 `EstateMine.Seized`일 때 `Info(r, statusRow++, "광산 " + EstateMine.SeizeLine())` =
>   「광산 생산 압류 100%(§18-5)」를 그린다. `EstateMine.SeizeLine()`이 소비처 0곳이었다(EstateScreen은
>   `EnvShowSeize`·`SeedSeizeQaIfRequested`만 읽고 문구는 안 읽음). 도크 5칸은 DockH(하단)이라
>   3줄 스택과 안 겹침. `W3Party`/`EstateBuild`/`EstateStatusHud`/`Resources`는 안 건드림.
> - **통과 기준**: `AshesToStars.MineSeizeSelfCheck.Run` PASS
>   (`/tmp/mineseize_selfcheck.log [MineSeizeSelfCheck] PASS`), 검사줄
>   `영지 현황이 압류 문구·시드를 읽는다` PASS.
> - **네거티브**: unity_meas에서 `EstateMine.SeizeLine()` 소비를 빼면(「광산 압류중」 리터럴로 교체)
>   FAIL 1 (`/tmp/mineseize_neg.log FAIL 영지 현황이 압류 문구·시드를 읽는다`). ⚠️ 주석에 그 함수명
>   글자를 안 남겨야 grep 오탐 없음(①의 교훈 반영 — 주석 회피 확인).
> - **화면**(직접 열음, 빈 화면 아님, `QA_MINE_SEIZE=1 go:Estate`):
>   `output/qa/ashes-to-stars/mine_seize_shots/qa_go:Estate.png` 991676B — 영지 현황 상단 3줄 스택
>   `파산 강등 −1(§18-5)` · `비장착 30% 압류 · 4골드 32실버 상환(§18-5)` · `광산 생산 압류 100%(§18-5)`,
>   하단 도크 5칸(광산=`생산 압류`). 겹침 없음. 마을 배경.
> - **정직한 미완**: full_check ③NetWorth ④RaceDrop ⑤BossBattleDps NRE는 남았다. ③은 ①②와 같은
>   계열(NetWorth.Line은 TowerScreen:134가 읽는데 검사는 EstateScreen만 훑음 — 현황 도크에 순자산
>   배선 필요)이라 다음 코드 칸. ⑤는 W3Party 의존 NRE라 대화 세션과 상의. 오너 에디터 PID 25198(원본)은
>   안 죽였고 unity_meas 사본으로 빌드·측정. ⚠️ qa_shot에 positional 인자를 주면 GAME_PROJ보다 우선해
>   자동파일럿 모드로 샌다 — 허브는 `qa_shot.sh --skip-build "go:Field" N` 형태로 부를 것.
> - **코드** `ae8f4056`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(UI/실행): 던전 입장 카드 부제는 한 줄.**
> - 직전 트랙이 코드라 필드 화면 한 결함. 레이드급은 `7b61d1bb`로 줄었는데
>   던전 입장만 옛 `랜덤 생성 + 종점 보스 · 1실버 99쿠퍼(§7)`를 붙여 잘렸다.
>   INBOX 이펙트는 FX 미커밋이라 안 겹침. 영지 §4는 클로드. 필드 정예는 W3Party.
> - **생산 소비처**: `FieldDockCap.Dungeon`/`OldDungeon`/`DungeonLine`/`DungeonShort`/`SeedDungeonQaIfRequested`.
>   `FieldScreen` 던전 입장 카드가 Dungeon을 읽는다. `랜덤 · 종점 보스`(10 ≤ 18).
>   `QA_NO`면 옛 긴 줄. `QA_FIELD_DUNGEON_CAP=1`은 자막.
>   `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: Dungeon 10 ≤ 18. 옛 줄은 CaptionFits 아님.
>   차단하면 옛 긴 줄. 화면 `던전 입장 부제는 한 줄이다(§16)` + `랜덤 · 종점 보스`.
> - **TDD/실행**: `unity_meas` `FieldDockCapSelfCheck` 전항 PASS
>   (`field_dungeon_cap_selfcheck.log`). `CardTextFitSelfCheck` 회귀 PASS
>   (도크[1]던전 입장 필요 18.7 ≤ 칸 39).
> - **화면**(직접 열음, 빈 화면 아님, `QA_FIELD_DUNGEON_CAP=1`):
>   `field_dungeon_cap_shots/qa_go:Field.png` 874954B — 필드,
>   자막 `던전 입장 부제는 한 줄이다(§16)`,
>   카드 `던전 입장` · `랜덤 · 종점 보스`. 들판.
>   옛 `랜덤 생성 + 종점 보스 · 1실버 99쿠퍼(§7)`가 아님.
> - **네거티브**: FieldScreen에서 Dungeon을 OldDungeon로 되돌리면 FAIL 1
>   (`field_dungeon_cap_negctrl.log` — 도크가 긴 던전 입장 줄을 붙임). `QA_NO`면 옛 긴 줄.
> - **정직한 미완**: 사냥 시작 카드 부제는 안 줄임. 이펙트는 FX 미커밋이라 안 넣음.
>   EstateBuild는 클로드. 원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `595a0e08`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(코드/실행): 보스 스킬 수 — 중간 2→3 · 대보스 2→3→4 · 50층+ 2→3→4→5.**
> - 직전 트랙이 UI라 코드 칸. INBOX 이펙트는 FX 미커밋이라 안 겹침.
>   영지 §4는 클로드. 필드 정예는 W3Party.
>   옛 CreateBosses·HP 바는 층 ≤5/≤10이라 15층이 4페이즈(2→3→4→5)였다.
> - **생산 소비처**: `BossSkills.PhaseCount`/`SkillsAt`/`Line`/`OldPhaseCount`/`Chain`.
>   `BossBattle.CreateBosses`가 PhaseCount를 읽는다. 페이즈가 SkillsAt을 읽는다.
>   탑 자막이 Line을 읽는다. `UiAtlas.PhaseCountForFloor`가 PhaseCount를 읽는다.
>   5·15층 2→3 · 10·20·40층 2→3→4 · 50·100층 2→3→4→5.
>   `QA_NO`면 옛 ≤5/≤10(15층 4). `QA_BOSS_SKILLS=1`은 15층+자막.
>   `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: PhaseCount 5=2 · 15=2 · 10=3 · 50=4. Final 3·3·4·5.
>   차단하면 15=4. 화면 `중간 2→3(§10-5)` · 탑 15층.
> - **TDD/실행**: `unity_meas` `BossSkillsSelfCheck` 전항 PASS
>   (`boss_skills_selfcheck.log`). `UiAtlasSelfCheck` 회귀 PASS
>   (`boss_skills_atlas_regress.log` — 15층 페이즈 2).
> - **화면**(직접 열음, 빈 화면 아님, `QA_BOSS_SKILLS=1`):
>   `boss_skills_shots/qa_go:Tower.png` 790142B — 탑 · 15층,
>   자막 `중간 2→3(§10-5)`,
>   카드 `레이드 (5층 단위)` · `5층 ×1.5`. 계단·창.
>   옛 `2→3→4→5`가 아님.
> - **네거티브**: CreateBosses에서 PhaseCount를 OldPhaseCount로 되돌리면 FAIL 1
>   (`boss_skills_negctrl.log` — 보스가 PhaseCount를 안 읽음). `QA_NO`면 옛 15층 4.
> - **정직한 미완**: 격노 타이머 표시는 안 넣음(목표×2는 CreateBosses가 이미 씀).
>   던전 입장 카드 부제는 안 줄임. 이펙트는 FX 미커밋이라 안 넣음.
>   EstateBuild는 클로드. 원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `73195f8d`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(UI/실행): 레이드급 카드 부제는 한 줄.**
> - 직전 트랙이 코드라 필드 화면 한 결함. 배회 보스는 `61e9ad82`로 줄었는데
>   레이드급만 옛 `5인 전제 · 비용 · 환생석·증표 없음(§10-8)`를 붙여 잘렸다.
>   INBOX 이펙트는 FX 미커밋이라 안 겹침. 영지 §4는 클로드. 필드 정예는 W3Party.
> - **생산 소비처**: `FieldDockCap.Raid`/`OldRaid`/`RaidLine`/`RaidShort`/`SeedRaidQaIfRequested`.
>   `FieldScreen` 레이드급 카드가 Raid를 읽는다. `5인 · 환생석 없음`(11 ≤ 18).
>   `QA_NO`면 옛 긴 줄. `QA_FIELD_RAID_CAP=1`은 출현+자막.
>   `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: Raid 11 ≤ 18. 옛 줄은 CaptionFits 아님.
>   차단하면 옛 긴 줄. 화면 `레이드급 부제는 한 줄이다(§16)` + `5인 · 환생석 없음`.
> - **TDD/실행**: `unity_meas` `FieldDockCapSelfCheck` 전항 PASS
>   (`field_raid_cap_selfcheck.log`). `CardTextFitSelfCheck` 회귀 PASS
>   (도크[2]레이드급 필요 18.7 ≤ 칸 39).
> - **화면**(직접 열음, 빈 화면 아님, `QA_FIELD_RAID_CAP=1`):
>   `field_raid_cap_shots/qa_go:Field.png` 865554B — 필드,
>   자막 `레이드급 부제는 한 줄이다(§16)`,
>   카드 `레이드급 19:59` · `5인 · 환생석 없음`. 들판.
>   옛 `5인 전제 · 11실버 99쿠퍼 · 환생석·증표 없음(§10-8)`가 아님.
> - **네거티브**: FieldScreen에서 Raid를 OldRaid로 되돌리면 FAIL 1
>   (`field_raid_cap_negctrl.log` — 도크가 긴 레이드급 줄을 붙임). `QA_NO`면 옛 긴 줄.
> - **정직한 미완**: 던전 입장 카드 부제는 안 줄임. 이펙트는 FX 미커밋이라 안 넣음.
>   EstateBuild는 클로드. 원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `7b61d1bb`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(코드/실행): 계열 상성 — ×1.3 / ×0.7.**
> - 직전 트랙이 UI라 코드 칸. INBOX 이펙트는 FX 미커밋이라 안 겹침.
>   영지 §4는 클로드. 필드 정예는 W3Party.
>   옛 던전 제목은 `야수 계열`만 보여 §10-3 배율 소비처가 0곳이었다.
> - **생산 소비처**: `FamilyAdv.Mul`/`Title`/`Line`/`OldTitle`/`PartyMul`.
>   `DungeonScreen` 제목·부제가 Title·Line을 읽는다.
>   야수+마법사·정령사·마딜=1.3 · 궁수·딜=0.7 · 검사=1.
>   `QA_NO`면 옛 `던전 · 야수 계열`. `QA_FAMILY_ADV=1`은 T1 야수+자막.
>   전투 수치는 안 넣음. `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: Mul 야수+마법사=1.3 · 궁수=0.7. Title에 ×1.3.
>   차단하면 1·옛 제목. 화면 `던전 · 야수 · 마법사·정령사 ×1.3`.
> - **TDD/실행**: `unity_meas` `FamilyAdvSelfCheck` 전항 PASS
>   (`family_adv_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님, `QA_FAMILY_ADV=1`):
>   `family_adv_shots/qa_go:Dungeon.png` 696356B — 던전 · 야수,
>   제목 `던전 · 야수 · 마법사·정령사 ×1.3`,
>   부제 `야수 · 마법사·정령사 ×1.3(§10-3)` · 시드 7 · T1.
>   입구·전투 카드·동굴. 옛 `야수 계열`만 있던 제목이 아님.
> - **네거티브**: DungeonScreen에서 Title을 OldTitle로 되돌리면 FAIL 1
>   (`family_adv_negctrl.log` — 지도가 Title을 안 읽음). `QA_NO`면 옛 계열 제목.
> - **정직한 미완**: 전투 피해에 배율을 안 곱한다(W3Party). 레이드급 카드 부제는 안 줄임.
>   이펙트는 FX 미커밋이라 안 넣음. EstateBuild는 클로드.
>   원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `a7f82e6a`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(UI/실행): 필드 배회 보스 카드 부제는 한 줄.**
> - 직전 트랙이 코드라 필드 화면 한 결함. 일정·저체력은 `c10357b9`로 줄었는데
>   배회 보스는 옛 `FieldBoss.CardBody`를 그대로 붙여
>   `배회하는 재의 야수 · 준비 없이 만나면 위험 · 환생석 없음(§10-1·§10-8)`가 잘렸다.
>   INBOX 이펙트는 FX 미커밋이라 안 겹침. 영지 §4는 클로드. 필드 정예는 W3Party.
> - **생산 소비처**: `FieldDockCap.Boss`/`OldBoss`/`ShortBossName`/`BossLine`.
>   `FieldScreen` 배회 카드가 Boss를 읽는다. T1=`재의 야수 · 환생석 없음`(14)
>   · T10=`탑의 그림자 · 환생석 없음`(15 ≤ 18). `QA_NO`면 옛 긴 줄.
>   `QA_FIELD_BOSS_CAP=1`은 출현+자막. `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: Boss 14 · T10 15 ≤ 18. 옛 줄은 CaptionFits 아님.
>   차단하면 옛 긴 줄. 화면 `배회 보스 부제는 한 줄이다(§16)` + `재의 야수 · 환생석 없음`.
> - **TDD/실행**: `unity_meas` `FieldDockCapSelfCheck` 전항 PASS
>   (`field_boss_cap_selfcheck.log`). `CardTextFitSelfCheck` 회귀 PASS
>   (도크[5]배회 필요 18.7 ≤ 칸 39).
> - **화면**(직접 열음, 빈 화면 아님, `QA_FIELD_BOSS_CAP=1`):
>   `field_boss_cap_shots/qa_go:Field.png` 869750B — 필드,
>   자막 `배회 보스 부제는 한 줄이다(§16)`,
>   카드 `배회 보스 20:00` · `재의 야수 · 환생석 없음`. 들판.
>   옛 `준비 없이 만나면 위험 · 환생석 없음(§10-1·§10-8)`가 아님.
> - **네거티브**: FieldScreen에서 Boss를 CardBody로 되돌리면 FAIL 1
>   (`field_boss_cap_negctrl.log` — 도크가 CardBody를 읽음). `QA_NO`면 옛 긴 줄.
> - **정직한 미완**: 레이드급 카드 부제는 안 줄임. 이펙트는 FX 미커밋이라 안 넣음.
>   EstateBuild는 클로드. 원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `61e9ad82`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(코드/실행): 영공 — `1 + 층/10`(100층 = 11).**
> - 직전 트랙이 UI라 코드 칸. INBOX 이펙트는 FX 미커밋이라 안 겹침.
>   영지 §4는 클로드. 필드 정예는 W3Party.
>   옛 `SenseBase`는 4~16 선형이라 §18-13 공식 소비처가 0곳이었다.
> - **생산 소비처**: `WorldStar.SenseMul`/`SenseBase`/`SenseLine`/`OldSenseBase`.
>   `SenseBase`가 SenseMul을 읽는다. 월드맵 자막·SizeLabel이 읽는다.
>   1층 1.10 · 50층 6 · 100층 11. `QA_NO`면 옛 4~16.
>   `QA_STAR_SENSE=1`은 100층+자막. `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: SenseMul 1=1.10 · 100=11. 차단하면 16. 차단하면 SenseBase가 SenseMul을 안 읽음.
>   화면 `영공 11.00(§18-13)`.
> - **TDD/실행**: `unity_meas` `WorldStarSelfCheck` 전항 PASS
>   (`star_sense_selfcheck.log`). `RaceSenseSelfCheck` 회귀 PASS
>   (`race_sense_regress.log` — 30층 인간 4.0 · 엘프 4.8).
> - **화면**(직접 열음, 빈 화면 아님, `QA_STAR_SENSE=1`):
>   `star_sense_shots/qa_go:WorldMap.png` 762609B — 월드맵 · 100층,
>   자막 `영공 11.00(§18-13)`,
>   판 `내 별 · 100층 · 별 ×3.00 · 영공 11.0`. 별·궤도.
>   옛 선형 16.0 줄이 아님.
> - **네거티브**: SenseBase에서 SenseMul을 빼고 OldSenseBase만 쓰면 FAIL 2
>   (`star_sense_negctrl.log` — 100층 16, SenseMul 미읽음). `QA_NO`면 옛 선형.
> - **정직한 미완**: 전장의 안개·탐험 +30%는 안개 시스템이 없어 안 넣음.
>   필드 허브 카드 글씨 잘림은 안 고침. 이펙트는 FX 미커밋이라 안 넣음.
>   EstateBuild는 클로드. 원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `484eaa4e`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(UI/실행): 월드맵 수비대 카드 부제는 한 줄.**
> - 직전 트랙이 코드라 월드맵 화면 한 결함. 옛 수비대 카드가
>   `침략 전투는 아직 없다(§13-5)`를 붙여 잠김 접두와 함께 24자였다.
>   INBOX 이펙트는 FX 미커밋이라 안 겹침.
>   영지 §4는 클로드. 필드 정예는 W3Party.
> - **생산 소비처**: `WorldMapDockCap.Defense`/`OldDefense`/`DefenseCap`.
>   `WorldMapScreen` 수비대 카드가 Defense를 읽는다. 잠김=`침략 없음`
>   (접두 포함 10 ≤ 18). `QA_NO`면 옛 19자.
>   `QA_WORLD_DOCK=1`은 30층+자막. `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: Defense 5 · 접두 10 ≤ 18. 옛 줄 19.
>   차단하면 옛 긴 줄. 화면 `수비대 부제는 한 줄이다(§16)` + `잠김 — 침략 없음`.
> - **TDD/실행**: `unity_meas` `WorldMapDockCapSelfCheck` 전항 PASS
>   (`worldmap_defense_selfcheck.log`). `CardTextFitSelfCheck` 회귀 PASS
>   (월드맵도크[3] `잠김 — 침략 없음` 필요 18.7 ≤ 칸 39).
> - **화면**(직접 열음, 빈 화면 아님, `QA_WORLD_DOCK=1`):
>   `worldmap_defense_shots/qa_go:WorldMap.png` 757370B — 월드맵,
>   자막 `수비대 부제는 한 줄이다(§16)`,
>   카드 `수비대 0/5` · `잠김 — 침략 없음`. 별·궤도.
>   옛 `침략 전투는 아직 없다(§13-5)`가 아님.
> - **네거티브**: WorldMapScreen에서 Defense를 OldDefense로 되돌리면 FAIL 1
>   (`worldmap_defense_negctrl.log` — 지도가 Defense를 안 읽음). `QA_NO`면 옛 긴 줄.
> - **정직한 미완**: 필드 허브 카드 글씨 잘림은 안 고침. 이펙트는 FX 미커밋이라 안 넣음.
>   EstateBuild는 클로드. 원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `17d19655`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(코드/실행): 별 크기 — `1 + 층×0.02`(100층 ×3).**
> - 직전 트랙이 UI라 코드 칸. INBOX 이펙트는 FX 미커밋이라 안 겹침.
>   영지 §4는 클로드. 필드 정예는 W3Party.
>   옛 `SizePx`는 40~112 선형이라 §18-13 공식 소비처가 0곳이었다.
> - **생산 소비처**: `WorldStar.SizeMul`/`SizePx`/`SizeLine`/`OldSizePx`.
>   `SizePx`가 SizeMul을 읽는다. 월드맵 자막·별 아이콘이 읽는다.
>   1층 ×1.02 · 50층 ×2 · 100층 ×3. `QA_NO`면 옛 40~112.
>   `QA_STAR_SIZE=1`은 100층+자막. `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: SizeMul 1=1.02 · 100=3. SizePx(100)=120. 차단하면 112.
>   차단하면 SizePx가 SizeMul을 안 읽음. 화면 `별 크기 ×3.00(§18-13)`.
> - **TDD/실행**: `unity_meas` `WorldStarSelfCheck` 전항 PASS
>   (`star_size_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님, `QA_STAR_SIZE=1`):
>   `star_size_shots/qa_go:WorldMap.png` 767934B — 월드맵 · 100층,
>   자막 `별 크기 ×3.00(§18-13)`,
>   판 `내 별 · 100층 · 별 ×3.00 · 영공 16.0`. 별·궤도.
>   옛 선형 112px 줄이 아님.
> - **네거티브**: SizePx에서 SizeMul을 빼고 OldSizePx만 쓰면 FAIL 4
>   (`star_size_negctrl.log` — 100층 112, SizeMul 미읽음). `QA_NO`면 옛 선형.
> - **정직한 미완**: 영공 `1 + 층/10`은 안 넣음(4~16 선형 유지). 수비대 부제는 안 줄임.
>   이펙트는 FX 미커밋이라 안 넣음. EstateBuild는 클로드.
>   원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `1c7219aa`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(UI/실행): 월드맵 성계·랭킹 카드 부제는 한 줄.**
> - 직전 트랙이 코드라 월드맵 화면 한 결함. 옛 성계·랭킹 카드가
>   미구현 설명을 통째로 붙여 잠김 접두와 함께 길었다. INBOX 이펙트는 FX 미커밋이라 안 겹침.
>   영지 §4는 클로드. 필드 정예는 W3Party.
> - **생산 소비처**: `WorldMapDockCap.Star`/`Rank`/`OldStar`/`OldRank`.
>   `WorldMapScreen` 성계·랭킹 카드가 읽는다. 성계=`로컬 허브만`
>   · 랭킹=`서버 없음`(접두 포함 11·10 ≤ 18). `QA_NO`면 옛 긴 줄.
>   `QA_WORLD_DOCK=1`은 30층+자막. `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: Star 6 · 접두 11 · Rank 5 · 접두 10 ≤ 18. 옛 성계 36 · 옛 랭킹 24.
>   차단하면 옛 긴 줄. 화면 `성계·랭킹 부제는 한 줄이다(§16)` + `로컬 허브만` · `서버 없음`.
> - **TDD/실행**: `unity_meas` `WorldMapDockCapSelfCheck` 전항 PASS
>   (`worldmap_star_rank_selfcheck.log`). `CardTextFitSelfCheck` 회귀 PASS
>   (월드맵도크[0] `잠김 — 로컬 허브만` · [2] `잠김 — 서버 없음` 필요 18.7 ≤ 칸 39).
> - **화면**(직접 열음, 빈 화면 아님, `QA_WORLD_DOCK=1`):
>   `worldmap_star_rank_shots/qa_go:WorldMap.png` 749685B — 월드맵,
>   자막 `성계·랭킹 부제는 한 줄이다(§16)`,
>   카드 `성계 이동` · `잠김 — 로컬 허브만`, `랭킹` · `잠김 — 서버 없음`.
>   별·궤도. 옛 미구현 설명 이어붙임이 아님.
> - **네거티브**: WorldMapScreen에서 Star·Rank를 OldStar·OldRank로 되돌리면 FAIL 1
>   (`worldmap_star_rank_negctrl.log` — 지도가 Star·Rank를 안 읽음). `QA_NO`면 옛 긴 줄.
> - **정직한 미완**: 수비대 부제는 안 줄임. 이펙트는 FX 미커밋이라 안 넣음.
>   EstateBuild는 클로드. 원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `db2ffb69`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(코드/실행): 대보스 개체 HP — 2체 65% · 3체 45%.**
> - 직전 트랙이 UI라 코드 칸. INBOX 이펙트는 FX 미커밋이라 안 겹침.
>   영지 §4는 클로드. 필드 정예는 W3Party.
>   옛 `CreateBosses`는 `Hp(..., 1)` 뒤에 로컬 0.65/0.45라 `CountMul` 소비처가 0곳이었다.
> - **생산 소비처**: `BossHp.CountMul`/`CountLine`/`Hp(..., bossCount)`.
>   `BossBattle.CreateBosses`가 Hp에 마릿수를 넘긴다. 차단 길도 CountMul.
>   탑 자막이 CountLine을 읽는다. `QA_NO`면 옛 DPS 100.
>   `QA_BOSS_COUNT=1`은 10층 Force 2+자막. `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: Hp(10,180,2)=DPS×180×2.2×0.65. 3체 ×0.45. 1체는 기본과 같음.
>   차단하면 CreateBosses가 마릿수를 안 넘김. 화면 `2체 각 65%(§18-11)`.
> - **TDD/실행**: `unity_meas` `BossHpSelfCheck` 전항 PASS
>   (`boss_countmul_selfcheck.log`). V3 `BossBattleRunSelfCheck` 16724→8362→0 PASS
>   (`boss_countmul_run_regress.log` — 5층 1체라 수치 불변).
> - **화면**(직접 열음, 빈 화면 아님, `QA_BOSS_COUNT=1`):
>   `boss_countmul_shots/qa_go:Tower.png` 796391B — 탑 · 10층,
>   자막 `대보스 2체(§10-7) · 2체 각 65%(§18-11)` ·
>   `보스 HP는 기대 파티 162 DPS(§18-11) · 대보스 ×2.2(§18-10)`.
>   계단·창. 옛 마릿수만 있고 65%가 없던 줄이 아님.
> - **네거티브**: CreateBosses에서 Hp에 1을 넘기고 CountMul을 빼면 FAIL 2
>   (`boss_countmul_negctrl.log` — 마릿수 미전달·CountMul 미읽음).
> - **정직한 미완**: 필드 배회 다중은 💡. 성계·랭킹 카드 부제는 안 줄임.
>   이펙트는 FX 미커밋이라 안 넣음. EstateBuild는 클로드.
>   원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `a3648bc6`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(UI/실행): 월드맵 침략 카드 부제는 한 줄.**
> - 직전 트랙이 코드라 월드맵 화면 한 결함. 옛 침략 카드가 면·출정·약탈을
>   이어 붙여 34~58자였다. INBOX 이펙트는 FX 미커밋이라 안 겹침.
>   영지 §4는 클로드. 필드 정예는 W3Party.
> - **생산 소비처**: `WorldMapDockCap.Caption`/`Open`/`Lock`/`OldOpen`/`Line`.
>   `WorldMapScreen` 침략 카드가 Caption을 읽는다. 잠김=`30층 해금`
>   · 열림=`북 3칸 · 출정`(접두 포함 11·9 ≤ 18). `QA_NO`면 옛 긴 줄.
>   `QA_WORLD_DOCK=1`은 30층+자막. `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: Lock 6 · 접두 11 · Open 9 ≤ 18. 옛 잠금 41 · 옛 열림 58.
>   차단하면 옛 긴 줄. 화면 `침략 부제는 한 줄이다(§16)` + `남 1칸 · 출정`.
> - **TDD/실행**: `unity_meas` `WorldMapDockCapSelfCheck` 전항 PASS
>   (`worldmap_dock_selfcheck.log`). `CardTextFitSelfCheck` 회귀 PASS
>   (월드맵도크[1] 부제 `북 3칸 · 출정` 필요 18.7 ≤ 칸 39).
> - **화면**(직접 열음, 빈 화면 아님, `QA_WORLD_DOCK=1`):
>   `worldmap_dock_shots/qa_go:WorldMap.png` 763896B — 월드맵,
>   자막 `침략 부제는 한 줄이다(§16)`,
>   카드 `침략` · `남 1칸 · 출정`. 별·궤도.
>   옛 `진입 면 … · 출정 7실버 99쿠퍼`가 아님.
> - **네거티브**: WorldMapScreen에서 Caption을 OldOpen으로 되돌리면 FAIL 1
>   (`worldmap_dock_negctrl.log` — 지도가 Caption을 안 읽음). `QA_NO`면 옛 긴 줄.
> - **정직한 미완**: 성계·랭킹·수비대 부제는 안 줄임. 이펙트는 FX 미커밋이라 안 넣음.
>   EstateBuild는 클로드. 원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `98591ad5`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(코드/실행): 정예 유형 1~2종이 던전 지도에 보인다.**
> - 직전 트랙이 UI라 코드 칸. INBOX 이펙트는 FX 미커밋이라 안 겹침.
>   영지 §4는 클로드. 필드 정예는 W3Party.
>   옛 `WavePlan.EliteKinds`는 생성기만 채우고 지도는 퍼센트만 보였다.
> - **생산 소비처**: `EliteKinds.Format`/`Caption`/`Line`/`OldCaption`/`ApplyQa`.
>   `DungeonScreen` 정예 카드가 Caption을 읽는다. 부제 ShowQa면 Line.
>   수호자·주술사. `QA_NO`면 옛 `동시 N체 · 정예 P%`.
>   `QA_ELITE_KINDS=1`은 시드 7 던전+자막. 전투 기믹은 안 넣음.
>   `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: Format 수호자·주술사. Caption에 % 없음. 생성기 정예 52노드 전부 1~2종.
>   차단하면 옛 퍼센트 줄. 화면 `수호자 · 주술사(§10-2)`.
> - **TDD/실행**: `unity_meas` `EliteKindsSelfCheck` 전항 PASS
>   (`elite_kinds_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님, `QA_ELITE_KINDS=1`):
>   `elite_kinds_shots/qa_go:Dungeon.png` 694895B — 던전 · 야수 계열,
>   자막 `수호자 · 주술사(§10-2)` · 시드 7 · T1 · 입구.
>   옛 퍼센트만 있던 줄이 아님.
> - **네거티브**: DungeonScreen에서 Caption을 OldCaption으로 되돌리면 FAIL 1
>   (`elite_kinds_negctrl.log` — 지도가 Caption을 안 읽음). `QA_NO`면 옛 퍼센트 줄.
> - **정직한 미완**: 정예 전투 기믹(오라·힐·소환)은 W3Party라 안 넣음.
>   입구 다음 카드는 전투라 유형은 부제에만 보인다. 이펙트는 FX 미커밋이라 안 넣음.
>   EstateBuild는 클로드. 원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `0d1d5ac7`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(UI/실행): 탑 레이드(5층 단위) 도크 부제는 한 줄.**
> - 직전 트랙이 코드라 탑 화면 한 결함. 옛 레이드 카드가 훈련 줄 또는
>   `대보스 비용 · 5층마다 보스, 10층 단위는 대보스(§9)`를 붙여 24~42자였다.
>   INBOX 이펙트는 FX 미커밋이라 안 겹침. 영지 §4는 클로드. 필드 정예는 W3Party.
> - **생산 소비처**: `TowerDockCap.Raid`/`OldRaid`/`CaptionFits`/`Line`.
>   `TowerScreen` 레이드 카드가 Raid를 읽는다. 훈련=`비살상 · HP 1 귀환`
>   · 5층=`5층 ×1.5` · 10·50층=`대보스 ×2.2`. `QA_NO`면 옛 긴 줄.
>   `QA_TOWER_DOCK=1`은 51층+자막. Lower는 그대로.
>   `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: Raid 훈련 13 · 5층 6 · 대보스 8 ≤ 18. 옛 줄 24·29·42.
>   차단하면 옛 긴 줄. 화면 `레이드 부제는 한 줄이다(§16)` + `대보스 ×2.2`.
> - **TDD/실행**: `unity_meas` `TowerDockCapSelfCheck` 전항 PASS
>   (`tower_raid_dock_selfcheck.log`). `CardTextFitSelfCheck` 회귀 PASS
>   (탑도크[1] 부제 `대보스 ×2.2` 필요 18.7 ≤ 칸 39).
> - **화면**(직접 열음, 빈 화면 아님, `QA_TOWER_DOCK=1`):
>   `tower_raid_dock_shots/qa_go:Tower.png` 790877B — 탑 · 51층,
>   자막 `레이드 부제는 한 줄이다(§16)`,
>   카드 `레이드 (5층 단위)` · `대보스 ×2.2`. 계단·창.
>   옛 24~42자 이어붙임이 아님.
> - **네거티브**: TowerScreen에서 Raid를 OldRaid로 되돌리면 FAIL 1
>   (`tower_raid_dock_negctrl.log` — 전투가 Raid를 안 읽음). `QA_NO`면 옛 긴 줄.
> - **정직한 미완**: 월드맵 침략 카드 부제는 안 줄임. 이펙트는 FX 미커밋이라 안 넣음.
>   EstateBuild는 클로드. 원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `061f5415`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(코드/실행): 탑 대보스 마릿수 — 1=60 · 2=30 · 3=10.**
> - 직전 트랙이 UI라 코드 칸. INBOX 이펙트는 FX 미커밋이라 안 겹침.
>   영지 §4는 클로드. 필드 정예는 W3Party. 필드 배회 다중은 💡라 안 넣음.
>   옛 `BattleScreen`은 던전만 Plan.BossCount를 읽고 탑은 항상 1이었다.
> - **생산 소비처**: `BossCount.Applies`/`FromRoll`/`Of`/`Begin`/`Fight`/`Line`.
>   `BattleScreen`이 Begin을 읽고 드랍이 Fight를 읽는다. 탑 자막이 Line을 읽는다.
>   10·20·…=60/30/10. 5층·배회·던전 중=1(던전은 Plan).
>   `QA_NO`면 옛 1. `QA_BOSS_COUNT=1`은 10층+Force 2+자막.
>   `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: FromRoll 0.59=1 · 0.60=2 · 0.90=3. Of(5)=1. 차단하면 10층도 1.
>   화면 `대보스 2체(§10-7)`.
> - **TDD/실행**: `unity_meas` `BossCountSelfCheck` 전항 PASS
>   (`boss_count_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님, `QA_BOSS_COUNT=1`):
>   `boss_count_shots/qa_go:Tower.png` 800086B — 탑 · 10층,
>   자막 `대보스 2체(§10-7)` · `보스 HP는 기대 파티 162 DPS(§18-11)` ·
>   `대보스 ×2.2(§18-10)`. 계단·창. 옛 항상 1체가 아님.
> - **네거티브**: BattleScreen에서 Begin을 1로 되돌리면 FAIL 1
>   (`boss_count_negctrl.log` — 전투가 Begin을 안 읽음). `QA_NO`면 옛 1.
> - **정직한 미완**: 필드 배회 다중은 💡. 레이드(5층 단위) 카드 부제는 여전히 길다.
>   이펙트는 FX 미커밋이라 안 넣음. EstateBuild는 클로드.
>   원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `9a84944d`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(UI/실행): 탑 하위 레이드 도크 부제는 한 줄.**
> - 직전 트랙이 코드라 탑 화면 한 결함. 옛 하위 카드가 재입장·풀·스케일
>   FormatLine을 이어 붙여 101자라 슬림 도크에서 두 줄로 잘렸다.
>   INBOX 이펙트는 FX 미커밋이라 안 겹침. 영지 §4는 클로드. 필드 정예는 W3Party.
> - **생산 소비처**: `TowerDockCap.Lower`/`OldLower`/`CaptionFits`/`Line`.
>   `TowerScreen` 하위 카드가 Lower를 읽는다. 51층 T5 2회차 `×2 · 10종 · 0.65`.
>   `QA_NO`면 옛 101자. `QA_TOWER_DOCK=1`은 51층+자막.
>   `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: Lower 15 ≤ 18. 옛 줄 101. 차단하면 옛 긴 줄.
>   화면 `하위 레이드 부제는 한 줄이다(§16)` + `×2 · 10종 · 0.65`.
> - **TDD/실행**: `unity_meas` `TowerDockCapSelfCheck` 전항 PASS
>   (`tower_dock_selfcheck.log`). `CardTextFitSelfCheck` 회귀 PASS
>   (탑도크[2] 필요 18.7 ≤ 칸 39).
> - **화면**(직접 열음, 빈 화면 아님, `QA_TOWER_DOCK=1`):
>   `tower_dock_shots/qa_go:Tower.png` 795617B — 탑 · 51층,
>   자막 `하위 레이드 부제는 한 줄이다(§16)`,
>   카드 `하위 레이드 5층` · `×2 · 10종 · 0.65`. 계단·창.
>   옛 101자 세 줄 이어붙임이 아님.
> - **네거티브**: TowerScreen에서 Lower를 옛 FormatLine 이어붙임으로 되돌리면 FAIL 2
>   (`tower_dock_negctrl.log` — Lower 미읽음·옛 긴 줄). `QA_NO`면 옛 101자.
> - **정직한 미완**: 레이드(5층 단위) 카드 부제는 여전히 길다. 이펙트는 FX 미커밋이라 안 넣음.
>   EstateBuild는 클로드. 원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `7930b5cf`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(코드/실행): 레이드 벽 — 5층 ×1.5 · 10층 ×2.2.**
> - 직전 트랙이 UI라 코드 칸. INBOX 이펙트는 FX 미커밋이라 안 겹침.
>   영지 §4는 클로드. 필드 정예는 W3Party.
>   옛 `BossHp.Hp`는 층 곡선만 곱해 5층·10층 벽이 없었다.
> - **생산 소비처**: `BossHp.WallMul`/`WallMid`/`WallMega`.
>   `Hp`가 WallMul을 읽는다. `CreateBosses`가 Hp를 읽는다. 탑 자막이 Line을 읽는다.
>   5·15·…=×1.5 · 10·20·…=×2.2. 던전·배회는 1.
>   `QA_NO`면 옛 1. `QA_BOSS_HP=1`은 탑 30층+자막.
>   `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: WallMul 5=1.5 · 10=2.2 · 6=1. 5층 HP = DPS×90×1.5.
>   던전 중엔 5·10도 1. 차단하면 10층도 1. 화면 `대보스 ×2.2(§18-10)`.
> - **TDD/실행**: `unity_meas` `BossHpSelfCheck` 전항 PASS
>   (`boss_hp_selfcheck.log`). V3 `BossBattleRunSelfCheck` 16724→8362→0 PASS
>   (`boss_wall_run_regress.log`).
> - **화면**(직접 열음, 빈 화면 아님, `QA_BOSS_HP=1`):
>   `boss_wall_shots/qa_go:Tower.png` 833121B — 탑 · 30층,
>   자막 `보스 HP는 기대 파티 472 DPS(§18-11) · 대보스 ×2.2(§18-10)`.
>   계단·창. 옛 곡선만 있던 자막이 아님.
> - **네거티브**: Hp에서 WallMul을 빼면 FAIL 2
>   (`boss_wall_negctrl.log` — 5층 HP 11149, 소스). `QA_NO`면 옛 1.
> - **정직한 미완**: 탑 하위 레이드 도크 부제는 안 줄임. 이펙트는 FX 미커밋이라 안 넣음.
>   EstateBuild는 클로드. 원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `9346b691`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(UI/실행): 일정·저체력 도크 부제는 한 줄.**
> - 직전 트랙이 코드라 필드 화면 한 결함. 옛 저체력·일정·사망없음 부제가
>   슬림 도크에서 두 줄로 잘렸다. INBOX 이펙트는 FX 미커밋이라 안 겹침.
>   영지 §4는 클로드. 필드 정예는 W3Party.
> - **생산 소비처**: `FieldDockCap.LowHp`/`Schedule`/`Death`/`CaptionFits`.
>   `FieldScreen` 도크가 읽는다. 저체력 `30%면 3초 이탈` · 일정 `허브에서도 돈다 · 12h`
>   · 사망없음 `카운트 없음 · 12h`(잠김 접두 포함 17). `QA_NO`면 옛 긴 줄.
>   `QA_FIELD_DOCK=1`은 레이드·배회 보스 걷음+자막.
>   `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: LowHp 10 · Schedule 14 · 잠김 Death 17 ≤ 18.
>   차단하면 옛 긴 줄. 화면 `일정·저체력 부제는 한 줄이다(§16)`.
> - **TDD/실행**: `unity_meas` `FieldDockCapSelfCheck` 전항 PASS
>   (`field_dock_selfcheck.log`). `CardTextFitSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_FIELD_DOCK=1`):
>   `field_dock_shots/qa_go:Field.png` 877079B — 필드,
>   자막 `일정·저체력 부제는 한 줄이다(§16)`,
>   저체력 `30%면 3초 이탈` · 일정 `허브에서도 돈다 · 12h` ·
>   `잠김 — 카운트 없음 · 12h`. 옛 두 줄이 아님.
> - **네거티브**: FieldScreen에서 LowHp·Schedule을 빼면 FAIL 3
>   (`field_dock_negctrl.log` — 옛 긴 줄·CardBody). `QA_NO`면 옛 긴 줄.
> - **정직한 미완**: 탑 하위 레이드 도크 부제는 안 줄임. 이펙트는 FX 미커밋이라 안 넣음.
>   EstateBuild는 클로드. 원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `c10357b9`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(코드/실행): 목숨 시세 상한 — 환생석 300 · 부활초 8 · 두루마리 4 G/h.**
> - 직전 트랙이 UI라 코드 칸. INBOX 이펙트는 FX 미커밋이라 안 겹침.
>   영지 §4는 클로드. 필드 정예는 W3Party. 하한은 `89ef2269`에 있고
>   표의 상한만 소비처 0곳이었다.
> - **생산 소비처**: `LifePrice.Ceil`/`AboveCeil`/`CeilHoursOf`.
>   `AuctionState.TryListItem`이 AboveCeil을 읽는다. 경매 자막이 Line을 읽는다.
>   T1 환생석 상한 300골드 · 부활초 8골드 · 두루마리 4골드.
>   `QA_NO`면 옛 상한 없음. `QA_LIFE_PRICE=1`은 30층·T1+자막.
>   `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: T1 환생석 상한 3_000_000. 3_000_001 거절. 3_000_000 등록.
>   부활초 80_001·두루마리 40_001도 AboveCeil. 차단하면 상한 +1도 등록.
>   화면 `목숨 시세 하한 · 환생석 150골드 · 상한 300골드(§18-4)`.
> - **TDD/실행**: `unity_meas` `LifePriceSelfCheck` 전항 PASS
>   (`life_ceil_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님, `QA_LIFE_PRICE=1`):
>   `life_ceil_shots/qa_go:Estate.png` 898679B — 영지·경매장,
>   자막 `목숨 시세 하한 · 환생석 150골드 · 상한 300골드(§18-4)`,
>   마을·로컬 장. 옛 하한만 있던 자막이 아님.
> - **네거티브**: TryListItem에서 AboveCeil을 빼면 FAIL 4
>   (`life_ceil_negctrl.log` — 상한 +1이 등록, 소스). `QA_NO`면 옛 상한 없음.
> - **정직한 미완**: 가방 골드 확장은 수치가 없어 안 넣음. 이펙트는 FX 미커밋이라 안 넣음.
>   EstateBuild는 클로드. 원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `a17d310b`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(UI/실행): 필드 지갑 부제는 한 줄.**
> - 직전 트랙이 코드라 필드 화면 한 결함. 옛 지갑 카드가 `BagText() · 필드 사냥은 무료`를
>   붙여 `잠김 —` 접두와 함께 두 줄로 잘렸다. INBOX 이펙트는 FX 미커밋이라 안 겹침.
>   영지 §4는 클로드. 필드 정예는 W3Party.
> - **생산 소비처**: `BagTextFmt.Caption`/`CaptionFits`/`RuneCount`.
>   `FieldScreen` 지갑 도크가 Caption을 읽는다. 부활초+환생석만. 재료·증표는 헤더 BagText.
>   `QA_NO`면 옛 긴 줄. `QA_BAG_TEXT=1`은 목숨 2종+레이드 걷음+자막.
>   `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: Caption `부활초 1/3 · 환생석 1` · RuneCount 15 ≤ 18.
>   재료가 있어도 목숨 2종. 차단하면 긴 BagText + `필드 사냥은 무료`.
>   화면 `지갑 부제는 한 줄이다(§16)` + `잠김 — 부활초 3/3 · 환생석 3`.
> - **TDD/실행**: `unity_meas` `BagTextFmtSelfCheck` 전항 PASS
>   (`bag_caption_selfcheck.log`). `CardTextFitSelfCheck` 회귀 PASS
>   (도크[2] 필요 18.7 ≤ 칸 39).
> - **화면**(직접 열음, 빈 화면 아님, `QA_BAG_TEXT=1`):
>   `bag_caption_shots/qa_go:Field.png` 877126B — 필드,
>   자막 `지갑 부제는 한 줄이다(§16)`,
>   지갑 `10350골드 6실버 10쿠퍼` · `잠김 — 부활초 3/3 · 환생석 3`.
>   옛 두 줄(두루마리·증표·강화석·사냥 무료)이 아님.
> - **네거티브**: FieldScreen에서 Caption을 빼면 FAIL 2
>   (`bag_caption_negctrl.log` — 도크가 긴 BagText, Caption 미읽음). `QA_NO`면 옛 긴 줄.
> - **정직한 미완**: 일정·저체력 도크 부제는 여전히 두 줄로 좁다. 이펙트는 FX 미커밋이라 안 넣음.
>   EstateBuild는 클로드. 원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `3ce682d2`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(코드/실행): 증표 시세 상한 400 G/h.**
> - 직전 트랙이 UI라 코드 칸. INBOX 이펙트는 FX 미커밋이라 안 겹침.
>   영지 §4는 클로드. 필드 정예는 W3Party. 하한 200은 `b452cba3`에 있고
>   표의 상한 400만 소비처 0곳이었다.
> - **생산 소비처**: `TokenPrice.Ceil`/`AboveCeil`/`CeilHours`.
>   `AuctionState.TryListItem`이 AboveCeil을 읽는다. 경매 자막이 Line을 읽는다.
>   T1 상한 400골드. `QA_NO`면 옛 상한 없음. `QA_TOKEN_PRICE=1`은 30층·T1+자막.
>   `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: T1 상한 4_000_000. 4_000_001 거절. 4_000_000 등록.
>   차단하면 상한 +1도 등록. 화면 `증표 시세 하한 200골드 · 상한 400골드(§18-4)`.
> - **TDD/실행**: `unity_meas` `TokenPriceSelfCheck` 전항 PASS
>   (`token_ceil_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님, `QA_TOKEN_PRICE=1`):
>   `token_ceil_shots/qa_go:Estate.png` 900868B — 영지·경매장,
>   자막 `증표 시세 하한 200골드 · 상한 400골드(§18-4)`,
>   등록 `특수 직업 증표 1` `수수료 2골드 80실버 · 200골드`.
> - **네거티브**: TryListItem에서 AboveCeil을 빼면 FAIL 4
>   (`token_ceil_negctrl.log` — 상한 +1이 등록, 소스). `QA_NO`면 옛 상한 없음.
> - **정직한 미완**: 목숨 아이템 상한(부활초 8·두루마리 4·환생석 300)은 안 넣음.
>   가방 골드 확장은 수치가 없어 안 넣음. 이펙트는 FX 미커밋이라 안 넣음.
>   EstateBuild는 클로드. 원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `2c66efc1`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(아트/실행): 아틀라스 UV가 이웃을 물지 않는다.**
> - 직전 트랙이 코드라 필드 화면 한 결함. HuntBoon·글씨 잘림은 이미 `8a7e6b93`·`67664c3a`.
>   INBOX 이펙트는 FX 미커밋이라 안 겹침. 영지 §4는 클로드. 필드 정예는 W3Party.
>   옛 `TextureCoords`가 `texture.width`(NPOT 1024)로 나눠 tower→지구본·heart→깨진 하트.
> - **생산 소비처**: `UiAtlas.UvOf`/`TextureCoords`/`Line`/`SeedQaIfRequested`.
>   아틀라스 조각은 `Width`×`Height`(1448×1086). 크롬 솔로는 자기 텍스처 크기.
>   `FieldScreen`이 Line·시드를 읽는다. `QA_ATLAS_UV=1`은 자막.
>   `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: heart UV.x = 21/1448. tower UV.x = 275/1448 ≠ 275/1024.
>   heart 오른쪽이 깨진 하트를 안 문다. 차단하면 0.0205·0.2686.
>   화면 `아이콘은 이웃을 물지 않는다(§16)` + 등대·하트(지구본·녹물약 아님).
> - **TDD/실행**: `unity_meas` `UiAtlasUvSelfCheck` 전항 PASS
>   (`atlas_uv_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님, `QA_ATLAS_UV=1`):
>   `atlas_uv_shots/qa_go:Field.png` 868887B — 필드,
>   자막 `아이콘은 이웃을 물지 않는다(§16)`,
>   던전·레이드 = 등대. 저체력·일정 = 하트만. 옛 지구본·붉은 조각 아님.
> - **네거티브**: TextureCoords를 texture.width로 되돌리면 FAIL 8
>   (`atlas_uv_negctrl.log` — heart 0.0205, tower 0.2686, 하트가 깨진 하트를 묾).
> - **정직한 미완**: 지갑 부제가 두 줄로 좁다. 이펙트는 FX 미커밋이라 안 넣음.
>   EstateBuild는 클로드. 원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `795fd79b`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(코드/실행): 드랍 옵션이 체력을 올린다.**
> - 직전 트랙이 아트라 코드 칸. INBOX 이펙트는 FX 미커밋이라 안 겹침.
>   HuntBoon 도크는 이미 `8a7e6b93`. 영지 §4는 클로드. 필드 정예는 W3Party.
>   옛 `GearOpt`는 이름만 붙이고 `EffectiveHpMul`이 안 읽었다.
> - **생산 소비처**: `GearOpt.HpMul`/`HpPerAffix`/`CombatLine`.
>   `Equipment.EffectiveHpMul`이 HpMul을 읽는다. 캐릭터창이 CombatLine을 읽는다.
>   옵션 1개 = 강화 +1과 같은 `EnhanceHpPerLevel`(2%). 전설 4 = ×1.08.
>   제작품 0옵션은 ×1. `QA_NO`면 옛 ×1. `QA_GEAR_OPT=1`은 전설 흉갑+자막.
>   `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: 전설 4옵션 EffectiveHpMul > 옵션 없는 값. 제작품 불변.
>   차단하면 ×1. 화면 `옵션이 체력을 올린다(§11)` + `옵션 체력 ×1.08(§11)`.
> - **TDD/실행**: `unity_meas` `GearOptSelfCheck` 전항 PASS
>   (`gear_opt_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님, `QA_GEAR_OPT=1`):
>   `gear_opt_shots/qa_go:Character.png` 1099687B — 캐릭터,
>   자막 `드랍 옵션 1~4 · 전설만 4개(§11)`,
>   본문 `옵션이 체력을 올린다(§11)` · `옵션 체력 ×1.08(§11)` ·
>   `갑옷 · 전설 가죽 흉갑 · 옵션 4 · 생명 · 수호 · 강인 · 견고`.
> - **네거티브**: EffectiveHpMul이 HpMul을 빼면 FAIL 2
>   (`gear_opt_negctrl.log` — 1.15=1.15, 소스). `QA_NO`면 옛 ×1.
> - **정직한 미완**: 옵션이 공격·이속은 안 바꾼다(체력만, 강화와 같은 칸).
>   이펙트는 FX 미커밋이라 안 넣음. EstateBuild는 클로드.
>   원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `4cf2a11a`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(아트/실행): 월드맵 HUD 아래 도크.**
> - 직전 트랙이 코드라 월드맵 화면 한 결함. INBOX 이펙트는 FX PNG 미커밋이라 안 겹침.
>   옛 `UiPages.Grid(AfterPlate, 2, 2)`가 본문 456을 덮어 별·궤도가 안 보였다.
> - **생산 소비처**: `WorldMapHud.Cards`/`OverlayH`/`OpenH`/`Line`/`SeedQaIfRequested`.
>   `WorldMapScreen`이 Cards·Line·시드를 읽는다. `QA_NO`면 옛 AfterPlate 2×2.
>   `QA_WORLD_HUD=1`은 자막. 겹침 200 · 열린 배경 340. 탑 허브와 같다.
>   `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: OverlayH=200 < 본문 40%. OpenH>300. 도크 y 아래쪽. 칸 높이 <110.
>   차단하면 456·전폭. 화면 `HUD는 월드맵을 가리지 않는다(§16)` + 별·궤도가 보인다.
> - **TDD/실행**: `unity_meas` `WorldMapHudSelfCheck` 전항 PASS
>   (`worldmap_hud_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님, `QA_WORLD_HUD=1`):
>   `worldmap_hud_shots/qa_go:WorldMap.png` 753767B — 월드맵,
>   자막 `HUD는 월드맵을 가리지 않는다(§16)`,
>   가운데 별·궤도·행성. 아래 2×2 도크 4칸. 옛 전폭 4장이 아님.
> - **네거티브**: Cards를 옛 AfterPlate 2×2로 되돌리면 FAIL 6
>   (`worldmap_hud_negctrl.log` — 겹침 456, 도크 y 140, 칸 220). `QA_NO`면 옛 456.
> - **정직한 미완**: 침략 카드 부제가 도크에서 한 줄로 잘릴 수 있다. HuntBoon은 안 내림.
>   이펙트는 FX 미커밋이라 안 넣음. EstateBuild는 클로드.
>   원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `27436bf0`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(코드/실행): 경매 복원이 드랍 등급·옵션을 싣는다.**
> - 직전 트랙이 아트라 코드 칸. INBOX 이펙트는 FX PNG 미커밋이라 안 겹침.
>   옛 `TryListGear`는 `recipe|enhance`만 실어 유찰·취소하면 전설이 일반·옵션 0이었다.
> - **생산 소비처**: `GearOpt.Pack`/`Parse`/`ListLine`/`SeedListQaIfRequested`.
>   `AuctionState.TryListGear`가 Pack을 읽는다. `Equipment.RestoreListed`가 Parse를 읽는다.
>   캐릭터창이 줄·시드를 읽는다. `QA_NO`면 옛 `recipe|enhance`.
>   `QA_GEAR_LIST=1`은 전설 등록→취소 복원+자막.
>   `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: 전설+3+옵션4 Pack → 취소 뒤 같은 등급·옵션.
>   옛 칸은 일반 0. 차단하면 복원도 일반 0.
>   화면 `경매도 옵션을 싣는다(§11)` + `옵션 4 · 생명 · 수호 · 강인 · 견고(§11)`.
> - **TDD/실행**: `unity_meas` `GearListSelfCheck` 전항 PASS
>   (`gear_list_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님, `QA_GEAR_LIST=1`):
>   `gear_list_shots/qa_go:Character.png` 1093614B — 캐릭터,
>   자막 `경매도 옵션을 싣는다(§11)`,
>   본문 같은 줄 · `옵션 4 · 생명 · 수호 · 강인 · 견고(§11)`.
> - **네거티브**: RestoreListed가 Parse를 빼면 FAIL 3
>   (`gear_list_negctrl.log` — 복원 Common +3 옵션 0). `QA_NO`면 옛 칸.
> - **정직한 미완**: 옵션이 전투 수치를 바꾸지 않는다. 월드맵 전폭은 안 줄임.
>   이펙트는 FX 미커밋이라 안 넣음. EstateBuild는 클로드.
>   원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `cf5b27da`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(아트/실행): 탑 HUD 아래 도크.**
> - 직전 트랙이 코드라 탑 화면 한 결함. INBOX 이펙트는 FX PNG 미커밋이라 안 겹침.
>   옛 `UiPages.Grid(r, 2, 2)`가 본문 540을 전부 덮어 탑 계단이 안 보였다.
> - **생산 소비처**: `TowerHud.Cards`/`OverlayH`/`OpenH`/`Line`/`SeedQaIfRequested`.
>   `TowerScreen`이 Cards·Line·시드를 읽는다. `QA_NO`면 옛 2×2 전폭.
>   `QA_TOWER_HUD=1`은 자막. 겹침 200 · 열린 배경 340. 필드 허브와 같다.
>   `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: OverlayH=200 < 본문 40%. OpenH>300. 도크 y 아래쪽. 칸 높이 <110.
>   차단하면 540·전폭. 화면 `HUD는 탑을 가리지 않는다(§16)` + 계단·창이 보인다.
> - **TDD/실행**: `unity_meas` `TowerHudSelfCheck` 전항 PASS
>   (`tower_hud_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님, `QA_TOWER_HUD=1`):
>   `tower_hud_shots/qa_go:Tower.png` 797366B — 탑,
>   자막 `HUD는 탑을 가리지 않는다(§16)`,
>   오른쪽 계단·창. 아래 2×2 도크 4칸. 옛 전폭 4장이 아님.
> - **네거티브**: Cards를 옛 2×2 전폭으로 되돌리면 FAIL 6
>   (`tower_hud_negctrl.log` — 겹침 540, 도크 y 56, 칸 262). `QA_NO`면 옛 540.
> - **정직한 미완**: 하위 레이드 부제가 도크에서 두 줄로 좁다. 월드맵 전폭은 안 줄임.
>   이펙트는 FX 미커밋이라 안 넣음. EstateBuild는 클로드.
>   원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `3c68562a`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(코드/실행): 창고 현재 칸이 침략 경로.**
> - 직전 트랙이 아트라 코드 칸. INBOX 이펙트는 FX PNG가 다른 세션 미커밋이라 안 겹침.
>   원장 §13-3·GAME_SPEC §2-2: 옛 `PathLength`/`DistToStore`가 `StoreX/StoreY`만 봐
>   창고를 옮겨도 경로가 (3,3)으로 갔다.
> - **생산 소비처**: `EstateStore.Reached`/`TryMove`/`Line`/`SeedQaIfRequested`.
>   `EstateGrid.PathLength`·`DistToStore`가 Reached를 읽는다. 영지·월드맵이 줄·시드를 읽는다.
>   `QA_NO`면 옛 (3,3). `QA_ESTATE_STORE=1`은 창고 (3,6)+자막.
>   기본 스폰 상수는 `ApplyDefault`만. 마우스 드래그 UX는 안 넣음.
>   `W3Party`/`EstateBuild`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: 옮기면 북 3→6 · 남 4→1 · 최단 남. 차단하면 북 3.
>   본성 자리는 거부. 화면 `창고 (3,6) · 남 1칸(§13-3)`.
> - **TDD/실행**: `unity_meas` `EstateStoreSelfCheck` 전항 PASS
>   (`estate_store_selfcheck.log`). `EstateGridSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_ESTATE_STORE=1`):
>   `estate_store_shots/qa_go:Estate.png` 1139971B — 영지 마을,
>   자막 `창고 (3,6) · 남 1칸(§13-3)`, 본문 `침략 남 1칸`.
>   `estate_store_shots/qa_go:WorldMap.png` 761502B — 월드맵,
>   침략 카드 같은 문구. 옛 북 3칸이 아님.
> - **네거티브**: PathLength가 StoreX를 쓰면 FAIL 8
>   (`estate_store_negctrl.log` — 북 3, Reached 0). `QA_NO`면 옛 (3,3).
> - **정직한 미완**: 7동 마우스 드래그 미리보기는 안 넣음(TryMove만).
>   EstateBuild 업그레이드 창은 클로드. 이펙트는 FX 미커밋이라 안 넣음.
>   원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `b150d8a7`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(아트/실행): 서포터 공격 프레임 반쪽.**
> - INBOX 22:03 「서포터 스프라이트 잘못됨」. 오너 시트는 몸이 있는데
>   공격 행 슬래시가 칸을 10개로 쪼개 `attack_04`/`05`가 반쪽·궤적이었다
>   (면적 idle 대비 0.697·0.665). 힉스필드 재생성 없음.
> - **생산 소비처**: `import_owner_sheets.tidy_row_cells`가 행 중앙값 폭의
>   65% 미만 조각을 버린다. `process`는 면적 < idle 72%도 직전 유효 프레임.
>   서포터 31장만 다시 나눔. `W3Party`/`EstateYard`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: 공격·시전 6장 면적 ≥ idle 80%. tidy 후 공격 칸 6~8 · 폭 ≥170.
>   서포터 카드에 반쪽 몸이 없다.
> - **TDD/실행**: `python3 -m unittest test_import_owner_sheets` 3 OK.
>   반입 전 `attack_04` 0.697로 FAIL. 옛 10칸(폭 125·121·62)이면 FAIL.
> - **화면**(직접 열음, 빈 화면 아님, `QA_START_PICK=1`):
>   `buffer_sprite_shots/qa_go:Title.png` 975764B — 5직업 카드.
>   서포터는 루트 든 온전한 몸. 옛 반쪽·궤적 아님.
>   `buffer_sprite_shots/buffer_frames.png` — idle·걷기·공격 6장 전부 몸 있음.
> - **네거티브**: `buffer_sprite_negctrl.log` — tidy 없으면 raw[4]/[5] 0.697·0.665.
> - **정직한 미완**: 이펙트 위치/알파는 안 닫음(다른 세션이 FX PNG를 잡고 있음).
>   깃발 서포터(`five_jobs_base`)는 오너 시트가 루트라 안 바꿈.
>   원본 에디터 PID 25198은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `62af03ed`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(아트/실행): 필드 몹 5계열 알파 구멍.**
> - INBOX 22:03 「몬스터들 스프라이트 알파 잘못 빠져서 구멍」. p22 시트는 몸이 있는데
>   Resources 프레임만 외곽선이었다(추적형 idle 구멍 0.0308). 재생성 없음.
> - **생산 소비처**: `repair_mob_alpha.py`가 `out_p22_bw/sheet_mob_*_{A,B}.png`를
>   3×2로 나눠 `Resources/sprites/{mob01,mob_chaser,mob_charger,mob_ranged,mob_swarmer}`
>   22장씩. `knock_bg.apply`는 마젠타 칸(`mag_frac≥0.15`)에서 종이 휴리스틱을 안 돈다.
>   `W3Party`/`EstateYard`/`HuntBoon`/`Resources/FX`는 안 건드렸다.
> - **통과 기준**: 5계열 idle 내부 구멍 < 0.010 · 불투명 > 0.15.
>   추적형 0.0308→0.0000. 원거리 0.0391→0.0002. 옛 프레임을 되돌리면 FAIL.
>   화면 사냥에 외곽선만인 몹이 없다.
> - **TDD/실행**: `python3 -m unittest test_mob_alpha test_knock_bg` 11 OK.
>   네거티브 `mob_alpha_negctrl.log` — 옛 추적형 idle 구멍 500, unittest rc=1.
> - **화면**(직접 열음, 빈 화면 아님):
>   `mob_alpha_shots/qa_hunt.png` 1124562B — 들판.
>   흰 가면 벌레·뿔 딱정벌레·날개 추적형이 **채워진 몸**. 옛 외곽선 늑대 아님.
> - **정직한 미완**: 서포터 스프라이트·이펙트 위치/알파는 안 닫음.
>   전직 몹(`mob_berserker` 등)은 p22 5계열이 아니라 안 넣음.
>   원본 에디터 PID 25198은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `ac4b5383`. `W3Party`는 안 건드렸다.

> **직전 이터 결과(코드/실행): 영지 자리·밑동·팔레트 (GAME_SPEC_ESTATE_BUILD §1–3).**
> - 클로드 설계문서 `0949499d` + 분담 §4-B(그록=자리/앵커/팔레트, 클로드=`EstateBuild`).
> - **생산 소비처**: `EstateGrid.FootprintOf`/`Covers`/`CoveredByCore`/`TryOwner`/`Fits`.
>   `EstateYard.BuildingBox`가 자리 크기에서 상자를 내고 밑동은 마름모 중심.
>   `EstateHud.PaletteBar`/`NavPlateTop`을 마을이 읽는다.
>   `EstateBuildings.LastFallback` + 폴백 `LogWarning`. `QA_NO_ESTATE_FOOTPRINT`면 옛 sit=0.42.
>   본성 (1,2) 2×2, 수비대 (6,4) 2×1(옛 (7,4)는 격자 밖).
> - **통과 기준**: 8동 전용 PNG·폴백 0. 자리 겹침 0. 1×1/2×1/2×2 밑동=중심.
>   팔레트 아랫변 < 내비 플레이트. 차단하면 sit 0.42.
> - **TDD/실행**: `unity_meas` `EstateFootprintSelfCheck`·`EstateGridSelfCheck`·
>   `EstateHudSelfCheck`·`EstateBuildingsSelfCheck`·`EstateYardSelfCheck` 전항 PASS.
> - **화면**: `estate_footprint_shots/qa_go:Estate.png` 1139284B — 마을,
>   팔레트(화살·마법·성벽·함정)가 내비 도크 위에 있고 글씨가 안 가린다.
>   본성·창고가 2칸 폭. 밑동은 마름모 위(아트 여백은 §6).
> - **네거티브**: `QA_NO_ESTATE_FOOTPRINT=1`이면 sit 0.42(중심보다 위).
>   `QA_NO` HUD면 팔레트가 본문 바닥(옛 겹침).
> - **정직한 미완**: §4 업그레이드 창은 클로드(`EstateBuild.cs` 안 만짐).
>   §5 드래그·StoreX 제거는 이 커밋 뒤. §6 아트 재생성은 아직(전용 8동 유지).
>   원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** 이번 커밋. `W3Party`/`EstateBuild`는 안 건드렸다.

> **이전 이터 결과(UI/실행): 필드 지갑 카드 — 무제한 소지품은 개수만.**
> - INBOX 21:47 필드 정예는 못 열었다(W3Party 킬 훅 없음). 직전 트랙이 코드라
>   필드 화면 한 결함. WORKLOG 덤: `BagText()`가 `환생석 1/2147483647`을 붙였다.
> - **생산 소비처**: `BagTextFmt.Format`/`Unlimited`/`Line`/`SeedQaIfRequested`.
>   `GameState.BagText`가 Format을 읽는다. `FieldScreen` 자막·지갑 카드가 BagText를 읽는다.
>   부활초·두루마리는 n/상한. 환생석·재료·증표·석은 개수만. `QA_NO`면 옛 `/2147483647`.
>   `QA_BAG_TEXT=1`은 환생석+부활초+자막. `W3Party`/`EstateYard`/`HuntBoon`은 안 건드렸다.
> - **통과 기준**: 환생석=`환생석 1`. 부활초=`부활초 1/3`. 차단하면 `/2147483647`.
>   화면 `무제한 소지품은 개수만(§18-4)` + 지갑 카드에 상한 숫자 없음.
> - **TDD/실행**: `unity_meas` `BagTextFmtSelfCheck` 전항 PASS
>   (`bag_text_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님, `QA_BAG_TEXT=1`):
>   `bag_text_shots/qa_go:Field.png` 877139B — 필드,
>   자막 `무제한 소지품은 개수만(§18-4)`,
>   지갑 카드 `환생석 3` · `강화석 1` · `부활초 3/3` · `귀환의 두루마리 1/5`.
>   `2147483647` 없음.
> - **네거티브**: `BagText`에서 `BagTextFmt.Format`을 빼면 FAIL 5
>   (`bag_text_negctrl.log` — 환생석 1/2147483647, 소스). `QA_NO`면 옛 상한.
> - **정직한 미완**: 필드 정예 드랍은 W3Party 킬 훅이 없어 안 넣음. 영지는 클로드 문서 전 금지.
>   샷 지갑 9600골드는 이전 QA 잔재(표기는 보임).
>   원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `ab902c36`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 보스 HP는 기대 파티 DPS(§18-11).**
> - INBOX 21:43 승인 + 21:47 막힌 30층 곡선. 옛 `basePartyDps` 고정 100이라
>   1~30층 필요 DPS가 상수였고 요구 레벨이 Lv1이었다.
> - **생산 소비처**: `BossHp.PartyDps`/`Hp`/`FloorMul`/`ExpectedLevel`/`Line`/`SeedQaIfRequested`.
>   `BossBattle.CreateBosses`가 Hp를 읽는다. 탑 자막이 Line을 읽는다.
>   §18-10 1층=100 · +5.5%/층 → 30층 DPS 472. `QA_NO`면 옛 100.
>   `QA_BOSS_HP=1`은 탑 30층+자막. `W3Party`/`HuntBoon`/`FieldDecor`는 안 건드렸다.
> - **통과 기준**: 30층 DPS > 1층 ×1.5 · 변화 ≥10회 · 5h 사냥 레벨 ≥ 기대 30.
>   차단하면 30층도 100. 화면 `보스 HP는 기대 파티 472 DPS(§18-11)`.
> - **TDD/실행**: `unity_meas` `BossHpSelfCheck` 전항 PASS
>   (`boss_hp_selfcheck.log`). `TowerClimbCurveMeasure` G1·G2 PASS
>   (100→472 · 29회 · Lv36≥30). V3 `BossBattleRunSelfCheck` 11149→5575→0 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_BOSS_HP=1`):
>   `boss_hp_shots/qa_go:Tower.png` 858902B — 탑 · 30층,
>   자막 `보스 HP는 기대 파티 472 DPS(§18-11)`.
> - **네거티브**: `CreateBosses`에서 `BossHp.Hp`를 빼면 FAIL 1
>   (`boss_hp_negctrl.log` — 보스가 Hp를 안 읽음). `QA_NO`면 옛 100.
> - **정직한 미완**: 장비·전직 칸은 §18-10 권장 전투력에 흡수했다(별도 시뮬 없음).
>   관문 ② 5시간 지루함은 안 쟀다. 필드 정예는 W3Party라 다음 INBOX.
>   원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
>   HuntBoon 도크는 대화 세션이 같은 파일을 잡고 있어 안 넣음.
> - **코드** `cb050f29`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 드랍 장비 랜덤 옵션.**
> - 큐 1번. 원장 §11 위임 `랜덤 옵션 | 등급별 1~4개 (전설만 4개)`.
>   `GearItem`에 칸이 없었고 드랍·제작이 등급만 붙였다.
> - **생산 소비처**: `GearOpt.Apply`/`CountOf`/`Format`/`Line`/`SeedQaIfRequested`.
>   `Equipment.TryGrantDrop`이 Apply를 읽는다. 캐릭터창이 줄·장착 표시를 읽는다.
>   일반1·고급2·희귀3·영웅3·전설4. 제작품은 0. `QA_NO`면 옛 0.
>   `QA_GEAR_OPT=1`은 전설 흉갑+옵션 4+자막. 전투 수치는 안 넣음.
>   `W3Party`/`FieldDecor`/`HuntBoon`은 안 건드렸다.
> - **통과 기준**: 드랍 전설=4·영웅=3·제작품=0. 차단하면 드랍도 0.
>   화면 `드랍 옵션 1~4 · 전설만 4개(§11)` + `옵션 4 · 생명 · 수호 · 강인 · 견고(§11)`.
> - **TDD/실행**: `unity_meas` `GearOptSelfCheck` 전항 PASS
>   (`gear_opt_selfcheck.log`). `EquipmentSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_GEAR_OPT=1`):
>   `gear_opt_shots/qa_go:Character.png` 1093529B — 캐릭터,
>   자막 `드랍 옵션 1~4 · 전설만 4개(§11)`,
>   본문 `옵션 4 · 생명 · 수호 · 강인 · 견고(§11)`.
> - **네거티브**: `TryGrantDrop`에서 `GearOpt.Apply`를 빼면 FAIL 8
>   (`gear_opt_negctrl.log` — 드랍 칸 0·소스). `QA_NO`면 옛 0.
> - **정직한 미완**: 옵션이 전투 수치를 바꾸지 않는다. 제작품 지정 옵션은 💡.
>   경매 복원은 칸을 안 실어 온다. 착용 레벨 제한은 수치가 없어 안 넣음.
>   원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `298847b1`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 정예 일반 장비·강화석.**
> - 큐 1번. 원장 §10-8 `정예 = 강화석·일반 장비`. `ElitesKilled`는 세고 있었고
>   이긴 정예 노드는 경험만 줬다. 필드 정예는 W3Party라 이 칸 아님.
> - **생산 소비처**: `EliteDrop.Apply`/`Applies`/`Format`/`Line`/`SeedQaIfRequested`.
>   `DungeonRun.Complete`가 정예 노드에서 Apply를 읽는다. 전투 요약·던전 지도·
>   캐릭터창이 줄을 읽는다. 정예 1노드=일반 장비 1+강화석 1.
>   `QA_NO`면 옛 0. `QA_ELITE_DROP=1`은 일반 흉갑+석+자막.
>   `W3Party`/`FieldDecor`/`HuntBoon`/`EstateYard`는 안 건드렸다.
> - **통과 기준**: Apply 정예=일반+석. 전투·보스=0. 가득이면 석만. 차단하면 0.
>   Complete가 장비·석을 읽는다. 화면 `정예 일반 장비(§10-8)` + 가방 흉갑 + 석 1.
> - **TDD/실행**: `unity_meas` `EliteDropSelfCheck` 전항 PASS
>   (`elite_drop_selfcheck.log`). `EquipmentSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_ELITE_DROP=1`):
>   `elite_drop_shots/qa_go:Character.png` 1089822B — 캐릭터,
>   자막 `정예 일반 장비(§10-8)`, 가방 6/60에 흉갑, `석 1`.
> - **네거티브**: `DungeonRun.Complete`에서 `EliteDrop.Apply`를 빼면 FAIL 3
>   (`elite_drop_negctrl.log` — 장비·석·줄). `QA_NO`면 옛 0.
> - **정직한 미완**: 필드 정예는 킬 카운트가 W3Party라 안 넣음. 랜덤 옵션 1~4는
>   안 넣음. 원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `dd71b6c7`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 보스 고급 장비 드랍.**
> - 큐 1번. 원장 §10-8 `필드/던전 보스·5층·레이드급 = 고급 장비`. `GearGrade`는
>   있었고 제작품만 일반이었다. `RollBattleDrops`는 목숨·재료만 줘서 장비 칸이 0이었다.
> - **생산 소비처**: `GearDrop.Apply`/`GradeOf`/`Format`/`Line`/`SeedQaIfRequested`.
>   `Equipment.TryGrantDrop`/`DisplayName`. `BattleScreen.CalculateVictoryReward`가
>   Apply를 읽는다. 결과·캐릭터창이 줄을 읽는다. 보스 4출처=고급 1개.
>   `QA_NO`면 옛 0. `QA_GEAR_DROP=1`은 고급 흉갑+자막. 정예 일반·랜덤 옵션은 안 넣음.
>   `W3Party`/`FieldDecor`/`HuntBoon`은 안 건드렸다.
> - **통과 기준**: 5층 Apply=고급. 제작=일반. 가득·QA_NO=0. 차단하면 Apply 없음.
>   화면 `보스 고급 장비(§10-8)` + 가방 흉갑.
> - **TDD/실행**: `unity_meas` `GearDropSelfCheck` 전항 PASS
>   (`gear_drop_selfcheck.log`). `EquipmentSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_GEAR_DROP=1`):
>   `gear_drop_shots/qa_go:Character.png` 1067110B — 캐릭터,
>   자막 `보스 고급 장비(§10-8)`, 가방 5/60에 흉갑.
>   `gear_drop_shots/qa_go:Result.png` 895555B — 결과,
>   `생존 — 보스 고급 장비(§10-8)` · 같은 줄 · 갑옷 아이콘.
> - **네거티브**: `BattleScreen`에서 `GearDrop.Apply`를 빼면 FAIL 1
>   (`gear_drop_negctrl.log` — 보스가 Apply를 읽는다). `QA_NO`면 옛 0.
> - **정직한 미완**: 정예 일반 장비는 킬 카운트가 W3Party라 안 넣음. 랜덤 옵션 1~4는
>   안 넣음. 원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `e87fed1b`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 가방 60칸.**
> - 큐 1번. 원장 §11 위임 `가방 = 계정 공용 60칸`. 옛 Gain·제작은 칸을 안 봐
>   비장착 장비를 한없이 넣었다.
> - **생산 소비처**: `BagSlots.Used`/`CanGain`/`CanAddGear`/`WhyFull`/`Line`/`SeedQaIfRequested`.
>   `GameState.Gain`이 새 종류만 `CanGain`을 읽는다. 제작·벗기기·경매 복원이 `CanAddGear`를 읽는다.
>   캐릭터창·대장간이 줄을 읽는다. 목숨 아이템은 종류당 1칸. 비장착 장비는 1개 1칸.
>   장착 6부위는 가방 밖. `QA_NO`면 옛 무한. `QA_BAG_SLOTS=1`은 흉갑 60칸+자막.
>   골드 확장은 안 넣음. `W3Party`/`FieldDecor`/`HuntBoon`은 안 건드렸다.
> - **통과 기준**: 새 종류는 60에서 거부. 있던 스택은 60이어도 받는다. 61번째 장비 null.
>   차단하면 새 종류가 61칸. 화면 `가방 60/60(§11)`.
> - **TDD/실행**: `unity_meas` `BagSlotsSelfCheck` 전항 PASS
>   (`bag_slots_selfcheck.log`). `EquipmentSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_BAG_SLOTS=1`):
>   `bag_slots_shots/qa_go:Character.png` 1066652B — 캐릭터,
>   자막 `가방 60/60(§11)`, 장비 칸에도 같은 줄, 가방에 흉갑.
>   샷 명부의 추모시험은 이전 QA 잔재(자막은 보임).
> - **네거티브**: `Gain`에서 `CanGain`을 빼면 FAIL 3
>   (`bag_slots_negctrl.log` — 새 종류 61칸, 소스). `QA_NO`면 옛 무한.
> - **정직한 미완**: 골드 가방 확장은 안 넣음. 가방 가득 스케줄 복귀는 💡.
>   전장의 안개는 로컬 별 1개라 안 넣음. 환생 스킬 1개 선택은 Job+단계라 칸이 없다.
>   원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `02c0d658`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 무기 직업 계열.**
> - 큐 1번. 원장 §11 위임 `착용 제한 | 레벨 + 무기만 직업 계열 제한`.
>   옛 `TryEquip`은 직업을 안 봐 힐러도 송곳니 검을 찼다.
> - **생산 소비처**: `EquipJob.CanWear`/`WhyNot`/`Line`/`SeedQaIfRequested`.
>   `Equipment.TryEquip`이 `CanWear`를 읽는다. 캐릭터창 가방·자동장착·대장간이 읽는다.
>   송곳니 검=`물리`(탱·딜). 흉갑은 공용. `QA_NO`면 옛 항상 허용.
>   `QA_EQUIP_JOB=1`은 힐러+가방 검+자막. 레벨 제한·다른 무기 레시피는 안 넣음.
>   `W3Party`/`FieldDecor`/`HuntBoon`은 안 건드렸다.
> - **통과 기준**: 탱·딜 검 허용. 힐·마딜·버퍼 거부. 흉갑은 힐도 허용.
>   차단하면 힐러도 참다. 화면 `무기는 직업 계열만(§11)`.
> - **TDD/실행**: `unity_meas` `EquipJobSelfCheck` 전항 PASS
>   (`equip_job_selfcheck.log`). `EquipmentSelfCheck` 회귀 PASS(검 장착 포함).
> - **화면**(직접 열음, 빈 화면 아님, `QA_EQUIP_JOB=1`):
>   `equip_job_shots/qa_go:Character.png` 1069465B — 캐릭터,
>   자막 `무기는 직업 계열만(§11)`, 가방에 검.
>   샷 명부의 추모시험은 이전 QA 잔재(자막은 보임).
> - **네거티브**: `TryEquip`에서 `CanWear`를 빼면 FAIL 3
>   (`equip_job_negctrl.log` — 힐러가 검, 소스). `QA_NO`면 옛 항상 허용.
> - **정직한 미완**: 착용 레벨 제한은 수치가 없어 안 넣음. 마딜·힐·버퍼 전용 무기는 없다.
>   원본 에디터 PID 25198은 죽이지 않았고 사본으로 SelfCheck·촬영.
> - **코드** `a8a531be`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 침략 진입 면 선택.**
> - 큐 1번. 원장 §13-3 ✅ `침략자가 진입 방향을 4면 중 하나로 고른다`.
>   옛 경로는 `EstateGrid.InvaderSide`가 최단만 골랐다. 공격자 선택이 0곳이었다.
> - **생산 소비처**: `InvasionApproach.Pick`/`Side`/`Path`/`Line`/`SeedQaIfRequested`.
>   `InvasionState.TryBegin`이 `Side`를 읽는다. 월드맵 침략 카드가 고르기 화면을 연다.
>   북 3칸·동 4칸·남 4칸·서 3칸. `QA_NO`면 옛 최단 자동. `QA_INVASION_APPROACH=1`은
>   30층·남·고르기 자막. 경로 전투 시뮬은 💡라 안 넣음.
>   `W3Party`/`FieldDecor`/`HuntBoon`은 안 건드렸다.
> - **통과 기준**: Pick 남 → TryBegin Approach=남. 안 고르면 북. 차단하면 남도 북.
>   화면 `진입 면을 고른다(§13-3)` + 북/동/남/서 칸 수.
> - **TDD/실행**: `unity_meas` `InvasionApproachSelfCheck` 전항 PASS
>   (`invasion_approach_selfcheck.log`). `EstateGridSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_INVASION_APPROACH=1`):
>   `invasion_approach_shots/qa_go:WorldMap.png` 970597B — 월드맵,
>   자막 `진입 면을 고른다(§13-3)`,
>   북 3칸·동 4칸·남 4칸·서 3칸. 옛 최단 자동 한 장이 아님.
> - **네거티브**: `TryBegin`이 `InvaderSide`를 쓰면 FAIL 3
>   (`invasion_approach_negctrl.log` — Approach 북, 소스). `QA_NO`면 옛 최단.
> - **정직한 미완**: 경로 길이로 약탈을 깎지 않는다. 핀치 줌·캐릭터/몹 화질은 안 넣음.
>   원본 에디터는 죽이지 않았고 사본으로 빌드·촬영.
>   `art/.generating` PID 18279는 죽어 낡은 표시를 지웠다.
> - **코드** `0d3e9408`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 캐릭터 가시성 근본 수리 + 생성 계약.** `060989cf`
> - **「캐릭터가 안 보인다」의 원인은 크기 소스가 셋으로 갈라진 것**이었다(`790ee7e3`).
>   ①`prop_scale.json`(프랍 53종이 character_units=2.0 기준) ②`SpriteBank`의 C# 상수
>   ③`W3Party:1191`의 정예 `localScale 1.4` — 어느 표에도 안 적힌 곱셈. ③ 때문에
>   정예가 1.5×1.4=2.1u로 캐릭터 2.0u보다 **컸다**. 셋을 크기표 하나로 모았다
>   (`FieldDecor.Units`, 표에 `unit_char/mob/elite/boss/proj` 추가). 캐릭터 2.0→2.8.
> - **겹침은 원인이 아니다** — 탱 반경 1.5u 안에서 정렬값이 탱 이상인 렌더러 덤프 = **0개**.
>   렌더 픽셀 실측도 정상: 몸통 (109,103,83) vs 원본 아트 평균 (116,122,106).
> - **몹 틴트가 손그림을 죽이고 있었다**(`ad80da89`). 몹 원본 채도는 0.07~0.10으로
>   캐릭터(0.10)와 같은 계열인데 `FamilyTint`가 원색을 곱해 화면에서만 쨍했다 →
>   화면에서 채도 높은 것이 전부 잡몹이 되어 주인공이 묻혔다. 흰색 Lerp는 실패(크림색으로
>   뜬다), HSV 탈채도로 재수정.
> - **카메라** ortho 13 → 10. 720px에서 캐릭터가 55px(7.7%) → 72px(10.0%).
>   그록 `c77b782f`「전장 확대」를 되돌린 것이라 보드에 이견을 물어 뒀다.
> - **그록 생성물 검수 — 6셀 시트 10장 불합격**(`060989cf`). 크로마 배경이 흰색
>   (255,255,255)·남색(50,53,62)·회색(170,170,171)으로 나왔다. **같은 배치의 단일 그림
>   6장은 전부 정상 마젠타** — 실패는 6셀 시트에만 났다. 흰 배경은 치명적이다(가면이
>   뼈색이라 크로마키가 가면을 판다). 원인은 지시 부재가 아니라 앞에 선 `HOLLOW_STYLE`이
>   마젠타를 밀어낸 것 → `OUTPUT_CONTRACT`를 프롬프트 **맨 끝**에 붙여 어떤 룰셋도 못 덮게 했다.
>   라벨(`WALK L` 등)은 **별개 원인** — 프롬프트의 `Cells: walk L, walk R…` 목록을 모델이
>   캡션으로 그대로 옮겨 적었다(렌더된 글자가 그 문자열과 정확히 일치). 24개를 위치 산문으로 재작성.
>   불합격 10장은 `art/ref_old_not_hollow/2026-08-18_chroma/`에 격리, 재생성 중.
> - **막힌 것**: 오너가 준 스프라이트 시트 원본이 저장소에 없다(`spec_p9_hollow_chars.json`의
>   `refs`가 빈 배열). 그대로 쓰려면 재업로드가 필요하다.
> - 증거 샷: `output/qa/ashes-to-stars/shots/qa_hunt.png`

> **이전 이터 결과(코드/실행): 경매장 전용 그림.**
> - 큐 1번 캐릭터/몹 화질은 4직업·몹 재생성 금지. 「대기하지 마라」로 INBOX 09:57·09:43
>   남은 경매장 수레 폴백을 닫음. `DedicatedOf(Auction)`이 null이라 `village_cart_0`만 그렸다.
> - **생산 소비처**: `EstateBuildings.Auction`/`DedicatedOf(Auction)`/`Line`.
>   `EstateYard.PropOf`가 읽는다. `QA_NO`면 옛 수레. `QA_ESTATE_BUILDINGS=1`은 자막.
>   그림 `props/estate_auction_0` · 크기표 3.90. FieldDecor는 안 흩뿌린다.
>   `W3Party`/`FieldDecor`/`HuntBoon`은 안 건드렸다.
> - **통과 기준**: PropOf 경매장=`estate_auction_0`. Resources 1장.
>   차단하면 수레. 화면 `경매장은 전용 그림이다(§16)` + 목재 장·금화 간판.
> - **TDD/실행**: `unity_meas` `EstateBuildingsSelfCheck` 전항 PASS
>   (`estate_auction_selfcheck.log`). `EstateYardSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_ESTATE_BUILDINGS=1`):
>   `estate_auction_shots/qa_go:Estate.png` 651708B — 영지 마을,
>   자막 `경매장은 전용 그림이다(§16)`,
>   오른쪽 앞 목재 장·금화 간판. 옛 수레가 아님.
> - **네거티브**: `DedicatedOf`에서 Auction을 빼면 FAIL 3
>   (`estate_auction_negctrl.log` — 이름 null·PropOf 수레·소스). `QA_NO`면 수레.
> - **정직한 미완**: 핀치 줌·캐릭터/몹 화질은 안 넣음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
>   대화 세션 보스 death PNG WIP는 안 커밋했다.
> - **코드** `caf62da6`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 명예 승리 방어력 비례 ±50%.**
> - 큐 1번 캐릭터/몹 화질은 4직업·몹 재생성 금지 + `higgsfield generate list` waiting 0.
>   「대기하지 마라」로 §18-13 ✅ `승리 +30(상대 방어력 비례 ±50%)`을 닫음.
>   `Honor.ApplyInvasion`은 고정 +30만 줬다. 방어 건물의 `CutPercent`를 읽는다.
> - **생산 소비처**: `Honor.WinForCut`/`WinNow`/`SeedDefenseQaIfRequested`.
>   `ApplyInvasion`이 읽는다. `InvasionState.Settle`이 그대로 Apply를 탄다.
>   `WorldMapScreen` 자막·침략 카드가 `WinLine`을 읽는다.
>   Cut 0=15 · 20=30 · 40=45. `QA_NO_HONOR_DEFENSE=1`이면 옛 고정 +30.
>   `QA_HONOR_DEFENSE=1`은 화살탑 16·Cut 40·자막 +45.
>   수비 성공 +20은 들어오는 침략이 없어 안 넣음.
>   `W3Party`/`FieldDecor`/`HuntBoon`은 안 건드렸다.
> - **통과 기준**: 빈 영지 15. 화살탑4+수비 30. 화살탑8+수비·화살탑16 45.
>   차단하면 15·45가 30. 화면 `명예 +45(방어 비례 §18-13)`.
> - **TDD/실행**: `unity_meas` `HonorDefenseSelfCheck` 전항 PASS
>   (`honor_defense_selfcheck.log`). `HonorSelfCheck`·`RepeatLootSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_HONOR_DEFENSE=1`):
>   `honor_defense_shots/qa_go:WorldMap.png` 830771B — 월드맵,
>   자막 `명예 +45(방어 비례 §18-13)`,
>   침략 카드 같은 문구. 옛 고정 +30이 아님.
> - **네거티브**: `WinForCut`이 Cut을 무시하면 FAIL 18
>   (`honor_defense_negctrl.log` — Cut 0·40이 30). `QA_NO`면 옛 +30.
> - **정직한 미완**: 수비 성공 +20·방어력 시뮬 전투는 안 넣음.
>   핀치 줌·캐릭터/몹 화질은 안 넣음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
>   대화 세션 보스 death PNG WIP는 안 커밋했다.
> - **코드** `b4934096`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 긴급 탈출 수동 한정.**
> - 큐 1번 캐릭터/몹 화질은 4직업·몹 재생성 금지 + `higgsfield generate list` waiting 0.
>   대화 세션 보스 death PNG WIP. 「대기하지 마라」로 §4 ✅ `수동 조작 중에만`을 닫음.
>   `TryBegin`은 두루마리만 보고 잡몹·던전 노드에서도 캐스트가 열렸다.
> - **생산 소비처**: `EscapeManual.Allowed`/`WhyNot`/`Line`/`SeedQaIfRequested`.
>   `EmergencyEscape.TryBegin`이 읽는다. `BattleScreen` 잡몹 자막·거부 줄.
>   `FieldScreen`이 두루마리가 있으면 `Line`을 읽는다. 보스·침략만 허용.
>   `QA_NO_ESCAPE_MANUAL=1`이면 옛 항상 허용. `QA_ESCAPE_MANUAL=1`은 자막·두루마리.
>   `W3Party`/`FieldDecor`/`HuntBoon`은 안 건드렸다.
> - **통과 기준**: 잡몹·던전 TryBegin 거부·두루마리 불변. 보스·침략 캐스트.
>   차단하면 잡몹도 캐스트. 화면 `두루마리는 보스전 지휘 중에만(§4)`.
> - **TDD/실행**: `unity_meas` `EscapeManualSelfCheck` 전항 PASS
>   (`escape_manual_selfcheck.log`). `EmergencyEscapeSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_ESCAPE_MANUAL=1`):
>   `escape_manual_shots/qa_go:Field.png` 652717B — 필드,
>   자막 `두루마리는 보스전 지휘 중에만(§4)`,
>   들판·6칸 도크. 옛 자동에서도 두루마리가 되던 안내가 아님.
> - **네거티브**: `TryBegin`에서 `Allowed`를 빼면 FAIL 6
>   (`escape_manual_negctrl.log` — 잡몹 캐스트). `QA_NO`면 옛 항상 허용.
> - **정직한 미완**: 잡몹에서 리더를 직접 움직이는 경우는 W3Party라 안 넣음.
>   핀치 줌·캐릭터/몹 화질은 안 넣음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
>   대화 세션 보스 death PNG WIP는 안 커밋했다.
> - **코드** `4d2d759d`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 영지 마을 굴려 확대.**
> - 큐 1번 캐릭터/몹 화질은 4직업·몹 재생성 금지 + `higgsfield generate list` waiting 0.
>   INBOX 09:18·09:42 남은 확대/축소. 끌어 보기는 배율 1 고정이라 집을 키울 수 없었다.
> - **생산 소비처**: `EstateYard.Zoom`/`SetZoom`/`ClampZoom`/`HandleZoom`/`Line`/
>   `SeedQaIfRequested`. `EstateScreen` 마을 탭이 읽는다. 휠로 칸 배율.
>   `QA_NO_YARD_ZOOM=1`이면 옛 배율 1. `QA_YARD_ZOOM=1`은 자막·시드 1.50.
>   `W3Party`/`FieldDecor`/`HuntBoon`은 안 건드렸다.
> - **통과 기준**: Zoom 1.50 → 칸 187.6 = 기본 125.0 × 1.50. 상한 1.55·하한 0.70.
>   차단하면 칸 제자리. 화면 `마을을 끌어 보고 굴려 확대한다` + 집이 전면보다 크다.
> - **TDD/실행**: `unity_meas` `EstateYardZoomSelfCheck` 전항 PASS
>   (`estate_yard_zoom_selfcheck.log`). `EstateYard`·`EstateYardCam` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_YARD_ZOOM=1`):
>   `estate_yard_zoom_shots/qa_go:Estate.png` 916035B — 영지 마을,
>   자막 `마을을 끌어 보고 굴려 확대한다. 집을 누르면 들어간다(§16)`,
>   본성·광산·대장간이 전면 샷보다 크다. 옛 배율 1이 아님.
> - **네거티브**: `TileW`가 Zoom을 무시하면 FAIL 3
>   (`estate_yard_zoom_negctrl.log` — 칸 125.0 = 125.0 × 1.50). `QA_NO`면 옛 1.
> - **정직한 미완**: 핀치 줌은 안 넣음(데스크톱 휠). 캐릭터/몹 화질은 안 넣음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
>   대화 세션 보스 death PNG WIP는 안 커밋했다.
> - **코드** `3a9ff6aa`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 영지 마을 끌어 보기.**
> - 큐 1번 캐릭터/몹 화질은 4직업·몹 재생성 금지 + `higgsfield generate list` waiting 0.
>   INBOX 09:18 남은 카메라 이동. 전면 마을은 고정 시점이라 가장자리를 못 봤다.
> - **생산 소비처**: `EstateYard.Pan`/`TileOrigin`/`SetPan`/`ClampPan`/`HandlePan`/`Line`/
>   `SeedQaIfRequested`. `EstateScreen` 마을 탭이 읽는다. 끌어 옮김. 집은 MouseUp.
>   `QA_NO_YARD_PAN=1`이면 옛 고정. `QA_YARD_PAN=1`은 자막·시드(180,48).
>   `W3Party`/`FieldDecor`/`HuntBoon`은 안 건드렸다.
> - **통과 기준**: TileOrigin X +180. 상한 밖은 Clamp. 차단하면 Origin 제자리.
>   화면 `마을을 끌어 본다. 집을 누르면 들어간다(§16)` + 마름모가 오른쪽으로 밀림.
> - **TDD/실행**: `unity_meas` `EstateYardCamSelfCheck` 전항 PASS
>   (`estate_yard_cam_selfcheck.log`). `EstateYardSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_YARD_PAN=1`):
>   `estate_yard_cam_shots/qa_go:Estate.png` 661849B — 영지 마을,
>   자막 `마을을 끌어 본다. 집을 누르면 들어간다(§16)`,
>   마름모·본성·광산이 오른쪽에 있다. 옛 가운데 고정이 아님.
> - **네거티브**: `TileOrigin`이 Pan을 무시하면 FAIL 1
>   (`estate_yard_cam_negctrl.log` — 끌어 X 0.0 = 180). `QA_NO`면 옛 고정.
> - **정직한 미완**: 확대/축소는 안 넣음. 캐릭터/몹 화질은 안 넣음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
>   대화 세션 보스 death PNG WIP는 안 커밋했다.
> - **코드** `9d396c1f`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 현황 도크 부제.**
> - 큐 1번 캐릭터/몹 화질은 4직업·몹 재생성 금지 + `higgsfield generate list` waiting 0.
>   직전 이터 미완: 도크 88에서 부제 글씨가 잘렸다. INBOX 21:45·08:37.
> - **생산 소비처**: `UiPages.IsSlimCard`/`TitleHOf`/`CardChrome`/`SlimTitleH`.
>   `GameScreen.DrawCard`가 슬림이면 `button_normal`·`_h1Slim`/`_h2Slim`.
>   `EstateStatusHud.AuraCaption`/`KeepCaption`/`WorldCaption`/`MineCaption`/`StoreCaption`.
>   `EstateScreen` 현황 탭이 읽는다. 옛 제목 36은 부제 칸을 12로 만든다.
>   `QA_NO_ESTATE_STATUS=1`이면 옛 전폭. `QA_ESTATE_STATUS=1`은 자막·현황 탭.
>   `W3Party`/`FieldDecor`/`HuntBoon`은 안 건드렸다.
> - **통과 기준**: 88×230 도크 TitleH=22 · SubH≥18 · 부제 16자 이하.
>   제목을 36으로 되돌리면 TitleH 단언 FAIL. 화면 `현황 도크 부제가 잘리지 않는다(§16)` +
>   `30층 · 영공 7.5` · `Lv3 · 36골드` · `25실버/h` · `1000골드 / 36골드`.
> - **TDD/실행**: `unity_meas` `EstateStatusHudSelfCheck` 전항 PASS
>   (`estate_status_selfcheck.log`). `UiAtlasSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_ESTATE_STATUS=1`):
>   `estate_status_shots/qa_go:Estate.png` 856498B — 영지·현황,
>   자막 `현황 도크 부제가 잘리지 않는다(§16)`,
>   아래 5칸 `내 별 영공`/`본성`/`세계 T1`/`광산`/`창고` 부제가 칸 안에 있다.
>   옛 `잠김 — 950골드 7실` 잘림이 아님.
> - **네거티브**: `TitleHOf`를 항상 `CardTitleH`(36)로 되돌리면 FAIL 1
>   (`estate_status_negctrl.log` — 도크 제목 36 = 22). `QA_NO`면 옛 전폭.
> - **정직한 미완**: 카메라 이동·캐릭터/몹 화질은 안 넣음. 샷 지갑 1000골드는
>   이전 QA 잔재(부제는 보임). 원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
>   대화 세션 보스 death PNG WIP는 안 커밋했다.
> - **코드** `dc3c3004`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 영지 현황 도크.**
> - 큐 1번 캐릭터/몹 화질은 4직업·몹 재생성 금지 + `higgsfield generate list` waiting 0.
>   INBOX 21:45 허브 카드 여백·09:45 가림. 현황 2×2 전폭(영공 80+본문)이 마을을 덮었다.
> - **생산 소비처**: `EstateStatusHud.Cards`/`OverlayH`/`OpenH`/`Line`/`SeedQaIfRequested`.
>   `EstateScreen` 현황 탭이 읽는다. 마을을 그린 뒤 아래 5칸 도크 88.
>   `QA_NO_ESTATE_STATUS=1`이면 옛 전폭. `QA_ESTATE_STATUS=1`은 자막·현황 탭.
>   `W3Party`/`FieldDecor`/`HuntBoon`은 안 건드렸다.
> - **통과 기준**: OverlayH=88 < 본문 20%. OpenH>400. 도크 칸 높이 <90 · 폭 >180.
>   차단하면 540·전폭 영공 80. 화면 `현황은 마을을 가리지 않는다(§16)` +
>   집·본성·광산이 보이고 아래 5칸만 있다.
> - **TDD/실행**: `unity_meas` `EstateStatusHudSelfCheck` 전항 PASS
>   (`estate_status_selfcheck.log`). `EstateYard`·`EstateHud` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_ESTATE_STATUS=1`):
>   `estate_status_shots/qa_go:Estate.png` 768117B — 영지·현황,
>   자막 `현황은 마을을 가리지 않는다(§16)`,
>   마을 마름모·집·깃발 본성·광산. 아래 `내 별 영공`·`본성`·`세계 T1`·`광산`·`창고`.
>   옛 2×2 전폭이 아님.
> - **네거티브**: `OverlayH`를 항상 본문으로 되돌리면 FAIL 4
>   (`estate_status_negctrl.log` — 겹침 540). `QA_NO`면 옛 전폭.
> - **정직한 미완**: 도크 88에서 부제 글씨가 잘린다. 카메라 이동·캐릭터/몹 화질은 안 넣음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
>   대화 세션 보스 death PNG WIP는 안 커밋했다.
> - **코드** `e0802219`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 캐릭터창 3열·장비 라벨.**
> - 큐 1번 캐릭터/몹 화질은 4직업·몹 재생성 금지 + `W3Party`가 대화 세션이라 대기하지 않음.
>   INBOX 08:37 남은 3열 명부가 좁고 장비 둘레 「장신구」가 칸 폭 48에서 잘렸다.
> - **생산 소비처**: `CharHud.RosterSplit`/`RosterCell`/`EquipLabel`/`EquipRingFit`/
>   `SlotLabel`/`Line`/`SeedQaIfRequested`. `CharacterScreen`이 읽는다.
>   목록 최소 520·칸 168·라벨 80. `QA_NO_CHAR_HUD=1`이면 옛 435·140·48.
>   `QA_CHAR_HUD=1`은 자막. `W3Party`/`FieldDecor`/`HuntBoon`은 안 건드렸다.
> - **통과 기준**: 목록 ≥520 · 칸 ≥168 · 라벨 폭 80 · 라벨이 칸 아래·패널 안.
>   차단하면 435·140·48. 화면 `명부 3열과 장비 이름이 잘리지 않는다(§16)` +
>   투구·장갑·장신구·신발·무기·갑옷이 잘리지 않는다.
> - **TDD/실행**: `unity_meas` `CharHudSelfCheck` 전항 PASS
>   (`char_hud_selfcheck.log`). `CharacterRosterSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_CHAR_HUD=1`):
>   `char_hud_shots/qa_go:Character.png` 968042B — 캐릭터,
>   자막 `명부 3열과 장비 이름이 잘리지 않는다(§16)`,
>   왼쪽 2칸(3열 자리)·둘레 `투구`·`장신구`·`장갑`·`신발`·`무기`·`갑옷`.
>   옛 48폭 「장신」 잘림이 아님.
> - **네거티브**: `EquipLabel`을 항상 옛 48폭으로 되돌리면 FAIL 19
>   (`char_hud_negctrl.log` — 폭 48). `QA_NO`면 옛 좁은 칸.
> - **정직한 미완**: 캐릭터/몹 화질·카메라 이동은 안 넣음. 명부 칸은 초상+이름
>   세로라 세 글자 이름은 들어가고 회복 문구는 여전히 잘릴 수 있다.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
>   대화 세션 `W3Party` WIP는 안 커밋했다.
> - **코드** `bc3ce5a2`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 경매 전폭 막대.**
> - 큐 1번 캐릭터/몹 화질은 4직업·몹 재생성 금지 + `W3Party`가 대화 세션이라 대기하지 않음.
>   INBOX 09:45 남은 경매 Info 2줄이 본문 전폭(1232)을 덮었다.
> - **생산 소비처**: `AuctionHud.BarRect`/`LotsBody`/`OverlayH`/`StatusLine`/`Line`/
>   `SeedQaIfRequested`. `EstateScreen` 경매장이 읽는다. 슬림 580×36·두 줄 겹침 80.
>   `QA_NO_AUCTION_HUD=1`이면 옛 전폭 64×2(140). `QA_AUCTION_HUD=1`은 자막.
>   `W3Party`/`FieldDecor`/`HuntBoon`은 안 건드렸다(대화 세션 전투 WIP).
> - **통과 기준**: BarW=580 < 본문 50%. OverlayH(2)=80 < 옛 140.
>   차단하면 1232·140. 화면 `HUD는 경매 배경을 가리지 않는다(§16)` +
>   슬림 막대 `300골드 · 0/10` · 오른쪽 탑·집이 전폭 막대 없이 보인다.
> - **TDD/실행**: `unity_meas` `AuctionHudSelfCheck` 전항 PASS
>   (`auction_hud_selfcheck.log`). `AuctionTradeSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_AUCTION_HUD=1`):
>   `auction_hud_shots/qa_go:Estate.png` 427828B — 영지·경매장,
>   자막 `HUD는 경매 배경을 가리지 않는다(§16)`,
>   왼쪽 슬림 안내·구매 줄. 옛 전폭 Info 2줄이 아님.
> - **네거티브**: `BarW`를 항상 전폭으로 되돌리면 FAIL 4
>   (`auction_hud_negctrl.log` — 폭 1232). `QA_NO`면 옛 전폭.
> - **정직한 미완**: 캐릭터창 3열 명부·장비 둘레 라벨·캐릭터/몹 화질·카메라 이동은 안 넣음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
>   대화 세션 `W3Party` WIP는 안 커밋했다.
> - **코드** `d9e715f0`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 허브 제목판 52.**
> - 큐 1번 캐릭터/몹 화질은 4직업·몹 재생성 금지 + `W3Party`가 대화 세션이라 대기하지 않음.
>   INBOX 09:45 남은 헤더 88이 본문 시작 100을 덮었다.
> - **생산 소비처**: `HubHeader.H`/`BodyTop`/`IconRect`/`TitleRect`/`SubtitleRect`/`Line`/
>   `SeedQaIfRequested`. `GameScreen`이 읽는다. 슬림 52·본문 56·아이콘 36.
>   `QA_NO_HUB_HEADER=1`이면 옛 88·100. `QA_HUB_HEADER=1`은 자막.
>   `W3Party`/`FieldDecor`/`HuntBoon`은 안 건드렸다(대화 세션 전투 WIP).
> - **통과 기준**: HeaderH=52 · BodyTop=56 · 열린 본문 584 > 옛 540.
>   차단하면 88·100. 화면 `제목판은 화면을 가리지 않는다(§16)` +
>   영지 마을·필드 들판이 전폭 88 제목판 없이 보인다.
> - **TDD/실행**: `unity_meas` `HubHeaderSelfCheck` 전항 PASS
>   (`hub_header_selfcheck.log`). `EstateYard`·`UiAtlas`·`FieldHud`·`EstateHud` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_HUB_HEADER=1`):
>   `hub_header_shots/qa_go:Estate.png` 826288B — 영지 마을,
>   자막 `제목판은 화면을 가리지 않는다(§16)`, 집·본성·광산이 얇은 제목 아래.
>   `hub_header_shots/qa_go:Field.png` 488264B — 필드,
>   같은 자막, 들판·길·울타리. 옛 88 전폭이 아님.
> - **네거티브**: `H`를 항상 `OldH`로 되돌리면 FAIL 4
>   (`hub_header_negctrl.log` — 높이 88, HeaderH 88). `QA_NO`면 옛 88.
> - **정직한 미완**: 경매 전폭 막대·캐릭터/몹 화질·카메라 이동은 안 넣음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
>   대화 세션 `W3Party`/`HuntBoon` WIP는 안 커밋했다.
> - **코드** `2195c648`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 대화 세션 검증 — 사냥 3택·겹침.**
> - 오너 「테스트는 철저하게, 결과를 보드에 작성」.
> - **통과**: 사냥 강화 3택(8킬·중복 제외·8개 상한·QA_NO)·캐릭터 겹침·
>   집·나무 겹침·집 돌아나가기·길 한가운데 금지·로컬 시드·사냥 경험치.
>   보드 회귀 74개.
> - **TDD/실행**: `unity_meas` `ChatWorkBatchSelfCheck` 7/7 PASS
>   (`chat_work_batch.log`). `python3 -m unittest loop.test_board loop.test_v4_playtest` 74 OK.
>   보드 「검증 결과」칸 `loop/last_test_report.json`.
> - **정직한 미완**: 플레이 창 육안(8킬 후 3택이 실제로 보임)은 원본 에디터
>   PID 75776이라 사본 배치만 돌렸다. Play 재시작 후 확인 필요.
> - **코드** `a54b95de` + 이번 검증 커밋.

> **이전 이터 결과(코드/실행): 영지 전용 본성·광산·창고·수비대.**
> - INBOX 09:57이 큐보다 앞선다. ARTIFACT_INDEX: `village_house_*`/`barn`은 필드 장식.
>   옛 PropOf는 본성=큰 집·광산=헛간·창고=집·수비대=헛간이었다.
> - **생산 소비처**: `EstateBuildings.DedicatedOf`에 Keep/Mine/Warehouse/Barracks.
>   `EstateYard.PropOf`가 읽는다. `QA_NO`면 옛 집·헛간. `QA_ESTATE_BUILDINGS=1`은 자막.
>   힉스필드 nano_banana_2 · `out_estate_buildings/` ·
>   `props/estate_{keep,mine,warehouse,barracks}_0`.
>   `W3Party`/`FieldDecor`/`HuntBoon`은 안 건드렸다(대화 세션 전투 WIP).
> - **통과 기준**: PropOf 본성=`estate_keep_0` · 광산=`estate_mine_0` ·
>   창고=`estate_warehouse_0` · 수비대=`estate_barracks_0`. Resources 4장.
>   차단하면 집·헛간. 화면 `본성·광산·창고·수비대는 전용 그림이다(§16)` +
>   석조 본성(금별 깃발)·광산 입구(수레)·창고(상자)·수비대(허수아비·창)가
>   집·헛간이 아님.
> - **TDD/실행**: `unity_meas` `EstateBuildingsSelfCheck` 전항 PASS
>   (`estate_hub_selfcheck.log`). `EstateYardSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_ESTATE_BUILDINGS=1`):
>   `estate_hub_shots/qa_go:Estate.png` 813786B — 영지 마을,
>   자막 `본성·광산·창고·수비대는 전용 그림이다(§16)`,
>   별 깃발 본성·광산 수레·창고 상자·수비대 허수아비. 옛 초가·헛간이 아님.
> - **네거티브**: `PropOf`를 항상 `OldOf`로 되돌리면 FAIL 5
>   (`estate_hub_negctrl.log` — 본성·광산·창고·수비대·대장간이 옛 이름).
>   `QA_NO`면 옛 집·헛간.
> - **정직한 미완**: 캐릭터/몹 화질·카메라 이동은 안 넣음. 경매장은 수레 유지.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
>   대화 세션 `W3Party`/`HuntBoon` WIP는 안 커밋했다.
> - **코드** `2d4c52e8`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 영지 전용 대장간·영묘·탑.**
> - INBOX 09:57이 큐보다 앞선다. ARTIFACT_INDEX: `village_house_*`는 필드 장식이며
>   기능 건물을 대체하지 않는다. 옛 PropOf는 대장간=작은 집·영묘=우물·탑=등불이었다.
> - **생산 소비처**: `EstateBuildings.DedicatedOf`/`PropOf`/`HasDedicated`/`Line`/
>   `SeedQaIfRequested`. `EstateYard.PropOf`가 읽는다. `EstateScreen` 자막이 `Line`을 읽는다.
>   `QA_NO_ESTATE_BUILDINGS=1`이면 옛 집·우물·등불. `QA_ESTATE_BUILDINGS=1`은 화살탑 1칸+자막.
>   힉스필드 nano_banana_2 · `out_estate_buildings/` · `props/estate_{smith,mausoleum,tower}_0`.
>   `W3Party`/`FieldDecor`/`GetPropNames`는 안 건드렸다(대화 세션 전투 WIP).
> - **통과 기준**: PropOf 대장간=`estate_smith_0` · 영묘=`estate_mausoleum_0` · 탑=`estate_tower_0`.
>   Resources 3장. 차단하면 집·우물·등불. 화면 `대장간·영묘·탑은 전용 그림이다(§16)` +
>   대장간(화로·모루)·석조 영묘(금별)·망루가 집·우물·등불이 아님.
> - **TDD/실행**: `unity_meas` `EstateBuildingsSelfCheck` 전항 PASS
>   (`estate_buildings_selfcheck.log`). `EstateYardSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_ESTATE_BUILDINGS=1`):
>   `estate_buildings_shots/qa_go:Estate.png` 789751B — 영지 마을,
>   자막 `대장간·영묘·탑은 전용 그림이다(§16)`,
>   왼쪽 대장간·회색 영묘·앞 망루. 옛 초가·우물·등불이 아님.
> - **네거티브**: `PropOf`를 항상 `OldOf`로 되돌리면 FAIL 3
>   (`estate_buildings_negctrl.log` — 대장간·영묘·탑이 옛 이름). `QA_NO`면 옛 프랍.
> - **정직한 미완**: 본성·광산·창고·수비대는 기존 집·헛간. 카메라 이동 없음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
>   대화 세션 `UnitSeparation`/`W3Party` WIP는 안 커밋했다.
> - **코드** `d6f068c5`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 필드 허브 HUD — 아래 도크 200.**
> - INBOX 09:57이 큐보다 앞선다. 클래시·킹덤·하데스·뱀서·가디언테일즈·AFK아레나·
>   데드셀즈·스타듀·코어키퍼·문라이터는 세계가 화면을 채우고 HUD는 가장자리에만 있다.
>   필드 2×3 전폭(칸 169)이 본문 540을 전부 덮었다.
> - **생산 소비처**: `FieldHud.Cards`/`OverlayH`/`OpenH`/`Line`/`SeedQaIfRequested`.
>   `FieldScreen`이 읽는다. 선택 화면·경고는 전폭 유지. `QA_NO_FIELD_HUD=1`이면 옛 2×3.
>   `QA_FIELD_HUD=1`은 자막. `W3Party`는 안 건드렸다.
> - **통과 기준**: 겹침 200 < 본문 40%. 열린 배경 340. 도크 칸 95×396.
>   차단하면 칸 169·본문 위. 화면 `HUD는 필드를 가리지 않는다(§16)` +
>   들판 길이 전폭 카드 없이 보인다.
> - **TDD/실행**: `unity_meas` `FieldHudSelfCheck` 전항 PASS
>   (`field_hud_selfcheck.log`). `FieldBossSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_FIELD_HUD=1`):
>   `field_hud_shots/qa_go:Field.png` 802095B — 필드,
>   자막 `HUD는 필드를 가리지 않는다(§16)`,
>   들판·길·울타리. 아래 3×2 도크 6칸. 옛 2×3 전폭이 아님.
> - **네거티브**: `Cards`를 옛 2×3으로 되돌리면 FAIL 2
>   (`field_hud_negctrl.log` — 도크 y 100, 칸 169). `QA_NO`면 옛 540.
> - **정직한 미완**: 헤더 88·경매 전폭 막대는 안 줄임. 도크 부제 글씨는 좁다.
>   전용 건물 그림·캐릭터/몹 화질은 안 넣음. 원본 에디터 PID 75776은 죽이지 않았고
>   사본으로 빌드·촬영. 대화 세션 `3bc8ce2c` 로컬 Play 시드는 안 건드렸다.
> - **코드** `e75b9825`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 영지 마을 HUD — 선택 전 겹침 44.**
> - INBOX 09:45가 큐보다 앞선다. 클래시·킹덤은 HUD가 가장자리에만 있다.
>   전면 마을을 채운 뒤에도 안내 86+팔레트 68 전폭이 헛간·수레를 덮었다.
> - **생산 소비처**: `EstateHud.OverlayH`/`InspectH`/`PaletteH`/`PaletteTiles`/
>   `ShowInspectBar`/`Line`/`SeedQaIfRequested`. `EstateScreen` 마을 탭이 읽는다.
>   선택 전 안내 없음. 팔레트는 가운데 4칸 도크. `QA_NO_YARD_HUD=1`이면 옛 154.
>   `QA_YARD_HUD=1`은 자막. `W3Party`는 안 건드렸다.
> - **통과 기준**: 선택 없음 겹침 44. 선택 80. 도크 폭 226 < 전폭 40%.
>   차단하면 154·전폭 카드. 화면 `HUD는 마을을 가리지 않는다(§16)` +
>   헛간·수레가 전폭 안내 없이 보인다.
> - **TDD/실행**: `unity_meas` `EstateHudSelfCheck` 전항 PASS
>   (`estate_hud_selfcheck.log`). `EstateYardSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_YARD_HUD=1`):
>   `estate_hud_shots/qa_go:Estate.png` 773697B — 영지 마을,
>   자막 `HUD는 마을을 가리지 않는다(§16)`,
>   집·헛간·우물·수레. 가운데 도크 4칸. 옛 전폭 안내·4장 카드가 아님.
> - **네거티브**: `InspectH`를 옛 86으로 되돌리면 FAIL 4
>   (`estate_hud_negctrl.log` — 선택 없음 130). `QA_NO`면 옛 154.
> - **정직한 미완**: 필드 6장 카드·헤더 88·경매 전폭 막대는 안 줄임.
>   도크 글씨는 좁다. 원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `b69bf890`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 증표 경매 시세 하한 — T1 200골드.**
> - 큐 1번은 움직임·겹침이 W3Party/FieldDecor라 대기하지 않음. §18-4 ✅
>   `특수 직업 전직 증표 | 200~400배`. `LifePrice`는 목숨 3종만 보고
>   `ListPrice`는 증표를 25골드 고정이라 표의 200골드보다 쌌다.
> - **생산 소비처**: `TokenPrice.Floor`/`BelowFloor`/`Line`/`SeedQaIfRequested`.
>   `AuctionTrade.ListPrice`가 하한을 낸다. `TryListItem`이 하한 아래를 거절.
>   `QA_NO_TOKEN_PRICE=1`이면 옛 25골드. `QA_TOKEN_PRICE=1`은 30층·T1·증표.
>   `W3Party`는 안 건드렸다.
> - **통과 기준**: T1 증표 2000000. 1999999 거절. T5 ≈1311골드. T6 ≈2097골드.
>   차단하면 250000. 화면 `증표 시세 하한 · 200골드(§18-4)`.
> - **TDD/실행**: `unity_meas` `TokenPriceSelfCheck` 전항 PASS
>   (`token_price_selfcheck.log`). `AuctionTradeSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_TOKEN_PRICE=1`):
>   `token_price_shots/qa_go:Estate.png` 709624B — 영지·경매장,
>   자막 `증표 시세 하한 · 200골드(§18-4)`,
>   `등록 특수 직업 증표 1` `수수료 2골드 80실버 · 200골드`.
>   옛 증표 25골드가 아님.
> - **네거티브**: `TryListItem`에서 `BelowFloor`를 빼면 FAIL 5
>   (`token_price_negctrl.log` — 옛 25골드가 등록). `QA_NO`면 옛 25골드.
> - **정직한 미완**: 상한 400 G/h는 안 넣음(표가 하한~상한, 하한만). 증표 NPC 매물은 없음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `b452cba3`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 영지 전면 마을 — 집이 화면을 채운다.**
> - INBOX 09:18이 큐보다 앞선다. 쿠키런 킹덤·클래시 오브 클랜은 마을이 화면을
>   채우고 HUD가 위에 얹힌다. 옛 전경은 칸 상한 88 + 정보/팔레트 여백이라
>   마름모가 가운데 체스판이었다. `village_house_*`는 FieldDecor만 읽고 영지는
>   UI 아이콘만 썼다(소비처 0곳 계열).
> - **생산 소비처**: `EstateYard.VillageRect`/`TileW`/`PropOf`/`Line`.
>   `EstateScreen` 마을 탭이 본문을 채운 뒤 탭·검사·팔레트를 얹는다.
>   본성=`village_house_1` · 창고=`village_house_0` · 광산=`village_barn_0` ·
>   대장간=`village_house_2` · 성벽=`village_fence_0`. 마젠타는 그릴 때 뺀다.
>   `QA_NO_YARD_FILL=1`이면 옛 88 상한·여백. `W3Party`는 안 건드렸다.
> - **통과 기준**: 본문 1208×540 → 칸 116.8 · 마름모 폭 934 · 높이 620.
>   차단하면 칸 88 · 높이 234. 화면 `마을이 화면을 채운다. 집을 누르면 들어간다(§16)`
>   + 집·헛간·우물이 아이콘 체스판이 아님.
> - **TDD/실행**: `unity_meas` `EstateYardSelfCheck` 전항 PASS
>   (`estate_yard_selfcheck.log`). `EstateGridSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `go:Estate`):
>   `estate_yard_shots/qa_go:Estate.png` 821792B — 영지 마을,
>   자막 `마을이 화면을 채운다. 집을 누르면 들어간다(§16)`,
>   빨간 지붕 본성·초가 창고·대장간·우물·헛간 두 채·수레.
>   옛 작은 아이콘 마름모가 아님.
> - **네거티브**: `TileW`를 옛 88 상한으로 되돌리면 FAIL 2
>   (`estate_yard_negctrl.log` — 칸 88.0, 마름모 704). `QA_NO`면 옛 여백.
> - **정직한 미완**: 대장간·영묘·탑 전용 그림은 안 만들었다(기존 집·우물·등불).
>   카메라 이동·확대는 없다. 원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `9e3793e5`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 영묘 누적 출전 시간 — 전투·일정이 초를 남긴다.**
> - 큐 1번은 움직임·겹침이 W3Party/FieldDecor라 대기하지 않음. §4 ✅ 영묘 기록의
>   `누적 출전 시간`만 소비처 0곳이었다. 전투 `_t`와 일정 `Tick`이 이미 시계다.
> - **생산 소비처**: `SortieTime.Apply`/`AddToIndexes`/`Seconds`/`Line`/`Format`/
>   `SeedQaIfRequested`. `BattleScreen.RecordSortie`가 한 판의 초를 편성에 더한다.
>   `HuntSchedule.Tick`이 흐른 초를 일정 명부에 더한다. `Memorial.TimeLine`이 줄을 읽는다.
>   영묘·캐릭터가 삭제 칸에서 읽는다. `QA_NO_SORTIE_TIME=1`이면 0. `QA_MEMORIAL=1`은
>   추모시험+1시간. `W3Party`는 안 건드렸다.
> - **통과 기준**: Apply 3600 → 출전 1시간 0분. 1초 미만 0. 일정 Tick 3600=3600.
>   재기동 유지. 차단하면 0. 화면 `출전 1시간 0분(§4)` ·
>   `이름·직업·최고 층·사망 원인·마지막 동료·누적 출전을 남긴다(§4)`.
> - **TDD/실행**: `unity_meas` `SortieTimeSelfCheck` 전항 PASS
>   (`sortie_time_selfcheck.log`). `MemorialSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_MEMORIAL=1`):
>   `sortie_time_shots/qa_go:Estate.png` 728838B — 영지·영묘,
>   자막 `이름·직업·최고 층·사망 원인·마지막 동료·누적 출전을 남긴다(§4)`,
>   `추모시험 · 수호기사 Lv50` · `30층 · 탑 · 보스전 전멸(§4)` ·
>   `동료 힐러(§4)` · `출전 1시간 0분(§4)`.
>   옛 층·동료만 있던 영묘가 아님.
> - **네거티브**: `BattleScreen`에서 `SortieTime.Apply`를 빼면 FAIL 1
>   (`sortie_time_negctrl.log` — 전투가 Apply를 읽는다). `QA_NO`면 0.
> - **정직한 미완**: 생전 스킬 목록·환생 스킬 1개 선택은 안 넣음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `e769f877`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 목숨 아이템 경매 시세 하한 — 환생석 150 G/h.**
> - 큐 1번은 움직임·겹침이 W3Party/FieldDecor라 대기하지 않음. 같은 시각 대화 세션이
>   UI 파일을 고치고 있어(`0d8e50da`) 그 트랙은 안 건드렸다. §4 ✅ `경매장에서도 싸지 않다`
>   · §18-4 ✅ 두루마리 2 / 부활초 3 / 환생석 150 G/h. `ListPrice`는 T1 환생석을 20골드로
>   팔아 표의 150골드보다 쌌고, `TryListItem`은 아무 가격이나 받았다.
> - **생산 소비처**: `LifePrice.Hours`/`Copper`/`Floor`/`BelowFloor`/`Line`/`SeedQaIfRequested`.
>   `AuctionTrade.ListPrice`가 하한을 낸다. `TryListItem`이 하한 아래를 거절.
>   NPC 부활초도 `Floor`. `QA_NO_LIFE_PRICE=1`이면 옛 20골드. `QA_LIFE_PRICE=1`은
>   30층·T1·환생석. `W3Party`는 안 건드렸다.
> - **통과 기준**: T1 환생석 1500000·부활초 30000·두루마리 20000. 1499999 거절.
>   T5 환생석 ≈983골드. 차단하면 200000. 화면 `목숨 시세 하한 · 환생석 150골드(§18-4)`.
> - **TDD/실행**: `unity_meas` `LifePriceSelfCheck` 전항 PASS
>   (`life_price_selfcheck.log`). `AuctionTradeSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_LIFE_PRICE=1`+`QA_AUCTION_TRADE=1`):
>   `life_price_shots/qa_go:Estate.png` 709571B — 영지·경매장,
>   자막 `목숨 시세 하한 · 환생석 150골드(§18-4)`,
>   NPC `구매 부활초` `3골드`, `등록 환생석 1` `수수료 2골드 10실버 · 150골드`.
>   옛 환생석 20골드·부활초 4골드가 아님.
> - **네거티브**: `TryListItem`에서 `BelowFloor`를 빼면 FAIL
>   (`life_price_negctrl.log` — 하한 −1이 등록). `QA_NO`면 옛 20골드.
> - **정직한 미완**: 증표 시세 200~400 G/h는 안 넣음(표가 목숨 3종). 파산 처분가는
>   옛 고정. 원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `89ef2269`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): ui 글씨 — 넓은 카드는 아이콘 왼쪽 · 글씨 가운데.**
> - INBOX 08:37이 큐보다 앞선다. 필드 2×3(≈596×169)이 높이 168만 보고 세로 배치돼
>   제목이 아래 금테에 붙었다. 사냥 선택은 초상을 칸 가운데에 둬 이름이 초상을 가로질렀다.
> - **생산 소비처**: `UiPages.IsWideCard`/`CardWideAspect`. `CardLayout`이 가로비 1.45
>   이상을 가로로 본다. `PartyCardLayout`이 1.35 이상이면 초상 왼쪽.
>   `DrawCard`/`DrawHuntCard`/`DrawPartyCard`가 그대로 읽는다. `W3Party`는 안 건드렸다.
> - **통과 기준**: 596×169 아이콘 x < 제목 x, 제목 중심이 카드 중심 ±24.
>   397×132 초상 왼쪽·이름 가운데·목숨 오른쪽, 세 칸이 안 겹친다.
>   280×220 시작 카드는 아이콘이 위(세로 유지).
> - **TDD/실행**: `unity_meas` `UiAtlasSelfCheck` PASS (`ui_text_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님):
>   `ui_text_shots/qa_go:Field.png` 1017099B — 사냥 시작·던전 글씨가 아이콘 옆 가운데,
>   `ui_text_shots/qa_go_Field_huntpick.png` 889066B — `힐러 · 편성됨`이 초상 옆,
>   `ui_text_shots/qa_go_Estate_status.png` 916218B — 본성·광산 글씨가 가운데,
>   `ui_text_shots/qa_go:Estate.png` 749212B — 마을 탭·격자·팔레트 동작,
>   `ui_text_shots/qa_go:Title.png` — 게임 시작·이어하기 글씨가 카드 안.
> - **네거티브**: `IsWideCard`를 false로 되돌리면 단언 4건이 로그에 뜬다
>   (`ui_text_negctrl.log` — 596×169이 다시 세로, 사냥 초상이 가운데).
> - **정직한 미완**: 캐릭터창 3열 명부는 좁다. 장비 둘레 라벨이 잘린다.
>   샷의 `추모시험`은 이전 영묘 QA 잔재. INBOX 08:47 움직임·겹침·비율은
>   W3Party라 안 넣음. 원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `4570ab55`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 긴급 탈출 보상 포기 — 목숨 그대로 · 골드·경험 0.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §3 ✅
>   `전투 중 이탈은 그 판의 손해` · §4 ✅ `해당 런의 보상은 포기`.
>   `EmergencyEscape`는 6초 캐스트만 있고 결과 소비처가 0곳이었다.
>   옛 경로는 `Go(Estate)`라 줄이 없고 던전·침략 대기가 남을 수 있었다.
> - **생산 소비처**: `EscapeForfeit.Apply`/`Line`/`Body`/`SeedQaIfRequested`.
>   `BattleScreen.LeaveByEscape`가 Escaped에서 읽는다. `ResultScreen`이 줄을 읽는다.
>   `DungeonRun.End`. `InvasionState.AbortPending`은 패배 추가 소모·명예·보호막 없음.
>   출정비는 이미 낸 채로 남는다. `QA_NO_ESCAPE_FORFEIT=1`이면 옛 Estate 직행.
>   `QA_ESCAPE_FORFEIT=1`은 결과 화면. `W3Party`는 안 건드렸다.
> - **통과 기준**: Apply 뒤 골드·경험·목숨 불변. 보상 비움. 던전 Active 해제.
>   침략 대기 취소·약탈 0·패배 추가 0. 차단하면 줄 없음.
>   화면 `긴급 탈출 — 이번 판 보상 포기(§4)` · `목숨은 그대로. 전리품·경험치는 없다(§3·§4)`.
> - **TDD/실행**: `unity_meas` `EscapeForfeitSelfCheck` 전항 PASS
>   (`escape_forfeit_selfcheck.log`). `EmergencyEscapeSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_ESCAPE_FORFEIT=1`):
>   `escape_forfeit_shots/qa_go:Result.png` 714023B — 결과,
>   `긴급 탈출 — 이번 판 보상 포기(§4)`,
>   `목숨은 그대로. 전리품·경험치는 없다(§3·§4)`.
>   획득 골드 줄 없음. 옛 영지 직행이 아님.
> - **네거티브**: `BattleScreen`에서 `EscapeForfeit.Apply`를 빼면 FAIL 1
>   (`escape_forfeit_negctrl.log` — 전투가 Apply를 읽는다). `QA_NO`면 줄 없음.
> - **정직한 미완**: 생존형 HP 50% 자동 이탈은 W3Party라 안 넣음. 저체력 귀환은
>   기존 Estate 직행 유지. 원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `bd4ab945`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 필드 배회 보스 — 허브 출현 · 보스전 · FieldDungeonBoss.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §10-1 ✅
>   `필드 보스 | 오픈월드 배회`. 표와 §5 지휘 규칙만 있고 필드 소비처가 0곳이었다.
> - **생산 소비처**: `FieldBoss.Tick`/`SpawnNow`/`Consume`/`BeginFight`/`EndFight`/
>   `DropSource`/`FightFloor`/`Line`/`CardTitle`/`CardBody`/`BattleTitle`.
>   `FieldScreen` 2×3 마지막 칸. `GameFlow.GoBattle`이 Fighting이면 탑 풀을 안 뽑는다.
>   `BattleScreen`이 `FieldDungeonBoss`를 읽고 탑 층을 안 올린다.
>   `RaidScale.Applies`/`RaidBossPool.Applies`가 Fighting을 읽는다.
>   T1=5층·T2=15·T10=95. `QA_NO_FIELD_BOSS=1`이면 출현 없음.
>   `QA_FIELD_BOSS=1`은 T1 재의 야수. 다중 3체·배회 스프라이트는 💡/W3Party.
>   `W3Party`는 안 건드렸다.
> - **통과 기준**: T1 층 5·이름 배회하는 재의 야수. T2 층 15.
>   입장 중 하위 레이드 스케일·탑 풀 없음. 골드 2500(원래 층).
>   차단하면 출현 0. 화면 `배회 보스 20:00` · `환생석 없음(§10-1·§10-8)`.
> - **TDD/실행**: `unity_meas` `FieldBossSelfCheck` 전항 PASS
>   (`field_boss_selfcheck.log`). `RaidScale`·`RaidBossPool` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_FIELD_BOSS=1`):
>   `field_boss_shots/qa_go:Field.png` 794075B — 필드 2×3,
>   자막 `배회 보스 배회하는 재의 야수(§10-1)`,
>   카드 `배회 보스 20:00` · `배회하는 재의 야수 · 준비 없이 만나면 위험 ·
>   환생석 없음(§10-1·§10-8)`. 옛 사망 없음 잠김 칸이 아님.
> - **네거티브**: `BattleScreen`에서 `FieldBoss.DropSource`를 빼면 FAIL 1
>   (`field_boss_negctrl.log`). `QA_NO`면 출현 없음.
> - **정직한 미완**: 전장 위 배회 스프라이트는 W3Party라 안 넣음. 다중 3체는 💡.
>   출현은 허브 카드(레이드급과 같은 첫 슬라이스). 샷 지갑 50골드는 이전 QA 잔재.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `fe95b78f`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 대출 순자산 — 장비·영지 평가를 읽는다.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §18-5 ✅
>   `한도 | 순자산(장비+영지 평가액)의 30%` 와 `20 G/h` 중 작은 값.
>   `LoanLimitCopper`는 있었고 `GameState.LoanLimit`가 지갑만 봐 평가가 0곳이었다.
> - **생산 소비처**: `NetWorth.Assets`/`KeepCopper`/`GearCopper`/`DefenseCopper`/`Line`.
>   본성은 올린 건설비 합. 방어는 같은 공식 40%. 장비는 파산 처분가.
>   `LoanLimit`가 `Assets − 부채`를 읽는다. `QA_NO_NET_WORTH=1`이면 옛 지갑만.
>   `QA_NET_WORTH=1`은 본성 3+흉갑·지갑 0. 영지 현황·탑 대출이 줄을 읽는다.
>   `W3Party`는 안 건드렸다.
> - **통과 기준**: 지갑 0·본성 1 = 한도 0. 지갑 10골드 = 3골드.
>   본성 3·지갑 0 = 자산 300000·한도 90000. 흉갑만 3600.
>   본성+흉갑 312000·한도 93600. 화살탑 1이 48000을 더함.
>   빌린 돈은 한도를 안 올린다. 차단하면 0. 재기동 유지.
>   화면 `순자산 31골드 20실버 · 한도 9골드 36실버(§18-5)`.
> - **TDD/실행**: `unity_meas` `NetWorthSelfCheck` 전항 PASS
>   (`net_worth_selfcheck.log`). `LoanSanctionSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_NET_WORTH=1`):
>   `net_worth_shots/qa_go:Estate.png` 916080B — 영지·현황,
>   본성 `Lv3 · 창고 36골드 · 순자산 31골드 20실버 · 한도 9골드 36실버(§18-5)`.
>   창고 0쿠퍼. 옛 지갑만 쓰던 한도가 아님.
> - **네거티브**: `LoanLimit`를 지갑만으로 되돌리면 FAIL 7
>   (`net_worth_negctrl.log` — 본성 3·지갑 0이 한도 0). `QA_NO`면 지갑만.
> - **정직한 미완**: 가방 재료·명예는 평가에 안 넣음(표가 장비+영지).
>   샷의 세계 T3는 시드 30층. 원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `edf5d730`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 영묘 마지막 파티 동료 — 출전 이름을 남긴다.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §4 ✅
>   `마지막 파티 동료`. 층·출전·원인·장착은 `3057f302`에 있고 동료만 0곳이었다.
>   시계가 없어서가 아니다. `PartyState.SortieRecords`가 이미 있다.
> - **생산 소비처**: `Memorial.FormatParty`/`PartyLine`. `Stamp`가 죽은 본인을 빼고 찍는다.
>   편성이 본인뿐이면 `혼자 출전`. 로스터 15번째 탭 필드. 영묘·캐릭터가 줄을 읽는다.
>   `QA_NO_MEMORIAL=1`이면 기록 없음. `QA_MEMORIAL=1`은 추모시험+힐러.
>   시드는 일정·수비를 비운 뒤 슬롯 0·1을 고정한다. `W3Party`는 안 건드렸다.
> - **통과 기준**: 편성 0+1 → `동료 힐러(§4)`. 본인만 → `혼자 출전(§4)`.
>   재기동 유지. 차단하면 기록 없음. PvP는 안 찍는다.
>   화면 `동료 힐러(§4)` · `이름·직업·최고 층·사망 원인·마지막 동료를 남긴다(§4)`.
> - **TDD/실행**: `unity_meas` `MemorialSelfCheck` 전항 PASS
>   (`memorial_party_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님, `QA_MEMORIAL=1`):
>   `memorial_party_shots/qa_go:Estate.png` 557178B — 영지·영묘,
>   `이름·직업·최고 층·사망 원인·마지막 동료를 남긴다(§4)`,
>   `추모시험 · 수호기사 Lv50` · `30층 · 탑 · 보스전 전멸(§4)`,
>   `무기 송곳니 검 · 투구 유골 투구 · 갑옷 가죽 흉갑 · 장갑 부품 장갑 ·
>   신발 원소 신발 · 장신구 마정 장신구` · `동료 힐러(§4)`.
>   옛 이름·층만 있던 영묘가 아님.
> - **네거티브**: `Stamp`에서 `FormatParty`를 빼면 FAIL 4
>   (`memorial_party_negctrl.log` — 동료 줄이 `혼자 출전`). `QA_NO`면 기록 없음.
> - **정직한 미완**: 누적 출전 시간·생전 스킬 목록은 안 넣음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `862d659c`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 10층 대보스 입장 — 0.15 G/h. 5층은 0.10.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §18-2 ✅
>   `5층 중간 레이드 0.10` · `10층 대보스 0.15`.
>   `Tower10Boss` 키는 표에만 있고 `GetActionCost` 호출이 0곳이었다.
>   탑 레이드 버튼은 항상 `Tower5BossRaid`만 냈다.
> - **생산 소비처**: `RaidCost.ActionKey`/`Copper`/`IsMega`/`Line`/`FormatLine`/
>   `SeedQaIfRequested`. `TowerScreen` 레이드 카드가 `Copper`를 낸다.
>   자막·카드가 `Line`을 읽는다. `RaidReroll.Cost`도 층을 넘긴다.
>   `QA_NO_RAID_MEGA=1`이면 10층도 5층 요금. `QA_RAID_MEGA=1`은 10층.
>   `W3Party`는 안 건드렸다.
> - **통과 기준**: T1 5층=1000 · 10층=1500 · 15층=1000. T2 10층=2400.
>   드워프 1200. 차단하면 1000. 화면 `대보스 15실버(§18-2)`.
> - **TDD/실행**: `unity_meas` `RaidCostSelfCheck` 전항 PASS
>   (`raid_cost_selfcheck.log`). `RaidRerollSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_RAID_MEGA=1`):
>   `raid_mega_shots/qa_go:Tower.png` 1009243B — 탑 · 10층,
>   자막 `대보스 15실버(§18-2)`,
>   카드 `레이드 (5층 단위)` 같은 문구 · `5층마다 보스, 10층 단위는 대보스(§9)`.
>   옛 5층 요금만 쓰던 카드가 아님.
> - **네거티브**: `ActionKey`를 항상 5층 키로 되돌리면 FAIL 12
>   (`raid_cost_negctrl.log` — T1 10층 1000). `QA_NO`면 1000.
> - **정직한 미완**: 하위 카드는 여전히 5층만(`LowerRaidFloor=5`).
>   샷 지갑 1168골드는 이전 QA 잔재(대보스 문구는 보임).
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `aa3f91b1`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 필드 자동사냥 일정 — 허브에서도 돌고 사망은 없다.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §6 ✅
>   `자동으로 돌아가는 부분(자동사냥 등)은 유저가 스케줄을 정해 지시 가능`.
>   필드 4칸에는 사냥·던전·레이드·저체력만 있어 일정 소비처가 0곳이었다.
> - **생산 소비처**: `HuntSchedule.TryStart`/`Tick`/`Stop`/`PendingGold`/`PendingExp`/
>   `Line`/`CardTitle`/`CardBody`/`SeedQaIfRequested`. `GameScreen.Update`가 Tick.
>   `PartyState`가 `Contains`를 읽어 출전에서 뺀다. 필드 2×3 카드·자막이 문구를 읽는다.
>   `QA_NO_HUNT_SCHEDULE=1`이면 시작·정산 0. `QA_HUNT_SCHEDULE=1`은 1시간 대기 1골드.
>   일과표 타임라인·조건부 지시·오프라인 60%는 💡라 안 넣음. `W3Party`는 안 건드렸다.
> - **통과 기준**: T1 3600초=10000. 12시간+1초도 12시간. 전원 일정이면 출전 거부.
>   정산 골드 10000·사망 0. 차단하면 0. 재기동 골드 유지.
>   화면 `일정 사냥 1명 · 1시간 0분 · 1골드(§6)` · `일정 사냥 중`.
> - **TDD/실행**: `unity_meas` `HuntScheduleSelfCheck` 전항 PASS
>   (`hunt_schedule_selfcheck.log`). `HuntGold`·`HuntStart` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_HUNT_SCHEDULE=1`):
>   `hunt_schedule_shots/qa_go:Field.png` 1043337B — 필드 2×3,
>   자막 `일정 사냥 1명 · 1시간 0분 · 1골드(§6)`,
>   카드 `일정 사냥 중` · 같은 문구 · `탭하면 정산. 사망 없음(§6)`,
>   `사망 없음` 잠김 카드. 옛 4칸만 있던 필드가 아님.
> - **네거티브**: `Stop`에서 `Settle`을 빼면 FAIL 3
>   (`hunt_schedule_negctrl.log` — 정산 골드 0). `QA_NO`면 시작 거부.
> - **정직한 미완**: 오프라인(앱 종료) 60%·12시간 벽시계는 안 넣음. 일과표 드래그·
>   HP 30% 귀환 조건은 💡. 샷 지갑 1118골드는 이전 QA 잔재(일정 문구는 보임).
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `7d6c78a6`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 경매 — 드랍·제작만 거래, 칭호·명예는 귀속.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §12 ✅
>   `거래 가능 = 드랍·제작으로 얻은 아이템만` · `업적·도전 보상(칭호·스킨)·명예는 귀속`.
>   `TryListItem`이 증표만 막아서 드랍 품목을 거절했고, 화면은 가죽만 등록했다.
> - **생산 소비처**: `AuctionTrade.CanList`/`CanListBound`/`WhyCannotListBound`/
>   `TradeLine`/`ListPrice`/`TryFirstBag`/`SeedQaIfRequested`.
>   `AuctionState.TryListItem`이 `CanList`를 읽는다. `TryListBound`는 칭호·스킨·명예를 거절.
>   `EstateScreen` 경매 자막·본문·등록 줄이 문구와 첫 가방을 읽는다.
>   `QA_NO_AUCTION_TRADE=1`이면 증표만 옛처럼 거절. `QA_AUCTION_TRADE=1`은
>   인간+30층+증표 1. `W3Party`는 안 건드렸다.
> - **통과 기준**: 가죽·부활초·환생석·증표·강화석·두루마리 등록. 칭호·스킨·명예 거부.
>   증표 등록가 250000·수수료 3500. 재기동 유지. 차단하면 증표 거부·가죽은 등록.
>   화면 `드랍·제작만 거래 · 칭호·명예는 귀속(§12)` · `등록 특수 직업 증표 1`.
> - **TDD/실행**: `unity_meas` `AuctionTradeSelfCheck` 전항 PASS
>   (`auction_trade_selfcheck.log`). `AuctionExpireSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_AUCTION_TRADE=1`):
>   `auction_trade_shots/qa_go:Estate.png` 482488B — 영지·경매장,
>   자막 `드랍·제작만 거래 · 칭호·명예는 귀속(§12)`,
>   본문 같은 문구, `등록 특수 직업 증표 1` · `수수료 35실버 · 25골드`.
>   옛 증표 거절·가죽만 등록이 아님.
> - **네거티브**: `TryListItem`에서 `CanList`를 옛 증표 거절로 되돌리면 FAIL 10
>   (`auction_trade_negctrl.log` — 증표 등록 실패). `QA_NO`면 증표 거부.
> - **정직한 미완**: 다른 유저 서버 체결은 없다. 명예 상점 아이템은 아직 없다.
>   샷 지갑 1068골드는 이전 QA 잔재(등록 줄은 보임).
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `c25929ef`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 대장간 — 1차 전직 시점에 열린다.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §13-2 ✅
>   `대장간 | 사냥 드랍 재료로 장비 제작(§11) | 1차 전직 시점`.
>   안은 `SmithUnlocked`를 읽었지만 허브 카드는 항상 열려 소비처가 0곳이었다.
> - **생산 소비처**: `Equipment.LockReason`/`LockLine`/`UnlockBlocked`/
>   `SeedUnlockQaIfRequested`. `SmithUnlocked`가 기본직업만이면 잠김.
>   삭제된 1·2차는 안 연다. `QA_NO_SMITH_UNLOCK=1`이면 기본직업도 연다.
>   `QA_SMITH_UNLOCK=1`은 전원 기본직업·잠긴 카드. `EstateScreen` 허브 카드·
>   대장간 안·자막이 문구를 읽는다. `TryCraft`는 그대로 `SmithUnlocked`를 읽는다.
>   `W3Party`는 안 건드렸다.
> - **통과 기준**: 기본직업만이면 잠김·제작 거부. 1차·2차가 연다.
>   삭제된 1차는 안 연다. 재기동 유지. 차단하면 기본직업도 제작.
>   화면 `잠김 — 1차 전직 시 해금 — 기본직업만 있으면 제작하지 않는다(§13-2)`.
> - **TDD/실행**: `unity_meas` `SmithUnlockSelfCheck` 전항 PASS
>   (`smith_unlock_selfcheck.log`). `EquipmentSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_SMITH_UNLOCK=1`):
>   `smith_unlock_shots/qa_go:Estate.png` 759527B — 영지·건물,
>   자막 `1차 전직 시 해금 — 기본직업만 있으면 제작하지 않는다(§13-2)`,
>   대장간 `잠김 — 1차 전직 시 해금 — 기본직업만 있으면 제작하지 않는다(§13-2)`.
>   옛 층·전직과 무관하게 열리던 카드가 아님.
> - **네거티브**: `SmithUnlocked`를 항상 열림으로 되돌리면 FAIL 8
>   (`smith_unlock_negctrl.log` — 기본직업 제작, 시드 잠김 실패).
>   `QA_NO`면 기본직업도 열림.
> - **정직한 미완**: 환생 스킬 1개 선택·생전 스킬 목록은 안 넣음.
>   잠긴 대장간 카드의 건물 그림은 어두운 스프라이트라 잘 안 보인다(기능과 무관).
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `ca8ddb42`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 영묘 — 첫 캐릭터 삭제에 열린다.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §13-2 ✅
>   `영묘 | 삭제 캐릭터 기록 보관, 환생석 사용처(§4) | 첫 캐릭터 삭제 시`.
>   허브 카드는 삭제와 무관하게 열려 소비처가 0곳이었다.
> - **생산 소비처**: `Memorial.Unlocked`/`LockReason`/`LockLine`/`Open`/
>   `SeedUnlockQaIfRequested`. `RegisterDeath`가 삭제 확정 때 `Open`을 읽는다.
>   환생해도 플래그는 남는다. 옛 저장은 지금 삭제 명부가 있으면 연다.
>   `EstateScreen` 허브 카드·영묘 안·자막이 문구를 읽는다.
>   `QA_NO_MAUSOLEUM_UNLOCK=1`이면 삭제 없어도 연다.
>   `QA_MAUSOLEUM_UNLOCK=1`은 삭제 0·잠긴 카드. `W3Party`는 안 건드렸다.
> - **통과 기준**: 삭제 0은 잠김. 1회 사망은 잠김. 3회 삭제가 연다.
>   특수 직업 1회도 연다. PvP는 안 연다. 환생 뒤에도 열림. 재기동 유지.
>   차단하면 삭제 없어도 열림. 화면 `잠김 — 첫 캐릭터 삭제 시 해금`.
> - **TDD/실행**: `unity_meas` `MausoleumUnlockSelfCheck` 전항 PASS
>   (`mausoleum_unlock_selfcheck.log`). `MemorialSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_MAUSOLEUM_UNLOCK=1`):
>   `mausoleum_unlock_shots/qa_go:Estate.png` 998108B — 영지·건물,
>   자막 `첫 캐릭터 삭제 시 해금 — 3회 사망한 캐릭터가 여기 잠든다(§13-2)`,
>   영묘 `잠김 — 첫 캐릭터 삭제 시 해금 — 3회 사망한 캐릭터가 여기 잠든다(§13-2)`.
>   옛 층·삭제와 무관하게 열리던 카드가 아님.
> - **네거티브**: `RegisterDeath`에서 `Open`을 빼고 폴백 저장도 빼면 FAIL 3
>   (`mausoleum_unlock_negctrl.log` — 환생 뒤 잠김, 재기동 잠김, 소스).
>   `QA_NO`면 삭제 없어도 열림.
> - **정직한 미완**: 대장간 허브 카드는 안이 `SmithUnlocked`만 읽고 허브는 항상 연다.
>   잠긴 영묘 카드의 건물 그림은 어두운 스프라이트라 잘 안 보인다(기능과 무관).
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `aaa8f43b`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 수비대 주둔지 — 탑 30층(침략과 같다)에 열린다.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §13-2 ✅
>   `수비대 주둔지 | 보유 캐릭터 최대 5명 배치 | 30층(침략 해금)`.
>   허브 카드는 층과 무관하게 열려 소비처가 0곳이었다.
> - **생산 소비처**: `DefenseState.Unlocked`/`LockReason`/`LockLine`/`SeedUnlockQaIfRequested`.
>   `Toggle`이 넣을 때만 30층을 읽는다. 해임은 잠겨도 된다.
>   `EstateScreen` 허브 카드·수비대 안·자막이 문구를 읽는다.
>   `QA_NO_DEFENSE_UNLOCK=1`이면 1층도 연다. `QA_DEFENSE_UNLOCK=1`은 29층.
>   회복 시드는 배치 전에 30층으로 올린다. `W3Party`는 안 건드렸다.
> - **통과 기준**: 1·29층 배치 거부. 30층 배치. 잠겨도 해임.
>   재기동 유지. 차단하면 1층 배치. 화면 `잠김 — 탑 30층 달성 시 해금(현재 29층)`.
> - **TDD/실행**: `unity_meas` `DefenseUnlockSelfCheck` 전항 PASS
>   (`defense_unlock_selfcheck.log`). `DefenseRecover`·`DefenseState` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_DEFENSE_UNLOCK=1`):
>   `defense_unlock_shots/qa_go:Estate.png` 661993B — 영지·건물,
>   자막 `탑 30층 달성 시 해금(현재 29층) — 침략과 같다(§13-2)`,
>   수비대 `잠김 — 탑 30층 달성 시 해금(현재 29층) — 침략과 같다(§13-2)`.
>   옛 층과 무관하게 열리던 카드가 아님.
> - **네거티브**: `Toggle`에서 `Unlocked`를 빼면 FAIL 6
>   (`defense_unlock_negctrl.log` — 1층 배치). `QA_NO`면 1층 열림.
> - **정직한 미완**: 영묘 첫 삭제 시 해금은 안 넣음. 방어 건물 탭(20층)은 그대로.
>   월드맵 수비대 카드는 침략 시뮬 없어 계속 잠김. 원본 에디터 PID 75776은
>   죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `a8878ce2`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 영묘 추모 — 최고 층·마지막 출전·사망 원인·장착 이름.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §4 ✅
>   `영묘에는 이름·직업·마지막 출전·최고 층·사망 원인을 남긴다`.
>   삭제 캐릭터는 이름·직업·레벨만 있어 소비처가 0곳이었다.
> - **생산 소비처**: `Memorial.Stamp`/`Line`/`GearLine`/`HubLine`/`ResultLine`.
>   `RegisterDeath`가 장착을 지우기 전에 찍는다. `UseRebornStone`이 횟수를 읽는다.
>   영묘·결과·캐릭터가 문구를 읽는다. 계정 `TowerFloor`·`ReturnTo`·`Kind`.
>   `QA_NO_MEMORIAL=1`이면 기록 없음. `QA_MEMORIAL=1`은 추모시험+30층 탑 보스전.
>   PvP는 안 찍는다. `W3Party`는 안 건드렸다.
> - **통과 기준**: 3회 삭제 → `30층 · 탑 · 보스전 전멸(§4)` + 장착 6이름.
>   특수 직업 1회 → `특수 직업 1회 사망`. 필드 → `필드 전멸`.
>   재기동 유지. 차단하면 기록 없음. 환생 횟수 1.
>   화면 `추모시험 · 수호기사 Lv50` · `30층 · 탑 · 보스전 전멸(§4)` ·
>   `무기 송곳니 검 · 투구 유골 투구 · 갑옷 가죽 흉갑`.
> - **TDD/실행**: `unity_meas` `MemorialSelfCheck` 전항 PASS
>   (`memorial_selfcheck.log`). `RebirthSelfCheck` 회귀 PASS. 정적 오류 0.
> - **화면**(직접 열음, 빈 화면 아님, `QA_MEMORIAL=1`):
>   `memorial_shots/qa_go:Estate.png` 701412B — 영지·영묘,
>   `이름·직업·최고 층·사망 원인을 남긴다(§4)`,
>   `추모시험 · 수호기사 Lv50` · `30층 · 탑 · 보스전 전멸(§4)`,
>   `무기 송곳니 검 · 투구 유골 투구 · 갑옷 가죽 흉갑 · 장갑 부품 장갑 ·
>   신발 원소 신발 · 장신구 마정 장신구`.
>   옛 이름·직업만 있던 영묘가 아님.
> - **네거티브**: `RegisterDeath`에서 `Memorial.Stamp`를 빼면 FAIL 14
>   (`memorial_negctrl.log` — 층 0, 기록 없음). `QA_NO`면 기록 없음.
> - **정직한 미완**: 생전 스킬 목록·환생 스킬 1개 선택은 안 넣음.
>   누적 출전 시간·마지막 파티 동료는 시계가 없다. 16-6 별자리 카드는 💡.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `3057f302`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 마지막 목숨 경고 — 삭제될 장착 6부위를 이름으로 보여 준다.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §4·§11 ✅
>   `마지막 목숨 출전 경고에서 삭제될 장비 6개를 직접 보여준다`.
>   필드·탑 경고는 일반 문구만 있어 소비처가 0곳이었다.
> - **생산 소비처**: `LastLifeWarn.HasAny`/`GearLine`/`GearRest`/`Title`/`Body`.
>   `FieldScreen`·`TowerScreen`이 읽는다. 출전 마지막 목숨의 무기·투구·갑옷 /
>   장갑·신발·장신구. 빈 칸은 `빈칸`. 가방 비장착은 안 나온다.
>   `QA_NO_LAST_LIFE_GEAR=1`이면 옛 일반 문구. `QA_LAST_LIFE_GEAR=1`은
>   마지막시험+6칸+경고. `W3Party`는 안 건드렸다.
> - **통과 기준**: DeathCount=2 + 6칸이면 이름 6개. 빈 장착은 `장착 없음`.
>   재기동 유지. 차단하면 이름 없음. 화면 `마지막시험 · 무기 송곳니 검 ·
>   투구 유골 투구 · 갑옷 가죽 흉갑` · `장갑 부품 장갑 · 신발 원소 신발 ·
>   장신구 마정 장신구` · `가방·창고는 남는다(§4·§11)`.
> - **TDD/실행**: `unity_meas` `LastLifeWarnSelfCheck` 전항 PASS
>   (`last_life_gear_selfcheck.log`). 정적 오류 0.
> - **화면**(직접 열음, 빈 화면 아님, `QA_LAST_LIFE_GEAR=1`):
>   `last_life_gear_shots/qa_go:Field.png` 539361B — 필드 경고,
>   `[주의] 마지막 목숨 캐릭터가 파티에 있습니다`,
>   `사망 시 캐릭터와 아래 장착 6부위가 사라진다. 가방·창고는 남는다(§4·§11)`,
>   `마지막시험 · 무기 송곳니 검 · 투구 유골 투구 · 갑옷 가죽 흉갑`,
>   `장갑 부품 장갑 · 신발 원소 신발 · 장신구 마정 장신구`.
>   옛 일반 문구만 있던 경고가 아님.
> - **네거티브**: `FieldScreen`에서 `GearLine`을 빼면 FAIL 1
>   (`last_life_gear_negctrl.log` — 필드가 경고를 안 읽음). `QA_NO`면 이름 없음.
> - **정직한 미완**: 경고는 명부 전체 마지막 목숨을 본다(기존과 같음).
>   샷 지갑 768골드는 이전 QA 잔재. 원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `64f94736`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 필드 사냥 골드 — T1 1시간 = 1골드.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §18-1 ✅
>   `G/h = 그 티어에서 필드 자동사냥 1시간의 수익` · `T1 기준: 1 G/h = 정확히 1골드/시간`.
>   필드 생존은 가죽·경험만 주고 골드 Earn이 0곳이었다.
> - **생산 소비처**: `Economy.WaveHuntGold`/`HuntGoldHourLine`/`HuntGoldLine`.
>   `BattleScreen` 필드 생존이 `GameState.Earn`으로 읽는다. 던전·탑·전멸·저체력 귀환은 안 탄다.
>   `QA_NO_HUNT_GOLD=1`이면 0. 필드 자막·결과 줄이 문구를 읽는다.
>   `QA_HUNT_GOLD=1`은 T1·1시간·결과 1골드. `W3Party`는 안 건드렸다.
> - **통과 기준**: T1 3600초=10000·240초=666. T2 3600초=16000.
>   Earn 10000. 차단하면 0. 재기동 유지. 화면 `필드 1골드/h(§18-1)` ·
>   `생존 — 필드 사냥 1골드(§18-1)` · `획득 골드: 1골드`.
> - **TDD/실행**: `unity_meas` `HuntGoldSelfCheck` 전항 PASS
>   (`hunt_gold_selfcheck.log`). `HuntExp`·`SoftCap` 회귀 PASS. 정적 오류 0.
> - **화면**(직접 열음, 빈 화면 아님, `QA_HUNT_GOLD=1`):
>   `hunt_gold_shots/qa_go:Field.png` 1048895B — 필드,
>   `세계 T1 · 필드 1골드/h(§18-1)`.
>   `hunt_gold_shots/qa_go:Result.png` 749367B — 결과,
>   `생존 — 필드 사냥 1골드(§18-1)` · `획득 골드: 1골드` · `필드 사냥 1골드(§18-1)`.
>   옛 가죽·경험만 있던 필드 정산이 아님.
> - **네거티브**: `BattleScreen`에서 `WaveHuntGold` Earn을 빼면 FAIL 1
>   (`hunt_gold_negctrl.log` — 필드 생존이 WaveHuntGold를 Earn한다).
>   `QA_NO`면 골드 0.
> - **정직한 미완**: 잡몹 한 마리 3~10쿠퍼는 킬 카운트가 W3Party라 시간당으로 흡수.
>   샷 지갑 174골드는 이전 QA 잔재(시간당 문구는 보임).
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `d4afa59f`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 환생 — 레벨 1 · 경험 0.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §4·§3 ✅
>   `환생 시 상태 | 레벨 1부터 재육성` · `환생 캐릭터도 예외 없이 Lv.1부터 정상 육성`.
>   `UseRebornStone`은 삭제만 풀고 레벨을 유지했다. 소비처가 0곳이었다.
> - **생산 소비처**: `Rebirth.Apply`/`Line`/`DoneLine`/`MausoleumSubtitle`/`RowDesc`.
>   `LifeSystem.UseRebornStone`이 읽는다. 직업·1차·목숨 0·장비 비움·흡수 패시브 소멸은 그대로.
>   `QA_NO_REBORN_LV1=1`이면 레벨·경험 불변(환생 자체는 됨).
>   영묘 자막·줄·허브가 문구를 읽는다. `QA_REBORN_LV1=1`은 삭제 Lv50 영묘,
>   `=2`는 환생 직후 캐릭터 Lv1. `W3Party`는 안 건드렸다.
> - **통과 기준**: 수호기사 50·경험 12345 → Lv1·경험 0. 직업·1차 유지.
>   재기동 유지. 차단하면 50. 이미 1이면 1. 화면 `환생하면 Lv1부터 재육성(§4)` ·
>   `환생시험 · 수호기사 Lv50` · `지금 Lv50 → Lv1(§4)` · `Lv 1 · EXP 0/100`.
> - **TDD/실행**: `unity_meas` `RebirthSelfCheck` 전항 PASS
>   (`rebirth_selfcheck.log`). `Fusion`·`Equipment`·`SpecialJob` 회귀 PASS.
>   정적 오류 0.
> - **화면**(직접 열음, 빈 화면 아님, `QA_REBORN_LV1=1`/`=2`):
>   `rebirth_lv1_shots/qa_go:Estate.png` 490569B — 영지·영묘,
>   `환생하면 Lv1부터 재육성(§4) · 장비는 돌아오지 않는다`,
>   `환생시험 · 수호기사 Lv50` · `지금 Lv50 → Lv1(§4)`.
>   `rebirth_lv1_shots/qa_go:Character.png` 805136B — 캐릭터,
>   `환생시험` 선택 · `Lv 1 · EXP 0/100`. 옛 환생이 레벨을 유지하던 영묘가 아님.
> - **네거티브**: `UseRebornStone`에서 `Rebirth.Apply`를 빼면 FAIL 3
>   (`rebirth_negctrl.log` — 환생 후 Lv50). `QA_NO`면 레벨 유지.
> - **정직한 미완**: 환생 스킬 1개 선택은 안 넣음. 샷 명부의 탱커는 이전 QA 잔재.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `a5d33624`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 파산 — 건물 전체 −1레벨 · 비장착 30% 압류.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §18-5 ✅
>   `연체 3회 = 파산 | 영지 건물 전체 -1레벨, 비장착 아이템 30% 압류(경매 자동 처분→상환)`.
>   경매 7일·한도 −50%·재대출 7일은 `41c2ded8`. 건물·가방 소비처가 0곳이었다.
> - **생산 소비처**: `BankruptcySeize.Apply`/`TakeCount`/`SaleCopper`/`Line`/`KeepLine`/`ItemLine`.
>   `GameState.ApplyBankruptcy`가 읽는다. 본성 `DowngradeOne`(1 바닥)·방어 `DowngradeAll`(0 바닥).
>   비장착 장비·가방(증표 제외) `n*30/100`. 장착은 안 가져간다. 처분은 등록가
>   (가죽 2400·장비 12000) → `RepayFromIncome`, 남은 분만 Grant.
>   `QA_NO_BANKRUPT_SEIZE=1`이면 강등·압류 없음(파산 횟수·7일 정지는 유지).
>   영지 현황 본성·창고·허브 자막이 문구를 읽는다. `QA_BANKRUPT_SEIZE=1`은
>   인간+본성3+방어2+가죽10+흉갑10+연체3.
>   `W3Party`는 안 건드렸다.
> - **통과 기준**: 본성 3→2 · 방어 2→1. 본성 1·방어 0은 그대로. 연체 2회는 불변.
>   가죽 10→7 · 흉갑 10→7 · 처분 43200이 빚을 덮음. 장착 1장은 남음.
>   빚 1000이면 지갑에 잔액. 차단하면 건물·가방 불변. 재기동 유지.
>   화면 `건물 −1레벨 · 비장착 30% 압류 · 4골드 32실버 상환(§18-5)` ·
>   `Lv2 · 파산 강등 −1(§18-5)` · `비장착 30% 압류 · 4골드 32실버 상환(§18-5)`.
> - **TDD/실행**: `unity_meas` `BankruptcySeizeSelfCheck` 전항 PASS
>   (`bankruptcy_seize_selfcheck.log`). `LoanSanction` 회귀 PASS. 정적 오류 0.
> - **화면**(직접 열음, 빈 화면 아님, `QA_BANKRUPT_SEIZE=1`):
>   `bankrupt_seize_shots/qa_go:Estate.png` 915878B — 영지·현황,
>   허브 `건물 −1레벨 · 비장착 30% 압류 · 4골드 32실버 상환(§18-5)`,
>   본성 `Lv2 · 창고 24골드 · 파산 강등 −1(§18-5)`,
>   창고 `24골드 32실버 / 24골드 · 비장착 30% 압류 · 4골드 32실버 상환(§18-5)`.
>   옛 본성 Lv1·압류 없는 현황이 아님.
> - **네거티브**: `ApplyBankruptcy`에서 `BankruptcySeize.Apply`를 빼면 FAIL 17
>   (`bankruptcy_seize_negctrl.log` — 본성 3, 가죽 10, 빚 30000).
>   `QA_NO`면 건물·가방 불변.
> - **정직한 미완**: 건물 HP·안개·명예 상점·경로 전투는 없다. 증표는 압류 안 함.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `848acae5`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 연체 2회 영지 생산 100% 압류 — 광산이 빚으로 간다.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §12·§18-5 ✅
>   `연체 2회 | 영지 생산 100% 압류 + 침략 불가`. 침략 잠금은 `41c2ded8`.
>   `LoanAutoRepayRate` 50%는 모든 Earn. 광산 100% 소비처가 0곳이었다.
> - **생산 소비처**: `GameState.RepayFromIncome`/`SeedMineSeizeLoan`.
>   `EstateMine.Seized`/`SeizeLine`/`Tick`/`SeedSeizeQaIfRequested`.
>   연체 2회+빚이면 생산을 창고보다 먼저 빚에 넣는다. 창고 가득이어도 소멸 아님.
>   빚을 갚고 남은 분만 Earn. 사냥 `Earn`은 50% 유지. `QA_NO_MINE_SEIZE=1`이면 Earn 50%.
>   영지 현황 광산 카드가 `SeizeLine`을 읽는다. `QA_MINE_SEIZE=1`은 인간+T1+연체2+1시간.
>   `W3Party`는 안 건드렸다.
> - **통과 기준**: 연체1 광산 Earn 50% — 2500→지갑 1250·빚 −1250.
>   연체2 광산 2500 전액 빚, 지갑 0. 사냥 10000은 지갑 5000.
>   빚 1000만 남기면 나머지 1500은 지갑·연체 0. 창고 가득이어도 빚 −2500·소멸 0.
>   차단하면 Earn 50%. 재기동 유지. 화면 `25실버/h · 생산 압류 100%(§18-5)` · 창고 0쿠퍼.
> - **TDD/실행**: `unity_meas` `MineSeizeSelfCheck` 전항 PASS
>   (`mine_seize_selfcheck.log`). `EstateMine`·`EstateRaceMine`·`LoanSanction`·`SoftCap` 회귀 PASS.
>   정적 오류 0.
> - **화면**(직접 열음, 빈 화면 아님, `QA_MINE_SEIZE=1`):
>   `mine_seize_shots/qa_go:Estate.png` 908977B — 영지·현황,
>   광산 `25실버/h · 생산 압류 100%(§18-5)`, 창고 `0쿠퍼 / 12골드`.
>   옛 자동 적립만 쓰던 카드가 아님.
> - **네거티브**: `Tick`에서 `RepayFromIncome`을 빼면 FAIL 10
>   (`mine_seize_negctrl.log` — 지갑 1250, 빚 28750). `QA_NO`면 Earn 50%.
> - **정직한 미완**: 파산 건물 −1레벨·비장착 아이템 30% 압류는 안 넣음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `ca0329de`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 경매 등록 24시간 유찰 — 물건은 돌아오고 수수료는 소각.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §18-3 ✅
>   `등록 기간 | 24시간` · `등록 수수료 2% (유찰 시 미환급 = 소각)`.
>   `ListHours=24`·`Until`은 정의만. 내 등록을 만료하는 소비처가 0곳이었다.
>   NPC만 `RestockNpc`이 지웠고 플레이어 롯은 영구히 남았다.
> - **생산 소비처**: `AuctionState.SweepExpired`/`ExpireLine`/`MineLine`/`LotTimeLine`.
>   `Lots`/`MineCount`/`TryList*`/`RestockNpc`이 읽는다. 만료 1초 전 유지·+1초 유찰.
>   유찰은 `TryCancel`과 같다 — 물건 반환, 수수료 이미 소각. `QA_NO_AUCTION_EXPIRE=1`이면
>   내 등록은 안 지운다. 영지 경매장이 `MineLine`·남은 시간을 읽는다. 내 등록을 NPC보다 앞에.
>   `QA_AUCTION_EXPIRE=1`은 인간+30층+가죽 1건·구매 잠금 끔.
>   `W3Party`는 안 건드렸다.
> - **통과 기준**: 등록 직후 1/10·남은 24시간. 만료 1초 전 유지. +1초면 0/10·가죽 복귀·수수료 불변.
>   재기동 유지. 차단하면 유지. 화면 `내 등록 1/10 · 등록 24시간 · 유찰 시 수수료 소각(§18-3)`
>   · `취소 사냥 가죽` `내 등록 · 24실버 · 남은 23시간 58분`.
> - **TDD/실행**: `unity_meas` `AuctionExpireSelfCheck` 전항 PASS
>   (`auction_expire_selfcheck.log`). `AuctionBuyLock`·`AuctionFee`·`AuctionInvasion` 회귀 PASS.
>   정적 오류 0.
> - **화면**(직접 열음, 빈 화면 아님, `QA_AUCTION_EXPIRE=1`):
>   `auction_expire_shots/qa_go:Estate.png` 708944B — 영지·경매장,
>   `내 등록 1/10 · 등록 24시간 · 유찰 시 수수료 소각(§18-3)`,
>   `취소 사냥 가죽` · `내 등록 · 24실버 · 남은 23시간 58분`.
>   옛 내 등록이 만료되지 않던 장이 아님.
> - **네거티브**: `SweepExpired`에서 내 등록을 건너뛰면 FAIL 7
>   (`auction_expire_negctrl.log` — 24시간+1초에도 1/10). `QA_NO`면 유지.
> - **정직한 미완**: 다른 유저 서버 체결은 없다. 연체 2회 생산 압류·파산 건물 −1은 안 넣음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `8605408e`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 하위 레이드 재입장 누진 — 1×1 · 2×2 · 3×4 · 4+×8 · 24h 리셋.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §18-2 ✅
>   `하위 레이드 재입장(리롤) 1회차 ×1 → 2회차 ×2 → 3회차 ×4 → 4회차+ ×8 (24h 리셋)`.
>   `Economy.GetRerollCostMultiplier`는 정의만. `RaidReroll` 게임 코드 0곳이었다.
> - **생산 소비처**: `RaidReroll.Cost`/`Apply`/`Record`/`Line`/`FormatLine`.
>   `GetRerollCostMultiplier(이전 횟수)`를 읽는다. 하위 카드만 누진.
>   첫 클리어·첫 10층 카드는 1배. 드워프 80%는 `GetActionCost`가 먼저.
>   `TowerScreen` 입장 `Pay` 뒤에 `Record`. 자막·하위 카드가 `Line`을 읽는다.
>   `QA_NO_RAID_REROLL=1`이면 매번 1배. `QA_RAID_REROLL=1`은 11층·2회차(샷은 51층 잔재).
>   `W3Party`·`BossBattle`은 안 건드렸다.
> - **통과 기준**: 첫 5층 1000. 11층 1회차 1600·2회차 3200·3회차 6400·4회차 12800.
>   24h 정각은 2회차. +1초면 1600. 드워프 1280→2560. 51층 10485→20970.
>   차단하면 1배. 화면 `재입장 ×2(§18-2) · 2골드 9실버 70쿠퍼`.
> - **TDD/실행**: `unity_meas` `RaidRerollSelfCheck` 전항 PASS
>   (`raid_reroll_selfcheck.log`). 정적 오류 0.
> - **화면**(직접 열음, 빈 화면 아님, `QA_RAID_REROLL=1` + 스케일/풀 시드):
>   `raid_reroll_shots/qa_go:Tower.png` 1044234B — 탑 · 51층,
>   자막 `재입장 ×2(§18-2) · 2골드 9실버 70쿠퍼`,
>   카드 `하위 레이드 5층` 같은 문구. 옛 기준값만 쓰던 카드가 아님.
> - **네거티브**: `Cost`에서 `Apply`를 빼면 FAIL 12
>   (`raid_reroll_negctrl.log` — 2회차가 1600). `QA_NO`면 매번 1배.
> - **정직한 미완**: 현재 층 첫 클리어 탈출 후 재입장은 안 센다(하위 카드만).
>   샷의 51층은 `RaidScale`/`RaidBossPool` QA 시드가 먼저 깔린 잔재.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `a23b944a`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 하위 레이드 보스 풀 추첨 — 깬 레이드에서 무작위.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §9 ✅
>   `하위 레이드에는 그동안 잡았던 보스가 랜덤으로 생성`.
>   `보스 풀`/`RaidBossPool` 게임 코드 0곳이었다. 하위 카드는 5층 고정.
> - **생산 소비처**: `RaidBossPool.Pick`/`Name`/`Line`/`FightFloor`/`DropSourceFor`.
>   `GameFlow.GoBattle`이 보스전에서 뽑는다. 첫 클리어·던전은 입장 층.
>   골드·경험은 입장 층 `RaidScale`. 고유 드랍·페이즈는 출현 층.
>   탑 자막·하위 카드가 `Line`을 읽는다. 전투 제목·결과 줄이 이름을 읽는다.
>   `QA_NO_RAID_BOSS_POOL=1`이면 입장 층. `QA_RAID_BOSS_POOL=1`은 51층·심연의 눈.
>   `W3Party`·`BossBattle`은 안 건드렸다.
> - **통과 기준**: 첫 5·10층 고정. 11층 풀 2(5·10). 51층 풀 10(5…50).
>   시드 5→30 심연의 눈. 30층=대보스 테이블·증표 없음. 50층 출현은 증표.
>   골드 11523 유지. 차단하면 5층. 화면 `하위 레이드 보스 10종(§9)`.
> - **TDD/실행**: `unity_meas` `RaidBossPoolSelfCheck` 전항 PASS
>   (`raid_boss_pool_selfcheck.log`). 정적 오류 0.
> - **화면**(직접 열음, 빈 화면 아님, `QA_RAID_BOSS_POOL=1`):
>   `raid_boss_pool_shots/qa_go:Tower.png` 840240B — 탑 · 51층,
>   자막 `하위 레이드 보스 10종(§9)`,
>   카드 `하위 레이드 5층` 같은 문구. 옛 5층 고정만 쓰던 카드가 아님.
> - **네거티브**: `Pick`에서 풀을 비우면 FAIL 7
>   (`raid_boss_pool_negctrl.log` — 시드 5가 문지기 골렘). `QA_NO`면 입장 층.
> - **정직한 미완**: 진입 전 풀만 공개(💡)는 카드에 종수만. 변종 패턴 1개·
>   다중 3체는 안 넣음. `go:Battle`은 잡몹 웨이브라 출현 이름 샷은 없음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `3b0085cb`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 하위 레이드 스케일 0.65 — 원래+(선택−원래)×0.65.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §9·§18-10 ✅
>   `실난이도 = 원래 + (현재 티어 기준 − 원래) × 0.65, 보상도 동일 계수로 상승`.
>   `BalanceConfig.스케일링계수` 0.65는 정의만. `RaidScale` 게임 코드 0곳이었다.
> - **생산 소비처**: `RaidScale.ScalePercent`가 `스케일링계수`를 읽는다.
>   `Gold`/`Exp`/`TargetSeconds`가 이미 깬 탑 레이드에만 0.65를 곱한다.
>   `BattleScreen.CalculateVictoryReward`와 `Begin` 덮어쓰기가 읽는다.
>   탑 11층부터 `하위 레이드 5층` 카드. 자막·카드가 `FormatLine`을 읽는다.
>   `QA_NO_RAID_SCALE=1`이면 원래 층. `QA_RAID_SCALE=1`은 51층·선택 T5.
>   던전·첫 클리어는 층/10 레거시. `W3Party`·`BossBattle`은 안 건드렸다.
> - **통과 기준**: 첫 5층 2500·Applies 없음. 51층+T5 5층=11523·경험 460·226.5초.
>   50층+T5=16383. 선택 T1이면 50층=7360(최고 기록 강제 상승 없음).
>   차단하면 2500. 화면 `하위 레이드 스케일 0.65(§18-10) · 5층 T1→T5 · 1골드 15실버 23쿠퍼`.
> - **TDD/실행**: `unity_meas` `RaidScaleSelfCheck` 전항 PASS
>   (`raid_scale_selfcheck.log`). 정적 오류 0.
> - **화면**(직접 열음, 빈 화면 아님, `QA_RAID_SCALE=1`):
>   `raid_scale_shots/qa_go:Tower.png` 1036849B — 탑 · 51층,
>   자막 `하위 레이드 스케일 0.65(§18-10) · 5층 T1→T5 · 1골드 15실버 23쿠퍼`,
>   카드 `하위 레이드 5층` 같은 문구. 옛 잠긴 층 카드가 아님.
> - **네거티브**: `Gold`에서 `Blend`를 빼면 FAIL 8
>   (`raid_scale_negctrl.log` — 5층+T5가 2500). `QA_NO`면 원래 층.
> - **정직한 미완**: 보스 풀 랜덤 출현은 안 넣음(5층만). HP는 목표 시간 덮어쓰기라
>   BossBattle 공식은 그대로. 샷 자막의 `별에 닿은 자`는 이전 QA 잔재.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `ae87ce1d`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 5층 전 비살상 훈련 — 사망 대신 HP 1 귀환 · 5층 입장 직전 동의.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §온보딩 ✅
>   `첫 5층 레이드 전 튜토리얼 전투는 비살상 훈련으로 분류해 사망 대신 HP 1 귀환`.
>   `5층 입장 직전 영구 사망 규칙을 다시 보여주고 동의한 뒤부터 실제 PvE 사망에 카운트`.
>   `10층까지의 장기 면제는 두지 않는다`. `비살상 훈련`/`DeathConsent` 게임 코드 0곳이었다.
> - **생산 소비처**: `DeathTraining.IsTraining`/`NeedsConsent`/`Consent`/`ApplyReturn`/`Line`.
>   `GameFlow.ApplyPveDefeat`가 훈련이면 `ApplyWipe`를 안 탄다. `LifeSystem.ApplyWipe`는
>   살상 유지(V4). 탑 5층+ 입장이 동의 화면. 필드·탑 자막이 `Line`을 읽는다.
>   `QA_NO_DEATH_TRAINING=1`이면 처음부터 살상. `QA_DEATH_TRAINING=1`은 5층·미동의·동의 화면.
>   `W3Party`는 안 건드렸다.
> - **통과 기준**: 1층 패배 목숨 0·TrainingReturn. 4층 입장 OK·5층 거부. 동의 뒤 목숨 +1.
>   6층·10층은 동의 없어도 살상. 재기동 유지. `QA_NO`면 살상. PvP 목숨 0.
>   화면 `[주의] 5층부터는 영구 사망이 적용된다` · `비살상 훈련 — 5층 레이드 전 HP 1 귀환(§4)`.
> - **TDD/실행**: `unity_meas` `DeathTrainingSelfCheck` 전항 PASS
>   (`death_training_selfcheck.log`). `LifeSystemSelfCheck` 회귀 PASS — V4는 동의 뒤.
>   정적 오류 0.
> - **화면**(직접 열음, 빈 화면 아님, `QA_DEATH_TRAINING=1`):
>   `death_training_shots/qa_go:Tower.png` 430103B — 탑 · 5층,
>   자막 `비살상 훈련 — 5층 레이드 전 HP 1 귀환(§4)`,
>   `[주의] 5층부터는 영구 사망이 적용된다`,
>   `3번 죽으면 캐릭터와 장착 장비가 사라집니다(§4)`,
>   `동의하고 입장` / `아직 훈련`. 옛 레이드 카드만 있던 탑이 아님.
> - **네거티브**: `ApplyPveDefeat`에서 `IsTraining`을 `false &&`로 막으면 FAIL 6
>   (`death_training_negctrl.log` — 훈련 패배가 목숨 1·[사망]). `QA_NO`면 살상.
> - **정직한 미완**: 결과 샷은 이전 QA `별에 닿은 자` 에필로그가 PlayerPrefs에 남아
>   훈련 귀환 줄이 가려짐(동의 화면은 탑에서 확인). 스케일링계수 0.65는 안 넣음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `13763cfd`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 신규 계정 구매 잠금 — 경매장 해금 후 7일 판매만.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §18-3·§18-14 ✅
>   `30층 달성 후에도 첫 7일간 판매만 가능·구매 불가`.
>   `구매 잠금`/`BuyLock` 게임 코드 0곳. `TryBuy`는 거래 잠금만 보고 7일 시계가 없었다.
> - **생산 소비처**: `NoteUnlock`/`BuyLockLeft`/`CanBuy`/`BuyLockLine`/`WhyCannotBuy`/
>   `SeedBuyLockQaIfRequested`. `TryBuy`가 읽는다. `TryList*`는 연다.
>   `ClearFloor`가 30층에 처음 닿을 때만 시계를 찍는다. `SetTowerFloorForTest`는 안 찍는다.
>   `QA_NO_AUCTION_BUY_LOCK=1`이면 구매가 열린다. 영지 경매장 자막·구매 줄이
>   `BuyLockLine`을 읽는다. `QA_AUCTION_BUY_LOCK=1`은 인간+30층+해금 직후.
>   `W3Party`는 안 건드렸다.
> - **통과 기준**: 해금 직후 구매 거부·등록 허용. 7일−1초 거부. +1초 구매.
>   재기동 유지. 차단하면 구매. 화면 `신규 계정 구매 잠금 7일 0시간(§18-3) — 판매만 가능`.
> - **TDD/실행**: `unity_meas` `AuctionBuyLockSelfCheck` 전항 PASS
>   (`auction_buy_lock_selfcheck.log`). `AuctionInvasionSelfCheck`·`AuctionFeeSelfCheck`
>   회귀 PASS. 정적 오류 0.
> - **화면**(직접 열음, 빈 화면 아님, `QA_AUCTION_BUY_LOCK=1`):
>   `auction_buy_lock_shots/qa_go:Estate.png` 723633B — 영지·경매장,
>   `신규 계정 구매 잠금 7일 0시간(§18-3) — 판매만 가능`,
>   구매 줄 같은 문구, `등록 사냥 가죽 1`·수수료 33쿠퍼.
>   옛 구매가 열려 있던 장이 아님.
> - **네거티브**: `TryBuy`에서 `WhyCannotBuy`를 `WhyCannotTrade`로 되돌리면 FAIL 1
>   (`auction_buy_lock_negctrl.log` — 잠금 중 NPC 구매). `QA_NO`면 구매.
> - **정직한 미완**: 다른 유저 서버 체결은 없다. 5층 전 비살상·스케일링계수 0.65는 안 넣음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `391c055b`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 반복 침략 억제 — 동일 상대 24h 2회차 −80% · 3회차 0.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §18-13 ✅
>   `동일 상대 24시간 내 2회차 보상 -80%, 3회차부터 0`.
>   `반복 침략`/`2회차` 게임 코드 0곳. 보호막 12h가 먼저 막고, 풀린 뒤 직전 침략 24h 창만 본다.
> - **생산 소비처**: `NextAttempt`/`RepeatPercent`/`ApplyRepeatLoot`/`RecordStrike`/`RepeatLootLine`.
>   `LootCopper`가 바닥·상한 뒤에 읽는다(앞에 곱하면 2회차가 다시 바닥 5000).
>   `Honor.ApplyInvasion`이 같은 배율을 읽는다. `Settle`이 승패 모두 회차를 남긴다.
>   `QA_NO_REPEAT_LOOT=1`이면 매번 전액. 월드맵 자막·침략 카드가 `RepeatLootLine`을 읽는다.
>   `QA_REPEAT_LOOT=1`은 인간+30층+2회차·창고 25000·보호막 없음.
>   `W3Party`는 안 건드렸다.
> - **통과 기준**: 첫 승리 약탈 5000·명예 30. 12h+1초 두 번째 1000·명예 6. 세 번째 0.
>   24h+1초면 다시 1회차. 차단하면 매번 전액. 화면 `반복 침략 −80%(§18-13)` · `예상 10실버`.
> - **TDD/실행**: `unity_meas` `RepeatLootSelfCheck` 전항 PASS
>   (`repeat_loot_selfcheck.log`). `HonorSelfCheck`·`LootWarehouseSelfCheck`·
>   `AuctionInvasionSelfCheck` 회귀 PASS. 정적 오류 0.
> - **화면**(직접 열음, 빈 화면 아님, `QA_REPEAT_LOOT=1`):
>   `repeat_loot_shots/qa_go:WorldMap.png` 686723B — 월드맵,
>   `반복 침략 −80%(§18-13)`,
>   침략 카드 `반복 침략 −80%(§18-13) · 예상 10실버`.
>   옛 1회차 전액만 쓰던 카드가 아님.
> - **네거티브**: `Settle`에서 `RecordStrike`를 빼면 FAIL 16
>   (`repeat_loot_negctrl.log` — 두 번째 약탈 5000·명예 30). `QA_NO_REPEAT_LOOT=1`이면 매번 전액.
> - **정직한 미완**: 수비 성공 +20·방어력 비례 ±50%는 시뮬 없음.
>   매칭 ±5층·디버프 중첩 2별은 로컬 별 1개라 안 넣음. 샷 자막의 100층은 이전 QA 잔재.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `5c523fc1`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 명예 — 침략 승리 +30, 패배 0.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §18-13 ✅
>   `명예 | 승리 +30(상대 방어력 비례 ±50%) / 수비 성공 +20 / 패배 0`.
>   `명예` 게임 코드 0곳. 방어력 비례·수비 +20은 시뮬이 없어 고정 +30만.
> - **생산 소비처**: `Honor.ApplyInvasion`/`WinLine`/`BalanceLine`/`SeedQaIfRequested`.
>   `InvasionState.Settle`이 승패를 읽는다. 월드맵 자막·침략 카드가 `WinLine`을 읽는다.
>   침략 승리 요약이 `명예 +30(§18-13)`을 붙인다. 잔액은 PlayerPrefs.
>   `QA_NO_HONOR=1`이면 정산해도 0. `QA_HONOR=1`은 인간+30층·보호막 없음.
>   `W3Party`는 안 건드렸다.
> - **통과 기준**: 승리 +30, 패배 0, 재기동 유지. 차단하면 불변.
>   정산 승리 명예 30. 화면 `명예 +30(§18-13)`.
> - **TDD/실행**: `unity_meas` `HonorSelfCheck` 전항 PASS
>   (`honor_selfcheck.log`). `LootWarehouseSelfCheck`·`AuctionInvasionSelfCheck` 회귀 PASS.
>   정적 오류 0.
> - **화면**(직접 열음, 빈 화면 아님, `QA_HONOR=1`):
>   `honor_shots/qa_go:WorldMap.png` 876797B — 월드맵,
>   `명예 +30(§18-13)`,
>   침략 카드 `명예 +30(§18-13) · 진입 서 3칸 · 출정 7실버 99쿠퍼`.
>   옛 출정 비용만 쓰던 카드가 아님.
> - **네거티브**: `Settle`에서 `Honor.ApplyInvasion`을 빼면 FAIL 3
>   (`honor_negctrl.log` — 정산 승리 명예 0). `QA_NO_HONOR=1`이면 불변.
> - **정직한 미완**: 수비 성공 +20·방어력 비례 ±50%는 시뮬 없음.
>   반복 침략 24h −80%·명예 상점은 안 넣음. 샷 자막의 100층은 이전 QA 잔재.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `ee2d18f5`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 창고 20% 약탈 — LootCopper가 지갑의 20%를 읽는다.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §18-13 ✅
>   `약탈량(패자 손실) | 창고 자원 20% + 미수령 생산량 50%`. 미수령은 자동적립이라 안 넣음.
>   `LootCopper`는 출정×3만 써서 창고와 무관했다.
> - **생산 소비처**: `WarehouseCopper`/`ApplyWarehouseLoot`/`WarehouseLootLine`/`SetWarehouseCopper`.
>   `LootCopper`가 출정×3 대신 `창고×20%`를 읽는다. 출정 대기 중엔 낸 출정비를 다시 더한다.
>   `Settle`은 대기를 끄기 전에 약탈을 읽는다(끄면 출정비만큼 빠진다).
>   창고 0→바닥 5000. 창고 25000→5000. 창고 100000→20000.
>   `QA_NO_WAREHOUSE_LOOT=1`이면 옛 출정×3. 월드맵 자막·침략 카드가 `WarehouseLootLine`을 읽는다.
>   `QA_WAREHOUSE_LOOT=1`은 인간+30층+본성1+창고 25000·보호막 없음.
>   `W3Party`는 안 건드렸다.
> - **통과 기준**: 창고 25000→5000. 창고 100000→20000. 차단하면 창고와 무관.
>   정산 약탈 5000. 화면 `창고 20%(§18-13) · 50실버` · `예상 50실버`.
> - **TDD/실행**: `unity_meas` `LootWarehouseSelfCheck` 전항 PASS
>   (`warehouse_loot_selfcheck.log`). `LootFloor`·`LootCap`·`SoftCap`·
>   `AuctionInvasion`·`RaceLoot` 회귀 PASS. 정적 오류 0.
> - **화면**(직접 열음, 빈 화면 아님, `QA_WAREHOUSE_LOOT=1`):
>   `warehouse_loot_shots/qa_go:WorldMap.png` 785147B — 월드맵,
>   `창고 20%(§18-13) · 50실버`,
>   침략 카드 `창고 20%(§18-13) · 50실버 · 예상 50실버`.
>   옛 출정×3만 쓰던 카드가 아님.
> - **네거티브**: `LootCopper`에서 `WarehouseFormulaCopper`를 출정×3으로 되돌리면 FAIL 3
>   (`warehouse_loot_negctrl.log` — 창고 100000이 5000, 시드 9211).
>   `QA_NO_WAREHOUSE_LOOT=1`이면 창고와 무관.
> - **정직한 미완**: 미수령 50%는 자동적립이라 안 넣음. 샷 자막의 100층은 이전 QA 잔재.
>   명예·반복 침략 24h -80%는 안 넣음. 원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `407c9129`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 승자 최소 0.5 G/h — LootCopper가 본성×0.5 G/h 밑으로 안 내린다.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §18-13 ✅
>   `승자 최소 보상 | 상대 영지 레벨 × 0.5 G/h`. 로컬 별은 내 본성. Keep1=5000.
>   `LootCopper`는 출정×3만 써서 T1 자연값이 3600 근처였다.
> - **생산 소비처**: `FloorCopper`/`ApplyLootFloor`/`LootFloorLine`.
>   `LootCopper`가 상한 앞에 바닥을 읽는다. Keep1=5000·Keep2=10000.
>   공식 3000→5000. `QA_NO_LOOT_FLOOR=1`이면 3000. T3 공식 9211은 그대로.
>   월드맵 자막·침략 카드가 `LootFloorLine`을 읽는다.
>   `QA_LOOT_FLOOR=1`은 인간+30층+본성1+공식 3000·보호막 없음.
>   `W3Party`는 안 건드렸다.
> - **통과 기준**: 공식 3000→5000. 차단하면 3000. 정산 약탈 5000.
>   화면 `승자 최소 0.5 G/h(§18-13) · 50실버` · `예상 50실버`.
> - **TDD/실행**: `unity_meas` `LootFloorSelfCheck` 전항 PASS
>   (`loot_floor_selfcheck.log`). `LootCapSelfCheck` 회귀 PASS — T1 약탈 5000.
>   정적 오류 0.
> - **화면**(직접 열음, 빈 화면 아님, `QA_LOOT_FLOOR=1`):
>   `loot_floor_shots/qa_go:WorldMap.png` 880766B — 월드맵,
>   `승자 최소 0.5 G/h(§18-13) · 50실버`,
>   침략 카드 `승자 최소 0.5 G/h(§18-13) · 50실버 · 예상 50실버`.
>   옛 출정×3만 쓰던 카드가 아님.
> - **네거티브**: `LootCopper`에서 `ApplyLootFloor`를 빼면 FAIL 5
>   (`loot_floor_negctrl.log` — 공식 3000이 그대로). `QA_NO_LOOT_FLOOR=1`이면 안 올린다.
> - **정직한 미완**: 창고 20%+미수령 50%는 공식 교체라 안 넣음.
>   샷 자막의 100층은 이전 QA 잔재(해금만 올리고 층을 내리진 않음). 바닥 문구는 보임.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `600fcd0f`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 시간당 수익 소프트캡 — Earn이 150% 초과분을 −70%로 읽는다.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §18-14 ✅
>   `티어 기대 수익의 150% 초과분은 획득량 -70%`. `GameState.Earn`이 시간창 없이
>   전액을 넣어서 소비처 0곳이었다.
> - **생산 소비처**: `SoftCap.Preview`/`Apply`/`Line`/`HourLine`. `GameState.Earn`이
>   입금 전에 읽는다. 사냥 `BattleScreen` 골드·약탈 `LastLoot`·광산 `Tick`이 Earn을 탄다.
>   T1 기대 10000·문턱 15000. 20000→16500. `QA_NO_SOFT_CAP=1`이면 안 깎는다.
>   `Grant`는 환급·QA 시드·검사 준비금. 영지 현황 창고·자막이 `HourLine`을 읽는다.
>   `QA_SOFT_CAP=1`은 T1에서 20000을 넣어 16500이 남게 한다.
>   `W3Party`는 안 건드렸다.
> - **통과 기준**: T1 10000·15000 그대로, 20000→16500. 차단하면 20000.
>   정산 공식 20000이면 받은 금액 16500. 화면 `소프트캡 150%(§18-14)` · `이번 시간 1골드 65실버`.
> - **TDD/실행**: `unity_meas` `SoftCapSelfCheck` 전항 PASS
>   (`soft_cap_selfcheck.log`). `LootCapSelfCheck` 회귀 PASS. 정적 오류 0.
> - **화면**(직접 열음, 빈 화면 아님, `QA_SOFT_CAP=1`):
>   `soft_cap_shots/qa_go:Estate.png` 914245B — 영지·현황,
>   `시간당 수익 소프트캡 150%(§18-14) · 한도 1골드 50실버/h · 이번 시간 1골드 65실버`,
>   창고 같은 문구. 옛 전액 입금 창고가 아님.
> - **네거티브**: `Earn`에서 `SoftCap.Apply`를 빼면 FAIL 12
>   (`soft_cap_negctrl.log` — Earn 20000이 그대로). `QA_NO_SOFT_CAP=1`이면 안 깎는다.
> - **정직한 미완**: 필드 잡몹은 골드 Earn이 없어 보스·약탈·광산만 탄다.
>   오프라인 정산 감쇠는 일과표가 💡라 안 넣음. 승자 최소 0.5 G/h·창고 20%는 안 넣음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `85eff13a`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 사냥 시작 — 선택 후 스타트, 배치 후 스타트.**
> - INBOX 01:18이 큐보다 앞선다. 필드 `사냥 시작`이 `GoBattle`을 바로 불렀다.
>   오너: 캐릭터 선택 → 스타트 → 전장 → 배치 → 스타트 → 전투.
> - **생산 소비처**: `HuntStart.BeginPick`/`ConfirmPick`/`ConfirmStart`/`TryPlace`.
>   `FieldScreen`이 선택 화면+스타트. `BattleScreen`이 `CombatHeld`를 켜고
>   배치 오버레이+스타트. `W3Party.ReleaseCombat`가 몹을 뽑는다.
>   탑·던전·침략·`GAME_START=hunt` 직행은 Idle이라 바로 싸운다.
>   `QA_HUNT_START=1`은 필드 선택. `QA_HUNT_DEPLOY=1`은 전장 배치.
>   `QA_NO_HUNT_START=1`이면 예전처럼 바로 전투.
> - **통과 기준**: 빈 편성 거부. 1명 스타트 → Deploying·ShouldHold.
>   자리 (2.4,-1.1) 저장. 두 번째 스타트 → Fighting. 차단이면 Idle.
>   화면 `출전할 캐릭터를 고른 뒤 스타트` · `배치한 뒤 스타트` · 몹 0.
> - **TDD/실행**: `unity_meas` `HuntStartSelfCheck` 전항 PASS
>   (`hunt_start_selfcheck.log`). 정적 오류 0.
> - **화면**(직접 열음, 빈 화면 아님):
>   `hunt_start_shots/qa_go:Field.png` 878902B — 필드 선택,
>   `출전할 캐릭터를 고른 뒤 스타트`, 힐러·탱크 카드, 스타트/취소.
>   `hunt_start_shots/qa_hunt.png` 692390B — 전장 배치,
>   `배치한 뒤 스타트 — 전투가 시작된다`, 스타트 버튼, 몹 없음.
> - **네거티브**: `BeginPick`을 `Cancel`로 바꾸면 FAIL 1
>   (`hunt_start_negctrl.log`). `QA_NO_HUNT_START=1`이면 선택을 안 연다.
> - **정직한 미완**: 탑·던전·침략은 바로 전투. 파티 화면 「필드 출전」은
>   필드 허브만. 드래그 배치는 없음(클릭). 선택 카드 글자가 좁다.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `933daadf`. `W3Party`는 HOLD 후 보류·배치 훅만.

> **이전 이터 결과(코드/실행): 약탈 상한 6 G/h — LootCopper가 같은 티어 6시간치로 자른다.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §18-13 ✅
>   `약탈 상한 6 G/h (6시간치)`. `LootCopper`는 출정비용×3만 쓰고 상한이 없었다.
> - **생산 소비처**: `InvasionState.CapCopper`/`ApplyLootCap`/`LootCapLine`.
>   `LootCopper`가 종족·디버프 뒤에 `6 × TierRevenue × 10000`으로 자른다.
>   T1=60000 · T10=4123168. `QA_NO_LOOT_CAP=1`이면 안 자른다.
>   공식 T10 약탈 247387은 상한 아래. SelfCheck가 `ForceLootBeforeCap=상한+1`로
>   T10이 잘리는지 본다. 월드맵 자막·침략 카드가 `LootCapLine`을 읽는다.
>   `QA_LOOT_CAP=1`은 인간+100층+T10 선택·보호막 없음·적 디버프 끔.
>   `W3Party`는 안 건드렸다.
> - **통과 기준**: 같은 티어 약탈 ≤ `6 × TierRevenue × 10000`. T1=60000.
>   차단하면 상한+1이 그대로. 정산 약탈이 상한. 화면 `약탈 상한 6 G/h(§18-13) · 412골드`.
> - **TDD/실행**: `unity_meas` `LootCapSelfCheck` 전항 PASS
>   (`loot_cap_selfcheck.log`). T1 3595≤60000 / T10 공식 247387≤4123168.
>   상한+1 → 4123168. `QA_NO`면 4123169. `AuctionInvasionSelfCheck`·
>   `RaceLootSelfCheck`·`AuraDebuffSelfCheck` 회귀 PASS. 정적 141소스 오류 0.
> - **화면**(직접 열음, 빈 화면 아님, `QA_LOOT_CAP=1`):
>   `loot_cap_shots/qa_go:WorldMap.png` 506967B — 월드맵,
>   `내 별 100층 · 별 112px · 영공 16.0 · 약탈 상한 6 G/h(§18-13) · 412골드 31실버 68쿠퍼`,
>   침략 카드 `약탈 상한 6 G/h(§18-13) · 412골드 31실버 68쿠퍼 · 예상 23골드`.
>   옛 출정 비용만 쓰던 카드가 아님.
> - **네거티브**: `LootCopper`에서 `ApplyLootCap`을 빼면 FAIL 3
>   (`loot_cap_negctrl.log` — T10 4123169, 정산 4173168). `QA_NO_LOOT_CAP=1`이면 안 자른다.
> - **정직한 미완**: 출정×3 공식 T10은 상한 아래라 훅으로 자름을 본다.
>   창고 20%·승자 최소 0.5 G/h는 안 넣음. 샷 예상 금액은 이전 QA 방어 잔재로
>   SelfCheck 247387과 다를 수 있다(상한 문구는 보임).
>   시간당 수익 소프트캡은 `Earn`이 아직 안 읽음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `c7f737b9`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 드워프 골드 소모 80% — GetActionCost가 종족 배율을 읽는다.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §3·§18-9 ✅
>   `골드 소모 행위 비용 감소`. `RaceDef`에 칸이 없었고 `GetActionCost`가 티어 상수만 써서
>   소비처 0곳이었다.
> - **생산 소비처**: `Economy.RaceCostPercent`/`ApplyRaceCost`/`RaceCostLine`/`GetActionCostBase`.
>   `GetActionCost`가 기준값에 정수 80%를 곱한다. 드워프 80·나머지 100.
>   같은 티어 출정 인간 2047 → 드워프 1637. `QA_NO_RACE_COST=1`이면 드워프도 100.
>   약탈은 `GetActionCostBase`라 인간=드워프 9211. 던전·패배 추가도 80%.
>   월드맵 자막·침략 카드가 `RaceCostLine`을 읽는다.
>   `QA_RACE_COST=1`은 드워프+30층·보호막 없음. `W3Party`는 안 건드렸다.
> - **통과 기준**: 드워프 80·인간/엘프/수인 100. 같은 티어 출정이 인간×80%.
>   차단하면 100. 정산 약탈은 기준값. 화면 `드워프 골드 소모 −20%(§18-9)` · 출정 금액.
> - **TDD/실행**: `unity_meas` `RaceCostSelfCheck` 전항 PASS
>   (`race_cost_selfcheck.log`). 인간 2047 / 드워프 1637. `AuctionInvasionSelfCheck`·
>   `RaceLootSelfCheck`·`WorldStarSelfCheck` 회귀 PASS. 정적 오류 0.
> - **화면**(직접 열음, 빈 화면 아님, `QA_RACE_COST=1`):
>   `race_cost_shots/qa_go:WorldMap.png` 449000B — 월드맵,
>   `내 별 30층 · 별 61px · 영공 7.5 · 드워프 골드 소모 −20%(§18-9)`,
>   침략 카드 `드워프 골드 소모 −20%(§18-9) · 진입 서 3칸 · 출정 16실버 39쿠퍼`.
>   옛 출정 비용만 쓰던 카드가 아님.
> - **네거티브**: `GetActionCost`에서 `ApplyRaceCost`를 빼면 FAIL 2
>   (`race_cost_negctrl.log` — 켜짐 2047 = 인간 2047). `QA_NO_RACE_COST=1`이면 드워프도 100.
> - **정직한 미완**: 샷의 출정 금액은 이전 QA 티어 잔재로 SelfCheck 1637과 1~2쿠퍼 다를 수 있다
>   (배율 문구는 보임). 샷 자막에 이전 QA `적 디버프 −5%`가 PlayerPrefs에 남음.
>   탐험 범위 +30%는 안개 없음. 방어 내구는 건물 HP 없음.
>   약탈 상한 6 G/h는 `LootCopper`가 아직 안 읽음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `a3e956dc`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 영공 적 디버프 — 침략 약탈이 95%로 읽는다.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §14 ✅
>   `인식 범위 안에서 적에게는 디버프`. `WorldStar.EnemyMul`은 영지 카드 문구만이고
>   `LootCopper`/`TryBegin`이 안 봤다. 아군 버프는 광산이 이미 읽음.
> - **생산 소비처**: `WorldStar.EnemyPercent`/`ApplyEnemy`/`EnemyLine`.
>   `LootCopper`가 종족 약탈 뒤에 정수 95%를 곱한다. `TryBegin`도 `EnemyMul`을 읽는다.
>   꺼짐 9211 → 켜짐 8750. `QA_NO_AURA_DEBUFF=1`이면 켜져도 100.
>   월드맵 자막·침략 카드, 영지 영공 카드가 `EnemyLine`을 읽는다.
>   `QA_AURA_DEBUFF=1`은 인간+30층+디버프 켬·보호막 없음.
>   `W3Party`는 안 건드렸다.
> - **통과 기준**: 디버프 켜면 같은 티어 약탈 95%. 차단하면 100.
>   정산 약탈이 미리보기와 같다. 화면 `적 디버프 −5%(§14)` · 예상 금액.
> - **TDD/실행**: `unity_meas` `AuraDebuffSelfCheck` 전항 PASS
>   (`aura_debuff_selfcheck.log`). 꺼짐 9211 / 켜짐 8750. `WorldStarSelfCheck`·
>   `AuctionInvasionSelfCheck`·`RaceLootSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_AURA_DEBUFF=1`):
>   `aura_debuff_shots/qa_go:WorldMap.png` 551280B — 월드맵,
>   `내 별 30층 · 별 61px · 영공 7.5 · 적 디버프 −5%(§14)`,
>   침략 카드 `적 디버프 −5%(§14) · 예상 31실버 75쿠퍼`.
>   옛 「침략이 읽기 전엔 표시」가 아님.
> - **네거티브**: `LootCopper`에서 `ApplyEnemy`를 빼면 FAIL 1
>   (`aura_debuff_negctrl.log` — 켜짐 9211 = 꺼짐 9211).
>   `QA_NO_AURA_DEBUFF=1`이면 켜져도 100.
> - **정직한 미완**: 샷의 예상 금액은 이전 QA 방어 잔재로 SelfCheck 8750과 다를 수 있다
>   (배율 문구는 보임). 탐험 범위 +30%는 안개 없음. 방어 내구는 건물 HP 없음.
>   드워프 골드 소모 감소는 `GetActionCost`가 아직 안 읽음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `191a4074`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 엘프 별 인식 +20% — RaceDef.인식범위배율을 읽는다.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §14·§18-9 ✅
>   `별 인식 범위 +20%`. `WorldStar.Sense`가 층만 써서 소비처 0곳이었다.
> - **생산 소비처**: `WorldStar.RaceSensePercent`/`ApplyRaceSense`/`RaceSenseLine`.
>   `Sense`가 `SenseBase * percent / 100`. 엘프 120·나머지 100.
>   같은 층 인간 7.515 → 엘프 9.018. 라벨 `영공 7.5` / `영공 9.0`.
>   월드맵 자막·별 캡션, 영지 영공 카드가 `RaceSenseLine`을 읽는다.
>   계정 종족은 `RacePrefs`. `QA_RACE_SENSE=1`은 엘프+30층.
>   `QA_NO_RACE_SENSE=1`이면 엘프도 100. `W3Party`는 안 건드렸다.
> - **통과 기준**: 엘프 120·인간/드워프/수인 100. 같은 층 영공이 인간의 120%.
>   만료 없이 재기동 유지. 화면 `엘프 인식 +20%(§18-9)` · `영공 9.0`.
> - **TDD/실행**: `unity_meas` `RaceSenseSelfCheck` 전항 PASS
>   (`race_sense_selfcheck.log`). 인간 7.515 / 엘프 9.018. `WorldStarSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_RACE_SENSE=1`):
>   `race_sense_shots/qa_go:WorldMap.png` 552553B — 월드맵,
>   `내 별 30층 · 별 61px · 영공 9.0 · 엘프 인식 +20%(§18-9)`,
>   별 캡션 같은 문구. 옛 층만 쓰던 영공 7.5가 아님.
> - **네거티브**: `Sense`에서 `ApplyRaceSense`를 빼면 FAIL 5
>   (`race_sense_negctrl.log` — 인간 7.515 / 엘프 7.515). `QA_NO_RACE_SENSE=1`이면 엘프도 100.
> - **정직한 미완**: 탐험 범위 +30%는 안개 없음. 방어 내구는 건물 HP 없음.
>   `EnemyMul`은 여전히 침략이 안 읽음. 원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `fee0aed1`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 수인 약탈량 +20% — RaceDef.약탈량배율을 읽는다.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §3·§18-9 ✅
>   `약탈량 +20%`. `RaceDef`에 칸이 없었고 `LootCopper`가 기준값만 써서 소비처 0곳이었다.
> - **생산 소비처**: `InvasionState.RaceLootPercent`/`ApplyRaceLoot`/`RaceLootLine`.
>   `LootCopper`가 방어 감소 뒤에 정수 %를 곱한다. 수인 120·나머지 100.
>   같은 티어 인간 9211 → 수인 11053. `WorldMapScreen` 침략 카드가 `RaceLootLine`을 읽는다.
>   승리 요약도 수인만 한 줄을 붙인다. 계정 종족은 `RacePrefs`.
>   `QA_RACE_LOOT=1`은 수인+30층·보호막 없음. `QA_NO_RACE_LOOT=1`이면 수인도 100.
>   `W3Party`는 안 건드렸다.
> - **통과 기준**: 수인 120·인간/엘프/드워프 100. 같은 티어 약탈이 인간의 120%.
>   만료 없이 재기동 유지. 화면 `수인 약탈 +20%(§18-9) · 예상 40실버 11쿠퍼`.
> - **TDD/실행**: `unity_meas` `RaceLootSelfCheck` 전항 PASS
>   (`race_loot_selfcheck.log`). 인간 9211 / 수인 11053. `AuctionInvasionSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_RACE_LOOT=1`):
>   `race_loot_shots/qa_go:WorldMap.png` 446581B — 월드맵·침략,
>   `수인 약탈 +20%(§18-9) · 예상 40실버 11쿠퍼 · 진입 서 3칸`.
>   옛 출정 비용만 쓰던 카드가 아님.
> - **네거티브**: `LootCopper`에서 `ApplyRaceLoot`를 빼면 FAIL 1
>   (`race_loot_negctrl.log` — 인간 9211 / 수인 9211). `QA_NO_RACE_LOOT=1`이면 수인도 100.
> - **정직한 미완**: 엘프 별 인식·탐험 범위는 그대로 소비처 0. 방어 내구는 건물 HP 없음.
>   샷의 예상 금액은 이전 QA 방어/티어 잔재로 SelfCheck 11053과 다를 수 있다(배율 문구는 보임).
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `2587a1fe`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 인간 전직 재료 +15% — RaceDef.전직재료배율을 읽는다.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §3·§18-9 ✅
>   `전직 재료 +15%`. `RaceDef.전직재료배율`은 인간 1.15·나머지 1.00인데
>   `RollBattleDrops`가 테이블 상수만 써서 소비처 0곳이었다.
> - **생산 소비처**: `Economy.RaceAdvMatPercent`/`ApplyAdvMatRate`/`RaceAdvMatLine`.
>   `RollBattleDrops`가 전직 재료만 `ApplyDropRate` 뒤에 한 번 더 곱한다.
>   정수 % — 인간 던전 보스 0.35→0.4025. 가죽은 그대로 0.50.
>   이미 100%인 레이드 칸은 그대로 항상 나온다. 수인 드랍률과 겹치면
>   재료만 둘 다 40.25%(수인은 드랍률, 인간은 전직재료).
>   `ResultScreen`이 `RaceAdvMatLine`을 읽는다. 계정 종족은 `RacePrefs`.
>   `QA_RACE_ADV=1`은 인간+전직 재료 시드.
>   `QA_NO_RACE_ADV=1`이면 인간도 100. `W3Party`는 안 건드렸다.
> - **통과 기준**: 인간 115·엘프/드워프/수인 100. 재료 35%→40.25%.
>   같은 시드 2000회 재료가 인간이 엘프보다 많다. 만료 없이 재기동 유지.
>   화면 `생존 — 인간 전직 재료 +15%(§18-9)` · `인간 전직 재료 +15%(§18-9)`.
> - **TDD/실행**: `unity_meas` `RaceAdvMatSelfCheck` 전항 PASS
>   (`race_adv_selfcheck.log`). 인간 787 / 엘프 697(+12.9%).
> - **화면**(직접 열음, 빈 화면 아님, `QA_RACE_ADV=1`):
>   `race_adv_shots/qa_go:Result.png` 437989B — 결과,
>   `생존 — 인간 전직 재료 +15%(§18-9)`, 같은 문구 한 줄 더,
>   `획득: 전직 재료 — 1차 전직에 5개 필요`.
>   옛 테이블 상수만 쓰던 결과 화면이 아님.
> - **네거티브**: `RollBattleDrops`에서 `ApplyAdvMatRate`를 빼면 FAIL 2
>   (`race_adv_negctrl.log` — 인간 697 / 엘프 697). `QA_NO_RACE_ADV=1`이면 인간도 100.
> - **정직한 미완**: 수인 약탈량 +20%는 그대로 소비처 0. 방어배율·불굴·야성은 W3Party.
>   샷에 이전 QA `홀로 깬 자` 배너가 PlayerPrefs에 남아 재료 줄이 계속 버튼과 겹침
>   (배율 문구는 보임). 원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `7787b9b0`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 수인 드랍률 +15% — RaceDef.드랍률배율을 읽는다.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §3·§18-9 ✅
>   `수인 드랍률 +15%`. `RaceDef.드랍률배율`은 수인 1.15·나머지 1.00인데
>   `RollBattleDrops`가 테이블 상수만 써서 소비처 0곳이었다.
> - **생산 소비처**: `Economy.RaceDropPercent`/`ApplyDropRate`/`RaceDropLine`.
>   `RollBattleDrops`가 `rate * percent / 100`을 굴린다. 정수 % — 수인 가죽 0.50→0.575.
>   이미 100%인 칸은 그대로 항상 나온다. `ResultScreen`이 `RaceDropLine`을 읽는다.
>   계정 종족은 `RacePrefs`. `QA_RACE_DROP=1`은 수인+가죽 시드.
>   `QA_NO_RACE_DROP=1`이면 수인도 100. `W3Party`는 안 건드렸다.
>   필드 가죽 확정 1장은 확률이 아니라 안 넣음. 인간 전직재료배율은 다음.
> - **통과 기준**: 수인 115·인간/엘프/드워프 100. 가죽 50%→57.5%·증표 2%→2.3%.
>   같은 시드 2000회 가죽이 수인이 더 많다. 만료 없이 재기동 유지.
>   화면 `생존 — 수인 드랍 +15%(§18-9)` · `수인 드랍 +15%(§18-9)`.
> - **TDD/실행**: `unity_meas` `RaceDropSelfCheck` 전항 PASS
>   (`race_drop_selfcheck.log`). 인간 1003 / 수인 1151(+14.8%).
> - **화면**(직접 열음, 빈 화면 아님, `QA_RACE_DROP=1`):
>   `race_drop_shots/qa_go:Result.png` 591087B — 결과,
>   `생존 — 수인 드랍 +15%(§18-9)`, 같은 문구 한 줄 더, 가죽.
>   옛 테이블 상수만 쓰던 결과 화면이 아님.
> - **네거티브**: `RollBattleDrops`에서 `ApplyDropRate`를 빼면 FAIL 2
>   (`race_drop_negctrl.log` — 인간 1003 / 수인 1003). `QA_NO_RACE_DROP=1`이면 수인도 100.
> - **정직한 미완**: 전직재료배율은 그대로 소비처 0. 필드 확정 가죽은 그대로 1장.
>   샷에 이전 QA `홀로 깬 자` 배너가 PlayerPrefs에 남아 가죽이 계속 버튼과 겹침
>   (드랍 문구는 보임). 원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `e03b6fd5`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 드워프 영지 생산 +20% — RaceDef.영지생산배율을 읽는다.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §3·§18-9 ✅
>   `드워프 영지 생산 +20%` / `수인 영지 생산 −20%`. `RaceDef.영지생산배율`은
>   드워프 1.20·수인 0.80인데 `EstateMine`이 기준 25실버만 써서 소비처 0곳이었다.
> - **생산 소비처**: `EstateMine.RacePercent`/`ApplyRace`/`CopperPerHourEffective`.
>   정수 `copper * percent / 100` — 드워프 3000·수인 2000·인간/엘프 2500.
>   영공 아군 버프는 그 위에 곱한다(3000×1.05≈3149). `EstateScreen` 현황 광산 카드가
>   `RaceLine`을 읽는다. 계정 종족은 `RacePrefs`.
>   `QA_RACE_MINE=1`은 드워프+1시간. `QA_NO_RACE_MINE=1`이면 드워프도 100.
>   `W3Party`는 안 건드렸다. 엘프 기준 `WorldStarSelfCheck` 회귀 PASS.
> - **통과 기준**: 드워프 T1 3000·수인 2000·인간/엘프 2500. 1시간 적립 동일.
>   만료 없이 재기동 유지. 화면 `30실버/h · 드워프 생산 +20%(§18-9)`, 창고 30실버.
> - **TDD/실행**: `unity_meas` `EstateRaceMineSelfCheck` 전항 PASS
>   (`estate_race_mine_selfcheck.log`). `EstateMineSelfCheck`·`WorldStarSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_RACE_MINE=1`):
>   `estate_race_mine_shots/qa_go:Estate.png` 497437B — 영지·현황,
>   광산 `30실버/h · 드워프 생산 +20%(§18-9)`, 창고 `30실버 / 12골드`.
>   옛 `25실버/h`가 아님.
> - **네거티브**: `CopperPerHourEffective`에서 `ApplyRace`를 빼면 FAIL 8
>   (`estate_race_mine_negctrl.log` — 드워프 실제 2500). `QA_NO_RACE_MINE=1`이면 드워프도 100.
> - **정직한 미완**: 드랍률·전직재료는 그대로 소비처 0. 방어 건물 내구 +20%는 안 넣음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `c0773443`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 인간 경매 수수료 10%→7% — RaceDef.경매수수료를 읽는다.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §3·§18-9 ✅
>   `경매 수수료 10%→7%`. `RaceDef.경매수수료`는 인간 7·나머지 10인데
>   `AuctionState.ListFee`가 `0.02` 상수만 써서 소비처 0곳이었다.
> - **생산 소비처**: `AuctionState.FeePercent`/`ListFee`/`SaleFee`/`FeeLine`.
>   총수수료를 2:8로 나눠 등록 1.4%·체결 5.6%(인간) / 2%·8%(나머지).
>   정수는 `price * p * 2 / 1000` — 부동소수면 10000이 139가 된다.
>   `EstateScreen` 허브·장 안이 `FeeLine`을 읽는다. 계정 종족은 `RacePrefs`.
>   `QA_AUCTION_FEE=1`은 인간+30층+가죽. `QA_NO_AUCTION_FEE=1`이면 인간도 10%.
>   `W3Party`·`WorldStar`는 안 건드렸다. 엘프 기준 `AuctionInvasionSelfCheck` 회귀 PASS.
> - **통과 기준**: 인간 등록 140/10000·체결 560. 엘프 200·800. 만료 없이 재기동 유지.
>   화면 `인간 수수료 7% — 등록 1.4%·체결 5.6% 소각(§18-9)`. 가죽 등록 수수료 33쿠퍼.
> - **TDD/실행**: `unity_meas` `AuctionFeeSelfCheck` 전항 PASS (`auction_fee_selfcheck.log`).
>   `AuctionInvasionSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_AUCTION_FEE=1`):
>   `auction_fee_shots/qa_go:Estate.png` 539507B — 영지·경매장,
>   `인간 수수료 7% — 등록 1.4%·체결 5.6% 소각(§18-9)`,
>   `등록 사냥 가죽 1` 옆 `수수료 33쿠퍼`. 옛 `등록 2%·체결 8%`가 아님.
> - **네거티브**: `ListFee`를 `ListFeeRate` 상수로 되돌리면 FAIL 3
>   (`auction_fee_negctrl.log` — 인간 등록 200). `QA_NO_AUCTION_FEE=1`이면 인간도 10%.
> - **정직한 미완**: 체결 8%는 로컬 장이라 판매 정산이 없어 API·화면만. 등록만 지갑에서 빠진다.
>   영지생산·드랍률·전직재료는 그대로 소비처 0. 원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `7fa1cfae`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 인간 PvE 사망 회복 18시간 — RaceDef.회복시간을 읽는다.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §3·§18-8 ✅
>   `인간 회복 시간 단축(1일 → 18시간)`. `RaceDef.회복시간`은 인간 18·나머지 24인데
>   `RegisterDeath`가 `86400` 상수만 써서 소비처 0곳이었다.
> - **생산 소비처**: `LifeSystem.PveRecoverSeconds`. `RegisterDeath`(PvE)가 읽는다.
>   에셋이 없으면 인간 64800·나머지 86400으로 폴백. `QA_RACE_RECOVER=1`은 인간+사망1·수비 비움.
>   `QA_NO_RACE_RECOVER=1`이면 인간도 24시간. 캐릭터·파티가 `회복 18시간 0분 — 출전 불가(§4·§18-8)`.
>   PvP는 12시간 유지. `W3Party`·`EstateScreen`·`WorldStar`는 안 건드렸다.
> - **통과 기준**: 인간 PvE 64800초. 만료 1초 전 거부. +1초 출전. 엘프 86400.
>   인간 PvP는 43200(9시간 아님). 재기동 유지. 화면 `회복 18시간 0분 — 출전 불가(§4·§18-8)`.
> - **TDD/실행**: `unity_meas` `RaceRecoverSelfCheck` 전항 PASS (`race_recover_selfcheck.log`).
>   `LifeSystemSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_RACE_RECOVER=1`):
>   `race_recover_shots/qa_go:Character.png` 533285B — 힐러 선택,
>   `회복 18시간 0분 — 출전 불가(§4·§18-8)`, 하트 2칸+깨진 1칸, `회복 중`.
>   옛 수비대 `12시간`·§15 문구가 아님.
> - **네거티브**: `RegisterDeath`에서 `PveRecoverSeconds`를 `DefaultPveRecoverSeconds`로
>   되돌리면 FAIL 3 (`race_recover_negctrl.log` — 남은 초 86400·문구 24시간).
>   `QA_NO_RACE_RECOVER=1`이면 인간도 24시간.
> - **정직한 미완**: 인간 PvP 9시간은 보호막 12시간과 어긋나 안 넣음.
>   경매수수료·영지생산·드랍률·전직재료·불굴·야성 감각은 그대로 소비처 0.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `fd23be03`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 수비대 회복 12시간 — 보호막과 같은 시계로 출전 불가.**
> - 큐 1번은 사람 육안·다른 세션(EstateScreen 영공) 가능이라 대기하지 않음. §13-5·§15 ✅
>   `수비대 전멸해도 사망 카운트 없음, 회복 12시간` = 보호막 `GuardSeconds`.
>   `DefenseRecoverSeconds`는 상수만 있고 `RegisterDeath(isPvp)`가 즉시 return이라 소비처 0곳이었다.
> - **생산 소비처**: `LifeSystem.PvpRecoverSeconds`/`StartPvpRecovery`. `RegisterDeath(isPvp)`와
>   `ApplyWipe(isPvp)`가 12시간을 건다. `DefenseState.ApplyPvpRecover`가 수비 전원에 같은 시계.
>   `Prune`은 회복 중 수비를 안 뺀다(빼면 보호막이 끝날 때 수비가 비어 무방비).
>   `GameFlow.ApplyPveDefeat(isPvp)`·침략 패배 결과 요약이 소비. `QA_DEFENSE_RECOVER=1` 시드.
>   `QA_NO_DEFENSE_RECOVER=1`이면 정산해도 안 건다. `W3Party`·`EstateScreen`·`WorldStar`는 안 건드렸다.
> - **통과 기준**: 수비 전멸 직후 43200초. 만료 1초 전 출전 거부. +1초 출전 가능(수비는 유지).
>   목숨 0. 침략 패배 출전 전원 회복. 재기동 유지. 화면 `수비대 회복 12시간 0분 — 출전 불가(§15)`.
> - **TDD/실행**: `unity_meas` `DefenseRecoverSelfCheck` 전항 PASS (`defense_recover_selfcheck.log`).
>   `LifeSystemSelfCheck` 회귀 PASS — PvP는 목숨 0 + 12시간 회복.
> - **화면**(직접 열음, 빈 화면 아님, `QA_DEFENSE_RECOVER=1`):
>   `defense_recover_shots/qa_go:Character.png` 724129B — `수비대 회복 12시간 0분 — 출전 불가(§15)`,
>   하트 3칸(목숨 안 깎임), `수비 배치 중`. `qa_go:Party.png` 561357B — 힐러 같은 문구, 탱커만 편성.
> - **네거티브**: `RegisterDeath(isPvp)`에서 `StartPvpRecovery`를 빼면 FAIL 12
>   (`defense_recover_negctrl.log`). `QA_NO_DEFENSE_RECOVER=1`이면 회복 0·출전 가능.
> - **정직한 미완**: 상대가 내 별을 치는 수비 전투 시뮬은 없음(시드·침략 패배 출전이 산 소비처).
>   인간 회복 9h는 보호막 12h와 어긋나 안 넣음. 동일 상대 24h -80%는 없음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `1a84fdb2`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 침략 보호막 12시간 — 정산하면 같은 별을 12시간 못 친다.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §15 ✅ `침략당한 직후 보호막 12시간`
>   = 수비대 회복. `SceneStructureBuilder` 주석만 있고 소비처 0곳이었다.
>   반복 침략 -80%·수비대 12시간 출전 불가는 안 넣음(회복 시계 자체는 다음).
> - **생산 소비처**: `InvasionState.GuardSeconds`/`DefenseRecoverSeconds`(같은 상수).
>   `Settle`이 `ArmShield`. `TryBegin`/`GameFlow.TryGoInvasion`이 막는다.
>   `WorldMapScreen.InvasionHubLockReason`이 `보호막 12시간 0분`을 읽는다.
>   `QA_INVASION_SHIELD=1`은 30층+보호막. `QA_NO_INVASION_SHIELD=1`이면 정산해도 안 건다.
>   `W3Party`·`UiPages`는 안 건드렸다. 월드맵 별 크기(`51c08cab`)는 덮지 않음.
> - **통과 기준**: 정산 직후 43200초. 만료 1초 전 거부. +1초 재출정. 승·패 둘 다 건다.
>   재기동 유지. 화면 `잠김 — 보호막 12시간 0분 — 수비대 회복과 같은 12시간(§15)`.
> - **TDD/실행**: 정적 130소스 오류 0. `unity_meas` `InvasionShieldSelfCheck` 전항 PASS
>   (`invasion_shield_selfcheck.log`). `AuctionInvasionSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_INVASION_SHIELD=1`):
>   `invasion_shield_shots/qa_go:WorldMap.png` 564853B — 내 별 30층 61px,
>   침략 카드 `잠김 — 보호막 12시간 0분`. 옛 출정 비용·진입 면 카드가 아님.
> - **네거티브**: `Settle`에서 `ArmShield`를 빼면 FAIL 11 (`invasion_shield_negctrl.log`).
>   `QA_NO_INVASION_SHIELD=1`이면 정산 직후 재출정.
> - **정직한 미완**: 수비대 캐릭터 12시간 출전 불가·동일 상대 24h -80%는 없음.
>   다른 유저 서버·랭킹·동맹은 OUT. 원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `4f19fd03`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 시작 로스터 2명 — 고른 1명, 5분 뒤 두 번째.**
> - 큐 1번은 사람 육안·다른 세션 가능이라 대기하지 않음. §3 ✅ `시작 로스터는 2캐릭터`.
>   `BeginNewGame`이 나머지 기본직업을 같이 넣어 5인이었다(정의와 반대).
>   무료 영입 3회·추천 탱+딜은 💡라 안 넣음.
> - **생산 소비처**: `LifeSystem.BeginNewGame`은 고른 직업 1명만. `StarterSecond`가
>   플레이 5분(`UnlockSeconds=300`) 뒤 대기를 연다. `AddStarterCompanion`은 Lv10.
>   `GameScreen.Update`가 Tick. 캐릭터·영지가 5장 카드를 그린다. 같은 역할 허용.
>   `QA_STARTER_SECOND=1`은 힐 1명+대기. `=2`는 힐+탱 지급. `QA_NO_STARTER_SECOND=1`이면 거부.
>   `Initialize()` 저장 없는 폴백 5인은 그대로(테스터·V4). `W3Party`는 안 건드렸다.
> - **통과 기준**: 힐 선택=명부 1·출전 1. 299초 거부. 301초 대기. 탱 영입=2명 Lv10.
>   수호기사 거부. 세 번째 거부. 재기동 유지. 화면「두 번째 동료를 고른다」5장 · `파티 2/5`.
> - **TDD/실행**: `unity_meas` `StarterSecondSelfCheck` 전항 PASS (`starter_second_selfcheck.log`).
>   `StarterPickSelfCheck` 회귀 PASS — 고른 뒤 1명.
> - **화면**(직접 열음, 빈 화면 아님):
>   `starter_second_shots/qa_go:Character.png` 595989B — `두 번째 동료를 고른다 — 시작 로스터 2명(§3)`,
>   탱커·물리딜러·마법딜러 / 힐러·서포터. `qa_go:Estate.png` 560653B — 같은 5장.
>   `starter_second_claimed_shots/qa_go:Character.png` 716300B — 왼쪽 힐러+탱커, `파티 2/5`,
>   힐러 Lv10. 옛 시작 5인 명부가 아님.
> - **네거티브**: `QA_NO_STARTER_SECOND=1`이면 400초에도 1명. 5인 동반 주석을 되돌리면
>   SelfCheck FAIL. Tick/Pending 소비를 빼면 소스 단언 FAIL (`starter_second_negctrl.log`).
> - **정직한 미완**: 선택 카드는 글자만(타이틀 idle 전신은 첫 선택만). 무료 영입 3회·
>   5층 전 5인은 💡. 일과표와 시계는 별개. 원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `102a48ca`. `W3Party`는 안 건드렸다.

V4 외부 테스터 70% → 넘김. 사람 70% 계속·24h 재실행은 측정하지 않았다. 테스터 통과가 아니다.

> **이번 이터 결과(코드/실행): 격자 8×8 — 성벽이 길을 늘리고 침략자는 가장 짧은 4면으로 들어온다.**
> - 큐 1번. ✅ 확정은 진입 방향 4면(§13-3). 드래그·16×16·프리셋은 💡라 안 넣음.
>   침략 시뮬은 약탈 %만이라 경로 길이를 약탈 공식에 넣지 않았다.
> - **생산 소비처**: `EstateGrid` 8×8. 본성(2,3)·창고(3,3)·광산(5,3) 고정.
>   성벽만 길을 막는다. `PathLength`/`InvaderSide`/`InvaderPath`.
>   `InvasionState.TryBegin`이 출정 순간 최단 면을 `ApproachSide`에 기록.
>   `WorldMapScreen` 침략 카드가 `진입 서 3칸`을 읽는다.
>   `EstateScreen` 네 번째 탭「배치」. `QA_ESTATE_GRID=1`은 북 3칸 벽.
>   `QA_NO_GRID=1`이면 배치 거부. 마지막 면을 막는 벽은 거부(만능 배치 없음).
>   `W3Party`·`UiPages`는 안 건드렸다.
> - **통과 기준**: 열린 북/서=3칸·남/동=4칸, 동률은 북. 성벽 0이면 배치 거부.
>   북 3칸 벽이면 북>3·다른 면이 최단. 4면 봉쇄 거부. 재기동 유지.
>   출정 `ApproachSide`=미리보기. 화면 `침략 진입 서 3칸` · `북5 동4 남4 서3` ·
>   벽3·본·창·광이 서로 다른 칸.
> - **TDD/실행**: 정적 125소스 오류 0. 구현 전 RED 143건(클래스 부재).
>   `unity_meas` `EstateGridSelfCheck` 전항 PASS (`estate_grid_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님, `QA_ESTATE_GRID=1`):
>   `estate_grid_shots/qa_go:Estate.png` 672843B — 탭 건물/현황/방어/배치,
>   배치 선택, `침략 진입 서 3칸 · 북5칸 동4칸 남4칸 서3칸`,
>   `성벽 3/3`, 8×8에 벽3·본(금)·창(파랑)·광(갈), 서쪽 주황 길.
>   옛 영지 3탭(격자 없음)과 갈림.
> - **네거티브**: `EstateGrid.cs` 없으면 컴파일 RED 6 (`estate_grid_RED.log`).
>   `QA_NO_GRID=1`이면 칸 불변. 창고/본성은 거둘 수 없다. 화살탑은 길을 안 막는다.
> - **정직한 미완**: 드래그 앤 드롭·16×16·프리셋 3개·경로 전투(벽 파괴)는 없음.
>   약탈 %는 기존 방어 레벨만. 동시 슬롯 2는 없음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `fbaca355`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 건설 단축 — 골드 15%/h · 재료 2% · 상한 50%.**
> - 큐 1번 중 ✅ 확정인 단축만. 격자 8×8은 침략 경로 소비처가 없어 안 넣음.
> - **생산 소비처**: `EstateRush` 수학. `EstateBuild`/`EstateDefense`가 `TryRushGold`/`TryRushMaterial`.
>   `EstateScreen` 본성·방어 공사 중 버튼. 계열 재료 6종만. 부활초·환생석·두루마리·강화석 거부.
>   `QA_ESTATE_RUSH=1`은 본성 공사+골드+가죽 3장. `QA_NO_RUSH=1`이면 거부.
>   `W3Party`·`UiPages`는 안 건드렸다.
> - **통과 기준**: 본성 1→2 300초·단축 150초·골드 750쿠퍼. 가죽 1장=6초.
>   24h 바닥 12h. 화살탑 120초→60초. 화면 `골드 단축 · 7실버 50쿠퍼` · `사냥 가죽 1장 단축`.
> - **TDD/실행**: 정적 123소스 오류 0. 구현 전 RED 44건. `unity_meas`
>   `EstateRushSelfCheck` 전항 PASS (`estate_rush_selfcheck.log`).
>   `EstateBuildSelfCheck` 회귀 PASS.
> - **화면**(직접 열음, 빈 화면 아님, `QA_ESTATE_RUSH=1`):
>   `estate_rush_shots/qa_go:Estate.png` 457210B — 영지·본성, 공사 중 5분,
>   골드 단축 7실버 50쿠퍼(150초), 가죽 1장·3장. 옛 본성 화면(단축 버튼 없음)과 갈림.
> - **네거티브**: `EstateRush.cs` 없으면 컴파일 RED 37 (`estate_rush_RED.log`).
>   `QA_NO_RUSH=1`이면 남은 시간 불변. 부활초·강화석 미소모. 바닥에서 재단축 거부.
> - **정직한 미완**: 격자 8×8·경로 지연·동시 슬롯 2는 없음. 한 번에 바닥까지/1장만(부분 골드 슬라이더는 없음).
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `bbce9f9a`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 방어 건물 4종 — 20층 순차 · 본성 40% · 수비 없으면 효율 50%.**
> - 큐 1번. 본성·광산·창고 다음 구멍. §13-2 화살탑/마법탑/성벽/함정 + §13-5 수비 미배치 효율 감소.
>   격자 8×8·단축 50%는 STATUS가 다음이라고 적어 안 넣음.
> - **생산 소비처**: `EstateDefense.TryStart`/`Tick`/`ApplyToLoot`. `InvasionState.LootCopper`가
>   감소를 소비. `EstateScreen` 허브 세 번째 탭「방어」. `QA_ESTATE_DEFENSE=1`은 20층·화살탑 Lv1.
>   `QA_NO_DEFENSE=1`이면 감소 0. `W3Party`·`UiPages`는 안 건드렸다
>   (다른 세션이 `UiPages`/`GameScreen`을 잡고 있음).
> - **통과 기준**: 1→2=120초(본성 300의 40%). 19층 거부. 20층 화살탑→마법탑 순차.
>   119초 Lv0, 121초 수령 없이 Lv1. 수비 있으면 약탈 -10%(2레벨), 없으면 -5%.
>   화면 `수비 0명 · 효율 50% · 약탈 -2%`, 화살탑 Lv1 본성 상한, 마법탑 해금, 성벽/함정 순차 잠금.
> - **TDD/실행**: 정적 121소스 오류 0. 구현 전 RED 84건. `unity_meas`
>   `EstateDefenseSelfCheck` 전항 PASS (`estate_defense_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님, `QA_ESTATE_DEFENSE=1`):
>   `estate_defense_shots/qa_go:Estate.png` 692106B — 탭 건물/현황/방어, 방어 선택,
>   화살탑 Lv1 잠김(본성 상한), 마법탑 Lv0 `4골드 80실버 · 2분`, 성벽·함정 순차 잠금.
> - **네거티브**: `EstateDefense` 없으면 컴파일 RED 84. `QA_NO_DEFENSE=1`이면 약탈 불변.
>   수비 0이면 같은 건물의 감소가 절반. 성벽 없이 함정 거부.
> - **정직한 미완**: 격자 배치·경로 지연·단축 50%·동시 슬롯 2는 없음.
>   효율 50%·레벨당 5%는 기획이 숫자를 안 준 프로토타입 값. 건물 전투(단일딜/광역/차단)는
>   침략 전투 시뮬이 아니라 약탈 %로만 소비. 원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `948f83f6`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 광산 생산을 창고에 자동 적립.**
> - 큐 1번. 대화 세션이 본성 레벨(`cd469f0f`)을 닫은 다음 구멍. §13-2 광산·창고 + §18-12
>   필드 25%(T1=25실버/h)·본성×12G 용량·초과 소멸. 수령 버튼 없음.
> - **생산 소비처**: `EstateMine.Tick`/`CopperPerHour`/`SeedQaIfRequested`.
>   `EstateScreen` 현황 3·4번 카드. `GameState.Earn`으로 입금(대출 50% 상환 유지).
>   사냥 `Earn`은 한도에 안 걸림. `QA_ESTATE_MINE=1`은 지갑을 비우고 1시간분만.
>   `QA_NO_MINE=1`이면 0. `W3Party`·허브 카드 레이아웃은 안 건드렸다
>   (다른 세션이 `UiPages`를 잡고 있음).
> - **통과 기준**: T1 3600s=+2500쿠퍼. T2=4000. 한도 가득=적립0·소멸. 화면
>   `25실버/h` · `25실버 / 12골드`.
> - **TDD/실행**: 정적 119소스 오류 0. 구현 전 RED 36건. `unity_meas`
>   `EstateMineSelfCheck` 전항 PASS (`estate_mine_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님, `QA_ESTATE_MINE=1`):
>   `estate_mine_shots/qa_go:Estate.png` 636482B — 현황 탭, 광산 `25실버/h`,
>   창고 `25실버 / 12골드 · 넘치면 소멸`. 옛 골드/수비 Locked 카드가 아님.
> - **네거티브**: `EstateMine` 없으면 컴파일 RED 36. `QA_NO_MINE=1`이면 적립 0.
>   한도까지 채우면 추가 0. 시드를 빼면 기존 저장 골드가 한도를 가린다.
> - **정직한 미완**: 광산 자체 레벨업·동시 건설 2슬롯·방어 건물·격자·단축 50%는 없음.
>   약탈 20%는 기존 침략이 지갑을 보고, 미수령 생산 50%는 자동적립이라 별도 풀 없음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `6a4e99af`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 수직 슬라이스 시작 — 본성 레벨·건설 시간.**
> - 오너 「기획서 읽고 수직슬라이스 시작」. §22 범위: 티어1~3·영지7종·탑30·1차·제작.
>   1차·제작·탑100은 이미 있다. 첫 구멍은 본성+타이머(§13-2·§18-12).
> - **생산 소비처**: `EstateBuild` · `EstateScreen.Keep`. 현황 첫 카드. 끝나면 수령 없이 적용.
> - **통과 기준**: 1→2=300초, 3→4=5×1.6², 24h 상한. 골드 없으면 거부. 299초 Lv1, 301초 Lv2.
> - **TDD**: `EstateBuildSelfCheck` PASS. 코드 `cd469f0f`.
> - **정직한 미완**: 광산·창고 적립·방어 건물·격자·단축 50%는 다음.

> **이전 이터 결과(코드/실행): 하단 고정바 5칸을 가운데 도크로 줄였다.**
> - 큐 1번이자 INBOX 21:45. 옛 칸은 (1280−96−40)/5 ≈ 229×72(가로/세로 3.2).
>   AFK·세븐나이츠처럼 아이콘+짧은 라벨 타일을 가운데 모은다. 새 힉스필드 0.
> - **생산 소비처**: `UiPages.NavDock`/`NavIcon`/`NavReserve`. `GameScreen.BottomBar`가
>   타일 5칸+왼쪽 ESC를 그린다. 본문 아래 여백 100→80.
> - **통과 기준**: 타일 폭≤100·가로/세로≤1.45. 5칸 합폭 < 화면 55%, 첫 칸 x>240.
>   화면에서 영지·필드·탑·월드맵·캐릭터가 가운데 작은 칸이고, 옛 가로 알약이 아님.
> - **TDD/실행**: 정적 115소스 오류 0. `unity_meas` `UiAtlasSelfCheck` PASS
>   (`nav_dock_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님):
>   `nav_dock_shots/qa_go:Estate.png` 597981B — 하단 가운데 5칸, ESC 왼쪽.
>   `nav_dock_shots/qa_go:Character.png` 746006B — 같은 도크. 옛
>   `starter_pick5_shots/qa_go:Character.png`(가로 알약 5개)와 갈림.
> - **네거티브**: NavDock를 옛 5등분으로 되돌리면 단언 6건
>   (합폭·가로/세로 3.27×5) (`nav_dock_negctrl.log`).
> - **정직한 미완**: 허브 카드 여백, 명부 아래 가로 줄 3개, 16:46 「다른 게임만큼」은
>   닫지 않음. 원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `2dd637d6`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): UI 이미지 늘어남 금지 + 시작/영입 카드 5장 Packed.**
> - INBOX 21:50이 큐보다 앞선다. 시작 카드 3×2가 가로로 넓은 칸에 `portrait_frame`을 늘려
>   베이지 여백이 컸고, 6번째 칸이 비어 있었다. idle 포즈는 `bbd5d31b`에서 이미 닫힘.
> - **생산 소비처**: `UiAtlas.FitInside`/`DrawFit`. `UiPages.LookDest`/`PackedCards`/
>   `StarterPickCards`/`JobPickCards`. `DrawJobLook`은 스프라이트 비율 칸에만 프레임.
>   버튼 크롬은 `DrawSliced`. 아이콘은 `DrawFit`. 타이틀·결과·캐릭터 영입이 Packed 5장.
> - **통과 기준**: FitInside 비율=1.11, 가로 400칸에서 dest 폭 < 상자. 시작 카드 5장·
>   둘째 줄 가운데. 화면 3+2, 프레임이 캐릭터를 감싸고 가로 기둥 늘어남 없음.
> - **TDD/실행**: 정적 115소스 오류 0. `unity_meas` `StarterPickSelfCheck` 전항 PASS
>   (`starter_idle_selfcheck.log`). `UiAtlasSelfCheck` PASS (`ui_fit_atlas_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님, `QA_START_PICK=1`):
>   `ui_fit_shots/qa_go:Title.png` 350207B — 위 탱커·물리딜러·마법딜러, 아래 힐러·서포터
>   가운데. 옛 `starter_idle_shots/qa_go:Title.png`(가로로 늘린 창·빈 6번째)와 갈림.
> - **네거티브**: `FitInside`가 상자 그대로면 FAIL 3건 exit 1 (`starter_idle_negctrl.log`).
>   Title을 Grid 3×2로 되돌리면 SelfCheck FAIL. 라벨 88px를 되돌리면 FAIL.
> - **정직한 미완**: 하단바 5칸은 여전히 길다(그래픽만 9-slice). 허브 카드 여백·16:46
>   「다른 게임만큼」은 사람 육안. 코드는 보드가 `b9ab486e`로 먼저 쓸어 담음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `b9ab486e`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(문서/집계): V4 외부 테스터 70% → 넘김.**
> - 오너 「그부분 넘어가」. 사람 70%·24h 재실행을 기다리지 않는다.
> - **생산 소비처**: `board._v4_owner_skipped` / `v4_released`. STATUS `→ 넘김`만 100.
>   옛 보드 `skip`(보류)는 그대로 90. 테스터 통과 문구를 쓰지 않는다.
> - **통과 기준**: 프로토 100, V4b 노트에 「넘김」있고 「통과」없음.
> - **TDD**: `test_owner_skip_closes_proto_without_claiming_pass`. 옛 skip=90 유지.
> - **정직한 미완**: 외부 테스터가 계속하고 싶은지는 모른다. 수직 슬라이스는 아직 0.

> **이전 이터 결과(코드/실행): 시작 직업 카드는 idle 포즈.**
> - INBOX 21:52가 큐보다 앞선다. 타이틀 5종이 `DrawJobLook(..., true)`라 걷고 있었다.
>   `walk=false`는 초상(fire_mage 등)을 먼저 그려 idle 전신이 가려지는 길이었다.
> - **생산 소비처**: `UiPages.StarterLookWalks=false` · `JobLookFrame` · `IdleFrame`.
>   `TitleScreen.DrawStarterPick`이 그 상수를 소비. 스프라이트가 있으면 ScaleToFit,
>   없을 때만 초상. `QA_START_PICK=1` 선택 화면.
> - **통과 기준**: 시작 카드 프레임=`idle_00`. 화면 탱크=방패 앞·검 내림, 물리딜러=발 모음.
>   힌트「idle 포즈로 고른다」. 옛 걷기 샷과 갈림.
> - **TDD/실행**: 정적 115소스 오류 0. `unity_meas` `StarterPickSelfCheck` 전항 PASS
>   (`starter_idle_selfcheck.log`). 시작 카드 idle · walk=false=idle_00 · walk=true만 걷기.
> - **화면**(직접 열음, 빈 화면 아님, `QA_START_PICK=1`):
>   `starter_idle_shots/qa_go:Title.png` 531214B — 탱크 idle(방패 전면), 물리딜러 idle(발 모음),
>   마법딜러·힐러·서포터 idle. 옛 `starter_pick5_shots/qa_go:Title.png`(걷기)와 갈림.
> - **네거티브**: `StarterLookWalks=true`면 SelfCheck FAIL. DrawJobLook에 `true` 리터럴을
>   되돌리면 다시 걷는다 (`starter_idle_negctrl.log`).
> - **정직한 미완**: 카드 안 가로 여백·늘어남 금지는 INBOX 21:50·21:45라 안 닫음.
>   전투 걷기 프레임은 그대로. 원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `bbd5d31b`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 프로토 80% 정체 — 집계가 실측을 버렸다.**
> - INBOX 21:27. 관문 5개 중 100% 개수만 세서 4/5=80%로 고정. V4b는 세션 10삭제인데 `pct: 0`.
> - **생산 소비처**: `board.v4_gate_pct` / 프로토 평균. 키트·실행·삭제·계속경로는 숫자를 올리고
>   사람 70% 전에는 90 상한. 사람 판정만 100.
> - **통과 기준**: 실측 세션 있으면 프로토 > 80 그리고 ≤ 90. V4b=90. 사람 skip이면 100 아님.
> - **TDD**: `loop/test_v4_playtest.py` 3항 + `test_progress_charts` V4b=20(세션 없음).
> - **정직한 미완**: V4 70% 사람 응답·24h 재실행은 그대로. 구현 관문(V1~V4a)은 이미 100.
> - **코드** `43d12541`. 루프가 「80% 유지」로 시작 5종만 닫던 경로를 끊었다.

> **이전 이터 결과(코드/실행): 스킬 자동 사용 + 수동 사용. 자동 중에도 누르면 나간다.**
> - INBOX 21:47이 큐보다 앞선다. 기본 딜·버퍼 스킬은 ForceSkill만 있어 자동이 0이었다.
>   암묵 자동(도발 3체·화염폭풍 4체)은 토글과 무관하게 돌아 수동이 성립하지 않았다.
> - **생산 소비처**: `SkillUse.Resolve`/`Apply`/`SettleAuto`. `W3Party.ApplySkillUse`가
>   Tick마다 소비. 누르면 슬롯이 이긴다. 자동은 쿨0일 때 1↔2. 나간 뒤에만 기본 쿨 4초.
>   암묵 자동은 `SkillUse.IsAuto`로 막는다. HUD 옛「자동 · ×2」장식을 `스킬 자동`/`스킬 수동` 토글로 교체.
>   `QA_NO_SKILL_AUTO=1`이면 수동. `QA_SKILL_AUTO=1`이면 자동.
> - **통과 기준**: 기본=자동. 누른 슬롯이 자동 큐를 덮음. 수동은 안 누르면 0.
>   화면 자동=「스킬 자동」+도발/고양 콜아웃. 수동=「스킬 수동」+콜아웃 없음.
> - **TDD/실행**: 정적 115소스 오류 0. `unity_meas` `SkillUseSelfCheck` 전항 PASS
>   (`skill_use_selfcheck.log`). Tick 호출·HUD 라벨·IsAuto 게이트를 소스에서 단언.
> - **화면**(직접 열음, 빈 화면 아님):
>   `skill_use_shots/qa_hunt_auto.png` 947534B — 우상단 `스킬 자동`, `도발의 함성`·`고양`.
>   `skill_use_shots/qa_hunt_manual.png` 943488B — 우상단 `스킬 수동`, 스킬 이름 없음. 버튼은 그대로.
> - **네거티브**: `QA_NO_SKILL_AUTO=1`이면 Apply 빈손. 끔이 저장에서 유지.
>   Tick의 `ApplySkillUse(ref m.ForceSkill)`를 빼면 SelfCheck FAIL.
> - **정직한 미완**: 캐릭터별·스킬별 수동 전용(§3 💡 즉시/아껴/수동전용 3단)은 안 넣음.
>   파티 공용 토글 하나. 초필은 기존 E/버튼. `W3Party`는 HOLD 후 소비처만.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `89622247`.

> **이전 이터 결과(코드/실행): 시작 기본직업 5종 — 마법딜러를 가르고 전신으로 고른다.**
> - INBOX 21:38이 큐보다 앞선다. ARTIFACT_INDEX·`sprites/mage` 13프레임이 이미 있었다.
>   `higgsfield generate list` waiting 0. `.generating` 없음. 4종 실루엣은 안 다시 그림.
> - **생산 소비처**: `LifeSystem.BasicJobs`에 `마딜`. `LookDir("마딜")=="mage"`.
>   `PartyState` 전투 어댑트 마딜→마법사. 1차 선택 딜=검사·궁수, 마딜=마법사·소환사.
>   타이틀 3×2. 층/캐릭터 영입 격자도 3×2. 라벨 탱커·물리딜러·마법딜러·힐러·서포터.
>   `QA_START_PICK=1` 선택 화면. `QA_START_JOB=마딜` 0번=마딜.
> - **통과 기준**: 타이틀 5장이 서로 다른 전신. 마딜을 고르면 명부 0번「마법딜러 · 마딜」.
> - **TDD/실행**: 정적 113소스. `unity_meas` `StarterPickSelfCheck` 전항 PASS
>   (`starter_pick_selfcheck.log`). `LifeSystemSelfCheck` 5종·마딜 1차 2종 PASS.
> - **화면**(직접 열음, 빈 화면 아님):
>   `starter_pick5_shots/qa_go:Title.png` 523266B — 탱커·물리딜러(걷기)·마법딜러(지팡이)·힐러·서포터.
>   `qa_go:Character.png` 468766B — `마법딜러 · 마딜` · 파티 5/5.
> - **네거티브**: `LookPath("마딜")`이 `sprites/mage/mage_idle_00`이 아니면 FAIL.
>   LookDir에서 마딜을 빼면 dps로 폴백한다. 1차 이름(수호기사) 거부.
> - **정직한 미완**: 마딜 전용 2스킬은 없고 전투는 마법사 어댑트. 초상은 fire_mage(전신과 다른 그림).
>   시작 로스터는 고른 1+나머지 4(레이드 5인). 기획 ✅ 시작 2명은 아직.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `669a1534`. `W3Party`는 안 건드렸다.

> **이번 이터 결과(코드/실행): 시작 기본직업 4종 — 타이틀에서 전신·걷기로 고른다.**
> - INBOX 21:25·21:27이 큐보다 앞선다. 80% 정체의 실행 가능 구멍은 「타이틀이 직업을 안 고른다」였다.
>   ARTIFACT_INDEX·Resources에 4종×13프레임이 이미 있었다. `higgsfield generate list` waiting 0.
>   재생성을 걸면 크레딧만 나간다(ashes-art 재생성 금지). 없는 것은 시작 소비처.
> - **생산 소비처**: `StarterPick.Request`/`TryChoose`. `LifeSystem.BeginNewGame`/`HasSavedRoster`.
>   `UiPages.DrawJobLook`(idle·walk_00/01). `TitleScreen.DrawStarterPick` 2×2.
>   저장 없으면 「게임 시작」이 선택. 저장 있으면 이어하기. 1차 이름(수호기사) 거부.
>   0번은 고른 기본직업, 나머지 역할+여분 딜로 5인(프로토 레이드).
>   `QA_START_PICK=1` 선택 화면. `QA_START_JOB=힐` 0번=힐. `QA_NO_START_PICK=1` 거부.
> - **통과 기준**: 타이틀 4장이 서로 다른 전신. 힐을 고르면 명부 0번「힐러 · 힐」. 수호기사 거부.
> - **TDD/실행**: 정적 112소스 오류 0. `unity_meas` `StarterPickSelfCheck` 전항 PASS
>   (`starter_pick_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님):
>   `starter_pick_shots/qa_go:Title.png` 477808B — 탱크·딜러(걷기)·힐러·버퍼 2×2.
>   `qa_go:Character.png` 540095B — `힐러 · 힐` · 파티 5/5. 옛「탱크 · 수호기사」가 아님.
> - **네거티브**: `BeginNewGame`을 빼면 컴파일 RED 2건 (`starter_pick_RED.log`).
>   `TryChoose("수호기사")` 거부. `QA_NO_START_PICK=1`이면 딜 유지.
> - **정직한 미완**: 마법딜러 기본직업은 없다(오너 21:38 5종이 다음). 4종 그림은 안 다시 그림.
>   시작 로스터는 고른 1+동반 4(레이드 5인). 기획 ✅ 시작 2명은 아직.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `ac413891`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 캐릭터창 — 목록 왼쪽 · 대형 모습·장비 오른쪽.**
> - INBOX 20:39가 큐보다 앞선다. 대화 세션 스튜디오(`fe5b7204`)는 초상 둘레 6칸이었고,
>   목록은 3×2 카드 → 클릭하면 목록이 사라졌다. 옛 `char_sprite_shots`는 오른쪽이 비어 있었다.
> - **생산 소비처**: `UiPages.RosterSplit`/`LargeLook`/`LookDir`. `CharacterScreen.DrawRosterSplit`.
>   명부 탭에서 왼쪽 줄 목록(선택 유지) + 오른쪽 장비/속성. 전신 `sprites/<dir>/<dir>_idle_00`.
>   없으면 초상 폴백. 장비 6칸은 모습 둘레, 가방은 그 오른쪽. 전직 시험·합성·층 영입은 전체 화면.
>   `QA_CHAR_LOOK=1`이면 층 보상 대기를 지우고 0번 캐릭터에 제작 장비를 끼운다.
> - **통과 기준**: 목록 x < 모습 x, 목록 폭 < 모습 폭, 대형 모습 ≥160×200·목록 얼굴(56)보다 큼.
>   화면에서 왼쪽 명부 + 오른쪽 전신과 장비가 같이 보인다.
> - **TDD/실행**: 정적 109소스 오류 0. `unity_meas` `CharacterRosterSelfCheck` 전항 PASS
>   (`char_roster_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님, `QA_CHAR_LOOK=1`):
>   `char_look_shots/qa_go:Character.png` 679755B — 왼쪽 `탱크 · 수호기사` 선택,
>   오른쪽 기사 전신 + 투구·장갑·신발·장신구, 가방 패널, `파티 1/5`.
> - **네거티브**: `RosterSplit` 좌우를 뒤집으면 FAIL 4건·exit 1
>   (`char_roster_RED.log`).
> - **정직한 미완**: 속성 탭은 줄 목록. 전용 전신 연출(회전·3D)은 없음. 샷의 명부가 3명인 것은
>   이전 QA 저장 잔재. 오너 21:25 기본직업 전용 아트는 다음 큐. V4 70%는 자동으로 안 닫는다.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `d1de9f64`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 레이드 클리어는 기본직업 2종 + 특수 직업 캐릭터 1%.**
> - INBOX 21:06·21:07이 큐보다 앞선다. 직전 슬라이스(`5b157014`)는 레이드도 1종+증표 2장(20%)이었다.
> - **생산 소비처**: `FloorRecruit.PendingPicks`/`PendingSpecialPick`. `LifeSystem.AddSpecialRecruit`.
>   일반 층=1종. 레이드=2종을 고른 뒤 1%면 특수 역할 카드. `QA_RAID_SPECIAL=1`이면 확정.
>   특수 영입은 `영입특수힐러1`·`IsSpecialJob`·목숨 1. 부활초·환생석·합성 재료 거부.
>   이 경로는 증표를 안 주고 안 쓴다. 50층 드랍표 2%는 그대로. 직업명(사신 등)은 💡.
>   `QA_FLOOR_REWARD=1`은 5층 2종+특수 대기. `QA_NO_FLOOR_REWARD=1`이면 거부.
> - **통과 기준**: 1층=1종·둘째 거부. 5층+강제성공=기본2+특수1·증표 0.
>   10층 강제실패=기본2·특수 없음. 화면「2종을 고른다 (1/2)」「당첨」.
> - **TDD/실행**: 정적 109소스. `unity_meas` `FloorRecruitSelfCheck` 전항 PASS
>   (`floor_recruit_selfcheck.log`). `AddSpecialRecruit`를 빼면 컴파일 RED 3건
>   (`floor_recruit_RED.log`). `RaidSpecialChance==0.01`.
> - **화면**(직접 열음, 빈 화면 아님, `QA_FLOOR_REWARD=1`):
>   `floor_recruit_shots/qa_go:Result.png` 255194B — `5층 레이드 — 기본 직업 2종을 고른다 (1/2)`,
>   `당첨 — 2종을 고른 뒤 특수 직업 역할을 고른다`, 탱크·딜러·힐러·버퍼 2×2.
>   `qa_go:Character.png` 295315B — 같은 4장. 옛「1종」「증표 2장」이 아님.
> - **네거티브**: 일반 층 2번째 거부. 재도전 명부 불변. 강제 실패는 특수 없음·증표 0.
>   `QA_NO_FLOOR_REWARD=1`이면 대기 없음. 특수 영입은 환생석 미소모.
> - **정직한 미완**: 특수 직업명·던전 특화는 💡라 없음. 같은 역할을 두 번 고를 수 있다.
>   파티 전용 헤더 없음. V4 70%는 자동으로 안 닫는다.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `a9850be4`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 층 클리어마다 기본직업 1종 선택 · 레이드 증표 2장 확률.**
> - INBOX 20:47이 큐보다 앞선다. 큐 1번은 사람 육안, 2번은 오너 보류라 대기하지 않음.
>   `홀로 깬 자` 다음으로 층 클리어 영입 소비처가 0곳이었다.
> - **생산 소비처**: `FloorRecruit.OnCleared`/`TryClaim`. `LifeSystem.AddBasicRecruit`.
>   탑 층 1~100 최초 클리어만. 플레이어가 탱·딜·힐·버퍼 중 1종을 고르면 Lv1 기본직업이 명부에 들어온다.
>   레이드(5·10·…·100)는 같은 클리어에서 증표 2장을 20%로 더 준다(`QA_RAID_SPECIAL=1`이면 확정).
>   직업명(사신 등)은 💡라 증표만. 던전·필드·재도전은 안 준다. `GameFlow.ApplyTowerBossVictory`와
>   탑 잡몹 돌파가 같은 경계를 부른다. 결과·캐릭터 화면이 2×2 선택 카드.
>   `QA_FLOOR_REWARD=1` 시드. `QA_NO_FLOOR_REWARD=1`이면 거부.
> - **통과 기준**: 1층→대기→탱 영입 명부+1·Lv1 Basic. 재도전 거부. 수호기사 거부.
>   5층+강제성공=증표+2. 10층 강제실패=증표 0·이전 배너 유지. 화면 4종 카드.
> - **TDD/실행**: 정적 109소스. `unity_meas` `FloorRecruitSelfCheck` 전항 PASS
>   (`floor_recruit_selfcheck.log`). `AddBasicRecruit`를 빼면 컴파일 RED 3건
>   (`floor_recruit_RED.log`).
> - **화면**(직접 열음, 빈 화면 아님, `QA_FLOOR_REWARD=1`):
>   `floor_recruit_shots/qa_go:Result.png` 448855B — `1층 돌파 — 기본 직업 1종을 고른다`,
>   `특수 직업 증표 2장`, 탱크·딜러·힐러·버퍼 2×2.
>   `qa_go:Character.png` 532915B — 명부 대신 같은 4장.
> - **네거티브**: 0·101층 거부. 재도전 명부 불변. `QA_NO_FLOOR_REWARD=1`이면 대기 없음.
>   기본직업은 합성 재료 아님. 골드·기존 레벨 불변.
> - **정직한 미완**: 특수 직업명·던전 특화는 💡라 없음. 레이드 확률 20%는 오너가 숫자를
>   안 줘서 프로토타입 값. 50층 드랍표 2%는 그대로(이 경로는 별도).
>   파티 전용 헤더 없음. V4 70%는 자동으로 안 닫는다.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `5b157014`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): 테스터 레벨 정체 — 필드·탑·던전 잡몹 생존 경험치.**
> - INBOX 20:36 「테스터들의 레벨이 오르지 않는다」가 큐보다 앞선다.
>   `AwardBattleExp`는 보스 승리(`CalculateVictoryReward`)에만 있었다.
>   필드 생존은 가죽 1장만, 탑 일반층·던전 노드는 요약만. 화면은 `EXP +4`를 띄우지만
>   저장되는 경험치는 0. 시작 로스터는 Lv10이라 보스 100XP로는 한 칸도 안 찬다.
> - **생산 소비처**: `Economy.WaveHuntExp` · `LifeSystem.AwardWaveHunt`.
>   T1 초당 58(§18-6 솔로 Lv20≈2h). 선택 월드 티어 곱. 인간 `경험치배율` +15%(정수 %).
>   `BattleScreen`이 필드 생존·탑 일반층·던전 노드에서 호출. 전멸·저체력 귀환은 안 줌.
>   `QA_NO_HUNT_EXP=1`이면 0. `QA_HUNT_EXP=1`이면 솔로 274초 → Lv10→11.
> - **통과 기준**: T1 100초=5800. 솔로 한 판에 한 칸. 5인 240초 총합 13920·레벨은 그대로.
>   인간 6670 > 엘프 5800. 화면 캐릭터 `Lv 11 · EXP 2427/19546`.
> - **TDD/실행**: 정적 107소스 오류 0. `unity_meas` `HuntExpSelfCheck` 전항 PASS
>   (`hunt_exp_selfcheck.log`). 심볼을 빼면 컴파일 RED 27건 (`hunt_exp_RED.log`).
> - **화면**(직접 열음, 빈 화면 아님, `QA_HUNT_EXP=1`):
>   `hunt_exp_shots/qa_go:Character.png` 643763B — `탱크 · 수호기사` `Lv 11` `EXP 2427/19546`.
>   `qa_go:Result.png`는 이전 QA_TOWER_END 에필로그가 PlayerPrefs에 남아 경험치 줄이
>   가려짐(정직). 레벨 증거는 캐릭터 화면.
> - **네거티브**: 0초=0. `QA_NO_HUNT_EXP=1`이면 Lv10·Exp0. 5인 240초는 한 칸이 아님.
> - **정직한 미완**: 보스 승리 XP는 예전 `tierRevenue×100`(T1=100) 그대로 — 필드 한 판보다
>   작다. 처치 수 연동은 W3Party라 안 건드렸다. 파티 인원 ×3 총량(💡)은 없음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
>   `CharacterScreen` 시드 훅은 대화 세션 장비 스튜디오와 겹쳐 이 커밋에 안 넣는다.
> - **코드** `e3ed5030`. `W3Party`는 안 건드렸다.

> **같은 시각 대화 세션: 캐릭터 상세 장비 스튜디오 — 참고작형 초상 둘레 6칸+가방.**
> - 오너가 모바일 RPG 장비 화면(큰 캐릭터·링 슬롯·가방 격자·전투력·자동장착)을 참고로 줌.
>   기존 상세는 장착 6칸을 오른쪽 한 줄(`DrawWornStrip`)로만 그렸다.
> - **생산 소비처**: `CharacterScreen.DrawEquipStudio`. 상세 탭 장비/속성.
>   왼쪽 초상 180×216 + `UiPages.SlotOnRing` 6칸(투구·장신구·무기·신발·갑옷·장갑).
>   빈 칸은 흐린 부위 실루엣. 장착 클릭=해제+가방 필터. 가방 클릭=장착.
>   오른쪽 4×4 가방 + 부위 탭. 아래 자동장착/골드·강화석/목록.
>   전투력 = `max(1, Lv×120×HpMul×Fusion.HpMul)`. `DrawWornStrip` 제거.
> - **통과 기준**: 탭「장비」가 기본. 초상 위에 투구 칸(`EquipRingDegrees[0]=-90`).
>   자동장착은 빈 칸만 가방 첫 해당 부위를 끼운다. 삭제된 캐릭터는 장착/해제 불가.
> - **TDD/실행**: 정적 컴파일 오류 0. `unity_meas` `UiAtlasSelfCheck` PASS
>   (`equip_studio_selfcheck.log`) — 링 6칸·12시 칸이 초상 위.
> - **화면**: 오너 Unity PID 75776은 안 죽였다. Play를 다시 켠 뒤
>   캐릭터 → 명부 → 카드 → 장비.
> - **정직한 미완**: 3D 전신·장식 프레임·100칸 가방·날개/보석 칸은 IMGUI+기존 초상이라 없음.
>   속성 탭은 전직/부활 줄 목록. V4 70%는 자동으로 안 닫는다.
>   사냥 경험치(`HuntExp*`)는 루프 #20 작업이라 이 커밋에 안 넣었다.
> - **코드** `fe5b7204`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): §8 1인 레이드 최초 클리어 — 칭호·홀로 선 별.**
> - 큐 1번은 사람 육안, 2번은 오너 보류. INBOX 20:04 「프로토 속도」·20:15 「막힌부분」+「대기하지 마라」로
>   기획서 ✅·소비처 0곳을 찾음. 직전 이터가 「1인 최초 클리어는 안 넣음」이라고 남긴 구멍.
>   `홀로 깬 자`/`SoloRaidClear` grep 0곳.
> - **생산 소비처**: `SoloRaidClear.TryGrant`/`AckBanner`. `GameFlow.ApplyTowerBossVictory`가
>   던전이 아닐 때 출전 인원과 층을 넘긴다. 레이드는 5·10·…·100층만. 출전 1명만.
>   같은 보스는 재지급 없음. 다른 보스는 따로. 골드/레벨/부활초/전투력 불변.
>   결과 배너·캐릭터 상세「홀로 선 별」·탑/영지/타이틀 자막(100층 칭호가 없을 때).
>   `QA_SOLO_CLEAR=1` 시드. `QA_NO_SOLO_CLEAR=1`이면 지급 거부.
>   `PartyState.SetSlotsForTest` — AutoFill이 빈 파티를 5명으로 채우는 길을 우회.
> - **통과 기준**: 1인 5층 → 칭호 `5층을 홀로 깬 자`·외형·배너. 5인/6층/0명 거부.
>   같은 보스 재도전 재지급 없음. 10층은 따로. 화면「5층을 홀로 깬 자」「홀로 선 별」.
> - **TDD/실행**: 정적 106소스 오류 0. `unity_meas` `SoloRaidClearSelfCheck` 전항 PASS
>   (`solo_raid_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님, `QA_SOLO_CLEAR=1`):
>   `solo_raid_shots/qa_go:Result.png` 301481B — `5층을 홀로 깬 자 — 1인 최초 클리어(§8) · 홀로 선 별`.
>   `qa_go:Character.png` 325313B — 상세 `별 외형 · 홀로 선 별`.
>   탑/영지/타이틀은 이전 QA_TOWER_END 칭호가 PlayerPrefs에 남아 100층 자막이 앞선다
>   (표시 우선순위는 의도. 1인 줄은 결과·캐릭터에서 확인).
> - **네거티브**: 5인·6층·0명·2인 15층 거부. 같은 보스 재지급 없음.
>   `QA_NO_SOLO_CLEAR=1`이면 칭호 없음. `TryGrant`/`AckBanner`/`SeedQaIfRequested`를
>   빼면 컴파일 RED 22건 (`solo_raid_RED.log`).
> - **정직한 미완**: 희귀 장비는 💡라 없음. 외형은 문구이지 새 스프라이트가 아님.
>   파티 전용 헤더 없음. V4 70%는 자동으로 안 닫는다.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `7ad6bad9`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): §8 100층 최초 클리어 결말 — 칭호·별 외형·에필로그.**
> - 큐 1번은 사람 육안, 2번은 오너 보류. INBOX 20:04 「프로토 속도」·20:15 「막힌부분」+「대기하지 마라」로
>   기획서 ✅·소비처 0곳을 찾음. `별에 닿은 자` grep 0곳. `ClearFloor`는 100층 격파를 101로 올렸다.
> - **생산 소비처**: `TowerEnding.TryGrant`/`SkipEpilogue`. `GameFlow.ApplyTowerBossVictory`가
>   던전이 아닐 때만 연다. `GameState.ClearFloor`는 100에서 멈춘다.
>   결과=에필로그+건너뛰기. 타이틀·영지 자막·탑 자막·캐릭터 상세「별 외형」.
>   `QA_TOWER_END=1` 시드. `QA_NO_TOWER_END=1`이면 지급 거부.
> - **통과 기준**: 100층 격파 뒤 층=100·칭호·별 외형·에필로그. 골드/레벨/부활초 불변.
>   99층은 칭호 없음. 재도전은 재지급 없음. 화면「별에 닿은 자」.
> - **TDD/실행**: 정적 104소스 오류 0. `unity_meas` `TowerEndingSelfCheck` 전항 PASS
>   (`tower_ending_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님, `QA_TOWER_END=1`):
>   `tower_ending_shots/qa_go:Result.png` 532968B — `별에 닿은 자 — 100층 최초 클리어`, 건너뛰기.
>   `qa_go:Tower.png` 528180B — `탑 · 100층` · `별에 닿은 자 · 100층 재도전`.
>   `qa_go:Title.png` — 타이틀에 칭호. `qa_go:Character.png` 648825B — `별 외형`.
>   `qa_go:Estate.png` — 자막 `별에 닿은 자`.
> - **네거티브**: 99층 거부. 두 번째 100층은 에필로그 재오픈 없음.
>   `QA_NO_TOWER_END=1`이면 칭호 없음. `TryGrant`/`SkipEpilogue`/`SeedQaIfRequested`를
>   빼면 컴파일 RED 16건 (`tower_ending_RED.log`).
> - **정직한 미완**: 에필로그는 짧은 텍스트(영상 없음). 별 외형은 문구이지 새 스프라이트가 아님.
>   1인 최초 클리어 특별 보상(§8)은 안 넣음. 파티 전용 헤더 없음. V4 70%는 자동으로 안 닫는다.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `b250ac0c`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): §4·§6 저체력 자동 귀환 — 필드 자동화 일정의 첫 슬라이스.**
> - 큐 1번은 사람 육안, 2번은 오너 보류. INBOX 20:04 「프로토 속도」+「대기하지 마라」로
>   기획서 ✅·소비처 0곳을 찾음. `FieldScreen` 「자동화 일정」은 Locked였고,
>   전투 스타일 문구「HP 30%에 후퇴」만 있었다(개인 자리 이동이지 영지 귀환이 아님).
> - **생산 소비처**: `LowHpReturn.Tick`/`ShouldWatch`/`Enabled`. 기본 켜짐.
>   필드 잡몹만 본다. 출전 최저 HP≤30%면 3초 이탈(피격해도 취소 아님) 후 영지.
>   사망 카운트 0, 이번 판 보상 없음. 보스·던전·탑·침략은 안 본다.
>   `W3Party.ActivePartyLowestHpRatio`는 읽기만. `FieldScreen` 토글이 Locked를 대체.
>   `QA_NO_LOW_HP_RETURN=1`이면 꺼짐.
> - **통과 기준**: 기본 ON. 30%에서 3초 뒤 Left. 31%는 Idle. 비필드는 1%여도 NotField.
>   화면「저체력 귀환 켜짐」+하트. 옛 Locked「스케줄러 미구현」이 아님.
> - **TDD/실행**: 정적 102소스 오류 0. `unity_meas` `LowHpReturnSelfCheck` 전항 PASS
>   (`low_hp_return_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님):
>   `low_hp_return_shots/qa_go:Field.png` 421774B — `저체력 귀환 켜짐`,
>   `HP 30%면 3초 뒤 영지. 이번 판 보상 없음(§4·§6)`, 빨간 하트.
> - **네거티브**: 끄면 1%여도 Disabled. `QA_NO_LOW_HP_RETURN=1`이면 꺼짐.
>   끔이 저장에서 유지. `ActivePartyLowestHpRatio`를 빼면 컴파일 RED 2건
>   (`low_hp_return_RED.log`).
> - **정직한 미완**: 일과표·시간 배정·오프라인 정산은 💡라 없음. 파티 전용 헤더 없음.
>   V4 70%는 자동으로 안 닫는다. 원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `c196162c`. `W3Party`는 HOLD 후 비율 getter만.

> **이전 이터 결과(코드/실행): §6 월드 티어 선택 — 해금과 선택을 갈랐다.**
> - 큐 1번은 사람 육안, 2번은 오너 보류라 「대기하지 마라」로 기획서 ✅·소비처 0곳을 찾음.
>   `GameState.Tier`는 `(층-1)/10` 한 줄이라 최고 층에 세계가 묶여 있었고,
>   영지 현황 카드는 「10층마다 티어가 오른다」Locked였다. 환생 Lv1이 고층 필드에 강제됐다.
> - **생산 소비처**: `UnlockedTier`(최고 층) / `Tier`(선택) / `TrySelectTier`.
>   해금보다 높으면 거부. 새 10층 해금은 최고를 기본 선택. 필드·던전·합성·침략은 기존처럼 `Tier`.
>   탑 입장 비용만 `UnlockedTier` — 낮춘 세계로 고층이 싸지면 안 된다.
>   `EstateScreen` 현황 카드 → 월드티어 목록. `QA_WORLD_TIER=1`이면 해금 T3·선택 T1.
> - **통과 기준**: 21층에서 T1 선택 가능·해금 T3 유지. 던전 비용 T1 < T3.
>   화면「현재 세계」=T1, T4 잠김. 탑 비용은 해금 유지.
> - **TDD/실행**: `unity_meas` `WorldTierSelfCheck` 전항 PASS
>   (`world_tier_selfcheck.log`). 던전 199 < 511. 거부·저장 유지·새 해금 기본 선택.
> - **화면**(직접 열음, 빈 화면 아님, `QA_WORLD_TIER=1`):
>   `world_tier_shots/qa_go_Estate_tier.png` 604953B — `영지 · 월드티어`,
>   `해금 T3 · 탑 30층 · 최고 기록은 안 내려간다`,
>   T1「현재 세계」, T2/T3「이 티어로 세계를 맞춘다」, T4 Locked「31층 해금」.
> - **네거티브**: T4·음수 선택 거부. 저장을 빼면 선택이 해금 최고로 돌아간다.
>   `TrySelectTier`/`UnlockedTier`를 빼면 SelfCheck 컴파일 RED.
> - **정직한 미완**: 필드 잡몹 전투 스케일은 `W3Party`라 안 건드렸다(던전 계획만 선택 티어).
>   자동화 일정은 그대로 Locked. 파티 전용 헤더 없음. V4 70%는 자동으로 안 닫는다.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `8ad47211`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): §3 특수 직업 첫 슬라이스 — 증표 전직 · 1회 사망 소멸 · 부활초/환생석 거부.**
> - 큐 1번은 사람 육안, 2번은 오너 보류라 「대기하지 마라」로 기획서 ✅·소비처 0곳을 찾음.
>   `IsSpecialJob`는 합성 재료 거부만 있었고, `RegisterDeath`는 일반 3회만, 증표는 드랍표 이름뿐이었다.
>   직업명(사신 등)·특정 던전 특화는 💡라 안 넣음.
> - **생산 소비처**: `LifeSystem.TryBecomeSpecial`/`CanBecomeSpecial`. 증표 1장 소비, 재건·이미 특수·삭제 거부.
>   `RegisterDeath`는 특수+PvE면 즉시 삭제(장비 소멸, 영묘 기록만). PvP는 목숨 0.
>   `UseRevivePotion`/`UseRebornStone`은 특수면 아이템을 안 쓴다. 영묘는 Locked「기록만」.
>   `Economy.CanDropSpecialJobToken` — 50층 미만 Tower10Boss는 증표 0. 10층 환생석은 유지.
>   `CharacterScreen` 상세: 목숨 n/1, 하트 1칸, 부활초 Locked. `QA_SPECIAL_JOB=1` 시드.
> - **통과 기준**: PvE 1회=삭제. 50층 롤에서 증표>0, 10층=0. 화면「목숨 0/1」「부활초 사용」잠김.
> - **TDD/실행**: 정적 99소스. `unity_meas` `SpecialJobSelfCheck` 전항 PASS
>   (`special_job_selfcheck.log`). 50층 증표 166/8000(2%), 10층 증표 0, 10층 환생석 82/8000.
> - **화면**(직접 열음, 빈 화면 아님, `QA_SPECIAL_JOB=1`):
>   `special_job_shots/qa_go:Character.png` 403784B — `탱크 (수호기사) · 특수 직업`,
>   `목숨 0/1 출전 가능 · 부활초·환생석 불가`, 하트 1칸, 부활초 Locked「쓸 수 없다」,
>   `특수 직업 · 1목숨 · 일반 전직 경로 밖`.
> - **네거티브**: 증표 0·이미 특수·재건은 거부·아이템 미소모. 일반 1회 사망은 삭제 아님.
>   PvP 특수 사망은 소멸 아님. 부활초/환생석 거부는 재화 잔존. 10층 증표 0.
> - **정직한 미완**: 사신·성기사 등 직업명·던전 특화 배율은 💡라 없음. 파티 전용 헤더 없음.
>   V4 70%는 자동으로 안 닫는다. 원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `1070efb6`. `W3Party`는 안 건드렸다.

> **이전 이터 결과(코드/실행): §3 합성 나머지 — 비-HP 전투 소비처 · 골드 2 G/h · 인간 +20%p.**
> - 큐 1번은 사람 육안, 2번은 오너 보류라 「대기하지 마라」로 큐 3번을 닫음.
>   첫 슬라이스는 강골→HpMul만. 예리함 등은 저장·화면만이고 `W3Party` `_bAtk` 버스는 던전 임시 강화 전용이었다.
> - **생산 소비처**: `Fusion.CombatOf`/`CostCopper`/`Pick`. `Economy.ActionCostMultiplier["Fusion"]=2`.
>   출전 `SortieCombatant.Fuse` → `W3Party`가 Atk·Range에 곱하고 Spd/Cd/Heal/Shield/AtkSpd는 시전자 배율.
>   골드 부족이면 재료 미소멸. 인간은 호스트 역할 계열 +20%p(탱=강골·방벽).
>   `CharacterScreen` 확인 줄에 실비용. `W3Party`는 HOLD 후 소비처만.
> - **TDD/실행**: 정적 98소스 오류 0. `unity_meas` `FusionSelfCheck` 전항 PASS
>   (`fusion_combat_selfcheck.log`). 예리함 Atk 1.20, T1 비용 20000쿠퍼,
>   인간 139/200=70% · 엘프 104/200=52%.
> - **화면**(직접 열음, 빈 화면 아님, `QA_FUSION=1`):
>   `fusion_combat_shots/qa_go:Character.png` 619485B — 힐러(사제)→탱크,
>   `소멸시키고 흡수한다` 옆에 `5골드 11실버 99쿠퍼`(저장된 T3의 2 G/h=5.12G).
> - **네거티브**: 골드 0이면 거부·재료 잔존. `QA_NO_FUSION=1`이면 예리함 Atk=1.
>   엘프는 +20%p 없음(52%).
> - **정직한 미완**: 원하는 계열을 플레이어가 고르는 UI는 없다(호스트 역할이 기본 선호).
>   파티 전용 헤더 없음. V4 70%는 자동으로 안 닫는다. 원본 에디터 PID 75776은 죽이지 않음.
> - **코드** `abda2a01`. 같은 시각 대화 세션이 캐릭터를 명부/합성 페이지로 나눔(`1de7bf80`) — 확인 줄만 겹쳤고 페이지는 안 되돌렸다.

> **같은 시각 대화 세션: 캐릭터 화면을 명부/합성 페이지로 나눔.**
> - 오너 「계속」. 허브 페이지 다음으로 줄 목록이던 캐릭터 화면.
> - **생산 소비처**: 탭 `명부`=3×2 카드+파티 편성. `합성`=시작/결과/잠금 카드.
>   상세·전직 시험·Fusion.TryFuse 흐름은 그대로. W3Party는 루프 #14가 잡고 안 건드림.
> - **TDD**: 정적 98소스 오류 0.
> - **네거티브**: 재료 없으면 합성 카드 Locked.

> **이전 이터 결과(코드/실행): §3 합성 첫 슬라이스 — 1차 이상 재료를 소멸시켜 패시브 1개를 흡수.**
> - 큐 1번은 사람 육안, 2번은 오너 보류라 「대기하지 마라」로 기획서 ✅·소비처 0곳을 찾음.
>   `CharacterScreen` 「합성」은 Locked 문구만 있었고 `Boons`는 던전 임시 강화만 썼다.
> - **생산 소비처**: `Fusion.TryFuse`/`SacrificeForFusion`. 재료는 로스터에서 삭제(영묘 아님).
>   직업별 제공 풀에서 이미 가진 것을 빼고 1개 추첨. 슬롯 4, 넘치면 본 뒤 교체/포기.
>   강골만 `PartyState.SortieCombatant.HpMul`에 곱한다. `W3Party`는 안 건드렸다.
>   출전·수비 인덱스는 `NotifyRosterRemoved`로 당긴다. 환생은 흡수 전부 소멸.
>   `QA_FUSION=1`이면 캐릭터 화면에 강골 시드. `QA_NO_FUSION=1`이면 배율 1.
> - **TDD/실행**: 정적 97소스 오류 0. `unity_meas` `FusionSelfCheck` 전항 PASS
>   (`fusion_selfcheck.log`). 강골 HpMul 1.25, 중복 거부, 슬롯4, 보류 교체/포기, 환생 소멸.
> - **화면**(직접 열음, 빈 화면 아님):
>   `fusion_shots/qa_go:Character.png` 556263B — 탱크(수호기사) 상세에
>   `흡수 강골 (1/4)`. 옛 Locked「준비 중」이 아님.
> - **네거티브**: 기본직업·자기자신·특수직업 거부. 풀이 비면 재료 미소멸.
>   `QA_NO_FUSION=1`이면 HpMul 1.
> - **정직한 미완**: 예리함·숙련 등 비-HP 배율은 저장·화면만(전투 소비처는 다음).
>   골드 2 G/h·인간 +20%p는 안 넣음. 파티 전용 헤더 없음. V4 70%는 자동으로 안 닫는다.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `b0c85e52`. 대화 세션이 허브 페이지를 같은 시각에 올려 캐릭터 화면은 안 건드렸다.

> **같은 시각 대화 세션: 허브 UI를 페이지·카드로 고도화.**
> - 오너 「너무 단순하고 디자인도 좋지 않음 · 페이지를 추가해 고도화」.
> - **생산 소비처**: `UiPages` 탭/2×2 격자. `GameScreen.DrawTabs`/`DrawCard`.
>   타이틀=히어로+3카드. 영지=건물/현황 페이지. 파티=편성/출전 페이지.
>   캐릭터 화면은 루프 합성 슬라이스와 겹쳐 안 건드림.
> - **TDD**: `UiAtlasSelfCheck` 격자·탭 영역. 정적 98소스 오류 0.
> - **네거티브**: AfterTabs를 빼면 본문이 탭과 겹친다. 카드 Locked는 클릭 false.

> **이전 이터 결과(코드/실행): UI 퀄리티 — `boss_hp_frame`을 보스전 상단 중앙에 연결.**
> - 큐 #1(INBOX 16:46). 등급 프레임은 `fa37a821`. 큐 1번은 사람 육안이라 「대기하지 마라」로
>   기획서 ✅·소비처 0곳을 찾음. `boss_hp_frame`은 Pieces에만 있고 RequiredKeys·화면 0곳.
>   `ActiveTotalHp` 주석이 이미 "HUD가 읽는다"고 했는데 그리는 코드가 없었다.
> - **생산 소비처**: `UiAtlas.BossHpFrameKey`/`PhaseCountForFloor`/`DrawBossHp`.
>   `BattleScreen.Overlay`가 `BossBattle.IsActive`일 때 상단 중앙에 프레임+채움+페이즈 경계선.
>   Begin 직후 `_bossMaxHp = ActiveTotalHp`. `QA_BOSS_HP=1`이면 탑 화면에만 견본 3칸.
>   `W3Party`·`BossBattle`은 안 건드렸다(대화 세션이 경매·침략을 연 직후).
> - **TDD/실행**: 정적 95소스 오류 0. `unity_meas` `UiAtlasSelfCheck` PASS
>   (`boss_hp_frame_selfcheck.log`). 층 1·5=2페이즈, 10=3, 50=4.
> - **화면**(직접 열음, 빈 화면 아님):
>   `boss_hp_shots/qa_go:Tower.png` 193585B — 견본 만피/1/2/낮음이 서로 다르고 경계선이 보임.
>   `boss_hp_shots/qa_boss.png` 648352B — 실전 상단 `35층 보스 7500/7500 페이즈 4`,
>   골렘+파티 위에 장식 프레임 바가 있고 지휘 카드를 덮지 않음.
> - **네거티브**: `DrawBossHp`/`BossHpFrameKey`/`PhaseCountForFloor`를 빼면 컴파일 RED 11건
>   (`boss_hp_frame_RED.log`). `QA_BOSS_HP` 없으면 탑 견본은 안 뜸. 잡몹전은 바 없음.
> - **정직한 미완**: 페이즈 전환 색반전·다음 기믹 아이콘 예고는 §16-5 💡라 안 넣음.
>   파티 전용 헤더 없음. V4 70%는 자동으로 안 닫는다. 원본 에디터 PID 75776은 죽이지 않음.
> - **코드** `0971456c`.

> **이전 이터 결과(코드/실행): 로컬 경매장 + 침략 본게임.**
> - 오너 「영지 3건물 남은 OUT 해결해」. 다른 유저 서버·랭킹·동맹·4면 공성은 안 연다.
> - **경매**: `AuctionState` NPC 장 4칸 + 내 등록(최대 10). 등록 2%·체결 8% 소각.
>   드랍·제작만. 부채/연체면 거부. `EstateScreen.AuctionHouse`가 구매·등록·취소.
> - **침략**: `InvasionState.TryBegin` 출정 비용 → `BattleKind.침략` 실전투 →
>   승리 약탈(층 기준, 수비 공백 가산)·패배 추가 소모·`ApplyPveDefeat(isPvp)`.
> - **TDD**: `AuctionInvasionSelfCheck`. 정적 컴파일 후 `unity_meas` 실행.
> - **네거티브**: 연체 2회면 침략 시작 거부. 부채면 등록/구매 거부. 취소해도 등록 수수료 미소환.
> - 큐 3번 OUT 줄을 내린다. 보드는 막힘에서 빠진다.

> **이전 이터 결과(코드/실행): UI 퀄리티 — §11 등급 프레임 5종을 장비 아이콘에 연결.**
> - 큐 #1(INBOX 16:46). `rarity_common`~`rarity_legendary`는 RequiredKeys에만 있고 화면 소비처 0곳.
>   상태 아이콘 HUD는 대화 세션이 닫음(`d64e6cfa`)이라 안 건드렸다. `W3Party`도 안 건드렸다.
> - **생산 소비처**: `GearGrade` 5단계. 제작품은 일반. `UiAtlas.RarityKey`/`DrawRarity`.
>   `ItemAtlas.DrawGear`가 프레임+부위 아이콘. Character 상세 장착 6칸, 대장간 강화/장착 줄.
>   `QA_UI_RARITY=1`이면 캐릭터 화면에만 견본 5칸(일반/고급/희귀/영웅/전설). 랜덤 옵션은 💡라 안 넣음.
> - **TDD/실행**: 정적 92소스 오류 0. `unity_meas` `UiAtlasSelfCheck`·`ItemAtlasSelfCheck`·
>   `EquipmentSelfCheck` PASS(`rarity_frame_selfcheck.log`·`rarity_item_selfcheck.log`·
>   `rarity_equip_selfcheck.log`). 제작=일반, 전설로 저장 후 재기동 유지.
> - **화면**(직접 열음, 빈 화면 아님):
>   `rarity_frame_shots/qa_go_Character.png` 309325B — 오른쪽 장착 6칸(일반 테두리),
>   아래 견본 5종이 회색·초록·파랑·보라·금으로 갈림. 전직 버튼을 덮지 않음.
>   `rarity_frame_shots/qa_go_Estate_smith.png` 267879B — 송곳니 검 줄에 일반 프레임.
> - **네거티브**: `DrawRarity`/`DrawGear`/`RarityKey`를 빼면 SelfCheck 컴파일 RED
>   (`rarity_frame_RED.log` 심볼 8곳 PASS). `QA_UI_RARITY` 없으면 견본 5칸은 플레이 화면에 안 뜸.
>   옛 저장(등급 칸 없음)은 일반으로 읽힌다.
> - **정직한 미완**: 드랍 등급·랜덤 옵션 없음. 파티 전용 헤더 없음. 원본 에디터 PID 75776은 죽이지 않음.
> - **코드** `fa37a821`.

> **이전 이터 결과(코드): 연체 시 경매장/침략을 실제로 차단.**
> - 오너 보드 유보 「경매장/침략 화면에서 연체 시 실제로 차단」. 문 잠금은 `41c2ded8`에 있었다.
> - **구멍**: 침략은 버튼만 회색이고 `GoBattle`은 그대로라 다른 호출이 잠금을 우회할 수 있었다.
> - **생산 소비처**: `GameFlow.TryGoInvasion`이 `CanInvade`가 아니면 전투에 안 들어간다.
>   영지 경매는 `Locked`+`AuctionHouse`가 이미 거부. 거래서버·침략 본게임은 안 연다.
> - **TDD**: `LoanSanctionSelfCheck`에 QA 시드에서 `TryGoInvasion==false` 추가.
> - **네거티브**: 연체 0·30층이면 침략 문 열림(기존). 연체 1이면 경매만 잠김.
> - 큐 표 11행 유보를 완료로 내림 — 보드 막힘에서 사라진다.

> **이전 이터 결과(코드): 전투 HUD에 켜진 상태 아이콘.**
> - 큐 UI 남은 것. `StatusIconAtlas`는 조각만 있고 `Draw`·전투 소비처 0곳이었다.
> - **생산 소비처**: `StatusIconAtlas.Draw`/`DrawRow`/`LiveKeys`. `W3Party.CommandBar`가
>   방패·도발·집중·최후의 보루가 켜진 카드에만 그린다. `QA_STATUS_ICONS=1`이면 견본.
> - **TDD**: 정적 92소스 오류 0. `StatusVfxSelfCheck`에 켜짐/꺼짐/최후보루→방패 단언.
> - **네거티브**: LiveKeys 전부 false면 0개. 방패 없으면 아이콘 없음.
> - **정직한 미완**: 독·감속 등 전투에 아직 없는 상태는 안 그림. 파티 전용 헤더 없음.
> - **코드** `d64e6cfa`.

> **이전 이터 결과(코드/실행): 대출 연체·파산이 경매장/침략 문을 잠근다.**
> - 큐 #1이자 INBOX 18:40 「혀결해」. 거래서버·침략 본게임·영지 생산 압류·건물 -1레벨·아이템 30% 압류는 OUT.
> - **생산 소비처**: `GameState.RefreshSanctions`가 만기 정각은 연체로 안 세고(`>`), 1회 이자×1.5·2회 침략 잠금·3회 파산(경매 7일 정지·한도 -50%·재대출 7일 유예).
>   `CanUseAuction`/`CanInvade` + `EstateScreen.AuctionHubLockReason`/`WorldMapScreen.InvasionHubLockReason`.
>   부채 보유 중에도 경매 문 잠금(§18-5). 층 게이트와 동시 해금(30층).
> - **TDD/실행**: 정적 92소스 오류 0. `unity_meas` `LoanSanctionSelfCheck.Run` 전항 PASS
>   (`loan_sanction_selfcheck.log`). 연체1 이자 43283=기대값, 파산 한도 51649 < 지갑 30%.
> - **화면**(직접 열음, 빈 화면 아님, `QA_LOAN_OVERDUE=2`):
>   `loan_sanction_shots/qa_go:Estate.png` 343122B — 경매장 잠김「연체 2회 — 경매장 이용 정지(§12)」, 대장간·영묘·수비는 열림.
>   `loan_sanction_shots/qa_go:WorldMap.png` 250471B — 침략 잠김「연체 2회 — 침략 불가(§18-5)」.
> - **네거티브**: 연체 0이면 30층에서 침략 열림. 연체 1회면 침략은 열리고 경매만 잠김.
>   연체 1회만 갚으면 경매가 바로 열림(7일 정지 없음). 만기 정각은 기본 금리(SelfCheck ⑩과 같음).
>   `QA_LOAN_OVERDUE` 없으면 시드 없음. DebugAutoPilot `Earn(500000)`이 시드 빚을 갚아도 잠금 API가 다시 심음.
> - **정직한 미완**: 거래 등록/구매·침략 전투·영지 생산 압류·건물 강등·비장착 30% 압류는 소비 시스템이 없어 안 넣음.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드·촬영.
> - **코드** `41c2ded8`.

> **이전 이터 결과(코드/실행): 외부 테스터 10명 키트 + 삭제 세션.**
> - 오너 「테스터 10개 만들어서 외부 테스터 진행」. 경매·침략·대출 제재는 OUT.
> - **키트**: `loop/v4_testers.json` — 이서연/백호 … 신유라/이랑. 전원 30분 이상, 이름 안 겹침.
> - **생산 소비처**: `V4ExternalPlaytest.Run`이 각자 애착 캐릭터를 `AddExp`로 키우고
>   (4명은 Lv20+1차+흉갑+강화), 그 한 명만 `ApplyWipe` 3회. 벤치 4명은 산다.
> - **TDD/실행**: 정적 92소스 오류 0. `unity_meas` `[V4Playtest] PASS`.
>   삭제 10/10 · 계속경로(남은 파티) 10/10. `v4_playtest/sessions.json`.
> - **네거티브**: 2회 사망은 삭제 아님. 생존이 있으면 긴급 재건 안 줌.
>   사람 70%는 `human_70=pending` — 자동으로 닫지 않음.
> - **화면**: 보드는 그래프 아래 `V4 외부 테스터 10` 카드.

> **이번 이터 결과(코드/실행): UI 퀄리티 — 아이템 아틀라스를 대장간·캐릭터 버튼에 연결.**
> - 큐 #1(INBOX 16:46). 작업 중 오너 18:40 「혀결해」(대출 연체)가 들어왔으나 한 항목만 닫는 규칙이라 다음 큐 1번으로 넘김.
> - **구멍**: `item_atlas` 16조각과 `ItemAtlas.KeyFor`는 있는데 `Row`/`Locked`가 `UiAtlas.Draw`만 불러 검·물약이 글자였다. 강화 버튼은 건물 실루엣(`building_smith`)을 쓰고 있었다.
> - **생산 소비처**: `ItemAtlas.DrawHud`(허브 없으면 아이템) · `KeyForSlot` 6부위 · `KeyForGear` · `SmithMaterials` 7종. 대장간 재료 줄·강화·제작·장착, 캐릭터 부활초/전직재료, 영묘 환생석.
> - **TDD/실행**: 정적 90소스 오류 0. `unity_meas` `ItemAtlasSelfCheck.Run` PASS
>   (`item_icon_selfcheck.log`). `game_asset_names` 이상 없음.
> - **화면**(직접 열음, 빈 화면 아님):
>   `item_icon_shots/qa_go_Estate_smith.png` 549700B — 재료 7종 아이콘+숫자(장갑18·검13·투구13·방패13·지팡이13·부적13·금화8),
>   강화 버튼은 금화(옛 건물 실루엣 아님), 장착 줄은 검.
>   옛 `smith_enhance_shots/qa_go_Estate_smith.png`(318037B, 글자 나열·건물 아이콘)과 갈림.
> - **네거티브**: `KeyForSlot`/`DrawHud`/`KeyForGear`를 빼면 SelfCheck 컴파일 RED.
>   `DrawHud`를 `UiAtlas.Draw`만으로 되돌리면 강화·장착 아이콘이 사라진다(`gold`/`sword`는 허브 아틀라스에 없음).
> - **정직한 미완**: 파티 전용 헤더 없음. `StatusIconAtlas`는 Draw/전투 소비처 0. 등급 프레임 5종도 소비처 0.
>   경매·침략·대출 연체는 안 열었다. 원본 에디터 PID 75776은 죽이지 않음.
> - **코드** `63da5850`.

> **이전 이터 결과(코드/실행): 대장간 둘째 슬라이스 — 강화 +15 · 6부위 · 계열 재료.**
> - 큐 #1. 경매 거래서버·침략 본게임은 안 열었다. `W3Party`는 대화 세션 HUD 작업 중이라 안 건드렸다.
> - **생산 소비처**: `Equipment.TryCraft` 6레시피(야수가죽/송곳니·언데드유골·기계부품·정령원소·마족마정).
>   `TryEnhance` +0~+15, 실패해도 장비 유지·석만 소모. `HpMulOf`가 장착 6부위의 강화 포함 배율을 곱하고
>   기존 `SortieCombatant.HpMul` → `W3Party.GearHpMultiplier`가 읽는다.
> - **TDD/실행**: 정적 90소스 오류 0. `unity_meas` `EquipmentSelfCheck.Run` 전항 PASS
>   (`smith_enhance_selfcheck.log`). 6부위 합산 1.386, +15 상한, 실패 시 장비 잔존.
> - **화면**(직접 열음, 빈 화면 아님):
>   `smith_enhance_shots/qa_go_Estate_smith.png` 318037B — 제목 영지·대장간,
>   계열 재료 6종+강화석, `송곳니 검 +3 강화`(석 4·성공 85%·실패해도 파괴 없음),
>   `탱크 · 송곳니 검+3 외 5` 체력 ×1.56.
> - **네거티브**: `QA_ENHANCE_FAIL=1`이면 석 소모·강화 0·장비 잔존. +15에서 석 미소모.
>   `QA_NO_GEAR=1`이면 배율 1. 삭제 시 장착 6부위만 소멸·가방 장비 유지.
> - **정직한 미완**: 강화 골드 소모는 §18-2에 단계표가 없어 안 넣음(석만). 등급·랜덤옵션 없음.
>   경매장 거래서버 없음. 수비 침략 전투는 OUT. 원본 에디터 PID 75776은 죽이지 않음.
> - **코드** `783af30e`.

> **이전 이터 결과(코드): 전투 HUD 초상을 키우고 옆에 스킬을 붙임.**
> - 카드 196×132, 초상 92×112(옛 36×46). 스킬 아이콘 2칸이 초상 오른쪽. 호버 시 이름.
> - 오른쪽 끝 선택-전용 스킬 줄은 없앰 — 초상 옆이 소비처.
> - Play를 다시 켜야 에디터에 보인다.

> **이전 이터 결과(코드/실행): 긴급 탈출 6초 캐스트·피격 취소.**
> - ESC는 즉시 소모하지 않는다. `EmergencyEscape` 6초 캐스트 → 끝나면 두루마리 1개 소모 후 영지.
>   캐스트 중 파티가 실제 HP를 잃으면 취소·미소모(`W3Party.LastPartyDamageAt`).
> - **TDD**: `EmergencyEscapeSelfCheck` PASS. 정적 컴파일 오류 0.
> - 루프의 대장간 둘째 파일은 커밋에 넣지 않았다.
> - 대출 파산·경매·침략 본게임·V4 70%는 안 열었다.

> **오너 선택(2026-08-16): 보드 「내 선택」은 정말 중요한 것만. 외부 테스터는 제외.**

> **이번 이터 결과(코드/실행): 오너 18:04 「영지 3건물 이걸로 진행」의 대장간 첫 슬라이스.**
> - 경매·침략 본게임은 서버/OUT이라 안 열었다. 수비대 배치(`DefenseState`)는 동시 세션이
>   출전 제외 소비처를 이미 넣고 있어 덮지 않고 함께 컴파일·SelfCheck했다.
> - **생산 소비처**: `Economy.LifeItem.CraftHide` 필드 생존 1장 + 보스 테이블.
>   `Equipment.TryCraftLeatherArmor`(가죽5→흉갑, 1차 전직 해금). 장착은
>   `CharacterRecord.EquippedArmorId`. `PartyState.SortieCombatant.HpMul` →
>   `W3Party.GearHpMultiplier`가 MaxHp에 곱한다.
> - **TDD/실행**: 정적 88소스 오류 0, 검사기 고의 오류 1건 탐지.
>   `unity_meas` `EquipmentSelfCheck.Run` 전항 PASS(`smith_selfcheck.log`).
>   `DefenseStateSelfCheck.Run` PASS. `QA_NO_GEAR=1`이면 배율 1.
> - **화면**(직접 열음, 빈 화면 아님):
>   `smith_shots/qa_go_Estate_smith.png` 313566B — 제목 영지·대장간, 가죽 10장,
>   제작 버튼, `탱크 · 가죽 흉갑 착용 중`. 옛 잠금 문구 없음.
> - **네거티브**: 기본직업만이면 제작 거부. 가죽 0이면 제작 거부. 삭제 시 장착 장비 소멸.
>   환생은 장비 없이 돌아옴. `QA_NO_GEAR=1`이면 HP 배율 1.
> - **정직한 미완**: 강화 +15·5부위·계열 재료 없음. 경매장 거래서버 없음.
>   수비 침략 전투 없음. 명부 5줄이 하단바와 겹침. 원본 에디터 PID 75776은 죽이지 않음.
> - **코드** `ec234975`.

> **이전 이터 결과(코드/실행): 파티 편성 명부에 이미 있던 초상 프레임·역할·목숨을 연결.**
> - 큐 맨 위는 V4 70% 사람 관문이라 자동으로 안 닫음. INBOX UI의 남은 빈 칸:
>   캐릭터 화면은 초상+하트+역할인데 편성은 회색 글자 `목숨 3/3`만(정의만 있고 이 화면 소비처 0).
>   전용 헤더 조각은 없어서 `HeaderKey("Party")==null` 유지 — 나침반 폴백을 되돌리지 않음.
> - **생산 소비처**: `UiAtlas.SlotChrome`·`DrawRosterFrame`·`DrawRosterMarks`.
>   `PartyScreen`이 캐릭터와 같은 프레임/역할뱃지/`DrawHearts`를 그린다. 새 힉스필드 0.
> - **TDD**: 생산 API 부재 컴파일 오류 6건 RED(`party_chrome_RED.log`). 이후 정적 84소스 오류 0,
>   검사기 고의 오류 1건 탐지. `unity_meas` `UiAtlasSelfCheck.Run` PASS(`party_chrome_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님):
>   `party_chrome_shots/qa_go:Party.png` 643294B — 5인 초상+프레임, 역할 뱃지(방패/검/지팡이/별)가
>   서로 다름, 온전한 하트 3칸, 설명은 `편성됨`(옛 `목숨 3/3` 글자 없음).
>   옛 `shots/qa_go:Party.png`(회색 글자 버튼·하트 없음)과 갈림.
> - **네거티브**: `SlotChrome`/`DrawRosterFrame`/`DrawRosterMarks`를 빼면 SelfCheck 컴파일 RED 6건.
>   `PartyScreen.DrawSlotChrome`을 되돌리면 편성이 다시 글자만.
> - **정직한 미완**: 파티 제목 옆 전용 아이콘은 아틀라스에 없음(의도). V4 70%는 자동으로 안 닫는다.
>   원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드했다.

> **이전 이터 결과(코드): ashes-code·ashes-art 에이전트가 남은 갭을 가른 뒤 거짓 버튼 2개를 닫음.**
> - 레벨 전투력(`LevelStatMultiplier` + SelfCheck)은 이미 닫혀 있었다. 파티 나침반도 낡은 인계.
> - 새 힉스필드 대상 0. `village_fence_1`만 기존 PNG를 집 뒤에 번갈아 세움.
> - **전투 ESC**가 `GameScreen` 공짜 영지 이동을 타고 있었다. 두루마리 1개 실소모로만 귀환.
>   0개면 전투에 남는다. 6초 캐스트는 안 넣음.
> - 타이틀 「이어하기」는 저장 슬롯이 없는데 시작과 같게 동작 → `Locked`.
> - 사람 관문: V4 70%. 영지 3건물·보스 애니는 소비 시스템 없어 그대로.

아이콘·호버 육안은 위 관문 사이 빈 칸을 채울 때만. 기획서 OUT을 새 범위로 열지 마라.

> **이번 이터 결과(코드/실행): 그래픽 연결 점검 + 검은 화면 3장 배경.**
> - `game_asset_names.py` 런타임 키는 이상 없음. 캐릭터 4종×13·몹 4종×22·보스 정적 4·FX 8·허브 배경 6은 이미 연결.
> - **안 만들 것(소비처 없음/재생성 금지):** 1차 11종 전신, 영지 건물 7종, 보스 애니, `estate_barrel/crate`.
> - **연결 구멍:** `village_tree_0`이 반입만 되고 `GetPropNames`에 없어 화면에 0그루. 집 옆에만 세움(길에 안 올라감).
> - **생성:** 타이틀·결과·던전 배경 3장 `nano_banana_2` 1376×768. 스타일은 기존 `bg_party` 재사용. 전투는 카메라라 배경 없음.
> - **화면:** `bg_rest_shots/qa_go:Title.png` 374000B 고목+계단. `qa_go:Result.png` 188780B 모닥불·방패. `qa_go:Dungeon.png` 163087B 석재 바닥. `qa_hunt.png` 818514B 집 옆 열매나무 + 몹 4계열.
> - 파티 헤더 나침반은 이미 폴백 제거됨(`HeaderKey` null = 아이콘 없음). 전용 조각이 없어서 새로 안 그림.

아이콘·호버 육안은 위 관문 사이 빈 칸을 채울 때만. 기획서 OUT을 새 범위로 열지 마라.

> **루프 세팅(2026-08-16):** 클로드 주간 한도 + 코덱스 `usage limit … Aug 23 9:23 AM` 실측. 실행기는 `loop/agent=grok`. 그록은 stdin을 프롬프트로 안 읽으므로 `--prompt-file`로 호출한다.

> **오너 선택(2026-08-16 18:23): V4 외부 테스터 70% → 보류.**


최종 갱신: 2026-08-16 · 이터레이션(폴더 함정 정리)

> **이번 이터 결과(정리): INBOX 17:13 폴더·파일 정리 — 죽은 함정만 한 슬라이스.**
> - 범위가 한 줄(「지금 폴더 구조 개편」)이라 STATUS 지침대로 **라이브 경로는 안 옮겼다.**
>   `./tools/qa_shot.sh`·`docs/STATUS.md`·`schedules.json`·`Assets/Scripts/`(대화 세션
>   소유, 17:15 편집)는 손대지 않음. `My project/`·`build_*`는 오너 로컬/측정물이라 삭제 안 함.
> - **지운 것**: 빈 함정 `unity/Assets/_Game/Art/`(+FX/Ground/Sprites meta) —
>   `Resources.Load`가 못 읽는 자리(SpriteBank 주석·2026-08-13 실측). 루트 유령
>   `qa_vfx_live/qa_boss.png`(소비처 0). 빈 `out_p2_gem/`·`art/out_p2_charger_b/`.
>   캡처 사본만 `output/qa/ashes-to-stars/shots/legacy_qa_vfx_live_boss.png`(gitignore).
> - **재발 방지**: `game_asset_names.art_trap_problems()` — `_Game/Art`에 PNG가 있거나
>   빈 폴더만 남아도 `--strict` FAIL. `split_sheets.py` 출력 안내를 `Resources/sprites`로 정정.
>   프로젝트 `README.md` 폴더 지도. 루트 `.gitignore`에 `qa_vfx_live/`.
> - **검증**: 네거티브 `trap.png` 주입 → `--strict` exit 1 + 함정 경고. 제거 후
>   `✅ 네이밍·반영 이상 없음` exit 0. 빈 폴더만 남겨도 exit 1. `grep` 런타임 소비처 0
>   (경고 주석·검사기·README만). `check_all.py` FAIL 0 — 남은 WARN은 이번과 무관한
>   기존(추적된 blender results·`.mcp.json`). 커밋 후 README는 unclassified에서 빠진다.
> - **정직한 미완**: 저장소 전체 개편(docs/GAME_* 이동, tools/ 이관, Assets/Editor 통합,
>   art/out_* 재배치)은 라이브 경로라 다음 슬라이스. 파티 헤더는 UI 큐.
> - **코드** `8c0defaf`(삭제) + `8c988f35`(검사기·지도).
> - ⚠️ 이 이터 중 다른 세션이 `art/.generating`에 `spec_p8_bg_rest.json 17:23 pid=83851`
>   (PGID==PID, 생존)을 걸어 **타이틀·결과 배경**을 뽑고 있다. 다음 이터는 표시·산출을
>   먼저 보고 **재생성 금지**. 그 세션 파일(Title/Result/Style/Dungeon/FieldDecor,
>   ARTIFACT_INDEX)은 이 커밋에 안 넣었다.

> **이전 이터 결과(코드/실행): UI 퀄리티 둘째 슬라이스 — 필드·탑 헤더 + 버튼 3상태 육안.**
> - 큐 #1이자 INBOX UI의 남은 빈 칸. 아틀라스에 `field`/`tower`/`button_hover`/`button_pressed`가
>   이미 있는데 필드·탑 제목은 기본 나침반(`worldmap`)이었다(정의만 있고 화면이 틀린 조각).
>   새 힉스필드 생성 없음. 오너 Unity PID 75776은 죽이지 않았고 `unity_meas`로 빌드·촬영.
> - **생산 소비처**: `UiAtlas.HeaderKey` (Field→field, Tower→tower). `GameScreen` 기본 헤더가
>   씬 이름으로 그 키를 읽고, Field/Tower가 같은 키를 명시 override. `QA_UI_STATES=1`일 때만
>   보통·호버·눌림 견본 3칸(`ButtonStateSamples`) — qa_shot에 마우스가 없어 산 호버는 안 찍힘.
> - **TDD**: 생산 API 부재 컴파일 오류 16건 RED(`ui_chrome_RED.log`). 이후 정적 86소스 오류 0,
>   검사기 고의 오류 1건 탐지. `unity_meas` `UiAtlasSelfCheck.Run` PASS(`ui_chrome_selfcheck.log`).
> - **화면**(직접 열음, 빈 화면 아님):
>   `ui_chrome_shots/qa_go:Field.png` 494741B — 제목 옆 검+월계관(필드). 옛 나침반·단색과 갈림.
>   `ui_chrome_shots/qa_go:Tower.png` 230392B — 제목 옆 등대(탑). 옛 `shots/qa_go:Tower.png`
>   (아이콘 없음·회색 기본 버튼)과 갈림. 두 장 모두 하단 견본 보통(어두움)/호버(금빛)/눌림(눌린 금).
> - **네거티브**: `HeaderKey`/`ButtonStateSamples`를 빼면 SelfCheck 컴파일 RED 16건.
>   Field/Tower override만 지워도 기본 `HeaderKey(scene)`이 받쳐 준다. 그 매핑까지 지우면
>   필드·탑이 다시 나침반. `QA_UI_STATES` 없으면 견본 3칸은 플레이 화면에 안 뜬다.
> - **정직한 미완**: 파티 편성 헤더는 `HeaderKey("Party")==null`이라 여전히 나침반.
>   `item_atlas`/`status_icon_atlas`/`combat_icon_atlas`는 타 세션 자산 — 이 커밋에 안 넣음.
>   V4 70% 사람 판정은 자동으로 안 닫는다. **다음 이터는 INBOX 17:13 폴더 정리**가 앞선다.

> **이전 이터 결과(코드/실행): UI 퀄리티 첫 슬라이스 — 정의만 있던 아틀라스 조각을 화면에 연결.**
> - 오너 INBOX 16:46 「다른 게임 분석해서 UI 퀄리티」. 한 슬라이스만. 새 힉스필드 생성 없음
>   (`waiting` 0, 아틀라스에 건물·목숨·역할이 이미 있는데 소비처 0곳 — 이 저장소의 반복 함정).
> - **다른 게임에서 가져온 것**(세븐나이츠 키우기·CoC 허브·AFK식 로스터): 목적지는
>   아이콘+짧은 라벨, 헤더는 지금 있는 곳, 명부는 얼굴+목숨 파이프, 게이지는 프레임.
> - **생산 소비처**: `HeaderIcon`(영지=성, 캐릭터=파티). 영지 4건물 `BuildingKey`.
>   캐릭터 목록 초상+역할뱃지+`DrawHearts`+`xp_frame`. 유니코드 하트(□로 나옴) 제거.
> - **TDD**: 정적 컴파일 85소스 오류 0, 검사기 고의 오류 1건 탐지. `unity_meas`
>   `UiAtlasSelfCheck.Run` PASS(역할/건물/목숨 키·HeartKey 삭제=3칸 깨짐).
> - **화면**(직접 열음, 빈 화면 아님):
>   `ui_icon_shots/qa_go:Estate.png` 369940B — 제목 옆 성, 대장간·경매장·영묘·수비대
>   실루엣이 버튼 왼쪽에 서로 다르게 보임. 옛 `shots/qa_go:Estate.png`(회색 글자만)과 갈림.
>   `ui_icon_shots/qa_go:Character.png` 337474B — 제목 옆 파티 아이콘, 초상 6장,
>   삭제=깨진 하트 3, 재건1=온전한 하트 3+XP 바. 옛 나침반 헤더·□ 하트와 갈림.
> - **네거티브**: `RoleKey`/`BuildingKey`/`heart` 조각을 빼면 SelfCheck FAIL.
>   화면 소비를 되돌리면 영지는 다시 글자만, 캐릭터는 다시 □/나침반.
> - **정직한 미완**: 호버/눌림 육안, 필드·탑 헤더는 아직 나침반. V4 70% 사람 판정은
>   자동으로 안 닫는다. 원본 에디터 PID 75776은 죽이지 않았고 사본으로 빌드했다.

> **이전 이터 결과(코드/실행): V4 삭제 루프 첫 슬라이스.**
> - 오너가 V4 준비를 골랐다(16:52). 창에서 사람을 대신 판정하지는 않았고, 코드에 없던
>   **패배→목숨→삭제→계속 플레이** 생산 경계만 닫았다.
> - **단절**: `OnPartyWiped`(힐체크 실패)는 결과만 열고 목숨을 안 깎았다. `OnBattleEnd`
>   전멸은 출전이 아니라 **로스터 전원**에게 `RegisterDeath`를 뿌렸다. 생존 0명 긴급 재건은
>   헌법에 있는데 코드 소비처 0곳이었다.
> - **생산 경계**: `GameFlow.ApplyPveDefeat` → `LifeSystem.ApplyWipe(출전 레코드)`.
>   BattleScreen 전멸·힐체크 실패가 한 판에 한 번만 부른다. 생존 0명이면 Lv1 기본직업
>   긴급 재건 1명. 살아있는 재건이 있으면 두 번째 무료 영입은 거부.
> - **TDD**: 생산 API 부재 컴파일 오류 17건 RED(`v4_wipe_RED.log`). 구현 후 정적 컴파일
>   85소스 오류 0, 검사기 고의 오류 1건 탐지.
> - **실행 수치**: unity_meas SelfCheck ⑫ 전부 PASS — 출전2만 사망1 / 벤치3 목숨0 /
>   PvP 목숨0 / 3회=삭제 / 생존3이면 재건 없음 / 전원삭제→재건1(딜 Lv1) /
>   재건 중복 거부 / 재기동 후 삭제+재건 유지. 로그 `v4_wipe_selfcheck.log`.
> - **화면**: `v4_wipe_shots/qa_go:Result.png` — 보스전 패배·영묘 기록·재건1 계속 플레이.
>   `qa_go:Character.png` — 기존 5인 전부 `삭제됨`, `재건1 (딜) ❤❤❤ · 재건` 출전 가능.
>   빈 화면 아님. 원본 에디터는 죽이지 않았고 사본으로 빌드했다.
> - **네거티브**: API 제거 시 컴파일 RED 17건. `isPvp:true`면 사망 0. 벤치 목숨을 건드리면
>   SelfCheck ⑫ FAIL. 재건을 생존 중에 또 주면 중복 영입 FAIL.
> - **정직한 미완**: V4 **외부 테스터 70%** 판정은 자동으로 닫지 않는다(오너 보드도
>   “자동검사로 통과를 선언한 것이 아니다”라고 적음). 전투 중 개인 사망은 W3Party 타이밍.
> - **커밋 귀속**: 생산 코드(LifeSystem·BattleScreen·SelfCheck)가 보드 세션의
>   `3b9563af chore(game): 보드 커밋`에 먼저 실렸다. 이 커밋은 인수인계·QA 시드·화면 소비처.

> **이전 이터 결과(코드/실행): V3 한 판 종단.**
> - HP·장판·힐 보고 조각은 다시 만들지 않았다. 빠진 건 **처치가 탑 층을 올리는 생산 경계**와
>   그 경계를 같은 실행에서 잇는 증거였다. `GameFlow.ApplyTowerBossVictory`를 신설하고
>   `BattleScreen` OnBossDefeated가 직접 `ClearFloor` 대신 그걸 부른다.
> - `BossBattleRunSelfCheck`가 **한 실행**에서 장판 피해·힐체크 보고·소환 생존·HP 9000→4500
>   (페이즈 1)·처치·층 5→6을 단언한다. `BOSS_NO_DPS=1`은 HP 9000 유지·처치 0·층 5 유지.
> - **TDD**: 생산 API 부재 컴파일 오류 3건 RED(`boss_run_RED.log`). 구현 후 정적 컴파일
>   85소스 오류 0, 검사기 고의 오류 1건 탐지.
> - **실행 수치**: Unity 6000.5.6f1 `unity_meas` `-executeMethod` exit 0,
>   `[BossBattleRunSelfCheck] PASS hp=9000→4500→0 phase=1 aoe>0 heal_report=aoe summon>0 floor=5→6 negative_floor=5`.
> - **화면**: `boss_run_shots/qa_boss.png`(676380 bytes)를 직접 열어 거석 골렘 보스·5인 파티·
>   마을 지형이 한 화면에 렌더됨을 확인(빈 화면 아님). `qa_shot.sh boss 600`은 기존 던전
>   보스 지름길 — 층 숫자는 SelfCheck가 증명한다.
> - **네거티브**: `ApplyTowerBossVictory`를 지우면 컴파일 RED. `BOSS_NO_DPS=1`이면 층이 안 오른다.
> - 코드 `ec927cbe`. 증거 `output/qa/ashes-to-stars/boss_run_*`.

> **이전 이터 결과(코드/실행): UI 프레임 소비처 — 버튼 3상태·9-slice·체력바**

> **이번 이터 결과(코드/실행): UI 아틀라스 조각을 실제 화면에 연결.**
> - 클로드 세션이 주간 한도로 끊긴 지점: 배경 6장 반입 직후, 합의된 2단계
>   (패널 9-slice · 버튼 3상태 · 체력바)를 못 닫은 채 중단. 아틀라스에는
>   `button_hover`/`button_pressed`/`hp_frame`이 있었는데 소비처가 0곳이었다.
> - `UiAtlas.ButtonKey`·`DrawSliced`·`DrawMeter`를 단일 도구로 두고
>   `GameScreen` 버튼/잠김/정보 패널과 `W3Party` 파티 카드 HP·스킬 버튼이 소비한다.
>   프레임이 없으면 예전 단색으로 조용히 돌아간다.
> - **검증**: 정적 컴파일 84소스 오류 0. `unity_meas`에서 `UiAtlasSelfCheck.Run`
>   PASS(호버/눌림 키 우선순위 + hp_frame 실재). 원본 에디터는 열어 둔 채 사본만 실행.
> - **정직한 미완**: 호버/눌림 육안 캡처와 아이콘 3단계는 아직이다. V3 한 판 종단도 그대로 남음.
>   증거: `output/qa/ashes-to-stars/ui_frame_selfcheck.log`.

> **이번 이터 결과(코드/실행): §10-5 보스 장판 피해→힐체크 피해 보고 한 경계.**
> - `BossBattle` 장판이 W3 실파티 위치 2곳을 1초 예고한 뒤 공용 `Damage` 경계로 HP를 깎는다.
>   이 공용 경계가 보호막/무적/치명 초과분을 제외한 **실제 HP 감소량만** 활성 힐체크에 보고한다.
>   힐체크는 발동 전 타이머/피해를 세지 않으며 `Begin` 재진입 때 이전 장판을 비운다.
> - **실행 수치**: 정상 장판 2개 피해 `60+30=90`, 힐체크 보고 `90`; `BOSS_NO_AOE=1`은
>   신규 판과 같은 컴포넌트 재진입 모두 HP 피해0·보고0. Unity SelfCheck PASS.
> - **화면**: `boss_aoe_shots/qa_boss.png`(691,162 bytes)를 직접 열어 큰 보스·5인 파티·주황색
>   장판 예고·잡몹이 한 화면에 실제 렌더됨을 확인. 정적 컴파일 83소스 오류0, 검사기 고의 오류1
>   탐지, 독립 리뷰 재검토 Critical/Important 0. 코드 `d59a267c`, 증거
>   `output/qa/ashes-to-stars/boss_aoe_*`.
> - **남은 단절**: HP·장판·힐 피해 경계는 각각 닫혔지만 한 판에서 힐체크 판정→보스 처치→
>   탑 층 돌파까지 이어지는 V3 통합 실행 증거는 아직 없다. 다음은 이 단일 종단 검증/수리다.

> **이번 이터 결과(코드): §10-5 보스 HP 첫 슬라이스 — 실제 보스 타깃 피해 배선.**
> - 최상위 큐 #1 `qa_shot.sh hunt 180`을 먼저 재시도했으나 원본 manifest의 타 세션
>   `com.unity.modules.physicscore2d@1.0.0` 미해결 의존성으로 exit 1. 증거:
>   `output/qa/ashes-to-stars/mob_family_hunt_attempt_20260816.log`. 따라서 #1은 GUI 대기 유지.
> - 원장 「지금 당장 할 일」 1순위 V3에서 다음 단절점인 파티 공격→보스 HP만 잡았다.
>   `BossBattle.AttachCombatTargets`가 W3에 보스별 비가시 공격 타깃을 등록하고, 보스전에서는
>   일반 웨이브 보충을 멈춘다. 실제로 그 타깃을 맞힌 피해만 해당 `bossIndex` HP·페이즈·처치에
>   전달한다. 쫄/잡몹 피해 복제, 보스 프록시 이동·공격·처치 보상은 독립 리뷰에서 발견해 제거했다.
> - **TDD/네거티브**: 신규 HP/피해 API 부재 컴파일 오류 10건 RED. SelfCheck 계약은
>   5층 HP `9000→4500→0`, 페이즈1회·처치1회, `BOSS_NO_DPS=1`에서 Boss/W3 HP 모두
>   `9000→9000`을 실제 `W3Party.DamageMob` 경계로 검증한다. 정적 컴파일 82소스 오류0,
>   검사기 고의 오류1 탐지, diff check 통과, 최종 리뷰 Critical/Important 0. 코드 `e48aee09`.
> - **정직한 미완**: Unity 실행 SelfCheck·보스 PNG는 위 패키지 의존성 때문에 미확인이다.
>   #2b 전체도 장판 피해·힐체크 피해 보고가 남아 완료로 내리지 않는다.

> **이번 이터 결과(검증/수리): 전직 #8 아홉 번째 슬라이스 — 1차 6종 슬롯1/2 실행 증거 종결.**
> - 일반 프레임 캡처가 `advancement` 전용 8초 계측보다 먼저 종료해 PNG만 남기던 제어 흐름을
>   분리했다. 첫 실제 RED에서 광전사 `18.0/0.0 FAIL`, 드루이드 `12.0/0.0 FAIL`을 재현했고,
>   각각 주 대상 명중 누락과 만피 회복 픽스처 부재를 최소 수리했다. 코드 `b35fc3ac`.
> - **최종 실행 수치(슬롯1/2)**: 광전사 `18.0/1.0`, 궁수 `62.4/4.0`, 소환사 `93.6/1.0`,
>   드루이드 `12.0/28.0`, 주술사 `5.0/2.5`, 정령사 `5.0/100.0` — 모두 `>0`, 6/6 PASS.
> - **네거티브**: 광전사 `QA_ADV_NO_SLOT=1` → `0.0/1.0 FAIL`, `=2` → `18.0/0.0 FAIL`.
>   정상 6장+차단 2장과 각 Player.log는 `output/qa/ashes-to-stars/first_advancement/`에 보존.
> - **화면**: 정상 PNG 6장을 직접 열어 전투·파티5인·몹·직업 연출이 모두 렌더된 것을 확인했다.
>   정적 컴파일 80소스 오류0, 검사기 고의 오류1 탐지, 독립 리뷰 Critical/Important/Minor 0.
> - 원본의 타 세션 미커밋 `physicscore2d` 의존성은 건드리지 않고 git 제외 `unity_meas`에서만
>   제거해 빌드했다. `game_asset_names`의 타 세션 `item_atlas`/VFX 미커밋 19건도 포함하지 않았다.

> **이번 이터 결과(코드): 전직 #8 여덟 번째 슬라이스 — 4스킬+초필 1개 실전 소비처.**
> - 2차 `Second`만 직업 고유 자원과 별개인 초필 게이지를 전투 중 누적하고, 100%·쿨다운0
>   종료에서만 E/별도 버튼으로 발동한다. 재사용 대기는 §18-6 확정값 180초. 코드 `31a50057`.
> - 프로토타입 효과는 현재 전투 역할 경계를 극대화했다: 탱=전원 3초 보호, 딜=광역 폭발,
>   힐=전원 회복·보호막, 버퍼=전원 가속·보호막. 직업별 세부 밸런스는 지어내지 않았다.
> - **실행 검증**: 광전사(탱) 400·궁수(딜) 632·드루이드(힐) 150·주술사(버퍼) 205, 모두
>   효과 `>0`·쿨 180초 PASS. 네거티브는 1차 단계/게이지99/쿨10초 각각 효과0·PASS.
> - **화면**: `shots/qa_second_광전사_normal.png` 육안 확인 — 2차 선택 카드, 기존 2개 조작
>   버튼, 별도 `초필 2% / 178s` 비활성 버튼, 파티 보호 연출이 함께 보임.
> - **TDD/검증**: 신규 계약 API 부재 7건 RED + 리뷰 회귀 9건 RED, 최종 정적 컴파일
>   80소스 오류0, 검사기 고의 오류1 탐지, `unity_meas` SelfCheck 실행 PASS, 독립 리뷰
>   Critical/Important 0. 증거 `output/qa/ashes-to-stars/second_ultimate_*`.
> - **인수인계**: `game_asset_names` 경고 19건은 타 세션의 미커밋 `item_atlas`/VFX 자산이며 이 커밋에
>   포함하지 않았다. 다음 한 항목은 이전에 코드만 있고 실행이 막혔던 **1차 6종 슬롯1/2 정상·
>   `QA_ADV_NO_SLOT` 차단 실행 수치·PNG 증거 종결**.

> **이번 이터 결과(코드): 전직 #8 일곱 번째 슬라이스 — Lv50 같은 직업 2차 각성 상태·시험·화면.**
> - 1차 직업 캐릭터만 Lv50·전직 재료20 조건에서 비살상 역할 시험을 시작하고, 성공 확인 때만
>   직업명은 그대로 둔 채 `AdvancementTier.Second`로 저장한다. Lv49·재료부족·중단·반복 각성은
>   상태 불변이며 저장 실패 주입은 재기동 뒤 1차 단계와 재료를 모두 복원한다. 코드 `4a0b21d3`.
> - Character 화면은 Lv50/재료20 잠김, 역할별 시험, 같은 직업 각성 완료 상태를 소비한다. 전투의
>   초필살기 자체는 아직 배선하지 않았으므로 완료 문구도 “초필살기 전투 배선 대기”로 정직하게 표시한다.
> - **TDD/검증**: 생산 코드 전 신규 API 부재 컴파일 오류 **11건 RED**. 최종 정적 컴파일
>   **79소스 오류 0**, 검사기 self-test 고의 오류 1건 탐지, `git diff --check` 통과. 증거:
>   `output/qa/ashes-to-stars/second_advancement_{RED,compile,negctrl,assets,qa}.log`.
> - **시각 QA 블로커**: `qa_shot.sh go:Character 180`은 오너 Unity PID 43474가 원본 프로젝트를
>   점유해 exit 21. 편집기는 죽이지 않았다. 자산 검사는 이번 변경과 무관한 타 세션 미커밋 자산
>   19개만 경고했다. **다음 한 항목**: 2차의 `4개+초필 1개`, 180초·게이지100% 전투 소비처와 계측.

> **이번 이터 결과(코드): 전직 #8 여섯 번째 슬라이스 — 6종 슬롯1/2 실전 계측 경로 추가.**
> - `GAME_START=advancement` + `QA_ADV_JOB=<직업>`이 결정적 신규 로스터의 해당 1차 직업을
>   BattleScreen에 넣고 슬롯1/2를 1초·3초에 강제 시전한다. 6종은 각각 피해/명중/지속/
>   교체/회복/지연/보호막 중 슬롯별 서로 다른 수치를 남기고, 8초 후 둘 다 `>0`인지
>   로그와 PNG로 판정한다. `QA_ADV_NO_SLOT=1|2`는 해당 슬롯을 빼 0/FAIL을 만든다. 코드 `ab5fe3d0`.
> - **TDD/헤드리스**: 생산 API 전 SelfCheck로 컴파일 오류 1건 RED. 최종 정적 컴파일
>   **78소스 오류 0**, 검사기 self-test 주입 오류 1건 탐지, `git diff --check` 통과.
> - **정직한 미완**: `unity_meas` 빌드가 기존 `com.unity.modules.physicscore2d@1.0.0`
>   미해결 의존성에서 멈춰 6종 수치·PNG와 차단 FAIL은 미확인. 원본 `-useHub` Unity와 다른
>   세션의 manifest는 건드리지 않았다. 증거: `output/qa/ashes-to-stars/first_job_probe_{RED,compile,negctrl,광전사}.log`.
> - **다음**: 패키지 의존성 정상화 후 6종 실행 PASS + PNG + 슬롯 차단 FAIL을 닫고 Lv50 2차 각성.

> **이번 이터 결과(코드): 전직 #8 다섯 번째 슬라이스 — 검사 폴백 6종의 고유 전투 분기 연결.**
> - W3 `Job`이 1차 11종을 모두 파싱한다. 광전사=저체력 분노/광역, 궁수=집중/관통, 소환사=
>   소환 추가타/실제 위치교체, 드루이드=자연표식 회복+피해, 주술사=저주스택/공격지연,
>   정령사=공격템포/보호막으로 분기한다. 역할·사거리·스킬 라벨도 같은 런타임 계약을 소비한다.
> - **리뷰 수리**: 광전사 광역이 주대상을 두 번 `KillMob`하던 경로를 제외했고, 도발 시전자를
>   `_party[0]`으로 고정하던 가정을 제거했다. 수호 게이지·최후의 보루는 수호기사에게만 남겨
>   광전사가 탱 역할이어도 수호기사 고유기를 훔치지 않는다.
> - **TDD/검증**: 생산 API 전 SelfCheck를 추가해 컴파일 오류 2건 RED. 최종 정적 컴파일 **75소스
>   오류 0**, 검사기 self-test 주입 오류 1건 탐지, `git diff --check` 통과. 코드 커밋 `8128fbf7`.
> - **정직한 미완**: SelfCheck는 6종의 고유 역할·사거리·메커니즘·스킬 계약을 검증하지만 실제
>   전투 `ForceSkill`/피해/회복/보호막 계측은 아직 없다. `qa_shot.sh party 180`은 오너 `-useHub`
>   Unity PID 43474와 사본 Lockfile 때문에 exit 21. 에디터는 죽이지 않았다. 증거 로그:
>   `output/qa/ashes-to-stars/first_job_archetypes_{RED,compile,negctrl,qa}.log`.
> - **다음 슬라이스**: 정상 Unity 환경에서 6종 각각 슬롯1/2 효과 계측과 화면 확인을 먼저 닫고,
>   그 뒤 Lv50 같은 직업 2차 각성으로 진행한다.

> **이번 이터 결과(코드): 전직 #8 네 번째 슬라이스 — 기본 2스킬/1차 4스킬 전투 경계 연결.**
> - `PartyState.SortieCombatants`가 직업 어댑터 뒤에도 `AdvancementTier`와 `SkillCount`를 보존하고,
>   `BattleScreen → W3Party.ApplyGameParty`가 실제 로스터를 다시 적용한다. 기존 `PartySetup`은 정의만
>   있고 호출 0곳이었는데 이번에 실게임 소비처를 연결했다.
> - 기본직업은 역할별 2개만 실제 분기한다: 탱 도발/방패벽, 딜 강타/집중, 힐 치유/정화,
>   버퍼 고양/쇠약. 1차는 기존 직업 고유 메커니즘+능력의 4개 계약을 유지하며 기본 탱에게
>   1차 전용 `최후의 보루`가 발동하지 않도록 단계 게이트를 추가했다. 조작 버튼은 §5대로 2개 유지한다.
> - **TDD/네거티브**: 생산 코드 전 `SortieCombatants` 부재 컴파일 오류 2건 RED. SelfCheck에 신규
>   기본 5인=Basic/2개, 전직 광전사=First/4개 단언을 추가했다. 최종 정적 컴파일 **75소스 오류 0**,
>   검사기 self-test 주입 오류 1건 탐지, `git diff --check` 통과. 코드 커밋 `42315c7b`.
> - **정직한 잔여 범위**: W3의 원래 1차 전투 아키타입은 5종뿐이라 광전사·궁수·소환사·드루이드·
>   주술사·정령사는 아직 검사 폴백을 쓴다. 다음 슬라이스는 이 6종의 고유 메커니즘/스킬 소비처다.
> - **실행/시각 QA 블로커**: 오너 `-useHub` Unity PID 43474가 원본을, 사본 Lockfile도 점유 상태라
>   `qa_shot.sh party 180`이 exit 21로 중단했다. 에디터는 죽이지 않았다. 정적 증거:
>   `output/qa/ashes-to-stars/advancement_skill_progression_compile.log`와 `_negctrl.log`.

> **이번 이터 결과(코드): 전직 #8 세 번째 슬라이스 — 재료5 + 비살상 역할 시험 + 성공 원자 커밋.**
> - 던전 드랍/가방/결과 화면에 `AdvancementMaterial`을 연결했다. 일반 던전 보스 35%, 레이드급
>   던전 100%는 확정 절대값이 없는 **프로토타입 검증값**이며, 1차 전직 요구량 5개는 §18-6 확정값이다.
> - Character 화면에서 Lv20·재료5를 확인한 뒤 역할별 3행동 훈련을 수행한다. 캐릭터 영속 ID+단계로
>   3개 패턴을 고정하고, 패턴과 다른 행동은 비살상 실패한다. 실패·중단은 재료/목숨/직업 상태 불변이다.
> - 성공 확인만 재료5와 직업/단계를 **PlayerPrefs.Save 1회**로 원자 커밋한다. 저장 실패 주입 시 옛
>   로스터를 다시 스테이징하고 재료를 복원하며, 로스터/가방 캐시를 모두 버린 재기동 검사로 원상복구를 단언한다.
> - 9번째 저장 필드에 영속 캐릭터 ID를 추가했다. 기존 6~8필드 저장은 이름·직업·인덱스의 결정 ID로
>   하위호환하고, 기존 `LifeItem` 숫자값은 명시해 `SpecialJobToken=3` 호환을 보존했다.
> - **TDD/네거티브**: 구현 전 새 API 부재 컴파일 오류 20건 RED. 이후 오답 행동/목표 미달/중단/저장
>   실패 주입 모두 0소비·상태 불변, 성공만 5소비, 재입장 패턴 동일을 SelfCheck에 추가. 최종 정적 컴파일
>   **75소스 오류 0**, 검사기 self-test 주입 오류 1건 탐지, `git diff --check` 통과. 코드 커밋 `93b9fe39`.
> - **실행/시각 QA 블로커**: `unity_meas`는 라이선스 통과 뒤 다른 세션이 수정 중인 manifest의
>   `com.unity.modules.physicscore2d@1.0.0 cannot be found`에서 중단했다. 증거:
>   `output/qa/ashes-to-stars/first_advancement_material_trial_selfcheck.log`. 따라서 SelfCheck 실행 PASS와
>   Character 스크린샷은 미확인이다. 남의 manifest/씬 변경은 건드리지 않았다.
> - **다음 슬라이스**: 기본 2개 → 1차 4개 스킬의 실제 전투 반영. 그 뒤 Lv50 2차 각성으로 진행한다.

> **이번 이터 결과(코드): 전직 #8 두 번째 슬라이스 — Lv20 역할별 1차 직업 선택·저장 연결.**
> - `LifeSystem.FirstAdvancementOptions`가 기획서 확정표 그대로 탱 2(수호기사·광전사) / 딜 4
>   (검사·궁수·마법사·소환사) / 힐 2(사제·드루이드) / 버퍼 3(음유시인·주술사·정령사)를 반환한다.
> - `TryFirstAdvance`는 Lv20·Basic·생존·실제 로스터 소속·역할 일치만 허용하고, 성공 시 직업명과
>   `AdvancementTier.First`를 함께 즉시 저장한다. Lv19·타 역할·삭제·비로스터·반복 전직은 상태 불변으로 거부한다.
> - Character 상세를 정보/전직선택 두 상태로 나눠 최대 4개 선택지와 취소가 row 0~5 안에 모두 보이게 했다.
>   Lv20 미만·삭제 캐릭터는 정직한 잠김 표시다. 재료·비살상 시험·스킬 2→4는 다음 슬라이스로 남겼다.
> - **TDD/네거티브**: 생산 코드 전 새 API 부재 컴파일 오류 7건으로 RED 확인. SelfCheck는 11개 이름,
>   Lv19/타 역할/삭제/비로스터/반복 거부, 성공 전환, 재기동 저장을 단언한다. 컴파일 검사기 `--self-test`가
>   고의 오류 1건을 탐지했고, 최신 본 검사 **72소스 오류 0**, `git diff --check` 통과.
> - **리뷰·커밋**: 1차 리뷰가 선택지 화면 잘림을 발견해 별도 선택 상태로 수리했고 재리뷰 Critical/Important 0.
>   코드 3파일 커밋 `76c6a80b`.
> - **실행/시각 QA 블로커**: `unity_meas` SelfCheck는 Unity Licensing Client `505 Unsupported protocol version
>   '1.18.1'`에서 멈춰 실행되지 않았다. 증거: `output/qa/ashes-to-stars/first_advancement_selfcheck.log`.
>   `qa_shot.sh go:Character`도 그 배치 PID가 사본 락을 잡아 중단했고, 이번 세션이 띄운 PID만 종료했다.
>   다음 정상 Unity GUI/라이선스 환경에서 SelfCheck 후 `GAME_START=go:Character` 캡처로 실제 선택 화면을 확인한다.
> - **다음 슬라이스**: 전직 재료 `LifeItem`·드랍표와 비살상 전직 시험을 성공 확인 시 소비하도록 연결한다.

> **이번 이터 결과(코드): 전직 #8의 첫 필수 슬라이스 — 신규 로스터를 기본직업 4종으로 복구.**
> - `CharacterRecord.Advancement`(`Basic/First/Second`)를 8번째 저장 필드로 추가하고,
>   신규 5인 프로토타입 로스터를 `탱·딜·딜·힐·버퍼` 모두 `Basic`으로 생성한다.
> - 기존 6/7필드 저장은 직업명이 11종 1차 JobDef와 일치하면 `First`로 추론해
>   오너 지시의 “기존 1차 저장은 1차 완료로 보존”을 지킨다. 경험치도 그대로 복원한다.
> - 기본직업 문자열이 `W3Party.Job` enum 파싱에서 탈락해 파티 0명이 되지 않도록
>   `PartyState` 경계에서 기존 전투 아키타입(수호기사·검사·사제·음유시인)으로 어댑트한다.
>   초상화도 기본직업 4종에 대한 기존 아틀라스 매핑을 제공한다.
> - **TDD/NERGATIVE**: SelfCheck에 신규 직업명·단계·재기동·구저장·1차 보존·전투 어댑터를 먼저 추가했고,
>   생산 코드 전 `Advancement` 부재 6건으로 RED를 확인. 구현 후 `game_compile_check` 70소스 오류 0.
>   이 생성·추론·어댑터 중 하나를 되돌리면 SelfCheck 단언이 깨진다.
> - **검증·커밋**: `game_compile_check --self-test`가 고의 오류 1건을 탐지했고, 본 검사 **72소스 오류 0**.
>   지정 코드 4파일만 커밋 `ab20a005`(테스트 포함). 생성·추론·어댑터 중 하나를 되돌리면 SelfCheck 단언이 깨진다.
> - **실행 QA 블로커**: `unity_meas` SelfCheck는 Unity Licensing Client가 `505 Unsupported protocol version
>   '1.18.1'` 및 `com.unity.editor.headless` 미발견으로 재접속을 반복해 중단했다. 증거:
>   `output/qa/ashes-to-stars/life_system_selfcheck.log`. 실행 PASS·파티 스크린샷은 정직하게 미확인이다.
>   다음 정상 Unity GUI/라이선스 환경에서 SelfCheck→`qa_shot.sh party`를 먼저 재실행한다.
> - **다음 슬라이스**: Lv20 기본직업별 1차 선택지(2/4/2/3)·전직 상태 전환. 재료·시험·스킬 2→4는 그 다음 독립 슬라이스.

> **이번 이터 결과(코드): §8 탑 등반 — "다음 층 도전"을 이겨도 층이 안 올랐다. 진행도 배선.**
> 「대기하지 마라」 지침대로 큐/INBOX/락 상태 재확인 → 여전히 전면 막힘(전직=오너 A/B/C 미결,
> combat=대화세션 `W3Party.cs` 22:55 편집, GUI=오너 `-useHub` 락 PID 46914, 아트 생성 마커 없음).
> 원장·코드를 훑어 **✅ 확정인데 소비처/배선이 어긋난 것**을 찾음 — **§8 탑 층 진행이 그것**이었다.
> - **실측 근거(거짓 버튼)**: `TowerScreen`의 "다음 층 도전"(`BattleKind.잡몹웨이브`, 라벨="다음 층")을
>   버텨 살아남아도 **층이 안 올랐다.** `GameState.ClearFloor`는 도입 이래 **보스 격파(`BattleScreen.cs:56`
>   OnBossDefeated) 경로에서만** 불렸고, 층수의 대부분인 일반 층을 처리하는 `OnBattleEnd(survived)`는
>   ClearFloor를 **안 불렀다**(그냥 "생존 N초"만 표시). 즉 §8 벽 콘텐츠·§10-6 티어 상승·**직전 이터가
>   짠 §15 침략 30층 게이트**가 일반 등반으로는 영영 안 열렸다("눌러도 규칙 무시하는 거짓말" 계열).
>   `BattleScreen.cs:54-55` 주석이 그 원칙을 적어놓고도 보스 경로에만 적용한 상태였다.
> - **한 것(4파일)**: ①`GameFlow.cs` 순수 판정 `IsTowerFloorClear(survived,inDungeon,returnScene,kind)`
>   =`survived && !inDungeon && returnScene==Tower && kind==잡몹웨이브`(보스 제외 → 이중 상승 방지) ②`BattleScreen.cs`
>   OnBattleEnd 생존 분기에서 참이면 `ClearFloor(BossFloor)`(BossFloor엔 입장 시 TowerFloor가 담김) +
>   "N층 돌파 · 다음 M층" 요약 ③`GameState.cs` `SetTowerFloorForTest`(단조증가라 자가검사가 층 복원할 수단
>   부재) ④`LifeSystemSelfCheck` ⑪블록.
> - **왜 오펀/추측이 아닌가**: `ClearFloor`·`TowerFloor`는 이미 존재하고 전방위로 소비된다(Tier·침략 게이트·
>   경매장 게이트·난이도). 새 시스템 0. 보스 경로가 이미 같은 함수를 쓰던 **내부 선례**를 일반 층에 확장한 것.
>   "다음 층 도전"이 실제로 층을 올리는 것은 §8·§1496(프로토타입 "5층까지+5층 보스") 확정 모델과 일치.
> - **검증(헤드리스)**: `game_compile_check` **PASS(오류 0, 60소스)** · `game_asset_names` **✅ 이상 없음**.
>   **불변식(SelfCheck ⑪)**: 탑 잡몹웨이브 생존→참 · 보스전→거짓(이중상승 방지) · 필드/전멸/던전→거짓 ·
>   ClearFloor(1)→2층 · 지난 층 재도전은 진행도 유지(단조) · 재기동 후 유지. ⚠️**배치 SelfCheck·인게임은
>   오너 `-useHub` 락으로 GUI/빌드 세션 대기**(표준 인계). 실행: `Unity -batchmode -quit -projectPath <프로젝트>
>   -executeMethod AshesToStars.LifeSystemSelfCheck.Run`(⑪ 확인).
> - **네거티브 컨트롤**: BattleScreen의 `ClearFloor(BossFloor)` 호출을 지우면 "다음 층 도전"을 이겨도 층이
>   그대로 → §15 게이트·티어가 안 열리는 회귀. `IsTowerFloorClear`에서 `kind==잡몹웨이브` 조건을 빼면 보스전이
>   이중으로 층을 올린다(SelfCheck ⑪ 두 번째 단언 FAIL).
>
> **이전 이터 결과(코드): §15 침략 「탑 30층 해금」 게이트 — 경매장과 대칭을 맞췄다.**
> 「대기하지 마라」 지침대로 큐/INBOX/락 상태를 전수 재확인 → 여전히 전면 막힘(전직=오너 A/B/C
> 미결, combat=대화세션 `W3Party.cs` 22:55 편집 중, GUI=오너 `-useHub` 락 PID 46914, 아트=
> `spec_char_mage.json` 생성 중 pid 66926 생존). 원장을 훑어 **✅ 확정인데 소비처/게이트가 어긋난 것**을 찾음.
> - **실측 근거(대칭 붕괴)**: §15 ✅ "탑 30층 이상 등반 시 해금"인데 `WorldMapScreen.cs:21` 침략 버튼은
>   **무게이트 상시 활성**(층 1에서도 GoBattle 발동)이었다. 그런데 **경매장은 이미 게이트가 있다** —
>   `EstateScreen.cs:66`이 "탑 30층을 달성해야 열린다(현재 N층)"로 막는다. `SceneStructureBuilder.cs:156`도
>   "30층 돌파 → 침략·**경매장 동시 해금**"이라 명시. 즉 **둘이 동시 해금이어야 하는데 침략만 뚫려 있던**
>   비대칭(이 저장소가 반복 경고한 "안전장치 비대칭"·"눌러도 규칙 무시하는 거짓말" 계열).
> - **한 것(1파일)**: `WorldMapScreen.cs` — `InvasionUnlockFloor=30` 상수 + `GameState.TowerFloor>=30`일 때만
>   침략 라이브 버튼, 미만이면 `Locked("탑 30층 달성 시 해금(현재 N층) — 30층 미만은 초보 보호")`.
>   성계이동·랭킹은 이미 Locked라 그대로. **소비처 실재**(기존 `GameState.TowerFloor`·기존 침략 배틀 재사용,
>   신규 시스템·오펀 0). 경매장 게이트와 동일 어법.
> - **왜 이것이 오펀/추측이 아닌가**: 30층 게이트는 §15 ✅ 확정값이고, **경매장이 이미 같은 패턴으로 구현**돼
>   있어 침략에 미러링한 것뿐이다(내부 선례 존재 → 지어낸 임계값 아님). 프로토타입 QA는 sim을 직접 몰아
>   이 메뉴 버튼을 안 쓰므로 무영향.
> - **검증(헤드리스)**: `game_compile_check` **PASS(오류 0, 60소스)** · `game_asset_names` **✅ 이상 없음**
>   (신규 자산 0, 1파일 편집). 게이트 로직은 순수 불변식(`TowerFloor>=30`) — 층 29=Locked, 30=라이브.
>   ⚠️ **인게임 화면 확인만 GUI세션 대기**(오너 useHub 락) — 표준 인계.
> - **네거티브 컨트롤**: `>=30` 분기를 지우고 무조건 라이브 Row로 되돌리면 층 1에서 침략이 발동해
>   §15 30층 게이트가 회귀(경매장과 다시 비대칭). 즉 되돌리면 거짓말이 되돌아온다.
> - **다음 후보(loop-owned·소비처 실재)가 사실상 고갈**: 이번 전수 조사로 남은 ✅ 갭은 전부 상류 시스템
>   부재로 막힘을 재확인 — 대출 연체/파산(경매장·침략 PvP·영지레벨 필요) · §18-2 리롤 누진비용
>   (`GetRerollCostMultiplier` 0소비처지만 RaidSpawn은 **의도적 1회용**이라 재입장 리롤 표면 자체가 없다,
>   §403 하위레이드 재도전 콘텐츠 미존재) · 종족 수치 §18-9(CharacterRecord에 Race 필드 없음 — 새 정체성
>   차원 도입은 §107·전직 오너 결정과 얽힘) · PvP 12h 회복(§15, isPvp=true 호출부 0곳) · 방치 수익
>   (§6에서 💡, 미확정). combat/GUI/전직 블로커가 풀리기 전엔 원장에서 **깨끗한 새 슬라이스가 거의 없다**.

> **이전 이터 결과(코드): §12·§18-5 대출 — 코드에 loan/debt 참조 0곳이던 경제 키스톤을 배선.**
> 「대기하지 마라」 지침대로 큐/INBOX 재확인 → 전면 막힘(전직=오너 결정 대기, combat=대화세션 활성
> W3Party 22:40 편집, GUI=오너 useHub 락). 원장을 훑어 **✅ 확정인데 런타임 소비처 0곳**을 찾음:
> **대출(§12·§18-5)**이 그것 — `grep -rniE "대출|loan|debt"` 전 코드 **0곳**. "골드=목숨"이라 대출은
> 곧 목숨을 빌리는 것(§12), 기획서가 "게임에서 가장 극적인 순간"이라 부른 시스템인데 통째로 없었다.
> - **소비처가 실재하는 부분만 정직하게 슬라이스**(오펀 방지): ①**자동** — `GameState.Earn`에 수입 50%
>   자동상환(§18-5). 전투 보상이 이 경로로 들어오므로 **상시 작동하는 진짜 소비처**다. ②**수동** —
>   `TowerScreen` 「골드 부족」 화면에 "대출받고 입장"(§12 "빚내서 다음 판에"). **건물을 안 늘려** §13-2
>   ("건물 7종 여기서 늘리지 않는다") 준수 — 기존 경고 화면에 붙였다.
> - **한 것(4파일)**: ①`Economy.cs` 순수계산(`LoanLimitCopper`·`AccrueLoan`·상수 4종) ②`GameState.cs`
>   부채 상태 3키(PlayerPrefs, 하위호환 0 기본값)·`Borrow/Repay/AccrueLoan`(시각 주입형+실시간 래퍼)·
>   `Earn` 자동상환·`ForgetInMemoryForTest` ③`TowerScreen.cs` 대출 패널+pendingCost 보존 ④`LifeSystemSelfCheck` ⑩.
> - **정직 유보(오펀 방지)**: §12 연체 제재(경매장 등록금지·침략불가)·3회 연체 파산(영지건물 강등)은
>   **그 제재 대상 시스템(경매장·침략·영지레벨)이 없어** 미배선. 지금 넣으면 "정의만 있고 소비처 0곳"
>   오펀이 된다 — 그 시스템이 생길 때 함께 배선. `due_at`은 저장만 하고 제재 escalation은 안 한다.
> - **설계 결함 1건 예방**: 순자산을 지갑 전액으로 잡으면 **대출→순자산↑→한도↑→대출**의 무한 피드백
>   루프가 생긴다(계산 검증 중 발견). 순자산 = 지갑−부채(net worth)로 수정 → 빌린 돈은 한도를 안 늘린다.
> - **검증**: `game_compile_check` **PASS(오류 0, 60소스)**. `game_asset_names` 경고는 보스 meta 4개
>   (전투 세션 소유, 내 것 아님 — 안 건드림). **수치 근거(결정론, SelfCheck ⑩)**: 보유10골드(100000쿠퍼)·T1
>   → 한도=순자산30%=30000 · 대출30000→지갑130000/부채30000 · 한도초과1쿠퍼 거부 · Earn(1000)→50%
>   자동상환→부채29500/지갑130500 · 72h복리>단리(27200) · 재기동 후 부채 유지 · 수동상환 후 0.
> - **⚠️ 배치 SelfCheck·인게임은 오너 `-useHub` 에디터 락으로 GUI/빌드 세션 대기**(표준 인계).
>   실행: `Unity -batchmode -quit -projectPath <프로젝트> -executeMethod AshesToStars.LifeSystemSelfCheck.Run`(⑩ 확인).
> - **네거티브 컨트롤**: `Earn` 자동상환 블록을 되돌리면 부채가 안 줄고 SelfCheck ⑩ FAIL · `Borrow`
>   한도체크를 지우면 한도초과 대출이 통과 · TowerScreen 대출 패널을 되돌리면 loan borrow 소비처가 0곳 회귀.
> - **다음 후보(loop-owned·소비처 0곳)**: 대출 연체/파산은 경매장·침략이 선행. 나머지 0-소비처 ✅는
>   전직 위에 얹혀 막힘. combat/GUI 락 상태를 재확인하고 여전히 막힘이면 원장 재훑기.

> **이전 이터 결과(코드): §4 긴급 탈출 — `귀환의 두루마리`에 첫 소비처를 붙였다(소비처 0곳 함정 해소).**
> 「대기하지 마라」 지침대로 큐/INBOX 전수 재확인 → 여전히 전면 막힘(전직=오너 결정 대기, combat/GUI=타 세션·락)이라
> **원장을 훑어 ✅ 확정인데 런타임 소비처 0곳**인 것을 찾았다. **§4 긴급 탈출(`ScrollOfReturn`)이 그것**이었다:
> - **실측 근거**: `Economy`에 `ScrollOfReturn` 정의·드랍표(3~5%)·상한(5)까지 있는데 `GameState.Consume(ScrollOfReturn)`
>   호출부가 **0곳**(부활초·환생석은 `LifeSystem`이 소비하는데 이것만 소비처 부재). `BattleScreen.cs:244` "후퇴"
>   버튼은 라벨이 "긴급 탈출 아이템(§4)"인데 **아이템을 무시하고 공짜로** `GoBattle(ReturnTo)` 했다 — §4 ✅
>   "희귀·고가 아이템"이 안 쓰이는 반쪽 상태(정확히 이 저장소가 반복 경고한 "정의만 있고 소비처 0곳").
> - **한 것(2파일)**: ①`BattleScreen.cs` "후퇴"를 **두루마리 보유 시에만 활성 + 1개 실제 소모** 후 탈출, **0개면 `Locked`**
>   (부활초·환생석과 동일한 `Bag.GetCount`→`Consume` 패턴). ②`Editor/LifeSystemSelfCheck.cs`에 ⑨ 불변식 추가
>   (초기 0개=희소 · 0개일 때 소모실패=후퇴잠김 · 획득 반영 · 소모 시 실제 차감 · 다 쓰면 재잠김).
> - **소유권/락**: `BattleScreen.cs`는 전투 **메뉴 화면**(sim은 `W3Party`)이라 loop 소유(combat 세션은 W3Party·Boss·Arena·Decor·DebugAutoPilot만). 편집 시각 22:03로 idle 확인.
> - **소프트락 없음(검증)**: 전투는 `W3Party`가 `OnBattleEnd`(파티/잡몹 전멸)로 **스스로 종료**해 Result로 간다 —
>   "후퇴"는 조기 이탈 옵션일 뿐이라 잠가도 화면에 갇히지 않는다. **QA 무영향**: 후퇴는 사람 메뉴 전용,
>   QA 오토파일럿은 sim을 직접 몬다(이 버튼 안 누름).
> - **검증**: `game_asset_names` **✅ 이상 없음**(신규 파일 0 → meta 문제 없음, 기존 2파일 편집). 제출 심볼 전부
>   기존 API(`GameState.Gain/Bag.GetCount/Consume`, `LifeSystem`에서 실사용). **⚠️ 배치 SelfCheck·인게임 화면은
>   오너 `-useHub` 에디터 락(PID 46914)+combat W3Party 편집중(22:32)으로 GUI/빌드 세션 대기** — 표준 인계.
>   실행법: `Unity -batchmode -quit -projectPath <프로젝트> -executeMethod AshesToStars.LifeSystemSelfCheck.Run`(⑨ 확인).
> - **네거티브 컨트롤**: `BattleScreen` 편집을 되돌리면 후퇴가 다시 공짜가 되고 `ScrollOfReturn`은 소비처 0곳으로 회귀 ·
>   ⑨ 블록을 되돌리면 불변식 가드가 사라진다.
> - **⚠️ 미완(정직, combat 후속)**: §4 ✅ "**캐스팅 6초·피격 시 취소**"는 전투 시뮬(`W3Party`) 타이밍이라 미구현 —
>   지금은 **즉시 소모형**이다. 아래 큐에 combat-owned 후속(#10)으로 남김. 즉시형도 공짜형보다 정직하다(아이템을 실제 쓴다).

> **이전 이터 결과(감사·상신): 클린한 ✅ 코드 과제가 남아 있지 않음을 전수 확인 → 근본 블로커를 오너에게 상신.**
> 시작 시 큐/INBOX/기획서를 전수로 훑고 각 후보를 코드로 실측한 결과, **남은 ✅ 갭이 전부 막혀 있다**:
> - **전직·합성·특수직업·전직재료·증표** = 설계 충돌로 막힘(아래). 이 저장소가 반복 경고한
>   "정의만 있고 소비처 0곳"을 새로 만들지 않으려면 지금 구현하면 안 된다(전부 전직 위에 얹혀 있음).
> - **대시·구르기·#9 성장→전투력·보스 HP/장판/힐·스킬슬롯** = combat(`W3Party` 등) = 대화 세션 소유.
>   실측: 이 이터 중 대화 세션이 `2eee4306 돌진형 신설(§10-2)`을 커밋(W3Party 22:20 편집) — 활성.
> - **인게임 렌더 확인**(#1 hunt 등) = 오너 `-useHub` 에디터 락으로 배치빌드 불가.
> - **영지 건물 3종·경매장·수비대** = 소비 시스템 부재(ceb522c8에서 이미 정직 잠금). **§6 자동화
>   스케줄러** = ✅ 핵심("일정 지시 가능")은 얇고 "일과표"는 전부 💡(설계 미정) — 지어내면 안 됨.
> - **이미 완료 확인**(중복 착수 방지): 드랍 시스템(BattleScreen→RollBattleDrops→Gain→ResultScreen 라이브),
>   진입 골드비용(§18-2, FieldScreen/TowerScreen, 취소버그도 이미 수리), 레이드급 랜덤출현(RaidSpawn),
>   성장(§18-6), 전투 스타일, RaceDef, 잡몹 상한 500 — 전부 살아 있음.
> - **베이스라인**: `game_compile_check` **PASS**(60소스·오류 0). 트리 건강.
>
> **왜 코드 커밋 없이 상신만 했나(정직)**: 「대기하지 마라」 지침은 "✅인데 소비처 0곳을 찾아 구현"이나,
> 지금 남은 0-소비처 ✅는 **소비처가 없는 이유 자체가 설계/소유 블로커**라 구현하면 anti-pattern
> 오펀을 새로 만든다. 그 충돌의 정답은 INBOX 규칙대로 "멈추고 질문"이다. 저위험·저가치 코드 변경을
> 커밋을 위해 억지로 만드는 것(예: 소비처 없는 증표 드랍층 §566 수정 — §10-8 리롤억제 자가검사 불변식에
> 손대는 위험)은 하지 않았다. 규율 있는 무행동 > 무규율 행동.
>
> **🔴 상신한 블로커(INBOX 최상단 "오너 결정 필요")**: **전직 base-vs-1차 설계 충돌.** 코드는 캐릭터를
> **1차 전직 이름으로 곧바로 생성**(`LifeSystem.cs:93-97` 수호기사·마법사·검사·사제·음유시인)하고
> `Data/Jobs/`에 **11종 1차 JobDef 전부 존재**, 그런데 기획서 §73/§109는 **기본직업(탱/딜/힐/버퍼)→1차
> 세분화**를 요구한다. `RoleId{탱딜힐버퍼}`는 분류용일 뿐 그 상태로 생성되는 캐릭터는 0명. 전직 전체가
> 이 미해결 단계 위에 얹혀 막혀 있다(STATUS가 ~4이터 맴돈 근본 원인).
> **추천 옵션 A**(현 캐릭 = 1차 확정) 채택 시 다음 이터가 **2차 전직(§77: 같은 직업 심화·분기 없음·초필)**을
> 바로 구현할 수 있다 — base→1차 애매성을 안 건드리는 클린 슬라이스. 오너 한 줄 결정 대기.
>
> **다음 세션 지침**: ①INBOX 최상단에 오너 답(A/B/C)이 있으면 그걸 집는다(A면 2차 전직 데이터 레이어).
> ②없으면 combat/GUI 락 상태를 재확인하고, 여전히 전면 막힘이면 **아트 트랙**(새 spec이 있거나 오너
> 「몬스터 특색」 후속 요청)이나 이 상신을 재확인만 한다 — 억지 코드 커밋은 만들지 마라.

> **이전 이터 결과(검증): 오너 「몬스터를 더 특색 있게」 통과 기준을 스프라이트 레벨에서 충족.**
> 시작 시 큐/INBOX를 훑으니 상단이 전부 막힘이었다: 아트 생성은 free(higgsfield `waiting` 0)지만
> 반입 대상 아트(#2 보스·#3 영지건물)는 **소비처 0곳**으로 막힘, 코드 트랙 #8 전직은 combat(스킬 2→4)·
> Economy 드랍표 재설계·기본직업 재구조화가 얽혀 한 이터에 깨끗이 못 끝냄(전직재료는 enum에도 없음, 주석뿐).
> 그래서 「대기하지 마라」 지침대로 **지금 완결 가능하고 loop 소유이며 유니티 없이 검증되는** 것을 잡았다:
> 오너 「몬스터 특색」 요청(대기 중 INBOX)의 **통과 기준 = "AI 4종이 서로 다르게 읽힌다"**를 실제로 증명.
> - **왜 지금·왜 이것**: 4계열(chaser·charger·ranged·swarmer)은 이미 재생성·반입 완료(#1)돼 있고 개별
>   Read 검증도 끝났지만, 개별 샷은 **넷이 서로 갈리는가**(통과 기준의 실제 문장)를 증명하지 못한다.
>   4종을 한 판에 나란히 놓은 매트릭스가 그 유일한 검증이다.
> - **한 것**: `scratchpad/mk_montage.py`로 `output/qa/ashes-to-stars/shots/mob_family_matrix.png` 생성
>   (행=계열, 열=idle/walk/attack/death, 맨 위 캐릭터 세계관 대조·맨 아래 옛 mob01 네거티브). **Read 육안 판정**:
>   추격=낮은 4족 늑대(앞기움) · 돌진=육중 뿔짐승(attack_00 wind-up 텔레그래프 §10-2) · 원거리=직립 활든 궁수 ·
>   포위=낮고 넓은 다족 거미 → **넷 완전 상호구분** ✅. 4계열 전부 무채색(색조=런타임 FamilyTint, §0-B) ✅.
>   캐릭터와 아웃라인·셀셰이딩 동일 세계관 ✅.
> - **네거티브 컨트롤**: 옛 `mob01`(분홍 3D톤 'P' 플레이스홀더)을 같은 판에 나란히 두면 톤이 눈에 띄게 갈림 ✅.
> - **렌더 배선 확인(코드)**: `SpriteBank.MobAnim(kind)`가 `MOB_DIRS[kind]`로 4계열을 정확히 그린다
>   (charger kind=2→`mob_charger` ✅, W3Party.MobSpriteKind). 즉 화면 배선은 이미 옳다.
> - **⚠️ 미완(정직)**: **인게임 hunt 렌더 확인만** 남음 — 오너 `-useHub` 에디터 락(PID 46914)으로 이 세션은
>   유니티 배치빌드 불가. GUI/빌드 세션이 `qa_shot.sh hunt`로 최종 눈확인하면 완전 종결. INBOX 항목은
>   그래서 「처리됨」이 아니라 진행 마커로 남겼다(자기 통과 기준이 hunt 화면을 요구하므로 거짓 완료 금지).
> - **⚠️ 잠재 결함(화면 무영향·기록만)**: `ProjectSetup.cs:220` 돌진형 `MobDef.스프라이트="mob_chaser_0"`가
>   늑대 정적키를 가리킴. 단 `MobDef`는 런타임 소비처 0곳(dead field)이고 `mob_charger_0` 정적키가
>   `SpriteBank.MOB_KEYS`에 미등록이라 지금 바꾸면 null이 된다 → **반쪽 수정 금지**, MobDef에 소비처가
>   생길 때 정적키 등록과 함께 정정. (이 저장소가 반복 경고한 "정의만 있고 소비처 0곳"의 예방적 기록.)

> **이전 이터 결과(코드): 캐릭터 성장(레벨·경험치) 반입 — §3 경험치 분배 + §18-6 레벨 곡선.**
> 큐 상단이 전부 막힘/GUI대기라(§#1 인게임hunt=오너 useHub 에디터 락, #2b·combat=대화 세션이
> DebugAutoPilot 21:46·W3Party 21:33 편집 중, #4 영지=소비시스템 부재) 「대기하지 마라」 지침대로
> 원장을 훑어 **✅ 확정인데 소비처 0곳**인 것을 찾았다. **성장(레벨업)이 그 키스톤**이었다:
> `CharacterRecord.Level`이 생성값 그대로 굳어 있었고(증가 코드 0곳), 그 위에 얹힌 전직(Lv20 전제)·
> 합성(1차 전직 이상 재료 전제)이 연쇄로 막혀 있었다.
> - **왜 지금 가능했나(핵심)**: XP 소스가 전투라 combat(대화 세션)을 건드려야 하는 줄 알았으나,
>   `BattleScreen.CalculateVictoryReward`가 **골드를 이미 승리 해소 시 1회 지급**(`:160`)하고
>   `BattleRewardInfo`가 그걸 실어 `ResultScreen`이 표시하는 구조였다. 그래서 **골드 바로 옆에
>   XP 지급 1줄**을 넣어 combat 시뮬(`W3Party`)은 일절 안 건드리고 성장을 붙였다. 전 경로가
>   루프 소유·정지 파일(LifeSystem 20:36·BattleScreen/ResultScreen Aug14·CharacterScreen 직전 루프).
> - **한 것**(6파일 +168): ①`CharacterRecord.Exp` 필드 + 직렬화 7번째 필드(옛 6필드 저장은 0으로
>   읽어 하위호환) ②`LifeSystem.ExpToNext(Lv)=100×Lv^2.2`·`AddExp`(만렙 100 상한)·`AwardBattleExp`
>   (출전 파티=`PartyState.Slots`에 **레벨 비례 분배**, 총합 보존) ③BattleScreen 골드 옆 1회 지급
>   ④ResultScreen "📈 경험치" 표시(순수 표시, IMGUI 프레임 반복에도 지급은 BattleScreen 1회라 안전)
>   ⑤CharacterScreen에 Lv·EXP 진척 표기 + 부제 정직화(성장은 이제 됨, 전직·합성은 잠김 유지)
>   ⑥`LifeSystemSelfCheck` ⑧블록(곡선·레벨업·상한·분배총합보존·재기동 유지).
> - **검증(헤드리스)**: `game_compile_check` **PASS(오류 0)** · `game_asset_names` **✅ 이상 없음**(새 파일
>   0 → meta 문제 없음). **수치 근거(결정론)**: ExpToNext 1=100·2=459·3=1121·10=15848·20=72822·99=2456956;
>   총100 XP·출전5인 Lv1 → 각 20, 합계 100(보존). **네거티브 컨트롤**: `ExpToNext`를 상수로 바꾸거나
>   `AddExp`를 no-op으로 되돌리면 레벨이 안 오르고 SelfCheck ⑧ FAIL·CharacterScreen Lv 고정.
> - **⚠️ 미완(정직)**: 배치 SelfCheck 실행·인게임 화면 확인은 **오너 useHub 에디터 락으로 GUI/빌드
>   세션 대기**(이 세션은 유니티 런타임 접근 불가 — 이 저장소의 Unity락 표준 인계). **절대 총량
>   `battleExp = tierRevenue×100`은 프로토타입 기준값**이다: §18-6 육성시간 앵커(Lv20≈2h) 보정은
>   전투 빈도 데이터가 필요해 유보. ✅ 확정분(분배 레벨비례·곡선 100×Lv^2.2)은 정확하다.
> - **파생 해금**: 전직(§3, Lv20 전제)·합성(§3, 1차 전직 이상 재료)이 "불가능"에서 "도달 가능하나
>   미구현"으로 이동했다 — CharacterScreen 잠김은 유지(정직). 다음 후보는 **전직 시스템**이다.

> **이전 이터 결과(코드): 영지 대장간·경매장·수비대 진입 화면 정직화 (`ceb522c8`).**
> 시작 시 INBOX 루프 우선순위를 훑어 **셋 다 지금은 막힘/완료**임을 실측 확인 → 대신
> 정직화 한 건을 처리했다. 확인·판정 근거:
> - **INBOX #1 swarmer**: 종결(`428cffb7`).
> - **INBOX #2 보스 반입**: 여전히 「소비처 0곳」으로 막힘 — `out_p6_boss/` 4종 생성완료
>   (마젠타), 그러나 `SpriteBank`에 Boss 필드 없고 `W3Party`가 보스를 안 그린다. 배선 자리는
>   전투 렌더(대화 세션 소유). `art/.generating`의 `spec_p6_boss` 표시는 생성완료된 stale.
> - **INBOX #3 영지 건물 7종 아트**: 「소비처 0곳」으로 막힘 — 원장 §2-6은 아이소메트릭
>   **배치 UI**를 전제하나 `EstateScreen`은 **텍스트 메뉴**다(건물 스프라이트 렌더 0곳).
>   `estate_barrel/crate`도 참조 0곳 orphan. 지금 아트 걸면 보스와 동일한 잡동사니 함정.
>   **아트 생성 안 함**(크레딧 보호).
> - **INBOX A 빈버튼(loop 소유)**: 이미 종결 — FieldScreen 자동화일정·WorldMapScreen
>   성계이동/랭킹 전부 `Locked(...)`로 표시됨(`0eeaddb4`). INBOX 감사표가 낡음. 남은 A는
>   전투 스킬슬롯(W3Party, 대화 세션)뿐.
> - **큐 #3 RaceDef·#5 잡몹 상한: 둘 다 이미 완료** — 실측: `W3Party.cs:336 MAXM=500`(§10-9
>   충족), `W3Party.cs:557-567`이 RaceDef 로드→`_bHp*=체력배율`·`_bSpd*=이속배율` 소비(§3).
>   큐 표가 낡았다(아래 정정). INBOX C가 이미 "살아있음"으로 적었던 것과 일치.
> - **한 것**: `EstateScreen`의 대장간·경매장·수비대 진입이 셋 다 제네릭 "아직 내용이 없다"만
>   떠서 "고장난 게임"으로 읽혔다(캐릭터 화면과 같은 A지침). 영묘가 기존 시스템으로 성립한
>   것과 달리 이 셋은 소비할 시스템 자체가 없어 비어 있음을 **건물별 사유**로 밝혔다(대장간=
>   장비·재료 부재 §11, 경매장=30층 미달+거래서버 부재 §12·현재층 표시, 수비대=침략이 배치
>   미소비 §13-5). **검증**: `game_compile_check` PASS(오류 0). **네거티브 컨트롤**: 되돌리면
>   3건물 전부 제네릭 메시지로 회귀. **인게임 스크린샷(`GAME_START=estate`)은 오너 `-useHub`
>   에디터 락으로 GUI 세션 인계.**
>

> **이번 이터 결과(코드): 캐릭터 화면의 "성장·전직·합성" 거짓 광고 정리 (`da37246d`).**
> INBOX 최우선(작업분담) #1 swarmer는 이미 종결(`428cffb7`). #2 보스 실루엣 반입을 잡으려
> 조사하다 **막힘 원인을 확정**(아래) → 대신 독립 loop-owned 항목을 처리했다.
> - **한 것**: `CharacterScreen`의 부제가 성장·전직·합성 3기능을 약속하는데 **셋 다 시스템이
>   없다**(레벨업·전직 재료·패시브 흡수 미구현 — `CharacterRecord.Level`은 생성값 그대로,
>   증가 코드 0곳 확인). 영지·월드맵은 이미 `Locked`로 정직 표시하는데 캐릭터만 조용히 빼서
>   "고장난 게임"으로 읽혔다(오너 A지침). 부제를 되는 것만 말하도록 고치고 전직·합성을
>   `Locked` 행으로 명시. **검증**: `game_compile_check` 전수 파싱에서 CharacterScreen 오류 0
>   (유일 오류는 대화 세션 WIP인 `DebugAutoPilot.cs:209`, 내 변경과 무관). **네거티브 컨트롤**:
>   되돌리면 부제가 다시 없는 3기능을 광고하고 잠김 표시가 사라진다. **인게임 스크린샷
>   (`GAME_START=go:Character`)은 오너 `-useHub` 에디터 락으로 GUI 세션 인계.**
>
> **🔴 #2 보스 실루엣 반입은 「막힘」이다 — 반입할 소비처가 아예 없다(원인 확정).**
> `art/out_p6_boss/`에 4종(brute·serpent·wraith·construct, 마젠타 배경, 크로마키 필요) 생성 완료.
> 그런데 **게임 어디에도 보스 스프라이트를 그리는 코드가 없다**: ①`SpriteBank`에 `Boss` 필드 없음
> (index 5 `boss_0`은 **Projectile로 오용** 중, `ProjectSetup.cs:361`) ②`BossBattle.cs`는 순수
> 데이터(HP·페이즈, SpriteRenderer 0곳) ③`W3Party`는 파티·잡몹·정예만 그리고 보스 실루엣은 안 그린다.
> 즉 지금 반입하면 `game_asset_names.py`가 **잡동사니(참조 0곳)로 FAIL**한다 — 이 저장소가 반복해서
> 경고한 "정의만 있고 소비처 0곳" 함정. **소비처 배선은 `W3Party`/`SpriteBank`(전투 렌더)에 있어야
> 하는데 지금 대화 세션이 그 파일들을 실시간 편집 중**(W3Party 21:22, DebugAutoPilot 21:33 = dash
> 계측 `AiDashUsesOnActive` 추가 WIP로 트리가 컴파일 안 됨). **결론: #2는 대화 세션이 보스 시각
> 시스템을 W3Party에 넣은 뒤에야 반입 가능.** 그 전에는 크로마키/반입해도 잡동사니가 된다. (INBOX에
> 이 발견을 요약해 둠 — 오너 판단 필요: 보스를 화면에 그릴 자리를 W3Party에 만들지 결정.)

> **이번 이터 결과(아트): 큐 #1 「몹 실루엣 재생성」 4/4 완료 — swarmer✅ 반입.**
> 직전 이터가 21:12 분리세션(pid=54305)으로 걸어둔 swarmer 생성이 **완료돼 있었다**: `out_p2/
> sheet_mob_swarmer2_A.png`·`_B.png`(둘 다 21:13, `/tmp/gen_swarmer.log`에 두 시트 ✅), 활성
> higgsfield 프로세스 0 → 이중생성 위험 없음. 이 세션이 **처리·반입**:
> ①두 시트 검은 격자선 → `wipe_gridlines`(A 행9·열20, B 행22·열34 덮음) ②`split_ai_sheet`
> **고정격자**(A 5×2=idle×4·walk×6). **⚠️ B는 spec이 4×3(12셀)을 요청했으나 higgsfield가
> 4×4(16셀)로 생성** — 실측(896/4=224 정수, 896/3=298.67 비정수) + 16셀 라벨 몽타주로 확정.
> 4×4를 `attack_00..03(row0)·hurt_00..03(row1)·skip×4(row2 중복 중립포즈)·death_00..03(row3)`로
> 매핑, **row2 4셀은 skip으로 버림** ③`align_frames`로 22장 공통 222×122·바닥정렬
> ④death 4장에 남은 격자선 파편(상단 2~3px 분리 밴드) — 바닥앵커 본체와 떨어진 상단 섬만 제거
> (idle/hurt 등 단일밴드 프레임 무영향, 4장만 정리) ⑤`mob_swarmer/` **동일 파일명 덮어쓰기**
> (dest+.meta 존재 단언 후 copy → **GUID 보존, git status png 22개만 M·meta 0**) ⑥`game_asset_names.py`
> **✅ 이상 없음**. **아트 육안검증**(`output/qa/ashes-to-stars/shots/swarmer_frames_montage.png`):
> 낮고 넓게 벌어진 다족 벌레형(§0-B 포위형), attack_02 물기 임팩트 별, death_01 뒤집혀 다리
> 위로(죽은 벌레), 무채색 셀셰이딩 — 캐릭터·charger/chaser(4족)·ranged(2족직립)와 한눈에 갈림.
> **네거티브 컨트롤**: 옛 swarmer 아트로 되돌리면 톤/실루엣이 갈림 → `git checkout HEAD -- .../mob_swarmer`(반입 전 커밋).
> ⚠️ **인게임 hunt 확인만 미완**(charger·ranged와 동일 벽): 원본 unity는 오너 `-useHub` 에디터 락
> (PID 46914, 죽이지 않음), unity_meas는 다른 배치빌드 진행중(PID 55542, 21:22) + swarmer 미동기.
> **스프라이트 자산 자체는 검증됨. 다음 GUI/빌드 세션이 `qa_shot.sh hunt`로 인게임 배치만** 확인하면
> #1 완전 종결. **큐 #1 4계열(chaser·charger·ranged·swarmer) 반입 전부 끝 — 다음은 위 「최우선」
> 코드 트랙**(INBOX 「📋 기획서↔코드 대조」의 A 빈버튼/B 대시·구르기).
> **⚠️ combat 파일 만지기 전 `stat -f %Sm W3Party.cs`**(이번 확인 시 21:07 = 대화세션 최근 편집).

> **이전 이터 결과(아트): 큐 #1 마지막 계열 swarmer 생성 착수 — 아트 막힘 완전 해소.**
> INBOX ⭐가 「아트 서버가 waiting 적체」라 했으나 **이 시각 실측으로 풀렸다**: `higgsfield
> generate list` 최근 20건 **전부 completed·waiting 0**, 계정 964크레딧·plus. swarmer만
> A/B 시트 미생성이라(`out_p2`에 `*swarmer2*` 없음, mob01~ranged는 반입됨) 마지막 계열을 걸었다.
> **분리 세션으로 착수**: `aigen.py --spec spec_p2_swarmer2.json --out-dir out_p2 --backend
> higgsfield`를 `start_new_session=True`로 Popen(**PGID==PID=54305 검증**, 로그 `/tmp/gen_swarmer.log`).
> `art/.generating`에 `spec_p2_swarmer2.json 21:12 pid=54305` 표시. **크레딧 964→960으로 실호출
> 확인**(무출력 stall 아님). 한 계열 20~40분·세션과 무관 생존.
> **⚠️ 동시에 대화 세션이 살아 있다**: `W3Party.cs` mtime **21:07**(방금), `art/out_p6_boss/`에
> 보스 4종(brute·serpent·wraith·construct, 21:07~21:10) 새로 생성됨 + `.generating`에
> `spec_p6_boss.json 21:07` 표시(큐 밖, 대화 세션 소유로 추정 — 건드리지 않음). 그래서 이 세션은
> **combat·보스·공유 씬을 일절 안 만졌다**(충돌 회피). swarmer 아트만 착수하고 종료.
> **다음 세션이 할 일**: ①`art/.generating`의 swarmer 표시 확인 후 2h 안이면 **재생성 금지**(크레딧
> 이중차감) ②`out_p2/sheet_mob_swarmer2_A.png`·`_B.png`가 나왔으면 charger/ranged와 동일 파이프라인
> (sheet A 격자선 있으면 `wipe_gridlines`→`split_ai_sheet`(A 5×2 idle×4·walk×6, B 4×3 attack×4·
> hurt×4·death×4)→`align_frames`→`Resources/sprites/mob_swarmer` 동일 파일명 덮어쓰기(meta 보존)→
> `game_asset_names.py` 통과→montage 육안→`qa_shot.sh hunt`). 반입되면 **큐 #1 4/4 완전 종결**.
> 안 나왔으면 곧장 코드 트랙(단 `W3Party.cs` mtime 재확인 — 대화 세션 활성 여부).

> **⚠️ 다음 세션 최우선: INBOX 상단 21:01 신규 감사 「📋 기획서 ↔ 코드 대조」를 먼저 읽어라.**
> 오너가 코드 트랙 우선순위를 새로 제시했다 — **A(눌러도 반응 없는 빈 버튼: 필드 자동화 일정,
> 월드맵 성계이동·랭킹, 전투 스킬슬롯 2칸)를 먼저 치우거나 버튼을 비활성 표시**, 그 다음
> **B 중 대시·구르기**(§10-2 「피할 수 있는 위협」의 전제, `W3Party`에 0곳)가 가장 크다.
> 이게 아래 큐 #2b~#5보다 앞선다. combat 파일(`W3Party.cs`)을 만지기 전 `git log`·mtime으로
> 대화 세션 활성 여부부터 확인(§CLAUDE.md 충돌 회피).

> **이번 이터 결과(아트): 큐 #1 「몹 실루엣 재생성」 3/4 완료 — ranged✅ 반입 (`f603e43d`).**
> 이전 이터가 분리 세션으로 걸어둔 ranged 생성이 완료돼 있었다: `art/out_p2/sheet_mob_ranged2_A.png`
> (20:37)·`_B.png`(20:38), `.generating` 지워짐, 활성 프로세스 0 → **이중생성 위험 없음**.
> 이 세션이 **처리·반입**: 두 시트 모두 검은 격자선(Read 확인) → `wipe_gridlines`(A 8행·32열,
> B 10행·14열 덮음, 스켈레톤 손상 0 육안) → `split_ai_sheet` 고정격자(A 5×2=idle×4·walk×6,
> B 4×3=attack×4·hurt×4·death×4, 배치는 시트 육안 사전확인) → `align_frames`로 22장 **공통
> 117x126·바닥정렬**(A 106폭·B 120폭 크로스시트 튐 제거) → `mob_ranged/` 동일 파일명 덮어쓰기
> (**.meta 무변경 = GUID 보존**, `git status`가 png 22개만 M) → `game_asset_names.py` **통과**.
> **아트 육안 검증(montage)**: `output/qa/ashes-to-stars/shots/ranged_frames_montage.png` — 앙상한
> 직립 2족 궁수, 서 있는 자세 동일 배율·공통 바닥선, death는 눕고(안 늘어남), 무채색 셀셰이딩.
> **4족 charger/chaser와 한눈에 갈린다**(§10-4 도발 성립 조건). 네거티브 컨트롤: 옛 ranged 아트로
> 되돌리면 톤/실루엣이 캐릭터·신규몹과 갈림 → `git checkout HEAD~1 -- .../mob_ranged`로 재현.
> ⚠️ **인게임 hunt 화면 확인만 미완**: 원본 unity는 오너 `-useHub` 에디터가 락(§3, 죽이지 않음),
> 이 세션은 `unity_meas` rsync·배치 빌드·unityMCP가 **전부 권한 거부**(charger 반입 세션과 동일 벽).
> 스프라이트 자산 자체는 검증됨. **다음 GUI/빌드 세션이 `qa_shot.sh hunt`로 인게임 배치만** 확인하면
> ranged 몫 종결. **남은 1계열: swarmer** — `art/spec_p2_swarmer2.json` 준비 여부 확인 후 같은
> 파이프라인(wipe→split→align→덮어쓰기→검사기→montage육안). 단 위 「최우선」 코드 트랙이 앞선다.

> **이번 이터 결과(아트): 큐 #1 「몹 실루엣 재생성」 3계열째 ranged 생성 착수.**
> 시작 시 `git log`가 이터 중 `0bc4b950 → af9d43cb`로 움직여 **대화 세션이 살아 있음**을
> 확정(af9d43cb=charger 인게임 확인 완료, 3초 전 커밋). `BossBattle.cs`에 커밋 안 된 WIP
> (`bossDpsDisabled` 필드, #2b용)이 있어 **combat 파일은 손대지 않음**(충돌 회피).
> 아트는 풀림 확정: `higgsfield account status`=976크레딧·plus, `generate list` 최근 20건 **전부
> completed·waiting 0**(8슬롯 해제). 활성 잡 없어 이중생성 위험 없음.
> **ranged 생성을 분리 세션으로 착수**: `aigen.py --spec spec_p2_ranged2.json --out-dir art/out_p2
> --backend higgsfield`를 `start_new_session=True`로 Popen(**PGID==PID=48030 검증**, 로그
> `/tmp/gen_ranged.log`). `art/.generating`에 표시 남김. 한 계열 20~40분·세션과 무관하게 생존.
> **다음 세션이 할 일**: ①`art/.generating` 확인 후 2h 안이면 **재생성 금지**(크레딧 이중차감)
> ②`art/out_p2/sheet_mob_ranged2_A.png`·`_B.png`가 나왔으면 charger와 동일 파이프라인으로 반입
> (split→(sheet A 격자선 있으면 wipe_gridlines)→align→`Resources/sprites/mob_ranged` 덮어쓰기
> →`game_asset_names.py` 통과→`qa_shot.sh hunt` 육안). 반입 후 남은 1계열 swarmer 동일 진행.
> 안 나왔으면 곧장 코드 트랙(단, combat은 대화 세션 활성 여부 재확인).

> **이번 이터 결과(아트): 큐 #1 「몹 실루엣 재생성」 2/4 완료 — charger✅ 반입.**
> **아트 막힘은 풀렸다** — `higgsfield generate list`가 `waiting` 0·전부 `completed`(8슬롯 해제됨).
> charger 시트 A/B가 이미 생성돼 있었고(20:15·20:20, out_p2, 미추적) 이 세션이 **처리·반입**했다:
> split(5×2·4×3 고정격자)→align(공통 141x126·바닥정렬)→`Resources/sprites/mob_charger`에
> 22프레임 덮어쓰기(동일 파일명 → 기존 .meta GUID 보존, 씬 참조 안 끊김)→`game_asset_names.py` **통과**.
> **아트 품질은 Read로 육안 확인**(이미지 렌더): 시트 A는 육중한 무채색 코뿔소 브루트(덩치·뿔·앞쏠림,
> INBOX 돌진형 표 충족), 시트 B **attack_00이 명시적 wind-up 텔레그래프**(뒤로 젖힌 예고 자세 →
> §10-2 0.8초 예고 성립), 캐릭터 앵커(`char_dps_A`)와 같은 셀셰이딩 도트 화풍. 옛 art는 매끈한 3D톤
> **양**(`양·회색` 오너 지적 그대로) — 네거티브 컨트롤 자명.
> ⚠️ **이 세션은 유니티 실행 권한이 막혀(qa_shot "requires approval" 거부) 인게임 화면 검증만 미완.**
> 스프라이트 자산 자체는 Read로 검증됨. **다음 GUI 가능 세션이 `qa_shot.sh hunt`로 인게임 배치만 확인**하면
> #1의 charger 몫은 완전 종결. 남은 2계열: ranged·swarmer(spec 준비됨, 같은 파이프라인 1계열씩).

> **이번 이터 결과(코드 트랙): 큐 #2 「보스 쫄 소환」 통과 기준 충족.** 아트는 여전히 막혀
> (`higgsfield generate list` 타임아웃/waiting) INBOX 지침대로 코드 트랙 진행. 소환 기믹이
> 빈 GameObject 대신 **진짜 W3Party 쫄을 파티 한복판에 스폰**해 실제로 때리게 배선했다.
> **판정: 정상 소환피해 24 vs `BOSS_NO_SUMMON=1` 소환피해 0**(`output/qa/ashes-to-stars/boss_summon_*.log`),
> 스크린샷 `shots/qa_boss_summon_on.png`에 파티가 쫄에 포위된 실전투 렌더 확인(빈 화면 아님).
> **함정 하나 밟고 고침**: `_game`(소환 대상 인스턴스)을 `NextStyle`에서 잡았는데 그건 Awake라
> `GameMode`가 아직 false → `_game=null`로 굳어 소환이 조용히 샜다(로그에 `[W3] 보스 소환` 0건).
> `Update()`에서 `GameMode`일 때 잡도록 고쳐 해결(EnableFieldCover와 같은 계열의 알려진 함정).
> **남은 보스 통합은 큐 #2b로 분리**(보스 HP 미차감·장판 무피해·힐체크 미배선 — 정직하게 남김).

> ⚠️ **이 시각 이후 큐를 잡기 전에 `git log --oneline -12`부터 볼 것.** 오너가 대화
> 세션에서 마을·이펙트·UI를 연속으로 지시해 큐 밖 작업이 여럿 들어갔다. 아래 「대화
> 세션이 한 것」에 커밋 해시가 있다 — 같은 것을 다시 하지 마라.

> **이번 이터레이션 결과: 인프라 블록은 오진이었다 — chaser 계열 실제로 생성·반입·화면 확인 완료.**
> INBOX의 정정(higgsfield는 죽은 게 아니라 느릴 뿐)이 옳았다. 인내심을 갖고 돌리니
> `spec_p2_chaser2.json`이 **정상 생성**됐다(sheet A는 10분 타임아웃 2회 후 3번째 성공, 총 ~30분).
> 22프레임을 split→wipe_gridlines(sheet A에 검은 격자선 남아 재분할)→align→`Resources/sprites/mob_chaser`
> 반입, `game_asset_names.py` 통과, `qa_shot.sh hunt`에서 **늑대 실루엣이 색조별로 화면에 정상 표시**됨.
>
> **다음 세션이 오판 안 하도록 — higgsfield 실사용 사실:**
> 1. **살아 있다. 느릴 뿐이다.** 한 계열(2시트)에 20~40분. sheet 하나가 10분 타임아웃으로
>    실패해도 `aigen.py`가 3회 재시도하며 대개 성공한다. **무출력을 죽음으로 오판하지 마라** —
>    판정 근거는 크레딧 잔액 변화 + `pgrep -f higgsfield`(자식 프로세스)다. 크레딧: 1044→1030.
> 2. **stdout은 프로세스 종료 시에만 flush된다**(Python 버퍼링, non-tty). 진행 중 로그가
>    비어 있는 건 정상. 출력 파일이 out-dir에 나타나는지로 판단하라.
> 3. **sheet A는 검은 격자선을 그려서 나온다** → chroma_key가 마젠타만 지우고 검은 선은 남긴다.
>    반드시 `wipe_gridlines.py`로 시트를 먼저 청소한 뒤 split. sheet B는 격자선 없이 나옴.
> 4. **동시 probe 주의**: 이 세션 중 다른 프로세스가 higgsfield로 wolf-silhouette 테스트를
>    돌리고 있었다(크레딧 신호 교란). 크레딧으로 내 생성만 격리하려면 프로세스 트리를 봐라.
> 5. Gemini는 여전히 `limit:0`(유료결제만) — 하지만 higgsfield가 되므로 신경 쓸 필요 없다.

> **⚠️ 이터레이션 인계 (2026-08-15 18:18, 코드 트랙 루프 — 충돌 회피로 무편집 종료)**
> 이 루프 세션은 큐 #2를 잡으려 조사에 들어갔으나, **대화 세션이 바로 그 #2를 동시에
> 완성·커밋 중이었다.** 조사 도중 `ecf4b4b4`(보스 쫄 소환 실몹화)가 커밋되고 STATUS까지
> #2 완료로 갱신됐다 — 즉 내가 조사한 것은 전부 남이 지금 끝내는 중인 작업이었다.
> 다음 루프 세션이 같은 헛수고를 반복하지 않도록 이번에 확인한 두 가지를 남긴다:
> 1. **남은 코드 큐(#2b·#3·#5)는 전부 `W3Party.cs`를 만진다 = 대화 세션의 활성 편집면과 충돌.**
>    큐 잡기 전에 `stat -f %Sm Assets/Scripts/W3Party.cs`로 mtime을 보라. 방금(수 분 내)
>    바뀌었으면 대화 세션이 살아 있는 것이니 combat 파일을 편집하지 마라(커밋 오염·에디트 유실).
>    combat이 막혔을 때 **유일하게 비충돌**인 큐는 **#4 영지 건물**(새 파일 위주, `W3Party` 무관).
> 2. **이 루프 세션은 유니티 실행 권한이 막혔다** — `./tools/qa_shot.sh boss 0`이 "requires
>    approval"로 거부됐다(대화 세션은 18:11에 정상 빌드했으므로 세션별 권한 차이). GUI 검증이
>    필요한 항목(#4 스크린샷, #3/#5 fps·combat run)은 이 세션에서 **완료의 정의를 못 채운다.**
>    다음 세션이 Unity를 돌릴 수 있는지부터(qa_shot 한 번) 확인하고 큐를 고를 것.
> 3. **검증 명령 함정(실측)**: `qa_shot.sh boss`는 기본 프레임 300이라 DebugAutoPilot의
>    프레임캡처 경로가 30초 보스 시나리오보다 **먼저 return**해 소환이 안 터진다. 보스 소환을
>    보려면 **프레임을 0으로**(`qa_shot.sh boss 0`) 줘 `_shotFrame==0 && _shotSec==0` 경로로
>    30초 시나리오(`DebugAutoPilot.cs:166`)를 태워야 `[QA] 소환 몹이 파티에 준 피해` 로그가 남는다.

---

## 지금 어디까지 왔나

**핵심 루프 중 전투 부분이 측정 가능한 상태로 서 있다.** 파티 구성이 생존을 가르는 것이
5회 중앙값으로 확정됐고(§9·§10-4), 캐릭터·몹 아트가 화면에 실제로 나온다.

**루프가 끊기는 지점**: V3·V4 자동 경계는 닫혔다. 오너가 V2 사람 판정을 통과로 내렸다.
허브 UI 크롬(영지·캐릭터·필드/탑 헤더·3상태·**편성 명부 초상/목숨/역할**·보스 HP 프레임)도 닫혔다. 폴더 함정(`_Game/Art`·루트 유령 샷)은
이미 지웠다. 파티 제목 전용 아이콘은 아틀라스에 없어 새로 안 그림.
V4 **70% 사람 판정**은 자동검사로 완료 선언 금지.
영지 확장·침략·경매·합성은 §21-3 OUT — V4 70% 전에 새 범위를 열지 않는다.

## 다음 할 일 큐 (맨 위부터 하나씩)

| # | 항목 | 통과 기준 | 네거티브 컨트롤 |
|---|---|---|---|
| 1 | **몹 AI 4계열 실루엣 재생성** ⭐ **4/4 반입 완료 + 상호구분 매트릭스 검증 완료** — 인게임 hunt 확인만 GUI세션 대기 | §0-A 픽셀아트 화풍, **4 AI 실루엣** × 22프레임씩. 무채색(색은 런타임 `FamilyTint`). **오너 「몬스터 특색」 통과 기준 = "AI 4종이 서로 다르게 읽힌다"를 4계열 나란히 매트릭스로 직접 증명**(`shots/mob_family_matrix.png` — 늑대/뿔짐승/궁수/거미 완전 상호구분, 캐릭터와 동일 세계관, 무채색). 렌더 배선도 확인(`MobAnim(kind)→MOB_DIRS[kind]`). **인게임 hunt 확인만 미완**(오너 useHub 락 — GUI세션 인계) | 옛 `mob01`(분홍 3D톤)을 같은 판에 나란히 두면 톤이 눈에 띄게 갈림. 재현: 매트릭스 맨 아래 행 |
| ~~2~~ | ~~**§10-5 보스 쫄 소환**~~ ✅ **통과 기준 충족** | 기믹 발동 배선은 `006564f8`가 이미 함(호출부 0곳 → `FireNextGimmick`). 이번 이터: **소환이 빈 GameObject가 아니라 진짜 W3Party 쫄을 파티 한복판에 스폰**해 실제로 때린다. 측정: 정상 **소환피해 24**(5마리 스폰·`shots/qa_boss_summon_on.png`에 파티 포위) | ✅ **`BOSS_NO_SUMMON=1` → 소환 0마리·소환피해 0** (`boss_summon_NEGCTRL.log` vs `_NORMAL.log`) |
| ~~2b~~ | ~~**보스 나머지 통합 + V3 한 판 종단**~~ ✅ **이번 이터 종결** | 한 실행 SelfCheck: HP `9000→4500→0`·페이즈1·장판>0·힐보고=장판·소환>0·층 `5→6`. PNG `boss_run_shots/qa_boss.png` 676380B 보스+파티+마을 실렌더. 생산 경계 `GameFlow.ApplyTowerBossVictory` | ✅ `BOSS_NO_DPS=1` → HP 9000·처치0·층 5. API 제거 시 컴파일 RED |
| ~~3~~ | ~~**§3 RaceDef 배선**~~ ✅ **이미 완료(2026-08-15 21:45 실측)** | `W3Party.cs:557-567`이 `Resources.LoadAll<RaceDef>("races")` → `_bHp*=체력배율`·`_bSpd*=이속배율` 소비. 소비처 ≥1 충족(§3·§18-9). INBOX C도 "살아있음"으로 확인 | `--race`로 종족 강제 시 로그 `[W3] 종족=… 체력×… 이속×…` 확인됨 |
| 4 | **§16 영지 하위 건물 3종** — 영묘✅ · 대장간 첫 슬라이스✅(가죽→흉갑→HP) · 수비 배치✅(출전 제외). 남은 것: 강화+15·5부위, 경매 거래서버, 수비 침략 전투 | 갑옷 장착 시 SortieCombatant.HpMul=1.15, SelfCheck PASS, 화면 제작/착용 | `QA_NO_GEAR=1` 또는 장착 제거 시 배율 1. 기본직업만이면 제작 거부 |
| ~~5~~ | ~~**§10-9 잡몹 상한**~~ ✅ **이미 완료** | `W3Party.cs:336 const int MAXM=500`(기획서 300~500 충족). 인게임 500체 fps는 GUI/빌드 세션이 재확인 | 그리드를 끄면 fps가 무너져야 함(측정 미완) |
| ~~6~~ | ~~**전투 스타일 UI**~~ ✅ **완료 (`967daa89`)** | `StyleScreen`(직업별 4종 선택·PlayerPrefs 저장)·파티 편성에 진입 버튼·W3Party가 저장값 사용. 검증 하네스는 `UseFixedStyle`로 일괄 지정 유지 | `UseFixedStyle=true`로 되돌리면 선택이 무시되고 전원 균형형이 된다 |
| 7 | **§3·§18-6 캐릭터 성장(레벨업)** — 이번 이터 **반입·컴파일통과**(상단 블록), 배치 SelfCheck·인게임은 GUI세션 대기 | XP가 전투 후 출전 파티에 레벨 비례로 쌓여 `100×Lv^2.2`에서 레벨업(상한 100). `LifeSystemSelfCheck` ⑧이 곡선·레벨업·상한·총합보존·재기동유지 단언 — **배치모드 `-executeMethod AshesToStars.LifeSystemSelfCheck.Run`으로 확인**. CharacterScreen에 Lv·EXP 진척 표시 | `ExpToNext`를 상수/`AddExp` no-op으로 되돌리면 레벨 고정·SelfCheck ⑧ FAIL |
| ~~8~~ | ~~**§3 전직 시스템**~~ ✅ **오너 INBOX 전체 흐름 완료** — 기본/구저장(`ab20a005`) → Lv20 선택(`76c6a80b`) → 재료/시험(`93b9fe39`) → 2→4스킬(`42315c7b`) → 11종 고유 분기(`8128fbf7`) → Lv50 각성(`4a0b21d3`) → 2차 초필(`31a50057`) → 1차 6종 실전 종결(`b35fc3ac`) | 1차 6종 슬롯1/2 모두 `>0`, 6/6 PASS·PNG 6장. 수치와 로그는 상단 인계 및 `output/qa/ashes-to-stars/first_advancement/` | `QA_ADV_NO_SLOT=1` → 해당 수치0/FAIL, `=2` → 해당 수치0/FAIL |
| ~~§4~~ | ~~**긴급 탈출 아이템 소비처 배선**~~ ✅ **완료(이번 이터)** — `ScrollOfReturn` 첫 소비처 | `BattleScreen` 후퇴가 두루마리 1개 실소모(0개면 Locked). `LifeSystemSelfCheck` ⑨ 불변식. `game_asset_names` ✅. ⚠️배치 SelfCheck·인게임 GUI세션 대기 | 되돌리면 후퇴가 공짜로 회귀·`ScrollOfReturn` 소비처 0곳 |
| 10 | **⚠️ combat 후속: 긴급 탈출 6초 캐스팅·피격 취소(§4)** — 이번 이터는 즉시 소모형만 배선. §4 ✅ "캐스팅 6초·피격 시 취소·수동 조작 한정"은 전투 타이밍이라 `W3Party` 소유 | 후퇴 발동 시 6초 캐스트 바 → 피격 시 취소·두루마리 미소모, 완료 시 소모+탈출 | 캐스트 제거하면 즉시 탈출로 회귀 |
| ~~11~~ | ~~**대출 연체·파산 제재(§12·§18-5)**~~ ✅ **경매/침략 문 차단** (`41c2ded8`+진입 거부) | 연체1 경매 잠김·연체2 침략 잠김+`TryGoInvasion` false. Locked 줄은 클릭 없음. PNG `loan_sanction_shots/` | 연체 0이면 30층에서 침략 열림. 제재 제거 시 연체해도 문 열림. 거래서버·침략 본게임·건물 강등·30%압류는 OUT |
| 9 | **⚠️ 발견(combat 소유): 성장이 전투력에 반영 안 됨** — `CharacterRecord.Level`을 `W3Party`(전투)가 안 읽는다(이번 이터 grep 실측: Level 소비처는 LifeSystem·EstateScreen·CharacterScreen뿐, 전투 0곳). 즉 레벨을 올려도 전투에서 강해지지 않아 §18-6 성장 곡선이 절반만 소비됨. **combat 파일이라 대화 세션 소유** — 여기 기록만 남긴다 | 레벨↑ → 전투 스탯(HP/공격)↑이 W3Party 측정에 나타남 | Level 계수를 1.0 고정으로 되돌리면 성장 무의미 |

### 대화 세션이 한 것 (2026-08-15, 오너 직접 지시 — 큐 밖)

| 지시 | 결과 | 커밋 |
|---|---|---|
| 몹 깜빡임·좌우 방향·지형 | 스폰 시점 색 캐시, 뒤집기 히스테리시스, 프랍 밀도 | `e8ef6606` |
| 「지형을 마을처럼」 | 마을 프랍 10종 + `BuildVillage`. 전투 스타일 UI도 함께 | `967daa89` |
| 「같은 종류 같은 색」·「skill_ring 지워라」·「길도 있어야지」 | 색을 종류에서 결정, fx 로딩 수리, 길 신설 | `97d25ba9` |
| 「스킬링 아직 나온다」·「바닥도 어울리게」 | 바닥 링 표시 **전면 삭제**, 거리형 마을, 흙바닥 | `35dbf9d6` |
| 실제 마을 사진 「이런 식으로」·「이펙트 붙이고」 | 굽은 길 + 샛길, 불규칙 배치(구성물 375), 이펙트 배선 | `6af253ab` |
| 「왜 루프가 자꾸 멈추지」 | 인프라 장애를 실패로 세지 않도록 분류 + 백오프 | `66d0f9cb` |

**진행 중(다음 세션이 이어받을 것)**
- ~~**나무 6종 생성 중**~~ ✅ **완료·화면 확인 (2026-08-15 18:0x, 루프+대화 세션 동시 작업)**
  6종 전부 생성→크로마키→128 정규화→`Resources/props/` 반입. 배선은 대화 세션이 커밋
  (`53d676a2` — `ScatterCount`를 손으로 세지 않고 `village_` 접두 전까지 자동 카운트하도록
  재작성). 루프가 짝 `.meta` 6종 반입(`88ec87d5` — png만 커밋되고 import 설정이 빠져 있었다).
  **`qa_shot.sh hunt` 육안 확인**: field_tree 0~3·shrub_row가 마을 들판에 집보다 크게
  산포돼 렌더됨(`shots/qa_hunt.png`). 검사기 통과.
  ⚠️ **남은 것: `village_tree_0`(과수원 열매나무)는 반입·크기지정됐으나 `FieldDecor`에
  배선 안 됨 → 화면에 안 나온다.** `village_` 접두라 자연물 산포에 넣으면 `ScatterCount`가
  거기서 끊긴다 — 마을 구간에 넣고 `BuildVillage`가 집 옆에 세우도록 해야 한다(FieldDecor는
  대화 세션 소유라 재충돌 피해 미착수).
- **집이 그림일 뿐 통과된다** — 마을 구성물을 `place(..., asCover:false)`로 세워서
  몹·파티가 건물을 뚫고 지나간다. 막으려면 집만 골라 `_cover`에 넣어야 하는데,
  아레나 안 장애물 수가 크게 늘어 W1~W3 구성 비교(빈 판 전제)와 경로 탐색에 영향이 간다.
  **fps와 W3 지표를 재보고 결정할 것.**

**드러난 결함 — 다음 세션이 되돌리지 않도록:**
0. **`PackTextures`는 정규화 UV를 돌려주고 `Sprite.Create`는 픽셀 Rect를 받는다.** 이걸
   그대로 넘겨서 **프랍이 도입 이래 한 번도 안 그려지고 있었다**(로그는 "90개 배치"로 정상).
   화면이 비었는데 수치가 정상이면 이 계열을 의심할 것.
1. **`placed < PROP_CAP`을 for 조건에 걸면 프랍이 맵 아래쪽에만 몰린다.** 상한에 닿는
   순간 훑기가 멈추기 때문이다. 후보를 다 모은 뒤 균일 간격으로 솎아낼 것.
2. **`art/prop_scale.json`은 2026-08-14에 만들어 놓고 읽는 코드가 0곳이었다.** 그래서
   PPU 32 고정 → 128px 원본이 전부 4유닛(캐릭터의 2배)이었다. 지금은 `FieldDecor`가
   `Resources/prop_scale.json`을 읽어 PPU를 역산한다. **아트 쪽 파일을 고치면
   `Assets/Resources/`로 복사해야 반영된다**(두 벌인 것이 약점 — 통합은 미해결).

### ⚠️ 설계 정정 — 몹 아트(#1)를 "5계열 5장"으로 만들지 마라 (이번 세션 확인)

`GAME_ART_RESOURCES.md` §0-B(아트 물량 최종 권위) 규칙 3: **"몬스터 계열 5종도 색조로만
구분한다. AI 4종의 실루엣만 만들고 계열은 `MobDef.색조`로 처리 → 20종이 실루엣 4개."**
런타임 틴트는 실제로 배선돼 있다 — `ProjectSetup.cs:348` `sr.color = m.색조;` +
`FamilyColor(f)`(`:260`). 그래서 큐의 "5계열 × 22프레임"은 **5장 별개 시트가 아니라
AI 실루엣 4종(각 22프레임)을 회색으로 재생성 + 색은 런타임**이 옳다. 5장을 따로 그리면
§0-B를 위반한다(물량 폭증·틴트 이중적용). 큐 #1을 그 뜻으로 다듬어 둠.

**재실행 준비물 — 4계열 spec 전부 작성 완료(이터2)**: `art/spec_p2_{chaser,charger,ranged,swarmer}2.json`.
넷 다 동일 구조: `anchor_mobs_gray.png` + **캐릭터 시트 `out_char/char_dps_A.png`(충실도 앵커)**,
룰셋 `m2`(충실도만 베끼고 색은 무채색 유지), A=10셀(idle×4·walk×6)·B=12셀(attack×4·hurt×4·death×4).
각 계열 프롬프트는 **INBOX 행동예고 표대로** 실루엣을 지었다(웹서치 anticipation 원칙 반영):
- **chaser**(추격형): 날렵 늑대형·낮은 앞기운 자세 (기존, 이터1)
- **charger**(돌진형): 육중·뿔·앞쏠린 무게중심 + attack 첫 프레임이 **명시적 WIND-UP 예고**(§10-2 0.8초)
- **ranged**(원거리형): 직립 2족·긴 팔·활 지참 — 4족 근접과 한눈에 갈림
- **swarmer**(포위형): 낮고 넓게 벌어진 다족 벌레형 — 무리 중 하나로 읽힘

구조 검증 완료(4계열 A=10·B=12 셀·refs 존재·무채색 룰 포함, `scratchpad/validate_specs.py`).
**백엔드 살아나면 계열마다**: `python3 aigen.py --spec spec_p2_<fam>2.json --out-dir out_p2` →
`split_ai_sheet.py`(자동 격자검출, `--names idle_00..idle_03 walk_00..walk_05 attack_00..attack_03 hurt_00..hurt_03 death_00..death_03` — 순서는 A/B 셀 순서) →
`align_frames.py <dir>` → `Resources/sprites/mob_<fam>` → `game_asset_names.py`로 반영 확인 → `qa_shot.sh hunt` 육안.

## 완료 (근거 포함)

| 항목 | 근거 | 커밋 |
|---|---|---|
| **§4·§6 저체력 자동 귀환** | 필드만 HP≤30%→3초 이탈→영지. 기본 ON. 사망·보상 없음. SelfCheck 전항 PASS. PNG 421774B `저체력 귀환 켜짐`. API 제거 시 컴파일 RED 2건 | `c196162c` |
| **§3 합성 첫 슬라이스** | 1차+ 재료 소멸(영묘 아님)·강골 HpMul 1.25·슬롯4·보류 교체/포기·환생 소멸. SelfCheck 전항 PASS. PNG 556263B `흡수 강골 (1/4)`. `QA_NO_FUSION=1`이면 배율 1 | `b0c85e52` |
| **보스 HP 프레임 소비처** | `boss_hp_frame` RequiredKeys+DrawBossHp+Overlay. SelfCheck PASS. PNG: 탑 견본 3칸 193585B, 실전 7500/7500 페이즈4 648352B. API 제거 시 컴파일 RED 11건 | `0971456c` |
| **파티 편성 명부 크롬(초상·역할·목숨)** | 캐릭터만 쓰던 프레임/하트/역할을 편성이 소비. SelfCheck PASS. PNG 643294B: 5인 초상+서로 다른 역할뱃지+하트3, 옛 회색글자·`목숨 3/3`과 갈림. API 제거 시 컴파일 RED 6건 | `f737d92b` |
| **폴더 함정 정리(_Game/Art·qa_vfx_live)** | 빈 Art 함정 4 meta + 루트 유령 PNG 삭제. 검사기 네거티브 exit 1 / 정상 exit 0. 런타임 옛 경로 소비처 0. README 지도 | `8c0defaf` + `8c988f35` |
| **UI 필드·탑 헤더 + 버튼 3상태 육안** | `HeaderKey`가 Field/Tower를 field/tower 조각에 연결. SelfCheck PASS. PNG: 필드=검+월계관 494741B, 탑=등대 230392B, 견본 보통/호버/눌림이 서로 다름. API 제거 시 컴파일 RED 16건 | `32a257d6` |
| **UI 아이콘 소비처(영지 건물·캐릭터 초상/목숨)** | 아틀라스에 있던 건물4·하트2·역할4·xp/초상 프레임을 Estate/Character가 소비. 헤더가 화면과 일치. SelfCheck PASS. PNG: 영지 성+4건물, 캐릭터 파티아이콘+초상6+하트/XP. 정적 85소스 0오류 | `f690e210` |
| **V4 패배→삭제→재건 자동 경계** | 출전만 사망·벤치 불변·3회 삭제·생존0=재건1·PvP 목숨0·재기동 유지. SelfCheck ⑫ PASS. PNG: 결과(영묘+재건1)·캐릭터(5인 삭제됨+재건1 출전가능). API 제거 시 컴파일 RED 17건 | `3b9563af` + 이 커밋 |
| **V3 한 판 종단** | 파티 공격→HP 9000→4500(페이즈1)→0·장판/힐보고·소환·처치·층 5→6이 **같은 실행**. `ApplyTowerBossVictory`를 BattleScreen OnBossDefeated가 소비. Unity SelfCheck PASS, `qa_boss.png` 육안 확인. `BOSS_NO_DPS=1`이면 층 불변 | `ec927cbe` |
| **§8 탑 등반 층 진행 배선** | "다음 층 도전"(잡몹웨이브)을 이겨도 층이 안 올랐다 — `ClearFloor`가 보스 격파에서만 불려 일반 층은 진행도에 반영 안 됨. `OnBattleEnd` 생존 분기에 `IsTowerFloorClear`(잡몹웨이브만·보스 제외 이중상승 방지)로 `ClearFloor(BossFloor)` 배선. 새 시스템 0(기존 ClearFloor·TowerFloor 재사용). `game_compile_check` PASS·`game_asset_names` ✅·SelfCheck ⑪(잡몹웨이브→참/보스→거짓/필드·전멸·던전→거짓·단조증가·재기동유지). ⚠️배치SelfCheck·인게임 GUI세션 대기 | `feeb9f96` |
| **§15 침략 30층 해금 게이트** | §15 ✅ "30층 이상 등반 시 해금"인데 `WorldMapScreen` 침략은 무게이트 상시 활성(층 1에서도 발동)이었고, **경매장은 이미 30층 게이트 존재**(`EstateScreen.cs:66`)·`SceneStructureBuilder.cs:156` "침략·경매장 동시 해금" — 비대칭을 해소. `TowerFloor>=30`만 라이브, 미만은 Locked(현재 층 표시). 신규 시스템·오펀 0(기존 TowerFloor·침략 배틀 재사용). `game_compile_check` PASS·`game_asset_names` ✅. ⚠️인게임 GUI세션 대기 | `29f1b991` |
| **§12·§18-5 대출 시스템(핵심 슬라이스)** | `grep loan/debt` 전 코드 0곳이던 경제 키스톤. 소비처 실재분만: Earn 수입50% 자동상환(상시)·TowerScreen "대출받고 입장"(수동). 한도=순자산(지갑-부채)30%∧20G/h·티어, 이자 0.5%/h 복리. `game_compile_check` PASS·SelfCheck ⑩ 결정론 검증(30000한도·자동상환·복리>단리·재기동유지·상환0). 연체/파산 제재는 경매장·침략 부재로 유보(정직). ⚠️배치SelfCheck·인게임 GUI세션 대기 | `e88649b9` |
| **§3·§18-6 캐릭터 성장(레벨업)** | `CharacterRecord.Exp`+직렬화(하위호환) · `ExpToNext=100×Lv^2.2`(1=100·2=459·10=15848·상한100) · `AwardBattleExp`가 출전파티에 레벨비례 분배(총100→5인 각20, 총합보존) · BattleScreen 골드옆 1회지급 · ResultScreen/CharacterScreen 표시 · `LifeSystemSelfCheck` ⑧. `game_compile_check` PASS·`game_asset_names` 이상무. ⚠️배치SelfCheck·인게임 GUI세션 대기, 절대총량은 프로토타입값 | `f5e0778b` |
| §10-4 도발 하드 락 | **D/A 0.66** (목표 0.75 이하). 5회 중앙값 | `e5c5c6e1` |
| §9 레이드 1인 불가 | **C/A 0.08** | `e5c5c6e1` |
| §3 전투 스타일 SO 배선 | 소비처 0곳 → `W3Party.cs:52` `Resources.LoadAll` | `067b8d6e` |
| 밸런스 앵커 재판정 | **오진 종결** — 26.9%는 게임 수치가 아니라 리텐션 가정이 지배 | `fbc5bc69` |
| P1 프랍 32종 반입 | 검사기 통과 | `da80d6f8` |
| P2 몹 4계열 코드 연결 | 88장 반입, `MOB_DIRS` 1→5종, 스폰/애니 규칙 통일 | `5caf3841` |
| 몹 걷기 튐 수리 | 캔버스 4종 혼재 → 1종, 발 여백 0 | `c7d66a04` |
| 캐릭터 4직업 52프레임 재생성 | 캔버스 1종·발여백 0·검사기 통과·meta 52개 | `17657ae9` |
| 화면 3종 실태 확정 | 파티편성·캐릭터 ✅구현 / 영지 ⚠️껍데기 | `fbc5bc69` |
| 시각 QA 도구 + 자동 루프 | `qa_shot.sh` 전체 흐름 실증(692KB 캡처) · STOP·환경변수·가드 실측 | `bcadbca1` |
| **§10-5 보스 소환을 실몹으로** | 정상 **소환피해 24**(5마리, `shots/qa_boss_summon_on.png` 파티 포위 렌더) / 네거티브 **`BOSS_NO_SUMMON=1` → 0** (`boss_summon_NORMAL.log`·`_NEGCTRL.log`). 컴파일 PASS | `ecf4b4b4` |
| 캐릭터 52장 화면 반영 확인 | `qa_hunt.png`에서 4직업이 새 아트로 표시·실루엣 구분됨 | `bcadbca1` |
| **몹 chaser 계열 재생성(INBOX⭐)** | higgsfield 정상 생성(크레딧 1044→1030), 22프레임 반입, `game_asset_names` 통과, `qa_hunt.png`에서 늑대 실루엣 색조별 표시·캐릭터와 같은 세계관 | `20a048b9` |
| **몹 charger 계열 재생성(INBOX⭐)** | 시트 A/B(생성 완료분) 처리·반입, 22프레임(동일 파일명→기존 meta 보존), `game_asset_names` 통과. **Read 육안검증**: 무채색 코뿔소 브루트(덩치·뿔·앞쏠림)·attack_00 wind-up 텔레그래프(§10-2)·캐릭터와 같은 셀셰이딩. 옛 art=매끈 3D톤 양(네거티브 자명). ⚠️인게임 qa_hunt는 GUI세션 대기(이 세션 유니티 실행 권한 없음) | `c80c6d2f` |
| **몹 swarmer 계열 재생성(INBOX⭐)** | 21:13 생성분 시트 A/B 처리·반입. **B가 4×3 spec인데 4×4로 생성 → 라벨몽타주+치수(896/4정수)로 확정 후 row2 중복행 skip**, 22프레임(동일 파일명→meta 보존, git png22 M·meta0), `game_asset_names` 통과. **montage 육안**(`shots/swarmer_frames_montage.png`): 다족 벌레형 포위형·attack_02 물기 임팩트·death_01 뒤집힘·무채색 셀셰이딩, 4족/2족과 갈림. death 4장 잔여 격자선 파편 제거. ⚠️인게임 qa_hunt GUI세션 대기 | `7e3c06ac` |

## 막힌 것 · 보류

**✅ (해소) "이미지 생성 백엔드 죽음"은 이터1·2의 오진이었다** — 이터3에서 chaser를 실제로
생성해 반증했다. higgsfield는 **살아 있고 느릴 뿐**이다(위 「이번 이터레이션 결과」 참조). 이터1·2가
15분에서 죽음으로 단정한 것이 오류였다 — 한 시트가 10분 타임아웃으로 실패해도 aigen의 3회 재시도가
대개 성공하고, 한 계열에 20~40분이 걸린다. **다음 세션은 남은 charger·ranged·swarmer를 같은
파이프라인으로 1계열씩 뽑으면 된다. 무출력 stall처럼 보여도 크레딧·프로세스가 살아 있으면 기다려라.**
(gemini는 여전히 `limit:0`이지만 higgsfield가 되므로 무관.)

**탱 상시 DR 20%** — 도입 근거가 "E/A 0.67 → 0.60 이하"였는데 최신 측정에서 **E/A가 이미
0.39**다(`w3_reps5.csv`). 힐러 가치가 충분히 나오고 있어 지금 넣으면 근거 없이 수치를
만지는 것이 된다. 재개하려면 새 근거가 필요하다.

## 도구

| 무엇 | 명령 |
|---|---|
| 시각 QA(화면 확인) | `./tools/qa_shot.sh [dungeon\|hunt\|boss\|raid\|party] [프레임]` |
| 에셋 네이밍·반영 검사 | `python3 projects/ai-team/skills/마루_게임개발/tools/game_asset_names.py` |
| 프레임 캔버스 정렬 | `python3 projects/ashes-to-stars/art/align_frames.py <디렉터리>` |
| 캐릭터 생성 파이프라인 | `./projects/ashes-to-stars/art/build_chars.sh [직업]` |
| W3 밸런스 측정 | `python3 projects/ai-team/skills/마루_게임개발/tools/game_regression.py` |

## 루프 정지 이력 — 해결됨 (2026-08-15 17:18 → `66d0f9cb`)

`Not logged in`으로 3연속 실패해 멈췄다. **인증이 죽은 게 아니라 44초 사이에
재시도 상한을 다 태운 것**이었다(직후 `echo ok | claude -p` 정상). 루프가 인프라
장애와 작업 실패를 구분하지 않던 것이 원인이고, 지금은 구분한다 — 인프라 장애는
횟수를 세지 않고 제곱 백오프로 물러섰다 다시 온다.

**다음에 루프가 멈췄다면 이 순서로 볼 것**: ①`loop/loop_main.log`의 정지 사유
②`echo ok | claude -p`로 구독 토큰 직접 확인(만료면 사람이 `claude auth login`)
③그 외면 `loop/logs/iter_*.log` 마지막 것.

## (해결됨) 루프 자동 정지 (2026-08-15 18:26)
연속 3회 실패로 멈췄다. 마지막 로그: `/Users/junholee/ai_lab/loop/logs/iter_20260815_182611.log`
원인을 확인하고 `rm loop/STOP` 후 재개할 것.

## ⚠️ 루프 정지 — 인프라 장애 지속 (2026-08-16 02:22)
`claude -p`가 12회 연속 실패했다. 마지막 로그: `/Users/junholee/ai_lab/loop/logs/iter_20260816_022155.log`
1순위 의심: **구독 토큰 만료**. `echo ok | claude -p`로 직접 찔러 확인하고,
만료면 `claude auth login`(브라우저 OAuth라 사람만 가능) 후 `rm loop/STOP`.

## ⚠️ 루프 정지 — 인프라 장애 지속 (2026-08-19 05:23)
`grok` 실행기가 12회 연속 실패했다. 마지막 로그: `/Users/junholee/ai_lab/loop/logs/iter_20260819_052356.log`
구독 한도·로그인·네트워크 상태를 확인한 뒤 `rm loop/STOP`으로 재개할 것.

## ⚠️ 루프 정지 — 인프라 장애 지속 (2026-08-19 11:35)
`grok` 실행기가 12회 연속 실패했다. 마지막 로그: `/Users/junholee/ai_lab/loop/logs/iter_20260819_113538.log`
구독 한도·로그인·네트워크 상태를 확인한 뒤 `rm loop/STOP`으로 재개할 것.

## ⚠️ 루프 정지 — 인프라 장애 지속 (2026-08-20 01:06)
`grok` 실행기가 12회 연속 실패했다. 마지막 로그: `/Users/junholee/ai_lab/loop/logs/iter_20260820_010606.log`
구독 한도·로그인·네트워크 상태를 확인한 뒤 `rm loop/STOP`으로 재개할 것.
