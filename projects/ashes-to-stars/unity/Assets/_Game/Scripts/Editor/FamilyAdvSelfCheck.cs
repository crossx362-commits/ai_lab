using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>계열 상성 ×1.3 / ×0.7이 던전 제목에 보인다. QA_NO면 옛 계열 제목(§10-3).</summary>
    public static class FamilyAdvSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Family Adv Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(FamilyAdv.EnvShow);
            string no = Environment.GetEnvironmentVariable(FamilyAdv.EnvNo);
            Environment.SetEnvironmentVariable(FamilyAdv.EnvShow, null);
            Environment.SetEnvironmentVariable(FamilyAdv.EnvNo, null);

            GameState.ResetAll();
            FamilyAdv.ResetForTest();
            DungeonRun.End();

            Check(!FamilyAdv.Blocked, "기본은 켜짐");
            Check(Mathf.Approximately(FamilyAdv.Mul("마법사", MobFamily.야수), FamilyAdv.Strong),
                $"야수+마법사 {FamilyAdv.Mul("마법사", MobFamily.야수):0.0} = 1.3");
            Check(Mathf.Approximately(FamilyAdv.Mul("정령사", MobFamily.야수), FamilyAdv.Strong),
                $"야수+정령사 {FamilyAdv.Mul("정령사", MobFamily.야수):0.0} = 1.3");
            Check(Mathf.Approximately(FamilyAdv.Mul("궁수", MobFamily.야수), FamilyAdv.Weak),
                $"야수+궁수 {FamilyAdv.Mul("궁수", MobFamily.야수):0.0} = 0.7");
            Check(Mathf.Approximately(FamilyAdv.Mul("검사", MobFamily.야수), 1f),
                $"야수+검사 {FamilyAdv.Mul("검사", MobFamily.야수):0.0} = 1");
            Check(Mathf.Approximately(FamilyAdv.Mul("마딜", MobFamily.야수), FamilyAdv.Strong),
                $"야수+마딜 {FamilyAdv.Mul("마딜", MobFamily.야수):0.0} = 1.3");
            Check(Mathf.Approximately(FamilyAdv.Mul("딜", MobFamily.야수), FamilyAdv.Weak),
                $"야수+딜 {FamilyAdv.Mul("딜", MobFamily.야수):0.0} = 0.7");
            Check(Mathf.Approximately(FamilyAdv.Mul("마법사", MobFamily.마족), FamilyAdv.Weak),
                $"마족+마법사 {FamilyAdv.Mul("마법사", MobFamily.마족):0.0} = 0.7");
            Check(Mathf.Approximately(FamilyAdv.Mul("검사", MobFamily.기계), FamilyAdv.Weak),
                $"기계+검사 {FamilyAdv.Mul("검사", MobFamily.기계):0.0} = 0.7");
            Check(Mathf.Approximately(FamilyAdv.Mul("광전사", MobFamily.정령), FamilyAdv.Weak),
                $"정령+광전사 {FamilyAdv.Mul("광전사", MobFamily.정령):0.0} = 0.7");
            Check(Mathf.Approximately(FamilyAdv.Mul("사제", MobFamily.언데드), FamilyAdv.Strong),
                $"언데드+사제 {FamilyAdv.Mul("사제", MobFamily.언데드):0.0} = 1.3");

            string title = FamilyAdv.Title(MobFamily.야수);
            Check(title.IndexOf("야수", StringComparison.Ordinal) >= 0
                  && title.IndexOf("마법사", StringComparison.Ordinal) >= 0
                  && title.IndexOf("×1.3", StringComparison.Ordinal) >= 0,
                $"Title (실제 {title})");
            string old = FamilyAdv.OldTitle(MobFamily.야수);
            Check(old == "던전 · 야수 계열", $"OldTitle (실제 {old})");
            Check(FamilyAdv.Line(MobFamily.야수).IndexOf("§10-3", StringComparison.Ordinal) >= 0,
                $"Line (실제 {FamilyAdv.Line(MobFamily.야수)})");

            Environment.SetEnvironmentVariable(FamilyAdv.EnvNo, "1");
            Check(FamilyAdv.Blocked, "QA_NO");
            Check(Mathf.Approximately(FamilyAdv.Mul("마법사", MobFamily.야수), 1f),
                "차단하면 배율 1");
            Check(FamilyAdv.Title(MobFamily.야수) == old, "차단하면 옛 계열 제목");
            Environment.SetEnvironmentVariable(FamilyAdv.EnvNo, null);

            Environment.SetEnvironmentVariable(FamilyAdv.EnvShow, "1");
            FamilyAdv.ResetForTest();
            DungeonRun.End();
            FamilyAdv.SeedQaIfRequested();
            Check(FamilyAdv.ShowQa, "시드 ShowQa");
            Check(DungeonRun.Active, "시드 던전");
            Check(DungeonRun.Active && DungeonRun.Plan.Family == MobFamily.야수,
                $"시드 계열 (실제 {(DungeonRun.Active ? DungeonRun.Plan.Family.ToString() : "없음")})");
            Check(FamilyAdv.Title().IndexOf("×1.3", StringComparison.Ordinal) >= 0,
                $"시드 제목 (실제 {FamilyAdv.Title()})");
            Environment.SetEnvironmentVariable(FamilyAdv.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string mapSrc = File.ReadAllText(Path.Combine(runtime, "DungeonScreen.cs"));
            Check(mapSrc.IndexOf("FamilyAdv.Title", StringComparison.Ordinal) >= 0,
                "던전 지도가 Title을 읽는다");
            Check(mapSrc.IndexOf("FamilyAdv.SeedQaIfRequested", StringComparison.Ordinal) >= 0,
                "던전 지도가 시드를 읽는다");
            Check(mapSrc.IndexOf("FamilyAdv.Line", StringComparison.Ordinal) >= 0,
                "던전 지도가 Line을 읽는다");

            _ = nameof(FamilyAdv.Mul);
            _ = nameof(FamilyAdv.Title);
            _ = nameof(FamilyAdv.Line);
            _ = nameof(FamilyAdv.SeedQaIfRequested);
            _ = nameof(FamilyAdv.PartyMul);

            Environment.SetEnvironmentVariable(FamilyAdv.EnvShow, show);
            Environment.SetEnvironmentVariable(FamilyAdv.EnvNo, no);
            FamilyAdv.ResetForTest();
            DungeonRun.End();
            GameState.ResetAll();

            if (_fail == 0) Debug.Log("[FamilyAdvSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[FamilyAdvSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[FamilyAdvSelfCheck] FAIL {_fail}건");
        }
    }
}
