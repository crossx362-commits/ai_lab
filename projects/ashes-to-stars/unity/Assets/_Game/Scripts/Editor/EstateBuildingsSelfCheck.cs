using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 영지 대장간·영묘·탑이 전용 프랍을 읽는다. QA_NO면 옛 집·우물·등불(§16).
    /// </summary>
    public static class EstateBuildingsSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Estate Buildings Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(EstateBuildings.EnvShow);
            string no = Environment.GetEnvironmentVariable(EstateBuildings.EnvNo);
            Environment.SetEnvironmentVariable(EstateBuildings.EnvShow, null);
            Environment.SetEnvironmentVariable(EstateBuildings.EnvNo, null);
            EstateBuildings.ResetForTest();

            Check(EstateBuildings.DedicatedOf(EstateGrid.Cell.Smith) == EstateBuildings.Smith,
                "대장간 전용 이름");
            Check(EstateBuildings.DedicatedOf(EstateGrid.Cell.Mausoleum) == EstateBuildings.Mausoleum,
                "영묘 전용 이름");
            Check(EstateBuildings.DedicatedOf(EstateGrid.Cell.Arrow) == EstateBuildings.Tower,
                "화살탑 전용 이름");
            Check(EstateBuildings.DedicatedOf(EstateGrid.Cell.Magic) == EstateBuildings.Tower,
                "마법탑 전용 이름");
            Check(EstateBuildings.DedicatedOf(EstateGrid.Cell.Keep) == null, "본성은 전용 칸 아님");
            Check(EstateBuildings.OldOf(EstateGrid.Cell.Smith) == "village_house_2", "옛 대장간=작은 집");
            Check(EstateBuildings.OldOf(EstateGrid.Cell.Mausoleum) == "village_well_0", "옛 영묘=우물");
            Check(EstateBuildings.OldOf(EstateGrid.Cell.Arrow) == "village_lamp_0", "옛 탑=등불");

            Check(EstateBuildings.HasDedicated(EstateBuildings.Smith), "대장간 PNG");
            Check(EstateBuildings.HasDedicated(EstateBuildings.Mausoleum), "영묘 PNG");
            Check(EstateBuildings.HasDedicated(EstateBuildings.Tower), "탑 PNG");
            Check(EstateYard.PropOf(EstateGrid.Cell.Smith) == EstateBuildings.Smith,
                "마을이 대장간 전용을 읽는다");
            Check(EstateYard.PropOf(EstateGrid.Cell.Mausoleum) == EstateBuildings.Mausoleum,
                "마을이 영묘 전용을 읽는다");
            Check(EstateYard.PropOf(EstateGrid.Cell.Arrow) == EstateBuildings.Tower,
                "마을이 탑 전용을 읽는다");
            Check(EstateYard.PropOf(EstateGrid.Cell.Keep) == "village_house_1",
                "본성은 큰 집 유지");
            Check(EstateBuildings.Line().Contains("전용 그림"),
                $"줄 (실제 {EstateBuildings.Line()})");

            Environment.SetEnvironmentVariable(EstateBuildings.EnvNo, "1");
            Check(EstateBuildings.Blocked, "QA_NO면 차단");
            Check(EstateYard.PropOf(EstateGrid.Cell.Smith) == "village_house_2",
                "차단 대장간=작은 집");
            Check(EstateYard.PropOf(EstateGrid.Cell.Mausoleum) == "village_well_0",
                "차단 영묘=우물");
            Check(EstateYard.PropOf(EstateGrid.Cell.Arrow) == "village_lamp_0",
                "차단 탑=등불");
            Check(EstateBuildings.Line().Contains("집·우물·등불"),
                $"차단 줄 (실제 {EstateBuildings.Line()})");
            Environment.SetEnvironmentVariable(EstateBuildings.EnvNo, null);

            Environment.SetEnvironmentVariable(EstateBuildings.EnvShow, "1");
            EstateBuildings.SeedQaIfRequested();
            Check(EstateBuildings.ShowQa, "시드 켜짐");
            Check(EstateGrid.Count(EstateGrid.Cell.Arrow) + EstateGrid.Count(EstateGrid.Cell.Magic) >= 1,
                "시드가 탑 한 칸을 세운다");
            Check(EstateBuildings.Line().Contains("전용 그림"), "시드 줄");
            Environment.SetEnvironmentVariable(EstateBuildings.EnvShow, null);
            EstateBuildings.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string estate = File.ReadAllText(Path.Combine(runtime, "EstateScreen.cs"));
            string yard = File.ReadAllText(Path.Combine(runtime, "EstateYard.cs"));
            Check(yard.Contains("EstateBuildings.PropOf"), "마을이 PropOf를 읽는다");
            Check(estate.Contains("EstateBuildings.Line"), "자막이 Line을 읽는다");
            Check(estate.Contains("EstateBuildings.SeedQaIfRequested"), "시드를 읽는다");

            Environment.SetEnvironmentVariable(EstateBuildings.EnvShow, show);
            Environment.SetEnvironmentVariable(EstateBuildings.EnvNo, no);
            if (_fail == 0) Debug.Log("[EstateBuildingsSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[EstateBuildingsSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[EstateBuildingsSelfCheck] FAIL {_fail}건");
        }
    }
}
