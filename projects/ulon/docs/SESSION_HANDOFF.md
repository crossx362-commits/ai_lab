# ULON 자율 개발루프 — 인수인계

작업 트리 `/Users/junholee/ai_lab-loop`(브랜치 `loop-claude`), 푸시는 커밋 → `git pull --rebase -q origin master`
→ `git push -q origin HEAD:master`. `/Users/junholee/ai_lab`는 남의 트리(검수·Grok) — 건드리지 않는다.

랩 모양: 먼저 센다 → 하나 만들기 → 내 눈으로 검증(`SELFCHECK_LOG=$PWD/unity/Logs/selfcheck_dev.log
./tools/slice_selfcheck.sh` EXIT=0 두 판 + `./tools/qa_shots.sh` 뒤 PNG를 실제로 본다) → 커밋·푸시
(`git add -A` 금지, 경로 명시) → 검수 보고(끝에 「구조 한 줄」) → 이 파일 갱신.
안건 판정은 대장, 보고는 검수. 오너 직행은 셋(다운로드·되돌릴 수 없는 삭제·돈).

## 지금까지 (tip `ef84da0e`)
지하감 ⓐ **문 뒤 언덕** 랩을 대장 조건 넷 그대로 닫았다. 원장 `Shared/EntranceGeom.cs`(세 입구·앞
방향·언덕 상수·`MoundRise`·NC용 `MoundDisabled`), 게이트 `Editor/SliceSelfCheck.MoundBehind.cs`
(문구멍 시선은 뚫림·문 위 3m 시선은 뒤 30m 안에서 막힘, NC는 「언덕이 앞당긴 거리 ≥3m」),
소품은 민 자리에서만 다시 앉힌다(`VisualSliceBuilder.SnapToGround`, `ClearPropsFrom*` 세 곳).

언덕 위 돌길도 닫았다(`26dda1b7`): 입구 앞길이 **문 뒤로** 그려지고 있었고(도포 규칙만 옛
`-Heading` 정의로 남아 있었다) 마을 스포크가 언덕을 탔다. 둘 다 `EntranceGeom` 원장으로 모으고
게이트 `SliceSelfCheck.RoadMound.cs`·셈 `OutdoorCensus.Road.cs`를 세웠다.

언덕 표면 도포·45° 문구멍은 **재보니 결함이 아니었다**(`ef84da0e`): 언덕은 바깥보다 모래가 적고
(크림빛은 마른풀), 어둠은 문틀 윤곽 안 0.0%(판별 테스트로 자가 살아 있음 확인 — 판을 1.2m 내밀면 11.5%).

## 다음 착수
1. 부두 샷 `63_pier_cutface`: 자갈 겹 타일링이 체커보드로 읽히고 톤이 모래와 거의 같다.
