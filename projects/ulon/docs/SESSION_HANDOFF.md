# SESSION HANDOFF — Ulon

작업 루프: 한 항목이 끝나면 기획서 17장에서 다음을 고르고 바로 구현한다.

## 현재 상태
- 검술 전투, FishNet, persist, 캐릭터 생성, 채광/벌목, 제작, 거래, 은행, 주문책, 유령/시체/부활, 무게/도구, 광맥 리스폰, 시약 채집까지 슬라이스에 들어 있음.
- 시체 회수 UX: 누운 캐릭터 메시(캡슐 금지), HUD에 방향·거리, 유령은 치유사 안내, 시체 15분 소멸.
- Party 최소: 리더 초대(동료 자동 수락), HP 표시, 파티 말, 파티원 시체 룻 공유. 파티 밖은 loot_right.
- Guild 1: TryGuildCreate(이름 1~12, 골드 25), TryGuildInvite/Accept, 두 아바타 GuildId/GuildName 공유, TryGuildLeave. 파티와 별개. HUD `[길드명]` 태그.
- Guild War 1: TryGuildWarDeclare(길드 A→B), 야외 아바타 A vs B TryAttack은 무고에게도 적용, Notoriety는 Innocent(Criminal 없음). GuardZone은 기존처럼 차단(필드만). TryGuildWarPeace로 종료. 비길드 야외는 Open PvP(범죄). leftover 던전3.
- Duel 1: TryDuelInvite + TryDuelAccept 두 아바타. 야외 TryAttack은 Criminal 없이 적용(필드만, GuardZone 차단). yield/death/TryDuelEnd로 종료. Notoriety Innocent. Guild War·Open PvP와 별개. AssertDuel. leftover 던전3.
- Exceptional 1: TryCraft 성공 시 숙련 롤(Force/seed 결정적). 플래그+내구/피해 소폭. MakerId와 별개. persist는 maker_id `EX:` prefix. AssertExceptional. leftover 던전3.
- Inscription 1: SkillId.Inscription(각인/각인사, INT). TryInscribe는 천 또는 blank + 아는 주문 Ember → scroll_ember, 0.0→0.1. 주문서는 불씨 1회 시전 후 소모. 마법/연금술과 별개. AssertInscription. leftover 던전3.
- Poisoning 1: SkillId.Poisoning(독/독살자, DEX). TryPoisonWeapon은 연금 물약 또는 천 독병(poison_vial/cloth)을 장착 근접무기에 도포. 다음 TryAttack은 짧은 HP 틱(마법 아님). 0.0→0.1. 연금술/수의학과 별개. AssertPoisoning. leftover 던전3.
- Detect Hidden 1: SkillId.DetectHidden(감지/탐지자, DEX). TryDetectHidden은 근처 은신 대상 HiddenUntil 해제. 0.0→0.1. 은신/잠행과 별개. AssertDetectHiddenSlice. leftover 던전3.
- Camping 1: SkillId.Camping(야영/야영꾼, DEX). TryCamp는 기존 Campfire 근처 또는 나무 불씨. 0.0→0.1. CampSafeUntil 짧은 안전로그아웃 플래그. 요리/은신과 별개. AssertCamping. leftover 던전3.
- Stealing 1: SkillId.Stealing(훔치기/도둑, DEX). TrySteal은 마을 LockedCrate 더미 팩에서 최저가 골드/천 1. 0.0→0.1. 가드존/목격 실패→Criminal, 조용 성공은 무고. 자물쇠따기/플레이어가방 아님. AssertStealing. leftover 던전3.
- Healing 붕대 부활 1: TryResurrectBandage(시술자 비유령·붕대1·근접·대상 아바타 Ghost). target.Resurrect()+붕대 소모, Healing 0.0→0.1. HealerStation TryResurrect 유지. Magery/Veterinary 아님. AssertHealingResurrect. leftover 던전3.
- Pet Attack 1: TryPetAttack(주인·활성 팔로워·근처 IsEnemy 몹). PetAttackTarget 추격·TryAttack. F/H/G/A(Follow/Stay/Guard/Attack). 아바타 Open PvP 없음. AssertPetAttack. leftover 던전3.
- Pet Come 1: TryPetCome(주인·활성 팔로워). Attack 해제 후 Follow, TickPets로 주인 오프셋. C/펫호출. 실패: no pet/ghost/stabled/dead. AssertPetCome. leftover 던전3.
- Strength Requirement 1: iron_sword StrReq 25(catalog), TryEquip 저STR 실패/고STR 성공·명확 메시지, AssertStrengthRequirement. leftover 던전3.
- Meditation Armor Penalty 1: iron_plate catalog HeavyArmor, 명상 틱 마나 회복 ½(무갑/경갑 정상), AssertMeditationArmorPenalty. leftover 던전3.
- Magery Cast Interruption 1: Bolt만 CastingUntil 짧은 풍업. TryAttack Applied 피격 시 취소·효과 없음·마나/시약 소모 유지. Ember/Mend 즉시. AssertCastInterrupt. leftover 던전3.
- Magery Cleanse 1: SpellId.Cleanse(정화) 즉시 시전, 자가/근처 아군 아바타 PoisonTicks 해제, 마나/시약 Ember급, Magery 0.0→0.1. AssertCleanse. leftover 던전3.
- Healing 붕대 해독 1: TryCurePoison(시술자 비유령·붕대1·근접·대상 생존·PoisonTicks>0). 독 해제, Healing 0.0→0.1. Magery Cleanse/Veterinary/rez 아님. HUD 「해독」. AssertBandageDetox. leftover 던전3.
- Bonded Pet + Veterinary 부활 1: 조련 시 Bonded. Bonded 펫 HP0→Ghost(슬롯 유지·시체 룻 없음). TryVetResurrect(붕대1·Veterinary 0.0→0.1). 플레이어 붕대 부활/마법/Stable claim 아님. AssertPetBondVetRez. leftover 던전3.
- Weight/과적 1: CarryCap=STR*4(min10), 가방+아이템>한도 시 TryGather/TryBuy/TryCraft 실패·명확 메시지, AssertOverweight. leftover 던전3.
- Nested Container 1: pouch(ItemCatalog)·ItemRecord InstanceId/ParentContainerId, backpack→pouch→item depth1(파우치 속 파우치 금지), TryMoveToPouch/TryTakeFromPouch, 내용물 무게 Carry 합산, AssertNestedBag. leftover 던전3.
- Ground Drop 1: 월드 GroundItem DecayAt(기본 30s)·TickGroundItems 만료 삭제, 집 Lockdown/secure는 GroundItem 아님(예외), AssertGroundDecay. leftover 던전3.
- Reputation Title 1: Fame/Karma/Notoriety 기반 평판 칭호(Murderer→살인자, Criminal→범죄자, Fame≥100→유명인). HUD 이름 옆 SkillTitles와 별개. AssertReputationTitle. leftover 던전3.
- Keyword Speech 1: TrySpeechKeyword(bank/은행·guards/경비·vendor/상점) → 기존 Banker.TryBank/GuardStrike/Vendor.TryVendor, HUD 「은행」「경비」「상점」. AssertKeywordSpeech. leftover 던전3.
- Magery Ward 1: SpellId.Ward(수호) 즉시 시전, 자가 WardUntil~8s, incoming TryAttack 피해×0.5, 마나/시약 Ember급, Magery 0.0→0.1. AssertWard. leftover 던전3.
- Magery Bind 1: SpellId.Bind(속박) 즉시 시전, 근처 적 몹 RootUntil~4s(추격/이동·반격 불가), 마나/시약 Ember급, Magery 0.0→0.1. AssertBind. leftover 던전3.
- Magery Weaken 1: SpellId.Weaken(약화) 즉시 시전, 근처 적 몹 WeakenUntil~6s, outgoing TryAttack/strike 피해×0.5, 마나/시약 Ember급, Magery 0.0→0.1. AssertWeaken. leftover 던전3.
- Magery Spark 1: SpellId.Spark(섬광) 즉시 시전, 근처 적 몹 짧은 사거리(6)·불씨보다 낮은 피해, 마나/시약 Ember급, Magery 0.0→0.1. AssertSpark. leftover 던전3.
- Magery Restore 1: SpellId.Restore(회복) 즉시 시전, 자가/근처 아군 아바타 HP 회복(봉합보다 높음), 마나/시약 봉합보다 약간 높음, Magery 0.0→0.1. AssertRestore. leftover 던전3.
- Magery Blink 1: SpellId.Blink(도약) 즉시 시전, 자가 전방 ~3.5m 단거리 텔레포트, 마나/시약 Ember급, 유령/전투/마나·시약 실패, Mark/Recall/문게이트와 별개, Magery 0.0→0.1. AssertBlink. leftover 던전3.
- Magery Bless 1: SpellId.Bless(축복) 즉시 시전, 자가/근처 아군 BlessUntil~8s, outgoing TryAttack 피해×1.25, Ward와 별개·Weaken 반대, 마나/시약 Ember급, Magery 0.0→0.1. AssertBless. leftover 던전3.
- CraftOrder/제작의뢰 1: Forge/Vendor TryAcceptOrder→ActiveCraftOrder(iron_sword×1), TryTurnInOrder 직접 제작(MakerId) 납품·골드10·대장 소폭, 한 건만. HUD 「의뢰」「납품」. AssertCraftOrder. leftover 던전3.
- Follower Control Slots 1: MaxControlSlots/FollowerCap=2, 야생하트+야생멧돼지 ControlCost=1, 둘 OK·셋째 no_slot, release/stable 슬롯 해제. AssertControlSlots. leftover 던전3.
- 운영툴 최소: F1 GM(워프/지급/회수/스킬/스켈 소환삭제/정지/백업), oplog(거래·제작·시체), 계정 정지 파일, data/backups.
- Closed Alpha 로컬 준비: `tools/closed_alpha_smoke.sh` (postgres+persist+백업). 클라 `-ulon-host <LAN>`. 외부 배포는 안 함.
- 명성/카르마/노토라이어티 persist. 가드존은 광장 반경 16m. 무고 공격→범죄+가드 타격. 몬스터 처치 명성+10. Open PvP 꺼짐.
- 자원 노드: Remaining 0이면 숨고, RespawnSeconds(8초) 후 Capacity로 재생. 광맥 12, 나무 12, 수지 덤불 8.
- 시약: `ResinBush`(Kenney 덤불) 클릭 → resin, 마법 스킬 상승. 도구 불필요. 주문 시전은 resin 소비.
- 씬: IronVein, Forge, OakTree, ResinBush, Banker=풍차, Healer=분수.
- 중앙 마을 1(시스템 루프가 이 안에서 돈다). 메뉴 `Ulon/Dress Village`.
  - 광장 스폰/거래(동료) → 서쪽 대장간·잡화 상점 → 북서 은행 → 남쪽 치유
  - 동쪽 광맥, 북동 벌목, 광장 옆 시약, 북쪽 문 밖 사냥
  - Gold. 상점: 곡괭이/도끼/시약 구매, 광석/나무 판매. 시작금 40.
  - 훈련사(광장 동쪽 Mage): 5G에 스킬 +1, 상한 30, 스탯은 안 오름.
  - 집 7채 + 울타리/대문. 바닥은 Unity Terrain + 노이즈 하이트맵(광장 평평). 광장만 돌길 타일.
  - 필드 3/던전은 아직 안 함.

