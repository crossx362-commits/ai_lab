# AutoDev Orchestrator — Unity 자율개발 오케스트레이터

기존 AutoDev와 무관한 신규 프로젝트다. **PHASE 8(Crash Recovery)까지 구현·검증 완료.**

## 원칙 (코드로 강제되는 것만 적는다)

- **AI가 "완료했다"고 말한 것은 완료가 아니다.** 완료는 ①실제 파일 변경(git) ②Unity 컴파일 판정
  둘을 통과했을 때만이다. `DONE`은 Unity `PASS` 경로에서만 쓰인다.
- **확인 못 한 것은 UNKNOWN.** Unity 종료코드가 0이어도 판정 마커가 없으면 PASS가 아니다.
- **인프라 실패 ≠ 코드 실패.** CLI 부재·타임아웃은 시도를 차감하지 않고 즉시 세운다.
- **main 자동 병합 없음.** 작업은 격리 worktree의 브랜치에만 커밋된다.
- **파괴적 git 금지.** `gitwt.FORBIDDEN`이 push/reset/clean/rebase/merge를 코드에서 막는다.
- **무한 재시도 없음.** 기본 3회.
- **원본 로그 보존 + 마스킹.** 모든 AI/Unity 출력은 `logs/`에 남되 비밀값은 치환된다.

## 사용

```bash
./autodev doctor                       # 환경 점검
./autodev run "<개발 목표>"            # 자율 실행 (worktree→Codex→검증→커밋)
./autodev verify                       # Unity 컴파일 판정만
./autodev status [--task N]            # 상태
./autodev stop                         # 실행 중 프로세스 그룹 정리
./autodev recover                      # 죽은 판 회수(완료로 만들지 않는다)
./autodev clean --task N [--delete-branch]
```

`AUTODEV_CONFIG=<파일>`로 설정을 갈아끼울 수 있다 — 게이트 자체를 시험하는 네거티브 컨트롤용.

## 구조

| 파일 | 역할 |
|---|---|
| `autodev_core/config.py` | 설정·대상 해석. 경로가 실제와 다르면 여기서 먼저 실패 |
| `autodev_core/db.py` | SQLite 상태 저장소(tasks/attempts/processes/usage) |
| `autodev_core/proc.py` | 프로세스 **그룹** 관리(타임아웃 시 그룹째 종료), STOP |
| `autodev_core/gitwt.py` | worktree 격리·변경 수집(merge-base 기준)·범위 위반 검출 |
| `autodev_core/unityrun.py` | Unity 배치 실행 + PASS/FAILED/UNKNOWN 판정 |
| `autodev_core/agents/codex.py` | Codex 어댑터(프롬프트는 **stdin**, 승인 요구 금지 명시) |
| `autodev_core/cli.py` | 오케스트레이션 루프 |
| `sandbox/` | 검증용 소형 Unity 프로젝트(**자체 git 저장소**, ai_lab에서는 무시) |

`sandbox/Assets/AutoDev/Editor/AutoDevCompileCheck.cs`가 판정 마커를 낸다. 대상 프로젝트를
바꾸려면 이 파일을 그 프로젝트에 넣고 `config.json`에 target을 추가한다.

## 네거티브 컨트롤 스위트

```bash
./tools/nc_suite.sh     # 25개 시험, 모델 호출 없음(무비용) · 약 6분, 케이스별 소요 시간 표시
```

게이트가 **빨간불을 낼 줄 아는지**를 매번 확인한다. 통과만 보는 검증은 검증이 아니다.
`type: "script"` 에이전트로 원하는 실패를 결정적으로 만들어 넣는다.

## 검증 기록 (2026-09-11, 실제 실행)

