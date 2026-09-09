using System;
using System.Collections.Generic;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **자 배선** — 어떤 자를 어떤 순서로 부르는가만 담는다(랩 ㉪, `Run`에서 갈라 나왔다).
        /// 자의 **내용**은 여기 없다(주제별 partial에 있다). 자를 하나 더 세우면 여기에 한 줄을 더한다.
        ///
        /// 순서에 뜻이 있는 자리는 주석으로 표시돼 있다 — 옮길 때 그 뜻이 따라오지 않으면 깨진다
        /// (예: 액터 명단 자는 빌더를 다시 부르고 빌더는 씬을 다시 열기 때문에 **맨 끝**이다).
        /// </summary>
        static void RunGates()
        {
            wiredGates.Clear();

            Gate(AssertMeditationSlice);
            Gate(AssertMagicResistSlice);
            Gate(AssertEvalIntSlice);
            Gate(AssertBolt);
            Gate(AssertFishingSlice);
            Gate(AssertCookingSlice);
            Gate(AssertFencingSlice);
            Gate(AssertMaceSlice);
            Gate(AssertAlchemySlice);
            Gate(AssertInscription);
            Gate(AssertPoisoning);
            Gate(AssertTrackingSlice);
            Gate(AssertMusicianshipSlice);
            Gate(AssertPeacemakingSlice);
            Gate(AssertProvocationSlice);
            Gate(AssertHidingSlice);
            Gate(AssertStealthSlice);
            Gate(AssertDetectHiddenSlice);
            Gate(AssertCamping);
            Gate(AssertStealing);
            Gate(AssertHealingResurrect);
            Gate(AssertBandageDetox);
            Gate(AssertLockpickingSlice);
            Gate(AssertAnimalLoreSlice);
            Gate(AssertVeterinarySlice);
            Gate(AssertHousingSlice);
            Gate(AssertTamingSlice);
            Gate(AssertPetCommands);
            Gate(AssertPetAttack);
            Gate(AssertPetCome);
            Gate(AssertPetBondVetRez);
            Gate(AssertDungeonInterior);
            Gate(AssertPlayCameraSight);
            Gate(AssertDungeonLighting);
            Gate(AssertWorldTerrain);
            Gate(AssertHuntGround);
            Gate(AssertFishingSpotMissingNegativeControl);
            Gate(AssertNoRolelessWatermillNegativeControl);
            Gate(AssertNoRolelessWatermill);
            Gate(AssertHuntMobsAwayFromVillageNegativeControl);
            Gate(AssertHuntMobsAwayFromVillage);
            Gate(AssertRoomFurnished);
            Gate(AssertRoomFurnishedNegativeControl);
            Gate(AssertPropsQualified);
            Gate(AssertRoomPropsNegativeControl);
            Gate(AssertEntranceArtQualified);
            Gate(AssertEntranceArtNegativeControl);
            Gate(AssertPropDistribution);
            Gate(AssertPropDistributionNegativeControl);
            Gate(AssertPropScaleRatio);
            Gate(AssertPropScaleNegativeControl);
            Gate(AssertActionSfxDistinct);
            Gate(AssertActionSfxNegativeControl);
            Gate(AssertTerrainClassNegativeControl);   // 차폐 게이트들이 「지표」를 무엇으로 아는지 먼저 증명한다
            Gate(AssertPlayerNotOccluded);
            Gate(AssertPlayerNotOccludedNegativeControl);
            Gate(AssertIndoorRule);
            Gate(AssertSightFadeTranslucent);
            Gate(AssertSightFadeTranslucentNegativeControl);
            Gate(AssertCloseUpSubjectNotGhostedNegativeControl);   // 피사체가 자기 페이드에 물리는지(33 반려)
            Gate(AssertCloseUpSubjectNotGhosted);
            Gate(AssertOutdoorSightLine);
            Gate(AssertOutdoorSightLineNegativeControl);
            Gate(AssertSpawnOnGround);
            Gate(AssertSpawnOnGroundNegativeControl);
            Gate(AssertPerfMaterialRulerNegativeControl);
            Gate(AssertPerfRegressionAlarm);
            Gate(AssertBossMobContrast);
            Gate(AssertBossMobContrastNegativeControl);
            Gate(AssertCharacterArtQualified);
            Gate(AssertCreatureArtQualified);
            Gate(AssertCreatureArtNegativeControl);
            Gate(AssertVillagerLooksNegativeControl);           // 두 사람을 같게 만들면 빨간불인가(랩 ③사람)
            Gate(AssertVillagerLooksDistinct);                  // 5역할이 화면에서 갈리는가 — 존재가 아니라 차이
            Gate(AssertBarehandVillagersNegativeControl);       // 손에 물건을 쥐여 주면 빨간불인가
            Gate(AssertBarehandVillagers);                      // 맨손이어야 할 사람 손이 실제로 비었는가(이름 아닌 위치로)
            Gate(AssertVillagerHatFitsNegativeControl);         // 모자를 키우면 빨간불인가
            Gate(AssertVillagerHatFits);                        // 챙이 사람을 덮지 않는가(§8.1)
            Gate(AssertGearDressedNegativeControl);             // 꺼 둔 장비를 켜면 빨간불인가
            Gate(AssertGearDressed);                            // 무기 1·방패 1(플레이어가 검 3·방패 4였다)
            Gate(AssertNobodyInsideStructureNegativeControl);
            Gate(AssertNobodyInsideStructure);
            Gate(AssertOutdoorPropsNotBlackNegativeControl);    // 소품을 새까맣게 칠하면 빨간불인가
            Gate(AssertOutdoorPropsNotBlack);                   // 대낮 야외에 검은 덩어리가 없는가
            Gate(AssertDungeonPlaceByPositionNegativeControl);  // 이름을 갈면 그대로, 들판으로 옮기면 빨간불인가
            Gate(AssertDungeonPlaceByPosition);                 // 실내/실외를 이름 아닌 자리로 가르는가(④)
            Gate(AssertPropsNotOverlappingNegativeControl);     // 소품 둘을 같은 자리에 놓으면 빨간불인가
            Gate(AssertPropsNotOverlapping);                    // 실내 소품이 서로 파고들지 않았는가(§8.2)
            Gate(AssertExclusionsAliveNegativeControl);         // 유령 조항을 넣으면 빨간불인가(양방향 NC)
            Gate(AssertExclusionsAlive);                        // 선언한 예외가 실제로 무엇을 빼는가 — 매 판 이름·수
            Gate(AssertNoDoubleWiredGatesNegativeControl);      // 호출을 한 줄 복사하면 빨간불인가
            Gate(AssertNoDoubleWiredGates);                     // 같은 게이트가 두 번 배선돼 있지 않은가(동시 개발)
            Gate(AssertWarpSpotsClearNegativeControl);          // 몹을 출구 위에 세우면 빨간불인가
            Gate(AssertWarpSpotsClear);                         // 나가는 자리에 몹이 붙어 있지 않은가
            Gate(AssertCompanionOffSightAxisNegativeControl);
            Gate(AssertCompanionOffSightAxis);
            Gate(AssertCompanionDistinctNegativeControl);       // 동료를 플레이어와 같게 만들면 빨간불인가
            Gate(AssertCompanionDistinct);                      // 동료가 내 편으로 갈리는가
            Gate(AssertSceneRosterNegativeControl);             // 명단 하나를 지우면 빨간불인가
            Gate(AssertSceneRosterPresent);                     // 있어야 할 것이 조용히 사라지지 않았는가(3(b))
            Gate(AssertNoUndeclaredLookTwinsNegativeControl);   // 선언 없는 쌍을 만들면 빨간불인가
            Gate(AssertNoUndeclaredLookTwins);                  // 화면에서 같은 쌍은 선언된 것만(검수 지시 2)
            Gate(AssertActorsAnimatedNegativeControl);          // 컨트롤러를 떼면 빨간불인가
            Gate(AssertActorsAnimated);                         // T포즈로 서 있는 사람형이 없는가(랩 ④ (a) 실측)
            Gate(AssertFlaxFieldNegativeControl);               // 한 포기만 남기면 빨간불인가
            Gate(AssertFlaxFieldReadsAsField);                  // 아마밭이 「밭」으로 읽히는가(랩 ⑤)
            Gate(AssertMobHeightsNegativeControl);               // 한 마리를 1.4배로 키우면 빨간불인가
            Gate(AssertMobHeightsMatchCatalog);                  // 몹이 원장 키로 서 있는가(랩 ⑥)
            Gate(AssertEffectWiring);
            Gate(AssertEffectWiringNegativeControl);
            Gate(AssertCharacterArtNegativeControl);
            Gate(AssertFootOnGround);
            Gate(AssertFootNegativeControl);
            Gate(AssertWorldRegions);
            Gate(AssertRegionSplat);
            Gate(AssertRidgeAndShore);       // 절벽이 한 겹인가 · 해안 폭이 자리마다 다른가(검수 랩 ⑥)
            Gate(AssertTerrainProjection);   // 경사면에서 무늬가 세로로 늘어나는가(검수 랩 ⑧)
            Gate(AssertWallGrass);           // 수직 암벽에 풀이 흘러내리는가(검수 랩 ⑨)
            Gate(AssertCliffMaskFall);       // 얼룩이 낙하선으로 늘어나는가(검수 랩 ㉡ — 무늬는 삼면, 마스크는 평면이었다)
            Gate(AssertCliffGrain);          // 암벽에 결이 남았는가(검수 랩 ㉢ — 톤은 갈렸는데 표면이 뭉개졌다)
            Gate(AssertShoreBand);           // 물가에 걸어 다닐 만한 띠가 있는가(폭은 높이가 아니라 수평 거리)
            Gate(AssertPlaneMix);            // 두 벽 평면이 겹쳐 격자를 그리는가(검수 랩 ㉧)
            Gate(AssertWorldMaterials);
            Gate(AssertServerAuthorityWiring);
            Gate(AssertReachableFeatures);
            Gate(AssertWarpLandings);
            Gate(AssertBlinkLanding);
            Gate(AssertBlinkLandingNegativeControl);
            Gate(AssertWarpLandingsNegativeControl);
            Gate(AssertHudControlsOnScreen);
            Gate(AssertHudControlsNegativeControl);
            Gate(AssertBossSilhouetteHeadgearNegativeControl);   // 모자를 되돌리면 빨간불인가(분모 오염 감시)
            Gate(AssertBossSilhouette);
            Gate(AssertBossHeadgearFramed);
            Gate(AssertEntranceMouthClear);
            Gate(AssertEntrancePortalCloses);
            Gate(AssertEntranceBannerReads);
            Gate(AssertBossShotsDiffer);
            Gate(AssertBossTraits);
            Gate(AssertDungeonEntrance);
            Gate(AssertItemDataFile);
            Gate(AssertRecipeDataFile);
            Gate(AssertMobDataFile);
            Gate(AssertDataRecordSanity);
            Gate(AssertStrengthRequirement);
            Gate(AssertRepairAndToolReadouts);
            // VFX는 **화면으로** 재야 한다 — 셀프체크는 -nographics라 카메라 렌더가 불가능하다.
            // 그래서 VFX 게이트는 그래픽이 있는 QA 샷 실행(tools/qa_shots.sh)에서 돈다.
            Gate(AssertOverweight);
            Gate(AssertMeditationArmorPenalty);
            Gate(AssertCastInterrupt);
            Gate(AssertCleanse);
            Gate(AssertWard);
            Gate(AssertBind);
            Gate(AssertWeaken);
            Gate(AssertSpark);
            Gate(AssertRestore);
            Gate(AssertBlink);
            Gate(AssertBless);
            Gate(AssertControlSlots);
            Gate(AssertNestedBag);
            Gate(AssertGroundDecay);
            Gate(AssertStableSlice);
            Gate(AssertTravelSlice);
            Gate(AssertMarkRecall);
            Gate(AssertOpenPvpSlice);
            Gate(AssertSkillTitleSlice);
            Gate(AssertReputationTitle);
            Gate(AssertKeywordSpeech);
            Gate(AssertEastFieldSlice);
            Gate(AssertSouthFieldSlice);
            Gate(AssertNorthFieldSlice);
            Gate(AssertDungeon1Slice);
            Gate(AssertDungeon2Slice);
            Gate(AssertDungeon3Slice);
            Gate(AssertFieldBossSlice);
            Gate(AssertGuildSlice);
            Gate(AssertGuildWar);
            Gate(AssertDuel);
            Gate(AssertExceptional);
            Gate(AssertCraftOrder);
            // 역할↔외형은 **맨 끝**에 둔다 — 지금 마을 시설이 전부 어긋나 빨간불이라, 앞에 두면
            // 나머지 게이트가 한 줄도 못 돌아 회귀를 못 본다(검수 지시로 세운 게이트, 랩 ① 작업 목록).
            Gate(AssertNoWholeFacilityAnchorNegativeControl);   // 자리 기준이 시설 전체 바운드로 새는지(소스)
            Gate(AssertNoWholeFacilityAnchor);
            Gate(AssertBuilderIdempotentNegativeControl);       // 다시 돌려도 좌표가 안 밀리는지(실측)
            Gate(AssertBuilderIdempotent);
            Gate(AssertCapeIsBossOnlyNegativeControl);
            Gate(AssertCapeIsBossOnly);
            Gate(AssertGearAboveFloorNegativeControl);          // 장비가 바닥을 뚫는지(41 반려)
            Gate(AssertGearAboveFloor);
            Gate(AssertActorFeetNegativeControl);
            Gate(AssertActorFeetOnSurface);
            Gate(AssertFishingAtWaterNegativeControl);
            Gate(AssertFishingAtWater);
            Gate(AssertCampfireFireNegativeControl);
            Gate(AssertCampfireHasFire);
            Gate(AssertRoleLookNegativeControl);
            Gate(AssertRoleLook);
            Gate(AssertHeadgearFoundByPlace);
            LogRigExempt();                                  // 뼈대 규칙의 예외를 매 판 세어 남긴다(제외는 조용히 넓어진다)
            Gate(AssertShieldPickedByPlace);
            Gate(AssertNoNestedActorsNegativeControl);
            Gate(AssertNoNestedActors);
            Gate(AssertHouseRoofPiecesDontOverlapNegativeControl);
            Gate(AssertHouseRoofPiecesDontOverlap);
            Gate(AssertHuntSpotsApartNegativeControl);           // 간격 자는 `AssertHuntGround` 안에서 잰다
            Gate(AssertDoorFitsPersonNegativeControl);           // 배율을 뺀 집은 빨간불이어야 한다(랩 B)
            Gate(AssertDoorFitsPerson);                          // 문이 사람보다 큰가 — 킷 배율의 근거
            VisualSliceBuilder.ClearDecorFromRegions();           // 커밋된 씬에 남은 마을 장식은 지역에서 치운다
            Gate(AssertVillagePropsNotClashingNegativeControl);       // 종류가 다른 둘을 겹치면 빨간불인가(양방향 NC)
            VisualSliceBuilder.KeepFireOffWalls();        // 불은 담에서 한 걸음(검수 2026-09-09)
            Gate(AssertFireOffWallsNegativeControl);
            Gate(AssertFireOffWalls);
            Gate(AssertVillagePropsNotClashing);                      // 마을 소품끼리 파고들지 않았는가(검수 랩)
            Gate(AssertNoRegionIntrusionNegativeControl);      // 지역 안에 남의 배치물을 넣으면 빨간불인가(양방향 NC)
            Gate(AssertPlazaClearNegativeControl);             // 광장 마당에 담을 넣으면 빨간불인가(같은 자, 다른 절)
            Gate(AssertNoRegionIntrusion);                           // 지역 안에는 그 지역이 놓은 것만(검수 2026-09-09)
            Gate(AssertDoorFrontClearNegativeControl);                // 문 개구부 정면에는 소품이 서지 않는다(양방향 NC)
            Gate(AssertDoorFrontClear);
            Gate(AssertActorWeaponsHeldNegativeControl);              // 물림 자 셋을 보스 밖 일반 배우까지(양방향 NC)
            Gate(AssertActorWeaponsHeld);
            Gate(AssertAtmosphereFromLedgerNegativeControl);          // 하늘·앰비언트·안개가 원장 하나에서 오는가(양방향 NC)
            Gate(AssertAtmosphereFromLedger);
            Gate(AssertVillagePropsClearOfBuildingsNegativeControl);  // 던전 소품 자를 마을로 넓힌 것(양방향 NC)
            Gate(AssertVillagePropsClearOfBuildings);             // 울타리가 벽을 뚫고 등이 처마에 박히지 않는가
            Gate(AssertActorBodyMatchesCapsuleNegativeControl);  // 먼저 자가 우는지 본다
            Gate(AssertActorBodyMatchesCapsule);                // 그림이 충돌체와 같은 크기인가(랩 A)
            Gate(AssertGearFoundByPlace);                      // 장비를 이름표로만 찾지 않는가(랩 ①, 양방향 NC)
            // **맨 끝**에 둔다 — 이 게이트는 빌더를 다시 부르고, 빌더는 첫 줄에서 씬을 다시 연다.
            Gate(AssertActorRosterFree);                        // 액터 선정이 이름 명단인가(랩 ③, 양방향 NC)

        }

        /// <summary>
        /// **같은 자가 두 번 배선되면 그 자리에서 빨간불**(검수 지시 2026-09-09).
        ///
        /// 실제로 난 사고다: 같은 게이트가 `SliceSelfCheck.cs`에 두 번 배선돼 있었고, 아무도 못 봤다 —
        /// 두 번 도는 자는 시간만 먹고 **한 번 도는 자와 결과가 같아서** 화면에도 로그에도 표가 안 난다.
        /// 사고가 난 자리를 고치면서 그 사고를 못 잡는 채로 두지 않는다.
        ///
        /// **이 자가 못 보는 것**: 여기 배선된 자만 센다. 다른 partial이 자기 안에서 자를 또 부르면
        /// 그건 이 셈에 안 잡힌다(그때는 「배선」이 아니라 「자 안의 호출」이라 성격이 다르다).
        /// </summary>
        static readonly HashSet<string> wiredGates = new HashSet<string>();

        static void Gate(Action gate)
        {
            string name = gate.Method.Name;
            if (!wiredGates.Add(name))
                throw new InvalidOperationException("자 " + name + "가 두 번 배선됐습니다 — 배선은 한 번입니다.");
            gate();
        }
    }
}
