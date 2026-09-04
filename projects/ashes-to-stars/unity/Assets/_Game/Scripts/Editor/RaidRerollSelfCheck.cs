using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>하위 레이드 재입장 ×1·×2·×4·×8. QA_NO면 매번 1배(§18-2).</summary>
    public static class RaidRerollSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Raid Reroll Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(RaidReroll.EnvShow);
            string no = Environment.GetEnvironmentVariable(RaidReroll.EnvNo);
            Environment.SetEnvironmentVariable(RaidReroll.EnvShow, null);
            Environment.SetEnvironmentVariable(RaidReroll.EnvNo, null);

            GameState.ResetAll();
            RaidReroll.ResetForTest();
            RaidScale.ResetForTest();
            RacePrefs.Set(RaceId.인간);

            Check(RaidReroll.WindowSeconds == 24 * 3600, "창 24시간");
            Check(Mathf.Approximately(Economy.GetRerollCostMultiplier(0), 1f), "이전0=×1");
            Check(Mathf.Approximately(Economy.GetRerollCostMultiplier(1), 2f), "이전1=×2");
            Check(Mathf.Approximately(Economy.GetRerollCostMultiplier(2), 4f), "이전2=×4");
            Check(Mathf.Approximately(Economy.GetRerollCostMultiplier(3), 8f), "이전3=×8");

            GameState.SetTowerFloorForTest(5);
            Check(!RaidReroll.Applies(5), "첫 5층은 누진 없음");
            Check(RaidReroll.Cost(5) == 1000, $"첫 5층 1000 (실제 {RaidReroll.Cost(5)})");
            Check(RaidReroll.NextAttempt() == 1, "첫 층은 회차 1");
            RaidReroll.Record(5);
            Check(RaidReroll.NextAttempt() == 1, "첫 클리어 Record는 안 센다");
            Check(RaidReroll.Line() == "", "10층 전엔 문구 없음");

            GameState.SetTowerFloorForTest(11);
            Check(RaidReroll.Applies(5), "11층에서 5층은 하위");
            Check(RaidReroll.Applies(10), "11층에서 10층도 이미 깬 레이드");
            RaidReroll.Record(10);
            Check(RaidReroll.NextAttempt() == 1, "첫 10층 카드 Record는 안 센다");
            Check(RaidReroll.NextAttempt() == 1, "시작 1회차");
            Check(RaidReroll.Multiplier() == 1, "1회차 ×1");
            Check(RaidReroll.Cost(5) == 1600, $"1회차 T2 1600 (실제 {RaidReroll.Cost(5)})");
            Check(RaidReroll.FormatLine(5).Contains("×1") && RaidReroll.FormatLine(5).Contains("§18-2")
                  && RaidReroll.FormatLine(5).Contains("16실버"),
                $"1회차 문구 (실제 {RaidReroll.FormatLine(5)})");

            RaidReroll.Record(5);
            Check(RaidReroll.NextAttempt() == 2, "정산 뒤 2회차");
            Check(RaidReroll.Multiplier() == 2, "2회차 ×2");
            Check(RaidReroll.Cost(5) == 3200, $"2회차 3200 (실제 {RaidReroll.Cost(5)})");
            Check(RaidReroll.FormatLine(5).Contains("×2") && RaidReroll.FormatLine(5).Contains("32실버"),
                $"2회차 문구 (실제 {RaidReroll.FormatLine(5)})");

            RaidReroll.ForgetInMemoryForTest();
            Check(RaidReroll.NextAttempt() == 2, "재기동 뒤에도 2회차");
            Check(RaidReroll.Cost(5) == 3200, "재기동 뒤에도 3200");

            RaidReroll.Record(5);
            Check(RaidReroll.Cost(5) == 6400, $"3회차 6400 (실제 {RaidReroll.Cost(5)})");
            RaidReroll.Record(5);
            Check(RaidReroll.Multiplier() == 8, "4회차 ×8");
            Check(RaidReroll.Cost(5) == 12800, $"4회차 12800 (실제 {RaidReroll.Cost(5)})");
            RaidReroll.Record(5);
            Check(RaidReroll.Cost(5) == 12800, $"5회차도 12800 (실제 {RaidReroll.Cost(5)})");

            long t0 = 1_700_000_000;
            RaidReroll.NowUnix = () => t0;
            RaidReroll.ResetForTest();
            RaidReroll.NowUnix = () => t0;
            GameState.SetTowerFloorForTest(11);
            RaidReroll.Record(5);
            RaidReroll.NowUnix = () => t0 + RaidReroll.WindowSeconds;
            Check(RaidReroll.NextAttempt() == 2, "24시간 정각은 아직 2회차");
            Check(RaidReroll.Cost(5) == 3200, "24시간 정각은 3200");
            RaidReroll.NowUnix = () => t0 + RaidReroll.WindowSeconds + 1;
            Check(RaidReroll.NextAttempt() == 1, "24시간+1초면 1회차");
            Check(RaidReroll.Cost(5) == 1600, "24시간+1초면 1600");

            GameState.ResetAll();
            RaidReroll.ResetForTest();
            RacePrefs.Set(RaceId.드워프);
            GameState.SetTowerFloorForTest(11);
            Check(RaidReroll.Cost(5) == 1280, $"드워프 1회차 1280 (실제 {RaidReroll.Cost(5)})");
            RaidReroll.Record(5);
            Check(RaidReroll.Cost(5) == 2560, $"드워프 2회차 2560 (실제 {RaidReroll.Cost(5)})");

            GameState.ResetAll();
            RaidReroll.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            GameState.SetTowerFloorForTest(51);
            Check(RaidReroll.Cost(5) == 10485, $"51층 1회차 10485 (실제 {RaidReroll.Cost(5)})");
            RaidReroll.Record(5);
            Check(RaidReroll.Cost(5) == 20970, $"51층 2회차 20970 (실제 {RaidReroll.Cost(5)})");

            Environment.SetEnvironmentVariable(RaidReroll.EnvNo, "1");
            Check(RaidReroll.Blocked, "QA_NO면 차단");
            Check(RaidReroll.Cost(5) == 10485, $"차단하면 10485 (실제 {RaidReroll.Cost(5)})");
            RaidReroll.Record(5);
            Check(RaidReroll.NextAttempt() == 1, "차단하면 Record 안 함");
            Check(RaidReroll.FormatLine(5).Contains("없음"),
                $"차단 문구 (실제 {RaidReroll.FormatLine(5)})");
            Environment.SetEnvironmentVariable(RaidReroll.EnvNo, null);
            Check(RaidReroll.Cost(5) == 20970, "차단을 풀면 다시 20970");

            Environment.SetEnvironmentVariable(RaidReroll.EnvShow, "1");
            GameState.ResetAll();
            RaidReroll.ResetForTest();
            RaidReroll.SeedQaIfRequested();
            Check(GameState.TowerFloor == 11, $"시드는 11층 (실제 {GameState.TowerFloor})");
            Check(RaidReroll.NextAttempt() == 2, "시드는 2회차");
            Check(RaidReroll.Cost(5) == 3200, $"시드 비용 3200 (실제 {RaidReroll.Cost(5)})");
            Check(RaidReroll.Line().Contains("×2") && RaidReroll.Line().Contains("32실버"),
                $"시드 문구 (실제 {RaidReroll.Line()})");
            Environment.SetEnvironmentVariable(RaidReroll.EnvShow, null);

            string tower = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/TowerScreen.cs"));
            Check(tower.Contains("RaidReroll.Cost") && tower.Contains("RaidReroll.Record")
                  && tower.Contains("RaidReroll.Line") && tower.Contains("RaidReroll.SeedQaIfRequested"),
                "TowerScreen이 Cost·Record·Line·Seed를 읽는다");
            string econ = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/Economy.cs"));
            Check(econ.Contains("GetRerollCostMultiplier"),
                "Economy에 GetRerollCostMultiplier가 있다");
            string impl = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/RaidReroll.cs"));
            Check(impl.Contains("Economy.GetRerollCostMultiplier") && impl.Contains("Apply("),
                "RaidReroll이 배수와 Apply를 읽는다");

            _ = nameof(RaidReroll.Cost);
            _ = nameof(RaidReroll.Record);
            _ = nameof(RaidReroll.Line);
            _ = nameof(RaidReroll.SeedQaIfRequested);
            _ = nameof(Economy.GetRerollCostMultiplier);

            Environment.SetEnvironmentVariable(RaidReroll.EnvShow, show);
            Environment.SetEnvironmentVariable(RaidReroll.EnvNo, no);
            RaidReroll.ResetForTest();
            GameState.ResetAll();

            if (_fail > 0)
            {
                Debug.LogError("[RaidRerollSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("RaidRerollSelfCheck FAIL " + _fail);
            }
            Debug.Log("[RaidRerollSelfCheck] PASS\n" + _log);
        }
    }
}
