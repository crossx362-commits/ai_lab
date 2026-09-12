# Orch — Unity 자율개발 오케스트레이터

옛 AutoDev(금지·삭제됨)와 코드·이름 모두 무관한 신규 프로젝트다(CLI `./orch`, 패키지 `orch_core`). PHASE 10(GUI)까지의 기반 코드가 있지만, **요청된 장기 자율 운영 구조 전체는 미완성**이다. 작업 간 코드 전달·같은 작업 재개·상태 저장 동시성에서 결함을 재현했다. [인수 요구사항 점검](docs/reports/2026-09-11_인수_요구사항_점검.md)을 현재 상태 기준으로 삼는다. PHASE 9 Windows Worker는 이 기계에서 검증할 수 없어 보류했다.

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
./orch doctor                       # 환경 점검
./orch run "<개발 목표>"            # 자율 실행 (worktree→Codex→검증→커밋)
./orch verify                       # Unity 컴파일 판정만
./orch status [--task N]            # 상태
./orch stop                         # 실행 중 프로세스 그룹 정리
./orch recover                      # 죽은 판 회수(완료로 만들지 않는다)
./orch clean --task N [--delete-branch]
```

`ORCH_CONFIG=<파일>`로 설정을 갈아끼울 수 있다 — 게이트 자체를 시험하는 네거티브 컨트롤용.

## 구조

| 파일 | 역할 |
|---|---|
| `orch_core/config.py` | 설정·대상 해석. 경로가 실제와 다르면 여기서 먼저 실패 |
| `orch_core/db.py` | SQLite 상태 저장소(tasks/attempts/processes/usage) |
| `orch_core/proc.py` | 프로세스 **그룹** 관리(타임아웃 시 그룹째 종료), STOP |
| `orch_core/gitwt.py` | worktree 격리·변경 수집(merge-base 기준)·범위 위반 검출 |
| `orch_core/unityrun.py` | Unity 배치 실행 + PASS/FAILED/UNKNOWN 판정 |
| `orch_core/agents/codex.py` | Codex 어댑터(프롬프트는 **stdin**, 승인 요구 금지 명시) |
| `orch_core/cli.py` | 오케스트레이션 루프 |
| `sandbox/` | 검증용 소형 Unity 프로젝트(**자체 git 저장소**, ai_lab에서는 무시) |

`sandbox/Assets/Orch/Editor/OrchCompileCheck.cs`가 판정 마커를 낸다. 대상 프로젝트를
바꾸려면 이 파일을 그 프로젝트에 넣고 `config.json`에 target을 추가한다.

## 네거티브 컨트롤 스위트

```bash
./tools/nc_suite.sh     # 44개 시험, 모델 호출 없음(무비용) · 약 9분, 케이스별 소요 시간 표시
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
| 리뷰어가 한도에 걸림 | 판정불가가 아니라 승계 | 1차 **뚫림**(실전 T3) → 대체 리뷰어로 승인 |
| 한도 해제 시각 파싱 | 그때까지만 쉰다 | "try again at 11:41 PM" → 142분 냉각 |
| 스왑 91%·증가 0 | RED 아님(오탐) | 1차 **오탐으로 NC 전멸** → YELLOW로 교정 |
| 멎은 프로세스 | 타임아웃 전에 조기 종료 | 55초 타임아웃을 8초에 끊음(일하는 것은 안 건드림) |
| Provider 대기 | 풀릴 것만 기다림 | 냉각은 대기·인증 만료는 즉시 세움, 재검사로 안 덮임 |
| T3 재리뷰(실전) | 코드 재작업 없이 판정 | 한도 해제 후 **승인 → DONE** |
| 실제 계획 6개 실행(유료) | 게이트가 실제로 가름 | T1·T2 DONE, T3 REVIEW(2회 반려+리뷰 불가), T4 한도로 중단 |
| 전 Provider 불가 | 억지로 안 돌리고 보존 | BLOCKED_CLOUD_REQUIRED, rc=3, 재개 가능 |

