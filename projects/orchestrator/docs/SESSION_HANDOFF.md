# SESSION HANDOFF — 오케스트레이터 (`projects/orchestrator`)

> 이 파일은 **오케스트레이터 세션 전용**이다. `/Users/junholee/ai_lab/docs/SESSION_HANDOFF.md`는
> 울온 검수 세션 것이니 건드리지 마라.

## 지금 상태 (2026-09-11)

- **PHASE 0~8 및 보드 기반 코드는 있으나 장기 자율 운영 요구사항은 미완성이다.** 최신 근거는 [인수 요구사항 점검](reports/2026-09-11_인수_요구사항_점검.md). 재개 시 기존 worktree 미사용·계획 간 코드 미전달·Provider 캐시 동시 갱신 유실을 재현했다. 실패 CLI 성공 오판은 수정하고 회귀 검증했다.
- **PHASE 9 Windows Worker는 의도적으로 안 만들었다** — 이 기계에서 검증할 방법이 없다.
  "검증 못 하는 것은 만들지 않는다"가 이 프로젝트의 전제다.
- 무비용 시험: `./tools/nc_suite.sh` (44종, 모델 호출 0 · `ORCH_NO_CLOUD=1`로 잠금).
  **새 게이트를 만들면 먼저 빨간불을 보여라.** 초록만 본 게이트는 게이트가 아니다.

## ■ 계획 7은 **오너 지시로 스톱** — 재개 지시 전 착수 금지

아래는 이전 인계 당시 기록이다. 2026-09-11 인수 점검 실측에서는 STOP 파일이 없고 명령 소비자는 대기 중이었다. 정적 문구를 현재 실행 상태로 판단하지 말고 DB·프로세스·로그를 함께 확인한다. 이번 인수 점검은 계획 7을 실행하지 않았다.

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

## 울온 연결 (2026-09-11~12, 오너 보드 명령 「울온 개발해」)
- target `ulon`(Unity, subdir `projects/ulon`, SliceSelfCheck 게이트, 공유 Library)·`ulon_props`(Blender).
  worktree는 `/Users/junholee/ai_lab/.orch/worktrees/`, 브랜치는 ai_lab 저장소의 `orch/task-NNNN`.
- **2026-09-12 수리 셋(커밋 전이면 `git status`로 확인)**:
  ① `link_paths`(config) — 본 체크아웃의 미추적 `server/.venv`를 worktree에 링크(+info/exclude). 없으면 게이트가
     psycopg2 부재로 죽는다(task 360). ② 게이트 부산물 자동 되돌리기(`gitwt.snapshot/discard_extra`) — SliceSelfCheck가
     씬·지형 8파일을 다시 저장하는데 그걸 AI 변경으로 세면 안 된다. AI가 만든 자산의 `.meta`는 남긴다.
     ③ `preflight`(config) — `pg_isready`가 실패하면 AI를 부르지 않고 BLOCKED/UNKNOWN(「전제 미충족」).
     ④ **계획 통합 브랜치** `orch/plan-NNNN`: run-plan이 HEAD에서 열고 Task PASS 커밋마다 전진, 다음 Task는 그 위에서
     출발(NC `plan_inherit` 초록, Codex 감사 P1 해결).
- **울온 게이트 전제: Postgres 16(brew, launchd 아님)이 127.0.0.1:5432에 떠 있어야 한다** — CharacterStore가
  `DATABASE_URL=postgresql://ulon@127.0.0.1:5432/ulon`을 박아 persist 서버를 띄운다. 재부팅 후엔 죽어 있다. 기동:
  `LC_ALL=en_US.UTF-8 pg_ctl -D /opt/homebrew/var/postgresql@16 -l /opt/homebrew/var/log/postgresql@16.log start`
  (LC_ALL 없이 켜면 「postmaster became multithreaded」로 즉사).
- 울온 스모크: `ORCH_NO_CLOUD=1 ORCH_CONFIG=$PWD/state/smoke_ulon.json ./orch run "..." --agent nc`
  (스크립트 에이전트가 `unity/Assets/Game/Scripts/OrchLinkProbe.cs`를 씀, 게이트 ~20s+컴파일).
- **Codex 명령 소비자**(`orch_core/board_commands.py`, launchd `com.ailab.orchestrator.commands`)는
  오너의 Codex 세션이 만든 것이다 — 건드리지 마라. 보드(:8767)도 launchd `com.ailab.orchestrator.board`가 띄운다.
- 울온 개발 계획(`./orch plan --target ulon`)은 유료 — 오너 「울온 개발해」(BOARD.md 명령 22:54)가 그 승인이다.
  권한 분류기가 유료 실행을 막으면 우회하지 말고 보고한다.

## 다음에 할 일

- 모델 정책(2026-09-12 오너 지시): 구현 Codex 계열은 `gpt-5.6-sol`, 설계·리뷰 Claude는 `opus`.
  난이도에 따라 모델명을 바꾸지 않는다. Claude가 불가하면 능력 기반 후보에서 Codex Sol로 승계한다.
  Grok은 기본 비활성이다.

- 최근 커밋으로 **§15 토큰 실측 기록**과 **§10 컨텍스트 상한**이 들어갔다.
  - 토큰은 **CLI가 스스로 보고한 값만** 기록한다(codex는 stderr에 보고, claude·grok은 보고 없음).
    어림수를 같은 칸에 넣지 마라 — 보드에 "토큰 보고 없음"으로 뜨는 것이 정상이다.
  - 프롬프트는 상한(`prompt_max_chars`, 기본 48000) 안에서 **피드백만** 접는다. 목표·완료 조건·
    쓰기 허용 범위는 절대 자르지 않는다(자르면 우리가 §6 요구사항 축소를 만드는 꼴).
- 남은 후보: 재개 시 같은 worktree 재사용(Codex 감사 P2), 병렬 실행 슬롯 튜닝, 보드에서 STOP/resume 조작, 계획 재수립 경로.
  **새 기능마다 NC부터.**

## 2026-09-12 작업별 모델 선택 (이전 고정 정책 대체)

사용자 정정에 따라 Opus/Sol 고정을 폐기했다. config.json model_policy를 호출 직전 적용하며 설계/리뷰는 단계, 구현은 목표 문구와 실패 이력으로 분류한다. 실제 모델은 usage/attempt 및 .model.txt에 기록한다. 관련 24개 테스트 통과. 잔여 퍼센트 기반 선제 전환과 실제 클라우드 생성은 미검증/미구현으로 구분한다.

- Fable 누락 수정: Claude planning=fable, debugging=opus. 설치 CLI의 fable 별칭과 공식 모델 문서 확인. 계정별 실제 생성/과금 조건은 미검증. https://platform.claude.com/docs/en/models/overview

- 모델 정책 검수: 계획 호출 도중 모델 부재/한도 장애가 발생하면 다음 가용 Provider로 대체하도록 수정. 명시 --agent는 유지하며 실패한 plan.json은 대체 호출 전에 제거. 실행 계약 테스트에 성공 대체/명시 지정/실패 파일 재사용 방지를 추가. 실제 클라우드 모델 응답은 여전히 미검증.

- 오너 지시: 게임 개발 최종 AI 검수는 Claude Fable, 대체 Codex Astra(gpt-6-astra)로 상향. 테스트 작성 모델과 최종 검수 모델을 분리했다. 플레이 검증 자동화가 추가된 것은 아니다.
