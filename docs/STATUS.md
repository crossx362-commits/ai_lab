# 재와 별 — 현재 위치 · 다음 할 일

> 인수인계서. 보드(`loop/board.py`)가 이 파일을 읽는다.
> 2026-08-23 빈 템플릿으로 갈리며 보드가 비었던 것을, 아카이브·WORKLOG 기준으로 복구.

최종 갱신: 2026-08-23 · proposals-triage 17차 검증(BASE `c8ee3679`)
마지막 트랙: 피드백 문서(proposals-triage — 16차 병합 선반영 재확인, 무변경)
폴리싱 다음: **캐릭터 속성 ConceptLine LabelClip** (SkillDesc wrap `6ba0a995` 닫음). 마법사 직업 특성은 이번 샷에서 끝 글자까지 보임 — 수호기사 등 긴 고유메커니즘은 미확인.

## 관문 부채 (루프 밖 · 사람/대화 세션)

> 원장 §22 운영규칙 3: 소비처0 루프는 로드맵이 아님. 아래는 루프가 닫지 않는 관문.

| 관문 | 상태 | 담당 | 재개 트리거 |
|---|---|---|---|
| 단계1 관문② (5h 지루함) | 규격 초안 · 측정 대기 | 사람 세션 · 루프=CSV훅 | 체크리스트 `docs/plans/GATE2_LOOP_CHECKLIST.md` · 설문=일시정지 오버레이 · 구현은 보드 배정 후 |
| V4 영구삭제 수용성 | §21-6 넘김 · 외부 표본 0 · **더임 리허설 완주**(`b812da86`: V2 PASS·V3 FAIL·V4 PASS, 실측 아님) | 사람 관문 | 데모·EA 전 / 사망 규칙 변경 시. 더임 보고서: `output/qa/ashes-to-stars/v4_playtest_dummy/dummy_report.json` |
| V2 손맛 · V3 | 사람 관문 | 사람 | 단계4 진입 전 §21-6과 함께 |
| W2 FAIL (회피 기회) | FAIL 유지 · **기준 낮추지 말 것** | 대화 세션 | 위협밀도·대시 손맛 손대기 직전 |

## 다음 할 일 (원장 §22 — 위에서부터 하나만)
1. **영지 §6 아트** — 닫음. SelfCheck+실루엣 PASS · `d461fbcb`. 아트 트랙 다음: 1차 전직 **수호기사** 전신 1장(증거 `b81ab754` 닫음). **지금 개발 직렬: UI·아트 상시 폴리싱**(직전=루프도구 코드) · 다음 화면은 아래 4번 ConceptLine LabelClip.