## 모델 선택 — 단계가 아니라 **난이도**로 (오너 지시 2026-09-12)

「일 난이도에 따라 뭐로 하면 좋을지 생각해서 결정한다」. 단계 이름만 보고 고정 표를 따르면
한 줄 문서 수정에 최상위 모델을 태우고, 동시성 복구 설계에 싸구려를 태우는 일이 같이 벌어진다.

`model_policy.difficulty()`가 **이미 가진 값만으로** 1~5를 매긴다(모델에게 묻지 않는다):
계획자가 매긴 risk · 지금까지의 실패 횟수 · 요구의 길이 · 어려운 축의 낱말(동시성·복구·정합성·
구조 변경·네트워크·성능·보안) · 가벼운 축의 낱말(문서·값·이름) · 선행 의존 개수.

`config.model_tiers`가 등급을 모델·추론 강도로 옮긴다. 예(현재 값):

| 등급 | codex | claude |
|---|---|---|
| 1 | gpt-5.6-luna · 낮음 | haiku |
| 2 | gpt-5.6-terra · 낮음 | sonnet |
| 3 | gpt-5.6-sol · 보통 | sonnet |
| 4 | gpt-5.6-sol · 높음 | opus |
| 5 | gpt-6-astra · 높음 | fable |

- **사유를 반드시 남긴다**: 「난이도 4/5 (risk=high, 어려운 축: 복구·정합성, 1회 실패) → gpt-5.6-sol · 추론 high」.
  로그(`logs/task*-a*.model.txt`)와 보드 파이프라인에 그대로 뜬다.
- **실패하면 등급이 오른다** — 같은 난이도로 다시 하면 같은 실패를 산다.
- **설계·리뷰는 바닥이 있다**(분해 4, 리뷰 3). 나눠 놓은 결과가 틀리면 그 아래 전부가 틀린다.
- NC `difficulty`: 쉬운 일이 1등급·어려운 일이 5등급인지, 신호가 나빠질 때 등급이 **내려가지 않는지**,
  사유가 비어 있지 않은지를 모델 호출 0으로 지킨다.

## 순서대로 · 동시에 (오너 지시 2026-09-12 「순서대로 일하기도 하고 병렬로도 일해야지」)

계획은 **물결(wave)** 로 돈다. 선행이 끝난 Task를 모아 한 물결로 만들고, 그 안의 것들은 동시에,
물결끼리는 순서대로 간다.

```bash
./orch run-plan --plan 59 --parallel 2 --keep-going   # 기본값은 config.parallel_tasks
```
- 같은 물결의 판은 **같은 끝(통합 브랜치 tip)에서 출발**하고, 끝나는 대로 한 번에 하나씩 통합된다
  (`gitwt.integrate`: 빨리감기, 아니면 cherry-pick). **충돌하면 합치지 않는다** — 되돌리고 그 Task를
  새 끝 위에서 다시 시킨다. merge는 여전히 금지다.
- 통합 worktree(`.orch/integrate`)는 항상 분리된 HEAD다. 브랜치를 물고 있으면 ref를 못 옮긴다.
- Unity는 `unity_slots` 파일 락이 동시 실행 수를 따로 막는다 — 16GB 기계가 스왑으로 죽지 않게.
- 로그는 판마다 `[T3]` 꼬리표가 붙는다. 안 붙이면 섞여서 어느 판이 실패했는지 못 읽는다.
- NC `parallel`: 독립 둘이 한 물결에 같이 뜨는지, 의존하는 셋째가 **그 뒤**에 뜨는지, 셋 다 DONE인지.

## 자율 운전 (오너가 세울 때만 멈춘다)

