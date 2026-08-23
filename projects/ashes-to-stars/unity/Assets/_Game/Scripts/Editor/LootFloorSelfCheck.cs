using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>침략 약탈은 본성×0.5 G/h 밑으로 안 내려간다. QA_NO면 그대로(§18-13).</summary>
    public static class LootFloorSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Loot Floor Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(InvasionState.EnvShowFloor);
            string no = Environment.GetEnvironmentVariable(InvasionState.EnvNoFloor);
            long oldForce = InvasionState.ForceLootBeforeCap;
            Environment.SetEnvironmentVariable(InvasionState.EnvShowFloor, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvNoFloor, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvShowCap, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvNoCap, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvShow, null);
            InvasionState.ForceLootBeforeCap = 0;

            GameState.ResetAll();
            SoftCap.ResetForTest();
            LifeSystem.ResetAll();
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            EstateDefense.ResetForTest();
            EstateBuild.ResetForTest();
            WorldStar.ResetForTest();
            RacePrefs.Set(RaceId.인간);

            Check(EstateBuild.KeepLevel == 1, $"본성 시작 1 (실제 {EstateBuild.KeepLevel})");
            Check(InvasionState.FloorCopperPerKeep == 5_000, "1레벨당 5000");
            Check(InvasionState.FloorCopper(1) == 5_000, $"Keep1=5000 (실제 {InvasionState.FloorCopper(1)})");
            Check(InvasionState.FloorCopper(0) == 5_000, "Keep 0도 1로 본다");
            Check(InvasionState.FloorCopper(2) == 10_000, $"Keep2=10000 (실제 {InvasionState.FloorCopper(2)})");
            Check(InvasionState.FloorCopper() == 5_000, "기본 본성이 5000");
            Check(InvasionState.ApplyLootFloor(3_000) == 5_000, "공식 3000→5000");
            Check(InvasionState.ApplyLootFloor(5_000) == 5_000, "5000은 그대로");
            Check(InvasionState.ApplyLootFloor(6_000) == 6_000, "6000은 그대로");
            Check(InvasionState.LootFloorLine().Contains("0.5 G/h"),
                $"문구 0.5 G/h (실제 {InvasionState.LootFloorLine()})");
            Check(InvasionState.LootFloorLine().Contains("50실버"),
                $"문구 50실버 (실제 {InvasionState.LootFloorLine()})");

            GameState.SetTowerFloorForTest(1);
            InvasionState.ForceLootBeforeCap = 3_000;
            Check(InvasionState.LootCopper() == 5_000,
                $"T1에서 공식 3000이면 5000 (실제 {InvasionState.LootCopper()})");

            Environment.SetEnvironmentVariable(InvasionState.EnvNoFloor, "1");
            Check(InvasionState.LootFloorBlocked, "QA_NO_LOOT_FLOOR이면 차단");
            Check(InvasionState.ApplyLootFloor(3_000) == 3_000, "차단하면 3000");
            Check(InvasionState.LootCopper() == 3_000,
                $"차단하면 T1이 3000 (실제 {InvasionState.LootCopper()})");
            Check(InvasionState.LootFloorLine().Contains("없음"),
                $"차단 문구 없음 (실제 {InvasionState.LootFloorLine()})");
            Environment.SetEnvironmentVariable(InvasionState.EnvNoFloor, null);

            Check(InvasionState.LootCopper() == 5_000, "차단을 풀면 다시 5000");
            InvasionState.ForceLootBeforeCap = 0;

            GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            InvasionState.ForceLootBeforeCap = 9_211;
            long t3 = InvasionState.LootCopper();
            Check(t3 == 9_211, $"강제 9211은 바닥 위 (실제 {t3})");
            Check(InvasionState.ApplyLootFloor(t3) == t3, "바닥 위는 안 올린다");
            InvasionState.ForceLootBeforeCap = 0;

            GameState.Grant(500_000);
            InvasionState.ForceLootBeforeCap = 3_000;
            Check(!InvasionState.ShieldActive, "정산 전 보호막 없음");
            Check(InvasionState.TryBegin(), "출정");
            long loot = InvasionState.Settle(true);
            Check(loot == 5_000, $"정산 약탈이 바닥 5000 (실제 {loot})");
            Check(InvasionState.LastLoot == 5_000, $"받은 금액 5000 (실제 {InvasionState.LastLoot})");
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            EstateDefense.ResetForTest();
            EstateBuild.ResetForTest();
            WorldStar.ResetForTest();
            SoftCap.ResetForTest();
            GameState.ResetAll();
            RacePrefs.Set(RaceId.인간);

            Environment.SetEnvironmentVariable(InvasionState.EnvShowFloor, "1");
            GameState.SetTowerFloorForTest(1);
            GameState.TrySelectTier(0);
            WorldStar.EnemyDebuff = true;
            InvasionState.SeedLootFloorQaIfRequested();
            Check(RacePrefs.Get() == RaceId.인간, "시드는 인간을 고른다");
            Check(GameState.TowerFloor >= WorldMapScreen.InvasionUnlockFloor,
                $"시드는 침략 해금 (실제 {GameState.TowerFloor}층)");
            Check(EstateBuild.KeepLevel == 1, "시드 본성 1");
            Check(!WorldStar.EnemyDebuff, "시드가 적 디버프 잔재를 끈다");
            Check(InvasionState.ForceLootBeforeCap == 3_000, "시드가 공식 3000을 넣는다");
            Check(InvasionState.LootCopper() == 5_000,
                $"시드 약탈이 5000 (실제 {InvasionState.LootCopper()})");
            Check(!InvasionState.ShieldActive, "시드는 보호막을 안 건다");
            Check(InvasionState.LootFloorLine().Contains("0.5 G/h"), "시드 화면 문구 0.5 G/h");
            Check(InvasionState.LootFloorLine().Contains("50실버"),
                $"시드 문구가 50실버 (실제 {InvasionState.LootFloorLine()})");
            Check(WorldMapScreen.InvasionHubLockReason() == null, "시드 침략 카드가 열린다");
            Environment.SetEnvironmentVariable(InvasionState.EnvShowFloor, null);

            _ = nameof(InvasionState.FloorCopper);
            _ = nameof(InvasionState.ApplyLootFloor);
            _ = nameof(InvasionState.LootFloorLine);

            string invSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/InvasionState.cs"));
            Check(invSrc.Contains("ShortCopper(FloorCopper())")
                  && invSrc.IndexOf("FormatCurrency(FloorCopper())") < 0,
                "FloorCopper은 ShortCopper만");
            Check(invSrc.Contains("FormatCurrency(ApplyWarehouseLoot"),
                "창고 FormatCurrency 유지(다음 칸)");
            _ = nameof(InvasionState.SeedLootFloorQaIfRequested);
            _ = nameof(InvasionState.ForceLootBeforeCap);

            Environment.SetEnvironmentVariable(InvasionState.EnvShowFloor, show);
            Environment.SetEnvironmentVariable(InvasionState.EnvNoFloor, no);
            InvasionState.ForceLootBeforeCap = oldForce;
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            EstateDefense.ResetForTest();
            EstateBuild.ResetForTest();
            WorldStar.ResetForTest();
            SoftCap.ResetForTest();
            GameState.ResetAll();

            if (_fail > 0)
            {
                Debug.LogError("[LootFloorSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("LootFloorSelfCheck FAIL " + _fail);
            }
            Debug.Log("[LootFloorSelfCheck] PASS\n" + _log);
        }
    }
}