2. **INBOX 22:03·22:04** — 몹 알파·서포터 반쪽·던전 입장 부제는 닫음. 남은 한 결함: 이펙트 위치/알파·생성. FX PNG는 다른 세션이 이미 수정 중이라 겹치지 말 것. 한 결함만.
3. **기획서 ✅ · 소비처 0곳** — 원장 `GAME_DESIGN_ASHES_TO_STARS.md`를 훑어 ✅인데 grep 소비처가 0인 칸 **하나만** 닫는다. **다음 칸: §3 SkillDef.초필살기** (`SkillDef.초필살기` bool, 정의만 있고 런타임 소비처 0). §10-5 보스 스킬 수(중간 2→3·대보스 2→3→4·50층+ 2→3→4→5)·§10-3 계열 상성(×1.3/×0.7)·§18-13 별 인식(`1 + 층/10`)·§18-13 별 크기(`1 + 층×0.02`)·§18-11 대보스 개체 HP(2체 65·3체 45)·§10-2 정예 유형 1~2종(지도 Caption)·§10-7 탑 대보스 마릿수(60/30/10)·§18-10 레이드 벽(5층 ×1.5·10층 ×2.2)·§18-4 목숨 시세 상한(부활초 8·두루마리 4·환생석 300)·증표 시세 상한 400·§11 드랍 옵션 체력(`GearOpt.HpMul`→`EffectiveHpMul`)·경매 복원 등급·옵션·§13-3 창고 현재 칸 경로·§11 드랍 옵션 1~4·§10-8 정예 일반·보스 고급 장비·가방 60칸·무기 직업 계열·진입 면 선택·§18-9 RaceDef.전투당발동(`f9f195e8`)·§3 SkillDef.쿨다운(속성 SkillLine `92cc2feb`)·§18-9 RaceDef.이속배율(속성 SpeedLine `dbe02f57`)·§18-9 RaceDef.체력배율(속성 HealthLine `e4fd1d15`)·§18-9 RaceDef.방어배율(속성 DefenseLine `ead55a1d`)·§3 SkillDef.위력배율(속성 SkillLine ×P `50202ce5`)·§3 SkillDef.반경(SkillLine `e4557f35`)·§3 SkillDef.자원소모(SkillLine `564baaf8`)·§3 SkillDef.설명(SkillDescLine `017335fc`)은 닫음. 시각 UI「다른 게임만큼」·V2 손맛·V4 70%는 사람 관문이라 닫지 않는다. `W3Party`·오너 Unity는 건드리지 않는다. **중복 리소스 재생성 금지.** 통과 기준: 소비처 ≥1 + SelfCheck + 네거티브. 증거 없는 완료 금지. 직전 트랙이 UI면 이번은 이 칸. 직전 트랙이 코드면 **UI·아트 상시 폴리싱**.
4. **UI·아트 상시 폴리싱** — 코드 구멍 다음 이터에 화면 하나·결함 하나. 「다른 게임만큼」완료 금지. 할로우 화풍 강제 취소. 이미 있는 장을 쓰고, 있는 대상은 다시 뽑지 않는다. 샷+한 결함이 완료. 필드 지갑 `2147483647`·탑 2×2 전폭·월드맵 2×2 전폭·아틀라스 UV(heart/tower 이웃)·지갑 부제 줄바꿈·일정/저체력 도크 부제·탑 하위 레이드 도크 부제·탑 레이드(5층 단위) 도크 부제·월드맵 침략 카드 부제·월드맵 성계·랭킹 카드 부제·월드맵 수비대 카드 부제·배회 보스 도크 부제·레이드급 카드 부제는 닫음. HuntBoon 도크 `8a7e6b93`·글씨 `67664c3a`·던전 입장 부제는 닫음. 파티 헤더·삭제·사냥 시작·영지 본성·광산·창고 도크 부제는 닫음(`236d90c8`/`1e517667`/`4805b2a2`/`f4267ba7`/`453210d3`/`d15d72e0`). 영지 ShortCopper(건물칸·업비·골드단축·Busy단축) 닫음(`369ef98a`/`dae7471e`/`fa0fcb74`/`8cda2934`/`514100b1`/`d9c26d53`/`2979746a`/`b0078725`). 목숨 시세 LifePrice `3e11e823`·증표 시세 TokenPrice `70f4a5ba`·필드 시간당 HuntGoldHourLine `261c8a21`·지갑 WalletText `66a445ed`·캐릭터 SkillDescLine 우측 잘림 `6ba0a995`은 닫음. 다음 화면: 캐릭터 속성 ConceptLine LabelClip(수호기사 등 긴 고유메커니즘). HuntGoldLine 획득은 전투 결과라 루프 밖. 영지는 `docs/GAME_SPEC_ESTATE_BUILD.md`대로.
5. **INBOX 09:57 전체 그래픽 남은 것** — 필드 허브 HUD·전용 7건물·경매장 전용 그림·제목판 52·경매 전폭 막대·캐릭터창 3열·장비 라벨·현황 도크·도크 부제·마을 끌어 보기·굴려 확대는 닫음. 중복 리소스 재생성 금지. 캐릭터/몹 화질은 사람 육안. 새 생성 전 `ARTIFACT_INDEX`·대기 작업 확인.
6. **INBOX 08:47 지금 문제점** — 캐릭터·몹 움직임, 맵 전투에서 캐릭터/몹/배경 비율. 겹침은 대화 세션이 `fe2eb9c8`(필드 프랍)·`95886088`(파티 겹침)로 닫음. 움직임·비율은 `W3Party`라 대화 세션. UI 퀄리티 전체는 사람 육안. 금테·글씨 여백은 대화 세션 `0d8e50da`. 루프는 전투 밖만. 영지 전면 마을·영지 마을 HUD·필드 허브 HUD·전용 7건물·경매장 전용 그림·제목판 52·경매 전폭 막대·캐릭터창 3열·장비 라벨·현황 도크·도크 부제·마을 끌어 보기·굴려 확대는 닫음.
7. **UI 퀄리티 남은 것** — 하단 도크·격자 8×8·시작 2명·침략 보호막·수비대 회복·인간 PvE 18h·경매 수수료 7%·영지생산·드랍률·전직재료배율·약탈량·엘프 인식·영공 적 디버프·드워프 골드 소모·약탈 상한·사냥 시작 두 단계·시간당 수익 소프트캡·승자 최소 0.5 G/h·창고 20% 약탈·명예 +30·반복 침략 −80%·신규 계정 7일 구매 잠금·5층 전 비살상 훈련·하위 레이드 스케일 0.65·하위 레이드 보스 풀 10종·재입장 누진 ×1·×2·×4·×8·경매 등록 24시간 유찰·연체 2회 생산 압류·파산 건물 −1·비장착 30%·환생 Lv1·필드 사냥 골드·마지막 목숨 장착 6부위·영묘 추모(층·출전·원인·장착·마지막 동료)·수비대 30층 해금·영묘 첫 삭제 해금·대장간 1차 전직 해금·경매 드랍·제작만 거래·필드 자동사냥 일정·10층 대보스 0.15 G/h·대출 순자산(장비·영지)·필드 배회 보스·긴급 탈출 보상 포기·넓은 카드 글씨 가운데·목숨 시세 하한·누적 출전·영지 전면 마을·증표 시세 200 G/h·영지 마을 HUD·필드 허브 HUD·전용 7건물·경매장 전용 그림·제목판 52·경매 전폭 막대·캐릭터창 3열·장비 라벨·현황 도크·도크 부제·마을 끌어 보기·굴려 확대·긴급 탈출 수동 한정·명예 승리 방어력 비례·침략 진입 면 선택·무기 직업 계열·가방 60칸은 닫음. 허브 마을 전경은 `9f4336f8`. 금테 여백은 `0d8e50da`. 전체 「다른 게임만큼」은 사람 육안.