```bash
./orch autopilot --target ulon        # launchd: com.ailab.orchestrator.autopilot
./orch stop                           # 멈추는 유일한 방법(보드 「세우기」와 같다)
```
주기마다 죽은 판을 회수하고, 남은 계획을 밀고, 없으면 `config.autopilot.goal`로 새 계획을 세운다.
실패는 멈춤이 아니라 기다림이다(지수 백오프, 최대 30분). 다만 **3주기 연속 진전 0인 계획은
그 계획만 보류**하고 사람 검토로 넘긴다 — 무한 재시도는 돈만 태운다.
NC `autopilot_stop`(STOP이면 한 주기도 일하지 않는다)·`autopilot_run`·`autopilot_idle`.

## 에이전트 등급 (사다리)

모델은 `config.json`의 `model_policy`에서 Provider 종류와 작업 단계로 선택한다.

| 작업 | Codex | Claude |
|---|---|---|
| 설계 | gpt-6-astra | fable |
| 구현 | gpt-5.6-sol | sonnet |
| 최종 검수 | gpt-6-astra | fable |
| 테스트 작성 | gpt-5.6-terra | sonnet |
| 오류 수정 | gpt-6-astra | opus |
| 문서 | gpt-5.6-luna | haiku |

설계·리뷰는 실제 실행 단계로, 구현 요청은 오류/문서/테스트 관련 문구와 이전 실패 여부로 분류한다.
기본은 구현이다. 이는 설정 가능한 운영 정책이며 모델 간 성능 우위를 검증했다는 뜻은 아니다.
`--agent`는 Provider 프로필을 지정하며 그 Provider 안에서는 작업별 모델 선택이 적용된다.
모델 정책이 없는 Provider는 기존 기본 모델을 유지한다. 실제 선택 모델은 시도/사용 기록과
`*.model.txt` 로그에 남는다. 인증·한도 장애 시 기존 Provider 대체 경로를 사용한다.
잔여 퍼센트에 따른 선제 전환은 아직 적용하지 않으며, 같은 계정 내 모델 변경으로 한도를 우회한다고 가정하지 않는다.
Grok은 기본 OFF, Ollama는 로그 압축 보조로 유지한다.

`ORCH_NO_CLOUD=1`이면 script 외의 에이전트를 **빌드조차 거부**한다 — 시험용 판이 유료 모델을
부르는 사고를 코드로 막는다.

## 완료 판정의 층 (현재)

```
변경 있음 → 검증장치 무변조 → 범위 내 → COMPILE → EDITMODE → PLAYMODE → 최종 리뷰 → 커밋
```
마지막 층(리뷰)은 "돌아간다"와 "목표를 했다"를 가른다. 반려면 커밋은 하되 **DONE이 아니라 REVIEW**다.
리뷰 결과 파일이 없으면 승인이 아니라 판정 불가 — 역시 DONE이 아니다.

## 분해와 실행

```bash
./orch plan "큰 목표"        # Astra가 Task로 나눈다(코드는 안 건드림)
./orch plans --plan 7        # 확인
./orch run-plan --plan 7     # 의존성 순서로 실행
```
계획의 Task는 **통합 브랜치 `orch/plan-NNNN`** 위에 쌓인다: run-plan이 HEAD에서 한 번 열고, Task가 PASS
커밋을 만들 때마다 전진시킨다. 다음 Task의 worktree는 그 위에서 출발하므로 앞 Task의 코드를 본다
(계획 7 T4가 T1·T2 코드를 못 봐 반려된 원인, NC `plan_inherit`). 브랜치를 옮기는 것은 `branch -f`뿐이다.

분해와 리뷰 모두 **응답을 stdout에서 긁지 않고 파일(plan.json·review.json)로 받는다** —
"모델이 뭔가 말했다"와 "결과물이 생겼다"를 구분하기 위해서다. 파일이 없으면 UNKNOWN이다.

## 메모리 (16GB 기계에서 오래 돌리기)

```bash
./orch mem              # 여유·스왑·추세·프로세스 (rc: 0=GREEN 1=YELLOW 2=RED)
./orch mem --relieve    # 로컬 모델만 내린다
./orch unity-kill       # **이 target 경로를 가진** Unity만 (기본은 목록만, --yes로 종료)
./orch review --task N  # 게이트 통과분의 리뷰만 다시 (리뷰어가 죽어 REVIEW에 멈춘 판)
```

