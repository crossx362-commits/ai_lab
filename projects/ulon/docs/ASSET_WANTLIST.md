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
| 우물·게시판 | 큐 ② 마을 광장 | 아직 안 감 |
| 부두 판때기·말뚝 | 큐 ⑤ 물가 | 아직 안 감 |

## 팩 후보 (최소 수로 묶음)

| 팩 | 덮는 물건 | 출처 URL | 받는 방법 | 라이선스 |
|---|---|---|---|---|
| **Quaternius Fantasy Props MegaKit** (이미 승인, 파일 대기) | 모루·화덕·절구 후보, 작업대·궤짝·좌판·도구 200+ | https://quaternius.com/packs/fantasypropsmegakit.html | 사이트 직접 다운로드 또는 https://quaternius.itch.io/fantasy-props-megakit (GitHub 배포 **없음**) | CC0(사이트 명시) |
| **Kenney Modular Cave Kit** | **갱도 입구**·터널·동굴 벽 40점 | https://kenney.nl/assets/modular-cave-kit | 직접 zip — `kenney.nl/media/pages/assets/modular-cave-kit/37ec3cb12d-1783667097/kenney_modular-cave-kit_1.0.zip` (**curl 가능**) | CC0(팩 페이지 명시) |
| **Quaternius Medieval Village MegaKit** | 마구간·목공소 등 **건물**(모듈 벽·지붕·계단 300+) | https://quaternius.com/packs/medievalvillage.html · https://quaternius.itch.io/medieval-village-megakit | 사이트/itch 다운로드(무료판은 일부 모델만) | CC0(팩 페이지 명시) |
| Poly Pizza 단품 — Kenney *Workbench Anvil* | 모루 하나만 급할 때의 예비 | https://poly.pizza/m/bY1pp3kAAb | 모델 페이지에서 OBJ/glTF 단품 | CC0(모델 페이지 명시) |

## 확인한 것과 **확인 못 한 것**(추측 금지)

- **확인**: 세 팩 모두 페이지에 CC0 명시. Kenney는 **직접 zip URL**이 있어 `curl`로 받힌다.
- **확인**: Kenney 3D 목차에 `Modular Cave Kit`이 실재한다(목차 페이지에서 이름 확인).
- **못 함**: 세 팩 어디에도 **품목 단위 목차(manifest)가 공개돼 있지 않다.** 그래서
  「Fantasy Props MegaKit 안에 모루·절구가 있다」와 「Cave Kit 안에 입구 조각이 있다」는
  **카테고리·팩 성격에 근거한 개연성이지 확인이 아니다.** 받은 뒤 첫 일은 목록 실측이다.
- **못 함**: **GitHub 공식 배포는 셋 다 없다**(대장 우선순위였다). Kenney 직접 zip이 그다음이다.
- 검색에서 나온 blacksmith 팩들(LazerBurns 등)은 **재배포 금지**라 이 저장소 규칙(§11 기록·반입)과
  맞지 않아 후보에서 뺐다.
