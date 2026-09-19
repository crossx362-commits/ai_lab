# SESSION HANDOFF — tank-artillery

> **이 파일은 «다음 세션이 5분 안에 무엇을 할지 알기 위한» 문서다.** 과정·사고 전말은 안 적는다.
> 상세는 이미 다른 곳에 있다: 설계·결정은 `docs/GAME_SPEC_TANK_ARTILLERY.md`, 사고와 교훈은
> `docs/HARNESS_GUARDRAILS_LEDGER.md`, 무엇을 왜 고쳤는지는 **커밋 메시지**.
> **완료 항목은 여기서 지운다**(CLAUDE.md 규칙). 남는 건 아래 넷뿐이다.

---

## 🙋 오너가 직접 해야 하는 것 — 넷

> ⚠️ **①②와 ③④ 를 한 판에 섞지 마라.** 전투에는 바람·기후·AI 턴·서든데스가 섞여
> «무엇이 달라졌는지»를 못 가른다. 연습장은 **산포가 꺼져 있어** 변수 하나가 이미 빠져 있다.

### ①② 중력 대조군 — 연습장, 약 2분 (§11 M1)
M1 은 지금 **「네거티브 컨트롤 미실시」 딱지가 붙은 통과**다(§12-1). 이걸 하면 딱지가 떨어진다.
```bash
open -a /Users/junholee/ai_lab/projects/tank-artillery/unity/Build/Tankfall.app --args -practice -screen-width 1600 -screen-height 900 -screen-fullscreen 0
```
```bash
open -a /Users/junholee/ai_lab/projects/tank-artillery/unity/Build/Tankfall.app --args -practice -g 15 -screen-width 1600 -screen-height 900 -screen-fullscreen 0
```
두 번째는 화면 상단에 **「중력 대조군 g=15」**가 뜬다(안 뜨면 인자가 안 먹은 것이다).
🎯 **「멀리 간다」는 답이 아니다**(중력 절반이니 당연하다). 물음은 하나 —
**같은 파워·각도로 쐈을 때 조준 감을 «다시 배워야 할 만큼» 달라지는가?**
· **"달라진다"** → M1 통과 확정(딱지 제거) · **"비슷하다"** → §11 M1 대로 **상수 재설계**, 작업 멈추고 판단 받는다.

### ③ 전투 한 판 완주 — 약 11분
```bash
open -a /Users/junholee/ai_lab/projects/tank-artillery/unity/Build/Tankfall.app
```
⚠️ **결과 화면까지 가야 저장된다.** 중간에 끄면 아무것도 안 쌓인다(결함 아님).
- **§2-5-0 의 18초/턴**: 결과 화면의 **「내 턴 평균 ○○초 · 전체 턴 평균 ○○초」**를 알려 주면 그 칸이 닫힌다.
  (사람 턴이 0 이면 **「내 턴 기록 없음」**이라고 뜬다 — 「0.0초」로 안 나온다.)
- **§11 M0 마지막 칸**: 「탱크가 지형에 제대로 붙어 있는가」(보기에 자연스러운가). 나머지 둘은 코드가 닫았다.

### ④ 궤적 체감 — ①②와 같은 자리에서
연습장에서 **두세 기종을 바꿔 가며** 같은 파워·각도로 쏴 본다(예: **캐터펄트 ↔ 이온어태커 ↔ 캐논**).
🎯 **기종마다 «다르게 난다»고 느껴지는가? 유독 조준하기 어려운 기종이 있는가?**
⚠️ **밸런스 표로는 절대 안 나오는 축이다** — AI 는 역산으로 정확히 쏘므로 중간 탄도가 바뀌어도 승률에 안 잡힌다.
지표로는 이미 갈렸다(정점위치 0.494~0.496 → **0.429~0.571**). 남은 물음은 **「지표가 갈린 걸 눈이 읽는가」** 하나다.

---

## 📌 오너 판단 대기 — 셋 (늘리지 마라)

> 대기가 길어지면 오너는 답을 안 한다. 셋 다 **막혀서 못 가는** 것들이다.
> 안 막힌 것은 판정해서 닫는다(예: 자유 초점 카메라 = 「지금 만들지 않는다」로 닫음).

