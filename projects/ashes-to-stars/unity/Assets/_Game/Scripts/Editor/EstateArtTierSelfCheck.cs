using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 영지 티어 아트(_0/_1/_2) + 공사판. PropOf가 레벨 구간을 고른다(§6).
    /// </summary>
    public static class EstateArtTierSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static readonly string[] Bases =
        {
            "estate_keep", "estate_mine", "estate_warehouse", "estate_barracks",
            "estate_smith", "estate_mausoleum", "estate_tower", "estate_auction",
        };

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Estate Art Tier Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string no = Environment.GetEnvironmentVariable(EstateBuildings.EnvNo);
            Environment.SetEnvironmentVariable(EstateBuildings.EnvNo, null);
            EstateBuildings.ResetForTest();
            EstateBuild.ResetForTest();

            Check(EstateBuildings.ArtTierOf(1) == 0, "Lv1→티어0");
            Check(EstateBuildings.ArtTierOf(4) == 0, "Lv4→티어0");
            Check(EstateBuildings.ArtTierOf(5) == 1, "Lv5→티어1");
            Check(EstateBuildings.ArtTierOf(9) == 1, "Lv9→티어1");
            Check(EstateBuildings.ArtTierOf(10) == 2, "Lv10→티어2");
            Check(EstateBuildings.ArtTierOf(13) == 2, "Lv13→티어2");
            Check(EstateBuildings.Scaffold == "estate_scaffold_0", "공사판 이름");

            foreach (string b in Bases)
            {
                for (int t = 0; t <= 2; t++)
                    Check(EstateBuildings.HasDedicated(b + "_" + t), b + "_" + t + " PNG");
            }
            Check(EstateBuildings.HasDedicated(EstateBuildings.Scaffold), "scaffold PNG");
            string yardSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/EstateYard.cs"));
            Check(yardSrc.IndexOf("DrawScaffoldIfBusy", StringComparison.Ordinal) >= 0
                  && yardSrc.IndexOf("ScaffoldOf", StringComparison.Ordinal) >= 0,
                "EstateYard가 DrawScaffoldIfBusy로 estate_scaffold_0을 소비한다");
            string screenSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/EstateScreen.cs"));
            Check(screenSrc.IndexOf("SeedArtDoneQaIfRequested", StringComparison.Ordinal) >= 0,
                "영지가 공사완료 시드를 읽는다");

            EstateBuild.SetLevelForTest(3);
            Check(EstateBuildings.PropOf(EstateGrid.Cell.Keep) == "estate_keep_0"
                  || EstateBuildings.PropOf(EstateGrid.Cell.Keep) == "estate_keep_1"
                  || EstateBuildings.PropOf(EstateGrid.Cell.Keep) == "estate_keep_2",
                "본성 PropOf 전용");
            Check(EstateBuildings.PropOf(EstateGrid.Cell.Keep) == "estate_keep_0",
                "Lv3→estate_keep_0");

            EstateBuild.SetLevelForTest(7);
            string p7 = EstateBuildings.PropOf(EstateGrid.Cell.Keep);
            Check(p7 == "estate_keep_1" || p7 == "estate_keep_0",
                $"Lv7→_1(없으면 _0) 실제={p7}");
            if (EstateBuildings.HasDedicated("estate_keep_1"))
                Check(p7 == "estate_keep_1", "Lv7·_1있음→estate_keep_1");

            EstateBuild.SetLevelForTest(12);
            string p12 = EstateBuildings.PropOf(EstateGrid.Cell.Keep);
            Check(p12 == "estate_keep_2" || p12 == "estate_keep_1" || p12 == "estate_keep_0",
                $"Lv12→_2폴백 실제={p12}");
            if (EstateBuildings.HasDedicated("estate_keep_2"))
                Check(p12 == "estate_keep_2", "Lv12·_2있음→estate_keep_2");

            // Busy + 티어 시드(시각 QA와 동일) → PropOf 구간 + scaffold Resources
            string artEnv = Environment.GetEnvironmentVariable("QA_ESTATE_ART_TIERS");
            Environment.SetEnvironmentVariable("QA_ESTATE_ART_TIERS", "1");
            EstateBuild.ResetForTest();
            EstateBuild.SeedArtTierQaIfRequested();
            Check(EstateBuildings.PropOf(EstateGrid.Cell.Keep) == "estate_keep_0",
                "시드 Keep Lv3→estate_keep_0");
            string pm = EstateBuildings.PropOf(EstateGrid.Cell.Mine);
            Check(pm == "estate_mine_1" || pm == "estate_mine_0",
                $"시드 Mine Lv7→_1 실제={pm}");
            if (EstateBuildings.HasDedicated("estate_mine_1"))
                Check(pm == "estate_mine_1", "시드 Mine·_1있음→estate_mine_1");
            string pw = EstateBuildings.PropOf(EstateGrid.Cell.Warehouse);
            Check(pw == "estate_warehouse_2" || pw == "estate_warehouse_1" || pw == "estate_warehouse_0",
                $"시드 Warehouse Lv12→_2 실제={pw}");
            if (EstateBuildings.HasDedicated("estate_warehouse_2"))
                Check(pw == "estate_warehouse_2", "시드 Warehouse·_2있음→estate_warehouse_2");
            Check(EstateBuild.Busy(EstateGrid.Cell.Barracks), "시드 Barracks Busy");
            Check(EstateBuildings.ScaffoldOf(EstateGrid.Cell.Barracks) == EstateBuildings.Scaffold,
                "Busy→ScaffoldOf=estate_scaffold_0");
            string pb = EstateBuildings.PropOf(EstateGrid.Cell.Barracks);
            Check(pb != null && pb.StartsWith("estate_barracks_", StringComparison.Ordinal),
                $"Busy 중 PropOf는 티어(오버레이) 실제={pb}");
            Check(EstateBuildings.ScaffoldOf(EstateGrid.Cell.Keep) == null,
                "비Busy Keep→ScaffoldOf null");
            Environment.SetEnvironmentVariable("QA_ESTATE_ART_TIERS", null);

            Environment.SetEnvironmentVariable("QA_ESTATE_ART_DONE", "1");
            EstateBuild.ResetForTest();
            EstateBuild.SeedArtDoneQaIfRequested();
            Check(!EstateBuild.Busy(EstateGrid.Cell.Barracks), "완료 시드 Barracks 비Busy");
            Check(EstateBuildings.ScaffoldOf(EstateGrid.Cell.Barracks) == null,
                "완료→ScaffoldOf null (비계 사라짐)");
            Check(EstateBuild.Level(EstateGrid.Cell.Barracks) == 7, "완료 Barracks Lv7");
            string pd = EstateBuildings.PropOf(EstateGrid.Cell.Barracks);
            Check(pd == "estate_barracks_1" || pd == "estate_barracks_0",
                $"완료 PropOf 티어 PNG 실제={pd}");
            if (EstateBuildings.HasDedicated("estate_barracks_1"))
                Check(pd == "estate_barracks_1", "완료·_1있음→estate_barracks_1");
            Environment.SetEnvironmentVariable("QA_ESTATE_ART_DONE", null);
            Environment.SetEnvironmentVariable("QA_ESTATE_ART_TIERS", artEnv);

            Environment.SetEnvironmentVariable(EstateBuildings.EnvNo, "1");
            Check(EstateBuildings.PropOf(EstateGrid.Cell.Keep) == "village_house_1",
                "QA_NO→옛 집");
            Check(EstateBuildings.ScaffoldOf(EstateGrid.Cell.Barracks) == null,
                "QA_NO→scaffold 끔");
            Environment.SetEnvironmentVariable(EstateBuildings.EnvNo, no);

            EstateBuild.ResetForTest();
            EstateBuildings.ResetForTest();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "estate_art_tier_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS EstateArtTierSelfCheck" : "FAIL EstateArtTierSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[EstateArtTierSelfCheck] PASS → " + path);
            else Debug.LogError("[EstateArtTierSelfCheck] FAIL " + _fail + " → " + path);
        }
    }
}
