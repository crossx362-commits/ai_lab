# Ulon 확정안

원장: `GAME_DESIGN.md` v1.1. 이 파일은 자주 읽는 확정만 둔다.

## 장르

지속형 온라인 샌드박스 RPG. 콘텐츠 양보다 시스템 연결.

## 그래픽·카메라

- 저폴리 3D. 고정 3/4 쿼터뷰 + 줌. 자유 카메라는 하지 않는다.
- 1 Unity Unit = 1 meter. 인간 키 약 1.7~1.9m.
- Directional Light 1 + Ambient. 실시간 GI 없음.
- 원본 에셋 Material을 직접 고치지 않고 Game용 Variant를 만든다.

### 월드 비주얼 금지 (오너 지시, 예외 없음)

플레이 화면에 아래가 보이면 그 작업은 실패다. 임시·프로토타입도 같다.
- Unity Primitive(Plane/Cube/Capsule/Sphere)를 Default-Material 또는 단색으로 월드 아트에 두지 않는다.
- 마을 바닥은 Unity Terrain + 노이즈 하이트맵. 광장만 평평, `ground_grass` 타일로 채우지 않는다. 광장 길은 Kenney 돌길 타일.
- Kenney Nature 기본 `grass`(시안)·`dirt`(주황) 무텍스처를 화면에 남기지 않는다. 바닥·풀은 KenneyGrass, 바위는 KenneyDirt.
- Fantasy Town은 `colormap`을 유지한다.
- 벽 타일 더미를 집으로 치지 않는다. 문·창·지붕이 붙어 멀리서 집으로 읽혀야 한다.

검증: 메뉴 `Ulon/Dress Village` 끝의 `AssertVillageVisuals`.

### 오너에게 보여줄 때

- 플레이 3/4 카메라만 올린다. 에디터 기본 카메라, 떠 있는 잔디 섬, 금지 비주얼이 남은 화면은 올리지 않는다.
- Kenney 공식 샘플 수준의 밀도: 건물·돌길·나무·소품이 붙어 마을로 읽혀야 한다. 빈 잔디 위에 집 몇 채는 실패다.

## 성장

- 고정 직업 선택 없음. 행동이 스킬을 키운다.
- 스킬 개별 최대 100, 총합 700. ↑/↓/Lock.
- STR/DEX/INT 총합 225, 개별 기본 상한 100. 스킬과 함께 성장.
- 직업명은 현재 플레이를 설명하는 명칭일 뿐 클래스가 아니다.

## 전투

물리 충돌로 베지 않는다. 대상·거리·스킬·장비를 서버가 판정하고 3D는 표현만 한다.

1차 몬스터: 스켈레톤, 도적, 야만인, 자객, 기사. 카탈로그(MobCatalog)와 NetMob은 서버 권한.
보스 MVP(사냥 8종과 별도): 던전 1 본워든(KayKit Skeleton_Warrior, HP 120, warden_crest), 던전 2 섀도우캡틴(KayKit Rogue 1.38배 HP 150, captain_sigil), 동쪽 필드 헥사크(KayKit Mage 1.5배 HP 180, hex_seal). 던전 3은 캡 밖.

## 경제

제작품 중심(설계 목표 약 70%) + 드랍. 내구도/수리/Maker Mark. NPC는 초급만 판다.

## 서버

Unity Dedicated Server + FishNet 후보 + PostgreSQL. 클라이언트가 보내는 것은 의도, 결과는 서버.

초기 동접 목표 20~50. 한 월드 프로세스부터.

## MVP 상한

마을 1, 필드 3, 광산 1, 던전 1~2, 스킬 16, 몬스터 20내외 + 보스 2~3.

완료: 외부 서버 2인 접속 → 생성/재접속 → 사냥 → 스킬 → 아이템 → 채광/벌목 → 제작 → 거래 → 은행.

## 후순위

하우징, 길드전, Open PvP, 조련, 선박, 공성은 MVP 이후. 데이터 구조만 먼저 열어 둔다.

하우징 1차: 지정 Housing Zone/Plot 1(가드존 밖, 계정당 1채, lockdown/secure). Player Vendor 1(Public House Vendor Slot, 가방 1개 등록/골드 구매). 자유 배치·미접속 회수는 아직 없음.

조련 1차: SkillId.AnimalTaming(조련/조련사, DEX), 가드존 밖 야생하트 1(Kenney plant_bushLarge), follow/release, Follower cap 1. Open PvP/던전3 없음.

마구간 1차: 마을 Stable Master(Kenney stall/poles Prefab, 광장 남동 잔디). TryStable은 팔로워 펫 despawn+슬롯 해제, TryClaimStable은 회수. 골드 2. 조련 스킬과 별개. 던전3 없음.

여행 1차: 공개 문게이트 1(Kenney arch/lantern, 광장 워프, 골드 비용).
여행 2차: Mark/Recall 1(한 슬롯 x,z, 골드 5, 유령/전투 중 실패). Runebook·주문 추가·던전3 없음. 문게이트는 광장.

