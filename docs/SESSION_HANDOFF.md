# SESSION HANDOFF — tank-artillery (2026-09-16)

이어받은 세션은 완료한 항목을 지우고, 전부 끝나면 이 파일을 비운다.

## 현재 상태 (전부 완료, master 에 커밋·푸시됨)
- 프로젝트 폴더: `projects/tankfall` → `projects/tank-artillery` (git mv, 히스토리 유지).
- 명세: `docs/GAME_SPEC_TANK_ARTILLERY.md` **§2-9-6 까지 반영됨**(맵 통합·Unity 6.6·미러 편차 원인 전부 기록).
- Unity 에디터 **6000.6.0f1**(최신 정식 릴리스)로 업그레이드 완료. `verify.sh`/`unity_build.sh` 의 csc.dll 경로·`UnityEngine.ScriptingModule` 참조 수정 포함. 6000.3/6000.6 양쪽에서 매치업 수치 바이트 단위 동일함 확인(Sim 레이어가 에디터에 안 기댐 재확인).
  - 사소한 남은 것: Unity Hub 로컬 config(`projects-v1.json`, git 비추적)의 `version` 필드가 아직 `6000.3.14f1` — 필요하면 정리, 급하지 않음.
- 매치업 하네스가 게임과 같은 지형(`TANKFALL_MAP=TwinHills(기본)|Crater|Terrace|Legacy`)을 쓴다. `Legacy` 로 §2-9-4 수치 재현 확인됨(회귀).
- **`verify.sh map` 전부 통과**(2건 다 해결): Crater 스폰 비대칭(리플 항을 짝함수로) + 저각 네거티브 컨트롤(최대파워로는 이 언덕을 기하학적으로 절대 못 막는다는 걸 확인하고 저파워 샷으로 교정).
- **TwinHills 미러 편차(v5, 21~91%) 원인 규명 완료**: 지형이 아니라 실제 게임(§52)이 턴 동률에서 **팀A를 항상 선공**시키는 설계 때문 — 짧은 판(레이저 6턴 등)일수록 선공 이점이 안 상쇄돼 미러가 크게 벌어졌다. [6-2] 판별(순차/교대/B선공/스폰교환)로 확정. 게임 코드(§52)는 의도된 설계라 안 건드렸고, **매치업 하네스의 미러 측정만** 스폰 교환처럼 선공도 2판마다 교차하도록 고쳐 "탱크 수치만의" 대칭성을 분리했다. 재측정(v6) 결과 미러가 41~60%대로 수렴 — 탱크·맵 수치는 안 건드렸다. 상세 표는 §2-9-6.

## 다음 세션이 볼 것 (급하지 않음, 열려있는 질문)
- "듀크는 맞아도 못 이긴다": TwinHills 에서 40° 자체 명중률이 46%→64%로 개선됐는데 승률은 30%→33%로 거의 안 움직임. 원인 미검증.
- MultiMissile 2번탄이 TwinHills 전체판에서 다시 0.5/판(Legacy·§2-9-5 실험2 에서는 ~0.8-0.9/판) — 맵/판 길이 상호작용 의심, 원인 미확인.
- v6(선공 교차 반영) 결과를 12×12 전체표로 명세에 정식 기록할지는 다음 세션 판단(§2-9-6 에 요약은 이미 반영됨, 전체 표까지 옮길지는 선택).

## 검증 명령
```bash
./projects/tank-artillery/tools/verify.sh compile   # 유니티 Play 가능 여부
./projects/tank-artillery/tools/verify.sh map       # 맵 3종 게이트 — 전부 통과
./projects/tank-artillery/tools/verify.sh battle    # 매치업(오래 걸림, 기본 맵 TwinHills, 선공 교차 반영)
TANKFALL_MAP=Legacy ./projects/tank-artillery/tools/verify.sh battle   # §2-9-4 수치 재현(회귀용)
TANKFALL_ROW=<Kind> TANKFALL_VAR=<Field> TANKFALL_VALUES=a,b,c ./projects/tank-artillery/tools/verify.sh battle  # 단일 행 실험
```
빌드: `./tools/unity_build.sh` 또는 `Unity.exe -batchmode -quit -nographics -projectPath D:\ai_lab\projects\tank-artillery\unity -executeMethod Tankfall.EditorTools.BuildScript.BuildWindows`
갤러리/자동사격: `unity/Build/Tankfall.exe -gallery|-autoshot [-map TwinHills|Crater|Terrace] [-roster A,B,C] [-forcespecial] -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -logFile -`
⚠️ 실행 중인 `verify.sh` 를 편집하지 마라(bash 는 실행하며 읽는다).
