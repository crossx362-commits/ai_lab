# SESSION HANDOFF — tank-artillery (2026-09-16)

이어받은 세션은 완료한 항목을 지우고, 전부 끝나면 이 파일을 비운다.

## 현재 상태
- 프로젝트 폴더: `projects/tankfall` → `projects/tank-artillery` 로 이동 완료(오너 지시, git mv, 히스토리 유지).
- 명세: `docs/GAME_SPEC_TANK_ARTILLERY.md` **§2-9 부터 읽을 것**.
- Unity 에디터: **6000.3.14f1 → 6000.6.0f1 로 업그레이드 완료**(오너 지시 "최신버전으로"). `ProjectVersion.txt` 갱신됨(`6000.6.0f1 (f7f8ed4d1e24)`). Unity Hub 프로젝트 목록에도 등록함(Hub 로컬 config `projects-v1.json`의 `version` 필드는 아직 `6000.3.14f1`로 남아있음 — git 비추적, 커밋 대상 아님, 사소한 정리 대상).
- `tools/verify.sh`·`tools/unity_build.sh` 의 `UNITY_VER` 기본값 `6000.6.0f1` 로 갱신. csc.dll 경로(버전마다 다름) 두 곳 다 찾아보게 고침. `UnityEngine.ScriptingModule` 참조 추가(6000.6부터 `RuntimeInitializeOnLoadMethodAttribute` 가 이걸 끌어옴).
- 매치업 하네스(`BattleSimVerify.cs`)를 게임과 같은 지형 함수에 연결: **`TANKFALL_MAP=TwinHills(기본)|Crater|Terrace|Legacy`**. `Legacy` 로 돌리면 §2-9-4 까지의 옛 수치를 정확히 재현함(회귀 확인 완료 — 듀크 30%/42%/명중46 그대로 나옴).
- **`verify.sh map` 이제 전부 통과(0건 실패)**. 두 건 다 이번 세션에 해결:
  1. Crater 스폰 높이 비대칭 — 디테일 리플 항을 `sin(x*k)` → `cos((x-100)*k)` (x=100 짝함수)로 고쳐 대칭 확보(`MapHeightFunction.cs`의 `AddDetailRipple`). 해석 기울기도 재유도, 유한차분·미러대칭 둘 다 검증.
  2. `CheckLowAngleLegacy` 네거티브 컨트롤이 안 막히던 문제 — 원인은 좌표가 아니라 **물리**였다: 40°·최대파워 궤적의 상승 기울기(tan40°=0.839)가 옛 언덕(22m 가우시안, 반경 900)의 최대 경사(≈0.628)보다 항상 커서, 어떤 좌표를 골라도 기하학적으로 절대 못 막힌다(해석·실측 둘 다 확인). 정점도 ~108m 밖이라 검사창(8~35m) 안에서 하강도 안 함. 실제 원작 대조에서 관측된 "막힘"은 근거리 조준(저파워)일 때 정점이 ~10m로 당겨져 언덕 사면에 박히는 경우였다 — `HitsTerrainEarly`에 `power` 파라미터(기본 1f, 기존 호출 안 바뀜)를 추가하고 `CheckLowAngleLegacy`만 `power:0f`로 쏘도록 고쳐서 통과시킴(`MapVerify.cs`).

