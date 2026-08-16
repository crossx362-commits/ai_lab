using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>침략 약탈은 RaceDef.약탈량배율을 읽는다. 수인 +20% · 나머지 100%(§3·§18-9).</summary>
    public static class RaceLootSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Race Loot Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(InvasionState.EnvShowLoot);
            string no = Environment.GetEnvironmentVariable(InvasionState.EnvNoLoot);
            RaceId oldRace = RacePrefs.Get();
            float oldForce = InvasionState.ForceRaceLootMul;
            Environment.SetEnvironmentVariable(InvasionState.EnvShowLoot, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvNoLoot, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvShow, null);
            InvasionState.ForceRaceLootMul = 0f;

            GameState.ResetAll();
            LifeSystem.ResetAll();
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            EstateDefense.ResetForTest();
            WorldStar.ResetForTest();
            GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            GameState.Grant(100_000);

            RacePrefs.Set(RaceId.인간);
            Check(InvasionState.RaceLootPercent() == InvasionState.HumanLootPercent,
                $"인간 약탈 100 (실제 {InvasionState.RaceLootPercent()})");
            long human = InvasionState.LootCopper();
            Check(human > 0, $"인간 약탈 > 0 (실제 {human})");
            Check(InvasionState.ApplyRaceLoot(1000) == 1000, "인간 1000 유지");
            Check(InvasionState.RaceLootLine().Contains("없음"),
                $"인간 문구는 배율 없음 (실제 {InvasionState.RaceLootLine()})");

            RacePrefs.Set(RaceId.엘프);
            Check(InvasionState.RaceLootPercent() == InvasionState.HumanLootPercent
                    && InvasionState.LootCopper() == human,
                $"엘프도 기준값 ({InvasionState.LootCopper()})");

            RacePrefs.Set(RaceId.드워프);
            Check(InvasionState.RaceLootPercent() == InvasionState.HumanLootPercent
                    && InvasionState.LootCopper() == human,
                $"드워프도 100 (실제 {InvasionState.LootCopper()})");

            RacePrefs.Set(RaceId.수인);
            long beast = InvasionState.LootCopper();
            Check(InvasionState.RaceLootPercent() == InvasionState.BeastLootPercent,
                $"수인 약탈 120 (실제 {InvasionState.RaceLootPercent()})");
            Check(beast == human * InvasionState.BeastLootPercent / 100,
                $"수인이 같은 티어 인간의 120% (인간 {human} / 수인 {beast})");
            Check(InvasionState.ApplyRaceLoot(1000) == 1200, "수인 1000→1200");
            Check(InvasionState.RaceLootLine().Contains("+20%"),
                $"수인 문구 +20% (실제 {InvasionState.RaceLootLine()})");

            RacePrefs.Set(RaceId.수인);
            Environment.SetEnvironmentVariable(InvasionState.EnvNoLoot, "1");
            Check(InvasionState.RaceLootPercent() == InvasionState.HumanLootPercent,
                "QA_NO_RACE_LOOT이면 수인도 100");
            Check(InvasionState.LootCopper() == human, "차단하면 수인=인간");
            Environment.SetEnvironmentVariable(InvasionState.EnvNoLoot, null);

            RacePrefs.Set(RaceId.수인);
            Check(InvasionState.LootCopper() == beast, "재기동 뒤에도 수인 120");

            long gold0 = GameState.Wallet.Copper;
            Check(InvasionState.TryBegin(), "출정");
            long loot = InvasionState.Settle(true);
            Check(loot == beast, $"정산 약탈이 수인 120% (실제 {loot})");
            Check(GameState.Wallet.Copper == gold0 - InvasionState.SortieCost() + loot
                    || GameState.Wallet.Copper == gold0 + loot,
                "약탈이 지갑에 들어온다");
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            EstateDefense.ResetForTest();
            GameState.ResetAll();
            GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            GameState.Grant(100_000);

            Environment.SetEnvironmentVariable(InvasionState.EnvShowLoot, "1");
            InvasionState.SeedRaceLootQaIfRequested();
            Check(RacePrefs.Get() == RaceId.수인, "시드는 수인을 고른다");
            Check(GameState.TowerFloor >= WorldMapScreen.InvasionUnlockFloor, "시드는 30층");
            Check(!InvasionState.ShieldActive, "시드는 보호막을 안 건다");
            Check(InvasionState.RaceLootLine().Contains("+20%"), "시드 화면 문구 +20%");
            Check(WorldMapScreen.InvasionHubLockReason() == null, "시드 침략 카드가 열린다");
            Environment.SetEnvironmentVariable(InvasionState.EnvShowLoot, null);

            _ = nameof(InvasionState.RaceLootPercent);
            _ = nameof(InvasionState.ApplyRaceLoot);
            _ = nameof(InvasionState.RaceLootLine);
            _ = nameof(InvasionState.SeedRaceLootQaIfRequested);
            _ = nameof(RaceDef.약탈량배율);

            Environment.SetEnvironmentVariable(InvasionState.EnvShowLoot, show);
            Environment.SetEnvironmentVariable(InvasionState.EnvNoLoot, no);
            InvasionState.ForceRaceLootMul = oldForce;
            RacePrefs.Set(oldRace);
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            EstateDefense.ResetForTest();
            GameState.ResetAll();

            if (_fail > 0)
            {
                Debug.LogError("[RaceLootSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("RaceLootSelfCheck FAIL " + _fail);
            }
            Debug.Log("[RaceLootSelfCheck] PASS\n" + _log);
        }
    }
}
