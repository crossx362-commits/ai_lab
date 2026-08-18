using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 자리 크기를 격자가 소유하고 밑동은 마름모 중심(GAME_SPEC_ESTATE_BUILD §2-1).
    /// QA_NO_ESTATE_FOOTPRINT면 옛 sit=0.42.
    /// </summary>
    public static class EstateFootprintSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Estate Footprint Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string no = Environment.GetEnvironmentVariable(EstateYard.EnvNoFootprint);
            string noYard = Environment.GetEnvironmentVariable(EstateYard.EnvNo);
            string noBld = Environment.GetEnvironmentVariable(EstateBuildings.EnvNo);
            Environment.SetEnvironmentVariable(EstateYard.EnvNoFootprint, null);
            Environment.SetEnvironmentVariable(EstateYard.EnvNo, null);
            Environment.SetEnvironmentVariable(EstateBuildings.EnvNo, null);
            EstateYard.ResetForTest();
            EstateBuildings.ResetForTest();
            EstateGrid.ResetForTest();

            Check(EstateGrid.FootprintOf(EstateGrid.Cell.Keep) == new Vector2Int(2, 2), "본성 2×2");
            Check(EstateGrid.FootprintOf(EstateGrid.Cell.Warehouse) == new Vector2Int(2, 1), "창고 2×1");
            Check(EstateGrid.FootprintOf(EstateGrid.Cell.Mine) == new Vector2Int(2, 1), "광산 2×1");
            Check(EstateGrid.FootprintOf(EstateGrid.Cell.Barracks) == new Vector2Int(2, 1), "수비대 2×1");
            Check(EstateGrid.FootprintOf(EstateGrid.Cell.Smith) == new Vector2Int(1, 1), "대장간 1×1");
            Check(EstateGrid.FootprintOf(EstateGrid.Cell.Auction) == new Vector2Int(1, 1), "경매장 1×1");
            Check(EstateGrid.FootprintOf(EstateGrid.Cell.Mausoleum) == new Vector2Int(1, 1), "영묘 1×1");
            Check(EstateGrid.FootprintOf(EstateGrid.Cell.Wall) == new Vector2Int(1, 1), "성벽 1×1");

            Check(EstateGrid.Fits(EstateGrid.KeepX, EstateGrid.KeepY, EstateGrid.Cell.Keep),
                "본성이 격자 안에 있다");
            Check(EstateGrid.Fits(EstateGrid.StoreX, EstateGrid.StoreY, EstateGrid.Cell.Warehouse),
                "창고가 격자 안에 있다");
            Check(EstateGrid.Fits(EstateGrid.MineX, EstateGrid.MineY, EstateGrid.Cell.Mine),
                "광산이 격자 안에 있다");
            Check(EstateGrid.Fits(EstateGrid.BarracksX, EstateGrid.BarracksY, EstateGrid.Cell.Barracks),
                "수비대가 격자 안에 있다");

            var seen = new HashSet<int>();
            int cores = 0;
            for (int y = 0; y < EstateGrid.Size; y++)
            for (int x = 0; x < EstateGrid.Size; x++)
            {
                var c = EstateGrid.At(x, y);
                if (!EstateGrid.IsCore(c)) continue;
                cores++;
                var f = EstateGrid.FootprintOf(c);
                for (int dy = 0; dy < f.y; dy++)
                for (int dx = 0; dx < f.x; dx++)
                {
                    int cx = x + dx;
                    int cy = y + dy;
                    Check(EstateGrid.InBounds(cx, cy), $"{c} 자리 ({cx},{cy})가 격자 안");
                    int key = cy * EstateGrid.Size + cx;
                    Check(seen.Add(key), $"{c}가 ({cx},{cy})를 겹쳐 덮지 않는다");
                    Check(EstateGrid.Covers(x, y, cx, cy), $"{c}가 ({cx},{cy})를 덮는다");
                }
            }
            Check(cores == 7, $"허브·생산 7동 (실제 {cores})");

            Check(EstateGrid.TryOwner(2, 3, out int ox, out int oy)
                  && ox == EstateGrid.KeepX && oy == EstateGrid.KeepY,
                "본성 여분 칸의 주인은 본성");
            Check(EstateGrid.CoveredByCore(2, 2), "본성 (2,2)는 자리");
            EstateDefense.SetLevelForTest(EstateDefense.Kind.성벽, 1);
            Check(!EstateGrid.TryPlace(2, 2, EstateGrid.Cell.Wall),
                "본성 자리 위에 벽을 못 놓는다");
            Check(EstateGrid.WhyCannotPlace(2, 2, EstateGrid.Cell.Wall) == "자리 크기가 겹친다",
                "거부 사유는 자리 겹침");

            float tw = 80f, th = 41.6f;
            var p = new Vector2(100f, 200f);
            var smith = EstateYard.BuildingBox(p, tw, th, EstateGrid.Cell.Smith);
            Check(Mathf.Abs(smith.center.x - (p.x + tw * 0.5f)) < 0.05f,
                $"1×1 밑동 x {smith.center.x:0.0} = 마름모 중심");
            Check(Mathf.Abs(smith.yMax - (p.y + th * 0.5f)) < 0.05f,
                $"1×1 밑동 y {smith.yMax:0.0} = 마름모 중심");

            var keep = EstateYard.BuildingBox(p, tw, th, EstateGrid.Cell.Keep);
            Check(Mathf.Abs(keep.center.x - (p.x + tw * 0.5f)) < 0.05f,
                $"2×2 밑동 x {keep.center.x:0.0} = 네 칸 중심");
            Check(Mathf.Abs(keep.yMax - (p.y + th)) < 0.05f,
                $"2×2 밑동 y {keep.yMax:0.0} = 네 칸 중심");
            Check(Mathf.Abs(keep.width - tw * 2f) < 0.05f, $"본성 폭 {keep.width:0} = 2칸");

            var store = EstateYard.BuildingBox(p, tw, th, EstateGrid.Cell.Warehouse);
            Check(Mathf.Abs(store.center.x - (p.x + tw * 0.75f)) < 0.05f,
                $"2×1 밑동 x {store.center.x:0.0} = 두 칸 중심");
            Check(Mathf.Abs(store.yMax - (p.y + th * 0.75f)) < 0.05f,
                $"2×1 밑동 y {store.yMax:0.0} = 두 칸 중심");

            Environment.SetEnvironmentVariable(EstateYard.EnvNoFootprint, "1");
            var old = EstateYard.BuildingBox(p, tw, th, EstateGrid.Cell.Smith);
            Check(Mathf.Abs(old.yMax - (p.y + th * 0.42f)) < 0.05f,
                $"차단 밑동 y {old.yMax:0.0} = sit 0.42");
            Check(Mathf.Abs(old.yMax - (p.y + th * 0.5f)) > 1f,
                "차단하면 중심보다 위에 앉는다");
            Environment.SetEnvironmentVariable(EstateYard.EnvNoFootprint, null);

            var cells = new[]
            {
                EstateGrid.Cell.Keep, EstateGrid.Cell.Mine, EstateGrid.Cell.Warehouse,
                EstateGrid.Cell.Barracks, EstateGrid.Cell.Smith, EstateGrid.Cell.Auction,
                EstateGrid.Cell.Mausoleum, EstateGrid.Cell.Arrow,
            };
            foreach (var c in cells)
            {
                string prop = EstateYard.PropOf(c);
                string want = EstateBuildings.DedicatedOf(c);
                Check(prop == want, $"{c} 전용 {want}");
                Check(EstateBuildings.HasDedicated(want), $"{want} PNG");
            }
            Check(string.IsNullOrEmpty(EstateBuildings.LastFallback),
                "폴백이 없다");

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string yard = File.ReadAllText(Path.Combine(runtime, "EstateYard.cs"));
            string grid = File.ReadAllText(Path.Combine(runtime, "EstateGrid.cs"));
            string bld = File.ReadAllText(Path.Combine(runtime, "EstateBuildings.cs"));
            Check(yard.Contains("FootprintOf") && yard.Contains("EnvNoFootprint"),
                "BuildingBox가 자리 크기를 읽는다");
            Check(!yard.Contains("float sit = cell == EstateGrid.Cell.Wall")
                  || yard.Contains("OldBuildingBox"),
                "sit 상수는 옛 길에만 있다");
            Check(grid.Contains("CoveredByCore") && grid.Contains("TryOwner"),
                "격자가 자리 겹침을 본다");
            Check(bld.Contains("LogWarning") && bld.Contains("전용 그림 없음"),
                "폴백 경고가 있다");

            Environment.SetEnvironmentVariable(EstateYard.EnvNoFootprint, no);
            Environment.SetEnvironmentVariable(EstateYard.EnvNo, noYard);
            Environment.SetEnvironmentVariable(EstateBuildings.EnvNo, noBld);
            EstateYard.ResetForTest();
            EstateBuildings.ResetForTest();
            EstateGrid.ResetForTest();
            if (_fail == 0) Debug.Log("[EstateFootprintSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[EstateFootprintSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[EstateFootprintSelfCheck] FAIL {_fail}건");
        }
    }
}
