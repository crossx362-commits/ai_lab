using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>10층 대보스 입장 0.15 G/h. QA_NO면 5층 요금(§18-2).</summary>
    public static class RaidCostSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Raid Cost Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(RaidCost.EnvShow);
            string no = Environment.GetEnvironmentVariable(RaidCost.EnvNo);
            Environment.SetEnvironmentVariable(RaidCost.EnvShow, null);
            Environment.SetEnvironmentVariable(RaidCost.EnvNo, null);

            GameState.ResetAll();
            RaidCost.ResetForTest();
            RaidReroll.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            GameState.SetTowerFloorForTest(10);

            Check(RaidCost.IsMega(10) && RaidCost.IsMega(20) && RaidCost.IsMega(100),
                "10·20·100은 대보스");
            Check(!RaidCost.IsMega(5) && !RaidCost.IsMega(15) && !RaidCost.IsMega(0),
                "5·15·0은 중간 레이드");
            Check(RaidCost.ActionKey(5) == RaidCost.MidKey, "5층은 Tower5BossRaid");
            Check(RaidCost.ActionKey(10) == RaidCost.MegaKey, "10층은 Tower10Boss");
            Check(RaidCost.Copper(5) == 1000, $"T1 5층 1000 (실제 {RaidCost.Copper(5)})");
            Check(RaidCost.Copper(10) == 1500, $"T1 10층 1500 (실제 {RaidCost.Copper(10)})");
            Check(RaidCost.Copper(15) == 1000, $"T1 15층 1000 (실제 {RaidCost.Copper(15)})");
            Check(RaidCost.FormatLine(10).Contains("15실버") && RaidCost.FormatLine(10).Contains("§18-2"),
                $"T1 문구 15실버 (실제 {RaidCost.FormatLine(10)})");
            Check(RaidCost.Line().Contains("15실버"),
                $"현재 10층 자막 15실버 (실제 {RaidCost.Line()})");
            Check(string.IsNullOrEmpty(RaidCost.FormatLine(5)), "5층 문구 없음");

            GameState.SetTowerFloorForTest(20);
            Check(RaidCost.Copper(10) == 2400, $"T2 10층 2400 (실제 {RaidCost.Copper(10)})");
            Check(RaidCost.Copper(5) == 1600, $"T2 5층 1600 (실제 {RaidCost.Copper(5)})");
            Check(RaidCost.FormatLine(10).Contains("24실버"),
                $"T2 문구 24실버 (실제 {RaidCost.FormatLine(10)})");

            GameState.ResetAll();
            RaidCost.ResetForTest();
            RacePrefs.Set(RaceId.드워프);
            GameState.SetTowerFloorForTest(10);
            Check(RaidCost.Copper(10) == 1200, $"드워프 10층 1200 (실제 {RaidCost.Copper(10)})");
            Check(RaidCost.Copper(5) == 800, $"드워프 5층 800 (실제 {RaidCost.Copper(5)})");

            GameState.ResetAll();
            RaidCost.ResetForTest();
            RaidReroll.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            GameState.SetTowerFloorForTest(11);
            Check(RaidReroll.Cost(5) == 1600, $"하위 5층 1600 유지 (실제 {RaidReroll.Cost(5)})");
            Check(RaidReroll.Cost(10) == 2400, $"하위 10층 첫 회차 2400 (실제 {RaidReroll.Cost(10)})");

            Environment.SetEnvironmentVariable(RaidCost.EnvNo, "1");
            GameState.ResetAll();
            RaidCost.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            GameState.SetTowerFloorForTest(10);
            Check(RaidCost.Blocked, "QA_NO면 차단");
            Check(RaidCost.ActionKey(10) == RaidCost.MidKey, "차단하면 10층도 5층 키");
            Check(RaidCost.Copper(10) == 1000, $"차단하면 10층 1000 (실제 {RaidCost.Copper(10)})");
            Check(RaidCost.FormatLine(10).Contains("없음"),
                $"차단 문구 (실제 {RaidCost.FormatLine(10)})");
            Environment.SetEnvironmentVariable(RaidCost.EnvNo, null);
            Check(RaidCost.Copper(10) == 1500, "차단을 풀면 다시 1500");

            Environment.SetEnvironmentVariable(RaidCost.EnvShow, "1");
            GameState.ResetAll();
            RaidCost.ResetForTest();
            RaidCost.SeedQaIfRequested();
            Check(GameState.TowerFloor == 10, $"시드는 10층 (실제 {GameState.TowerFloor})");
            Check(RaidCost.Copper(10) == 1500, $"시드 비용 1500 (실제 {RaidCost.Copper(10)})");
            Check(RaidCost.Line().Contains("15실버"),
                $"시드 문구 (실제 {RaidCost.Line()})");
            Check(GameState.Wallet.Copper >= 1500, "시드가 입장 골드를 넣는다");
            Environment.SetEnvironmentVariable(RaidCost.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string tower = File.ReadAllText(Path.Combine(runtime, "TowerScreen.cs"));
            string reroll = File.ReadAllText(Path.Combine(runtime, "RaidReroll.cs"));
            string econ = File.ReadAllText(Path.Combine(runtime, "Economy.cs"));
            Check(tower.Contains("RaidCost.Copper") && tower.Contains("RaidCost.Line")
                  && tower.Contains("RaidCost.SeedQaIfRequested"),
                "TowerScreen이 Copper·Line·Seed를 읽는다");
            Check(reroll.Contains("RaidCost.Copper"),
                "RaidReroll.Cost가 RaidCost.Copper를 읽는다");
            Check(econ.Contains("\"Tower10Boss\""),
                "Economy에 Tower10Boss 0.15가 있다");

            _ = nameof(RaidCost.Copper);
            _ = nameof(RaidCost.ActionKey);
            _ = nameof(RaidCost.Line);
            _ = nameof(RaidCost.SeedQaIfRequested);

            string raidSrc = File.ReadAllText(Path.Combine(runtime, "RaidCost.cs"));
            Check(raidSrc.Contains("ShortCopper(Copper(floor))")
                  && raidSrc.IndexOf("FormatCurrency(Copper(floor))") < 0,
                "대보스 입장 비용은 ShortCopper만");

            Environment.SetEnvironmentVariable(RaidCost.EnvShow, show);
            Environment.SetEnvironmentVariable(RaidCost.EnvNo, no);
            RaidCost.ResetForTest();
            RaidReroll.ResetForTest();
            GameState.ResetAll();

            if (_fail == 0) Debug.Log("[RaidCostSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[RaidCostSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[RaidCostSelfCheck] FAIL {_fail}건");
        }
    }
}
