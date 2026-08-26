using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>게임 주요 화면·전투·HUD 자가검사를 한 번에 돌리고 보드에 남긴다.</summary>
    public static class GameSweepSelfCheck
    {
        struct Row
        {
            public string Name;
            public bool Ok;
            public string Note;
        }

        /// <summary>
        /// 스윕 등록부(회의 20260826-095813 채택 1). 컴파일 타임 참조라 검사 클래스가
        /// 삭제·이름변경되면 즉시 컴파일 에러로 드러난다. 신규 SelfCheck는 이 등록부에
        /// 한 줄을 추가해야 하고, 빠뜨리면 SweepCoverageSelfCheck가 스윕 실행 즉시
        /// 「미등록」으로 지목한다. 전수 기준은 GameFullCheck의 반사 발견과 같고,
        /// 단독 실행 전용(UiAtlasUv)과 스윕 본체 자신만 등록 밖이다.
        /// </summary>
        internal struct Entry
        {
            public string Name;
            public Action Run;
        }

        internal static readonly Entry[] Registry =
        {
                new Entry { Name = "사냥 강화 3택", Run = HuntBoonSelfCheck.Run },
                new Entry { Name = "전투 오디오 문법", Run = SfxSelfCheck.Run },
                new Entry { Name = "전투 HUD", Run = CombatHudSelfCheck.Run },
                new Entry { Name = "계열 상성", Run = FamilyAdvSelfCheck.Run },
                new Entry { Name = "UI 아틀라스·카드 여백", Run = UiAtlasSelfCheck.Run },
                new Entry { Name = "필드 허브 HUD", Run = FieldHudSelfCheck.Run },
                new Entry { Name = "탑 허브 HUD", Run = TowerHudSelfCheck.Run },
                new Entry { Name = "월드맵 HUD", Run = WorldMapHudSelfCheck.Run },
                new Entry { Name = "파티 출전 HUD", Run = PartyHudSelfCheck.Run },
                new Entry { Name = "파티 편성 HUD", Run = PartyFormHudSelfCheck.Run },
                new Entry { Name = "사냥 편성 HUD", Run = HuntPickHudSelfCheck.Run },
                new Entry { Name = "필드 경고 HUD", Run = FieldWarnHudSelfCheck.Run },
                new Entry { Name = "탑 경고 HUD", Run = TowerWarnHudSelfCheck.Run },
                new Entry { Name = "월드맵 안개", Run = WorldExploreSelfCheck.Run },
                new Entry { Name = "영지 마을 HUD", Run = EstateHudSelfCheck.Run },
                new Entry { Name = "영지 현황 HUD", Run = EstateStatusHudSelfCheck.Run },
                new Entry { Name = "허브 제목판", Run = HubHeaderSelfCheck.Run },
                new Entry { Name = "사냥 시작 두 단계", Run = HuntStartSelfCheck.Run },
                new Entry { Name = "스킬 자동/수동", Run = SkillUseSelfCheck.Run },
                new Entry { Name = "시작 직업 선택", Run = StarterPickSelfCheck.Run },
                new Entry { Name = "캐릭터 명부", Run = CharacterRosterSelfCheck.Run },
                new Entry { Name = "캐릭터 3열·장비 라벨", Run = CharHudSelfCheck.Run },
                new Entry { Name = "전투 스타일 HUD", Run = StyleHudSelfCheck.Run },
                new Entry { Name = "멤버별 전투 스타일 배선", Run = MemberStyleSelfCheck.Run },
                new Entry { Name = "정예 드랍·필드 훅", Run = EliteDropSelfCheck.Run },
                new Entry { Name = "정예 수호자 오라", Run = EliteGuardianSelfCheck.Run },
                new Entry { Name = "정예 군단장 오라", Run = EliteLegionSelfCheck.Run },
                new Entry { Name = "정예 저주술사 오라", Run = EliteCurseSelfCheck.Run },
                new Entry { Name = "정예 처형자 폭딜", Run = EliteExecutionerSelfCheck.Run },
                new Entry { Name = "정예 분류(IsElite)", Run = EliteClassifySelfCheck.Run },
                new Entry { Name = "본문 내비 절단", Run = BodyNavSelfCheck.Run },
                new Entry { Name = "부활초 소지 상한", Run = ReviveCapSelfCheck.Run },
                new Entry { Name = "사망 상한", Run = DeathCapSelfCheck.Run },
                new Entry { Name = "PvP 회복", Run = PvpRecoverSelfCheck.Run },
                new Entry { Name = "PvE 회복", Run = PveRecoverSelfCheck.Run },
                new Entry { Name = "잡몹 상한", Run = PerfCapSelfCheck.Run },
                new Entry { Name = "소환수 상한", Run = SummonCapSelfCheck.Run },
                new Entry { Name = "소환수 재소환", Run = ResummonSelfCheck.Run },
                new Entry { Name = "동시 건설 슬롯", Run = BuildSlotsSelfCheck.Run },
                new Entry { Name = "부지 확장", Run = EstateExpansionSelfCheck.Run },
                new Entry { Name = "투사체 상한", Run = ProjCapSelfCheck.Run },
                new Entry { Name = "G/h 앵커", Run = GhAnchorSelfCheck.Run },
                new Entry { Name = "티어 배율", Run = TierMulSelfCheck.Run },
                new Entry { Name = "소각 목표", Run = BurnTargetSelfCheck.Run },
                new Entry { Name = "플레이어 이동속도", Run = MoveSpdSelfCheck.Run },
                new Entry { Name = "잡몹 이동속도", Run = MobSpeedSelfCheck.Run },
                new Entry { Name = "잡몹 HP", Run = MobHpSelfCheck.Run },
                new Entry { Name = "잡몹 피해", Run = MobDmgSelfCheck.Run },
                new Entry { Name = "원거리 유지거리", Run = MobRangedDistanceSelfCheck.Run },
                new Entry { Name = "근접 공격 주기", Run = MobMeleeCadenceSelfCheck.Run },
                new Entry { Name = "원거리 발사 주기", Run = MobShotCadenceSelfCheck.Run },
                new Entry { Name = "원거리 투사체 속도", Run = MobProjectileSpeedSelfCheck.Run },
                new Entry { Name = "잡몹 크기", Run = MobSizeSelfCheck.Run },
                new Entry { Name = "던전 포기 문구", Run = DungeonAbandonCopySelfCheck.Run },
                new Entry { Name = "던전 전투 문구", Run = DungeonEncounterCopySelfCheck.Run },
                new Entry { Name = "초상 아틀라스", Run = PortraitAtlasSelfCheck.Run },
                new Entry { Name = "아이템 아틀라스", Run = ItemAtlasSelfCheck.Run },
                new Entry { Name = "영지 격자", Run = EstateGridSelfCheck.Run },
                new Entry { Name = "영지 창고 경로", Run = EstateStoreSelfCheck.Run },
                new Entry { Name = "영지 자리", Run = EstateFootprintSelfCheck.Run },
                new Entry { Name = "영지 마당", Run = EstateYardSelfCheck.Run },
                new Entry { Name = "캐릭터 겹침", Run = UnitSeparationSelfCheck.Run },
                new Entry { Name = "집·나무 겹침", Run = FieldDecorOverlapSelfCheck.Run },
                new Entry { Name = "집 돌아나가기", Run = ArenaLayoutSelfCheck.Run },
                new Entry { Name = "길 한가운데 금지", Run = FieldDecorRoadSelfCheck.Run },
                new Entry { Name = "로컬 테스트 시드", Run = LocalPlayKitSelfCheck.Run },
                new Entry { Name = "사냥 경험치", Run = HuntExpSelfCheck.Run },
                new Entry { Name = "보스 사망 애니", Run = BossDeathAnimSelfCheck.Run },
                new Entry { Name = "직업 애니 13장", Run = JobAnimSelfCheck.Run },
                new Entry { Name = "할로우 배경 6장", Run = HollowBgSelfCheck.Run },
                new Entry { Name = "전직 11종 모습", Run = AdvLookSelfCheck.Run },
                new Entry { Name = "필드·던전 바닥", Run = GroundHollowSelfCheck.Run },
                new Entry { Name = "AuctionBuyLock", Run = AuctionBuyLockSelfCheck.Run },
                new Entry { Name = "AuctionExpire", Run = AuctionExpireSelfCheck.Run },
                new Entry { Name = "AuctionFee", Run = AuctionFeeSelfCheck.Run },
                new Entry { Name = "AuctionHudPlayerCopy", Run = AuctionHudPlayerCopySelfCheck.Run },
                new Entry { Name = "AuctionHud", Run = AuctionHudSelfCheck.Run },
                new Entry { Name = "AuctionInvasion", Run = AuctionInvasionSelfCheck.Run },
                new Entry { Name = "AuctionTrade", Run = AuctionTradeSelfCheck.Run },
                new Entry { Name = "AuraDebuff", Run = AuraDebuffSelfCheck.Run },
                new Entry { Name = "BagSlots", Run = BagSlotsSelfCheck.Run },
                new Entry { Name = "BagTextFmt", Run = BagTextFmtSelfCheck.Run },
                new Entry { Name = "BankruptcySeize", Run = BankruptcySeizeSelfCheck.Run },
                new Entry { Name = "BattlePlayerCopy", Run = BattlePlayerCopySelfCheck.Run },
                new Entry { Name = "BossAutoAttack", Run = BossAutoAttackSelfCheck.Run },
                new Entry { Name = "BossBattleAoe", Run = BossBattleAoeSelfCheck.Run },
                new Entry { Name = "BossBattleDps", Run = BossBattleDpsSelfCheck.Run },
                new Entry { Name = "BossBattleRun", Run = BossBattleRunSelfCheck.Run },
                new Entry { Name = "BossCount", Run = BossCountSelfCheck.Run },
                new Entry { Name = "BossHp", Run = BossHpSelfCheck.Run },
                new Entry { Name = "BossSkills", Run = BossSkillsSelfCheck.Run },
                new Entry { Name = "CardTextFit", Run = CardTextFitSelfCheck.Run },
                new Entry { Name = "ChatWorkBatch", Run = ChatWorkBatchSelfCheck.Run },
                new Entry { Name = "CombatIconAtlas", Run = CombatIconAtlasSelfCheck.Run },
                new Entry { Name = "CombatVfxAtlas", Run = CombatVfxAtlasSelfCheck.Run },
                new Entry { Name = "CompactInfo", Run = global::AshesToStars.Editor.CompactInfoSelfCheck.Run },
                new Entry { Name = "ConceptWrap", Run = ConceptWrapSelfCheck.Run },
                new Entry { Name = "DeathTraining", Run = DeathTrainingSelfCheck.Run },
                new Entry { Name = "DefenseRecover", Run = DefenseRecoverSelfCheck.Run },
                new Entry { Name = "DefenseState", Run = DefenseStateSelfCheck.Run },
                new Entry { Name = "DefenseUnlock", Run = DefenseUnlockSelfCheck.Run },
                new Entry { Name = "DungeonEmptyHud", Run = DungeonEmptyHudSelfCheck.Run },
                new Entry { Name = "DungeonGen", Run = DungeonGenSelfCheck.Run },
                new Entry { Name = "EliteKinds", Run = EliteKindsSelfCheck.Run },
                new Entry { Name = "EmergencyEscape", Run = EmergencyEscapeSelfCheck.Run },
                new Entry { Name = "EquipJob", Run = EquipJobSelfCheck.Run },
                new Entry { Name = "EquipLevel", Run = EquipLevelSelfCheck.Run },
                new Entry { Name = "Equipment", Run = EquipmentSelfCheck.Run },
                new Entry { Name = "EscapeForfeit", Run = EscapeForfeitSelfCheck.Run },
                new Entry { Name = "EscapeHintHud", Run = global::AshesToStars.Editor.EscapeHintHudSelfCheck.Run },
                new Entry { Name = "EscapeManual", Run = EscapeManualSelfCheck.Run },
                new Entry { Name = "EstateArtTier", Run = EstateArtTierSelfCheck.Run },
                new Entry { Name = "EstateBuild", Run = EstateBuildSelfCheck.Run },
                new Entry { Name = "EstateBuildings", Run = EstateBuildingsSelfCheck.Run },
                new Entry { Name = "EstateDefense", Run = EstateDefenseSelfCheck.Run },
                new Entry { Name = "EstateDrag", Run = EstateDragSelfCheck.Run },
                new Entry { Name = "EstateMine", Run = EstateMineSelfCheck.Run },
                new Entry { Name = "EstateRaceMine", Run = EstateRaceMineSelfCheck.Run },
                new Entry { Name = "EstateRush", Run = EstateRushSelfCheck.Run },
                new Entry { Name = "EstateYardCam", Run = EstateYardCamSelfCheck.Run },
                new Entry { Name = "EstateYardPinch", Run = EstateYardPinchSelfCheck.Run },
                new Entry { Name = "EstateYardZoom", Run = EstateYardZoomSelfCheck.Run },
                new Entry { Name = "FieldBoss", Run = FieldBossSelfCheck.Run },
                new Entry { Name = "FieldDockCap", Run = FieldDockCapSelfCheck.Run },
                new Entry { Name = "FieldPlayerCopy", Run = FieldPlayerCopySelfCheck.Run },
                new Entry { Name = "FloorRecruit", Run = FloorRecruitSelfCheck.Run },
                new Entry { Name = "Fusion", Run = FusionSelfCheck.Run },
                new Entry { Name = "FxPool", Run = FxPoolSelfCheck.Run },
                new Entry { Name = "GearDrop", Run = GearDropSelfCheck.Run },
                new Entry { Name = "GearList", Run = GearListSelfCheck.Run },
                new Entry { Name = "GearOpt", Run = GearOptSelfCheck.Run },
                new Entry { Name = "HonorDefense", Run = HonorDefenseSelfCheck.Run },
                new Entry { Name = "HonorGuard", Run = HonorGuardSelfCheck.Run },
                new Entry { Name = "Honor", Run = HonorSelfCheck.Run },
                new Entry { Name = "HuntGold", Run = HuntGoldSelfCheck.Run },
                new Entry { Name = "HuntSchedule", Run = HuntScheduleSelfCheck.Run },
                new Entry { Name = "InvasionApproach", Run = InvasionApproachSelfCheck.Run },
                new Entry { Name = "InvasionShield", Run = InvasionShieldSelfCheck.Run },
                new Entry { Name = "JobVfx", Run = JobVfxSelfCheck.Run },
                new Entry { Name = "LastLifeWarn", Run = LastLifeWarnSelfCheck.Run },
                new Entry { Name = "LevelCombatGrowth", Run = LevelCombatGrowthSelfCheck.Run },
                new Entry { Name = "LifePrice", Run = LifePriceSelfCheck.Run },
                new Entry { Name = "LifeSystem", Run = LifeSystemSelfCheck.Run },
                new Entry { Name = "LoanSanction", Run = LoanSanctionSelfCheck.Run },
                new Entry { Name = "LootCap", Run = LootCapSelfCheck.Run },
                new Entry { Name = "LootFloor", Run = LootFloorSelfCheck.Run },
                new Entry { Name = "LootWarehouse", Run = LootWarehouseSelfCheck.Run },
                new Entry { Name = "LowHpReturn", Run = LowHpReturnSelfCheck.Run },
                new Entry { Name = "MausoleumUnlock", Run = MausoleumUnlockSelfCheck.Run },
                new Entry { Name = "Memorial", Run = MemorialSelfCheck.Run },
                new Entry { Name = "MineSeize", Run = MineSeizeSelfCheck.Run },
                new Entry { Name = "MobilityDistance", Run = MobilityDistanceSelfCheck.Run },
                new Entry { Name = "MotionCycle", Run = MotionCycleSelfCheck.Run },
                new Entry { Name = "NetWorth", Run = NetWorthSelfCheck.Run },
                new Entry { Name = "OfflineSettle", Run = OfflineSettleSelfCheck.Run },
                new Entry { Name = "PartyHudCap", Run = PartyHudCapSelfCheck.Run },
                new Entry { Name = "RaceAdvMat", Run = RaceAdvMatSelfCheck.Run },
                new Entry { Name = "RaceBattleCap", Run = RaceBattleCapSelfCheck.Run },
                new Entry { Name = "RaceCost", Run = RaceCostSelfCheck.Run },
                new Entry { Name = "RaceDefense", Run = RaceDefenseSelfCheck.Run },
                new Entry { Name = "RaceDrop", Run = RaceDropSelfCheck.Run },
                new Entry { Name = "RaceDurability", Run = RaceDurabilitySelfCheck.Run },
                new Entry { Name = "RaceHealth", Run = RaceHealthSelfCheck.Run },
                new Entry { Name = "RaceLoot", Run = RaceLootSelfCheck.Run },
                new Entry { Name = "RaceRecover", Run = RaceRecoverSelfCheck.Run },
                new Entry { Name = "RaceSense", Run = RaceSenseSelfCheck.Run },
                new Entry { Name = "RaceSpeed", Run = RaceSpeedSelfCheck.Run },
                new Entry { Name = "RaidBossPool", Run = RaidBossPoolSelfCheck.Run },
                new Entry { Name = "RaidCost", Run = RaidCostSelfCheck.Run },
                new Entry { Name = "RaidReroll", Run = RaidRerollSelfCheck.Run },
                new Entry { Name = "RaidScale", Run = RaidScaleSelfCheck.Run },
                new Entry { Name = "Rebirth", Run = RebirthSelfCheck.Run },
                new Entry { Name = "RebirthSkill", Run = RebirthSkillSelfCheck.Run },
                new Entry { Name = "RepeatLoot", Run = RepeatLootSelfCheck.Run },
                new Entry { Name = "ResultPlayerCopy", Run = ResultPlayerCopySelfCheck.Run },
                new Entry { Name = "SkillCd", Run = SkillCdSelfCheck.Run },
                new Entry { Name = "SkillCost", Run = SkillCostSelfCheck.Run },
                new Entry { Name = "SkillDesc", Run = SkillDescSelfCheck.Run },
                new Entry { Name = "SkillPow", Run = SkillPowSelfCheck.Run },
                new Entry { Name = "SkillRad", Run = SkillRadSelfCheck.Run },
                new Entry { Name = "SkillUlt", Run = SkillUltSelfCheck.Run },
                new Entry { Name = "SmithUnlock", Run = SmithUnlockSelfCheck.Run },
                new Entry { Name = "SoftCap", Run = SoftCapSelfCheck.Run },
                new Entry { Name = "SoloRaidClear", Run = SoloRaidClearSelfCheck.Run },
                new Entry { Name = "SortieTime", Run = SortieTimeSelfCheck.Run },
                new Entry { Name = "SpecialJob", Run = SpecialJobSelfCheck.Run },
                new Entry { Name = "StarDebuffCap", Run = StarDebuffCapSelfCheck.Run },
                new Entry { Name = "StarterSecond", Run = StarterSecondSelfCheck.Run },
                new Entry { Name = "StatusVfx", Run = StatusVfxSelfCheck.Run },
                new Entry { Name = "StyleToggle", Run = StyleToggleSelfCheck.Run },
                new Entry { Name = "TitlePlayerCopy", Run = TitlePlayerCopySelfCheck.Run },
                new Entry { Name = "TokenPrice", Run = TokenPriceSelfCheck.Run },
                new Entry { Name = "TowerDockCap", Run = TowerDockCapSelfCheck.Run },
                new Entry { Name = "TowerEnding", Run = TowerEndingSelfCheck.Run },
                new Entry { Name = "TowerHubCap", Run = TowerHubCapSelfCheck.Run },
                new Entry { Name = "TowerPlayerCopy", Run = TowerPlayerCopySelfCheck.Run },
                new Entry { Name = "VfxTestScreen", Run = VfxTestScreenSelfCheck.Run },
                new Entry { Name = "WalletText", Run = WalletTextSelfCheck.Run },
                new Entry { Name = "WorldMapDockCap", Run = WorldMapDockCapSelfCheck.Run },
                new Entry { Name = "WorldMapPlayerCopy", Run = WorldMapPlayerCopySelfCheck.Run },
                new Entry { Name = "WorldStar", Run = WorldStarSelfCheck.Run },
                new Entry { Name = "WorldTier", Run = WorldTierSelfCheck.Run },
                new Entry { Name = "스윕 등록 대조", Run = SweepCoverageSelfCheck.Run },
        };

        [MenuItem("Ashes to Stars/QA/Game Sweep Self Check")]
        public static void Run()
        {
            var rows = new List<Row>();
            var errors = new List<string>();
            Application.LogCallback hook = (msg, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
                    errors.Add(msg);
            };
            Application.logMessageReceived += hook;
            try
            {
                foreach (var e in Registry)
                    One(e.Name, e.Run, rows, errors);
            }
            finally
            {
                Application.logMessageReceived -= hook;
                HuntBoon.End();
            }

            int fail = 0;
            for (int i = 0; i < rows.Count; i++)
                if (!rows[i].Ok) fail++;
            WriteReport(rows, fail);
            if (fail == 0) Debug.Log($"[GameSweepSelfCheck] PASS {rows.Count}/{rows.Count}");
            else Debug.LogError($"[GameSweepSelfCheck] FAIL {fail}/{rows.Count}");
        }

        static void One(string name, Action run, List<Row> rows, List<string> errors)
        {
            errors.Clear();
            string thrown = null;
            try { run(); }
            catch (Exception e) { thrown = e.Message; }
            bool ok = true;
            string note = "통과";
            if (thrown != null)
            {
                // 예외는 메시지에 FAIL 문구가 없어도 실패다 — GameFullCheck와 같은 기준으로 맞춘다.
                ok = false;
                note = thrown;
            }
            else
            {
                for (int i = 0; i < errors.Count; i++)
                {
                    if (errors[i].IndexOf("FAIL", StringComparison.OrdinalIgnoreCase) >= 0
                        || errors[i].IndexOf("Exception", StringComparison.OrdinalIgnoreCase) >= 0
                        || errors[i].IndexOf("Assert", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ok = false;
                        note = errors[i];
                        break;
                    }
                }
            }
            if (note.Length > 160) note = note.Substring(0, 160);
            rows.Add(new Row { Name = name, Ok = ok, Note = note });
        }

        static void WriteReport(List<Row> rows, int fail)
        {
            string root = FindRoot();
            if (string.IsNullOrEmpty(root))
            {
                Debug.LogWarning("[GameSweepSelfCheck] 저장소 루트를 못 찾아 JSON을 못 씀");
                return;
            }
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"at\": \"").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append("\",\n");
            sb.Append("  \"ok\": ").Append(fail == 0 ? "true" : "false").Append(",\n");
            sb.Append("  \"summary\": \"").Append(fail == 0
                ? rows.Count + "개 전부 통과"
                : fail + "개 실패 / " + rows.Count).Append("\",\n");
            sb.Append("  \"items\": [\n");
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                sb.Append("    {\"name\":\"").Append(Esc(r.Name))
                  .Append("\",\"ok\":").Append(r.Ok ? "true" : "false")
                  .Append(",\"note\":\"").Append(Esc(r.Note)).Append("\"}");
                sb.Append(i + 1 < rows.Count ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            File.WriteAllText(Path.Combine(root, "loop", "last_test_report.json"),
                sb.ToString(), Encoding.UTF8);
        }

        static string FindRoot()
        {
            var d = new DirectoryInfo(Application.dataPath);
            while (d != null)
            {
                if (File.Exists(Path.Combine(d.FullName, "loop", "board.py")))
                    return d.FullName;
                d = d.Parent;
            }
            return null;
        }

        static string Esc(string s) =>
            (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
