using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>골드 소모는 RaceDef.골드소비배율을 읽는다. 드워프 80% · 나머지 100%(§3·§18-9).</summary>
    public static class RaceCostSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Race Cost Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(Economy.EnvShowCost);
            string no = Environment.GetEnvironmentVariable(Economy.EnvNoCost);
            RaceId oldRace = RacePrefs.Get();
            float oldForce = Economy.ForceRaceCostMul;
            Environment.SetEnvironmentVariable(Economy.EnvShowCost, null);
            Environment.SetEnvironmentVariable(Economy.EnvNoCost, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvShow, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvShowLoot, null);
            Economy.ForceRaceCostMul = 0f;

            GameState.ResetAll();
            LifeSystem.ResetAll();
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            EstateDefense.ResetForTest();
            WorldStar.ResetForTest();
            GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            GameState.Earn(200_000);

            RacePrefs.Set(RaceId.인간);
            Check(Economy.RaceCostPercent() == Economy.HumanCostPercent,
                $"인간 비용 100 (실제 {Economy.RaceCostPercent()})");
            long human = InvasionState.SortieCost();
            Check(human > 0, $"인간 출정 > 0 (실제 {human})");
            Check(human == Economy.GetActionCostBase("InvasionAttack", GameState.Tier),
                "인간은 기준값");
            Check(Economy.ApplyRaceCost(1000) == 1000, "인간 1000 유지");
            Check(Economy.RaceCostLine().Contains("없음"),
                $"인간 문구는 배율 없음 (실제 {Economy.RaceCostLine()})");

            RacePrefs.Set(RaceId.엘프);
            Check(Economy.RaceCostPercent() == Economy.HumanCostPercent
                    && InvasionState.SortieCost() == human,
                $"엘프도 기준값 ({InvasionState.SortieCost()})");

            RacePrefs.Set(RaceId.수인);
            Check(Economy.RaceCostPercent() == Economy.HumanCostPercent
                    && InvasionState.SortieCost() == human,
                $"수인도 100 (실제 {InvasionState.SortieCost()})");

            RacePrefs.Set(RaceId.드워프);
            long dwarf = InvasionState.SortieCost();
            Check(Economy.RaceCostPercent() == Economy.DwarfCostPercent,
                $"드워프 비용 80 (실제 {Economy.RaceCostPercent()})");
            Check(dwarf == human * Economy.DwarfCostPercent / 100,
                $"드워프가 같은 티어 인간의 80% (인간 {human} / 드워프 {dwarf})");
            Check(Economy.ApplyRaceCost(1000) == 800, "드워프 1000→800");
            Check(Economy.RaceCostLine().Contains("−20%"),
                $"드워프 문구 −20% (실제 {Economy.RaceCostLine()})");
            Check(Economy.GetActionCost("DungeonEntry", GameState.Tier)
                    == Economy.GetActionCostBase("DungeonEntry", GameState.Tier)
                        * Economy.DwarfCostPercent / 100,
                "던전 입장도 80%");
            Check(InvasionState.DefeatCost() == dwarf, "패배 추가 소모도 80%");

            long lootHuman;
            RacePrefs.Set(RaceId.인간);
            lootHuman = InvasionState.LootCopper();
            RacePrefs.Set(RaceId.드워프);
            Check(InvasionState.LootCopper() == lootHuman,
                $"약탈 기준은 비용 배율을 안 탄다 (인간 {lootHuman} / 드워프 {InvasionState.LootCopper()})");

            RacePrefs.Set(RaceId.드워프);
            Environment.SetEnvironmentVariable(Economy.EnvNoCost, "1");
            Check(Economy.RaceCostPercent() == Economy.HumanCostPercent,
                "QA_NO_RACE_COST이면 드워프도 100");
            Check(InvasionState.SortieCost() == human, "차단하면 드워프=인간");
            Environment.SetEnvironmentVariable(Economy.EnvNoCost, null);

            RacePrefs.Set(RaceId.드워프);
            Check(InvasionState.SortieCost() == dwarf, "재기동 뒤에도 드워프 80");

            long gold0 = GameState.Wallet.Copper;
            Check(InvasionState.TryBegin(), "출정");
            Check(GameState.Wallet.Copper == gold0 - dwarf, "출정 비용이 드워프 80%");
            long loot = InvasionState.Settle(true);
            Check(loot == lootHuman, $"정산 약탈은 기준값 (실제 {loot})");
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            EstateDefense.ResetForTest();
            GameState.ResetAll();
            GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            GameState.Earn(200_000);

            Environment.SetEnvironmentVariable(Economy.EnvShowCost, "1");
            InvasionState.SeedRaceCostQaIfRequested();
            Check(RacePrefs.Get() == RaceId.드워프, "시드는 드워프를 고른다");
            Check(GameState.TowerFloor >= WorldMapScreen.InvasionUnlockFloor, "시드는 30층");
            Check(!InvasionState.ShieldActive, "시드는 보호막을 안 건다");
            Check(Economy.RaceCostLine().Contains("−20%"), "시드 화면 문구 −20%");
            Check(WorldMapScreen.InvasionHubLockReason() == null, "시드 침략 카드가 열린다");
            Check(InvasionState.SortieCost() == dwarf, "시드 출정이 80%");
            Environment.SetEnvironmentVariable(Economy.EnvShowCost, null);

            _ = nameof(Economy.RaceCostPercent);
            _ = nameof(Economy.ApplyRaceCost);
            _ = nameof(Economy.RaceCostLine);
            _ = nameof(Economy.GetActionCostBase);
            _ = nameof(InvasionState.SeedRaceCostQaIfRequested);
            _ = nameof(RaceDef.골드소비배율);

            Environment.SetEnvironmentVariable(Economy.EnvShowCost, show);
            Environment.SetEnvironmentVariable(Economy.EnvNoCost, no);
            Economy.ForceRaceCostMul = oldForce;
            RacePrefs.Set(oldRace);
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            EstateDefense.ResetForTest();
            GameState.ResetAll();

            if (_fail > 0)
            {
                Debug.LogError("[RaceCostSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("RaceCostSelfCheck FAIL " + _fail);
            }
            Debug.Log("[RaceCostSelfCheck] PASS\n" + _log);
        }
    }
}
