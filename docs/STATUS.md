# 재와 별 — 현재 위치 · 다음 할 일



> 인수인계서. 보드(`loop/board.py`)가 이 파일을 읽는다.

> 2026-08-23 빈 템플릿으로 갈리며 보드가 비었던 것을, 아카이브·WORKLOG 기준으로 복구.



최종 갱신: 2026-08-26 08:35 · **ORDERS③ 측정 하네스 50층 확장 + G3 파티 실DPS 대조 게이트** 완료(`f5e6b1c3`). 첫 실측 **FAIL 3건 공식 기록** — G2 5h사냥→Lv47 vs 필요50(최상층 도달 6.04h) · G3 권장 파티(기대레벨+전직) 25·50층 보스전 **전멸**(실측 DPS 0 vs 필요 542/3076 — 장비·합성·가호 미포함 베어 로스터 기준). 네거티브(약한 파티 Lv1 탈락) PASS로 판별력 입증. 직전 바퀴 ORDERS② 멤버별 스타일 배선(`51c3e862`)·캐릭터창 열람 개선(`80f934e8`) STATUS 반영. GameSweep 재실측 ok:true 63/63(07:45). 루프 가동 중(opencode).

> ⚠️ [2026-08-25 21:58 보드지킴이] 공유 인덱스의 `loop/board.py`·`loop/test_board.py`는 보드지킴이 커밋 `6b9699f4`(수함대 집계 수정+회귀검사) **이전** 스냅이다 — 이 상태로 맨몸 `git commit`하면 방금 닫힌 수정이 되돌려진다. board·test 두 파일은 커밋 전 작업 트리 기준으로 다시 `git add`해 해소할 것(board.py엔 인덱스에만 있는 cli_usage 변경분도 있으니 병합 확인).

루프 가동 중 — 실행기 체인(claude→grok→codex→opencode)에서 현재 opencode로 실랩 진행. 막힌 곳 해제: ①launchd 사용자 env `LOOP_AGENT=claude` 잔재가 loop/agent 파일 지정을 덮쓰던 것 제거 ②공급자 3종 만료(Claude 로그인·Grok/Codex 주간 소진)를 opencode 무료 실행기로 우회. 오너 위임 판정 완료(INBOX 2026-08-25 밤): ORDERS 3건 승인(P1부터 ②→③→① 순서) · §18-14 소환수 재소환 구현 승인 · G16/G17 출시 게이트 큐 등록 · G15 Steam Cloud/§6 오프라인 전투 정산/§14 동맹 쿨다운 보류. 오너가 터미널에서 `claude` 1회 로그인하면 체인이 자동으로 claude 우선 복귀. 사람 관문 더미 유지.





**정정 요약**: 직전 STATUS의 「AuctionHud·BagText 리크」는 오판으로 정정 — 유일 호출부가 QA 전용 게이트 뒤라 일반 플레이 리크 없음(`4dc069f4`). UI 폴리싱 캠페인 7화면 마감. 소비처0 재스캔 결과 §18-14 소환수 재소환은 신규 시스템 필요 → 오너 위임 승인 완료(INBOX 2026-08-25 밤). 원문 전문은 아카이브 참조.



## 관문 부채 (루프 밖 · 사람/대화 세션)



> 원장 §22 운영규칙 3: 소비처0 루프는 로드맵이 아님. 사람 관문(V2·V3·V4 70%·관문②)은 더미로 진행(오너 2026-08-24). W2 FAIL은 사람 관문이 아니다.



| 관문 | 상태 | 담당 | 재개 트리거 |

|---|---|---|---|

| — | ~2026-08-25 | **반복 재검증 통합 기록** — proposals-triage(최대 103차)·lane-doc(96차)·keeper-warn-chip(89차) 등 동일 지시 반복 배정 재검증 누적. 전 행 판정: 선반영 확인·변경 없음·중복 병합 대상 없음. 건너뛰기 처방(`a27b3d2b`)의 TASKS.json 적용은 미해결 — 별도 추적 | 세부 이력 아카이브 참조 · 이 커밋 |

