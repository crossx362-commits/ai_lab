# ULON 자율 개발루프 — 인수인계

작업 트리 `/Users/junholee/ai_lab-loop`(브랜치 `loop-claude`), 푸시는 커밋 → `git pull --rebase -q origin master`
→ `git push -q origin HEAD:master`. `/Users/junholee/ai_lab`는 남의 트리(검수·Grok) — 건드리지 않는다.

랩 모양: 먼저 센다 → 하나 만들기 → 내 눈으로 검증(`SELFCHECK_LOG=$PWD/unity/Logs/selfcheck_dev.log
./tools/slice_selfcheck.sh` EXIT=0 두 판 + `./tools/qa_shots.sh` 뒤 PNG를 실제로 본다) → 커밋·푸시
(`git add -A` 금지, 경로 명시) → 검수 보고(끝에 「구조 한 줄」) → 이 파일 갱신.
안건 판정은 대장, 보고는 검수. 오너 직행은 셋(다운로드·되돌릴 수 없는 삭제·돈).

## 지금까지 (tip = 이 커밋)
지하감 ⓐ **문 뒤 언덕** 랩을 대장 조건 넷 그대로 닫았다. 원장 `Shared/EntranceGeom.cs`(세 입구·앞
방향·언덕 상수·`MoundRise`·NC용 `MoundDisabled`), 게이트 `Editor/SliceSelfCheck.MoundBehind.cs`
(문구멍 시선은 뚫림·문 위 3m 시선은 뒤 30m 안에서 막힘, NC는 「언덕이 앞당긴 거리 ≥3m」),
소품은 민 자리에서만 다시 앉힌다(`VisualSliceBuilder.SnapToGround`, `ClearPropsFrom*` 세 곳).

언덕 위 돌길도 닫았다(`26dda1b7`): 입구 앞길이 **문 뒤로** 그려지고 있었고(도포 규칙만 옛
`-Heading` 정의로 남아 있었다) 마을 스포크가 언덕을 탔다. 둘 다 `EntranceGeom` 원장으로 모으고
게이트 `SliceSelfCheck.RoadMound.cs`·셈 `OutdoorCensus.Road.cs`를 세웠다.

언덕 표면 도포·45° 문구멍은 **재보니 결함이 아니었다**(`ef84da0e`): 언덕은 바깥보다 모래가 적고
(크림빛은 마른풀), 어둠은 문틀 윤곽 안 0.0%(판별 테스트로 자가 살아 있음 확인 — 판을 1.2m 내밀면 11.5%).

자갈 겹도 닫았다(`81a9a30c`): 무늬가 아니라 **톤**이었다 — 절개면은 자갈 77~94%인데 화면에서
모래톱과 한 톤(186 대 238)이었다. 겹을 0.17~0.44로 내리고 게이트 `SliceSelfCheck.GravelTone.cs`
(자갈÷모래 밝기 비 ≤0.45, 결 ≥0.035)를 세웠다.

밭도 닫았다(`7822d3a5`): 뙈기를 미는 대신 **땅을 고른다**(`FarmPlots.Flatten` → `WorldTerrain.HeightAt`).
게이트 `SliceSelfCheck.FieldFlat.cs`(고저차 ≤2.5m, NC는 `FlattenDisabled`).

화면 인상 둘을 더 재서 기각했다(`1998062a`): 사냥터 바닥 얼룩(대비는 광산·밭보다 높다),
울타리-건물 관통(겹친 쌍 0, 판별 테스트 0.16m).

`13_d1_room_cutaway`도 고쳤다(`af3e3aa5`): 이름만 절단면이고 화면은 지표 잔디였다 —
찍는 동안 지형·뚜껑을 걷는다(`CutAway` 플래그 + `AssertCutAwayShotsUncover`).
**`VisualSliceBuilder.cs` 분할은 이미 끝났다 — 다시 열지 마라(검수 지시, 세 번째 재발).**
파일 크기는 옛 수치를 적지 말고 그때그때 센다:
`find unity/Assets/Game/Scripts -name '*.cs' -exec wc -l {} + | sort -rn | head`
큰 셋(`AttackResolve`·`VisualSliceBuilder.Npc`·`NetAvatar`)은 손대지 않는다.

밭 도포가 문 뒤 둔덕을 덮던 것도 닫았다: 검수의 「경사 탓」 짐작은 셈
(`OutdoorCensus.RunFieldSplatSlope`)으로 기각됐고(밭 1210칸 중 20°↑ 0개, 최급 17.0°)
진짜 원인은 **언덕과 겹친 밭 392칸(32%)**이었다. 길에만 쓰던 `WorldSplat.MoundFade`를
지역 도포에도 곱해 걷어낸 몫은 기본 잔디가 채운다(경계는 저절로 겹친다). 게이트
`SliceSelfCheck.RoadMound.cs`는 길만이 아니라 인공 지표 넷(Road·Tilled·Soil·Gravel)을 묻고,
`SliceSelfCheck.RegionSplat.cs` 표본은 굽는 쪽과 **같은 자**로 언덕을 뺀다(뺀 수 19곳을 찍고
0이면 죽은 예외로 실패).

`07` 등불 그림자도 **세어서 기각**했다: D1은 등불 둘 다 햇빛 막힘 0%,
문설주에 가리는 것은 D2 등불2(67%)·D3 등불1(73%)뿐이고 제 점광(3.2세기/9m)이 0.6m 옆이라
`09`·`11` 화면에서 둘 다 밝게 읽힌다. 자 `Editor/EntranceCensus.LampShade.cs`만 남기고 고치지 않았다.

