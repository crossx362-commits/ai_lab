# SESSION HANDOFF — 오케스트레이터 (`projects/orchestrator`)

> 이 파일은 **오케스트레이터 세션 전용**이다. `/Users/junholee/ai_lab/docs/SESSION_HANDOFF.md`는
> 울온 검수 세션 것이니 건드리지 마라.

## 지금 상태 (2026-09-11)

- **PHASE 0~8 + Provider 독립성 + PHASE 10(보드 GUI) 완료.**
- **PHASE 9 Windows Worker는 의도적으로 안 만들었다** — 이 기계에서 검증할 방법이 없다.
  "검증 못 하는 것은 만들지 않는다"가 이 프로젝트의 전제다.
- 무비용 시험: `./tools/nc_suite.sh` (44종, 모델 호출 0 · `ORCH_NO_CLOUD=1`로 잠금).
  **새 게이트를 만들면 먼저 빨간불을 보여라.** 초록만 본 게이트는 게이트가 아니다.

## ■ 계획 7은 **오너 지시로 스톱** — 재개 지시 전 착수 금지

- `state/STOP` 플래그가 켜져 있다. 어떤 실행도 이것부터 만난다.
- T1·T2·T3 = DONE. **T4(task 314)는 시도 2 도중 정지**, 브랜치 `orch/task-0314`에 작업 남아 있음.
  T5·T6 미착수. master에 병합된 것 없음.
- 재개는 **돈이 드는 실행**이다 → 오너 승인 먼저. 승인 뒤 순서:
  ```bash
  ./orch resume
  ./orch requeue --plan 7
  ./orch run-plan --plan 7 --keep-going --wait-for-provider 3600
  ```

## 이 기계를 쓸 때 주의

- **다른 세션(울온)이 같은 맥에서 유니티를 돌린다.** 유니티를 죽여야 하면 `./orch unity-kill`만
  써라 — 이 명령은 **대상 프로젝트 경로를 인자로 가진 프로세스만** 고른다. `pkill Unity`는 금지.
- 메모리 게이트는 **스왑 사용률만으로 RED를 만들지 않는다**(오탐 전력 있음, `memory.py` 주석 참조).

## Blender 축 (2026-09-11 오너 지시 「코덱스는 블렌더 사용해서 개발」)
- `config.json` target `blender_sandbox`(`kind: blender`), 게이트 `orch_core/blenderrun.py`, 검증 장치는
  저장소 안 `orch_check.py`(protected). 구현자는 `BLENDER` 능력으로 고른다(codex·astra).
- NC 8종은 `tools/nc_suite.sh` 끝부분. 단독 실행은 `run_case_b` 줄만 떼서 돌리면 된다.
- 실제 Blender 개발 target(울온 자산 등)을 붙이려면 그 저장소에 `build.py`·`orch_check.py`·`tests/`를
  같은 규약으로 두고 target 항목을 추가한다 — 검증 장치 없는 target은 config 로드에서 거부된다.

## 울온 연결 (2026-09-11, 오너 보드 명령 「울온 개발해」)
- target `ulon`(Unity, subdir `projects/ulon`, SliceSelfCheck 게이트, 공유 Library)·`ulon_props`(Blender).
  worktree는 `/Users/junholee/ai_lab/.orch/worktrees/`, 브랜치는 ai_lab 저장소의 `orch/task-NNNN`.
- **Codex 명령 소비자**(`orch_core/board_commands.py`, launchd `com.ailab.orchestrator.commands`)는
  오너의 Codex 세션이 만든 것이다 — 보드 폼으로 접수된 명령을 codex가 `orch_core.cli`로 처리한다.
  건드리지 마라. 보드(:8767)도 launchd `com.ailab.orchestrator.board`가 띄운다.
- 울온 개발 계획(`./orch plan --target ulon`)은 유료 — 오너 「울온 개발해」가 그 승인이다.

## 다음에 할 일

- 최근 커밋으로 **§15 토큰 실측 기록**과 **§10 컨텍스트 상한**이 들어갔다.
  - 토큰은 **CLI가 스스로 보고한 값만** 기록한다(codex는 stderr에 보고, claude·grok은 보고 없음).
    어림수를 같은 칸에 넣지 마라 — 보드에 "토큰 보고 없음"으로 뜨는 것이 정상이다.
  - 프롬프트는 상한(`prompt_max_chars`, 기본 48000) 안에서 **피드백만** 접는다. 목표·완료 조건·
    쓰기 허용 범위는 절대 자르지 않는다(자르면 우리가 §6 요구사항 축소를 만드는 꼴).
- 남은 후보: 병렬 실행 슬롯 튜닝, 보드에서 STOP/resume 조작, 계획 재수립 경로.
  **새 기능마다 NC부터.**
