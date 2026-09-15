# TANKFALL — 3D 턴제 포격전

기획 검토 + 개발 명세: [`docs/GAME_SPEC_TANK_ARTILLERY.md`](../../docs/GAME_SPEC_TANK_ARTILLERY.md)

---

## 지금 실행하면 보이는 것

Unity Hub에서 **`projects/tank-artillery/unity`** 를 열고 **Play**를 누르면 된다.
씬을 만들 필요가 없다 — `PlayDemo.cs` 가 `[RuntimeInitializeOnLoadMethod]` 로 카메라·조명·지형·탱크를 전부 코드로 생성한다.

| 키 | 동작 |
|---|---|
| `W` `A` `S` `D` | 탱크 주행 (접지·턱 넘기 적용) |
| `←` `→` | 포탑 회전 |
| `↑` `↓` | 포신 각도 (−5° ~ 80°) |
| `Space` | 발사 → 지형 파괴 |
| `1` `2` `3` `4` | 포탄 반경 3 · 7 · 10 · 16m |
| 우클릭 드래그 | 카메라 회전 · 휠 줌 |
| `R` | 탱크 위치 리셋 |

**꼭 해볼 것** — 이 방향 전환(Heightfield → SDF)의 이유가 눈에 보인다:
1. 언덕 옆면을 **정면으로** 쏴서 파고들어라 → 위쪽 흙이 남아 **처마(오버행)** 가 생긴다
2. 그 안으로 계속 쏴라 → **동굴**이 뚫린다
3. `4`(굴착탄)로 발밑을 파라 → 지면이 꺼지고 탱크가 떨어진다 (기획서 §28)
4. 깊은 구덩이 안에서 나오려고 해봐라 → Rc=14m급에 갇히면 HUD가 `탈출 경로 0/8` 을 빨갛게 띄운다

⚠️ **아직 게임이 아니다.** 턴·피해·바람·탄도 파워는 M1~M3다. 지금 발사는 포신 방향 직선 레이마칭이다.

---

## 구조

```
projects/tank-artillery/
├── unity/                                  # 유니티 프로젝트 루트
│   └── Assets/_Project/Scripts/
│       ├── Sim/    (Tankfall.Sim.asmdef)   # 순수 C# · UnityEngine 참조 금지
│       │   ├── SdfVolume.cs                #   하이브리드 희소 SDF (파괴된 청크만 할당)
│       │   ├── SurfaceNets.cs              #   SDF → 메시 ("코드로 그린다")
│       │   ├── SdfDeformer.cs              #   구 빼기 폭발
│       │   ├── SdfRaymarch.cs              #   조준·클릭·시야 (지형 콜라이더 없음)
│       │   ├── CeilingCollapse.cs          #   부유 덩어리 방지
│       │   └── TankGroundProbe.cs          #   접지·턱 넘기·갇힘 판정
│       └── View/   (Tankfall.View.asmdef)  # MonoBehaviour · 그리기만
│           ├── TerrainView.cs              #   MeshData → Mesh, 청크 수명
│           ├── ProceduralTank.cs           #   탱크를 코드로 그린다 (에셋 없음)
│           └── PlayDemo.cs                 #   Play 누르면 전부 생성
├── tools/                                  # 유니티 없이 도는 검증 하네스
│   ├── verify.sh                           #   ./tools/verify.sh [all|sdf|play|compile]
│   ├── SdfVerify.cs                        #   지형·성능·오버행·동굴
│   └── GameplayVerify.cs                   #   §7-6 세 문제
└── _deprecated_heightfield/                # 폐기된 Heightfield 구현 + 사유
```

### SIM / VIEW 분리는 문서 규칙이 아니라 컴파일 규칙이다

`Tankfall.Sim.asmdef` 에 **`noEngineReferences: true`** 가 걸려 있다. Sim 안에서 `UnityEngine` 을 쓰면 **컴파일 에러**가 난다.

이게 지켜지면 서버(헤드리스)·AI·리플레이·궤적 프리뷰가 전부 같은 코드를 공유한다. 깨지면 결정론이 무너진다(명세 §4-1).

---

## 검증

유니티를 켜지 않고 돈다:

```bash
./tools/verify.sh
```

`compile` 은 「유니티에서 Play 가능한가」를, `sdf` 는 지형·성능을, `play` 는 §7-6 세 문제를 잰다.

⚠️ **상수를 바꿨으면 반드시 다시 돌려라.** 특히 이 셋은 바꾸면 탱크가 갇힌다:

| 상수 | 값 | 바꾸면 |
|---|---|---|
| `CeilingCollapse.MinThickness` | 1.0m | 공중에 흙이 뜨거나, 멀쩡한 천장이 무너진다 |
| `TankGroundProbe.StepHeight` | 1.2m | 크레이터 탈출 가능 반경이 통째로 바뀐다 |
| `TankGroundProbe.WalkStep` | 0.25m | 키우면 멀쩡한 크레이터에서 갇힌다 (√(2Rε) 때문) |

### 하네스 규칙

모든 검사에 **네거티브 컨트롤**이 붙어 있다 — 고치기 전에도 "통과"가 나오면 측정이 고장 난 것이다.
이 규칙 없이 만들었던 첫 하네스는 **세 검사 모두 거짓 통과**를 냈다(얇은 천장을 한 번도 안 만들었고, 갇힘을 "한 걸음 가능한가"로 쟀다).

---

## 실측 (Unity 6000.3 Roslyn, 유니티 없이 실행)

| 항목 | 값 |
|---|---|
| 초기 지형 | 청크 903 · 삼각형 451,270 · **127 ms** · SDF 메모리 **0 KB** |
| 일반탄 폭발(Rc 7m) | SDF 2.9ms + 메시 2.3ms = **5.2 ms** |
| 굴착탄 폭발(Rc 16m) | SDF 6.3ms + 메시 8.8ms = **15.1 ms** |
| 폭발 100회 누적 | 271 ms · SDF 10.4 MB |
| 오버행 | 처마 1.5m / 공동 11.5m |
| 발밑 도려내기 | 2발로 지면 **14.9m 붕괴** |

---

## 다음 (M1)

탄도. 명세 §5 — 해석해 `P(t) = P₀ + V₀t + ½at²` 하나를 서버·AI·프리뷰·리플레이가 공유한다.
`g=30`, 파워 100 = 81.2m/s = 사거리 220m, 비행 3.83초.
