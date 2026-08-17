using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>동일 상대 24h 2회차 약탈·명예 20%, 3회차 0. QA_NO면 매번 전액(§18-13).</summary>
    public static class RepeatLootSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        static void ResetWorld()
        {
            GameState.ResetAll();
            SoftCap.ResetForTest();
            LifeSystem.ResetAll();
            Honor.ResetForTest();
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            EstateDefense.ResetForTest();
            EstateBuild.ResetForTest();
            WorldStar.ResetForTest();
            RacePrefs.Set(RaceId.인간);
        }

        [MenuItem("Ashes to Stars/QA/Repeat Loot Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(InvasionState.EnvShowRepeat);
            string no = Environment.GetEnvironmentVariable(InvasionState.EnvNoRepeat);
            long oldForce = InvasionState.ForceLootBeforeCap;
            Environment.SetEnvironmentVariable(InvasionState.EnvShowRepeat, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvNoRepeat, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvShowWarehouse, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvNoWarehouse, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvShowFloor, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvNoFloor, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvShowCap, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvNoCap, null);
            Environment.SetEnvironmentVariable(Honor.EnvShow, null);
            Environment.SetEnvironmentVariable(Honor.EnvNo, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvShow, null);

            ResetWorld();
            InvasionState.ForceLootBeforeCap = 5_000;

            Check(InvasionState.RepeatWindowSeconds == 24 * 3600, "창 24시간");
            Check(InvasionState.RepeatSecondPercent == 20, "2회차 20%");
            Check(InvasionState.RepeatThirdPercent == 0, "3회차 0");
            Check(InvasionState.NextAttempt() == 1, "시작 1회차");
            Check(InvasionState.RepeatPercent() == 100, "1회차 100");
            Check(InvasionState.LootCopper() == 5_000,
                $"1회차 약탈 5000 (실제 {InvasionState.LootCopper()})");
            Check(Honor.ApplyInvasion(true) == 15, "1회차 명예 15");
            Honor.ResetForTest();

            GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            GameState.Grant(100_000);
            Check(InvasionState.TryBegin(), "1회 출정");
            long first = InvasionState.Settle(true);
            Check(first == 5_000, $"1회 정산 5000 (실제 {first})");
            Check(Honor.Points == 15, $"1회 정산 명예 15 (실제 {Honor.Points})");
            Check(Honor.LastGain == 15, "1회 LastGain 15");
            Check(InvasionState.NextAttempt() == 2, "정산 뒤 다음 2회차");
            Check(InvasionState.RepeatLootLine().Contains("−80%"),
                $"2회차 문구 −80% (실제 {InvasionState.RepeatLootLine()})");
            Check(InvasionState.RepeatLootLine().Contains("§18-13"), "문구 §18-13");

            InvasionState.ForgetInMemoryForTest();
            Check(InvasionState.NextAttempt() == 2, "재기동 뒤에도 2회차");

            long t0 = InvasionState.NowUnix();
            InvasionState.NowUnix = () => t0 + InvasionState.GuardSeconds + 1;
            Check(InvasionState.LootCopper() == 1_000,
                $"12h+1초 2회차 약탈 1000 (실제 {InvasionState.LootCopper()})");
            Check(Honor.ApplyInvasion(true) == 3, "2회차 명예 3");
            Honor.ResetForTest();
            Check(InvasionState.TryBegin(), "2회 출정");
            long second = InvasionState.Settle(true);
            Check(second == 1_000, $"2회 정산 1000 (실제 {second})");
            Check(Honor.Points == 3, $"2회 정산 명예 3 (실제 {Honor.Points})");
            Check(Honor.LastGain == 3, "2회 LastGain 3");
            Check(InvasionState.NextAttempt() == 3, "정산 뒤 다음 3회차");
            Check(InvasionState.RepeatPercent() == 0, "3회차 0");
            Check(InvasionState.LootCopper() == 0,
                $"3회차 약탈 0 (실제 {InvasionState.LootCopper()})");

            long t1 = InvasionState.NowUnix();
            InvasionState.NowUnix = () => t1 + InvasionState.GuardSeconds + 1;
            Check(InvasionState.LootCopper() == 0,
                $"12h+1초 3회차도 0 (실제 {InvasionState.LootCopper()})");
            Check(Honor.ApplyInvasion(true) == 0, "3회차 명예 0");
            Honor.ResetForTest();
            Check(InvasionState.TryBegin(), "3회 출정");
            long third = InvasionState.Settle(true);
            Check(third == 0, $"3회 정산 0 (실제 {third})");
            Check(Honor.Points == 0, $"3회 정산 명예 0 (실제 {Honor.Points})");

            long t2 = InvasionState.NowUnix();
            InvasionState.NowUnix = () => t2 + InvasionState.RepeatWindowSeconds + 1;
            Check(InvasionState.NextAttempt() == 1, "24h+1초면 다시 1회차");
            Check(InvasionState.RepeatPercent() == 100, "창 밖 100");
            Check(InvasionState.LootCopper() == 5_000,
                $"창 밖 약탈 5000 (실제 {InvasionState.LootCopper()})");

            Environment.SetEnvironmentVariable(InvasionState.EnvNoRepeat, "1");
            ResetWorld();
            InvasionState.ForceLootBeforeCap = 5_000;
            GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            GameState.Grant(100_000);
            Check(InvasionState.RepeatLootBlocked, "QA_NO_REPEAT_LOOT이면 차단");
            Check(InvasionState.RepeatPercent() == 100, "차단하면 100");
            Check(InvasionState.TryBegin(), "차단 출정");
            Check(InvasionState.Settle(true) == 5_000, "차단 1회 전액");
            InvasionState.NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                + InvasionState.GuardSeconds + 1;
            Check(InvasionState.LootCopper() == 5_000,
                $"차단하면 두 번째도 전액 (실제 {InvasionState.LootCopper()})");
            Check(InvasionState.RepeatLootLine().Contains("없음"),
                $"차단 문구 없음 (실제 {InvasionState.RepeatLootLine()})");
            Check(Honor.ApplyInvasion(true) == 15, "반복 차단이면 명예는 방어 0의 15");
            Environment.SetEnvironmentVariable(InvasionState.EnvNoRepeat, null);

            ResetWorld();
            InvasionState.ForceLootBeforeCap = 5_000;
            GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            GameState.Grant(100_000);
            Environment.SetEnvironmentVariable(InvasionState.EnvShowRepeat, "1");
            WorldStar.EnemyDebuff = true;
            InvasionState.SeedRepeatLootQaIfRequested();
            Check(RacePrefs.Get() == RaceId.인간, "시드는 인간을 고른다");
            Check(GameState.TowerFloor >= WorldMapScreen.InvasionUnlockFloor,
                $"시드는 침략 해금 (실제 {GameState.TowerFloor}층)");
            Check(!WorldStar.EnemyDebuff, "시드가 적 디버프 잔재를 끈다");
            Check(!InvasionState.ShieldActive, "시드는 보호막을 안 건다");
            Check(InvasionState.NextAttempt() == 2, "시드는 2회차");
            Check(InvasionState.LootCopper() == 1_000,
                $"시드 약탈 1000 (실제 {InvasionState.LootCopper()})");
            Check(InvasionState.RepeatLootLine().Contains("−80%"), "시드 화면 문구 −80%");
            Check(WorldMapScreen.InvasionHubLockReason() == null, "시드 침략 카드가 열린다");
            Environment.SetEnvironmentVariable(InvasionState.EnvShowRepeat, null);

            _ = nameof(InvasionState.NextAttempt);
            _ = nameof(InvasionState.RepeatPercent);
            _ = nameof(InvasionState.ApplyRepeatLoot);
            _ = nameof(InvasionState.RepeatLootLine);
            _ = nameof(InvasionState.RecordStrike);
            _ = nameof(InvasionState.SeedRepeatLootQaIfRequested);

            Environment.SetEnvironmentVariable(InvasionState.EnvShowRepeat, show);
            Environment.SetEnvironmentVariable(InvasionState.EnvNoRepeat, no);
            InvasionState.ForceLootBeforeCap = oldForce;
            ResetWorld();

            if (_fail > 0)
            {
                Debug.LogError("[RepeatLootSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("RepeatLootSelfCheck FAIL " + _fail);
            }
            Debug.Log("[RepeatLootSelfCheck] PASS\n" + _log);
        }
    }
}
