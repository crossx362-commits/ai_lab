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

## 다음 착수
**`07` 등불 그림자**(검수 지정). 그다음 큐는 비었다. 야외·실내 샷을 돌며 새 안건을 세울 것. **인상은 가설이다** — 이번 세션에 화면에서
의심한 넷 중 셋이 셈에서 기각됐다. 셈을 먼저 만들고, 0을 보고하는 자에는 판별 테스트를 붙여라.
