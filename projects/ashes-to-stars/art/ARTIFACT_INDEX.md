# 재와 별 아트 산출물 인덱스

> 최종 점검: 2026-08-17 · 제작 방식: **힉스필드 2D 픽셀아트** · UI 크롬은 **Grok Imagine**

아트 생성 작업물은 생성 원본과 Unity 런타임 반입물을 분리한다. `out_*` 폴더는 재생성·후처리용 원본이며, 게임은 `unity/Assets/Resources/` 아래 파일만 읽는다. 블렌더 이미지는 과거 플레이스홀더와 네거티브 비교본으로만 유지하며 신규 최종 아트 제작에 사용하지 않는다.

## 런타임 연결 현황

| 종류 | 생성·후처리 원본 | Unity 반입 경로 | 소비 코드 | 점검 상태 |
|---|---|---|---|---|
| 기본 캐릭터 4종 | `out_char/` | `unity/Assets/Resources/sprites/{tank,dps,healer,buffer}/` | `SpriteBank.cs` | ✅ 4종 × 13프레임 반입·전투 표시 |
| 마법사 | `out_char_mage/frames/` | `unity/Assets/Resources/sprites/mage/` | `SpriteBank.cs`, `StarterPick` | ✅ 13프레임 반입·전투·시작 마딜 카드 |
| 몬스터 행동형 4종 | `out_p2/frames_*` | `unity/Assets/Resources/sprites/mob_{chaser,charger,ranged,swarmer}/` | `SpriteBank.cs` | ✅ 4종 × 22프레임 반입·상호 구분 화면 확인 |
| 보스 실루엣 4종 | `out_p6_boss/` · 애니는 `out_boss_anim/` | `unity/Assets/Resources/sprites/boss_{brute,serpent,wraith,construct}/` 각 16프레임 | `SpriteBank.BossAnim`, `BossBattle` | 🟡 idle·attack·hurt·death 반입. 영상 파이프는 ZDR로 실패해 키프레임 편집 |
| 화면 배경 9종 | `out_p8_bg/bg_*.png` · 타이틀은 Imagine `11.jpg` | `unity/Assets/Resources/bg/bg_*.png` | `GameScreen.cs` | 🟡 허브 6 + 결과·던전 유지. `bg_title`만 2026-08-17 Imagine 교체 |
| UI 크롬 8종 | `art/out_ui_chrome/` | `unity/Assets/Resources/ui/chrome/{panel,button_*,portrait_frame,hp_frame,xp_frame,boss_hp_frame}.png` | `UiAtlas.cs` | 🟡 솔로 텍스처 우선, 아틀라스 아이콘은 유지. 화면 QA 전 |
| 마을·나무 16종 | `out_p3_village/`, `out_p5_trees/` | `unity/Assets/Resources/props/` | `FieldDecor.cs` | ✅ `village_tree_0`을 집 옆에만 세움(`qa_hunt.png` 열매나무). 길에 안 올라감 |
| 영지 기능 건물 3종 | `out_estate_buildings/` | `unity/Assets/Resources/props/estate_{smith,mausoleum,tower}_0.png` | `EstateBuildings.cs` | ✅ 대장간·영묘·탑 반입. `qa_go:Estate.png`에서 집·우물·등불과 갈림. 본성·광산·창고·수비대는 집·헛간 유지 |
| 공용 이펙트 8종 | `out_p4_fx/` | `unity/Assets/Resources/FX/` | `FxPool.cs` | ✅ 정적 8장 반입·코드 애니메이션 |

## 아직 새로 만들어야 하는 것

