# 재와 별 — 몬스터 애니메이션 리스트 · 스프라이트 생성 프롬프트 v0.1

> 상위 문서: [`GAME_MONSTERS_ASHES_TO_STARS.md`](GAME_MONSTERS_ASHES_TO_STARS.md) (로스터) · [`GAME_DESIGN_ASHES_TO_STARS.md`](GAME_DESIGN_ASHES_TO_STARS.md) (기획)
> 파이프라인: `blender/split_sheets.py` (시트 → 프레임) · `unity/Assets/Resources/sprites/` (런타임이 읽는 유일한 곳)
> 작성: 2026-08-13
>
> **이 문서의 원칙**: 애니메이션 프레임 수는 그림쟁이가 정하는 게 아니라 **기획서의 초 단위 수치가 정한다.** 돌진 예고가 0.8초면 예고 애니는 정확히 0.8초여야 하고, 아니면 유저가 예고를 보고도 못 피한다.

---

## 0. ⚠️ 먼저 — 이 문서는 최소화 방침에 종속된다

> ✅ **[`GAME_ART_RESOURCES.md`](GAME_ART_RESOURCES.md) §0-B(오너 지시 2026-08-13, 스프라이트 최소화)가 이 문서보다 우선한다.** 프롬프트를 쓰다 보면 "몬스터 20종이니 프롬프트도 20개"가 자연스러워 보이는데, **그게 정확히 오너가 금지한 것**이다.

| §0-B 규칙 | 이 문서에서의 의미 |
|---|---|
| **방향은 4방향만 렌더, 미러링으로 8방향** | 실제 생성은 `S`·`E`·`N` **3장**뿐. 8방향 프롬프트 금지 |
| **계열 5종은 색조로만 구분** (`MobDef.색조`) | **잡몹 실루엣은 AI 4종 = 프롬프트 4개.** 계열별 20종 개별 생성 금지 |
| **정예 6유형은 색 + 크기 + 오라** | 정예 신규 작화 **0장** — 오버레이 6종만 |
| **보스는 실루엣 4종 변주**, 전용은 상징적 2~3개만 | 20보스 개별 생성 금지 |

- 📌 **이 절은 초안을 고쳐 넣은 것이다.** 초안은 잡몹 20종 개별 프롬프트 + 보스 8방향(종당 350장)을 전제해 §0-B를 정면으로 뒤집고 있었다(총 물량 약 4,000장 → 실제 방침은 약 1,010장). 로스터의 "**종** 95개"는 **기획상의 종류**이지 **작화 단위**가 아니다 — 이 둘을 같은 수로 세는 순간 물량이 4배가 된다
- 💡 아래 §5의 계열별 20종 프롬프트는 **폐기가 아니라 보류**다 — 오너가 나중에 "계열별로 실루엣을 다르게 하자"고 방침을 완화하면 그때 쓸 수 있게 남겨 둔다. **지금 쓰지 않는다.**

---

## 1. 애니메이션 상태 목록

### 1-1. 네이밍 — 기존 파이프라인과 동일하게

현재 오너 픽셀아트 산출물이 이 규칙을 쓰고 있다(`unity/Assets/Resources/sprites/tank/tank_attack_00.png`):

```
<유닛ID>_<상태>_<프레임번호 2자리>.png        ← 상태별 스프라이트 (오너 시트 방식)
<유닛ID>_<방향번호>.png                       ← 8방향 단일 포즈 (블렌더 플레이스홀더 방식)
```

- ✅ **상태 영문명은 기존 6종을 그대로 계승**: `idle` · `walk` · `attack` · `special` · `hurt` · `death`
  (한글 시트 표기 `대기·이동·공격·특수·피격·사망`과 1:1 — `split_sheets.py`의 `상태` 배열이 이 매핑을 갖고 있다)
- 💡 몬스터는 여기에 **역할별 추가 상태**를 붙인다(아래 1-3). 새 이름을 만들 때도 **기존 6종은 절대 이름을 바꾸지 않는다** — 이미 32장이 그 이름으로 임포트돼 있다

### 1-2. 공통 6상태 (전 등급 필수)

| 상태 | 잡몹 | 정예 | 보스 | fps | 루프 | 이벤트 프레임 |
|---|---|---|---|---|---|---|
| `idle` | 2 | 3 | 4 | 4 | ✅ | — |
| `walk` | 2 | 4 | 6 | 8 | ✅ | — |
| `attack` | 2 | 4 | 6 | 10 | ✗ | **피해 판정 = 마지막 직전 프레임** |
| `special` | — | 4 | 6 | 8 | ✗ | 기믹 발동 프레임 명시 |
| `hurt` | 1 | 2 | 2 | 12 | ✗ | — |
| `death` | 3 | 4 | 6 | 8 | ✗ | 드랍 생성 = 마지막 프레임 |

