using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>침략 약탈은 창고의 20%다. QA_NO면 옛 출정×3(§18-13).</summary>
    public static class LootWarehouseSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Loot Warehouse Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(InvasionState.EnvShowWarehouse);
            string no = Environment.GetEnvironmentVariable(InvasionState.EnvNoWarehouse);
            long oldForce = InvasionState.ForceLootBeforeCap;
            Environment.SetEnvironmentVariable(InvasionState.EnvShowWarehouse, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvNoWarehouse, null);
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

            Check(InvasionState.WarehouseLootPercent == 20, "20%");
            Check(InvasionState.QaWarehouseCopper == 25_000, "시드 창고 25000");
            Check(InvasionState.WarehouseCopper() == 0, $"빈 창고 (실제 {InvasionState.WarehouseCopper()})");
            Check(InvasionState.ApplyWarehouseLoot(25_000) == 5_000, "25000→5000");
            Check(InvasionState.ApplyWarehouseLoot(0) == 0, "0→0");
            Check(InvasionState.ApplyWarehouseLoot(100_000) == 20_000, "100000→20000");
            Check(InvasionState.LootCopper() == 5_000,
                $"창고 0이면 바닥 5000 (실제 {InvasionState.LootCopper()})");

            InvasionState.SetWarehouseCopper(25_000);
            Check(GameState.Wallet.Copper == 25_000, $"지갑 25000 (실제 {GameState.Wallet.Copper})");
            Check(InvasionState.WarehouseCopper() == 25_000, "창고 25000");
            Check(InvasionState.LootCopper() == 5_000,
                $"창고 25000이면 5000 (실제 {InvasionState.LootCopper()})");
            Check(InvasionState.WarehouseLootLine().Contains("20%"),
                $"문구 20% (실제 {InvasionState.WarehouseLootLine()})");
            Check(InvasionState.WarehouseLootLine().Contains("50실버"),
                $"문구 50실버 (실제 {InvasionState.WarehouseLootLine()})");

            InvasionState.SetWarehouseCopper(100_000);
            Check(InvasionState.LootCopper() == 20_000,
                $"창고 100000이면 20000 (실제 {InvasionState.LootCopper()})");

            Environment.SetEnvironmentVariable(InvasionState.EnvNoWarehouse, "1");
            Check(InvasionState.LootWarehouseBlocked, "QA_NO_WAREHOUSE_LOOT이면 차단");
            Check(InvasionState.ApplyWarehouseLoot(25_000) == 25_000, "차단하면 Apply가 그대로");
            long old = InvasionState.LootCopper();
            Check(old != 20_000, $"차단하면 창고와 무관 (실제 {old})");
            Check(InvasionState.WarehouseLootLine().Contains("없음"),
                $"차단 문구 없음 (실제 {InvasionState.WarehouseLootLine()})");
            InvasionState.SetWarehouseCopper(25_000);
            Check(InvasionState.LootCopper() == old, "차단 중 창고를 바꿔도 공식 불변");
            Environment.SetEnvironmentVariable(InvasionState.EnvNoWarehouse, null);

            InvasionState.SetWarehouseCopper(100_000);
            Check(InvasionState.LootCopper() == 20_000, "차단을 풀면 다시 20000");

            GameState.SetTowerFloorForTest(1);
            InvasionState.SetWarehouseCopper(25_000);
            Check(!InvasionState.ShieldActive, "정산 전 보호막 없음");
            long preview = InvasionState.LootCopper();
            Check(preview == 5_000, $"정산 전 미리보기 5000 (실제 {preview})");
            Check(InvasionState.TryBegin(), "출정");
            Check(InvasionState.WarehouseCopper() == 25_000,
                $"출정 뒤에도 창고 25000 (실제 {InvasionState.WarehouseCopper()})");
            long loot = InvasionState.Settle(true);
            Check(loot == 5_000, $"정산 약탈이 5000 (실제 {loot})");
            Check(InvasionState.LastLoot == 5_000, $"받은 금액 5000 (실제 {InvasionState.LastLoot})");
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            EstateDefense.ResetForTest();
            EstateBuild.ResetForTest();
            WorldStar.ResetForTest();
            SoftCap.ResetForTest();
            GameState.ResetAll();
            RacePrefs.Set(RaceId.인간);

            Environment.SetEnvironmentVariable(InvasionState.EnvShowWarehouse, "1");
            GameState.Grant(500_000);
            WorldStar.EnemyDebuff = true;
            InvasionState.SeedWarehouseLootQaIfRequested();
            Check(RacePrefs.Get() == RaceId.인간, "시드는 인간을 고른다");
            Check(GameState.TowerFloor >= WorldMapScreen.InvasionUnlockFloor,
                $"시드는 침략 해금 (실제 {GameState.TowerFloor}층)");
            Check(EstateBuild.KeepLevel == 1, "시드 본성 1");
            Check(!WorldStar.EnemyDebuff, "시드가 적 디버프 잔재를 끈다");
            Check(GameState.Wallet.Copper == 25_000,
                $"시드 창고 25000 (실제 {GameState.Wallet.Copper})");
            Check(InvasionState.LootCopper() == 5_000,
                $"시드 약탈이 5000 (실제 {InvasionState.LootCopper()})");
            Check(!InvasionState.ShieldActive, "시드는 보호막을 안 건다");
            Check(InvasionState.WarehouseLootLine().Contains("20%"), "시드 화면 문구 20%");
            Check(InvasionState.WarehouseLootLine().Contains("50실버"),
                $"시드 문구가 50실버 (실제 {InvasionState.WarehouseLootLine()})");
            Check(WorldMapScreen.InvasionHubLockReason() == null, "시드 침략 카드가 열린다");
            Environment.SetEnvironmentVariable(InvasionState.EnvShowWarehouse, null);

            _ = nameof(InvasionState.WarehouseCopper);
            _ = nameof(InvasionState.ApplyWarehouseLoot);
            _ = nameof(InvasionState.WarehouseLootLine);
            _ = nameof(InvasionState.SeedWarehouseLootQaIfRequested);
            _ = nameof(InvasionState.SetWarehouseCopper);

            Environment.SetEnvironmentVariable(InvasionState.EnvShowWarehouse, show);
            Environment.SetEnvironmentVariable(InvasionState.EnvNoWarehouse, no);
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
                Debug.LogError("[LootWarehouseSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("LootWarehouseSelfCheck FAIL " + _fail);
            }
            Debug.Log("[LootWarehouseSelfCheck] PASS\n" + _log);
        }
    }
}