| — | 2026-08-24 | **UI 폴리싱 — 하단 도크 활성 탭 라벨 회색 결함 수정 + §10-3 선반영 판정** — 플레이모드 순회 실측(영지·탑·필드·월드맵·파티)에서 유일 결함 확정: 현재 화면 탭 라벨이 `GUI.enabled = !here` 비활성 알파로 회색 처리돼 위 금강조선과 정반대로 「막힌 탭」처럼 읽힘(비허브 파티 화면은 전 라벨 밝아 경로 입증). 처방: DrawAtlasButton 뒤 GUI.enabled 복구(아이콘·라벨 선명)·클릭 금지는 !here 가드 보존·네거티브 `QA_NO_DOCK_ACTIVE_BRIGHT`. §10-3 계열 상성은 `a7f82e6a` 선반영(`FamilyAdv` 1.3/0.7·SelfCheck·DungeonScreen 소비처 실측)으로 재구현 없이 닫음 | 컴파일 PASS(에러 0) · 플레이모드 A/B 실측 `output/qa/ashes-to-stars/dock_active_shots/`(before_field 회색 → after_field 흰색 크롭 육안) · 네거티브 neg_field(옛 흐림 재현) · 콘솔 에러 0 · `777eacab` |

| 단계1 관문② (5h 지루함) | **더미 관문 PASS** 9/10 (5h+숙제감≤2) | 더미 | 시드 20260823 · `v4_dummy_sim.py` · 실측 표본 오면 §22 재측정 |

| V4 영구삭제 수용성 | **더미 관문 PASS** 즉시계속 10/10 · 24h 10/10 · human_70=`dummy-pass` | 더미 | 실측 표본 오면 §21-1 재측정. 보고서 `output/qa/ashes-to-stars/v4_playtest_dummy/dummy_report.json` |

| V2 손맛 · V3 | 더미 V2 **PASS** 5/5 · V3 **FAIL** 3/5 (t03 기믹 1종 · t04 단축 미달) | 더미 | 사람 대기 종료. V3 FAIL은 공식 더미 결과(기준 낮추지 않음) |

| W2 FAIL (회피 기회) | FAIL 유지 · 기준 낮추지 말 것 | 대화 세션 | 위협밀도·대시 손맛 손대기 직전 → **더미 진행 승인**(오너 2026-08-25 밤): 더미 구현 완료 — **판정 FAIL**(지배전략·숙련보상 미달, 시드 20260825 결정론) |



