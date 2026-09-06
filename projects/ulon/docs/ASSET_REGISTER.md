# Third-Party Asset Register

출시 전에 다시 확인한다. 각 팩의 실제 `LICENSE` 파일이 최종 기준이다.

| 팩 | 용도 | 라이선스 | 무료 범위 | 다운로드일 | 사용 파일 | URL |
|---|---|---|---|---|---|---|
| Quaternius Universal Base Characters | 플레이어 베이스 | CC0 | Standard만 |  |  |  |
| Quaternius Modular Character Outfits Fantasy | 갑옷/옷 모듈 | CC0 | Standard만 |  |  |  |
| KayKit Adventurers 1.0 | 플레이어/동료/무기/파츠 | CC0 | GitHub Standard | 2026-08-31 | Knight.fbx, Mage.fbx, Rogue.fbx, sword_1handed.fbx, shield_round.fbx (Barbarian.fbx는 상반신 맨살이라 몹·동료 사용 금지 — `Editor/MobArt.cs` 자격 원장) | https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0 |
| KayKit Skeletons 1.0 | 적 | CC0 | GitHub Standard | 2026-08-31 | Skeleton_Warrior.fbx, Skeleton_Mage.fbx, Skeleton_Minion.fbx, Skeleton_Rogue.fbx | https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0 |
| KayKit Dungeon Remastered 1.0 | 던전 실내 소품 | CC0 | GitHub main zip | 2026-09-06 | RAW/Models: barrel_large·barrel_small·barrel_small_stack·box_large·box_small·box_stacked·crates_stacked·chest·pillar·pillar_decorated·rubble_large·rubble_half·table_medium_broken·torch_mounted (.obj+.mtl) + dungeon_texture.png. 나머지 189개 미도입. 자격 원장 `Editor/PropArt.cs` (오너 승인 2026-09-06) | https://github.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0 |
| Kenney Particle Pack 1.1 | VFX 파티클 텍스처 | CC0 | 공식 zip 일부 | 2026-09-06 | circle_01·circle_05·dirt_02·fire_01·flame_01·flame_04·light_01·magic_01·smoke_01·spark_01·star_01·trace_01 (PNG Transparent). 나머지 181개 미도입 (오너 승인 2026-09-06) | https://kenney.nl/assets/particle-pack |
| Kenney RPG Audio 1.0 | 행동 효과음(타격·회복·제작) | CC0 | 공식 zip 일부 | 2026-09-07 | RAW/Audio: knifeSlice.ogg(타격)·handleCoins.ogg(회복)·metalPot1.ogg(제작). 나머지 미도입. 클립이 없을 때만 코드 합성 폴백(`Client/ActionSfx.cs`) (오너 승인 2026-09-07) | https://kenney.nl/assets/rpg-audio |
| Kenney Fantasy Town Kit 2.0 | 마을 소품 | CC0 | 공식 zip 일부 | 2026-08-31 | windmill, stall, fountain-round, tree, cart, rock, planks(던전 널빤지). 던전 실내 소품은 2026-09-06부터 KayKit Dungeon Remastered로 교체 | https://kenney.nl/assets/fantasy-town-kit |
| Kenney Nature Kit | 필드 소품 | CC0 | 공식 zip 일부 | 2026-08-31 | grass, plant_bush, rock_largeA, rock_smallA(던전 잔해) | https://kenney.nl/assets/nature-kit |
| Kenney Retro Fantasy Kit | 소품 | CC0 |  |  |  |  |
| Noto Sans KR | 한글 UI | SIL OFL 1.1 |  |  |  |  |
| 지형 텍스처 4종(KenneyGrass·MountainRock·ShoreSand·SeaWater) | 지형 도포·수면 | 자체 제작 | VisualSliceBuilder.MakeNoiseMat이 128px 노이즈로 생성 | 2026-09-06 | Assets/Game/Art/Env/*.png | 코드 생성(외부 원본 없음) |

모델뿐 아니라 UI, 소리, VFX, 폰트, 음악도 한 줄씩 추가한다.
