using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>탑 대보스 마릿수 60/30/10. QA_NO면 옛 1체(§10-7·§18-11).</summary>
    public static class BossCountSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Boss Count Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(BossCount.EnvShow);
            string no = Environment.GetEnvironmentVariable(BossCount.EnvNo);
            Environment.SetEnvironmentVariable(BossCount.EnvShow, null);
            Environment.SetEnvironmentVariable(BossCount.EnvNo, null);
            GameState.ResetAll();
            BossCount.ResetForTest();
            DungeonRun.End();

            Check(!BossCount.Blocked, "기본은 켜짐");
            Check(BossCount.Applies(10) && BossCount.Applies(20) && BossCount.Applies(100),
                "10·20·100은 대보스 마릿수");
            Check(!BossCount.Applies(5) && !BossCount.Applies(15) && !BossCount.Applies(6),
                "5·15·6은 1체");
            Check(BossCount.Of(5) == 1 && BossCount.Of(6) == 1,
                $"중간·일반은 1 (실제 {BossCount.Of(5)}/{BossCount.Of(6)})");
            Check(BossCount.FromRoll(0f) == 1 && BossCount.FromRoll(0.59f) == 1,
                "0~0.59는 1체");
            Check(BossCount.FromRoll(0.60f) == 2 && BossCount.FromRoll(0.89f) == 2,
                "0.60~0.89는 2체");
            Check(BossCount.FromRoll(0.90f) == 3 && BossCount.FromRoll(1f) == 3,
                "0.90~1은 3체");

            BossCount.Force = 2;
            Check(BossCount.Of(10) == 2, "Force 2");
            Check(BossCount.Begin(10) == 2 && BossCount.Fight == 2,
                "Begin이 Fight에 2를 남긴다");
            Check(BossCount.FormatLine(10).IndexOf("2체", StringComparison.Ordinal) >= 0
                  && BossCount.FormatLine(10).IndexOf("§10-7", StringComparison.Ordinal) >= 0,
                $"줄 2체 (실제 {BossCount.FormatLine(10)})");
            Check(string.IsNullOrEmpty(BossCount.FormatLine(5)), "5층 줄 없음");
            BossCount.Force = 0;

            DungeonRun.Begin(1u, 0, DungeonKind.일반, GameFlow.Field);
            Check(!BossCount.Applies(10) && BossCount.Of(10) == 1,
                "던전 중엔 탑 마릿수를 안 굴린다");
            DungeonRun.End();
            Check(BossCount.Applies(10), "던전을 나가면 대보스 마릿수가 돌아온다");

            Environment.SetEnvironmentVariable(BossCount.EnvNo, "1");
            Check(BossCount.Blocked, "QA_NO");
            Check(!BossCount.Applies(10) && BossCount.Of(10) == 1,
                "QA_NO면 10층도 1");
            Check(BossCount.FormatLine(10).IndexOf("1체", StringComparison.Ordinal) >= 0,
                $"차단 줄 (실제 {BossCount.FormatLine(10)})");
            Check(BossCount.Begin(10) == 1 && BossCount.Fight == 1,
                "QA_NO Begin도 1");
            Environment.SetEnvironmentVariable(BossCount.EnvNo, null);

            Environment.SetEnvironmentVariable(BossCount.EnvShow, "1");
            GameState.ResetAll();
            BossCount.ResetForTest();
            BossCount.SeedQaIfRequested();
            Check(BossCount.ShowQa, "시드 ShowQa");
            Check(GameState.TowerFloor == 10, $"시드 10층 (실제 {GameState.TowerFloor})");
            Check(BossCount.Of(10) == BossCount.QaForce,
                $"시드 Force {BossCount.QaForce} (실제 {BossCount.Of(10)})");
            Check(BossCount.Line().IndexOf("2체", StringComparison.Ordinal) >= 0
                  && BossCount.Line().IndexOf("§10-7", StringComparison.Ordinal) >= 0,
                $"시드 줄 (실제 {BossCount.Line()})");
            Environment.SetEnvironmentVariable(BossCount.EnvShow, null);
            BossCount.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string battle = File.ReadAllText(Path.Combine(runtime, "BattleScreen.cs"));
            string tower = File.ReadAllText(Path.Combine(runtime, "TowerScreen.cs"));
            Check(battle.IndexOf("BossCount.Begin", StringComparison.Ordinal) >= 0,
                "전투가 Begin을 읽는다");
            Check(battle.IndexOf("BossCount.Fight", StringComparison.Ordinal) >= 0,
                "드랍이 Fight를 읽는다");
            Check(tower.IndexOf("BossCount.Line", StringComparison.Ordinal) >= 0
                  && tower.IndexOf("BossCount.SeedQaIfRequested", StringComparison.Ordinal) >= 0,
                "탑이 Line·시드를 읽는다");
            _ = nameof(BossCount.Begin);
            _ = nameof(BossCount.Fight);
            _ = nameof(BossCount.Of);

            Environment.SetEnvironmentVariable(BossCount.EnvShow, show);
            Environment.SetEnvironmentVariable(BossCount.EnvNo, no);
            BossCount.ResetForTest();
            GameState.ResetAll();

            if (_fail == 0) Debug.Log("[BossCountSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[BossCountSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[BossCountSelfCheck] FAIL {_fail}건");
        }
    }
}
