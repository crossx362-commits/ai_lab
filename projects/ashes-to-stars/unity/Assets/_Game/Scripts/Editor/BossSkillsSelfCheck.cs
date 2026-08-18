using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>보스 스킬 수 기본 2 + 페이즈마다 1. QA_NO면 옛 ≤5/≤10(§10-5).</summary>
    public static class BossSkillsSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Boss Skills Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(BossSkills.EnvShow);
            string no = Environment.GetEnvironmentVariable(BossSkills.EnvNo);
            Environment.SetEnvironmentVariable(BossSkills.EnvShow, null);
            Environment.SetEnvironmentVariable(BossSkills.EnvNo, null);
            GameState.ResetAll();
            BossSkills.ResetForTest();

            Check(!BossSkills.Blocked, "기본은 켜짐");
            Check(BossSkills.PhaseCount(5) == 2 && BossSkills.FinalSkills(5) == 3,
                $"5층 2페이즈 최종 3 (실제 {BossSkills.PhaseCount(5)}/{BossSkills.FinalSkills(5)})");
            Check(BossSkills.PhaseCount(15) == 2 && BossSkills.Chain(2) == "2→3",
                $"15층 중간 2→3 (실제 {BossSkills.PhaseCount(15)} {BossSkills.Chain(BossSkills.PhaseCount(15))})");
            Check(BossSkills.PhaseCount(10) == 3 && BossSkills.FinalSkills(10) == 4,
                $"10층 3페이즈 최종 4 (실제 {BossSkills.PhaseCount(10)}/{BossSkills.FinalSkills(10)})");
            Check(BossSkills.PhaseCount(20) == 3 && BossSkills.Chain(3) == "2→3→4",
                $"20층 대보스 2→3→4 (실제 {BossSkills.PhaseCount(20)} {BossSkills.Chain(BossSkills.PhaseCount(20))})");
            Check(BossSkills.PhaseCount(40) == 3, $"40층은 아직 대보스 3 (실제 {BossSkills.PhaseCount(40)})");
            Check(BossSkills.PhaseCount(50) == 4 && BossSkills.FinalSkills(50) == 5,
                $"50층 4페이즈 최종 5 (실제 {BossSkills.PhaseCount(50)}/{BossSkills.FinalSkills(50)})");
            Check(BossSkills.PhaseCount(100) == 4 && BossSkills.Chain(4) == "2→3→4→5",
                $"100층 2→3→4→5 (실제 {BossSkills.Chain(BossSkills.PhaseCount(100))})");
            Check(BossSkills.SkillsAt(0) == 2 && BossSkills.SkillsAt(1) == 3
                  && BossSkills.SkillsAt(2) == 4 && BossSkills.SkillsAt(3) == 5,
                "페이즈 0~3 = 2·3·4·5");
            Check(BossSkills.OldPhaseCount(15) == 4 && BossSkills.OldChain(15) == "2→3→4→5",
                $"옛 15층은 4페이즈 (실제 {BossSkills.OldPhaseCount(15)} {BossSkills.OldChain(15)})");
            Check(BossSkills.FormatLine(15).IndexOf("2→3", StringComparison.Ordinal) >= 0
                  && BossSkills.FormatLine(15).IndexOf("§10-5", StringComparison.Ordinal) >= 0
                  && BossSkills.FormatLine(15).IndexOf("4→5", StringComparison.Ordinal) < 0,
                $"15층 줄 (실제 {BossSkills.FormatLine(15)})");
            Check(BossSkills.FormatLine(10).IndexOf("2→3→4", StringComparison.Ordinal) >= 0,
                $"10층 줄 (실제 {BossSkills.FormatLine(10)})");
            Check(UiAtlas.PhaseCountForFloor(15) == 2 && UiAtlas.PhaseCountForFloor(50) == 4,
                $"HP 바가 PhaseCount를 읽는다 (15={UiAtlas.PhaseCountForFloor(15)} 50={UiAtlas.PhaseCountForFloor(50)})");

            Environment.SetEnvironmentVariable(BossSkills.EnvNo, "1");
            Check(BossSkills.Blocked, "QA_NO");
            Check(BossSkills.PhaseCount(15) == 4 && BossSkills.PhaseCount(20) == 4,
                $"차단하면 15·20도 옛 4 (실제 {BossSkills.PhaseCount(15)}/{BossSkills.PhaseCount(20)})");
            Check(BossSkills.FormatLine(15).IndexOf("옛", StringComparison.Ordinal) >= 0,
                $"차단 줄 (실제 {BossSkills.FormatLine(15)})");
            Check(UiAtlas.PhaseCountForFloor(15) == 4, "차단하면 바도 옛 4");
            Environment.SetEnvironmentVariable(BossSkills.EnvNo, null);
            Check(BossSkills.PhaseCount(15) == 2, "차단을 풀면 다시 2");

            Environment.SetEnvironmentVariable(BossSkills.EnvShow, "1");
            GameState.ResetAll();
            BossSkills.ResetForTest();
            BossSkills.SeedQaIfRequested();
            Check(BossSkills.ShowQa, "시드 ShowQa");
            Check(GameState.TowerFloor == BossSkills.QaFloor,
                $"시드 15층 (실제 {GameState.TowerFloor})");
            Check(BossSkills.Line().IndexOf("2→3", StringComparison.Ordinal) >= 0
                  && BossSkills.Line().IndexOf("§10-5", StringComparison.Ordinal) >= 0,
                $"시드 줄 (실제 {BossSkills.Line()})");
            Environment.SetEnvironmentVariable(BossSkills.EnvShow, null);
            BossSkills.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string tower = File.ReadAllText(Path.Combine(runtime, "TowerScreen.cs"));
            string battle = File.ReadAllText(Path.Combine(runtime, "BossBattle.cs"));
            string atlas = File.ReadAllText(Path.Combine(runtime, "UiAtlas.cs"));
            Check(tower.IndexOf("BossSkills.Line", StringComparison.Ordinal) >= 0
                  && tower.IndexOf("BossSkills.SeedQaIfRequested", StringComparison.Ordinal) >= 0,
                "탑이 Line·시드를 읽는다");
            Check(battle.IndexOf("BossSkills.PhaseCount", StringComparison.Ordinal) >= 0,
                "CreateBosses가 PhaseCount를 읽는다");
            Check(battle.IndexOf("BossSkills.SkillsAt", StringComparison.Ordinal) >= 0,
                "페이즈가 SkillsAt을 읽는다");
            Check(atlas.IndexOf("BossSkills.PhaseCount", StringComparison.Ordinal) >= 0,
                "HP 바가 PhaseCount를 읽는다");
            _ = nameof(BossSkills.PhaseCount);
            _ = nameof(BossSkills.SkillsAt);
            _ = nameof(BossSkills.Line);
            _ = nameof(BossSkills.SeedQaIfRequested);

            Environment.SetEnvironmentVariable(BossSkills.EnvShow, show);
            Environment.SetEnvironmentVariable(BossSkills.EnvNo, no);
            BossSkills.ResetForTest();
            GameState.ResetAll();

            if (_fail == 0) Debug.Log("[BossSkillsSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[BossSkillsSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[BossSkillsSelfCheck] FAIL {_fail}건");
        }
    }
}