RAM 퍼센트 하나로 판단하지 않는다. ①시스템 여유 ②스왑 사용률 ③**스왑 증가 속도(MB/분)** 셋 중
최악이 상태를 정하고, **못 잰 값은 GREEN이 아니라 YELLOW**다. 단 **스왑 사용률만으로는 RED를
만들지 않는다** — macOS의 스왑 사용량은 "지금 부족하다"가 아니라 "예전에 밀려났다"의 누적이라
회수되지 않고 남는다(실측: 여유 71%·증가 0인데 스왑 91%). 높은 비율 **+ 증가 중**일 때만 RED다. YELLOW 이상이면 Unity 동시 실행을
1개로 줄이고 로컬 요약기를 건너뛴다. RED면 최대 300초 기다렸다가 안 풀리면 시작을 거부한다(rc=2).
압박을 덜 때 죽이는 것은 **Ollama 모델뿐**이다 — Unity·클라우드 CLI는 남의 일을 하는 중이다.

`unity-kill`이 따로 있는 이유: 이 기계에는 **다른 세션의 Unity**가 같이 돈다. 전역
`pkill -f Unity`는 남의 10분짜리 검증을 죽인다(2026-09-11에 실제로 그럴 뻔했다). 그래서 선별을
코드(`safety.unity_procs`)에 넣고 NC로 양쪽(내 것은 고름 / 남의 것은 제외)을 시험한다.

## Provider 독립성