- ⚠️ **피해 판정 프레임을 마지막에 두지 마라** — 마지막 프레임에서 판정하면 유저 눈에는 "때리는 모션이 끝난 뒤에 맞는" 것으로 보인다. 타격은 팔이 뻗은 순간(마지막 직전)이다
- 💡 `hurt`가 잡몹에 1프레임인 이유: 500체가 동시에 피격 모션을 재생하면 그게 곧 프레임 예산이다. 잡몹의 피격은 **모션이 아니라 색 플래시**(흰색 0.08초)로 처리하고 스프라이트는 1장만 둔다

### 1-3. 역할·계열별 추가 상태

| 상태 | 대상 | 프레임 | **길이(초)** | 근거 |
|---|---|---|---|---|
| `telegraph` | 돌진형 잡몹 | 4 @5fps | **0.80** | 로스터 §3-1 돌진 예고 0.8초 |
| `dash` | 돌진형 잡몹 | 2 @8fps 루프 | **1.20** | 돌진 지속 1.2초 |
| `stagger` | 돌진형 잡몹 | 2 @2fps | **1.00** | 돌진 후 경직 1.0초 = 수동 플레이의 반격 창 |
| `aim` | 원거리형 잡몹 | 2 @4fps | 0.50 | 조준선 노출 구간(§10-2 발사 예고) |
| `fire` | 원거리형 잡몹 | 2 @10fps | 0.20 | 발사 간격 2.4초 중 발사 동작만 |
| `revive` | **언데드 전 개체** | 3 @6fps | 0.50 | 잔해 부활(로스터 §2-1) |
| `explode` | **정령 전 개체** | 4 @10fps | **0.40** | 사망 폭발 예고 0.4초 — 이 길이가 곧 회피 가능성 |
| `summon` | 정예 소환술사 · 보스 | 4 @8fps | 0.50 | |
| `channel` | 정예 주술사 | 4 @8fps 루프 | — | 지속 회복 중임을 계속 보여야 함 |
| `phase` | 보스 | 6 @8fps | 0.75 | 페이즈 전환 연출(§10-5 예고 원칙) |
| `enrage` | 보스 | 4 @8fps | 0.50 | 격노 진입 |
| `cast_a`~`cast_e` | 보스 | 각 6 @10fps | 0.60 | 보스 스킬 최대 5종(§18-11) |

- ⚠️ **`telegraph` 0.80초와 `explode` 0.40초는 밸런스 수치다.** 애니메이터가 "좀 짧아 보여서" 늘리거나 줄이면 §3-1(회피 가능한 위협)과 §2-1(정령 폭발 예고)이 조용히 깨진다. **애니메이션 길이 변경 = 밸런스 변경**으로 취급하고 기획서 수치를 함께 고칠 것
- 💡 `revive`가 계열 단위인 이유: 언데드 20종이 아니라 **언데드 잡몹 4종 전부**가 부활하므로, 부활 모션은 4종 각각에 필요하다. 대신 **`death`의 마지막 프레임을 `revive`의 첫 프레임으로 재사용**하면 실제 신규 작화는 2장뿐

### 1-4. 총 프레임 예산 (§0-B 최소화 방침 적용)

> ⚠️ **세는 단위는 "몬스터 종"이 아니라 "실루엣"이다.** 잡몹 20종은 실루엣 4개(AI 4종)를 색조로 변주한 것이므로, 아래 표의 행도 4줄뿐이다.

| 실루엣 | 상태 조합 | 프레임 | 제작 방향 | 이미지 |
|---|---|---|---|---|
| 잡몹 **추적형** | 공통6 | 11 | **3** (`S`·`E`·`N`, 나머지 미러) | **33** |
| 잡몹 **포위형** | 공통6 | 11 | 3 | 33 |
| 잡몹 **돌진형** | 공통6 + telegraph·dash·stagger | 19 | 3 | 57 |
| 잡몹 **원거리형** | 공통6 + aim·fire | 15 | 3 | 45 |
| 계열 공용 `revive` | 언데드 색조일 때만 재생 | 2 | 3 | 6 |
| 계열 공용 `explode` | 정령 색조일 때만 재생 | 4 | 1 (방향 무관) | 4 |
| 정예 6유형 | 오버레이 | — | — | **0** (§7) |
| 보스 실루엣 4종 | 공통6 + phase·enrage·cast×3 | 46 | 3 | 138 × 4 = **552** |

**잡몹 전체 178장 + 보스 552장 ≈ 730장** — §0-B의 "약 1,010장" 예산 안에 들어온다.

