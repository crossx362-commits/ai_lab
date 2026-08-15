# 재와 별 — 현재 위치 · 다음 할 일

> **인수인계서.** 매 이터레이션은 기억 없는 새 세션이다. 끝낼 때 이 파일을 갱신하지 않으면
> 다음 세션이 처음부터 다시 판단한다.
>
> 갱신 규칙: 완료로 내릴 때 **판정 근거(수치·커밋 해시)를 반드시 같이 적는다.**
> 근거 없는 완료는 다음 세션이 재검증해야 하므로 완료가 아니다.

최종 갱신: 2026-08-15 22:05 · 이터레이션(코드 트랙 — 캐릭터 성장/레벨업 §3·§18-6 반입: 키스톤 해금)

> **이번 이터 결과(코드): 캐릭터 성장(레벨·경험치) 반입 — §3 경험치 분배 + §18-6 레벨 곡선.**
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

**루프가 끊기는 지점**: 영지 하위 건물 4종이 전부 빈 화면이라 **성장→전직→합성 경로가
화면상 존재하지 않는다.** 탑 등반 풀 루프를 완성하려면 여기가 먼저다.

## 다음 할 일 큐 (맨 위부터 하나씩)

| # | 항목 | 통과 기준 | 네거티브 컨트롤 |
|---|---|---|---|
| 1 | **몹 AI 4계열 실루엣 재생성** ⭐ **4/4 반입 완료(chaser✅ charger✅ ranged✅ swarmer✅)** — 아트 종결, 인게임 hunt 확인만 GUI세션 대기 | §0-A 픽셀아트 화풍, **4 AI 실루엣** × 22프레임씩. 무채색 회색-백(색은 런타임 `MobDef.색조`). **swarmer 반입 완료**(montage 육안 — 다족 벌레형 포위형, attack 임팩트·death 뒤집힘, 4족/2족과 갈림, `game_asset_names` 통과, meta보존). **인게임 hunt 확인만 미완**(오너 useHub 락+unity_meas 배치중 — GUI/빌드 세션 인계) | 옛 아트로 되돌리면 캐릭터와 톤이 갈림. swarmer 재현: `git checkout HEAD -- .../mob_swarmer`(반입 커밋 전) |
| ~~2~~ | ~~**§10-5 보스 쫄 소환**~~ ✅ **통과 기준 충족** | 기믹 발동 배선은 `006564f8`가 이미 함(호출부 0곳 → `FireNextGimmick`). 이번 이터: **소환이 빈 GameObject가 아니라 진짜 W3Party 쫄을 파티 한복판에 스폰**해 실제로 때린다. 측정: 정상 **소환피해 24**(5마리 스폰·`shots/qa_boss_summon_on.png`에 파티 포위) | ✅ **`BOSS_NO_SUMMON=1` → 소환 0마리·소환피해 0** (`boss_summon_NEGCTRL.log` vs `_NORMAL.log`) |
| 2b | **보스 나머지 통합(HP·장판·힐체크)** — #2에서 갈라져 나옴 | ⚠️ **보스 HP가 파티 DPS로 안 깎인다** → 페이즈 전환·`OnBossDefeated`가 HP로 안 뜬다(보스 층 클리어가 보스 컴포넌트로는 아직 불가). **장판(FloorAOE)은 파티 피해 0**이고 위험 슬롯을 영영 물어 소환이 판당 1회만 터진다. **힐체크 `ReportDamageToActive` 호출부 0곳** → 요구회복 0으로 항상 통과 | 각 기믹 비활성 시 해당 효과 소멸 |
| ~~3~~ | ~~**§3 RaceDef 배선**~~ ✅ **이미 완료(2026-08-15 21:45 실측)** | `W3Party.cs:557-567`이 `Resources.LoadAll<RaceDef>("races")` → `_bHp*=체력배율`·`_bSpd*=이속배율` 소비. 소비처 ≥1 충족(§3·§18-9). INBOX C도 "살아있음"으로 확인 | `--race`로 종족 강제 시 로그 `[W3] 종족=… 체력×… 이속×…` 확인됨 |
| 4 | **§16 영지 하위 건물 3종(대장간·경매장·수비대)** | 영묘✅ 채움. 나머지 셋은 **소비 시스템이 없어** 건물별 정직 사유로 잠금(`ceb522c8`) — 채우려면 각각 장비·재료(§11)·거래서버(§12)·침략 배치소비(§13-5) 시스템이 선행. **소비처 없이 UI만 채우면 또 거짓말** | 되돌리면 3건물 제네릭 "내용 없음"으로 회귀 |
| ~~5~~ | ~~**§10-9 잡몹 상한**~~ ✅ **이미 완료** | `W3Party.cs:336 const int MAXM=500`(기획서 300~500 충족). 인게임 500체 fps는 GUI/빌드 세션이 재확인 | 그리드를 끄면 fps가 무너져야 함(측정 미완) |
| ~~6~~ | ~~**전투 스타일 UI**~~ ✅ **완료 (`967daa89`)** | `StyleScreen`(직업별 4종 선택·PlayerPrefs 저장)·파티 편성에 진입 버튼·W3Party가 저장값 사용. 검증 하네스는 `UseFixedStyle`로 일괄 지정 유지 | `UseFixedStyle=true`로 되돌리면 선택이 무시되고 전원 균형형이 된다 |
| 7 | **§3·§18-6 캐릭터 성장(레벨업)** — 이번 이터 **반입·컴파일통과**(상단 블록), 배치 SelfCheck·인게임은 GUI세션 대기 | XP가 전투 후 출전 파티에 레벨 비례로 쌓여 `100×Lv^2.2`에서 레벨업(상한 100). `LifeSystemSelfCheck` ⑧이 곡선·레벨업·상한·총합보존·재기동유지 단언 — **배치모드 `-executeMethod AshesToStars.LifeSystemSelfCheck.Run`으로 확인**. CharacterScreen에 Lv·EXP 진척 표시 | `ExpToNext`를 상수/`AddExp` no-op으로 되돌리면 레벨 고정·SelfCheck ⑧ FAIL |
| 8 | **§3 전직 시스템** (다음 후보) — 성장 반입으로 Lv20 전제가 도달 가능해짐. 1차 Lv20+전직재료5+시험, 2차 Lv50+재료20(§18-6). 전직 재료 드랍은 Economy에 이름만 있음 — 소비처 배선 필요 | 전직 시 스킬 2→4·직업 세분화가 실제로 반영. CharacterScreen 잠김 해제 | 전직 되돌리면 스킬 수·직업명 원복 |

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
