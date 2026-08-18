using System;

namespace AshesToStars
{
    /// <summary>
    /// 침략 경로는 창고의 지금 칸을 본다(GAME_SPEC_ESTATE_BUILD §2-2 · §13-3).
    /// 옛 경로는 EstateGrid.StoreX/StoreY 고정. QA_NO면 옛 상수.
    /// 드래그 UX는 이 칸 아님 — TryMove만 연다. EstateBuild는 안 만진다.
    /// </summary>
    public static class EstateStore
    {
        public const string EnvShow = "QA_ESTATE_STORE";
        public const string EnvNo = "QA_NO_ESTATE_STORE";
        public const int QaX = 3, QaY = 6;

        static bool _qaSeeded;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool ShowQa
        {
            get
            {
                if (Blocked) return false;
                string raw = Environment.GetEnvironmentVariable(EnvShow);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>경로의 도착 칸. QA_NO면 옛 (3,3).</summary>
        public static bool TryOrigin(out int x, out int y)
        {
            if (Blocked)
            {
                x = EstateGrid.StoreX;
                y = EstateGrid.StoreY;
                return true;
            }
            return TryFind(out x, out y);
        }

        public static int OriginX
        {
            get
            {
                TryOrigin(out int x, out _);
                return x;
            }
        }

        public static int OriginY
        {
            get
            {
                TryOrigin(out _, out int y);
                return y;
            }
        }

        public static bool Reached(int x, int y)
        {
            if (!TryOrigin(out int ox, out int oy)) return false;
            return x == ox && y == oy;
        }

        public static bool TryFind(out int x, out int y)
        {
            for (int iy = 0; iy < EstateGrid.Size; iy++)
            for (int ix = 0; ix < EstateGrid.Size; ix++)
            {
                if (EstateGrid.At(ix, iy) != EstateGrid.Cell.Warehouse) continue;
                x = ix;
                y = iy;
                return true;
            }
            x = EstateGrid.StoreX;
            y = EstateGrid.StoreY;
            return false;
        }

        public static string WhyCannotMove(int nx, int ny)
        {
            if (Blocked) return "창고 이동이 꺼져 있다";
            if (!TryFind(out int ox, out int oy)) return "창고가 없다";
            if (ox == nx && oy == ny) return null;
            if (!EstateGrid.InBounds(nx, ny)) return "격자 밖이다";
            var f = EstateGrid.FootprintOf(EstateGrid.Cell.Warehouse);
            if (nx + f.x > EstateGrid.Size || ny + f.y > EstateGrid.Size)
                return "격자 밖이다";
            for (int y = ny; y < ny + f.y; y++)
            for (int x = nx; x < nx + f.x; x++)
            {
                if (!EstateGrid.TryOwner(x, y, out int px, out int py)) continue;
                if (px == ox && py == oy) continue;
                return "자리 크기가 겹친다";
            }
            return null;
        }

        public static bool TryMove(int nx, int ny)
        {
            if (WhyCannotMove(nx, ny) != null) return false;
            if (!TryFind(out int ox, out int oy)) return false;
            if (ox == nx && oy == ny) return true;
            EstateGrid.SetCellForTest(ox, oy, EstateGrid.Cell.Empty);
            EstateGrid.SetCellForTest(nx, ny, EstateGrid.Cell.Warehouse);
            return EstateGrid.At(nx, ny) == EstateGrid.Cell.Warehouse;
        }

        public static string Line()
        {
            if (Blocked) return "창고는 고정 (3,3)";
            int n = EstateGrid.PathLength(EstateGrid.InvaderSide());
            return $"창고 ({OriginX},{OriginY}) · {EstateGrid.InvaderSide()} {n}칸(§13-3)";
        }

        public static void SeedQaIfRequested()
        {
            if (!ShowQa || _qaSeeded) return;
            _qaSeeded = true;
            EstateGrid.ResetForTest();
            TryMove(QaX, QaY);
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
        }
    }
}
