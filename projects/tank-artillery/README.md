# TANKFALL — 3D 턴제 포격전

> **현황 단일 소스는 코드다.** 본문과 충돌하면 `Sim/MapHeightFunction.TeamSize` · `Sim/TankStats.TankKind` · `Sim/MapHeightFunction.MapKind` · `View/BattleDemo.cs` 를 라.
> 기획·결정: [`docs/GAME_SPEC_TANK_ARTILLERY.md`](../../docs/GAME_SPEC_TANK_ARTILLERY.md)
> 다음 세션: [`docs/SESSION_HANDOFF.md`](../../docs/SESSION_HANDOFF.md)

탱크 13종·일반탄/특수탄 Blender 에셋: [art/blender/README.md](art/blender/README.md).
편집 원본 `art/blender/tankfall_roster.blend`, 게임 메시 `unity/Assets/_Project/Resources/BlenderModels/`.
절차적 탱크(`ProceduralTank`)는 에셋 누락 시 호환용.

---

## 이 세션의 레인 (Grok, 2026-09-20)

다른 AI와 안 겹치게 아래만 잡는다. **이 파일 밖은 수정 금지.**

| 잡음 | 안 잡음 (남이 진행 중·잠금) |
|---|---|
| `projects/tank-artillery/README.md` | `tools/BallisticsVerify.cs` (GPT) |
| 문서-코드 불일치 수리(숫자 동결 금지) | 밸런스 숫자 · `verify.sh battle` · TwinHills 기준선 |
| | 맵 형태(판 구성 ①) · `MapHeightFunction` |
| | `View/BattleDemo.cs` · 유니티 공유 트리 빌드 |
| | `Sim/*` 피해·탄도·이동 (매치업 표를 움직임) |

다음 조각(허가 후): `BattleDemo`에서 턴 상태만 Sim으로 뿐. 숫자 변경 없음.

---

## 지금 실행하면 보이는 것

Unity Hub에서 **`projects/tank-artillery/unity`** 를 열고 **Play**.
엔트리는 `View/BattleDemo.cs` (`[RuntimeInitializeOnLoadMethod]`).

조작의 정확한 키배치는 `BattleDemo`·`BattleScreens`가 원천. 아래는 입문용.

| 키 | 동작 |
|---|---|
| `W` `A` `S` `D` | 이동 페이즈 주행 (접지·턱 넘기) |
| 우클릭 | 조준 페이즈 진입 (되돌릴 수 없음) |
| 포탑·포신·파워 | 조준 (기종별 Min/MaxPitch 다름) |
| `Space` | 발사 (해석해 탄도, 지형 파괴) |
| `4` | 나이스샷 포인트가 차면 핵(궁극기 게이지 폐기) |
| 우클릭 드래그 | 카메라 (조준 페이즈에서는 포신 뒤 스냅) |

**꼭 해볼 것** — SDF로 바른 이유:
1. 언덕 옇면을 정면으로 파고들어 처마(overhang)
2. 그 안으로 계속 써 동굴
3. 굴착탄으로 발밑을 파 납하 (기획서 §28)
4. 깎은 구덩이에서 탈출 경로 HUD 확인

---

## 구조 (심볼 아니라 현황)

```
projects/tank-artillery/
├── unity/Assets/_Project/Scripts/
│   ├── Sim/   Tankfall.Sim.asmdef  (noEngineReferences: true)
│   │   Ballistics · ProjectileSimulator · FlightMotion · Guidance
│   │   SdfVolume · SdfDeformer · SurfaceNets · CeilingCollapse
│   │   TankStats · TankGroundProbe · MapHeightFunction
│   │   AiGunner · AiMover · TurnOrder · NiceShot
│   │   ShellEffects · Items · SupplyDrop · AirFeatures · BoomMode
│   └── View/  Tankfall.View.asmdef
│       BattleDemo.cs          한 판 규칙·입력·카메라 (고드 파일)
│       BattleScreens · ParticleFx · ProceduralTank · TerrainView
├── tools/verify.sh          ./tools/verify.sh [all|sdf|play|ball|compile|game]
└── _deprecated_heightfield/
```

`Scripts/Data/` 없음. 네트워크 0줄. 팀 인원·맵 개수·탱크 종수는 위 심볼에서만 읽는다.

---

## 검증

```bash
cd projects/tank-artillery
./tools/verify.sh all        # 밸런스 측정은 가드로 건너뛄
./tools/verify.sh compile
```

상수를 바꾸면 다시 돌려라. 특히 이 셋은 탱크가 갖힌다:

| 상수 | 값 | 바꾸면 |
|---|---|---|
| `CeilingCollapse.MinThickness` | 1.0m | 부유 덩어리 / 무널진 천장 붕괴 |
| `TankGroundProbe.StepHeight` | 1.2m | 탈출 가능 반경 변경 |
| `TankGroundProbe.WalkStep` | 0.25m | 크레이터에 갖힘 (√(2Rε)) |

`Sim` 을 바꾸는 커밋은 메시지에 「매치업 표를 움직일 수 있다」를 적는다.

---

## 실측 (지형 해너스, 유니티 없이)

| 항목 | 값 |
|---|---|
| 초기 지형 | 청크 903 · 삼각형 451,270 · **127 ms** · SDF **0 KB** |
| 일반탄 Rc 7m | SDF 2.9ms + 메시 2.3ms = **5.2 ms** |
| 굴착탄 Rc 16m | **15.1 ms** |
| 폭발 100회 | 271 ms · SDF 10.4 MB |
| 오버행 | 처마 1.5m / 공동 11.5m |
| 발밑 도려내기 | 2발로 지면 **14.9m 붕괴** |

판 길이(③ AI전, TwinHills 중급 4:4): 명세 §2-5-0. 숫자로 템포 레버를 움직이지 마라.

---

## 다음

탄도(M1)·파괴(M2)·AI 한 판(M3)·내용물(M4 구현)은 코드에 있다.
남은 것: M4 승률 게이트(포세이돈·이온·캐롯 이탈) · `BattleDemo` 규칙의 Sim 이전 · M5 네트워크.

순서(오너 09-19): ① 판 구성(맵·기후) → ② 수치 → ③ 표 확정 → ④ 네트워크.
TwinHills 는 기준선이므로 맨 마지막.
