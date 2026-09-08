# 없는 물건 — 자산 조달 목록 (조사만, 2026-09-09)

대장 지시(검수 전달 2026-09-09): 랩마다 하나씩 튀어나오던 「없는 물건」을 **한 번에 묶는다**.
**지금은 조사만이다 — 받지 않는다.** 다운로드는 오너 직행 사안이고, 실행하는 세션이 오너에게
직접 확인한다. 라이선스는 **CC0/상용 가능만**, 반입 시 `docs/ASSET_REGISTER.md`에 LICENSE·SOURCE_URL·
다운로드일·승인일을 기록해야 하며 **기록 없는 파일은 씬 반입 금지**(§11).

## 없는 물건 (랩에서 실제로 막힌 것)

| 물건 | 어디서 막혔나 | 지금 상태 |
|---|---|---|
| 모루·화덕 | `30_forge` 대장간 몸통이 무릎 높이 판매대(`stall.fbx` 1.0×0.4×0.7m) | 미결 표시(`RoleLook.cs`·`QaShots.cs`) |
| 절구통 | `34_mortar` | 미결 |
| **갱도 입구** | `20_mine` — 킷 벽 조각은 흰 판때기, 큐브는 자격 게이트, 볼록한 바위로는 구멍이 안 된다 | 미결(`BuildMine` 옆) |
| 마구간 건물 | `36_stable` 지붕도 말도 없는 울타리 사각형 | 큐 ③(자산 대기) |
| 목공소 건물 | `31_carpenter` 작업장 건물 없음 | 큐 ③(자산 대기) |
| 우물·게시판 | 큐 ② 마을 광장 — 광장 중심은 **분수(fountain-round, 실재 자산)**로 채웠고 우물·게시판은 킷에 없다 | **미결**(가짜 안 세움, 2026-09-09) |
| 부두 판때기·말뚝 | 큐 ⑤ 물가 | 해결 — planks·poles로 됨(2026-09-09) |

## A. 확인됨 — **물건 단위로 페이지가 열리는 것**(poly.pizza, 전부 CC0 명시)

단품이면 끝나는 것은 여기서 끝낸다. 각 줄은 **그 물건이 있다는 것이 확인된** 항목이다.

| 물건 | 모델 | 제작자 | URL | 라이선스 | 형식 |
|---|---|---|---|---|---|
| 모루 | Workbench Anvil | Kenney | https://poly.pizza/m/bY1pp3kAAb | CC0(페이지 명시) | OBJ/glTF |
| 대장간(건물) | Blacksmith | Quaternius | https://poly.pizza/m/bV52eTG1Aj | CC0(페이지 명시) | FBX/glTF |
| 마구간(건물) | Fantasy Stable | Quaternius | https://poly.pizza/m/qhNQSOGGbi | CC0(페이지 명시) | FBX/glTF |
| 목공소(건물) | Fantasy Sawmill(+ Sawmill Saw) | Quaternius | https://poly.pizza/m/alxTTFjKDM · /m/suqpa3jKWg | CC0(페이지 명시) | FBX/glTF |

## B. 미확인 — **팩(모듈 세트가 필요한 것)**

목차를 공개하지 않는 팩이다. 「그 안에 있다」는 **개연성이지 확인이 아니다** — 받아 봐야 안다.

| 팩 | 노리는 물건 | 출처 URL | 받는 방법 | 라이선스 |
|---|---|---|---|---|
| Kenney **Modular Cave Kit** | **갱도 입구**·터널·동굴 벽 40점 | https://kenney.nl/assets/modular-cave-kit | 직접 zip `kenney.nl/media/pages/assets/modular-cave-kit/37ec3cb12d-1783667097/kenney_modular-cave-kit_1.0.zip` (**curl 가능**) | CC0(팩 페이지 명시) |
| Quaternius **Fantasy Props MegaKit**(이미 승인·파일 대기) | 절구통·화덕·작업대·궤짝 등 200+ | https://quaternius.com/packs/fantasypropsmegakit.html | 사이트 직접 / itch (GitHub 없음) | CC0(팩 페이지 명시) |
| Quaternius **Medieval Village MegaKit** | 마을 건물 모듈 300+ (A의 단품으로 부족할 때) | https://quaternius.com/packs/medievalvillage.html | 사이트/itch(무료판은 일부만) | CC0(팩 페이지 명시) |

**갱도 입구는 단품으로 못 찾았다** — poly.pizza에서 mine/cave/tunnel entrance를 훑었지만 쓸 만한 단품이 없었다(나온 것은 맨홀 뚜껑·사원 입구·포털 문 따위였다). 그래서 갱도 입구만은 B에 남는다.
**절구통도 단품은 유료 픽밖에 없었다**(Poly by Google·MilkAndBanana의 mortar and pestle) — CC0 확인 못 함.

## 확인한 것과 **확인 못 한 것**(추측 금지)

- **확인**: 세 팩 모두 페이지에 CC0 명시. Kenney는 **직접 zip URL**이 있어 `curl`로 받힌다.
- **확인**: Kenney 3D 목차에 `Modular Cave Kit`이 실재한다(목차 페이지에서 이름 확인).
- **못 함**: 세 팩 어디에도 **품목 단위 목차(manifest)가 공개돼 있지 않다.** 그래서
  「Fantasy Props MegaKit 안에 모루·절구가 있다」와 「Cave Kit 안에 입구 조각이 있다」는
  **카테고리·팩 성격에 근거한 개연성이지 확인이 아니다.** 받은 뒤 첫 일은 목록 실측이다.
- **못 함**: **GitHub 공식 배포는 셋 다 없다**(대장 우선순위였다). Kenney 직접 zip이 그다음이다.
- 검색에서 나온 blacksmith 팩들(LazerBurns 등)은 **재배포 금지**라 이 저장소 규칙(§11 기록·반입)과
  맞지 않아 후보에서 뺐다.
