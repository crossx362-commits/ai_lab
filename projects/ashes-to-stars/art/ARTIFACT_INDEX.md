# 재와 별 아트 산출물 인덱스

아트 생성 작업물은 생성 원본과 Unity 런타임 반입물을 분리한다. `out_*` 폴더는 재생성·후처리용 원본이며, 게임은 `unity/Assets/Resources/` 아래 파일만 읽는다.

## 런타임 연결 현황

| 종류 | 생성·후처리 원본 | Unity 반입 경로 | 소비 코드 | 상태 |
|---|---|---|---|---|
| 몬스터 4종 | `out_p2/frames_*` | `unity/Assets/Resources/sprites/mob_{chaser,charger,ranged,swarmer}/` | `Assets/Scripts/SpriteBank.cs` | 4종 × 22프레임 |
| 화면 배경 6종 | `out_p8_bg/bg_*.png` | `unity/Assets/Resources/bg/bg_*.png` | `Assets/_Game/Scripts/Runtime/GameScreen.cs` | `Resources.Load("bg/" + key)` |
| 캐릭터 직업 아트 | `out_char*/` | `unity/Assets/Resources/sprites/` | `SpriteBank.cs` | 기존 연결 유지 |
| 마을·이펙트·나무 | `out_p3_village/`, `out_p4_fx/`, `out_p5_trees/` | `unity/Assets/Resources/props/`, `fx/` | `FieldDecor.cs` 등 | 기존 반입 경로 유지 |

## 생성물 보존 규칙

- `out_*` 아래의 시트·프레임·비교본은 삭제하지 않는다. 재생성·회귀 비교에 쓰이는 원본이다.
- `*_wip`, `*_wiped`, `_compare`, `_rejected` 파일은 런타임에 연결하지 않는다.
- Unity에 반입한 PNG에는 반드시 대응 `.meta`를 함께 둔다.
- 새 아트는 먼저 생성 폴더에 저장하고, 네이밍 검사와 화면 QA를 통과한 뒤 `Assets/Resources/`로 복사한다.
- Unity 코드에서 참조하지 않는 산출물은 `Resources/`에 두지 않는다.

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
