# 재와 별 — 현재 위치 · 다음 할 일

> **인수인계서.** 매 이터레이션은 기억 없는 새 세션이다. 끝낼 때 이 파일을 갱신하지 않으면
> 다음 세션이 처음부터 다시 판단한다.
>
> 갱신 규칙: 완료로 내릴 때 **판정 근거(수치·커밋 해시)를 반드시 같이 적는다.**
> 근거 없는 완료는 다음 세션이 재검증해야 하므로 완료가 아니다.

최종 갱신: 2026-08-16 · 루프 실행기 Grok (폴더 함정 정리)

## 다음 할 일 (원장 §22 — 위에서부터 하나만)

1. **INBOX UI 다음 슬라이스 — 파티 편성 헤더** (오너 16:46 「다른 게임 분석해서 UI」). `HeaderKey("Party")`가 아직 null이라 나침반. 아틀라스에 파티 조각이 있으면 연결만. 새 힉스필드 생성 없음. 통과: `qa_shot.sh go:Party`에서 제목 옆 아이콘이 나침반이 아님. 네거티브: 매핑을 빼면 다시 나침반.

아이콘·호버 육안은 위 관문 사이 빈 칸을 채울 때만. 기획서 OUT을 새 범위로 열지 마라.

> **루프 세팅(2026-08-16):** 클로드 주간 한도 + 코덱스 `usage limit … Aug 23 9:23 AM` 실측. 실행기는 `loop/agent=grok`. 그록은 stdin을 프롬프트로 안 읽으므로 `--prompt-file`로 호출한다.

> **오너 선택(2026-08-16 16:54): V2 사람 판정 → 통과.**


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
허브 UI 크롬(영지·캐릭터·필드/탑 헤더·3상태)도 닫혔다. 폴더 함정(`_Game/Art`·루트 유령 샷)은
이번 이터에서 지웠다. **다음 자동 슬라이스는 파티 편성 헤더.**
V4 **70% 사람 판정**은 자동검사로 완료 선언 금지.
영지 확장·침략·경매·합성은 §21-3 OUT — V4 70% 전에 새 범위를 열지 않는다.

## 다음 할 일 큐 (맨 위부터 하나씩)