## 다음 할 일 (원장 §22 — 위에서부터 하나만)
1. **ORDERS ① 유령 과제 종결 정리** — 승인. CombatStyle 실소비·UseFixedStyle·보스 쫄 소환을 코드 근거로 종결, `JobDef.이동기형태` 소비처 정리.
2. **§18-14 소환수 재소환 + 큐#1 필드 정예 훅** — 승인(오너 위임). 재소환 0.5G/h·쿨다운 30초 신규 시스템 / W3Party 정예 처치 1회를 `EliteDrop`·`GameState`로 넘기는 최소 훅(판정 로직 변경 금지·QA_NO 네거티브).
3. **기획서 ✅ · 소비처 0곳** — 원장 `GAME_DESIGN_ASHES_TO_STARS.md`를 훑어 ✅인데 grep 소비처가 0인 칸 **하나만** 닫는다. **§10-3 계열 상성은 닫음**(`a7f82e6a` 선반영 + `777eacab` 판정 + `927ce693` GameSweep 등록·재실측 PASS). §10-5 보스 스킬 수는 닫음(`BossSkills` 선반영 — SelfCheck 25/25 PASS, 2026-08-24 판정). **남은 알려진 오픈 셀 없음 — 다음 바퀴는 원장 재스캔으로 새 칸 발굴.** §18-11 잡몹 피해(MobDmg `fc2499d8`)·§18-11 잡몹 HP(MobHp `6f1e1226`)·§18-11 잡몹 이동속도(MobSpeed `8c89e69b`)·§18-8 PvE 회복시간(PveRecover `cff20f97`)·§4 PvP 회복시간(PvpRecover `903a1cf7`)·§18-11 플레이어 이동속도(MoveSpd `be1882cd`)·§18-2 소각목표(BurnTarget `470f416c`)·§18-1 티어1시간당골드(GhAnchor `12e3e0b4`)·§18-1 티어배율(TierMul `9de769f8`)·§10-9 투사체상한(ProjCap `89e7136d`)·§10-9 소환수상한(SummonCap `bfff2789`)·§10-9 잡몹상한(PerfCap `31836976`)·§4 사망상한(DeathCap `f45cd729`)·§4 부활초소지상한(ReviveCap `9b50c0d7`)·§3 전투 스타일 정예우선타겟·소모품자동사용(StyleScreen.ToggleLine `305bee56`)·§3 SkillDef.초필살기(SkillUltLine `24ef7e47` — 이번 바퀴 선반영 확인)·§18-13 별 인식(`1 + 층/10`)·§18-13 별 크기(`1 + 층×0.02`)·§18-11 대보스 개체 HP(2체 65·3체 45)·§10-2 정예 유형 1~2종(지도 Caption)·§10-7 탑 대보스 마릿수(60/30/10)·§18-10 레이드 벽(5층 ×1.5·10층 ×2.2)·§18-4 목숨 시세 상한(부활초 8·두루마리 4·환생석 300)·증표 시세 상한 400·§11 드랍 옵션 체력(`GearOpt.HpMul`→`EffectiveHpMul`)·경매 복원 등급·옵션·§13-3 창고 현재 칸 경로·§11 드랍 옵션 1~4·§10-8 정예 일반·보스 고급 장비·가방 60칸·무기 직업 계열·진입 면 선택·§18-9 RaceDef.전투당발동(`f9f195e8`)·§3 SkillDef.쿨다운(속성 SkillLine `92cc2feb`)·§18-9 RaceDef.이속배율(속성 SpeedLine `dbe02f57`)·§18-9 RaceDef.체력배율(속성 HealthLine `e4fd1d15`)·§18-9 RaceDef.방어배율(속성 DefenseLine `ead55a1d`)·§3 SkillDef.위력배율(속성 SkillLine ×P `50202ce5`)·§3 SkillDef.반경(SkillLine `e4557f35`)·§3 SkillDef.자원소모(SkillLine `564baaf8`)·§3 SkillDef.설명(SkillDescLine `017335fc`)은 닫음. 시각 UI「다른 게임만큼」·V2 손맛·V4 70%는 사람 관문이라 닫지 않는다. `W3Party`·오너 Unity는 건드리지 않는다. **중복 리소스 재생성 금지.** 통과 기준: 소비처 ≥1 + SelfCheck + 네거티브. 증거 없는 완료 금지. **선반영 감지 시 재구현 금지·닫음 판정 후 다음 칸으로**(PROPOSALS 병합 항목 처방). 직전 트랙이 UI면 이번은 이 칸. 직전 트랙이 코드면 **UI·아트 상시 폴리싱**.
4. **UI·아트 상시 폴리싱** — 최신 정상 샷에서 절단·겹침 한 곳만(다음 후보: GameScreen Body 절단). 「다른 게임만큼」완료 금지. 기존 닫음 나열은 아카이브 참조.
5. **W2 손맛(회피 기회)** — 더미 판정 FAIL 유지(`loop/w2_dummy_sim.py`, 2026-08-25). 오너 직접 플레이 후 위협밀도·대시 지시 필요.

## 다음 할 일 큐 (루프가 못 닫은 것)

| # | 항목 | 이유 |

|---|---|---|

| 1 | **필드 정예·전투 보정** | 킬 카운트가 `W3Party` 안에만 있다. 루프는 W3Party를 안 만진다. 선행: 대화 세션이 필드 정예 처치 1회를 `EliteDrop`/`GameState`로 넘기는 훅 |

| 2 | **30층 성장 곡선** | 닫음. `BossHp` + 재측정 PASS |

| 3 | **영지 §5 드래그 UX** | 닫음. SelfCheck PASS · `6d9b4fae` |

