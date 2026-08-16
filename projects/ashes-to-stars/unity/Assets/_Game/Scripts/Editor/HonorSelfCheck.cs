using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>침략 명예는 승리 +30, 패배 0. QA_NO면 불변(§18-13).</summary>
    public static class HonorSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Honor Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(Honor.EnvShow);
            string no = Environment.GetEnvironmentVariable(Honor.EnvNo);
            Environment.SetEnvironmentVariable(Honor.EnvShow, null);
            Environment.SetEnvironmentVariable(Honor.EnvNo, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvShowWarehouse, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvNoWarehouse, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvShowFloor, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvNoFloor, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvShowCap, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvNoCap, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvShow, null);

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

            Check(Honor.Win == 30, "승리 +30");
            Check(Honor.Lose == 0, "패배 0");
            Check(Honor.Points == 0, $"시작 0 (실제 {Honor.Points})");
            Check(Honor.ApplyInvasion(true) == 30, "승리 Apply=30");
            Check(Honor.Points == 30, $"승리 뒤 30 (실제 {Honor.Points})");
            Check(Honor.LastGain == 30, $"LastGain 30 (실제 {Honor.LastGain})");
            Check(Honor.WinLine().Contains("명예 +30"),
                $"문구 +30 (실제 {Honor.WinLine()})");
            Check(Honor.WinLine().Contains("§18-13"), "문구 §18-13");
            Check(Honor.BalanceLine().Contains("명예 30"),
                $"잔액 30 (실제 {Honor.BalanceLine()})");

            Honor.ForgetInMemoryForTest();
            Check(Honor.Points == 30, $"재기동 뒤에도 30 (실제 {Honor.Points})");
            Check(Honor.LastGain == 30, "재기동 LastGain 유지");

            Check(Honor.ApplyInvasion(false) == 0, "패배 Apply=0");
            Check(Honor.Points == 30, $"패배해도 잔액 30 (실제 {Honor.Points})");
            Check(Honor.LastGain == 0, "패배 LastGain 0");

            Environment.SetEnvironmentVariable(Honor.EnvNo, "1");
            Check(Honor.Blocked, "QA_NO_HONOR이면 차단");
            Check(Honor.ApplyInvasion(true) == 0, "차단하면 승리도 0");
            Check(Honor.Points == 30, $"차단 중 잔액 불변 (실제 {Honor.Points})");
            Check(Honor.WinLine().Contains("없음"),
                $"차단 문구 없음 (실제 {Honor.WinLine()})");
            Environment.SetEnvironmentVariable(Honor.EnvNo, null);

            Check(Honor.ApplyInvasion(true) == 30, "차단을 풀면 다시 30");
            Check(Honor.Points == 60, $"두 번째 승리 60 (실제 {Honor.Points})");

            Honor.ResetForTest();
            GameState.ResetAll();
            SoftCap.ResetForTest();
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            EstateDefense.ResetForTest();
            EstateBuild.ResetForTest();
            WorldStar.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            GameState.SetTowerFloorForTest(1);
            GameState.Grant(100_000);
            GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            Check(Honor.Points == 0, "정산 전 명예 0");
            Check(InvasionState.TryBegin(), "출정");
            long loot = InvasionState.Settle(true);
            Check(loot > 0, $"정산 약탈 {loot}");
            Check(Honor.Points == 30, $"정산 승리 명예 30 (실제 {Honor.Points})");
            Check(Honor.LastGain == 30, "정산 LastGain 30");

            InvasionState.NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                + InvasionState.GuardSeconds + 1;
            Check(InvasionState.TryBegin(), "두 번째 출정");
            InvasionState.Settle(false);
            Check(Honor.Points == 30, $"정산 패배 잔액 30 (실제 {Honor.Points})");
            Check(Honor.LastGain == 0, "정산 패배 LastGain 0");
            InvasionState.NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            Honor.ResetForTest();
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            EstateDefense.ResetForTest();
            EstateBuild.ResetForTest();
            WorldStar.ResetForTest();
            SoftCap.ResetForTest();
            GameState.ResetAll();
            RacePrefs.Set(RaceId.인간);

            Environment.SetEnvironmentVariable(Honor.EnvShow, "1");
            GameState.Grant(500_000);
            WorldStar.EnemyDebuff = true;
            Honor.SeedQaIfRequested();
            Check(RacePrefs.Get() == RaceId.인간, "시드는 인간을 고른다");
            Check(GameState.TowerFloor >= WorldMapScreen.InvasionUnlockFloor,
                $"시드는 침략 해금 (실제 {GameState.TowerFloor}층)");
            Check(!WorldStar.EnemyDebuff, "시드가 적 디버프 잔재를 끈다");
            Check(!InvasionState.ShieldActive, "시드는 보호막을 안 건다");
            Check(Honor.WinLine().Contains("명예 +30"), "시드 화면 문구 +30");
            Check(WorldMapScreen.InvasionHubLockReason() == null, "시드 침략 카드가 열린다");
            Environment.SetEnvironmentVariable(Honor.EnvShow, null);

            _ = nameof(Honor.ApplyInvasion);
            _ = nameof(Honor.WinLine);
            _ = nameof(Honor.BalanceLine);
            _ = nameof(Honor.SeedQaIfRequested);
            _ = nameof(InvasionState.ResetPendingForHonorQa);

            Environment.SetEnvironmentVariable(Honor.EnvShow, show);
            Environment.SetEnvironmentVariable(Honor.EnvNo, no);
            Honor.ResetForTest();
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            EstateDefense.ResetForTest();
            EstateBuild.ResetForTest();
            WorldStar.ResetForTest();
            SoftCap.ResetForTest();
            GameState.ResetAll();

            if (_fail > 0)
            {
                Debug.LogError("[HonorSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("HonorSelfCheck FAIL " + _fail);
            }
            Debug.Log("[HonorSelfCheck] PASS\n" + _log);
        }
    }
}
