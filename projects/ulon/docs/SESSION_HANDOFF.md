# ULON 자율 개발루프 — 인수인계

작업 트리 `/Users/junholee/ai_lab-loop`(브랜치 `loop-claude`), 푸시는 커밋 → `git pull --rebase -q origin master`
→ `git push -q origin HEAD:master`. `/Users/junholee/ai_lab`는 남의 트리(검수·Grok) — 건드리지 않는다.

랩 모양: 먼저 센다 → 하나 만들기 → 내 눈으로 검증(`SELFCHECK_LOG=$PWD/unity/Logs/selfcheck_dev.log
./tools/slice_selfcheck.sh` EXIT=0 두 판 + `./tools/qa_shots.sh` 뒤 PNG를 실제로 본다) → 커밋·푸시
(`git add -A` 금지, 경로 명시) → 검수 보고(끝에 「구조 한 줄」) → 이 파일 갱신.
안건 판정은 대장, 보고는 검수. 오너 직행은 셋(다운로드·되돌릴 수 없는 삭제·돈).

## 지금까지 (tip `af8e6da4`)
지하감 ⓐ **문 뒤 언덕** 랩을 대장 조건 넷 그대로 닫았다. 원장 `Shared/EntranceGeom.cs`(세 입구·앞
방향·언덕 상수·`MoundRise`·NC용 `MoundDisabled`), 게이트 `Editor/SliceSelfCheck.MoundBehind.cs`
(문구멍 시선은 뚫림·문 위 3m 시선은 뒤 30m 안에서 막힘, NC는 「언덕이 앞당긴 거리 ≥3m」),
소품은 민 자리에서만 다시 앉힌다(`VisualSliceBuilder.SnapToGround`, `ClearPropsFrom*` 세 곳).

## 다음 착수
1. **언덕 위로 돌길 도포가 정상까지 타고 올라간다** — 07·09·11 상단 중앙의 벽돌 띠. 길은 문으로
   오는 것이지 산꼭대기로 가지 않는다. 도포 규칙(`WorldSplat` 길)에 언덕 경사·문 뒤 게이트를 물릴 것.
2. `60_d3_entrance_45`에서 언덕 표면이 모래로 칠해진다(07은 잔디) — 21° 경사가 도포 임계에 걸린 듯.
3. 부두 샷 `63_pier_cutface`: 자갈 겹 타일링이 체커보드로 읽히고 톤이 모래와 거의 같다.