## 월드 비주얼 금지 (오너 지시 2026-09-01, 앞으로 절대)
단색 초록 Plane, Default-Material 프리미티브, Kenney 시안/주황 무텍스처, 집으로 안 읽히는 벽 타일 더미는 화면에 두지 않는다. `Dress Village`가 `AssertVillageVisuals`로 막는다.
오너에게 보여주는 화면은 플레이 3/4만. Kenney 샘플 밀도(건물·돌길·나무·소품이 붙을 것).

## 방금 고른 다음 일
셀프체크 PASS 복구(BindMob 침묵 실패 수리) 들어 있음. 다음은 던전3.

## 검증은 배치모드로 (2026-09-05)
`tools/slice_selfcheck.sh`가 완전 헤드리스다 — 에디터를 닫고 돌리면 사람 개입이 없다.
에디터를 켜 두면 프로젝트가 잠겨 배치모드가 못 뜨고, MCP 브리지는 에디터가 멎으면
같이 죽는다(실제로 한 번 멎어 반나절 막혔다). 자동 검증은 GUI가 아니라 배치모드에 걸어라.

## 슬라이스 코드 위치 (2026-09-05 partial 분할)
`SliceSelfCheck`(11739줄)와 `OfflineWorld`(4537줄)를 도메인별 partial 파일로 갈랐다.
새 Assert는 주제에 맞는 `SliceSelfCheck.<도메인>.cs`에, 새 서버 로직은
`OfflineWorld.<도메인>.cs`에 넣어라. 한 파일에 다시 쌓지 마라.

## 핵심 경로
- 기획 원장: `projects/ulon/docs/GAME_DESIGN.md`
- 확정안: `projects/ulon/docs/DESIGN.md`
- Unity: `projects/ulon/unity`
- 2클라 검증: `projects/ulon/tools/two_client_check.sh`
- Alpha 스모크: `projects/ulon/tools/closed_alpha_smoke.sh`
- 빌드: 메뉴 `Ulon/Build Dedicated Server + Client` → `projects/ulon/builds/client/UlonClient.app`
