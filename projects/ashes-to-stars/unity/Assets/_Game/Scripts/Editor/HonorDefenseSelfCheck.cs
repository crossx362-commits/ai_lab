using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>승리 명예는 상대 방어 Cut에 ±50%. QA_NO면 옛 +30(§18-13).</summary>
    public static class HonorDefenseSelfCheck
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

        [MenuItem("Ashes to Stars/QA/Honor Defense Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(Honor.EnvShowDefense);
            string no = Environment.GetEnvironmentVariable(Honor.EnvNoDefense);
            string honorShow = Environment.GetEnvironmentVariable(Honor.EnvShow);
            string honorNo = Environment.GetEnvironmentVariable(Honor.EnvNo);
            Environment.SetEnvironmentVariable(Honor.EnvShowDefense, null);
            Environment.SetEnvironmentVariable(Honor.EnvNoDefense, null);
            Environment.SetEnvironmentVariable(Honor.EnvShow, null);
            Environment.SetEnvironmentVariable(Honor.EnvNo, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvShow, null);

            ResetWorld();

            Check(Honor.WinForCut(0) == 15, "Cut 0 = 15");
            Check(Honor.WinForCut(20) == 30, "Cut 20 = 30");
            Check(Honor.WinForCut(40) == 45, "Cut 40 = 45");
            Check(Honor.WinForCut(-1) == 15, "음수는 0과 같다");
            Check(Honor.WinForCut(80) == 45, "상한 밖은 45");
            Check(Honor.WinNow() == 15, "빈 영지 WinNow=15");
            Check(Honor.ApplyInvasion(true) == 15, "빈 영지 Apply=15");
            Check(Honor.LastGain == 15, "LastGain 15");
            Check(Honor.WinLine().Contains("+15"),
                $"빈 영지 문구 +15 (실제 {Honor.WinLine()})");
            Check(Honor.WinLine().Contains("방어 비례"),
                $"문구 방어 비례 (실제 {Honor.WinLine()})");

            Honor.ResetForTest();
            EstateDefense.GarrisonCount = () => 1;
            EstateDefense.SetLevelForTest(EstateDefense.Kind.화살탑, 4);
            Check(EstateDefense.CutPercent() == 20,
                $"화살탑4·수비 Cut 20 (실제 {EstateDefense.CutPercent()})");
            Check(Honor.WinNow() == 30, "중간 방어 30");
            Check(Honor.ApplyInvasion(true) == 30, "중간 Apply=30");

            Honor.ResetForTest();
            EstateDefense.SetLevelForTest(EstateDefense.Kind.화살탑, 8);
            Check(EstateDefense.CutPercent() == 40,
                $"화살탑8·수비 Cut 40 (실제 {EstateDefense.CutPercent()})");
            Check(Honor.WinNow() == 45, "강한 방어 45");
            Check(Honor.ApplyInvasion(true) == 45, "강한 Apply=45");
            Check(Honor.WinLine().Contains("+45"),
                $"강한 문구 +45 (실제 {Honor.WinLine()})");

            Honor.ResetForTest();
            EstateDefense.ResetForTest();
            EstateDefense.SetLevelForTest(EstateDefense.Kind.화살탑, 16);
            Check(EstateDefense.CutPercent() == 40,
                $"화살탑16·수비 없음 Cut 40 (실제 {EstateDefense.CutPercent()})");
            Check(Honor.WinNow() == 45, "수비 없어도 레벨로 45");
            Check(Honor.ApplyInvasion(true) == 45, "수비 없음 Apply=45");

            Honor.ForgetInMemoryForTest();
            Check(Honor.Points == 45, "재기동 뒤에도 45");

            Environment.SetEnvironmentVariable(Honor.EnvNoDefense, "1");
            EstateDefense.ResetForTest();
            Check(Honor.ScaleBlocked, "QA_NO_HONOR_DEFENSE");
            Check(Honor.WinForCut(0) == 30, "차단하면 Cut 0도 30");
            Check(Honor.WinForCut(40) == 30, "차단하면 Cut 40도 30");
            Check(Honor.WinNow() == 30, "차단 WinNow=30");
            Check(Honor.ApplyInvasion(true) == 30, "차단 Apply=30");
            Check(Honor.WinLine().Contains("+30"),
                $"차단 문구 +30 (실제 {Honor.WinLine()})");
            Check(!Honor.WinLine().Contains("방어 비례"),
                "차단 문구는 옛 고정");
            Environment.SetEnvironmentVariable(Honor.EnvNoDefense, null);

            ResetWorld();
            GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            GameState.Grant(100_000);
            Check(Honor.Points == 0, "정산 전 0");
            Check(InvasionState.TryBegin(), "출정");
            long loot = InvasionState.Settle(true);
            Check(loot > 0, $"정산 약탈 {loot}");
            Check(Honor.Points == 15, $"빈 영지 정산 15 (실제 {Honor.Points})");

            ResetWorld();
            GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            GameState.Grant(100_000);
            EstateDefense.SetLevelForTest(EstateDefense.Kind.화살탑, 16);
            Check(InvasionState.TryBegin(), "강한 출정");
            InvasionState.Settle(true);
            Check(Honor.Points == 45, $"강한 정산 45 (실제 {Honor.Points})");

            ResetWorld();
            Environment.SetEnvironmentVariable(Honor.EnvShowDefense, "1");
            WorldStar.EnemyDebuff = true;
            GameState.Grant(500_000);
            Honor.SeedDefenseQaIfRequested();
            Check(RacePrefs.Get() == RaceId.인간, "시드는 인간");
            Check(GameState.TowerFloor >= WorldMapScreen.InvasionUnlockFloor, "시드 30층");
            Check(!WorldStar.EnemyDebuff, "시드가 적 디버프를 끈다");
            Check(EstateDefense.CutPercent() == 40, "시드 Cut 40");
            Check(Honor.WinNow() == 45, "시드 WinNow 45");
            Check(Honor.WinLine().Contains("+45"),
                $"시드 문구 +45 (실제 {Honor.WinLine()})");
            Check(WorldMapScreen.InvasionHubLockReason() == null, "시드 침략 카드");
            Environment.SetEnvironmentVariable(Honor.EnvShowDefense, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string honorSrc = File.ReadAllText(Path.Combine(runtime, "Honor.cs"));
            string invSrc = File.ReadAllText(Path.Combine(runtime, "InvasionState.cs"));
            string mapSrc = File.ReadAllText(Path.Combine(runtime, "WorldMapScreen.cs"));
            Check(honorSrc.IndexOf("WinForCut", StringComparison.Ordinal) >= 0
                  && honorSrc.IndexOf("EstateDefense.CutPercent", StringComparison.Ordinal) >= 0,
                "Honor가 CutPercent를 읽는다");
            Check(honorSrc.IndexOf("WinNow()", StringComparison.Ordinal) >= 0,
                "ApplyInvasion이 WinNow를 읽는다");
            Check(invSrc.IndexOf("Honor.ApplyInvasion", StringComparison.Ordinal) >= 0,
                "정산이 ApplyInvasion을 읽는다");
            Check(mapSrc.IndexOf("SeedDefenseQaIfRequested", StringComparison.Ordinal) >= 0
                  && mapSrc.IndexOf("Honor.WinLine", StringComparison.Ordinal) >= 0,
                "월드맵이 Seed·WinLine을 읽는다");

            _ = nameof(Honor.WinForCut);
            _ = nameof(Honor.WinNow);
            _ = nameof(Honor.ApplyInvasion);
            _ = nameof(Honor.SeedDefenseQaIfRequested);

            Environment.SetEnvironmentVariable(Honor.EnvShowDefense, show);
            Environment.SetEnvironmentVariable(Honor.EnvNoDefense, no);
            Environment.SetEnvironmentVariable(Honor.EnvShow, honorShow);
            Environment.SetEnvironmentVariable(Honor.EnvNo, honorNo);
            ResetWorld();

            if (_fail == 0) Debug.Log("[HonorDefenseSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[HonorDefenseSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[HonorDefenseSelfCheck] FAIL {_fail}건");
        }
    }
}
