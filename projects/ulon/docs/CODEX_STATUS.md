# 울온 Codex 개발 현황

- 바퀴: 1
- 상태: 검증 중 — Unity 로직 검사 통과, 게임 화면 검증 대기
- 작업 공간: `/Users/junholee/ai_lab/.worktrees/ulon-codex-art`
- 브랜치: `codex/ulon-storybook-map`
- 담당: 저폴리 동화풍 그래픽·UI·Blender 3D·월드맵
- 공유 보드: http://127.0.0.1:8787/

## 현재 카드

`codex-worldmap-guide` — 기획 §6.1 지역 안내와 §18.4 내 최근 시체 위치 표시.
기존 지도는 지역 이름/안내가 없고, 시체는 근접 상호작용용 NearestCorpse를 사용했다.
이를 WorldRegions 지역 범례와 본인 최근 시체 표식으로 수정했다.
크림 종이 바탕·밤색 글자, 농경지/숲/광산 안내와 축척, 시체 거리 표시를 추가했다.
계정 식별은 기존 OfflineWorld.AccountOf를 public으로 공개해 같은 원장을 사용한다(판정 로직 불변).
코드 커밋: `c3f00f34` — 전용 브랜치에만 있음. 원본/master에 아직 통합하지 않았다.

## 진행 증거

- 전용 워크트리 생성: 기준 b01c18d6, Grok 활성 변경과 분리.
- Unity 초기 임포트/컴파일 성공: `output/ulon-codex/baseline-unity.log` (기존 컴파일 경고 있음).
- 실패 재현: `output/ulon-codex/map-red.log`, `map-account-red.log`.
- 최종 Unity 검사: `output/ulon-codex/map-green.log`, `CODEX_WORLD_MAP_PASS: 9 behavior checks`, exit 0.
- 독립 코드 리뷰 후 작은 창 범례 폭·지도 경계 표식 문제 수정, 재검토에서 추가 결함 없음.
- 기존 Blender 가로등: `projects/ulon/art/storybook/`. Blender 검증만 완료, Unity 미반영.
- 실행 방식 수정: 예약 `codex-ui` PAUSED. 그록 방식의 독립 `loop/codex_loop.py` 프로세스 사용.
- 실제 시작: 2026-09-12 16:57 KST, supervisor PID 45326/PGID 45326, 첫 CLI PID 45328, Codex #2.
- `output/ulon-codex/loop_0002.jsonl`에서 turn.started와 지침 파일 읽기 도구 실행 확인.
- 종료→45초 대기→새 세션. 한 바퀴 90분, 연속 세션 실패 3회 정지. STOP 파일은 `loop/CODEX_STOP`.
- 프로세스 검사 6개 PASS: 새 세션 반복, 시작 전/바퀴 후 정지, 중복 잠금, 3회 실패, 타임아웃.
- PID와 로그는 실행 증거이며 개발 작업 완료 증거가 아니다.
- 공유 보드 API에서 owner=codex 카드 5개 확인, Grok #16 커밋 이후에도 유지됨.
- 전체 하네스 exit 1: 기존 데몬 중지·백업 지연과 새 아트 미분류 경고. 하네스나 운영 데몬은 수정하지 않았다.

## 남은 검증

- 다음 바퀴: 전용 Unity에서 1440×900 및 960×540 지도 화면을 찍고 범례 잘림·표식 식별·HUD 겹침 확인.
  `HudShots.cs`는 IMGUI 화면을 스탠드얼론 ScreenCapture로 찍는 기존 경로다.
  오프스크린 Camera.Render만으로 HUD를 검증했다고 하지 않는다.
- 전용 워크트리 새 Unity 임포트가 TerrainCtrl0/1/2.asset.meta를 삭제해 본인 유발 삭제만 복구했다.
  .asset 본체는 미추적 생성물이므로 전용 씬 실행 전 빌더/생성 경로를 확인한다.
- 실제 멀티플레이 시체 동기화는 별도 검증 필요.
- 600m 지형을 3km급으로 확장하는 것은 별도 카드. 현재 지도 크기를 바꾸지 않는다.

## 재검사 명령

```sh
/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -quit -projectPath /Users/junholee/ai_lab/.worktrees/ulon-codex-art/projects/ulon/unity -executeMethod Ulon.Editor.CodexWorldMapChecks.Run -logFile /Users/junholee/ai_lab/output/ulon-codex/map-green.log
```
