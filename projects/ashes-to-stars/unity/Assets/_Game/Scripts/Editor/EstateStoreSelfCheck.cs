using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>침략 경로는 창고의 지금 칸. QA_NO면 옛 (3,3)(§13-3).</summary>
    public static class EstateStoreSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Estate Store Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(EstateStore.EnvShow);
            string no = Environment.GetEnvironmentVariable(EstateStore.EnvNo);
            Environment.SetEnvironmentVariable(EstateStore.EnvShow, null);
            Environment.SetEnvironmentVariable(EstateStore.EnvNo, null);

            GameState.ResetAll();
            EstateGrid.ResetForTest();
            EstateStore.ResetForTest();

            Check(!EstateStore.Blocked, "기본은 켜짐");
            Check(EstateStore.OriginX == EstateGrid.StoreX
                  && EstateStore.OriginY == EstateGrid.StoreY,
                "기본 원점은 (3,3)");
            Check(EstateStore.Reached(EstateGrid.StoreX, EstateGrid.StoreY),
                "기본 도착은 (3,3)");
            Check(EstateGrid.PathLength(EstateGrid.Side.북) == 3, "기본 북 3칸");
            Check(EstateGrid.PathLength(EstateGrid.Side.남) == 4, "기본 남 4칸");
            Check(EstateGrid.InvaderSide() == EstateGrid.Side.북, "기본 최단은 북");

            Check(EstateStore.TryMove(EstateStore.QaX, EstateStore.QaY), "창고를 (3,6)으로");
            Check(EstateGrid.At(EstateGrid.StoreX, EstateGrid.StoreY) == EstateGrid.Cell.Empty,
                "옛 (3,3)은 비었다");
            Check(EstateGrid.At(EstateStore.QaX, EstateStore.QaY) == EstateGrid.Cell.Warehouse,
                "지금 칸은 (3,6)");
            Check(EstateStore.OriginX == EstateStore.QaX
                  && EstateStore.OriginY == EstateStore.QaY,
                "원점이 (3,6)");
            Check(EstateStore.Reached(EstateStore.QaX, EstateStore.QaY)
                  && !EstateStore.Reached(EstateGrid.StoreX, EstateGrid.StoreY),
                "도착은 새 칸만");
            Check(EstateGrid.PathLength(EstateGrid.Side.북) == 6,
                $"옮기면 북 6칸 (실제 {EstateGrid.PathLength(EstateGrid.Side.북)})");
            Check(EstateGrid.PathLength(EstateGrid.Side.남) == 1,
                $"옮기면 남 1칸 (실제 {EstateGrid.PathLength(EstateGrid.Side.남)})");
            Check(EstateGrid.InvaderSide() == EstateGrid.Side.남, "최단이 남으로 바뀐다");
            Check(EstateStore.Line().IndexOf("(3,6)", StringComparison.Ordinal) >= 0
                  && EstateStore.Line().IndexOf("남", StringComparison.Ordinal) >= 0
                  && EstateStore.Line().IndexOf("§13-3", StringComparison.Ordinal) >= 0,
                $"줄 (실제 {EstateStore.Line()})");

            Check(!EstateStore.TryMove(1, 2), "본성 자리는 거부");
            Check(EstateStore.WhyCannotMove(1, 2) == "자리 크기가 겹친다",
                $"본성 거부 사유 (실제 {EstateStore.WhyCannotMove(1, 2)})");
            Check(!EstateStore.TryMove(-1, 0), "격자 밖은 거부");

            EstateGrid.ForgetInMemoryForTest();
            Check(EstateGrid.At(EstateStore.QaX, EstateStore.QaY) == EstateGrid.Cell.Warehouse,
                "재기동 뒤에도 (3,6)");
            Check(EstateGrid.PathLength(EstateGrid.Side.남) == 1, "재기동 뒤에도 남 1칸");

            GameState.ResetAll();
            EstateGrid.ResetForTest();
            EstateStore.ResetForTest();
            EstateStore.TryMove(EstateStore.QaX, EstateStore.QaY);
            Environment.SetEnvironmentVariable(EstateStore.EnvNo, "1");
            Check(EstateStore.Blocked, "QA_NO");
            Check(EstateStore.OriginX == EstateGrid.StoreX
                  && EstateStore.OriginY == EstateGrid.StoreY,
                "QA_NO면 옛 (3,3)");
            Check(EstateStore.Reached(EstateGrid.StoreX, EstateGrid.StoreY),
                "QA_NO 도착은 옛 칸");
            Check(EstateGrid.PathLength(EstateGrid.Side.북) == 3,
                $"QA_NO면 북도 옛 3칸 (실제 {EstateGrid.PathLength(EstateGrid.Side.북)})");
            Check(EstateGrid.InvaderSide() == EstateGrid.Side.북, "QA_NO면 옛 최단 북");
            Check(!EstateStore.TryMove(5, 6), "QA_NO면 이동 거부");
            Check(EstateStore.Line().IndexOf("고정", StringComparison.Ordinal) >= 0,
                $"QA_NO 줄 (실제 {EstateStore.Line()})");
            Environment.SetEnvironmentVariable(EstateStore.EnvNo, null);

            GameState.ResetAll();
            EstateGrid.ResetForTest();
            EstateStore.ResetForTest();
            Environment.SetEnvironmentVariable(EstateStore.EnvShow, "1");
            EstateStore.SeedQaIfRequested();
            Check(EstateStore.ShowQa, "시드 ShowQa");
            Check(EstateGrid.At(EstateStore.QaX, EstateStore.QaY) == EstateGrid.Cell.Warehouse,
                "시드 창고 (3,6)");
            Check(EstateGrid.InvaderSide() == EstateGrid.Side.남, "시드 최단 남");
            Check(EstateStore.Line().IndexOf("남", StringComparison.Ordinal) >= 0,
                $"시드 줄 (실제 {EstateStore.Line()})");
            Environment.SetEnvironmentVariable(EstateStore.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string grid = File.ReadAllText(Path.Combine(runtime, "EstateGrid.cs"));
            string estate = File.ReadAllText(Path.Combine(runtime, "EstateScreen.cs"));
            string world = File.ReadAllText(Path.Combine(runtime, "WorldMapScreen.cs"));
            int reached = 0;
            int i = 0;
            while ((i = grid.IndexOf("EstateStore.Reached", i, StringComparison.Ordinal)) >= 0)
            {
                reached++;
                i += 19;
            }
            Check(reached >= 2, $"PathLength·DistToStore가 Reached를 읽는다 ({reached})");
            Check(estate.IndexOf("EstateStore.SeedQaIfRequested", StringComparison.Ordinal) >= 0
                  && estate.IndexOf("EstateStore.Line", StringComparison.Ordinal) >= 0,
                "영지가 시드·줄을 읽는다");
            Check(world.IndexOf("EstateStore.SeedQaIfRequested", StringComparison.Ordinal) >= 0
                  && world.IndexOf("EstateStore.Line", StringComparison.Ordinal) >= 0,
                "월드맵이 시드·줄을 읽는다");

            _ = nameof(EstateStore.TryMove);
            _ = nameof(EstateStore.Reached);
            _ = nameof(EstateStore.Line);
            _ = nameof(EstateStore.SeedQaIfRequested);
            _ = nameof(EstateGrid.PathLength);

            Environment.SetEnvironmentVariable(EstateStore.EnvShow, show);
            Environment.SetEnvironmentVariable(EstateStore.EnvNo, no);
            EstateStore.ResetForTest();
            EstateGrid.ResetForTest();
            GameState.ResetAll();
            if (_fail == 0) Debug.Log("[EstateStoreSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[EstateStoreSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[EstateStoreSelfCheck] FAIL {_fail}건");
        }
    }
}
