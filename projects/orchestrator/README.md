# AutoDev Orchestrator — Unity 자율개발 오케스트레이터

기존 AutoDev와 무관한 신규 프로젝트다. **PHASE 1(CLI Core)까지 구현·검증 완료.**

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

## 검증 기록 (2026-09-11, 실제 실행)

| 시험 | 기대 | 결과 |
|---|---|---|
| 정상 Unity 컴파일 | PASS | PASS (DLL 생성 확인) |
| 깨진 C# 삽입 | FAILED | FAILED, `error CS1525` 추출, exit 1 |
| 실제 목표 1건(Codex) | PASS + 커밋 | PASS, 2파일 +87줄, 브랜치에만 커밋 |
| 아무것도 안 고치는 AI | 3회 FAILED → BLOCKED | 그대로 (rc=0을 성공으로 안 봄) |
| CLI 부재 | 시도 미차감 UNKNOWN | 그대로 |

## 아직 없는 것 (PHASE 2 이후)

EditMode/PlayMode 테스트(Unity Test Framework 미설치), Claude/Grok/Ollama 어댑터, 승격 라우팅,
Planner 작업 분해, Memory Manager, 크래시 복구, Windows Worker, GUI.