| 시험 | 기대 | 결과 |
|---|---|---|
| 정상 Unity 컴파일 | PASS | PASS (DLL 생성 확인) |
| 깨진 C# 삽입 | FAILED | FAILED, `error CS1525` 추출, exit 1 |
| 실제 목표 1건(Codex) | PASS + 커밋 | PASS, 2파일 +87줄, 브랜치에만 커밋 |
| 아무것도 안 고치는 AI | 3회 FAILED → BLOCKED | 그대로 (rc=0을 성공으로 안 봄) |
| CLI 부재 | 시도 미차감 UNKNOWN | 그대로 |
| 허용 범위 밖 수정 | FAILED | 그대로 (Unity 안 돌림) |
| 검증 장치 삭제 | FAILED + 복구 | 그대로 |
| 에이전트 타임아웃 | 미차감 UNKNOWN | 그대로 |
| STOP 중 루프 | 즉시 STOPPED | 1차 **안 멈춤** → 수리 후 그대로 |
| 병렬 2건 | 서로 격리 | 각 worktree에 자기 파일만 |
| EditMode/PlayMode 테스트 | PASS | 각 1/1 통과 (결과 XML 파싱) |
| 실패하는 테스트 | FAILED | 그대로 (실패 메시지 추출) |
| 테스트 삭제 | FAILED | 그대로 (쓰기 허용·삭제 금지) |
| 테스트 0건 실행 | UNKNOWN | 코드상 처리(미발동) |
| Unity 슬롯 1 | 직렬화 | 겹침 -1.63s (DB 시각) |
| 사다리 승격 | 1등급 2회 실패 → 2등급 PASS | 그대로 (모델 없이 실증) |
| Claude 어댑터 실호출 | PASS + 커밋 | PASS, 31.3s, 2파일 +68줄 |
| Grok 어댑터 실호출 | PASS + 커밋 | PASS, 380.3s, 2파일 +92줄 |
| Ollama 요약 | 긴 로그 압축 후 언로드 | 5.6k자 → 18.2초, 적재 0 |
| 시험판이 유료 모델 호출 | 구조적 차단 | 1차 **뚫림(226초 과금)** → 자물쇠 추가 |
| 리뷰 반려 | DONE 아님 | REJECTED·status=REVIEW, 3회 재시도 후 커밋만 |
| 리뷰 결과 파일 없음 | 승인 아님 | UNKNOWN → status=REVIEW (1차 **DONE 오판** → 수리) |
| 계획 파일 없음 / 순환 의존 | 분해 실패 | 그대로 |
| 계획 실행(2 Task) | 의존성 순서로 전부 DONE | 그대로 |
| 실제 Astra 분해 | Task 여러 개 + 완료조건 | 6개, 131.8초, 의존성 사슬 |
| 메모리 판정 9종(합성 입력) | 최악이 이김·못 잰 값은 YELLOW | 그대로 (압박을 만들지 않고 시험) |
| 메모리 실측 | 상태 + 슬롯 축소 | 여유 68%·스왑 74% → YELLOW, 슬롯 1 |
| 남의 프로젝트 Unity | 대상에서 제외 | 제외, 내 것만 선별 (양쪽 다 확인) |
| 크래시 회수 5종 | 죽은 판만 INTERRUPTED | 그대로, DONE 경로 없음 (PID 재사용도 잡음) |
| 실제 잔재 회수 | RUNNING 2건 회수 | task 15·56 → INTERRUPTED (worktree 보존) |
| Provider 인증 실검사 | 로그아웃 CLI는 AVAILABLE 아님 | AUTH_REQUIRED (가짜 CLI로 확인) |
| 장애 분류 | 한도/인증/과금 구분, 코드 오류는 제외 | 그대로 (CS 오류를 장애로 안 봄) |
| Provider 승계 | 시도 미차감 + 인수인계 | nc_p1 한도 → nc_p2 PASS, 프롬프트에 앞 diff 실림 |
| 한도 오류가 stderr로만 올 때 | 장애로 감지 | 1차 **뚫림**(실전 계획 7 T4) → 수리 후 감지 |
| 실제 계획 6개 실행(유료) | 게이트가 실제로 가름 | T1·T2 DONE, T3 REVIEW(3회 반려), T4 한도로 중단 |
| 전 Provider 불가 | 억지로 안 돌리고 보존 | BLOCKED_CLOUD_REQUIRED, rc=3, 재개 가능 |

## 에이전트 등급 (사다리)

```
codex(낮은 추론) → claude → astra(높은 추론)
```
이 기계의 codex 기본 모델이 이미 `gpt-6-astra`라, Codex와 Astra는 **모델명이 아니라 추론 강도**로
가른다(`extra_config: model_reasoning_effort`). 등급은 시도마다 라우터가 점수로 정하고
(`router.py`) **한 시도에 한 칸만** 오른다 — 두 칸을 뛰면 중간 등급이 풀 수 있었는지 알 수 없다.
`grok`은 개발 사다리가 아니라 **조사**용, `ollama(gemma4:12b)`는 **로그 압축 보조**다(파일을 고치지 않는다).

`AUTODEV_NO_CLOUD=1`이면 script 외의 에이전트를 **빌드조차 거부**한다 — 시험용 판이 유료 모델을
부르는 사고를 코드로 막는다.

## 완료 판정의 층 (현재)

```
변경 있음 → 검증장치 무변조 → 범위 내 → COMPILE → EDITMODE → PLAYMODE → 최종 리뷰 → 커밋
```
마지막 층(리뷰)은 "돌아간다"와 "목표를 했다"를 가른다. 반려면 커밋은 하되 **DONE이 아니라 REVIEW**다.
리뷰 결과 파일이 없으면 승인이 아니라 판정 불가 — 역시 DONE이 아니다.

## 분해와 실행