- 💡 `revive`·`explode`가 **계열 공용 1벌**인 이유: 계열이 색조뿐이라 실루엣이 같다 → 언데드 4종이 각자 부활 모션을 가질 필요가 없다. 같은 모션에 색조만 얹는다
- 💡 **미러링 규칙**: `S`·`E`·`N` 3장만 만들고 `W`는 `E`의 X축 반전. 대각(`SE`·`SW`·`NE`·`NW`)은 **4방향으로 반올림**한다 — §0-A ⚠️("8방향을 요구하면 물량이 2배")
- ⚠️ **비대칭 디자인 금지** — 한쪽 어깨에만 견장, 한 손에만 무기 같은 디자인은 미러링하면 반대편으로 옮겨간다. **잡몹·정예·보스 전부** 좌우 대칭 실루엣을 원칙으로 한다(§0-B에서 보스도 4방향 미러링 대상이 되었으므로, 초안의 "보스만 비대칭 허용"은 철회)
- 📌 4096×4096 아틀라스 1장에 128px 타일 1,024개 → **전 몬스터가 단일 아틀라스**에 들어간다(§10-9 배칭 요구 충족, 여유 30%)

---

## 2. 시트 규격 — `split_sheets.py`가 읽을 수 있는 형태

> 📌 **이 규격을 지켜야 자동 분할이 된다.** 실측(2026-08-13, `split_sheets.py` 주석)으로 확정된 값이다.

