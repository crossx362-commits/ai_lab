# 펫과나 마이펫 게임 — 에셋 생성 프롬프트 팩 (v1, 2026-07-07)

모든 에셋은 **쿼터뷰(3/4 아이소메트릭)·입체 페인터리·투명배경**으로 통일한다.
이미 확보된 5종(나무·그네·개집·수영장·분수대)이 스타일 기준점이다.

---

## 공통 스타일 서픽스 (모든 프롬프트 뒤에 붙임)

```
3/4 quarter view, isometric 35-degree camera angle, cute cozy mobile game asset,
volumetric soft painterly 3D style, warm sunlight from top-left,
soft contact shadow on ground, rounded chunky shapes, warm vivid colors,
transparent background, single object centered, no text,
animal crossing aesthetic
```

통일 규칙:
- **카메라**: 항상 좌상단에서 내려다보는 35° 쿼터뷰 (기준 5종과 동일 각도)
- **조명**: 좌상단 웜 라이트 + 바닥 소프트 그림자 (그림자 방향 우하단)
- **배경**: 투명 (생성기가 체커보드/흰배경을 주면 배경제거 후 저장)
- **캔버스**: 아이템·펫·먹이 = 1024×1024 정방형 / 배경 씬 = 1792×1024 (16:9)

## 사이즈 템플릿 (오브젝트가 캔버스에서 차지하는 비율 → 인게임 기본 크기)

| 클래스 | 캔버스 점유율 | 인게임 기본 | 대상 |
|---|---|---|---|
| **L (대형)** | ~85% | 140–180px | 개집·수영장·분수대·나무·그네·소파·침대·벽난로·황금개집 |
| **M (중형)** | ~70% | 90–130px | 울타리·벤치·파라솔·텃밭·창문·러그·책장·캣타워·TV·장난감상자 |
| **S (소형)** | ~55% | 50–80px | 밥그릇·꽃밭·디딤돌·공·쿠션·램프·화분 |
| **먹이** | ~60% (살짝 위에서) | 시트 아이콘 64px | 6종 |
| **펫** | ~75%, 발끝이 캔버스 하단 15% 지점 | 단계별 90–150px | 강아지·고양이 × 4단계 |
| **배경** | 전체 (중앙은 비워둠) | 스테이지 풀폭 | 마당·방 |

> 점유율을 지켜야 게임 안에서 아이템끼리 크기가 자연스럽게 맞는다.
> 프롬프트에 `object fills about 85% of frame` 식으로 명시.

---

## 마당 아이템 (14종 — ✅ 5종 확보)

| # | 파일명 | 상태 | 프롬프트 앞부분 |
|---|---|---|---|
| 1 | `yard/tree.png` | ✅ 확보 | (나무+새집+꽃 화단) |
| 2 | `yard/swing.png` | ✅ 확보 | (나무 그네) |
| 3 | `yard/doghouse.png` | ✅ 확보 | (발바닥 엠블럼 개집+화분) |
| 4 | `yard/pool.png` | ✅ 확보 | (비치볼 수영장) |
| 5 | `yard/fountain.png` | ✅ 확보 | (꽃 두른 분수대) |
| 6 | `yard/fence.png` | 생성 | short cream-white wooden picket fence section with two horizontal rails, object fills 70% of frame |
| 7 | `yard/flowerbed.png` | 생성 | small round flower bed with white and yellow daisies and stone border, fills 55% |
| 8 | `yard/stones.png` | 생성 | three beige round stepping stones on small grass patch, fills 55% |
| 9 | `yard/bowl.png` | 생성 | red ceramic pet food bowl filled with brown kibble, fills 55% |
| 10 | `yard/parasol.png` | 생성 | cream and coral striped garden parasol with small cushion underneath, fills 70% |
| 11 | `yard/bench.png` | 생성 | small warm wooden garden bench, fills 70% |
| 12 | `yard/ball.png` | 생성 | coral and cream striped rubber ball, fills 55% |
| 13 | `yard/garden.png` | 생성 | tiny vegetable garden patch with carrots and wooden sign, fills 70% |
| 14 | `yard/golden_doghouse.png` | 생성 (Lv10) | golden luxury doghouse with crown emblem and gem decorations, glowing, fills 85% |