| 4 | **환생·탐험·내구·수비명예·착용레벨** | **닫음.** 환생 `86485fd7` · 탐험 `8c0b4b4a` · 내구 `5213c8bc` · 수비명예 `cd21fa0c` · 착용레벨 `4d9bc2d2`(요구레벨 기본 0, 오너 수치표는 안 만듦) |
| 5 | **G3 권장 파티 픽스처 고도화 후 재판정** | 첫 실측에서 베어 로스터 전멸(실측0 vs 필요3076). BossHp 권장 전투력은 장비·합성·가호를 「흡수」한 기대치 — G3 픽스처에 실제 Equipment·Fusion CombatMuls 경로를 심어 정의 일치 후 재판정 |



## 최근 완료 내역 (History)



| 바퀴 | 일시 | 작업 내용 | 검증 결과 / 커밋 |
| — | 2026-08-26 08:35 | **ORDERS③ 측정 하네스 50층 확장 + G3 파티 실DPS 대조 게이트** — `TowerClimbCurveMeasure` TopFloor 30→50, 실판 시뮬(`W3Party.Step` 리플렉션 경로 — BossAutoAttackSelfCheck와 동일 경계)로 기대 레벨·전직 파티의 실측 DPS를 필요 DPS와 대조하는 G3 신설 + 약한 파티(Lv1 기본직) 네거티브. CSV/JSON 파일명 TopFloor 연동. 첫 실측 **FAIL 3건 공식 기록**(기준 낮추지 않음): G2 5h→Lv47 vs 필요50(6.04h) · G3 권장 파티 전멸(실측0 vs 필요542@25층/3076@50층 — 장비·합성·가호 미포함 베어 로스터). W3Party 런타임 무변경 | 배치 실행 컴파일 에러 0 · JSON/CSV 산출 육안 `output/qa/ashes-to-stars/curve/tower_climb_50.{json,csv}` · 실판 로그 실측(편성 5인·스킬4·시간경과 사망) · 네거티브 PASS · `f5e6b1c3` |
| — | 2026-08-26 08:00 | **ORDERS② 파티 멤버별 전투 스타일 실소비 배선**(직전 바퀴 — STATUS 갱신 누락분 보완) — TickParty·TickMobs·TickShots가 `StyleFor(m.Style)` 개별 적용, UseFixedStyle·QA_NO_MEMBER_STYLE 삼항으로 측정 단일 경로 보존, MemberStyleSelfCheck 20건 + GameSweep 등록 | GameSweep 재실측 ok:true 63/63(`loop/last_test_report.json` 07:45) · `51c3e862` |
| — | 2026-08-25 21:40 | **W2 회피 기회 더미** — 오너 지시로 `loop/w2_dummy_sim.py` 신설(§21-1c 방법론 재현: 전략 3종 피격 수·대시 회피기회율, 에셋 수치 MobSpeed 0.90/0.85/0.65·MoveSpd 4.2). 판정 **FAIL** — 직선/원형 계층·숙련 보상 밴드 미달, 동일 시드 해시 일치로 결정론 입증. 리포트 `output/qa/ashes-to-stars/w2_playtest_dummy/w2_report.json`. 기준 낮추지 않음 | 동일 시드 2회 실행 digest 일치 · 이 커밋 |
| — | 2026-08-25 21:35 | **보드 표준 정리** — History 285행·폐기 포맷 「최신 바퀴」 4섹션·서술형 정정 원문을 `docs/archive/legacy_loop_docs_20260826/`로 이관, 반복 재검증 행 14건 요약 통합, 관문 부채 핵심 5행 복구 검증. board.py 파서 계약 유지(126테스트 중 125 PASS — 1건은 워커 커밋 `70db7ebf` 샌드박스 테스트의 ROOT 누출로 별도 이슈) | 정리 전후 test_board 비교 · 이 커밋 |
| — | 2026-08-25 21:30 | **루프 인프라 — 커밋 직전 공용 인덱스 가드(회의 채택 #3·PROPOSALS 09:49 상 이행)** — 시작 시점에도 공유 인덱스에 타 세션 스테이징이 살아 있어(실측 5건) 맨몸 커밋 혼입 위험이 상시. `loop/commit_guard.sh`: `git diff --cached --name-only`(GIT_INDEX_FILE 임시 인덱스 존중)가 허용 경로 집합과 정확히 같은지 강제하고, 불일치 시 혼입·누락 경로를 지목해 1로 끝난다(커밋 중단). temp-index 커밋 후 공유 인덱스 잔해는 `git reset -q -- <내 경로>`로만 닦음(타 세션 분 보존). 게임 코드 무변경(W3Party 무접촉) · 이미지 생성·블렌더 해당 없음 | 샌드박스 12/12 PASS(`loop/test_commit_guard.sh`) · 실공유 인덱스 네거티브(혼입 6건 지목 차단) + temp-index 포지티브 PASS · test_board 재실측 126/126 · `70db7ebf` |



| — | 2026-08-25 14:40 | **UI 폴리싱 — 영지 헤더 내부 절 번호 비노출.** 최신 실행 샷의 QA 헤더 끝에 플레이어에게 의미 없는 `§16`이 노출됐다. `EstateScreen.PlayerSubtitle`이 모든 영지 헤더의 `(§…)` 근거 표기를 제거하고 `QA_NO_ESTATE_HEADER_PLAYER_COPY=1`은 옛 노출을 재현한다. 표시 전용 — W3Party 무접촉. 이미지 생성·블렌더 건너뜀 | 컴파일 PASS(369소스 0) · EstateHudSelfCheck PASS · 실행 빌드 PASS(1,388,344,631 bytes) · 1280×720 A/B 육안 확인 `estate_header_player_copy_shots/{after2,neg}/auto_dungeon_map.png` · `bc7eb983` |

| — | 2026-08-25 08:25 | **UI 폴리싱 — 타이틀 로컬 테스트 상태 패널.** `QA_PLAY` 안내가 배경 위 작은 생텍스트라 상태 표시로 읽히지 않았다. 왼쪽 소개 열에 40px 금테 정보 패널로 통일하고 QA_NO는 옛 20px 생텍스트를 재현한다. 최초의 「종료 카드 가림」 가설은 A/B 샷에서 실제 겹침이 없어 폐기했다 | 컴파일 PASS · LocalPlayKitSelfCheck PASS · 실행 빌드 PASS · 1280×720 A/B 육안 확인 `title_local_kit_panel/{after,neg}/qa_go:Title.png` · `fbc0f375`/`ad3c7baf`/`84869544` |

| — | 2026-08-25 08:09 | **UI 폴리싱 — 마지막 목숨 경고 빈 장비 패널.** 장비 뒷줄이 빈 문자열이어도 필드·탑이 Info 4칸을 그려 빈 금테 한 줄이 남았다. 두 화면 모두 `GearRest`가 비어 있지 않을 때만 네 번째 패널을 그린다. 장착 6부위가 있으면 기존 두 줄 유지. 표시 전용 — W3Party 무접촉 | 컴파일 PASS(355소스 0) · LastLifeWarnSelfCheck PASS · 실행 빌드 PASS · 1280×720 `last_life_empty_row_shots/after_field/qa_go:Field.png` 육안 확인(빈 패널 0) · `e6cebda2` |

| — | 2026-08-25 05:55 | **§18-11 잡몹 피해 — 소비처 0곳.** MobDef.피해비율 기본 0.03(원장 2~4%=25~50대)이 에셋에만 있음. `MobDmg`가 읽고 속성 탭·던전 부제가 소비. QA_NO면 옛 0.03·줄 없음. `W3Party`는 안 만짐. 블렌더 꺼짐(3D 없음) | 컴파일 PASS(350소스 0) · MobDmgSelfCheck PASS 21/21 · GameSweep 행 추가 · 샷 `mob_dmg_shots/qa_go_Character.png`(속성 탭 「잡몹 피해 3%(§18-11)」) · 네거 `qa_negctrl_no_dmg.png`(QA_NO면 그 줄 없음·PvP 회복이 HP 다음) · `fc2499d8` |

| — | 2026-08-25 05:36 | **UI 폴리싱 — 허브 본문-내비 한 곳에서 자름.** NavReserve=80이면 Body yMax=640인데 내비 플레이트는 636이라 하단 금테 4px 겹침. `BodyNav.Fit`이 NavPlateTop-12. QA_NO_BODY_NAV면 옛 640. 화면마다 Hud.NavGap 복제를 끊음 | BodyNavSelfCheck PASS · GameSweep 행 추가 · `0fc2eb14` |

| — | 2026-08-25 05:25 | **§18-11 잡몹 HP — 소비처 0곳.** MobDef.체력배율 기본 1.2(원장 0.8~1.5=1~2타)가 에셋에만 있음. `MobHp`가 읽고 속성 탭·던전 부제가 소비. QA_NO면 옛 1.2·줄 없음. `W3Party`는 안 만짐. 블렌더 꺼짐(3D 없음) | 컴파일 PASS(346소스 0) · MobHpSelfCheck PASS 22/22 · GameSweep 행 추가 · 샷 `mob_hp_shots/qa_go_Character.png`(속성 탭 「잡몹 HP ×1.2(§18-11)」) · 네거 `qa_negctrl_no_hp.png`(QA_NO면 그 줄 없음) · `6f1e1226` |

| — | 2026-08-25 05:07 | **§18-11 잡몹 이동속도 — 소비처 0곳.** MobDef.속도배율(추적 0.90·포위 0.85·원거리 0.65)이 에셋에만 있음. `MobSpeed`가 읽고 속성 탭·던전 부제가 소비. QA_NO면 옛 표·줄 없음. `W3Party`는 안 만짐 | MobSpeedSelfCheck PASS · GameSweep 행 추가 · `8c89e69b` |

| — | 2026-08-25 04:52 | **§18-8 PvE 회복시간 — 소비처 0곳.** 에셋 기본 24시간인데 grep 소비처 0곳. `PveRecover`가 읽고 속성 탭·LifeSystem이 소비. QA_NO면 옛 24시간·줄 없음. `W3Party`는 안 만짐 | PveRecoverSelfCheck PASS · GameSweep 행 추가 · `cff20f97` |

| — | 2026-08-25 04:43 | **UI 폴리싱 — 영지 마을 팔레트-마름모.** 가운데 슬림 타일이 마름모 남단 오두막과 겹침(실측 estate_hud_nav_shots/after). `PaletteTiles`를 왼쪽 가장자리(EdgePad 8). QA_NO_YARD_PALETTE_EDGE면 옛 가운데. 표시 전용 — W3Party 무접촉. 블렌더 꺼짐(3D 없음) | 컴파일 PASS(340소스 0) · EstateHudSelfCheck PASS(도크 x 44 · 마지막 칸 334 < 가운데 640 · 차단 x 495) · 샷 `estate_palette_edge_shots/{after,neg}.png` · `37705577` |

| — | 2026-08-25 04:25 | **§4 PvP 회복시간 — 소비처 0곳.** 에셋 기본 12시간인데 grep 소비처 0곳. `PvpRecover`가 읽고 속성 탭·LifeSystem이 소비. QA_NO면 옛 12시간·줄 없음. `W3Party`는 안 만짐 | PvpRecoverSelfCheck PASS · GameSweep 행 추가 · `903a1cf7` |

| — | 2026-08-25 04:18 | **UI 폴리싱 — 탑 골드부족·마지막목숨·사망동의 선택 바-내비 12px.** DrawChoice가 본문 yMax=640에 붙어 내비(636)와 겹침. `TowerWarnHud.Content`를 NavPlateTop-12. QA_NO면 옛 겹침. 동의·골드·목숨 셋 다 Content. 블렌더 꺼짐(3D 없음) | 컴파일 PASS(338소스 0) · TowerWarnHudSelfCheck PASS(아랫변 624 · 간격 12 · 차단 아랫변 640) · GameSweep 행 추가 · 샷 `tower_warn_nav_shots/{after,neg}.png` · `c7077ab5` |

| — | 2026-08-25 04:02 | **§18-11 플레이어 이동속도 — 소비처 0곳.** 에셋 4.2인데 W2Arena·W3Party가 하드코딩해 grep 소비처 0곳. `MoveSpd`가 읽고 속성 탭이 소비. QA_NO면 옛 4.2·줄 없음. StatLine 붙이면 잘려 `90f65fd0`가 단독 행. `W3Party`는 안 만짐 | MoveSpdSelfCheck PASS · GameSweep 행 추가 · `be1882cd` · `90f65fd0` |

| — | 2026-08-25 03:47 | **UI 폴리싱 — 필드 골드부족·마지막목숨 선택 바-내비 12px.** DrawChoice가 본문 yMax=640에 붙어 내비(636)와 겹침. `FieldWarnHud.Content`를 NavPlateTop-12. QA_NO면 옛 겹침. 시드가 QA_NO에 꺼지던 구멍은 `78831df4`가 시드와 차단을 분리 | FieldWarnHudSelfCheck PASS · GameSweep 행 추가 · `9f553b44` · `78831df4` |

| — | 2026-08-25 03:34 | **§18-2 소각목표 — 소비처 0곳.** 에셋 45~55%가 authored인데 grep 소비처 0곳. `BurnTarget`이 읽고 속성 탭이 소비. QA_NO면 옛 45~55·줄 없음. `W3Party`는 안 만짐 | BurnTargetSelfCheck PASS · GameSweep 행 추가 · `470f416c` |

| — | 2026-08-25 03:13 | **§18-1 티어배율 — 소비처 0곳.** 에셋 기본 1.6인데 Economy가 ×1.6 거듭제곱 표를 하드코딩. `TierMul.Table`이 읽고 정산·비용·속성 탭·부제가 소비. QA_NO면 옛 표·줄 없음. `W3Party`는 안 만짐 | TierMulSelfCheck PASS · GameSweep 행 추가 · 샷 `tier_mul_shots/qa_go:Character.png`(부제 티어당 ×1.6) · 네거 `qa_negctrl_no_mul.png` · `9de769f8` |



## 오너 위임 판정 결산 (오너 확인용 · 2026-08-25)

| 항목 | 판정 | 현재 상태 |
|---|---|---|
| ORDERS ①유령과제 ②멤버별스타일 ③하네스50층+G3 | 전부 승인(P1 순서 ②→③①) | 랩 큐 — 순차 소화 중 |
| §18-14 소환수 재소환(0.5G/h·쿨30초) | 구현 승인(신규 시스템) | 랩 큐 대기 |
| 큐#1 필드 정예 훅(W3Party 최소 훅) | 루프 실행 승인(네거티브 필수) | 랩 큐 대기 |
| **W2 회피 기회** | **더미 구현 완료 → FAIL**(직선 도주 강함·원형 카이팅 밴드 초과, `loop/w2_dummy_sim.py` 시드 20260825 결정론) | 기준 유지 — 손맛 튜닝 후 재판정 |
| G16 로컬라이제이션·G17 접근성 | 출시 게이트 과제로 큐 등록 | 미착수 |
| G15 Steam Cloud / §6 오프라인 전투 정산 / §14 동맹 쿨다운 | 보류(외부 의존·설계 미확정·온라인 알파 이후) | 보류 유지 |
| 실행기 체인(스티키)·launchd env 잔재 제거 | 완료(`e48ea8d6`) | opencode 실랩 가동 중 |
| 반복 재검증 TASKS.json 처방 미적용 / 워커 테스트 REGRESSION(`70db7ebf` ROOT 누출) | 미해결 2건 — 추적 중 | 랩/다음 세션 처리 대상 |

## 막힌 것 · 보류
- W2 FAIL — 더미 판정 FAIL 유지(2026-08-25). 기준 낮추지 말 것 · 튜닝은 오너 플레이 후.
- V2/V3/V4 — 더미 관문 소화 완료 · 데모·EA 전 §21-6 실측 재판정.
- G15 Steam Cloud·§6 오프라인 전투 정산·§14 동맹 쿨다운 — 보류(외부 의존/설계 미확정/온라인 알파 이후).
- 미해결 추적 1건: 재검증 건너뛰기 처방 TASKS.json 미적용. (워커 `70db7ebf` 테스트 ROOT 누출은 재실측 해소 — test_board 126 OK, 2026-08-25 21:45)
- 전체 이력: `docs/archive/legacy_loop_docs_20260826/STATUS_history_20260825.md`

