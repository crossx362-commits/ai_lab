# 에셋 목록

작성: 2026-09-12 (loop#0). 출처·라이선스는 팩 옆 `LICENSE.txt` / `SOURCE_URL.txt` / `README_IMPORT.txt`와 `docs/ASSET_REGISTER.md`만 사용. 추측하지 않음.  
원본은 `unity/Assets/_ThirdParty/<Creator>/<Pack>/RAW/` — **직접 수정 금지.** 게임용 Variant는 `unity/Assets/Game/`.  
씬은 `unity/Assets/Game/Scenes/Bootstrap.unity` 하나. 월드는 `VisualSliceBuilder`가 조립한다.

스타일 메모(공통): 저폴리 3D, 고정 3/4 쿼터뷰, 1 unit = 1 m. Kenney Fantasy Town은 1 m 모듈 + colormap. KayKit는 페인트 텍스처 Humanoid.

## 게임에 적용된 팩

| 경로 | 종류 | 출처 | 라이선스 | 사용 | 스타일·범위 |
|---|---|---|---|---|---|
| `unity/Assets/_ThirdParty/KayKit/Adventurers/` | 3D | KayKit Adventurers 1.0, Kay Lousberg, https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0 (`SOURCE_URL.txt`, 2026-08-31 / 추가 2026-09-08) | CC0 (`LICENSE.txt`) | **사용.** Knight=플레이어, Mage/Rogue/RogueHooded=NPC·몹·보스, sword_1handed·shield_round. Prefab `Game/Prefabs/Characters|Weapons` | 저폴리 Humanoid. **Barbarian.fbx는 몹·동료 금지**(`Editor/MobArt.cs`). staff/dagger/spellbook_closed/mug_full는 RAW만 |
| `unity/Assets/_ThirdParty/KayKit/Skeletons/` | 3D | KayKit Skeletons 1.0, 동 제작자, https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0 (2026-08-31) | CC0 (`LICENSE.txt`) | **사용.** Warrior/Mage/Minion/Rogue + 본워든 | 공용 `skeleton_texture.png` |
| `unity/Assets/_ThirdParty/KayKit/Dungeon/` | 3D | KayKit Dungeon Remastered 1.0, https://github.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0 (2026-09-06) | CC0 (`LICENSE.txt`) | **사용.** 통·상자·궤짝·기둥·잔해·탁자·횃불 14종. `PropArt.cs` | 공용 `dungeon_texture.png`. 팩 나머지 189 미도입 |
| `unity/Assets/_ThirdParty/Kenney/FantasyTown/` | 3D | Kenney Fantasy Town Kit 2.0, https://kenney.nl/assets/fantasy-town-kit (2026-08-31). zip `art/kenney_fantasy-town-kit_2.0.zip` | CC0 (`LICENSE.txt`) | **사용.** 풍차·좌판·분수·나무·마차·바위·울타리·은행 벽/지붕·등불 등 마을 본체 | colormap 아틀라스. 일부 road/roof 메시는 반입만 |
| `unity/Assets/_ThirdParty/Kenney/Nature/` | 3D | Kenney Nature Kit, https://kenney.nl/assets/nature-kit (2026-08-31). zip `art/kenney_nature-kit.zip` | CC0. `LICENSE.txt`는 스텁(「공식은 kenney.nl」). `SOURCE_URL.txt`가 CC0·URL 명시 | **사용.** 덤불·풀·광맥 바위·`ground_pathTile`. **기본 `grass` 시안은 화면에 남기지 말 것** (`DESIGN.md`) | 저폴리 식생 |
| `unity/Assets/_ThirdParty/Kenney/Particles/` | 스프라이트 | Kenney Particle Pack 1.1, https://kenney.nl/assets/particle-pack (2026-09-06) | CC0 (`LICENSE.txt`) | **일부 사용.** spark_01 타격, circle_05 회복, star_01 제작, flame_01 화덕. `ActionVfx` | 알파 PNG 12장만. 181 미도입 |
| `unity/Assets/_ThirdParty/Kenney/RpgAudio/` | 사운드 | Kenney RPG Audio 1.0, https://kenney.nl/assets/rpg-audio (2026-09-07) | CC0 (`LICENSE.txt`) | **사용.** knifeSlice=타격, handleCoins=회복, metalPot1=제작. `ActionSfx` | ogg 3클립. `Game/Audio/` 비어 있음(원본 경로에서 로드) |
| `unity/Assets/_ThirdParty/OpenGameArt/Deer/` | 3D | OpenGameArt 「Deer low poly, rigged」, https://opengameart.org/content/deer-low-poly-rigged (2026-09-07) | CC0 1.0 (`LICENSE.txt`) | **사용.** 조련 야생하트·마구간. Prefab `Deer.prefab` | OBJ+MTL. 재질 Variant `DeerHide` 등 |
| `unity/Assets/_ThirdParty/OpenGameArt/Boar/` | 3D | OpenGameArt Boar, https://opengameart.org/content/boar (2026-09-07) | CC0 1.0 (`LICENSE.txt`) | **사용.** 멧돼지. Prefab `Boar.prefab` | `boar_0.blend` 원본 + 로컬 Blender 변환 `Boar.fbx` (`README_IMPORT.txt`) |

## 자체 제작 (외부 원본 없음)

| 경로 | 종류 | 출처 | 라이선스 | 사용 | 스타일 |
|---|---|---|---|---|---|
| `unity/Assets/Game/Art/Env/*.png` (+ `.mat` / terrainlayer) | 타일·지형 텍스처 | `VisualSliceBuilder.MakeNoiseMat` 등 코드 생성 (2026-09-06~). 이름에 Kenney가 있어도 **Kenney 텍스처가 아님** | 자체 | **사용.** Terrain 도포·물·던전 벽/바닥, 광장 돌, 밭, 자갈 등 | 노이즈 128~256 px. 쿼터뷰 실루엣 우선 |

`Game/Art/People/*Tint.mat`, `Game/Art/VFX/*.mat`, `Game/Art/Characters/SharedLocomotion.controller`는 게임 Variant·컨트롤러. 서드파티 RAW를 덮어쓰지 않음.

## 미반입 · 출처 미확인

팩 옆 LICENSE가 없으므로 라이선스를 단정하지 않는다. 새 작업에 쓰지 않음.

| 경로/이름 | 종류 | 상태 |
|---|---|---|
| `unity/Assets/_ThirdParty/Quaternius/` | 3D 예정 | **빈 폴더.** 기획 §9·레지스터의 Universal Base / Modular Outfits Fantasy Standard 미반입 |
| Kenney Retro Fantasy Kit | 3D | `ASSET_REGISTER.md` 행만, 파일 없음 |
| Noto Sans KR | UI 폰트 | 기획서·레지스터에 SIL OFL 1.1. **`.ttf/.otf` 없음.** HUD는 IMGUI 기본 |
| Kenney UI Pack / Fantasy UI Borders / Input Prompts | UI | 기획 §18.15만. `Game/UI/` 비어 있음 |
| Kenney Interface Sounds / Impact Sounds | 사운드 | 미반입 |
| Quaternius Fantasy Props MegaKit 등 | 3D | `ASSET_WANTLIST.md` 조사만. **다운로드는 오너 직행** |

## 적용 화면이 역할과 안 맞는 구멍 (에셋 없음)

`ASSET_WANTLIST.md` / `RoleLook.cs`: 모루·화덕, 절구통, 갱도 입구, 마구간 건물, 목공소 건물, 우물·게시판. 가짜 프리미티브로 채우지 않음.

## 쓰지 않는 것

원작 울티마 온라인 그래픽·사운드·맵·클라 파일: **사용·추출 없음.** 원작은 기준이지 소재가 아니다.