야외 Open PvP 1: 가드존 밖 아바타끼리 TryAttack, 공격자 Criminal. 마을 가드존은 기존.

길드전 1: TryGuildWarDeclare(길드 A→B) 후 야외에서만 합의 PvP. 무고 공격이 적용되고 Notoriety는 Innocent(Criminal 아님). GuardZone은 차단. TryGuildWarPeace로 종료. 비길드 야외는 Open PvP. 던전3 없음.

도둑질 1: SkillId.Stealing(훔치기/도둑, DEX). TrySteal 마을 LockedCrate 팩, 최저가 골드/천 1, 0.0→0.1, 가드존/목격 실패→Criminal. leftover 던전3.
붕대 부활 1: TryResurrectBandage(비유령 시술자·붕대1·근접·아바타 Ghost→Resurrect, Healing 0.0→0.1). HealerStation 유지. leftover 던전3.
펫 Attack 1: TryPetAttack(주인·팔로워 펫·근처 몹), 추격·공격(Stay/Guard 키패턴 A). 아바타 Open PvP 없음. leftover 던전3.
펫 Come 1: TryPetCome(주인·팔로워, Attack 해제→Follow·주인으로 이동, 키 C/펫호출). leftover 던전3.
Strength Requirement 1: iron_sword StrReq 25(catalog), TryEquip 저STR 실패/고STR 성공. leftover 던전3.
중갑 명상 패널티 1: iron_plate HeavyArmor, 명상 틱 마나 회복 ½. leftover 던전3.
시전 중단 1: Bolt CastingUntil 풍업, TryAttack Applied 피격 취소·효과 없음·마나 소모 유지. leftover 던전3.
정화 1: SpellId.Cleanse 즉시, 자가/근처 아군 독 틱 해제, Ember급 마나/시약, Magery 0.0→0.1. leftover 던전3.
붕대 해독 1: TryCurePoison(비유령·붕대1·근접·생존·PoisonTicks→해제, Healing 0.0→0.1). Magery Cleanse 아님. leftover 던전3.
Bonded Pet+Veterinary 부활 1: 조련 Bonded, HP0→pet Ghost·슬롯유지, TryVetResurrect 붕대1·Veterinary 0.0→0.1. leftover 던전3.
Weight/과적 1: CarryCap=STR*4, pickup/buy/craft 가방+아이템>한도 실패. leftover 던전3.
수호 1: SpellId.Ward 즉시·자가 WardUntil~8s·TryAttack 피해×0.5, Ember급 마나/시약, Magery 0.0→0.1. leftover 던전3.
속박 1: SpellId.Bind 즉시·근처 적 몹 RootUntil~4s·추격/이동·반격 불가, Ember급 마나/시약, Magery 0.0→0.1. leftover 던전3.
Nested Container 1: pouch parent_container_id(backpack→pouch→item depth1), TryMoveToPouch/TryTakeFromPouch, 내용물 무게 Carry 합산. leftover 던전3.
Ground Drop 1: 월드 GroundItem DecayAt(기본 30s)·TickGroundItems 만료 삭제, 집 Lockdown/secure 예외, AssertGroundDecay. leftover 던전3.
Reputation Title 1: Murderer→살인자/Criminal→범죄자/Fame≥100→유명인, HUD 이름 옆 SkillTitles와 별개, AssertReputationTitle. leftover 던전3.
Keyword Speech 1: TrySpeechKeyword(bank/은행·guards/경비·vendor/상점) 기존 Banker/GuardStrike/Vendor, AssertKeywordSpeech. leftover 던전3.
약화 1: SpellId.Weaken 즉시·근처 적 몹 WeakenUntil~6s·출격 TryAttack/strike 피해×0.5, Ember급 마나/시약, Magery 0.0→0.1. leftover 던전3.
섬광 1: SpellId.Spark 즉시·근처 적 몹 짧은 사거리·불씨보다 낮은 피해, Ember급 마나/시약, Magery 0.0→0.1. leftover 던전3.
회복 1: SpellId.Restore 즉시·자가/근처 아군 아바타 HP(봉합보다 높음), 마나/시약 봉합보다 약간 높음, Magery 0.0→0.1. leftover 던전3.
도약 1: SpellId.Blink 즉시·자가 전방 ~3.5m 텔레포트, Ember급 마나/시약, 유령/전투 실패, Mark/Recall과 별개, Magery 0.0→0.1. leftover 던전3.
축복 1: SpellId.Bless 즉시·자가/근처 아군 BlessUntil~8s·출격 TryAttack 피해×1.25, Ward와 별개·Weaken 반대, Ember급 마나/시약, Magery 0.0→0.1. leftover 던전3.
Follower Control Slots 1: MaxControlSlots=2, 야생하트/야생멧돼지 ControlCost=1, 둘 조련 OK·셋째 no_slot, release/stable 슬롯 해제. leftover 던전3.
CraftOrder/제작의뢰 1: Forge/Vendor TryAcceptOrder·TryTurnInOrder(직접 제작 iron_sword 1·골드10·한 건), AssertCraftOrder. leftover 던전3.
