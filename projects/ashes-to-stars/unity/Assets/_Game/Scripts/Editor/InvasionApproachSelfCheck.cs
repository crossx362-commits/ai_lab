using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>침략자가 4면 중 하나를 고른다. QA_NO면 옛 최단 자동(§13-3).</summary>
    public static class InvasionApproachSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Invasion Approach Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(InvasionApproach.EnvShow);
            string no = Environment.GetEnvironmentVariable(InvasionApproach.EnvNo);
            Environment.SetEnvironmentVariable(InvasionApproach.EnvShow, null);
            Environment.SetEnvironmentVariable(InvasionApproach.EnvNo, null);

            GameState.ResetAll();
            EstateGrid.ResetForTest();
            InvasionApproach.ResetForTest();

            Check(!InvasionApproach.Blocked, "기본은 켜짐");
            Check(!InvasionApproach.HasPick, "기본은 안 고름");
            Check(InvasionApproach.Side == EstateGrid.InvaderSide(),
                "안 고르면 최단 자동");
            Check(InvasionApproach.Side == EstateGrid.Side.북, "열린 격자는 북이 최단");
            Check(InvasionApproach.Path() == 3, "북 최단 3칸");
            Check(InvasionApproach.CanPick(EstateGrid.Side.남), "남도 열린다");
            Check(InvasionApproach.PathOf(EstateGrid.Side.남) == 4, "남 4칸");

            InvasionApproach.Pick(EstateGrid.Side.남);
            Check(InvasionApproach.HasPick, "고르면 HasPick");
            Check(InvasionApproach.Side == EstateGrid.Side.남, "고른 면은 남");
            Check(InvasionApproach.Path() == 4, "고른 남 4칸");
            Check(InvasionApproach.Line().IndexOf("남", StringComparison.Ordinal) >= 0
                  && InvasionApproach.Line().IndexOf("§13-3", StringComparison.Ordinal) >= 0,
                $"줄 남 (실제 {InvasionApproach.Line()})");

            GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            GameState.Grant(1_000_000);
            Check(InvasionState.TryBegin(), "고른 면으로 출정");
            Check(InvasionState.ApproachSide == EstateGrid.Side.남,
                $"출정 Approach는 남 (실제 {InvasionState.ApproachSide})");
            InvasionState.Settle(false);

            GameState.ResetAll();
            EstateGrid.ResetForTest();
            InvasionApproach.ResetForTest();
            Environment.SetEnvironmentVariable(InvasionApproach.EnvNo, "1");
            InvasionApproach.Pick(EstateGrid.Side.남);
            Check(InvasionApproach.Blocked, "QA_NO");
            Check(!InvasionApproach.HasPick, "QA_NO면 고름 무시");
            Check(InvasionApproach.Side == EstateGrid.Side.북, "QA_NO면 옛 최단 북");
            GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            GameState.Grant(1_000_000);
            Check(InvasionState.TryBegin(), "QA_NO 출정");
            Check(InvasionState.ApproachSide == EstateGrid.Side.북,
                "QA_NO면 출정도 북");
            InvasionState.Settle(false);
            Environment.SetEnvironmentVariable(InvasionApproach.EnvNo, null);

            GameState.ResetAll();
            EstateGrid.ResetForTest();
            InvasionApproach.ResetForTest();
            Environment.SetEnvironmentVariable(InvasionApproach.EnvShow, "1");
            InvasionApproach.SeedQaIfRequested();
            Check(InvasionApproach.ShowQa, "시드 ShowQa");
            Check(InvasionApproach.Picking, "시드는 고르기 화면");
            Check(InvasionApproach.Side == EstateGrid.Side.남, "시드 남");
            Check(InvasionApproach.Line().IndexOf("고른다", StringComparison.Ordinal) >= 0,
                $"시드 줄 (실제 {InvasionApproach.Line()})");
            Check(GameState.TowerFloor >= WorldMapScreen.InvasionUnlockFloor, "시드 30층");
            Environment.SetEnvironmentVariable(InvasionApproach.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string invSrc = File.ReadAllText(Path.Combine(runtime, "InvasionState.cs"));
            string mapSrc = File.ReadAllText(Path.Combine(runtime, "WorldMapScreen.cs"));
            Check(invSrc.IndexOf("InvasionApproach.Side", StringComparison.Ordinal) >= 0,
                "출정이 Side를 읽는다");
            Check(mapSrc.IndexOf("InvasionApproach.Picking", StringComparison.Ordinal) >= 0
                  && mapSrc.IndexOf("InvasionApproach.Pick", StringComparison.Ordinal) >= 0,
                "월드맵이 Picking·Pick을 읽는다");
            Check(invSrc.IndexOf("_approach = EstateGrid.InvaderSide()", StringComparison.Ordinal) < 0,
                "출정이 옛 InvaderSide 대입을 안 쓴다");

            _ = nameof(InvasionApproach.Pick);
            _ = nameof(InvasionApproach.Side);
            _ = nameof(InvasionApproach.Path);
            _ = nameof(InvasionApproach.Line);
            _ = nameof(InvasionApproach.SeedQaIfRequested);

            Environment.SetEnvironmentVariable(InvasionApproach.EnvShow, show);
            Environment.SetEnvironmentVariable(InvasionApproach.EnvNo, no);
            InvasionApproach.ResetForTest();
            GameState.ResetAll();
            EstateGrid.ResetForTest();

            if (_fail == 0) Debug.Log("[InvasionApproachSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[InvasionApproachSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[InvasionApproachSelfCheck] FAIL {_fail}건");
        }
    }
}