## ⚠️ 다음 세션이 먼저 볼 것 — TwinHills 맵 전체 재측정에서 미러 편차가 커졌다
새 기본 맵(TwinHills)으로 12×12 를 다시 돌리자(v5, n=20/셀) **격차가 21~91%로 v4(29~77%)보다 벌어졌고, 특히 미러가 커진 기종이 있다**: 레이저 미러 90%⬆, 듀크 74%⬆, 캐터펄트 74%⬆, 캐논 미러 70%⬆(그런데 캐논 자체 평균은 21% 로 v4 35% 보다 더 나빠짐). n=20 표준오차 ±11%p 를 감안해도 90%/74%는 우연이 아니다(공정한 동전으로 20판 중 18판 이상 나올 확률 <0.1%).
캐롯 전용 [6-1] 판별 테스트는 TwinHills 에서 스폰 높이·선공 전부 "대칭"으로 나왔다(45~65%, 노이즈 안) — **범용 판별 테스트는 문제를 못 잡는다.** 가설(미검증): 평균 턴 수가 짧은 기종(레이저 등)일수록 선공 이점이 상쇄되기 전에 판이 끝나 미러가 더 크게 벌어진다 — §2-5-1 나선과 같은 축("판 길이가 공정성을 좌우한다")의 다른 얼굴일 수 있다.
**하지 말 것**: 이 결과만 보고 탱크 수치를 만지지 말 것 — 원인이 맵인지 판정 방식인지부터 갈라야 한다. 다음 걸음: 미러 이탈이 큰 기종(레이저·듀크·캐터펄트) 각각에 [6-1]과 같은 판별(순차/교대/B선공/스폰교환)을 TwinHills 에서 돌려 원인을 좁힌다. 로그: 스크래치 `full_v5_twinhills.log`(있으면 재사용, 없으면 재실행).
- 듀크 각도 실험은 TwinHills 에서도 재현됨: 40°→명중64%·승33%, 55°→명중82%·승70%, 70°→명중95%·승85% (Legacy 대비 40° 자체 명중률은 46%→64%로 개선됐지만 승률은 30%→33%로 거의 안 움직임 — "맞아도 못 이긴다"는 새 질문이 생겼다. 원인 미검증).
- MultiMissile 2번탄이 TwinHills v5 전체판에서 다시 0.5/판 ❌사장 으로 나옴(Legacy·§2-9-5 실험2 에서는 ~0.8-0.9/판으로 고쳐진 상태였음) — 맵/판 길이 상호작용 의심, 원인 미확인.

## 검증 명령
```bash
./projects/tank-artillery/tools/verify.sh compile   # 유니티 Play 가능 여부
./projects/tank-artillery/tools/verify.sh map       # 맵 3종 게이트 — 이제 전부 통과
./projects/tank-artillery/tools/verify.sh battle    # 매치업(오래 걸림, 기본 맵 TwinHills)
TANKFALL_MAP=Legacy ./projects/tank-artillery/tools/verify.sh battle   # §2-9-4 수치 재현(회귀용)
TANKFALL_ROW=<Kind> TANKFALL_VAR=<Field> TANKFALL_VALUES=a,b,c ./projects/tank-artillery/tools/verify.sh battle  # 단일 행 실험
```
빌드: `./tools/unity_build.sh` 또는 `Unity.exe -batchmode -quit -nographics -projectPath D:\ai_lab\projects\tank-artillery\unity -executeMethod Tankfall.EditorTools.BuildScript.BuildWindows`
갤러리/자동사격: `unity/Build/Tankfall.exe -gallery|-autoshot [-map TwinHills|Crater|Terrace] [-roster A,B,C] [-forcespecial] -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -logFile -`
⚠️ 실행 중인 `verify.sh` 를 편집하지 마라(bash 는 실행하며 읽는다).

## 커밋 대기 중
이번 세션 변경분(Unity 6.6 업그레이드 + 맵 통합 하네스 + 맵 게이트 2건 수정)이 아직 커밋 안 됨 — `git status`로 확인 후 master 에 직접 커밋·푸시할 것(오너 표준: 브랜치 없이 master 직행).

## 명세 상태
§2-9~§2-9-5 에 원작 대조·통합·매치업 v2~v4·단일변수 실험 6건 기록됨. v5(TwinHills) 결과·맵 통합 하네스·Unity 6.6 업그레이드·리플 대칭 수정·저각 네거티브 컨트롤 수정은 아직 명세에 안 옮겼다 — 다음 세션이 위 미러 편차 원인을 좁힌 뒤 함께 기록할 것.