「죽은 장」 훑기(검수 착수 승인)의 **세는 단계**를 마쳤다. 원장 `Editor/ShotSubject.cs`
(샷 → 주인공 오브젝트), 셈 `Editor/ShotCensus.RunSubjectShare`(주인공 실루엣 화면 점유율).
카메라는 찍는 쪽과 같은 목록을 읽는다 — 그러려고 `QaShots.Run`의 샷 배열을
`QaShots.BuildShots()`로 뽑았다(동작 변경 0, 07 샷 PNG 비트 동일).

실측(18장): **06_field_boss 0.1%** 하나만 죽었다(화면에서도 보스가 오른쪽 위 손톱만 하다 —
샷 좌표가 고정값이라 보스 자리를 안 따라간다). 나머지는 근접 5.8~9.0 · 마을 6.3~21.1 ·
지역 7.4~25.6 · 입구 14.7~24.8 · 실내 60.5~100. 지형이 주인공인 넷(14·15·16·63)은 **못 잼**.
**검수 지시대로 고치지 않고 목록만 보고**했다 — 하한·게이트는 판정 뒤에.

이어서 **게이트로 승격**했다(`SliceSelfCheck.ShotSubject.cs`, 하한 3% = 살아 있는 최저 5.8%의 절반).
죽은 장 하나(`06_field_boss`)도 되살렸다: 고정 좌표 궤도 → 크기에서 거리를 유도하는 근접
프레이밍(39·40과 같은 규칙), **0.1% → 4.9%**. NC 둘 — 숨기면 0.0%, 등 돌리면 0.0%.
NC 대상은 **몸 하나짜리 근접 샷**이어야 한다(실내 방은 카메라를 둘러싸 등을 돌려도 95%였다).

입구 근접 과노출도 닫았다. 셈 `Editor/ExposureCensus.RunEntranceExposure`가 같은 카메라에서
조건만 바꿔 나란히 렌더해 갈랐다 — `07` A 29.4 → **입구 점광만 끄면 0.9**(태양만 끈 판 2.4,
알베도 0.0). 처방 값도 스윕으로 골랐다(3.2→29.4 · 1.2→7.4 · **0.8→3.3** · 0.5→1.5):
세기 0.8이 야외 광각(2.3/1.8) 언저리이면서 평균 밝기 162(등불 끄면 154)라 등불이 제 몫을 한다.
자 `SliceSelfCheck.AssertEntranceNotBlownOut`(상한 8%, NC는 옛 세기 3.2 → 29.4%로 걸림)은
렌더가 필요해 `QaShots.Run` 끝에서 돈다. 곁가지로 `AssertMouthDark`의 NC가 **등불 밝기에
얹혀 있었음**이 드러나 스스로 빛나는 재질로 고쳤다(자가 약해진 게 아니라 NC가 남의 빛을 쓰고 있었다).

검수 반려 하나를 수용해 **하한을 부류별로** 고쳤다(전역 3%는 실내 절단면이 60→20%로 죽어도
통과했다). 표는 원장 `ShotSubject.MinShare`에 있다: 근접·마을·지역 3 / 입구 7 / 실내 30
(각 부류 실측 최저의 절반). 자는 그대로 하나이고, 로그는 몫이 아니라 **하한 대비 여유**로 최악을 고른다.

`06`도 과노출 표본에 넣었다(검수 관찰). **태우는 것은 입구 등불이 아니라 보스 오라**다 —
A 8.6% · 입구 점광만 끄면 8.6(무관) · 점광 전부 끄면 1.7. 이름을 대는 셈
(`ExposureCensus.BlameLights`)이 `BossAura` −6.9를 지목했다. 처방 자리는
`Editor/VisualSliceBuilder.Npc.cs:1196` 근처인데 **그 파일은 손대지 않기로 한 파일**이라
판정을 청해 두었다.

`06`의 이름-자리 어긋남도 닫았다. 셈 `OutdoorCensus.RunFieldBossPlace`가 **자리는 문제가
아님**을 보였다(원장과 어긋남 0.00m · 마을 중심 50.6m · 지역 밖). 갈리는 것은 방위뿐이고
(배경 마을 몫 0.5~8.5%), 보스는 +z를 보는데 마을은 −110°다. 그래서 프레이밍만 고쳤다 —
「마을 반대쪽을 배경으로」는 **틀린 처방**이었고(카메라가 마을 쪽에 서면서 보스가 등을 보였다),
**보스의 등 뒤를 배경으로**(`QaShots.BehindSubject`) 주면 정면·들판 배경이 함께 선다.

## 다음 착수
검수 판정 대기: `06` 포화 6.2%의 범인 `BossAura`(끄면 0.4) 처방 자리가
`Editor/VisualSliceBuilder.Npc.cs`(손대지 않기로 한 파일)다 — 내가 그 한 줄을 고칠지 판정 필요.
그 밖에 열려 있는 구멍 둘(감시자 없음): **강 자체**, **부두·원거리 절개면**. 야외·실내 샷을 돌며 새 안건을 세울 것. **인상은 가설이다** — 이번 세션에 화면에서
의심한 넷 중 셋이 셈에서 기각됐다. 셈을 먼저 만들고, 0을 보고하는 자에는 판별 테스트를 붙여라.