## 다음 할 일 큐 (루프가 못 닫은 것)
| # | 항목 | 이유 |
|---|---|---|
| 1 | **필드 정예·전투 보정** | 킬 카운트가 `W3Party` 안에만 있다. 루프는 W3Party를 안 만진다. 선행: 대화 세션이 필드 정예 처치 1회를 `EliteDrop`/`GameState`로 넘기는 훅 |
| 2 | **30층 성장 곡선** | 닫음. `BossHp` + 재측정 PASS |
| 3 | **영지 §5 드래그 UX** | 닫음. SelfCheck PASS · `6d9b4fae` |
| 4 | **환생·탐험·내구·수비명예·착용레벨** | 선행이 없어 지금 넣으면 오펀 |

## 최근 완료 내역 (History)
| 바퀴 | 일시 | 작업 내용 | 검증 결과 / 커밋 |
|---|---|---|---|
| — | 2026-08-23 | proposals-triage 17차 검증(BASE `c8ee3679`) — 16차 병합(`a27b3d2b`) 선반영으로 PROPOSALS.md 현행 항목 3건(반복 배정 방지 병합(중)·board.py 더임 판정 표시(하)·PROFILE 키트 이동(하), 서로 상이): 중복 병합 대상 없음·전 항목 끝에 우선순위(상/중/하) 태그 존재로 빠진 것 없음·보정 불필요 · 기존 내용 삭제 없이 정리 마커 추가. 같은 지시 반복 배정 누적 — agent_runner 건너뛰기 제안(21:34 항목) 채택 시 해소 | tests 없음 · 문서 검증 · `a27b3d2b` 선반영 |
| — | 2026-08-23 | lane-doc 13차 재검증(BASE `c8ee3679`) — 속도 레인 운영법(병렬 worker/reviewer · autonomous/integration 적립 · 바퀴마다 master 흡수)은 `55fc2373` 선반영으로 README.md(80~85행·본문 4줄 ≤5줄)와 loop/README.md(15행·STOP_LANE 토글 48~50행)에 존속 확인. 재구현 없음 · 두 파일 무변경. 같은 지시 13회 반복 배정 — TASKS.json 큐 중복 제거 필요 | tests 없음 · 문서 검증 · 이 커밋 |
| — | 2026-08-23 | keeper-warn-chip 13차 재검증(BASE `c8ee3679`) — kChip warns→hold 톤·실패 칩 경고 병기는 `0ec68f11` 선반영(HEAD 조상 확인)으로 현재 board.html(921~929행)에도 존속(`kWarns` 필터·hold 노란 톤·실패 칩 경고 병기 확인). 재구현 없음 · loop/board.html 무변경. 같은 지시 13회 반복 배정 — 반복 배정 방지 제안은 `a27b3d2b`에서 병합됨. 행 최초 기록은 `66e8e18e`에 병행 편입됐다 이 커밋으로 귀속 정리 | test_board 102 OK · 이 커밋 |
| — | 2026-08-23 | proposals-triage 16차 검증(BASE `8149db96`) — PROPOSALS.md 현행 항목 4건 중 반복 배정 방지 제안 2건(agent_runner 건너뛰기 21:34(중)·keeper-warn-chip 선반영 감지 22:5x(중))이 동일 문제(무변경 랩 누적)·동일 처방이라 1건으로 병합 — 관찰·처방 전부 병합 항목에 보존. 병합 후 3건 모두 끝에 우선순위 태그 존재 · 기존 내용 삭제 없음 | tests 없음 · 문서 검증 · `a27b3d2b` |
| — | 2026-08-23 | lane-doc 12차 재검증(BASE `8149db96`) — 속도 레인 운영법(병렬 worker/reviewer · autonomous/integration 적립 · 바퀴마다 master 흡수)은 `55fc2373` 선반영으로 README.md(80~85행·본문 4줄 ≤5줄)와 loop/README.md(15행·STOP_LANE 토글 48~50행)에 존속 확인. 재구현 없음 · 두 파일 무변경. 같은 지시 12회 반복 배정 — TASKS.json 큐 중복 제거 필요 | tests 없음 · 문서 검증 · 이 커밋 |
| — | 2026-08-23 | keeper-warn-chip 12차 재검증(BASE `8149db96`) — kChip warns→hold 톤·실패 칩 경고 병기는 `0ec68f11` 선반영으로 현재 board.html(921~929행)에도 존속(`kWarns` 필터·hold 노란 톤 확인). 재구현 없음 · loop/board.html 무변경. 같은 지시 12회 반복 배정 — TASKS.json 큐 중복 제거 필요 | test_board 102 OK · 이 커밋 |
| — | 2026-08-23 | keeper-warn-chip 11차 재검증(BASE `d8bd8ead`) — kChip warns→hold 톤·실패 칩 경고 병기는 `0ec68f11` 선반영으로 현재 board.html(921~929행)에도 존속(`kWarns` 필터·hold 노란 톤 확인). 재구현 없음 · loop/board.html 무변경. 같은 지시 11회 반복 배정 — TASKS.json 큐 중복 제거 필요 | test_board 102 OK · 이 커밋 |
| — | 2026-08-23 | keeper-warn-chip 10차 재검증(BASE `6f84a139`) — kChip warns→hold 톤·실패 칩 경고 병기는 `0ec68f11` 선반영으로 현재 board.html(922~929행)에도 존속(`kWarns` 필터·`--hold` #e0a050 톤 확인). 재구현 없음 · loop/board.html 무변경. 같은 지시 10회 반복 배정 — TASKS.json 큐 중복 제거 필요 | test_board 102 OK · 이 커밋 |
| — | 2026-08-23 | proposals-triage 15차 검증(BASE `6f84a139`) — PROPOSALS.md 현행 항목 3건(agent_runner 건너뛰기(중)·board.py 더임 판정 표시(하)·PROFILE 키트 이동(하), 서로 상이): 중복 병합 대상 없음·우선순위(상/중/하) 태그 전 항목 존재로 보정 불필요 · 기존 내용 삭제 없이 정리 마커 추가. 같은 지시 반복 배정 누적 — agent_runner 건너뛰기 제안(21:34 항목) 채택 시 해소 | tests 없음 · 문서 검증 · `1dedc77c` |
| — | 2026-08-23 | lane-doc 11차 재검증(BASE `f80df219`) — 속도 레인 운영법(병렬 worker/reviewer · autonomous/integration 적립 · 바퀴마다 master 흡수)은 `55fc2373` 선반영으로 README.md(80~85행·본문 4줄 ≤5줄)와 loop/README.md(15행·STOP_LANE 토글 48~50행)에 존속 확인. 재구현 없음 · 두 파일 무변경. 같은 지시 11회 반복 배정 — TASKS.json 큐 중복 제거 필요 | tests 없음 · 문서 검증 · 이 커밋 |
| — | 2026-08-23 | proposals-triage 14차 검증(BASE `f80df219`) — PROPOSALS.md 현행 항목 3건(agent_runner 건너뛰기(중)·board.py 더임 판정 표시(하)·PROFILE 키트 이동(하), 서로 상이): 중복 병합 대상 없음·우선순위(상/중/하) 태그 전 항목 존재로 보정 불필요 · 기존 내용 삭제 없이 정리 마커 추가. 같은 지시 반복 배정 누적 — agent_runner 건너뛰기 제안(21:34 항목) 채택 시 해소 | tests 없음 · 문서 검증 · `de18f3f4` |
| — | 2026-08-23 | keeper-warn-chip 9차 재검증(BASE `bb34dd19`) — kChip warns→hold 톤·실패 칩 경고 병기는 `0ec68f11` 선반영으로 현재 board.html(921~929행)에도 존속. 재구현 없음 · loop/board.html 무변경. 같은 지시 9회 반복 배정 — TASKS.json 큐 중복 제거 필요 | test_board 102 OK · 이 커밋 |
| — | 2026-08-23 | proposals-triage 13차 검증(BASE `bb34dd19`) — PROPOSALS.md 현행 항목 3건(agent_runner 건너뛰기(중)·board.py 더임 판정 표시(하)·PROFILE 키트 이동(하), 서로 상이): 중복 병합 대상 없음·우선순위(상/중/하) 태그 전 항목 존재로 보정 불필요 · 기존 내용 삭제 없이 정리 마커 추가. 같은 지시 반복 배정 — agent_runner 건너뛰기 제안(21:34 항목) 채택 시 누적 해소 | tests 없음 · 문서 검증 · 이 커밋 |
| — | 2026-08-23 | keeper-warn-chip 8차 재검증(BASE `023899de`) — kChip warns→hold 톤·실패 칩 경고 병기는 `0ec68f11` 선반영으로 현재 board.html(921~929행)에도 존속. 재구현 없음 · loop/board.html 무변경. 같은 지시 8회 반복 배정 — TASKS.json 큐 중복 제거 필요 | test_board 102 OK · 이 커밋 |
| — | 2026-08-23 | lane-doc 10차 재검증(BASE `bb34dd19`) — 속도 레인 운영법(병렬 worker/reviewer · autonomous/integration 적립 · 바퀴마다 master 흡수)은 `55fc2373` 선반영으로 README.md(80~85행·본문 4줄 ≤5줄)와 loop/README.md(15행·STOP_LANE 토글 48~50행)에 존속 확인. 재구현 없음 · 두 파일 무변경. 같은 지시 10회 반복 배정 — TASKS.json 큐 중복 제거 필요 | tests 없음 · 문서 검증 · 이 커밋 |
| — | 2026-08-23 | proposals-triage 12차 검증(BASE `023899de`) — PROPOSALS.md 현행 항목 3건(agent_runner 건너뛰기(중)·board.py 더임 판정 표시(하)·PROFILE 키트 이동(하), 서로 상이): 중복 병합 대상 없음·우선순위(상/중/하) 태그 전 항목 존재로 보정 불필요 · 기존 내용 삭제 없이 정리 마커 추가 | tests 없음 · 문서 검증 · 이 커밋 |
| — | 2026-08-23 | lane-doc 9차 재검증(BASE `023899de`) — 속도 레인 운영법(병렬 worker/reviewer · autonomous/integration 적립 · 바퀴마다 master 흡수)은 `55fc2373` 선반영으로 README.md(80~85행·본문 4줄 ≤5줄)와 loop/README.md(15행·STOP_LANE 토글 48~50행)에 존속 확인. 재구현 없음 · 두 파일 무변경. 같은 지시 9회 반복 배정 — TASKS.json 큐 중복 제거 필요 | tests 없음 · 문서 검증 · 이 커밋 |
| — | 2026-08-23 | proposals-triage 11차 검증(BASE `4e8f1717`) — PROPOSALS.md 현행 항목 3건(agent_runner 건너뛰기·board.py 더임 판정 표시·PROFILE 키트 이동, 서로 상이): 중복 병합 대상 없음·우선순위(상/중/하) 태그 전 항목 존재로 보정 불필요 · 기존 내용 삭제 없이 정리 마커 추가 | tests 없음 · 문서 검증 · 이 커밋 |
| — | 2026-08-23 | keeper-warn-chip 7차 재검증(BASE `4e8f1717`) — kChip warns→hold 톤·실패 칩 경고 병기는 `0ec68f11` 선반영으로 현재 board.html(921~929행)에도 존속. 재구현 없음 · loop/board.html 무변경. 같은 지시 7회 반복 배정 — TASKS.json 큐 중복 제거 필요 | test_board 102 OK · 이 커밋 |
| — | 2026-08-23 | lane-doc 8차 재검증(BASE `4e8f1717`) — 속도 레인 운영법(병렬 worker/reviewer · autonomous/integration 적립 · 바퀴마다 master 흡수)은 `55fc2373` 선반영으로 README.md(80~85행·본문 4줄 ≤5줄)와 loop/README.md(15행·STOP_LANE 토글 48~50행)에 존속 확인. 재구현 없음 · 두 파일 무변경. 같은 지시 8회 반복 배정 — TASKS.json 큐 중복 제거 필요 | tests 없음 · 문서 검증 · 이 커밋 |
| — | 2026-08-23 | proposals-triage 10차 검증(BASE `ff843c4f`) — PROPOSALS.md 현행 항목 1건: 중복 병합 대상 없음·우선순위(상/중/하) 태그 전 항목 존재로 보정 불필요 · 기존 내용 삭제 없이 정리 마커 추가 | tests 없음 · 문서 검증 · 이 커밋 |
| — | 2026-08-23 | keeper-warn-chip 6차 재검증(BASE `ff843c4f`) — kChip warns→hold 톤·실패 칩 경고 병기는 `0ec68f11` 선반영으로 현재 board.html(921~929행)에도 존속. 재구현 없음 · loop/board.html 무변경. 같은 지시 6회 반복 배정 — TASKS.json 큐 중복 제거 필요 | test_board 102 OK · 이 커밋 |
| — | 2026-08-23 | INBOX 20:34 외부 테스터 더임 리허설 — `loop/v4_dummy_sim.py`(결정론 시뮬+시험지 기준 판정)·더임 키트 t01~t10·회귀 5건. 결과: V2 PASS 5/5 · V3 FAIL 3/5 · V4 PASS 10/10, human_70 pending 유지, live 키트(아나) 무변경 | test_v4_playtest 16 OK · test_board 102 OK · 시드 재현 확인 · `b812da86` |
| — | 2026-08-23 | lane-doc 7차 재검증(BASE `ff843c4f`) — 속도 레인 운영법(병렬 worker/reviewer · autonomous/integration 적립 · 바퀴마다 master 흡수)은 `55fc2373` 선반영으로 README.md(80~85행·본문 4줄 ≤5줄)와 loop/README.md(15행·STOP_LANE 토글 48~50행)에 존속 확인. 재구현 없음 · 두 파일 무변경 | tests 없음 · 문서 검증 · 이 커밋 |
| — | 2026-08-23 | keeper-warn-chip 5차 재검증(BASE `4c9b26d1`) — kChip warns→hold 톤·실패 칩 경고 병기는 `0ec68f11` 선반영으로 현재 board.html(909~918행)에도 존속(작업 트리 문구 개선 판에서도 로직 유지). 재구현 없음 · loop/board.html 무변경. 같은 지시 5회 반복 배정 — TASKS.json 큐 중복 제거 필요 | test_board 102 OK · 이 커밋 |
| — | 2026-08-23 | lane-doc 6차 재검증(BASE `4c9b26d1`) — 속도 레인 운영법(병렬 worker/reviewer · autonomous/integration 적립 · 바퀴마다 master 흡수)은 `55fc2373` 선반영으로 README.md(80~85행)·loop/README.md에 존속 확인. 재구현 없음 · 두 파일 무변경. 같은 지시 6회 반복 배정 — TASKS.json 큐 중복 제거 필요 | tests 없음 · 문서 검증 · 이 커밋 |
| — | 2026-08-23 | proposals-triage 9차 검증(BASE `4c9b26d1`) — PROPOSALS.md 현행 항목 1건: 중복 병합 대상 없음·우선순위(상/중/하) 태그 전 항목 존재로 보정 불필요 · 기존 내용 삭제 없이 정리 마커 추가 | tests 없음 · 문서 검증 · 이 커밋 |
| — | 2026-08-23 | keeper-warn-chip 4차 재검증(BASE `3f88250b`) — kChip warns→hold 톤·실패 칩 경고 병기는 `0ec68f11` 선반영으로 현재 board.html(909~918행)에도 존재(작업 트리 문구 개선 판에서도 로직 유지 확인). 재구현 없음 · loop/board.html 무변경. 같은 지시 4회 반복 배정 — TASKS.json 큐 중복 제거 필요 | test_board 102 OK · 이 커밋 |
| — | 2026-08-23 | proposals-triage 8차 검증(BASE `3f88250b`) — PROPOSALS.md 현행 항목 1건: 중복 병합 대상 없음·우선순위(상/중/하) 태그 전 항목 존재로 보정 불필요 · 기존 내용 삭제 없이 정리 마커 추가 | tests 없음 · 문서 검증 · 이 커밋 |
| — | 2026-08-23 | proposals-triage 7차 검증(BASE `bfe3c887`) — PROPOSALS.md 현행 항목 1건: 중복 병합 대상 없음·우선순위(상/중/하) 태그 전 항목 존재로 보정 불필요 · 기존 내용 삭제 없이 정리 마커 추가 | tests 없음 · 문서 검증 · 이 커밋 |
| — | 2026-08-23 | keeper-warn-chip 3차 재검증(BASE `bfe3c887`) — kChip warns→hold 톤·실패 칩 경고 병기는 `0ec68f11` 선반영 확인(board.html:909-916), 재구현 없음 · loop/board.html 무변경. 참고: 같은 작업 지시가 3회 반복 배정됨 — TASKS.json 큐 중복 점검 필요 | test_board 102 OK · 이 커밋 |
| — | 2026-08-23 | lane-doc 재검증(BASE `bfe3c887`) — 속도 레인 운영법(병렬 worker/reviewer · autonomous/integration 적립 · 바퀴마다 master 흡수)은 `55fc2373` 선반영으로 README.md·loop/README.md 무변경 | tests 없음 · 문서 검증 · 이 커밋 |
| — | 2026-08-23 | keeper-warn-chip 재검증(BASE `47b9e7ca`) — kChip warns→hold 톤 확장은 `0ec68f11` 선반영으로 재구현 없음 · loop/board.html 무변경 | test_board 102 OK · 이 커밋 |
| — | 2026-08-23 | proposals-triage 6차 검증(BASE `47b9e7ca`) — PROPOSALS.md 현행 항목 1건: 중복 병합 대상 없음·우선순위(상/중/하) 태그 전 항목 존재로 보정 불필요 · 기존 내용 삭제 없이 정리 마커 갱신(0건 기준 옛 마커는 유효각주 부여) | tests 없음 · 문서 검증 · 이 커밋 |
| — | 2026-08-23 | lane-doc 재검증(BASE `47b9e7ca`) — 속도 레인 운영법(병렬 worker/reviewer · autonomous/integration 적립 · 바퀴마다 master 흡수)은 `55fc2373` 선반영으로 README.md·loop/README.md 무변경 | tests 없음 · 문서 검증 · 이 커밋 |
| — | 2026-08-23 | proposals-triage 5차 검증(BASE `eb2b99ea`) — PROPOSALS.md 제안 항목 0건 재확인(`^- \[` grep 0), 중복 병합·우선순위(상/중/하) 보정 대상 없음 · 기존 내용 삭제 없음 · §⑤ 자가학습 1건 추가(반복 무변경 랩 방지 제안, 우선순위 중) | tests 없음 · 문서 검증 · 이 커밋 |
| — | 2026-08-23 | keeper-warn-chip 재검증(BASE `eb2b99ea`) — renderOps kChip 확장은 `0ec68f11`로 board.html에 이미 존재(warns→hold 톤·▲ 병기). 중복 재구현 없음 · loop/board.html 무변경 | test_board 102 OK · 이 커밋 |
| — | 2026-08-23 | lane-doc 재검증(BASE `eb2b99ea`) — 속도 레인 운영법(병렬 worker/reviewer · autonomous/integration 적립 · 바퀴마다 master 흡수)은 `55fc2373` 선반영으로 README.md·loop/README.md 무변경 | tests 없음 · 문서 검증 · 이 커밋 |
| — | 2026-08-23 | keeper-warn-chip 중복 확인 — 지정 작업이 기점 `9c31ca2a`에 이미 반영됨(`0ec68f11`: warns 있으면 hold 톤·실패 칩 경고 병기). 재구현 없이 종료 | test_board 102 OK · 코드 무변경 · 이 커밋 |
| — | 2026-08-23 | lane-doc 재검증 — 속도 레인 운영법은 `55fc2373`(BASE `9c31ca2a` 이전)에서 이미 반영돼 중복 작업 없음 · README.md(속도 레인 절 본문 4줄)·loop/README.md 무변경 | tests 없음 · 문서 검증 · 이 커밋 |
| — | 2026-08-23 | proposals-triage 4차 검증 — PROPOSALS.md 제안 항목 0건(`^- \[` grep 매치 없음), 중복 병합·우선순위 보정 대상 없음 · PROPOSALS.md 무변경(내용 삭제 없음) | tests 없음 · 문서 검증 · 이 커밋 |
| — | 2026-08-23 | proposals-triage 3차 검증 — PROPOSALS.md 형식 라인 grep 0건 재확인(원본 `30a5858b`부터 항목 0), 병합·우선순위 보정 대상 없음 · PROPOSALS.md 무변경(내용 삭제 없음) | tests 없음 · 문서 검증 · 이 커밋 |
| — | 2026-08-23 | proposals-triage 재점검 — PROPOSALS.md 제안 항목 0건(형식 라인 grep 0)이라 중복 병합·우선순위 보정 대상 없음 · PROPOSALS.md 무변경(선행 정리 `30cf1231` 유효, 내용 삭제 없음) | tests 없음 · 문서 검증 · 이 커밋 |
| — | 2026-08-23 | README 루프 섹션에 속도 레인 운영법 추가 (worktree 격리 병렬 worker/reviewer · autonomous/integration 적립 · 바퀴마다 master 흡수 · STOP_LANE 토글, 본문 4줄) | 요건 충족(5줄 이내) · loop/README.md는 기존 문서화로 무변경 · `55fc2373` |
| — | 2026-08-23 | §18-9 RaceDef.방어배율 소비처 DefenseLine(그록 바퀴 인수 완주) | compile PASS · RaceDefenseSelfCheck PASS · 네거 `QA_NO_RACE_DEFENSE` · 배치로그 `results/race_defense_selfcheck_20260823_201550.log` · `ead55a1d` |
| — | 2026-08-23 | 캐릭터 SkillDescLine 우측 잘림 InfoWrap | compile PASS · SkillDescSelfCheck PASS · 샷 `skill_desc_wrap_shots/qa_go:Character.png`(빙결: 광역 슬로우) · 네거 `qa_negctrl_no_wrap.png`(빙결: 광) · `6ba0a995` |
| — | 2026-08-23 | §3 SkillDef.설명 소비처 SkillDescLine | compile PASS · SkillDescSelfCheck PASS · 샷 `skill_desc_shots/qa_go:Character.png` · 네거 `qa_negctrl_no_desc.png` · `017335fc` |
| — | 2026-08-23 | 필드 지갑 WalletText ShortCopper | SelfCheck PASS · `66a445ed` |
| — | 2026-08-23 | §3 SkillDef.자원소모 소비처 | SelfCheck PASS · `564baaf8` |
| — | 2026-08-23 | 필드 시간당 HuntGoldHourLine ShortCopper | compile PASS · HuntGoldSelfCheck PASS · 샷 `hunt_gold_hour_shots/qa_go:Field.png` · `261c8a21` |
| — | 2026-08-23 | 증표 시세 TokenPrice ShortCopper | SelfCheck PASS · `70f4a5ba` |
| — | 2026-08-23 | 보드 STATUS 반영 복구 | 아카이브 큐 이관 |
| — | 2026-08-23 | EstateBuild §2-3 건물별 레벨·업그레이드 창 | SelfCheck PASS · `25559505`(+`5fa97195`/`355f4095`) |
| — | 2026-08-23 | 영지 §5 건물 드래그 미리보기 | SelfCheck PASS · `6d9b4fae` |

## 막힌 것 · 보류
- 위 **관문 부채** 표가 권위. 여기에는 한 줄 요약만.
- W2 FAIL — 기준 낮추지 말 것 · 담당 대화 세션
- V2/V3/V4 — 루프가 닫지 않음 · 데모·EA 전 재측정
- 전체 이력: `docs/archive/legacy_loop_docs_20260823/STATUS.md` · `docs/GAME_WORKLOG.md`