- **1차 전직 11종 전용 외형**: 현재 별도 완성본은 마법사뿐이다. 기본 딜러 등 기존 그림을 새 전직 완성본으로 중복 집계하지 않는다.
- **보스 애니메이션**: 4실루엣 정적 1장씩만 있다. 같은 보스를 다시 정적으로 만들지 말고, 필요 시 기존 실루엣을 참조해 상태·방향 시트를 만든다.
- **영지 기능 건물 나머지 4종**: 대장간·영묘·탑은 `estate_*_0`. 본성·광산·창고·수비대는 아직 집·헛간. `village_house_*`는 필드 장식이다.
- **UI·스킬·장비 아이콘**: 기존 범용 아이콘과 완성 목록을 먼저 대조한 뒤 비어 있는 키만 생성한다.

## 중복 생성 방지 게이트

새 힉스필드 요청은 아래 여섯 항목을 모두 확인한 뒤에만 실행한다.

1. 이 표에 같은 대상이 ✅ 또는 🟡로 있는지 확인한다. 🟡은 재생성이 아니라 기존 원본을 이어서 보완한다.
2. `out_*`와 `unity/Assets/Resources/`에서 대상 이름을 모두 검색한다. 원본과 반입물 중 한쪽만 있어도 새로 생성하지 않는다.
3. `SpriteBank.cs`, `FieldDecor.cs`, `FxPool.cs`, `GameScreen.cs`에서 실제 소비 키를 확인한다. 소비처가 없으면 생성하지 않는다.
4. `higgsfield generate list`에서 같은 작업의 `waiting`·`processing` 여부를 확인한다. 하나라도 있으면 추가 요청하지 않는다.
5. `aigen.py` 또는 `higgsfield` 실행 프로세스가 있는지 확인한다. 작업 종료를 확인하기 전 같은 spec을 다시 실행하지 않는다.
6. 기존 후보와 새 결과의 SHA-1이 같으면 새 산출물로 집계하지 않는다. 같은 시트의 복사본 폴더(`out_char3` 따위)는 만들지 않는다.

애니메이션에서 의도적으로 같은 그림을 여러 프레임 유지하는 것은 제작 중복이 아니라 **홀드 프레임**이다. 예: `buffer_attack_00/01`. 비교·백업 경로(`ref_old_chars`, `_compare`, `_rejected`)도 런타임 산출물 개수에 포함하지 않는다.

## 생성물 보존 규칙

- `out_*` 아래의 시트·프레임·비교본은 삭제하지 않는다. 재생성·회귀 비교에 쓰이는 원본이다.
- `*_wip`, `*_wiped`, `_compare`, `_rejected` 파일은 런타임에 연결하지 않는다.
- Unity에 반입한 PNG에는 반드시 대응 `.meta`를 함께 둔다.
- 새 아트는 먼저 생성 폴더에 저장하고, 네이밍 검사와 화면 QA를 통과한 뒤 `Assets/Resources/`로 복사한다.
- Unity 코드에서 참조하지 않는 산출물은 `Resources/`에 두지 않는다.
- 생성 성공은 완료가 아니다. `원본 존재 → 후처리 → Resources 반입 → 소비 코드 확인 → 실제 창 PNG` 다섯 단계를 모두 통과해야 ✅다.

## 화면 배경 연결 계약

`GameScreen.BackgroundArt`가 반환하는 키와 파일명이 일치해야 한다.

```text
bg_character  → Resources/bg/bg_character.png
bg_estate     → Resources/bg/bg_estate.png
bg_field      → Resources/bg/bg_field.png
bg_party      → Resources/bg/bg_party.png
bg_tower      → Resources/bg/bg_tower.png
bg_worldmap   → Resources/bg/bg_worldmap.png
```

반입 후에는 다음 검사를 실행한다.

```bash
python3 projects/ai-team/skills/마루_게임개발/tools/game_asset_names.py
GAME_START=go:Field ./tools/qa_shot.sh --skip-build go:Field 30
```

최근 확인 증거(2026-08-15): `qa_boss.png`, `qa_hunt.png`, `qa_estate.png`, `qa_go:WorldMap.png`. 2026-08-16 `game_asset_names.py` 결과는 `✅ 네이밍·반영 이상 없음`이었다.
