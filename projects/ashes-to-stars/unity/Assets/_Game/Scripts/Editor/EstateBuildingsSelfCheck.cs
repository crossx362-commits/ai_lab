using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 영지 기능 건물 전용 프랍. 경매장은 estate_auction_0. QA_NO면 옛 수레(§16).
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

            Check(EstateBuildings.DedicatedOf(EstateGrid.Cell.Keep) == EstateBuildings.Keep,
                "본성 전용 이름");
            Check(EstateBuildings.DedicatedOf(EstateGrid.Cell.Mine) == EstateBuildings.Mine,
                "광산 전용 이름");
            Check(EstateBuildings.DedicatedOf(EstateGrid.Cell.Warehouse) == EstateBuildings.Warehouse,
                "창고 전용 이름");
            Check(EstateBuildings.DedicatedOf(EstateGrid.Cell.Barracks) == EstateBuildings.Barracks,
                "수비대 전용 이름");
            Check(EstateBuildings.DedicatedOf(EstateGrid.Cell.Smith) == EstateBuildings.Smith,
                "대장간 전용 이름");
            Check(EstateBuildings.DedicatedOf(EstateGrid.Cell.Mausoleum) == EstateBuildings.Mausoleum,
                "영묘 전용 이름");
            Check(EstateBuildings.DedicatedOf(EstateGrid.Cell.Arrow) == EstateBuildings.Tower,
                "화살탑 전용 이름");
            Check(EstateBuildings.DedicatedOf(EstateGrid.Cell.Magic) == EstateBuildings.Tower,
                "마법탑 전용 이름");
            Check(EstateBuildings.DedicatedOf(EstateGrid.Cell.Auction) == EstateBuildings.Auction,
                "경매장 전용 이름");
            Check(EstateBuildings.OldOf(EstateGrid.Cell.Keep) == "village_house_1", "옛 본성=큰 집");
            Check(EstateBuildings.OldOf(EstateGrid.Cell.Mine) == "village_barn_0", "옛 광산=헛간");
            Check(EstateBuildings.OldOf(EstateGrid.Cell.Warehouse) == "village_house_0", "옛 창고=집");
            Check(EstateBuildings.OldOf(EstateGrid.Cell.Barracks) == "village_barn_0", "옛 수비대=헛간");

            Check(EstateBuildings.HasDedicated(EstateBuildings.Keep), "본성 PNG");
            Check(EstateBuildings.HasDedicated(EstateBuildings.Mine), "광산 PNG");
            Check(EstateBuildings.HasDedicated(EstateBuildings.Warehouse), "창고 PNG");
            Check(EstateBuildings.HasDedicated(EstateBuildings.Barracks), "수비대 PNG");
            Check(EstateBuildings.HasDedicated(EstateBuildings.Auction), "경매장 PNG");
            Check(EstateYard.PropOf(EstateGrid.Cell.Keep) == EstateBuildings.Keep,
                "마을이 본성 전용을 읽는다");
            Check(EstateYard.PropOf(EstateGrid.Cell.Mine) == EstateBuildings.Mine,
                "마을이 광산 전용을 읽는다");
            Check(EstateYard.PropOf(EstateGrid.Cell.Warehouse) == EstateBuildings.Warehouse,
                "마을이 창고 전용을 읽는다");
            Check(EstateYard.PropOf(EstateGrid.Cell.Barracks) == EstateBuildings.Barracks,
                "마을이 수비대 전용을 읽는다");
            Check(EstateYard.PropOf(EstateGrid.Cell.Smith) == EstateBuildings.Smith,
                "마을이 대장간 전용을 읽는다");
            Check(EstateYard.PropOf(EstateGrid.Cell.Auction) == EstateBuildings.Auction,
                "마을이 경매장 전용을 읽는다");
            Check(EstateBuildings.Line().Contains("경매장") && EstateBuildings.Line().Contains("전용 그림"),
                $"줄 (실제 {EstateBuildings.Line()})");

            Environment.SetEnvironmentVariable(EstateBuildings.EnvNo, "1");
            Check(EstateBuildings.Blocked, "QA_NO면 차단");
            Check(EstateYard.PropOf(EstateGrid.Cell.Keep) == "village_house_1",
                "차단 본성=큰 집");
            Check(EstateYard.PropOf(EstateGrid.Cell.Mine) == "village_barn_0",
                "차단 광산=헛간");
            Check(EstateYard.PropOf(EstateGrid.Cell.Warehouse) == "village_house_0",
                "차단 창고=집");
            Check(EstateYard.PropOf(EstateGrid.Cell.Barracks) == "village_barn_0",
                "차단 수비대=헛간");
            Check(EstateYard.PropOf(EstateGrid.Cell.Auction) == "village_cart_0",
                "차단 경매장=수레");
            Check(EstateBuildings.Line().Contains("수레"),
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
            string buildings = File.ReadAllText(Path.Combine(runtime, "EstateBuildings.cs"));
            Check(buildings.Contains("Cell.Auction => Auction"),
                "DedicatedOf가 경매장을 읽는다");

            Environment.SetEnvironmentVariable(EstateBuildings.EnvShow, show);
            Environment.SetEnvironmentVariable(EstateBuildings.EnvNo, no);
            if (_fail == 0) Debug.Log("[EstateBuildingsSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[EstateBuildingsSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[EstateBuildingsSelfCheck] FAIL {_fail}건");
        }
    }
}