| 항목 | 값 | 이유 |
|---|---|---|
| 배경색 | **#0D0D0D** (밝기 13) | 스플리터가 배경색과 **직접 비교**한다(허용 오차 ±10) |
| 격자선 | **밝기 45~130의 회색** (#3C3C3C~#828282) | 배경보다 밝고 캐릭터 외곽선보다 밝아야 선으로 인식됨 |
| 캐릭터 외곽선 | **밝기 0~4** (#000000~#040404) | ⚠️ 배경보다 **더 어둡다** — "어두우면 배경"이라는 임계값 방식이 원리적으로 틀린 이유 |
| 셀 크기 | 128×128 (권장 256으로 그린 뒤 축소) | 런타임 스프라이트 128px |
| 프레임 간 여백 | 셀 안에서 **6px 이상** | `GAP_MIN=6` — 프레임 경계 판정 |
| 레이아웃 | **행 = 상태, 열 = 프레임** | `SHEETS` 딕셔너리의 `상태`·`프레임수` 구조와 일치 |

- ⚠️ **AI 생성 이미지는 배경이 균일하지 않다** — "검정 배경"이라고 프롬프트에 써도 그라디언트·노이즈가 섞여 밝기가 5~40으로 흩어진다. 그러면 스플리터가 캐릭터를 조각낸다(실제로 겪은 사고). **대응: 생성 후 반드시 배경을 #0D0D0D 단색으로 평탄화하는 후처리 1단계를 거친다**
- 💡 후처리 없이 가려면 **투명 배경 PNG를 1장씩 개별 생성**하고 시트를 건너뛰는 편이 안전하다. 시트는 오너가 손으로 그릴 때 유리한 형식이지 AI 생성에 유리한 형식이 아니다

---

## 3. 스타일 프리픽스 (모든 프롬프트에 공통으로 붙인다)

> 💡 이 블록은 **한 글자도 바꾸지 않고** 재사용한다. 유닛마다 스타일 문구를 다르게 쓰면 95종이 제각각 다른 그림체가 되어 한 화면에 섞였을 때 무너진다.

```
STYLE_PREFIX =
pixel art game sprite, 2D isometric quarter view, camera pitched 30 degrees
down and rotated 45 degrees, orthographic projection, single character
centered, full body, readable silhouette, limited palette 16 colors,
hard black outline, flat cel shading with one light source from upper left,
no ground shadow baked in, no background scenery, transparent background,
128x128, crisp pixels, no anti-aliasing on outline
```

**한글 대응(작화 지시용)**: 픽셀아트 / 쿼터뷰(하강 30°·회전 45°, 직교) / 전신 1체 중앙 / **실루엣 가독성 최우선** / 16색 제한 / 검은 외곽선 / 좌상단 단일광 셀셰이딩 / 그림자 굽지 않음 / 배경 없음 / 128px

- 📌 **하강 30° / 회전 45° / 직교**는 임의 값이 아니라 `gen_characters.py`의 `CAM_PITCH=30`과 동일하다 — 기존 플레이스홀더와 각도가 다르면 한 화면에서 유닛마다 다른 방향을 보게 된다
- ⚠️ **"no ground shadow baked in"을 빼지 마라** — 스프라이트에 그림자가 구워져 있으면 쿼터뷰에서 유닛이 겹칠 때 남의 그림자가 위에 올라탄다. 그림자는 런타임에 별도 스프라이트로 깐다
- ⚠️ **실루엣 가독성이 최우선인 이유**: 화면에 500체가 동시에 있다(§10-9). 디테일은 어차피 안 보이고 **덩어리 모양만 보인다** — 잡몹 4종의 AI를 실루엣만으로 구분할 수 있어야 위치 선정이 성립한다

### 3-1. 네거티브 프롬프트 (공통)

```
NEGATIVE =
3D render, realistic, painterly, soft gradient, blurry, anti-aliased edges,
drop shadow, ground plane, grass, dirt, tiles, background, multiple characters,
text, watermark, UI, health bar, front view, side view orthogonal,
top-down 90 degrees, asymmetric shoulder armor, one-sided weapon
```

- 💡 `front view` / `side view` / `top-down 90` 를 막는 이유: 이미지 생성기는 방치하면 정면 초상화를 그린다. **쿼터뷰가 안 나오면 그 스프라이트는 전부 폐기**다
- 💡 `asymmetric shoulder armor` / `one-sided weapon`: §1-4의 미러링 제약(비대칭 금지)을 프롬프트 단계에서 강제

---

## 4. 모듈 프롬프트 — 계열 5 × AI 4의 곱을 문장으로

> 로스터가 곱셈 구조(계열 × AI)이므로 프롬프트도 **모듈을 조립**한다. 20종을 따로 쓰면 20번 다른 스타일이 섞인다.

### 4-1. 계열 모듈 (색·재질·정체성)

| 계열 | 프롬프트 조각 | 팔레트 |
|---|---|---|
| **야수** | `feral beast, matted fur, exposed fangs, lean muscular build, earthy brown and rust tones` | #6B4A2F · #A8763E · #D9A566 |
| **언데드** | `undead revenant, exposed bone, tattered gray shroud, hollow glowing eye sockets, ash-covered` | #4A4A52 · #8C8C7A · #C7C7B0 · 눈 #7FE0C0 |
| **마족** | `demonic fiend, dark violet chitinous skin, curved horns, faint purple magic glow` | #3B2350 · #6D3F9E · #B27BE8 |
| **기계** | `clockwork automaton, riveted steel plates, brass gears, glowing amber core` | #5A5A62 · #8E8E96 · 코어 #FFB347 |
| **정령** | `elemental spirit, semi-translucent body, floating particles, no solid feet, inner glow` | 계열색 + 발광 #FFFFFF |

- 💡 **계열은 색으로, AI는 형태로 구분**한다 — 색만으로 구분하면 색약 유저가 못 읽고, 형태만으로 구분하면 상성 판단이 안 된다. 두 축을 다른 감각 채널에 배정

### 4-2. AI 모듈 (실루엣·자세)

| AI | 프롬프트 조각 | 실루엣 키 |
|---|---|---|
| **추적** | `quadruped charging pose, low forward-leaning stance, head down` | 낮고 길다 |
| **포위** | `small scuttling creature, many short limbs, wide low body` | 넓고 납작하다 |
| **돌진** | `heavy bulky body, oversized front-facing ram or horn, braced legs` | 크고 앞이 무겁다 |
| **원거리** | `slender upright figure holding a ranged weapon, thin limbs, fragile frame` | 가늘고 서 있다 |

- ⚠️ **네 실루엣은 서로 겹치면 안 된다** — "낮고 긴 것 / 넓고 납작한 것 / 앞이 무거운 것 / 가늘고 선 것". 500체가 뭉쳐 있을 때 유저가 읽는 건 이 네 덩어리뿐이다. 새 잡몹을 추가할 때도 이 네 실루엣 중 하나에 반드시 속해야 한다

### 4-3. 조립 공식

```
[STYLE_PREFIX] + [계열 모듈] + [AI 모듈] + [개체 고유 1구] + [상태 포즈] + [NEGATIVE]
```

---

## 5. 실제로 생성할 프롬프트 — 잡몹 실루엣 4종

> ✅ **지금 만드는 것은 이 4개뿐이다**(§0-B 규칙 3). 계열 5종은 런타임 `MobDef.색조`가 처리한다.
> 아래는 `idle` 기준. 다른 상태는 §6의 포즈 조각으로 교체한다.

| 유닛ID | 프롬프트 (STYLE_PREFIX + 아래 + NEGATIVE) |
|---|---|
| `mob_chaser` | `hostile creature, quadruped charging pose, low forward-leaning stance, head down, lean muscular build, neutral gray-white base color for palette swapping, standing idle, weight settled` |
| `mob_swarmer` | `hostile creature, small scuttling form, many short limbs, wide low body, neutral gray-white base color for palette swapping, standing idle` |
| `mob_charger` | `hostile creature, heavy bulky body, oversized forward-facing ram horn, braced legs, neutral gray-white base color for palette swapping, standing idle` |
| `mob_ranged` | `hostile creature, slender upright figure holding a ranged weapon, thin fragile limbs, neutral gray-white base color for palette swapping, standing idle` |

- ⚠️ **`neutral gray-white base color for palette swapping`가 이 4줄에서 가장 중요한 구절이다.** 색조 셰이더로 계열 5색을 입히려면 원본이 **채도 없는 밝은 회색**이어야 한다. 원본에 갈색·보라가 이미 들어 있으면 색조를 곱했을 때 탁해져서 5계열이 서로 구분되지 않는다 — 계열 구분 전체가 이 한 구절에 달려 있다
- 💡 계열 정체성(털·뼈·뿔·기어·발광)은 **실루엣이 아니라 색조 + 파티클 이펙트**로 준다. 이펙트는 공용 8종 재사용(§0-B)
- ⚠️ **기존 플레이스홀더와 유닛ID를 맞췄다** — `mob_chaser`·`mob_swarmer`·`mob_ranged`는 이미 `unity/Assets/Resources/sprites/`에 8방향이 들어 있다. 같은 ID로 덮어쓰면 코드 수정 없이 교체된다(`mob_charger`만 신규)

---

## 5-B. 【보류】 계열별 20종 프롬프트

> ⚠️ **지금 쓰지 마라.** §0-B가 "계열은 색조로만 구분"으로 확정했으므로 아래 20종은 **방침이 완화될 때를 대비한 보관**이다.
> 쓰게 되는 조건: 프로토타입에서 **색조만으로는 계열이 안 읽힌다**는 실측이 나왔을 때. 그 전에는 §5의 4종만 만든다.

### 야수

| 유닛ID | 프롬프트 (STYLE_PREFIX + 아래 + NEGATIVE) |
|---|---|
| `mob_beast_chaser` (잿빛 들개) | `feral beast, matted ash-gray fur, exposed fangs, lean muscular build, quadruped charging pose, low forward-leaning stance, head down, ember-red eyes, earthy brown and rust palette` |
| `mob_beast_swarmer` (무리 살쾡이) | `feral beast, short bristled fur, small scuttling creature, many short limbs, wide low body, pack marking stripe on back, earthy brown and rust palette` |
| `mob_beast_charger` (뿔멧돼지) | `feral beast, coarse boar hide, heavy bulky body, oversized forward-curving tusks, braced legs, earthy brown and rust palette` |
| `mob_beast_ranged` (가시고슴도치) | `feral beast, quill-covered back, slender upright figure, thin limbs, quills raised as projectiles, fragile frame, earthy brown and rust palette` |

### 언데드

| 유닛ID | 프롬프트 |
|---|---|
| `mob_undead_chaser` (재의 시체) | `undead revenant, exposed rib bones, tattered gray shroud, hollow glowing teal eye sockets, ash-covered, shambling forward-leaning stance, head down` |
| `mob_undead_swarmer` (무덤손) | `undead revenant, severed crawling hands and forearms, many short bone limbs, wide low body, ash-covered, teal glow` |
| `mob_undead_charger` (광란 시체) | `undead revenant, bloated heavy corpse, oversized bony shoulder ram, braced legs, tattered shroud, teal glow` |
| `mob_undead_ranged` (유골 궁수) | `undead revenant, skeletal archer, slender upright figure holding a cracked bone bow, thin limbs, fragile frame, teal glowing eye sockets` |

### 마족

| 유닛ID | 프롬프트 |
|---|---|
| `mob_demon_chaser` (하급 임프) | `demonic fiend, dark violet chitinous skin, small curved horns, forward-leaning running stance, faint purple magic glow` |
| `mob_demon_swarmer` (그림자 추종자) | `demonic fiend, shadow-wreathed low creature, many short limbs, wide flat body, purple glow beneath` |
| `mob_demon_charger` (뿔악마) | `demonic fiend, heavy bulky body, massive forward-curving horn crown, braced hooved legs, dark violet chitin, purple glow` |
| `mob_demon_ranged` (저주받은 눈) | `demonic fiend, floating single large eyeball with trailing tendrils, slender hovering form, no legs, purple magic glow` |

### 기계

| 유닛ID | 프롬프트 |
|---|---|
| `mob_machine_chaser` (순찰 인형) | `clockwork automaton, riveted steel plates, brass gears, forward-leaning walker stance, glowing amber core in chest` |
| `mob_machine_swarmer` (포위 드론) | `clockwork automaton, small hovering disc drone, many short jointed legs, wide low body, amber optic lens` |
| `mob_machine_charger` (충각 기계) | `clockwork automaton, heavy armored ram vehicle, oversized front-facing steel ram plate, braced treads, brass rivets, amber core` |
| `mob_machine_ranged` (포탑 보행기) | `clockwork automaton, slender tripod walker with barrel-mounted turret head, thin metal limbs, fragile frame, amber lens` |

### 정령

| 유닛ID | 프롬프트 |
|---|---|
| `mob_spirit_chaser` (불티 정령) | `elemental spirit, semi-translucent ember body, floating fire particles, no solid feet, forward-streaming motion, inner orange glow` |
| `mob_spirit_swarmer` (안개 정령) | `elemental spirit, semi-translucent pale mist body, wide low drifting form, no solid feet, inner white glow` |
| `mob_spirit_charger` (낙뢰 정령) | `elemental spirit, semi-translucent crackling body, heavy concentrated core with arcing bolts, braced posture, inner electric blue glow` |
| `mob_spirit_ranged` (서리 정령) | `elemental spirit, semi-translucent ice body, slender upright form, sharp frost shards orbiting, no solid feet, inner pale cyan glow` |

---

## 6. 상태별 포즈 조각

> `idle` 프롬프트의 마지막 자세 구절만 아래로 **교체**한다. 유닛 설명은 그대로 두어야 같은 캐릭터로 나온다.

| 상태 | 교체 구절 |
|---|---|
| `idle` | `standing idle, weight settled, subtle breathing pose` |
| `walk` | `mid-stride walking pose, one limb forward, body weight shifted` |
| `attack` | `attack lunge, striking limb fully extended forward, body committed` |
| `hurt` | `recoiling backward, head snapped back, brief flinch` |
| `death` | `collapsing to the ground, body folding, losing structure` |
| `telegraph` | `winding up, body coiled backward, front limb planted, clear anticipation pose` |
| `dash` | `full-speed forward lunge, body horizontal, motion blur streaks behind` |
| `stagger` | `off-balance recovery, stumbling, head lowered, vulnerable` |
| `aim` | `drawing and aiming a ranged weapon at a distant target` |
| `fire` | `releasing a projectile, recoil pose, weapon snapped back` |
| `revive` | `rising from a pile of its own remains, half-assembled, unstable` |
| `explode` | `body swelling and cracking with inner light bursting through seams` |
| `channel` | `arms raised, sustained casting pose, energy tethers trailing outward` |
| `summon` | `slamming the ground, summoning circle forming beneath` |
| `phase` | `roaring transformation, armor breaking away, new form emerging` |
| `enrage` | `head thrown back roaring, body wreathed in rising energy` |

- 💡 **`telegraph`의 "clear anticipation pose"가 이 게임에서 가장 중요한 한 구절**이다 — 예고 동작이 안 읽히면 돌진형은 "피할 수 있는 위협"(§10-2)이 아니라 그냥 랜덤 피해가 된다. 이 프레임만은 생성 결과를 **축소해서(128px) 확인**할 것. 원본 크기에서 잘 보이는 예고는 실제 게임 크기에서 안 보이는 경우가 많다

---

## 7. 정예 30종 — 신규 작화 없이 만든다

> ✅ 정예는 잡몹 스프라이트 재사용 + **오라·색조·아이콘 오버레이**(로스터 §0). 30종을 따로 그리지 않는다.

| 정예 유형 | 오버레이 | 프롬프트(오버레이 에셋만 생성) |
|---|---|---|
| **수호자** | 청색 육각 실드 오라 + 방패 아이콘 | `hexagonal blue energy shield dome overlay, semi-transparent, pixel art, isolated on transparent background, 160x160` |
| **처형자** | 적색 잔상 트레일 + 칼날 아이콘 | `red motion-trail slash aura overlay, semi-transparent, pixel art, transparent background` |
| **주술사** | 녹색 회복 링(회전) + 십자 아이콘 | `green rotating healing rune ring overlay, semi-transparent, pixel art, transparent background` |
| **군단장** | 황색 상승 화살표 오라 + 깃발 아이콘 | `yellow rising arrow aura overlay, semi-transparent, pixel art, transparent background` |
| **저주술사** | 보라 하강 안개 + 해골 아이콘 | `purple descending curse mist overlay, semi-transparent, pixel art, transparent background` |
| **소환술사** | 자홍 소환진(바닥) + 눈 아이콘 | `magenta summoning circle ground decal, top-down quarter view, pixel art, transparent background` |

- ✅ **0.5초 안에 읽혀야 한다**(§10-2) — 정예 유형별 색은 위 6색으로 **고정**하고 계열 색과 절대 겹치지 않게 한다(계열=몸통 색, 정예=오라 색)
- 💡 정예는 잡몹 대비 **크기 ×1.4**(`MobDef.크기`)로 렌더 — 오라 없이 실루엣만으로도 "저건 크다"가 먼저 읽힌다. 색약 유저를 위한 두 번째 채널
- 💡 아이콘은 **머리 위 고정 UI**로 띄운다(스프라이트에 굽지 않음) — 유닛이 겹쳐도 아이콘은 항상 보인다

---

## 8. 보스 프롬프트

> ✅ **보스 20종을 개별 생성하지 않는다**(§0-B: "실루엣 4종을 크기·색·부착 장식으로 변주, 전용 모델은 상징적인 2~3개만").
> 보스도 잡몹과 같이 **3방향 렌더 + 미러링**이므로 좌우 대칭을 유지한다(초안의 "보스는 비대칭 허용"은 §0-B와 충돌해 철회).

**보스 전용 프리픽스 추가분**:
```
BOSS_SUFFIX =
imposing boss enemy, roughly 3x the height of a normal enemy,
massive symmetric silhouette, distinct readable weak point at center,
256x256, higher detail than common enemies
```

### 8-A. 실제로 생성할 것 — 보스 실루엣 4종

| 실루엣ID | 담당 보스 | 프롬프트 |
|---|---|---|
| `boss_humanoid` | 군주·마녀·장군·성전사 계열 (10·25·45·55·60·90층) | `towering humanoid overlord, long flowing mantle, crowned head, staff or blade held center, neutral gray-white base for palette swapping` |
| `boss_construct` | 골렘·파수꾼·기계 계열 (5·20·65·70층) | `colossal construct, thick layered armor plates, single glowing core at chest center, stout braced legs, neutral gray-white base` |
| `boss_beast` | 야수·용 계열 (15·75·80층) | `massive quadruped beast, broad horned skull, heavy shoulders, folded wings, neutral gray-white base` |
| `boss_aberration` | 눈·정령·별 계열 (30·35·40·85·95·100층) | `floating aberration, central eye or core, radial tendrils, no legs, neutral gray-white base` |

- 💡 **20보스 → 실루엣 4개 매핑이 위 표다.** 개별 보스는 **색조 + 크기 + 부착 장식(뿔·왕관·날개·후광) 오버레이**로 구분한다 — 부착 장식은 방향 무관 오버레이라 3방향을 다시 만들 필요가 없다
- ✅ **전용 작화를 허용하는 것은 2~3개뿐**(§0-B). 💡 우선순위: **10층 `boss_ash_lord`**(첫 환생석·첫 대보스) · **50층 `boss_death_warden`**(분수령·첫 특수직업) · **100층 `boss_tower_master`**(최종). 나머지 17종은 변주로 간다

### 8-B. 전용 작화 샘플 — 10층 대보스 `boss_ash_lord` (재의 군주 · 언데드)

```
[STYLE_PREFIX]
undead revenant lord, towering skeletal figure wrapped in a burnt regal
mantle, crown of embers, one arm replaced by a mass of fused bone,
hollow teal-glowing eye sockets, drifting ash particles,
standing idle, weight settled, slow menacing presence
[BOSS_SUFFIX]
[NEGATIVE − asymmetric 항목 제외]
```

### 8-C. 탑 보스 20종 고유 구절 — 【대부분 보류】

> ⚠️ 아래 20줄 중 **지금 쓰는 것은 굵게 표시된 3종(10·50·100층)뿐**이다. 나머지 17줄은 §8-A의 실루엣 4종에 **부착 장식·색조를 지시하는 설명문**으로만 쓴다(전신 생성 프롬프트로 쓰지 마라).

| 층 | 유닛ID | 고유 구절 |
|---|---|---|
| 5 | `boss_gate_golem` | `stone gatekeeper golem, moss-covered slabs, glowing seam cracks` |
| 10 | `boss_ash_lord` | `skeletal lord, burnt regal mantle, crown of embers` |
| 15 | `boss_chained_beast` | `massive chained beast, broken shackles, scarred hide` |
| 20 | `boss_steel_sentinel` | `colossal steel sentinel, layered plate armor, single amber eye` |
| 25 | `boss_obsidian_witch` | `obsidian witch, floating shards of black glass, violet robes` |
| 30 | `boss_abyss_eye` | `giant floating eye of the abyss, ring of smaller eyes, tendrils` |
| 35 | `boss_frostheart` | `frost heart elemental, crystalline core, jagged ice spires` |
| 40 | `boss_storm_king` | `storm elemental king, cyclone lower body, lightning crown` |
| 45 | `boss_bone_general` | `undead general, legion banner, commanding skeletal host` |
| **50** | `boss_death_warden` | `death warden, black scythe, veil of drifting souls` |
| 55 | `boss_fallen_templar` | `fallen templar, cracked holy armor, corrupted halo` |
| **60** | `boss_lightless_star` | `lightless constellation being, void-filled armor, dead starlight` |
| 65 | `boss_gear_judge` | `clockwork judge, scales of brass, frozen gear halo` |
| **70** | `boss_time_cogwork` | `time cogwork entity, rotating temporal rings, afterimages of itself` |
| 75 | `boss_egg_warden` | `dragon egg warden, three guardian heads, scaled shell shield` |
| **80** | `boss_ash_dragon` | `ash-gray dragon, tattered wings, ember breath glow in throat` |
| 85 | `boss_star_shard` | `living star shard, prismatic crystal body, orbiting fragments` |
| **90** | `boss_star_apostle` | `apostle of the stars, constellation-lined robes, six radiant wings` |
| 95 | `boss_tower_shadow` | `shadow of the tower, silhouette echoing past bosses, unstable form` |
| **100** | `boss_tower_master` | `master of the tower, five elemental aspects cycling across the body` |

- 💡 **95층은 "지난 보스들의 실루엣이 겹쳐 보이는" 디자인** — 기획의 "과거 보스 패턴 인용"(로스터 §8-2)을 그림에서도 반복해, 유저가 보자마자 무엇이 올지 알게 한다
- 💡 **100층은 페이즈마다 계열이 바뀌므로**(로스터 §8-2) 스프라이트도 5벌이 필요하다 — 실제로는 **몸통 1벌 + 계열별 색조·이펙트 오버레이 5종**으로 처리한다(보스 한 종에 5벌을 그리는 비용 회피)

---

## 9. 생성 후 검수 체크리스트

> ⚠️ 📌 이 프로젝트는 **"수치는 나왔는데 화면이 비어 있었다"**는 사고를 이미 겪었다(§21-1b — 텍스처 Read/Write 플래그 문제로 스프라이트를 하나도 안 그린 채 700fps PASS). 생성물도 같은 방식으로 조용히 실패한다.

생성한 스프라이트는 아래를 **전부** 통과해야 채택한다:

1. **128px로 축소해서 본다** — 원본에서 멋진 디테일은 실제 게임 크기에서 진흙이 된다. 검수는 항상 실사용 크기로
2. **실루엣만 검은색으로 칠해 본다** — AI 4종(낮고 긴 것 / 넓고 납작한 것 / 앞이 무거운 것 / 가늘고 선 것)이 구분되는가? 안 되면 폐기
3. **회전 각도** — 정면 초상화가 아니라 쿼터뷰인가? 기존 `player_knight_0.png`와 나란히 놓고 시선 각도 비교
4. **배경** — 완전 투명인가, 또는 #0D0D0D 단색인가? 그라디언트가 남아 있으면 `split_sheets.py`가 캐릭터를 조각낸다
5. **좌우 대칭**(잡몹·정예 한정) — 미러링해서 뒤집었을 때 어색한 곳이 없는가?
6. **20종을 한 화면에 늘어놓고 본다** — 계열 5색이 서로 구분되는가? 한 종만 보면 다 괜찮아 보인다
7. **회색조로 변환해서 본다** — 색을 뺐을 때도 등급(잡몹/정예/보스)이 크기와 형태로 구분되는가? (색약 대응 + 이펙트가 화면을 덮었을 때의 가독성)

- 💡 1·2·6·7은 **자동화 가능**하다 — 마루(`마루_게임개발`)의 검증 도구가 이미 "스크린샷의 유닛 픽셀을 센다"는 방식을 쓰고 있으므로(§21-1b), 실루엣 대비·계열 색 거리 검사를 같은 자리에 붙이면 사람이 매번 눈으로 확인하지 않아도 된다

---

## 10. 제작 순서 — W4에 필요한 것만

> ✅ **95종을 다 만들고 시작하지 않는다**(로스터 §9). W4가 답할 질문은 "계열 특성이 다르게 느껴지는가"와 "정예 우선순위 판단이 재미인가"뿐이다.

| 순서 | 제작물 | 장수 | 왜 지금 |
|---|---|---|---|
| 1 | 잡몹 실루엣 4종 (`idle`·`walk`·`attack`·`hurt`·`death`) | 33+33+33+33 = **132** | 기본 웨이브 |
| 2 | `revive`(계열 공용) + 언데드 색조 프리셋 | **6** | **잔해 부활이 실제로 다르게 느껴지는지**가 W4의 핵심 질문 |
| 3 | 돌진형 `telegraph`·`dash`·`stagger` | **24** | 0.8초 예고가 실제로 읽히는지 |
| 4 | 정예 오버레이 2종 (주술사·소환술사) | **2** | 방치하면 판이 안 끝나는 유일한 두 유형 |
| 5 | 보스 실루엣 1종 (`boss_construct`, 5층 문지기 골렘용) | **138** | 보스전 지휘(V3) 검증 |
| — | **나머지 실루엣·계열 색조·보스 변주** | — | W4 결과가 나온 뒤에 |

**W4 총 제작량 ≈ 302장** (§0-B 전체 예산 약 1,010장의 30%)

- ⚠️ 5번(보스 138장)이 나머지 전부에 맞먹는다. **W4에서는 보스도 `S`·`E` 2방향으로만** 만들어 92장으로 더 줄여도 검증에는 지장이 없다 — 재미없다고 판명될 수 있는 보스에 물량을 먼저 쓰는 것이 이 프로젝트에서 가장 비싼 실수가 된다
- 💡 **1~4번은 이미 있는 플레이스홀더를 같은 ID로 덮어쓰면 코드 수정이 0이다**(§5) — 아트만 갈아 끼우고 바로 W4를 돌릴 수 있다