## 방 아이템 (12종)

| # | 파일명 | 프롬프트 앞부분 |
|---|---|---|
| 1 | `room/sofa.png` | sage green cozy fabric sofa with cream cushions, fills 85% |
| 2 | `room/petbed.png` | round plush pet bed, coral color with cream inner cushion, fills 70% |
| 3 | `room/rug.png` | oval striped rug, coral and cream tones, flat on floor, fills 70% |
| 4 | `room/window.png` | wooden window frame with sunny sky view and small curtain, front-facing wall item, fills 70% |
| 5 | `room/tv.png` | small retro TV on low wooden stand, fills 70% |
| 6 | `room/bookshelf.png` | small warm wooden bookshelf with colorful books and a plant, fills 70% |
| 7 | `room/lamp.png` | cozy floor lamp with warm glowing cream shade, fills 55% |
| 8 | `room/fireplace.png` | small stone fireplace with warm crackling fire, fills 85% |
| 9 | `room/cattower.png` | beige carpeted cat tower with two platforms, fills 70% |
| 10 | `room/cushion.png` | soft round floor cushion, honey yellow, fills 55% |
| 11 | `room/plant.png` | potted leafy plant in terracotta pot, fills 55% |
| 12 | `room/toybox.png` | open wooden toy box with balls and rope toys spilling out, fills 70% |

## 먹이 (6종 — 시트 아이콘 + 스테이지 투척용 공용)

| # | 파일명 | 프롬프트 앞부분 |
|---|---|---|
| 1 | `food/kibble.png` | red pet bowl filled with brown kibble pieces |
| 2 | `food/bone.png` | cream white dog bone treat |
| 3 | `food/milk.png` | small glass milk bottle with blue cap |
| 4 | `food/meat.png` | juicy roasted meat drumstick |
| 5 | `food/cake.png` | strawberry cream cake slice |
| 6 | `food/bento.png` | open bento lunch box with rice and vegetables |

## 펫 캐릭터 (강아지·고양이 × 성장 4단계 = 8종)

공통 포즈 문구: `sitting, facing slightly left, happy open-mouth smile,
front 3/4 view, feet on ground, object fills 75% of frame`

| 파일명 | 변형 |
|---|---|
| `pet/dog_1_baby.png` | tiny chubby baby puppy, oversized head, golden cream fur |
| `pet/dog_2_junior.png` | young puppy wearing a small coral red scarf |
| `pet/dog_3_adult.png` | grown dog wearing blue collar with gold tag, confident |
| `pet/dog_4_king.png` | majestic dog wearing small golden crown, sparkles around |
| `pet/cat_1_baby.png` | tiny chubby baby kitten, oversized head, gray fur |
| `pet/cat_2_junior.png` | young kitten wearing a small coral red scarf |
| `pet/cat_3_adult.png` | grown cat wearing blue collar with gold tag, elegant |
| `pet/cat_4_king.png` | majestic cat wearing small golden crown, sparkles around |

> 4단계 모두 **같은 종·같은 털색·같은 포즈**로 나오게 한 세션에서 연달아 생성 권장.
> 크기 차이는 게임이 스케일로 처리하므로 그림은 동일 점유율(75%)로.

## 배경 씬 (2종, 1792×1024)

| 파일명 | 프롬프트 |
|---|---|
| `bg/yard.png` | wide cozy backyard scene, green lawn with soft mowing stripes, blue sky with fluffy clouds, distant hedge and picket fence at horizon, **empty center area for item placement**, 3/4 quarter view + 공통 서픽스 |
| `bg/room.png` | wide cozy room interior, warm cream wallpaper, honey wooden plank floor, soft afternoon light from left window glow, **empty center area for item placement**, 3/4 quarter view + 공통 서픽스 |

---

## 저장 위치·규칙

```
projects/petnna/images/petgame/
├── yard/   ├── room/   ├── food/   ├── pet/   └── bg/
```
- PNG, 투명배경(배경 씬 제외), 파일명은 위 표 그대로 (소문자·언더스코어)
- 확보된 5종부터 위 규칙대로 저장하면 게임이 바로 읽는다
- 아직 없는 에셋은 게임이 임시 플레이스홀더(이모지)로 표시 → 파일 추가 시 자동 교체