```bash
./orch providers [--refresh]     # 실제 인증·한도 상태 (설치 여부가 아니다)
./orch resume-blocked [--run]    # Provider가 없어 보존해둔 Task 재개
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
**인수 점검 정정:** 현재 `resume-blocked`는 목표를 새 작업으로 다시 실행하며 기존 worktree를 재사용하지 않는다. 같은 실행 안의 Provider 승계와 프로세스 종료 후 재개는 다르다. 기존 변경을 이어가는 재개 기능은 수정·검증이 필요하다.
대체 후보에서 reviewer·planner는 뺀다 — 리뷰어가 구현자가 되면 자기 작업을 자기가 승인하게 된다.
**리뷰어에게도 같은 규칙이 적용된다**(같은 함수 `_usable`를 쓴다): 지정 리뷰어가 한도·인증으로
죽으면 REVIEW 능력을 가진 다른 Provider가 이어받고, 사유에 `[대체 리뷰어 X]`를 남긴다.
"리뷰가 반려했다"와 "리뷰어가 못 돌았다"를 같은 UNKNOWN으로 적으면 처방을 고를 수 없다.

## 멎음 감지 · Provider 대기

「오래 걸린다」와 「멎었다」는 다르다. Unity는 로그를 파일에 쓰므로 **그 파일이 자라는지**로
진행을 잰다(`unity_stall_sec`, 기본 180초). 라이선스 핸드셰이크에서 멎은 Unity를 타임아웃
600초까지 기다린 적이 있어 넣었다. 로그가 아직 없거나 자라는 중이면 **절대 건드리지 않는다**.

```bash
./orch run-plan --plan 7 --keep-going --wait-for-provider 3600
./orch requeue --plan 7      # 한도로 죽은 Task를 다시 큐에 (DONE은 그대로)
```
`--wait-for-provider`는 한도에 걸리면 **해제 시각까지 자고 이어서** 한다 — 사람이 밤새
붙어 있을 이유가 없다. 단 무한정은 아니다: 냉각이 대기 한도를 넘거나, **인증 만료처럼
시간이 풀어주지 않는 문제**면 기다리지 않고 세운다.

## 크래시 복구

```bash
./orch recover --dry-run   # 주인 없는 RUNNING task 찾기
./orch recover             # INTERRUPTED로 회수 (run/run-plan 시작 시 자동으로도 돈다)
```

프로그램이 죽어도 상태는 SQLite에 남는다. 회수는 **모르는 것을 모른다고 세우는 일**이다 —
`INTERRUPTED` + `verdict=UNKNOWN`으로 세울 뿐, **재시작이 완료를 만드는 경로는 코드에 없다.**
컴파일까지 PASS하고 커밋 직전에 죽은 판도 DONE이 아니다. worktree와 브랜치는 지우지 않는다(증거).

살아있음 판정은 PID만 보지 않는다. PID는 재사용되므로 **명령줄에 `orch_core.cli`가 있는지까지**
확인한다 — 그 방향으로 틀리면 task가 영원히 RUNNING에 갇힌다. 잔존 자식 프로세스는
**DB에 기록된 프로세스 그룹만** 정리한다(패턴 kill 없음).

## 보드 (GUI)

```bash
python3 tools/board.py      # http://127.0.0.1:8767
```
보드는 명령을 `state/board_commands.sqlite3`에 접수하고 STOP 플래그를 제어한다. 별도 명령 소비자가 접수된 명령을 순서대로 실행한다.
한 화면에 **진행·막힘·단계·Provider 상태·사용량·메모리**가 들어가고, 작업 줄을 누르면
**시도 이력·리뷰 사유·프로세스·로그 꼬리**가 덮어서 뜬다(목록을 밀어내지 않는다).
Provider 카드는 저장된 마지막 검사 결과만 보여주고 **언제 잰 것인지**를 같이 적는다 —
보드가 3초마다 CLI에 인증을 물으면 기계를 더 느리게 만든다. 오래된 값을 지금 값인 척하지 않는다.

## 토큰 · 컨텍스트 상한

**토큰은 CLI가 스스로 보고한 값만 기록한다**(`usage.tokens`, 출처 `measured`). 실측 결과
codex만 보고하고(stderr `tokens used` / `30,845`), claude·grok은 보고하지 않는다 → 그 칸은
비워 두고 보드에 「토큰 보고 없음」으로 적는다. 문자 수로 어림한 값을 같은 칸에 넣지 않는다 —
틀린 숫자는 빈칸보다 나쁘다(NC `token_usage`).

프롬프트는 `prompt_max_chars`(기본 48,000) 안에서 **피드백(인수인계·오류 로그)만** 가운데를
접는다. 목표·완료 조건·쓰기 허용 범위는 절대 자르지 않는다 — 자르면 우리가 요구사항 축소를
만드는 꼴이다. 자른 사실은 프롬프트 안에 「총 N자 중 M자 생략」으로 적는다 — 조용히 자르면 AI는
자기가 전부 봤다고 믿는다(NC `context_cap`).

## Blender 축 (오너 지시 2026-09-11 「코덱스는 블렌더 사용해서 개발」)

target에 `"kind": "blender"`를 주면 판정 축이 Unity에서 Blender 배치로 바뀐다. 원칙은 같다 —
AI가 "만들었다"고 말한 것은 만든 것이 아니다. 저장소의 검증 장치 `orch_check.py`(protected)가
**팩토리 빈 씬**에서 `build.py`의 `build()`를 돌려 실제 메시가 생겼는지 재고, `tests/test_*.py`의
`test_*`를 전부 부른 뒤 stdout에 `ORCH_BLENDER_OK/FAIL` 마커를 찍는다. 마커가 없으면 UNKNOWN.
구현자는 이름이 아니라 **`BLENDER` 능력**으로 고른다(codex·astra에 배정, grok·ollama는 후보에서 빠진다).
게이트 8종(NC): 정상 빌드 PASS · 빈 변경 · 깨진 스크립트 · 빈 씬 · 검증 장치 변조 · 테스트 삭제 ·
실패 테스트 · 능력 배정. 시험 저장소 `blender_sandbox/`(Blender 5.2 LTS, 판정 약 1.2초).

```bash
./orch run "나무 상자 3개를 바닥에 놓아라" --target blender_sandbox
```

## 실제 프로젝트 붙이기 — 울온 (모노레포 하위 폴더)

울온은 `ai_lab` 모노레포 안(`projects/ulon`)이라 저장소 전체를 판마다 풀 수 없고, 다른 세션이 늘
무언가 고치고 있어 전체가 깨끗한 순간도 없다. 그래서 target에 `subdir`를 주면:
- worktree는 **sparse checkout**으로 그 폴더만 내려받고, AI의 작업 폴더는 `worktree/subdir`다.
- clean 검사·변경 집계·커밋은 **그 폴더 기준**이다. 폴더 밖으로 새어나간 쓰기(`../옆앱/파일`)는
  범위 밖 수정으로 잡힌다(NC `mono_subdir`, 처음엔 안 보여서 PASS가 났다 — 수리함).
- Unity는 `library_cache`로 target별 공유 Library를 쓴다(첫 캐시는 본 체크아웃 Library를 seed로 복사).
  판마다 750MB를 재임포트하지 않기 위해서다. 동시 실행은 슬롯이 막는다.
- 프로젝트 자기 검사는 `gates`에 `execute_method`로 건다 — 울온의 자는 NUnit이 아니라
  `Ulon.Editor.SliceSelfCheck.Run`의 **종료코드**다(NC `gate_probe_fail/ok`).
- `link_paths`: 본 체크아웃에만 있는 **미추적 로컬 자원**(울온 `server/.venv`)을 worktree에 링크한다 —
  없으면 게이트가 psycopg2 부재로 죽는다(task 360). 링크는 worktree의 info/exclude에 적어 변경으로 안 센다.
- `preflight`: 게이트 전제 명령(울온 `pg_isready`). 실패면 **AI를 부르지 않고** BLOCKED/UNKNOWN —
  환경 실패로 시도 3회를 태워 "AI가 못 했다"로 적지 않기 위해서다(NC `preflight`).
  울온 Postgres는 launchd가 아니라 손으로 켠다: `LC_ALL=en_US.UTF-8 pg_ctl -D /opt/homebrew/var/postgresql@16 start`.
- **게이트 부산물은 되돌린다**: SliceSelfCheck는 씬·지형 8파일을 다시 저장한다. 게이트 전 `git status`를 찍어
  두고 게이트 뒤 새로 생긴 변경만 되돌린다(AI가 만든 자산의 `.meta`는 그 자산의 일부라 남긴다).

| target | kind | 폴더 | 게이트 |
|---|---|---|---|
| `ulon` | unity | `projects/ulon` | 컴파일 + SliceSelfCheck(exit 0) · 보호: `Assets/Orch`, `SliceSelfCheck*.cs`, `tools/` |
| `ulon_props` | blender | `projects/ulon/art/blender` | 빈 씬 빌드 + `tests/`(§6.1 크기 자) — WANTLIST의 절구통·모루·화덕 |

```bash
./orch run "절구통을 §6.1 크기로 만든다" --target ulon_props
./orch plan "…" --target ulon && ./orch run-plan --plan N --keep-going
```

## 아직 없는 것

Windows Worker(PHASE 9) — 이 기계에 Windows가 없어 **검증할 수 없어서 만들지 않았다**.
검증 못 하는 코드를 넣는 것은 이 프로젝트의 첫 번째 원칙과 정면으로 어긋난다.
(진행 없음 감지는 `unity_stall_sec`로 들어갔다 — 「멎음 감지」 절.)


### 보드 명령 자동 실행 (2026-09-11)

보드의 **실행 요청**으로 접수하면 명령 번호가 발급된다. 명령 담당 Codex가 기존 프로젝트 상태를 확인하고 처리한다. 게임/Blender 개발은 기존 오케스트레이터 실행 경로를 사용하도록 지시한다. 명령 번호를 누르면 결과와 담당자가 제출한 검증 근거가 열린다.

- 접수 상태는 SQLite에 보존되며 같은 접수 식별자의 재요청은 중복 생성하지 않는다.
- 명령 담당은 한 번에 하나씩 처리한다. `BOARD.md`의 과거 명령은 자동으로 재실행하지 않는다.
- 세우기는 새 명령 실행을 보류하고 현재 명령 프로세스 그룹을 종료한다. 풀기는 대기 명령을 다시 소비한다. 중단된 명령은 부작용 중복 방지를 위해 자동 재시도하지 않는다.
- `처리 보고`는 실행기 성공 종료와 담당자 검증 근거가 있는 상태다. 독립적인 검증 완료 판정과는 구별한다. 실패·막힘·결과 누락은 각각 표시한다.
- 서비스가 끊겨도 접수는 보존되고 화면에 연결 확인 필요가 표시된다. 로그인 상태의 맥에서 launchd가 서비스를 유지한다.

설치 또는 등록 확인: `python3 tools/install_board_services.py`

서비스: `com.ailab.orchestrator.board`, `com.ailab.orchestrator.commands`.
실행 로그: `logs/command-<번호>.stdout.log`, `.stderr.log`, `.result.json`.
회귀 검증: `python3 -m unittest discover -s tests -p test_board_commands.py`.


### 보드에서 진행률과 AI 상태 확인

오너 지시(2026-09-11)에 따라 신규 명령과 후속 지시는 보드에서 접수한다. 명령 이력에서 이전 결과를 열고 **후속 명령 작성**으로 이어서 지시할 수 있다.

- 완료율은 시험용/보관된 작업을 제외하고 `DONE`이면서 `PASS`인 등록 작업 수를 전체 등록 작업 수로 나눈 값이다. 작업 시간이나 코드 작성량을 추정하지 않는다.
- AI의 **ON**은 실제 실행이 확인되었거나 최근 상태 검사에서 사용 가능한 상태다. **OFF**는 사용 중지·연결 불가·한도 상태이며, 오래되거나 없는 검사 결과는 **확인 필요**로 표시한다.
- 현재 작업 중인 AI와 구현/리뷰를 별도로 표시한다. PID·실행 파일·시작시각이 맞지 않는 과거 프로세스는 작업 중으로 표시하지 않는다.
- AI 상태는 보드에서 **상태 확인**으로 갱신한다. 오래된 상태는 자동으로 다시 확인하며 모델 생성 요청을 보내지 않는다.
- 명령 이력은 30개씩 이전 기록을 조회한다. 새 명령이 많아도 실행 중인 명령은 별도로 조회한다.

### 보드에서 남은 사용량 확인

**남은 사용량** 카드와 Codex/Astra 카드에 계정 잔여 비율을 표시한다. 설치된 Codex CLI의 `app-server` → `account/rateLimits/read` 응답을 사용하며, 사용량 조회를 위해 모델을 실행하지 않는다. 보드를 보고 있는 동안 60초마다 갱신하고 **새로고침**으로 다시 조회할 수 있다.

- 기간별 `100 - usedPercent`를 0~100%로 표시한다. 주간·5시간 등의 기간과 초기화 시각(KST)을 함께 표시하며 서로 다른 한도를 합치지 않는다.
- Codex와 Astra는 현재 같은 Codex CLI/계정 설정을 사용한다. **전체 · 사용 기록**에는 서버가 제공한 Spark 등 별도 한도, 추가 크레딧, 보드 작업 호출/토큰 기록과 메모리를 표시한다.
- 조회 실패, 2분 이상 지난 데이터, 초기화 시각이 지난 창은 현재 잔여 비율을 **미확인**으로 바꾸고 마지막 확인 값과 시각을 남긴다. 제공되지 않은 기간이나 한도를 0 또는 무제한으로 가정하지 않는다.
- Claude·Grok 잔여 한도는 현재 연동된 조회 값이 없어 **미확인**, Ollama는 로컬 실행으로 표시한다. 계정 식별자나 인증 값은 보드 응답·로그에 저장하지 않는다.

검증: `python3 -W error::ResourceWarning -m unittest discover -s tests -p 'test_board*.py'`.
