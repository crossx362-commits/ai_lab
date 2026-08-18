using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>탑 도크 레이드·하위 레이드 부제는 한 줄. QA_NO면 옛 긴 줄(§16).</summary>
    public static class TowerDockCapSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Tower Dock Cap Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(TowerDockCap.EnvShow);
            string no = Environment.GetEnvironmentVariable(TowerDockCap.EnvNo);
            Environment.SetEnvironmentVariable(TowerDockCap.EnvShow, null);
            Environment.SetEnvironmentVariable(TowerDockCap.EnvNo, null);

            GameState.ResetAll();
            DeathTraining.ResetForTest();
            RaidReroll.ResetForTest();
            RaidScale.ResetForTest();
            RaidBossPool.ResetForTest();
            TowerDockCap.ResetForTest();
            RaidScale.ForceScalePercent = 65;

            Check(!TowerDockCap.Blocked, "기본은 켜짐");
            Check(DeathTraining.IsTraining, "1층은 훈련");
            Check(TowerDockCap.Raid(5) == TowerDockCap.RaidTrain,
                $"훈련 부제 (실제 {TowerDockCap.Raid(5)})");
            Check(TowerDockCap.CaptionFits(TowerDockCap.Raid(5)),
                $"훈련 길이 {TowerDockCap.RuneCount(TowerDockCap.Raid(5))} ≤ {TowerDockCap.CaptionMaxRunes}");
            Check(!TowerDockCap.CaptionFits(TowerDockCap.OldRaid(5)),
                $"옛 훈련 줄은 안 맞음 (길이 {TowerDockCap.RuneCount(TowerDockCap.OldRaid(5))})");

            DeathTraining.Consent();
            GameState.SetTowerFloorForTest(6);
            Check(TowerDockCap.Raid(5) == TowerDockCap.RaidMid,
                $"5층 부제 (실제 {TowerDockCap.Raid(5)})");
            Check(TowerDockCap.Raid(10) == TowerDockCap.RaidMega,
                $"대보스 부제 (실제 {TowerDockCap.Raid(10)})");
            Check(TowerDockCap.CaptionFits(TowerDockCap.Raid(5))
                  && TowerDockCap.CaptionFits(TowerDockCap.Raid(10)),
                "5층·대보스 길이");
            Check(!TowerDockCap.CaptionFits(TowerDockCap.OldRaid(5))
                  && !TowerDockCap.CaptionFits(TowerDockCap.OldRaid(10)),
                $"옛 레이드 줄은 안 맞음 (5={TowerDockCap.RuneCount(TowerDockCap.OldRaid(5))} 10={TowerDockCap.RuneCount(TowerDockCap.OldRaid(10))})");

            GameState.SetTowerFloorForTest(1);
            RaidReroll.ResetForTest();
            RaidBossPool.ResetForTest();
            Check(TowerDockCap.Lower(5) == "×1 · 0.65",
                $"1층대 풀 없음 (실제 {TowerDockCap.Lower(5)})");

            GameState.SetTowerFloorForTest(51);
            GameState.TrySelectTier(4);
            RaidReroll.Record(RaidScale.LowerRaidFloor);
            string cap = TowerDockCap.Lower(5);
            Check(cap == "×2 · 10종 · 0.65",
                $"하위 부제 (실제 {cap})");
            Check(TowerDockCap.CaptionFits(cap),
                $"길이 {TowerDockCap.RuneCount(cap)} ≤ {TowerDockCap.CaptionMaxRunes}");
            Check(!TowerDockCap.CaptionFits(TowerDockCap.OldLower(5)),
                $"옛 줄은 안 맞음 (길이 {TowerDockCap.RuneCount(TowerDockCap.OldLower(5))})");
            Check(TowerDockCap.Line().IndexOf("한 줄", StringComparison.Ordinal) >= 0,
                $"줄 (실제 {TowerDockCap.Line()})");

            Environment.SetEnvironmentVariable(TowerDockCap.EnvNo, "1");
            Check(TowerDockCap.Blocked, "QA_NO");
            string old = TowerDockCap.Lower(5);
            Check(old == TowerDockCap.OldLower(5) && !TowerDockCap.CaptionFits(old),
                $"QA_NO 옛 긴 줄 (실제 {old})");
            string oldRaid = TowerDockCap.Raid(10);
            Check(oldRaid == TowerDockCap.OldRaid(10) && !TowerDockCap.CaptionFits(oldRaid),
                $"QA_NO 옛 레이드 줄 (실제 {oldRaid})");
            Check(TowerDockCap.Line().IndexOf("두 줄", StringComparison.Ordinal) >= 0,
                $"QA_NO 줄 (실제 {TowerDockCap.Line()})");
            Environment.SetEnvironmentVariable(TowerDockCap.EnvNo, null);

            TowerDockCap.ResetForTest();
            RaidReroll.ResetForTest();
            Environment.SetEnvironmentVariable(TowerDockCap.EnvShow, "1");
            Check(TowerDockCap.ShowQa, "시드 ShowQa");
            TowerDockCap.SeedQaIfRequested();
            Check(GameState.TowerFloor == 51, $"시드 층 (실제 {GameState.TowerFloor})");
            Check(RaidScale.LowerFloor == 5, $"시드 하위 (실제 {RaidScale.LowerFloor})");
            Check(RaidReroll.NextAttempt() >= 2, $"시드 2회차 (실제 {RaidReroll.NextAttempt()})");
            Check(TowerDockCap.Lower(5) == "×2 · 10종 · 0.65",
                $"시드 부제 (실제 {TowerDockCap.Lower(5)})");
            Check(TowerDockCap.Line().IndexOf("한 줄", StringComparison.Ordinal) >= 0,
                $"시드 자막 (실제 {TowerDockCap.Line()})");
            Environment.SetEnvironmentVariable(TowerDockCap.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string towerSrc = File.ReadAllText(Path.Combine(runtime, "TowerScreen.cs"));
            Check(towerSrc.IndexOf("TowerDockCap.SeedQaIfRequested", StringComparison.Ordinal) >= 0
                  && towerSrc.IndexOf("TowerDockCap.Line", StringComparison.Ordinal) >= 0
                  && towerSrc.IndexOf("TowerDockCap.Lower", StringComparison.Ordinal) >= 0
                  && towerSrc.IndexOf("TowerDockCap.Raid", StringComparison.Ordinal) >= 0,
                "탑이 시드·줄·Lower·Raid를 읽는다");
            Check(towerSrc.IndexOf("RaidReroll.FormatLine(lower) + \" · \" + RaidBossPool.Line()",
                      StringComparison.Ordinal) < 0
                  && towerSrc.IndexOf("RaidScale.FormatLine(lower)", StringComparison.Ordinal) < 0
                  && towerSrc.IndexOf("5층마다 보스, 10층 단위는 대보스", StringComparison.Ordinal) < 0,
                "도크가 옛 긴 줄을 안 붙인다");

            _ = nameof(TowerDockCap.Lower);
            _ = nameof(TowerDockCap.Raid);
            _ = nameof(TowerDockCap.Line);
            _ = nameof(TowerDockCap.SeedQaIfRequested);

            Environment.SetEnvironmentVariable(TowerDockCap.EnvShow, show);
            Environment.SetEnvironmentVariable(TowerDockCap.EnvNo, no);
            RaidScale.ForceScalePercent = -1;
            TowerDockCap.ResetForTest();
            RaidReroll.ResetForTest();
            RaidScale.ResetForTest();
            RaidBossPool.ResetForTest();
            DeathTraining.ResetForTest();
            GameState.ResetAll();

            if (_fail == 0) Debug.Log("[TowerDockCapSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[TowerDockCapSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[TowerDockCapSelfCheck] FAIL {_fail}건");
        }
    }
}
