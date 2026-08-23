using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>침략 약탈은 같은 티어 6 G/h로 자른다. QA_NO면 상한 없음(§18-13).</summary>
    public static class LootCapSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Loot Cap Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(InvasionState.EnvShowCap);
            string no = Environment.GetEnvironmentVariable(InvasionState.EnvNoCap);
            long oldForce = InvasionState.ForceLootBeforeCap;
            Environment.SetEnvironmentVariable(InvasionState.EnvShowCap, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvNoCap, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvShow, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvShowLoot, null);
            InvasionState.ForceLootBeforeCap = 0;

            GameState.ResetAll();
            LifeSystem.ResetAll();
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            EstateDefense.ResetForTest();
            WorldStar.ResetForTest();
            RacePrefs.Set(RaceId.인간);

            GameState.SetTowerFloorForTest(1);
            Check(GameState.Tier == 0, $"T1 층이면 티어 0 (실제 {GameState.Tier})");
            Check(InvasionState.CapCopper(0) == InvasionState.LootCapHours * Economy.COPPER_PER_GOLD,
                $"T1 상한 60000 (실제 {InvasionState.CapCopper(0)})");
            Check(InvasionState.CapCopper() == 60_000, $"CapCopper() T1=60000 (실제 {InvasionState.CapCopper()})");
            long t1 = InvasionState.LootCopper();
            Check(t1 > 0 && t1 <= 60_000, $"T1 약탈 ≤ 60000 (실제 {t1})");
            Check(InvasionState.ApplyLootCap(60_000) == 60_000, "T1 상한값은 그대로");
            Check(InvasionState.ApplyLootCap(60_001) == 60_000, "T1에서 60001→60000");
            Check(InvasionState.LootCapLine().Contains("6 G/h"),
                $"T1 문구 6 G/h (실제 {InvasionState.LootCapLine()})");

            GameState.SetTowerFloorForTest(100);
            Check(GameState.Tier == 9, $"100층이면 T10 (실제 {GameState.Tier})");
            long t10Cap = InvasionState.CapCopper(9);
            long expectCap = (long)(InvasionState.LootCapHours
                * Economy.TierRevenueMultiplier[9] * Economy.COPPER_PER_GOLD);
            Check(t10Cap == expectCap && t10Cap > 60_000,
                $"T10 상한은 6×수익 (실제 {t10Cap})");
            Check(InvasionState.CapCopper() == t10Cap, "CapCopper()가 선택 티어를 본다");
            InvasionState.ForceLootBeforeCap = 0;
            long t10 = InvasionState.LootCopper();
            Check(t10 > 0 && t10 <= t10Cap, $"T10 공식 약탈 ≤ 상한 ({t10} / {t10Cap})");

            InvasionState.ForceLootBeforeCap = t10Cap + 1;
            Check(InvasionState.LootCopper() == t10Cap,
                $"T10에서 상한이 자른다 (실제 {InvasionState.LootCopper()})");
            Check(InvasionState.ApplyLootCap(t10Cap + 1) == t10Cap, "ApplyLootCap이 T10을 자른다");

            Environment.SetEnvironmentVariable(InvasionState.EnvNoCap, "1");
            Check(InvasionState.LootCapBlocked, "QA_NO_LOOT_CAP이면 차단");
            Check(InvasionState.LootCopper() == t10Cap + 1,
                $"차단하면 T10이 6 G/h를 넘긴다 (실제 {InvasionState.LootCopper()})");
            Check(InvasionState.ApplyLootCap(t10Cap + 1) == t10Cap + 1, "차단하면 Apply가 그대로");
            Check(InvasionState.LootCapLine().Contains("없음"),
                $"차단 문구 없음 (실제 {InvasionState.LootCapLine()})");
            Environment.SetEnvironmentVariable(InvasionState.EnvNoCap, null);

            Check(InvasionState.LootCopper() == t10Cap, "차단을 풀면 다시 상한");
            InvasionState.ForceLootBeforeCap = 0;

            GameState.Grant(500_000);
            Check(!InvasionState.ShieldActive, "정산 전 보호막 없음");
            InvasionState.ForceLootBeforeCap = t10Cap + 50_000;
            Check(InvasionState.TryBegin(), "출정");
            long loot = InvasionState.Settle(true);
            Check(loot == t10Cap, $"정산 약탈이 상한 (실제 {loot})");
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            EstateDefense.ResetForTest();
            GameState.ResetAll();
            WorldStar.ResetForTest();
            RacePrefs.Set(RaceId.인간);

            Environment.SetEnvironmentVariable(InvasionState.EnvShowCap, "1");
            GameState.SetTowerFloorForTest(100);
            GameState.TrySelectTier(0);
            WorldStar.EnemyDebuff = true;
            InvasionState.SeedLootCapQaIfRequested();
            Check(RacePrefs.Get() == RaceId.인간, "시드는 인간을 고른다");
            Check(GameState.TowerFloor >= 100, $"시드는 T10 (실제 {GameState.TowerFloor}층)");
            Check(GameState.Tier == 9, "시드가 T1 잔재를 T10으로 고친다");
            Check(!WorldStar.EnemyDebuff, "시드가 적 디버프 잔재를 끈다");
            Check(InvasionState.CapCopper() == InvasionState.CapCopper(9),
                $"시드 상한이 T10 (실제 {InvasionState.CapCopper()})");
            Check(!InvasionState.ShieldActive, "시드는 보호막을 안 건다");
            Check(InvasionState.LootCapLine().Contains("6 G/h"), "시드 화면 문구 6 G/h");
            Check(InvasionState.LootCapLine().Contains("412골드"),
                $"시드 문구가 T10 상한 (실제 {InvasionState.LootCapLine()})");
            Check(WorldMapScreen.InvasionHubLockReason() == null, "시드 침략 카드가 열린다");
            Environment.SetEnvironmentVariable(InvasionState.EnvShowCap, null);

            _ = nameof(InvasionState.CapCopper);
            _ = nameof(InvasionState.ApplyLootCap);
            _ = nameof(InvasionState.LootCapLine);

            string invSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/InvasionState.cs"));
            Check(invSrc.Contains("ShortCopper(CapCopper())")
                  && invSrc.IndexOf("FormatCurrency(CapCopper())") < 0,
                "CapCopper은 ShortCopper만");
            Check(invSrc.Contains("ShortCopper(FloorCopper())"),
                "하한도 ShortCopper");
            _ = nameof(InvasionState.SeedLootCapQaIfRequested);
            _ = nameof(InvasionState.ForceLootBeforeCap);

            Environment.SetEnvironmentVariable(InvasionState.EnvShowCap, show);
            Environment.SetEnvironmentVariable(InvasionState.EnvNoCap, no);
            InvasionState.ForceLootBeforeCap = oldForce;
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            EstateDefense.ResetForTest();
            WorldStar.ResetForTest();
            GameState.ResetAll();

            if (_fail > 0)
            {
                Debug.LogError("[LootCapSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("LootCapSelfCheck FAIL " + _fail);
            }
            Debug.Log("[LootCapSelfCheck] PASS\n" + _log);
        }
    }
}