1. **맵 형태가 단조롭다.** 평평한 대지와 매끈한 돔뿐이다. 증거 **둘**: 3D 화면(`/tmp/maps_Clear.png`) ·
   **미니맵 실루엣**에서도 TwinHills·Crater 만 형태가 서고 나머지 넷은 «무늬»로 읽힌다(`/tmp/mini_maps6.png`).
   **서로 다른 두 표현이 각각 가리켰다.**
   고치려면 `MapHeightFunction` 높이 함수를 만져야 하는데 **명중률·엄폐·이동에 직접 닿는다**.
   🔑 **올릴 때의 틀**: **TwinHills 는 동결**(지금까지의 모든 측정이 그 맵이다) · 나머지 다섯만 ·
   각 맵에 「이름이 약속하는 형태」 한 줄(분화구·계단·계곡·능선·황무지).

2. **(A) 속도 비례 항력** — 궤적을 더 가르는 축. §13 이 「공기저항 미사용 → 닫힌 해, 결정론」을
   **명시 결정**한 사항이라 기획서 본문을 뒤집는 일이다.
   ⚠️ **급하지 않다** — 수평 상수 가속만으로 이미 합격선을 넘었다.

3. **기후 굴림이 «확률»이 아니다.** `WallChance/TornadoChance = 0.35` 인데 `_airRng` 이 **고정 시드**라
   **6맵 전부 한 글자도 다르지 않다**: `벽(x140 z87) · 회오리(x147 z87)` — 항상 둘 다, 항상 한가운데, 7m 간격으로 겹쳐서.
   기획서 35% ↔ 실제 100%.
   🔑 **고치는 법은 이미 답이 있다**(`_airRng` 을 판 시드에서 유도 — 게임은 판마다 달라지고 하네스는 재현된다).
   ⚠️ **남은 건 「고쳐도 되나」뿐이다.** §2-5-0·12×12·난이도 표가 **전부 이 고정 배치를 전제로** 나왔다.
   (같은 모양의 Boom 시드는 **고쳤다** — 하네스가 Boom 을 안 켜서 무효가 되는 표가 없었다. **«무효가 되는 표가 있느냐»가 둘을 가른다.**)

---

## 🛑 운영 가드 — 코드가 막는다 (문서 규칙이 아니다)

| 가드 | 무엇을 막나 | 여는 법 | 막혔을 때 |
|---|---|---|---|
| `unity/Build/.frozen` | 유니티 빌드(오너가 플레이하는 동안 굽지 않기) | 그 파일을 지운다. **우회 스위치 없음** | `unity_build.sh` **rc=3** |
| `balance_guard()` | **`BattleSimVerify`(매치업 승률표)** — 오너가 막은 그 측정 | `TANKFALL_BALANCE_OK=1` | `verify.sh battle` **rc=3**, `all` 은 건너뛰고 나머지는 살린다 |
| 자기 복사본 재실행 | 실행 중인 `verify.sh`·`unity_build.sh` 를 편집해 바이트가 밀리는 사고 | — | 자동 |

- **`TANKFALL_BUILD_DRYRUN=1`** — 빌드 직전까지만 가고 멈춘다. **가드를 확인할 땐 이걸 써라**
  (「일찍 죽겠지」라고 가정하고 돌리다 동결을 깬 적이 있다).
- ⚠️ **밸런스·네트워크는 여전히 정지**(오너 지시). `TankStats.cs` 수치 · 매치업 표 근거 조정 ·
  `TANKFALL_ROW=` 스윕 · AI 행동 변경 · 네트워크(M5). **기능·버그·연출·UI·검증 인프라는 계속해도 된다.**
- ⚠️ **팀 인원 4:4 의 단일 소스는 `MapHeightFunction.TeamSize`** — 팀 인원을 뜻하는 숫자를 다시 박지 마라.
  (`3` 이 팀 인원이 **아닌** 곳이 훨씬 많다: 날씨 3종·"3턴마다"·다탄두 발수·승/무/패·정점 xyz… 일괄 치환하면 조용히 깨진다.)
- 나머지 「되돌리면 안 되는 결정」은 **기획서 §1-1**(문서보다 코드가 이긴다 표)에 있다.

---

## 🎨 그래픽 남은 것

**`docs/GRAPHICS_BACKLOG.md`** — 닫힌 것 말고 **아직 안 한 것**만. 각 줄에 상태·비용·**밸런스에 닿는가**.
⚠️ 밸런스가 풀려서(2026-09-19) **맵 형태·기후 굴림은 그래픽이 아니라 ① 판 구성**으로 옮겨 붙었다.

---

## 📐 무엇을 잴 수 있나 — **만들기 전에 여기부터 봐라**

> 이게 있는 줄 모르면 **또 만든다**. 실제로 한 세션 안에서 같은 원근 특이점을 두 번 만든 적이 있다.
> 각 줄은 **«어떤 질문에 답하는가»**로 적었다 — 질문이 다르면 도구도 다르다.

