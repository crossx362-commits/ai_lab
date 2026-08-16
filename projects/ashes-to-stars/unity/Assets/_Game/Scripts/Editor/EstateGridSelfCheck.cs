using System;
using System.Text;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>격자 8×8 — 성벽이 길을 늘리고 침략자는 가장 짧은 4면으로 들어온다(§13-3·§18-12).</summary>
    public static class EstateGridSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            Environment.SetEnvironmentVariable("QA_ESTATE_GRID", null);
            Environment.SetEnvironmentVariable("QA_NO_GRID", null);
            GameState.ResetAll();

            Check(EstateGrid.Size == 8, "초기 격자는 8×8(§18-12)");
            Check(EstateGrid.At(EstateGrid.KeepX, EstateGrid.KeepY) == EstateGrid.Cell.Keep,
                "본성은 (2,3)");
            Check(EstateGrid.At(EstateGrid.StoreX, EstateGrid.StoreY) == EstateGrid.Cell.Warehouse,
                "창고는 중앙 (3,3)");
            Check(EstateGrid.At(EstateGrid.MineX, EstateGrid.MineY) == EstateGrid.Cell.Mine,
                "광산은 (5,3)");
            Check(EstateGrid.At(EstateGrid.SmithX, EstateGrid.SmithY) == EstateGrid.Cell.Smith,
                "대장간이 마을에 있다");
            Check(EstateGrid.At(EstateGrid.AuctionX, EstateGrid.AuctionY) == EstateGrid.Cell.Auction,
                "경매장이 마을에 있다");
            Check(!EstateGrid.TryPickUp(EstateGrid.SmithX, EstateGrid.SmithY),
                "대장간은 거둘 수 없다");
            Check(EstateGrid.IsHub(EstateGrid.Cell.Barracks), "수비대는 허브 건물이다");
            Check(EstateGrid.PathLength(EstateGrid.Side.북) == 3, "열린 북 진입은 3칸");
            Check(EstateGrid.PathLength(EstateGrid.Side.서) == 3, "열린 서 진입은 3칸");
            Check(EstateGrid.PathLength(EstateGrid.Side.남) == 4, "열린 남 진입은 4칸");
            Check(EstateGrid.PathLength(EstateGrid.Side.동) == 4, "열린 동 진입은 4칸");
            Check(EstateGrid.InvaderSide() == EstateGrid.Side.북,
                "동률이면 북·동·남·서 순으로 고른다");
            Check(EstateGrid.InvaderPath() == 3, "침략 경로는 최단 3칸");

            Check(!EstateGrid.TryPlace(3, 1, EstateGrid.Cell.Wall),
                "성벽 레벨 0이면 벽을 못 놓는다");
            EstateDefense.SetLevelForTest(EstateDefense.Kind.성벽, 1);
            Check(EstateGrid.Unplaced(EstateGrid.Cell.Wall) == 1, "성벽 1레벨 = 놓을 벽 1");
            Check(EstateGrid.TryPlace(3, 1, EstateGrid.Cell.Wall), "성벽 1이면 한 칸 놓인다");
            Check(EstateGrid.At(3, 1) == EstateGrid.Cell.Wall, "벽이 (3,1)에 있다");
            Check(EstateGrid.PathLength(EstateGrid.Side.북) > 3, "북 길이 3칸이 늘어난다");
            Check(EstateGrid.Unplaced(EstateGrid.Cell.Wall) == 0, "벽을 다 썼다");
            Check(!EstateGrid.TryPlace(4, 1, EstateGrid.Cell.Wall), "레벨을 넘는 벽은 거부");

            Check(EstateGrid.TryPickUp(3, 1), "놓은 벽을 거둔다");
            Check(EstateGrid.At(3, 1) == EstateGrid.Cell.Empty, "거두면 빈 칸");
            Check(!EstateGrid.TryPickUp(EstateGrid.StoreX, EstateGrid.StoreY),
                "창고는 거둘 수 없다");
            Check(!EstateGrid.TryPlace(EstateGrid.KeepX, EstateGrid.KeepY, EstateGrid.Cell.Wall),
                "본성 칸에는 못 놓는다");

            EstateDefense.SetLevelForTest(EstateDefense.Kind.성벽, 20);
            EstateDefense.SetLevelForTest(EstateDefense.Kind.화살탑, 1);
            Check(EstateGrid.TryPlace(0, 0, EstateGrid.Cell.Arrow), "화살탑은 가장자리에도 놓인다");
            Check(EstateGrid.At(0, 0) == EstateGrid.Cell.Arrow, "화살탑은 칸에 남는다");
            int northWithTower = EstateGrid.PathLength(EstateGrid.Side.북);
            Check(northWithTower == 3, "화살탑은 길을 막지 않는다");
            Check(EstateGrid.TryPickUp(0, 0), "화살탑도 거둔다");

            SealThreeSides();
            Check(EstateGrid.PathLength(EstateGrid.Side.북) < 0, "북은 막힘");
            Check(EstateGrid.PathLength(EstateGrid.Side.동) < 0, "동은 막힘");
            Check(EstateGrid.PathLength(EstateGrid.Side.서) < 0, "서는 막힘");
            Check(EstateGrid.PathLength(EstateGrid.Side.남) > 0, "남만 열려 있다");
            Check(EstateGrid.InvaderSide() == EstateGrid.Side.남, "막힌 3면이면 남으로 들어온다");
            EstateDefense.SetLevelForTest(EstateDefense.Kind.성벽,
                EstateGrid.Count(EstateGrid.Cell.Wall) + 1);
            Check(!EstateGrid.TryPlace(3, 5, EstateGrid.Cell.Wall),
                "마지막 면을 막으면 거부 — 만능 배치는 없다(§13-3)");
            Check(EstateGrid.PathLength(EstateGrid.Side.남) > 0, "거부 뒤에도 남은 면이 있다");

            EstateGrid.ResetForTest();
            EstateDefense.SetLevelForTest(EstateDefense.Kind.성벽, 3);
            Check(EstateGrid.TryPlace(2, 1, EstateGrid.Cell.Wall), "저장 검사 벽1");
            Check(EstateGrid.TryPlace(3, 1, EstateGrid.Cell.Wall), "저장 검사 벽2");
            Check(EstateGrid.TryPlace(4, 1, EstateGrid.Cell.Wall), "저장 검사 벽3");
            int savedNorth = EstateGrid.PathLength(EstateGrid.Side.북);
            var savedSide = EstateGrid.InvaderSide();
            Check(savedNorth > 3, "북 3칸 벽이 길을 늘린다");
            Check(savedSide == EstateGrid.Side.서 || savedSide == EstateGrid.Side.남
                  || savedSide == EstateGrid.Side.동,
                "북을 막으면 다른 면이 최단이다");
            EstateGrid.ForgetInMemoryForTest();
            Check(EstateGrid.At(2, 1) == EstateGrid.Cell.Wall
                  && EstateGrid.At(3, 1) == EstateGrid.Cell.Wall
                  && EstateGrid.At(4, 1) == EstateGrid.Cell.Wall,
                "재기동 뒤에도 벽이 남는다");
            Check(EstateGrid.PathLength(EstateGrid.Side.북) == savedNorth, "경로 길이도 유지");
            Check(EstateGrid.InvaderSide() == savedSide, "진입 면도 유지");

            GameState.ResetAll();
            GameState.SetTowerFloorForTest(30);
            GameState.Grant(1_000_000);
            EstateGrid.ResetForTest();
            EstateDefense.SetLevelForTest(EstateDefense.Kind.성벽, 3);
            EstateGrid.TryPlace(2, 1, EstateGrid.Cell.Wall);
            EstateGrid.TryPlace(3, 1, EstateGrid.Cell.Wall);
            EstateGrid.TryPlace(4, 1, EstateGrid.Cell.Wall);
            var preview = EstateGrid.InvaderSide();
            Check(InvasionState.TryBegin(), "침략이 시작된다");
            Check(InvasionState.ApproachSide == preview,
                "출정 순간 가장 짧은 면을 고른다(§13-3)");
            InvasionState.Settle(false);

            GameState.ResetAll();
            EstateGrid.ResetForTest();
            Environment.SetEnvironmentVariable("QA_NO_GRID", "1");
            EstateDefense.SetLevelForTest(EstateDefense.Kind.성벽, 1);
            Check(!EstateGrid.TryPlace(3, 1, EstateGrid.Cell.Wall), "QA_NO_GRID면 배치 거부");
            Check(EstateGrid.At(3, 1) == EstateGrid.Cell.Empty, "거부면 칸 불변");
            Environment.SetEnvironmentVariable("QA_NO_GRID", null);

            GameState.ResetAll();
            EstateGrid.ResetForTest();
            EstateGrid.SeedQaIfRequested();
            Check(EstateGrid.InvaderSide() == EstateGrid.Side.북, "시드 없으면 기본 북");
            Environment.SetEnvironmentVariable("QA_ESTATE_GRID", "1");
            EstateGrid.SeedQaIfRequested();
            Check(EstateGrid.At(2, 1) == EstateGrid.Cell.Wall
                  && EstateGrid.At(3, 1) == EstateGrid.Cell.Wall
                  && EstateGrid.At(4, 1) == EstateGrid.Cell.Wall,
                "QA_ESTATE_GRID=1은 북 3칸을 막는다");
            Check(EstateGrid.InvaderSide() != EstateGrid.Side.북, "시드 뒤 북이 최단이 아니다");
            Check(EstateGrid.InvaderPath() > 0, "시드 뒤에도 진입은 있다");
            Environment.SetEnvironmentVariable("QA_ESTATE_GRID", null);

            _ = nameof(EstateGrid.TryPlace);
            _ = nameof(EstateGrid.TryPickUp);
            _ = nameof(EstateGrid.PathLength);
            _ = nameof(EstateGrid.InvaderSide);
            _ = nameof(EstateGrid.InvaderPath);
            _ = nameof(EstateGrid.SeedQaIfRequested);
            _ = nameof(InvasionState.ApproachSide);

            GameState.ResetAll();
            EstateGrid.ResetForTest();
            if (_fail > 0)
            {
                Debug.LogError("[EstateGridSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("EstateGridSelfCheck FAIL " + _fail);
            }
            Debug.Log("[EstateGridSelfCheck] PASS\n" + _log);
        }

        static void SealThreeSides()
        {
            EstateGrid.ResetForTest();
            EstateDefense.SetLevelForTest(EstateDefense.Kind.성벽, 24);
            for (int x = 0; x < EstateGrid.Size; x++)
            {
                if (EstateGrid.At(x, 1) == EstateGrid.Cell.Empty)
                    EstateGrid.SetCellForTest(x, 1, EstateGrid.Cell.Wall);
            }
            for (int y = 0; y < EstateGrid.Size; y++)
            {
                if (EstateGrid.At(1, y) == EstateGrid.Cell.Empty)
                    EstateGrid.SetCellForTest(1, y, EstateGrid.Cell.Wall);
                if (EstateGrid.At(6, y) == EstateGrid.Cell.Empty)
                    EstateGrid.SetCellForTest(6, y, EstateGrid.Cell.Wall);
            }
            // 남은 남쪽을 폭 1(x=3)로 좁혀, (3,5) 한 칸이 마지막 문이 되게 한다.
            for (int y = 4; y < EstateGrid.Size; y++)
            {
                if (EstateGrid.At(2, y) == EstateGrid.Cell.Empty)
                    EstateGrid.SetCellForTest(2, y, EstateGrid.Cell.Wall);
                if (EstateGrid.At(4, y) == EstateGrid.Cell.Empty)
                    EstateGrid.SetCellForTest(4, y, EstateGrid.Cell.Wall);
                if (EstateGrid.At(5, y) == EstateGrid.Cell.Empty)
                    EstateGrid.SetCellForTest(5, y, EstateGrid.Cell.Wall);
            }
        }
    }
}