| # | 항목 | 통과 기준 | 네거티브 컨트롤 |
|---|---|---|---|
| 1 | **몹 AI 4계열 실루엣 재생성** ⭐ **4/4 반입 완료 + 상호구분 매트릭스 검증 완료** — 인게임 hunt 확인만 GUI세션 대기 | §0-A 픽셀아트 화풍, **4 AI 실루엣** × 22프레임씩. 무채색(색은 런타임 `FamilyTint`). **오너 「몬스터 특색」 통과 기준 = "AI 4종이 서로 다르게 읽힌다"를 4계열 나란히 매트릭스로 직접 증명**(`shots/mob_family_matrix.png` — 늑대/뿔짐승/궁수/거미 완전 상호구분, 캐릭터와 동일 세계관, 무채색). 렌더 배선도 확인(`MobAnim(kind)→MOB_DIRS[kind]`). **인게임 hunt 확인만 미완**(오너 useHub 락 — GUI세션 인계) | 옛 `mob01`(분홍 3D톤)을 같은 판에 나란히 두면 톤이 눈에 띄게 갈림. 재현: 매트릭스 맨 아래 행 |
| ~~2~~ | ~~**§10-5 보스 쫄 소환**~~ ✅ **통과 기준 충족** | 기믹 발동 배선은 `006564f8`가 이미 함(호출부 0곳 → `FireNextGimmick`). 이번 이터: **소환이 빈 GameObject가 아니라 진짜 W3Party 쫄을 파티 한복판에 스폰**해 실제로 때린다. 측정: 정상 **소환피해 24**(5마리 스폰·`shots/qa_boss_summon_on.png`에 파티 포위) | ✅ **`BOSS_NO_SUMMON=1` → 소환 0마리·소환피해 0** (`boss_summon_NEGCTRL.log` vs `_NORMAL.log`) |
| ~~2b~~ | ~~**보스 나머지 통합 + V3 한 판 종단**~~ ✅ **이번 이터 종결** | 한 실행 SelfCheck: HP `9000→4500→0`·페이즈1·장판>0·힐보고=장판·소환>0·층 `5→6`. PNG `boss_run_shots/qa_boss.png` 676380B 보스+파티+마을 실렌더. 생산 경계 `GameFlow.ApplyTowerBossVictory` | ✅ `BOSS_NO_DPS=1` → HP 9000·처치0·층 5. API 제거 시 컴파일 RED |
| ~~3~~ | ~~**§3 RaceDef 배선**~~ ✅ **이미 완료(2026-08-15 21:45 실측)** | `W3Party.cs:557-567`이 `Resources.LoadAll<RaceDef>("races")` → `_bHp*=체력배율`·`_bSpd*=이속배율` 소비. 소비처 ≥1 충족(§3·§18-9). INBOX C도 "살아있음"으로 확인 | `--race`로 종족 강제 시 로그 `[W3] 종족=… 체력×… 이속×…` 확인됨 |
| 4 | **§16 영지 하위 건물 3종(대장간·경매장·수비대)** | 영묘✅ 채움. 나머지 셋은 **소비 시스템이 없어** 건물별 정직 사유로 잠금(`ceb522c8`) — 채우려면 각각 장비·재료(§11)·거래서버(§12)·침략 배치소비(§13-5) 시스템이 선행. **소비처 없이 UI만 채우면 또 거짓말** | 되돌리면 3건물 제네릭 "내용 없음"으로 회귀 |
| ~~5~~ | ~~**§10-9 잡몹 상한**~~ ✅ **이미 완료** | `W3Party.cs:336 const int MAXM=500`(기획서 300~500 충족). 인게임 500체 fps는 GUI/빌드 세션이 재확인 | 그리드를 끄면 fps가 무너져야 함(측정 미완) |
| ~~6~~ | ~~**전투 스타일 UI**~~ ✅ **완료 (`967daa89`)** | `StyleScreen`(직업별 4종 선택·PlayerPrefs 저장)·파티 편성에 진입 버튼·W3Party가 저장값 사용. 검증 하네스는 `UseFixedStyle`로 일괄 지정 유지 | `UseFixedStyle=true`로 되돌리면 선택이 무시되고 전원 균형형이 된다 |
| 7 | **§3·§18-6 캐릭터 성장(레벨업)** — 이번 이터 **반입·컴파일통과**(상단 블록), 배치 SelfCheck·인게임은 GUI세션 대기 | XP가 전투 후 출전 파티에 레벨 비례로 쌓여 `100×Lv^2.2`에서 레벨업(상한 100). `LifeSystemSelfCheck` ⑧이 곡선·레벨업·상한·총합보존·재기동유지 단언 — **배치모드 `-executeMethod AshesToStars.LifeSystemSelfCheck.Run`으로 확인**. CharacterScreen에 Lv·EXP 진척 표시 | `ExpToNext`를 상수/`AddExp` no-op으로 되돌리면 레벨 고정·SelfCheck ⑧ FAIL |
| ~~8~~ | ~~**§3 전직 시스템**~~ ✅ **오너 INBOX 전체 흐름 완료** — 기본/구저장(`ab20a005`) → Lv20 선택(`76c6a80b`) → 재료/시험(`93b9fe39`) → 2→4스킬(`42315c7b`) → 11종 고유 분기(`8128fbf7`) → Lv50 각성(`4a0b21d3`) → 2차 초필(`31a50057`) → 1차 6종 실전 종결(`b35fc3ac`) | 1차 6종 슬롯1/2 모두 `>0`, 6/6 PASS·PNG 6장. 수치와 로그는 상단 인계 및 `output/qa/ashes-to-stars/first_advancement/` | `QA_ADV_NO_SLOT=1` → 해당 수치0/FAIL, `=2` → 해당 수치0/FAIL |
| ~~§4~~ | ~~**긴급 탈출 아이템 소비처 배선**~~ ✅ **완료(이번 이터)** — `ScrollOfReturn` 첫 소비처 | `BattleScreen` 후퇴가 두루마리 1개 실소모(0개면 Locked). `LifeSystemSelfCheck` ⑨ 불변식. `game_asset_names` ✅. ⚠️배치 SelfCheck·인게임 GUI세션 대기 | 되돌리면 후퇴가 공짜로 회귀·`ScrollOfReturn` 소비처 0곳 |
| 10 | **⚠️ combat 후속: 긴급 탈출 6초 캐스팅·피격 취소(§4)** — 이번 이터는 즉시 소모형만 배선. §4 ✅ "캐스팅 6초·피격 시 취소·수동 조작 한정"은 전투 타이밍이라 `W3Party` 소유 | 후퇴 발동 시 6초 캐스트 바 → 피격 시 취소·두루마리 미소모, 완료 시 소모+탈출 | 캐스트 제거하면 즉시 탈출로 회귀 |
| 11 | **⚠️ 유보: 대출 연체·파산 제재(§12·§18-5)** — 이번 이터에 대출 코어는 배선됨(`e88649b9`). 연체 제재(경매장 등록금지·침략불가)·3회 연체 파산(영지건물 강등·아이템 30%압류)은 **제재 대상 시스템이 없어** 미배선. `due_at`은 저장만 됨. 경매장(탑30층+거래서버)·침략·영지레벨이 생길 때 함께 배선 — **지금 넣으면 오펀** | 경매장/침략 화면에서 연체 시 실제로 차단 | 제재 제거 시 연체해도 아무 불이익 없음 |
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
| **폴더 함정 정리(_Game/Art·qa_vfx_live)** | 빈 Art 함정 4 meta + 루트 유령 PNG 삭제. 검사기 네거티브 exit 1 / 정상 exit 0. 런타임 옛 경로 소비처 0. README 지도 | 이 커밋 |
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