| 도구 | 어떤 질문에 답하나 | 부르는 법 |
|---|---|---|
| `Environment.SkyReport(cam)` | **하늘이 화면에 남아 있나** — 지평선 위 몇 도가 보이고, 원경이 그중 얼마를 덮나 | `-autoshot` 의 `11_전투개시` 에서 자동 로그 |
| `ClimateReport()` | **기후가 이 장면에 보이나**(면적·거리). ⚠️ **가림은 고려 못 하는 상한**이다 | **장면마다** 자동 로그 |
| `ClimatePose()` / `BoomPose()` | **제대로 보면 어떤가**(운에 안 맡긴다). `FramePose(center, span, dir, minDist)` 공용 | `10_기후` · `10b_지뢰밭` 자동 촬영 |
| `TANKFALL_PATHS=<csv>` | **궤적이 «모양»으로 갈렸나, «사거리»로만 갈렸나** — 정점위치·정점높이비 | `TANKFALL_PATHS=/tmp/p.csv ./tools/verify.sh ball` |
| `ball` **[1-1]** | **프로파일이 사거리를 바꿨나**(기종별, 켜고/끄고 나란히) | `./tools/verify.sh ball` |
| `sdf` **[6]** | **레이마치가 «지금 지형»을 읽나**(상수가 아니라) — 파면 거리가 늘어나는가 | `./tools/verify.sh sdf` |
| 미니맵 파낸칸 로그 | **파낸 자리가 실제로 잡히나** — 「안 보인다」와 「안 잡힌다」를 가른다 | 자동 모드에서 재생성 시 로그 |
| `기후 굴림` 로그 | **이번 판에 기후가 떴나** — 「안 보인다」와 「안 떴다」를 가른다 | 자동 모드 로그 |
| `TerrainView.RebuildCount` | **지형이 바뀌었나**(시간·프레임으로 재지 마라 — 안 바뀌었을 때 갈고 바뀐 직후엔 늦다) | 코드에서 읽는다 |
| 지형재생성 비용 로그 | 재생성 한 번에 **몇 ms · 몇 청크 · 파낸 정점 몇 개** | `_autoMode` 에서 자동 |
| `./tools/verify.sh dead` | **아무도 안 부르는 멤버가 있나**(프레임워크 호출은 제외하고, 제외한 걸 출력한다) | `./tools/verify.sh dead` |
| `-forceult` | **궁극기 연출이 실제로 어떻게 보이나**(버섯구름은 착탄 **뒤** 1.7초에 자란다) | `--args -autoshot -forceult` |
| `-noclimate` | 기후 대조군. ⚠️ **`-autoshot` 은 결정론이 아니라** 이미지 비교로는 못 잰다(잡음 695k > 신호 115k) | `--args -autoshot -noclimate` |

---

## 검증 명령 (맥이 운영 기계 — `primary_platform: darwin`)

```bash
cd projects/tank-artillery
./tools/verify.sh all        # 전체(38개 검사). 밸런스 측정은 가드로 건너뛴다
./tools/verify.sh compile    # 유니티 Play 가능 + manifest + 착탄 규칙 단일화
./tools/verify.sh game       # 게임 자체검사 10종 (빌드함 · TANKFALL_SKIP_BUILD=1 로 생략)
```
개별: `sdf | play | ball | turn | shell | nice | map | hit | guide | aimove | shellpick | dead`

빌드·실행(맥):
```bash
./tools/unity_build.sh
open -a "$PWD/unity/Build/Tankfall.app" --args -autoshot -shotdir /tmp/caps \
  -screen-width 1600 -screen-height 900 -screen-fullscreen 0
```
⚠️ **스크린샷 모드는 반드시 `open -a`.** 바이너리를 직접 부르면 캡처가 전부 실패하고
   `-nographics` 는 **PNG 가 새까맣게 나오는데 로그는 통과로 찍힌다.**
⚠️ 실행 파일 이름은 **`Contents/MacOS/unity`**(`Tankfall` 이 아니다).
   ⚠️ `.app` **디렉터리 mtime 은 빌드 시각이 아니다** — 최신 여부는 그 실행 파일 mtime 으로 봐라.
⚠️ **`timeout` 은 맥에 없다.** 기다려야 하면 `open -W`.

게임 자체검사(전부 rc=0): `-supplyselftest | -ultselftest | -impairselftest | -climateselftest |
-boomselftest | -rosterselftest | -practiceselftest | -gameselftest | -settingsselftest | -uiselftest`
무인 모드를 새로 만들면 **`headless` 목록에만** 추가하면 된다.