```bash
./autodev plan "큰 목표"        # Astra가 Task로 나눈다(코드는 안 건드림)
./autodev plans --plan 7        # 확인
./autodev run-plan --plan 7     # 의존성 순서로 실행
```
분해와 리뷰 모두 **응답을 stdout에서 긁지 않고 파일(plan.json·review.json)로 받는다** —
"모델이 뭔가 말했다"와 "결과물이 생겼다"를 구분하기 위해서다. 파일이 없으면 UNKNOWN이다.

## 메모리 (16GB 기계에서 오래 돌리기)

```bash
./autodev mem              # 여유·스왑·추세·프로세스 (rc: 0=GREEN 1=YELLOW 2=RED)
./autodev mem --relieve    # 로컬 모델만 내린다
./autodev unity-kill       # **이 target 경로를 가진** Unity만 (기본은 목록만, --yes로 종료)
```

RAM 퍼센트 하나로 판단하지 않는다. ①시스템 여유 ②스왑 사용률 ③**스왑 증가 속도(MB/분)** 셋 중
최악이 상태를 정하고, **못 잰 값은 GREEN이 아니라 YELLOW**다. YELLOW 이상이면 Unity 동시 실행을
1개로 줄이고 로컬 요약기를 건너뛴다. RED면 최대 300초 기다렸다가 안 풀리면 시작을 거부한다(rc=2).
압박을 덜 때 죽이는 것은 **Ollama 모델뿐**이다 — Unity·클라우드 CLI는 남의 일을 하는 중이다.

`unity-kill`이 따로 있는 이유: 이 기계에는 **다른 세션의 Unity**가 같이 돈다. 전역
`pkill -f Unity`는 남의 10분짜리 검증을 죽인다(2026-09-11에 실제로 그럴 뻔했다). 그래서 선별을
코드(`safety.unity_procs`)에 넣고 NC로 양쪽(내 것은 고름 / 남의 것은 제외)을 시험한다.

## Provider 독립성

```bash
./autodev providers [--refresh]     # 실제 인증·한도 상태 (설치 여부가 아니다)
./autodev resume-blocked [--run]    # Provider가 없어 보존해둔 Task 재개
```

특정 업체를 필수 의존성으로 두지 않는다. 시작할 때 `codex login status` · `claude auth status` ·
`grok models` · ollama `/api/tags`로 **실제로 물어본다** — `which`로 찾았다고 AVAILABLE이 아니다.
상태는 일곱(AVAILABLE/UNAVAILABLE/AUTH_REQUIRED/LIMITED/RATE_LIMITED/ERROR/DISABLED)이고,
확인하지 못하면 LIMITED다(초록으로 세지 않는다).

배정은 **이름이 아니라 능력**(CODING·PLANNING·REVIEW·RESEARCH·LONG_CONTEXT·LOCAL·LOW_COST)으로 한다.
실행 중 429·인증 만료·과금 문제를 만나면 **코드 실패와 구분해서** 기록하고(시도 미차감) 다음
Provider로 승계한다. 승계는 재시작이 아니다 — worktree를 그대로 두고 목표·완료조건·지금까지의
diff·변경 파일·Unity 판정·오류·지난 시도·남은 일을 인수인계 문서로 넘긴다(`handoff.py`).

전부 막히면 로컬 모드로 내려가되, **로컬이 감당 못 하는 일은 억지로 시키지 않고**
`BLOCKED_CLOUD_REQUIRED`로 보존한다. Provider가 살아나면 `resume-blocked --run`으로 이어서 한다.
대체 후보에서 reviewer·planner는 뺀다 — 리뷰어가 구현자가 되면 자기 작업을 자기가 승인하게 된다.

## 크래시 복구

```bash
./autodev recover --dry-run   # 주인 없는 RUNNING task 찾기
./autodev recover             # INTERRUPTED로 회수 (run/run-plan 시작 시 자동으로도 돈다)
```

프로그램이 죽어도 상태는 SQLite에 남는다. 회수는 **모르는 것을 모른다고 세우는 일**이다 —
`INTERRUPTED` + `verdict=UNKNOWN`으로 세울 뿐, **재시작이 완료를 만드는 경로는 코드에 없다.**
컴파일까지 PASS하고 커밋 직전에 죽은 판도 DONE이 아니다. worktree와 브랜치는 지우지 않는다(증거).

살아있음 판정은 PID만 보지 않는다. PID는 재사용되므로 **명령줄에 `autodev_core.cli`가 있는지까지**
확인한다 — 그 방향으로 틀리면 task가 영원히 RUNNING에 갇힌다. 잔존 자식 프로세스는
**DB에 기록된 프로세스 그룹만** 정리한다(패턴 kill 없음).

## 아직 없는 것 (PHASE 9 이후)

Windows Worker, GUI.
Unity가 라이선스 핸드셰이크에서 멈추면 지금은 타임아웃(600초)까지 기다린다 —
진행 없음 감지는 아직 없다.
