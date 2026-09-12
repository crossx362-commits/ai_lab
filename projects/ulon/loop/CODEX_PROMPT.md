# Codex 울온 자율 개발 루프

오너 승인(2026-09-12): 그록과 같은 보드를 사용하여 저폴리·동화풍 그래픽,
UI, Blender 3D, 기획서에 맞는 월드맵 개발을 자율적으로 이어간다.
추가 오너 지시: 헤드리스로 작업하고 결과 보고는 공유 보드에만 남긴다.

## 작업 장소와 공존

- 코드 작업 공간: `/Users/junholee/ai_lab/.worktrees/ulon-codex-art`
- 브랜치: `codex/ulon-storybook-map`
- 공유 보드: `/Users/junholee/ai_lab/projects/ulon/docs/board.json`
- 보드 화면/API: `http://127.0.0.1:8787/` / `/api/state`
- Codex 상태: `/Users/junholee/ai_lab/projects/ulon/docs/CODEX_STATUS.md`
- 실행 증거: `/Users/junholee/ai_lab/output/ulon-codex/`
- 원본 작업 트리의 Grok·Grok Bot·Unity·서버 프로세스를 정지하거나 재시작하지 않는다.
- Claude 워크트리·다른 에이전트의 변경을 수정·병합하지 않는다. push/자동 병합 금지.
- 전용 Unity 프로젝트만 실행한다. 원본 Library를 공유하거나 심볼릭 링크하지 않는다.
- Blender는 `--background`, Unity는 `-batchmode`로 실행한다. 앱 창·브라우저·에디터 UI를 열거나 조작하지 않는다.
- 렌더 검증에 그래픽 장치가 필요하면 batchmode에서 렌더하고, `-nographics` 결과를 화면 검증으로 대체하지 않는다.
  헤드리스에서 IMGUI 캡처가 검증되지 않으면 검증 중으로 남기고 보드에 증거·재개 조건을 기록한다.
- 공유 STATUS.md의 Grok 바퀴 헤더·이력은 수정하지 않는다.
- 원본의 변경 중인 파일을 복사해 덮지 않는다. 통합은 소유권과 diff를 대조한 후 별도 수행한다.

## 매 바퀴

1. DIRECTIVES.md, 공유 CODEX_STATUS.md, `/api/state` 또는 공유 board.json,
   미처리 feedback/INBOX.md를 확인한다. 같은 Codex 작업이 실행 중이면 중복 실행하지 않는다.
2. `model: gpt` 또는 `owner: codex` 카드 중 한 작업을 고른다. 그록의 진행 중 카드·파일은 제외한다.
   이미 본인 작업이 검증 중이면 새 작업보다 그 검증과 수정을 먼저 한다.
3. 기획서 `docs/GAME_DESIGN.md` 해당 절을 실제 코드와 대조한다.
   월드 규모는 지형·이동·콘텐츠·네트워크를 함께 고려한다. 문서의 미확인 UO 수치를 사실로 확정하지 않는다.
4. 보드는 최신 내용을 다시 읽고 담당 카드만 갱신하며 다른 카드·필드를 보존한다.
   `assigned_loop`/`completed_loop`는 Grok 숫자이므로 Codex 바퀴를 섞지 않는다.
   `owner: codex`, `codex_iteration`, `worktree`, `evidence` 필드에 담당·진척·실측 경로를 기록한다.
   동시 변경을 발견하면 덮어쓰지 말고 다시 읽고 병합한다. 잘못된 JSON을 과거 Git 내용으로 되돌리지 않는다.
5. 기존 코드를 보존하는 작은 변경을 구현한다. UI는 크림색 종이·밤색 글자·세이지/청록 강조,
   3D는 기존 저폴리 실루엣과 스케일을 유지한다. Blender는 별도 백그라운드 프로세스를 사용한다.
6. 변경 범위의 테스트와 Unity 컴파일을 실행한다. 비주얼은 전용 Unity에서 실제 화면을 찍고 검토한다.
   Blender 렌더·정적 검사·Unity 컴파일·게임 플레이·2인 네트워크 검증을 각각 구별한다.
   실행 확인 전에는 보드 상태를 `검증 중`에 둔다. 파일 생성이나 rc=0만으로 완료하지 않는다.
7. 검사된 본인 파일만 명시적으로 add+commit한다. 다른 변경을 스테이징하지 않는다.
8. CODEX_STATUS.md에는 내부 재개 정보를, 공유 보드의 본인 카드에는 결과만 간결히 남긴다.
   결과는 변경 내용·검증 결과·커밋/스크린샷·남은 검증 또는 막힌 이유다. 작업 중계·계획·반복 상태 보고는 올리지 않는다.
   같은 오류가 3회 반복되면 원인과 재개 조건을 기록하고 조건이 달라지기 전 반복하지 않는다.
   처리 가능한 다른 비중복 작업이 있으면 진행한다.

## 우선순위

1. `codex-worldmap-guide`: 기획 §6.1 지역 안내·§18.4 본인 최근 시체 표시·동화풍 지도 UI.
2. `codex-storybook-lantern`: 기존 `art/storybook` Blender 산출물 보존, Unity 임포트·기존 가로등 VFX 호환·게임 화면 검증.
3. `codex-storybook-hud`: HUD·가방·장비창 가독성/통일감. Grok 대상 지정 기능의 미커밋 변경 보존.
4. `uo-half-span`: 원작 절반 요청과 기획의 작은 완결 월드를 대조하고, 확장 지형/콘텐츠 배치를 단계적으로 구현·검증.
5. 물가·조명·나무/건물 폴리시. 핵심 검증이 막혔다는 이유로 작업을 무한히 벌리지 않는다.

## 알림·정지

대화에 진행 보고·완료 보고를 보내지 않는다. 결과와 막힘·필요한 판단은 공유 보드에만 기록한다.
변화가 없으면 보고를 추가하지 않고 조용히 종료한다.
사용자가 정지하면 새 카드를 시작하지 않고 이 자동 작업을 PAUSED로 전환한다.
추가 결제·유료 API·외부 메시지 발송은 하지 않는다.
