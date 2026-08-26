using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 수직 슬라이스 — 격자 8×8(§13-3·§18-12).
    /// 성벽이 길을 막고, 침략자는 4면 중 가장 짧은 길로 창고에 온다.
    /// 침략 전투 시뮬은 약탈 %만이라 경로 길이를 약탈 공식에 넣지 않는다.
    /// 만능 배치(4면 전부 봉쇄)는 마지막 벽을 거부한다.
    /// </summary>
    public static class EstateGrid
    {
        // 초기 격자 폭(§18-12). 부지 확장 전 기본.
        public const int BaseSize = 8;
        // 부지 확장 최대 폭(§18-12). 저장·인덱싱은 항상 이 폭으로 고정한다(Stride).
        public const int MaxSize = 16;
        // 물리 배열의 행 간격 — 논리 격자가 커져도 저장 배치가 안 흔들리게 최대 폭 고정.
        const int Stride = MaxSize;

        // 논리 격자 폭(해금된 크기). 소비처: EstateExpansion.CurrentSize(층 기반).
        // 렌더 핫패스에서 자주 불리니 호출부는 `int n = Size;`로 한 번만 받아 쓴다.
        public static int Size => EstateExpansion.CurrentSize();

        // (x,y) → 물리 배열 인덱스. 논리 폭이 아니라 고정 Stride로 잡아야 저장이 안 깨진다.
        static int Idx(int x, int y) => y * Stride + x;
        // 2×2 본성이 (2,3)이면 창고 (3,3)을 덮는다. 자리는 (1,2)에서 연다.
        public const int KeepX = 1, KeepY = 2;
        public const int StoreX = 3, StoreY = 3;
        public const int MineX = 5, MineY = 3;
        public const int SmithX = 0, SmithY = 6;
        public const int AuctionX = 7, AuctionY = 6;
        public const int MausoleumX = 0, MausoleumY = 4;
        // 2×1 수비대가 (7,4)면 한 칸이 격자 밖이다. 자리는 (6,4)에서 연다.
        public const int BarracksX = 6, BarracksY = 4;

        // 뒤에만 붙인다. 저장이 int라 가운데 끼우면 옛 세이브가 벽을 대장간으로 읽는다.
        public enum Cell
        {
            Empty, Keep, Mine, Warehouse, Arrow, Magic, Wall, Trap,
            Smith, Auction, Mausoleum, Barracks,
        }
        public enum Side { 북, 동, 남, 서 }

        public static readonly Side[] Sides = { Side.북, Side.동, Side.남, Side.서 };

        const string K_CELLS = "ats.estate.grid";

        static bool _loaded;
        static bool _qaSeeded;
        static readonly Cell[] _cells = new Cell[Stride * Stride];

        public static bool Disabled()
        {
            string raw = Environment.GetEnvironmentVariable("QA_NO_GRID");
            return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
        }

        public static Cell At(int x, int y)
        {
            Load();
            if (!InBounds(x, y)) return Cell.Wall;
            return _cells[Idx(x, y)];
        }

        public static bool InBounds(int x, int y)
        {
            int n = Size;
            return x >= 0 && y >= 0 && x < n && y < n;
        }

        public static bool IsCore(Cell c) =>
            c == Cell.Keep || c == Cell.Mine || c == Cell.Warehouse
            || c == Cell.Smith || c == Cell.Auction || c == Cell.Mausoleum
            || c == Cell.Barracks;

        public static bool IsHub(Cell c) =>
            c == Cell.Keep || c == Cell.Smith || c == Cell.Auction
            || c == Cell.Mausoleum || c == Cell.Barracks;

        public static bool IsDefense(Cell c) =>
            c == Cell.Arrow || c == Cell.Magic || c == Cell.Wall || c == Cell.Trap;

        public static bool Walkable(Cell c) => c != Cell.Wall;

        /// <summary>칸 단위 자리. 화면 크기는 여기서만 낸다(GAME_SPEC_ESTATE_BUILD §2-1).</summary>
        public static Vector2Int FootprintOf(Cell c) => c switch
        {
            Cell.Keep => new Vector2Int(2, 2),
            Cell.Warehouse => new Vector2Int(2, 1),
            Cell.Mine => new Vector2Int(2, 1),
            Cell.Barracks => new Vector2Int(2, 1),
            _ => new Vector2Int(1, 1),
        };

        public static bool Covers(int ox, int oy, int x, int y)
        {
            if (!InBounds(ox, oy) || !InBounds(x, y)) return false;
            var c = At(ox, oy);
            if (c == Cell.Empty) return false;
            var f = FootprintOf(c);
            return x >= ox && y >= oy && x < ox + f.x && y < oy + f.y;
        }

        /// <summary>허브·생산 건물이 덮는 칸. 방어 건물을 그 위에 못 놓는다.</summary>
        public static bool CoveredByCore(int x, int y)
        {
            Load();
            if (!InBounds(x, y)) return false;
            int n = Size;
            for (int oy = 0; oy < n; oy++)
            for (int ox = 0; ox < n; ox++)
            {
                var c = _cells[Idx(ox, oy)];
                if (!IsCore(c)) continue;
                if (Covers(ox, oy, x, y)) return true;
            }
            return false;
        }

        /// <summary>자리 크기를 포함해 (x,y)를 덮는 건물 원점. 없으면 false.</summary>
        public static bool TryOwner(int x, int y, out int ox, out int oy)
        {
            Load();
            ox = -1;
            oy = -1;
            if (!InBounds(x, y)) return false;
            int n = Size;
            for (int iy = 0; iy < n; iy++)
            for (int ix = 0; ix < n; ix++)
            {
                if (_cells[Idx(ix, iy)] == Cell.Empty) continue;
                if (!Covers(ix, iy, x, y)) continue;
                ox = ix;
                oy = iy;
                return true;
            }
            return false;
        }

        /// <summary>원점 (ox,oy)의 자리 칸이 격자 안에 있고 다른 건물과 안 겹친다.</summary>
        public static bool Fits(int ox, int oy, Cell c)
        {
            Load();
            if (!InBounds(ox, oy)) return false;
            var f = FootprintOf(c);
            if (ox + f.x > Size || oy + f.y > Size) return false;
            for (int y = oy; y < oy + f.y; y++)
            for (int x = ox; x < ox + f.x; x++)
            {
                if (TryOwner(x, y, out int px, out int py) && (px != ox || py != oy))
                    return false;
            }
            return true;
        }

        public static EstateDefense.Kind? DefenseKindOf(Cell c) => c switch
        {
            Cell.Arrow => EstateDefense.Kind.화살탑,
            Cell.Magic => EstateDefense.Kind.마법탑,
            Cell.Wall => EstateDefense.Kind.성벽,
            Cell.Trap => EstateDefense.Kind.함정,
            _ => null,
        };

        public static Cell CellOf(EstateDefense.Kind k) => k switch
        {
            EstateDefense.Kind.화살탑 => Cell.Arrow,
            EstateDefense.Kind.마법탑 => Cell.Magic,
            EstateDefense.Kind.성벽 => Cell.Wall,
            EstateDefense.Kind.함정 => Cell.Trap,
            _ => Cell.Empty,
        };

        public static int Count(Cell c)
        {
            Load();
            int n = 0;
            for (int i = 0; i < _cells.Length; i++)
                if (_cells[i] == c) n++;
            return n;
        }

        public static int Unplaced(Cell c)
        {
            var kind = DefenseKindOf(c);
            if (kind == null) return 0;
            int have = EstateDefense.Level(kind.Value);
            int used = Count(c);
            return have > used ? have - used : 0;
        }

        public static string WhyCannotPlace(int x, int y, Cell c)
        {
            Load();
            if (Disabled()) return "배치가 꺼져 있다";
            if (!IsDefense(c)) return "방어 건물만 놓는다";
            if (!InBounds(x, y)) return "격자 밖이다";
            if (At(x, y) != Cell.Empty) return "빈 칸이 아니다";
            if (CoveredByCore(x, y)) return "자리 크기가 겹친다";
            if (Unplaced(c) <= 0)
            {
                var kind = DefenseKindOf(c);
                return kind == null
                    ? "놓을 건물이 없다"
                    : $"{kind} Lv{EstateDefense.Level(kind.Value)}만큼만 놓는다";
            }
            if (c == Cell.Wall && WouldSeal(x, y))
                return "4면을 모두 막으면 만능 배치가 된다(§13-3)";
            return null;
        }

        public static bool TryPlace(int x, int y, Cell c)
        {
            if (WhyCannotPlace(x, y, c) != null) return false;
            _cells[Idx(x, y)] = c;
            Save();
            return true;
        }

        public static bool TryPickUp(int x, int y)
        {
            Load();
            if (Disabled()) return false;
            if (!InBounds(x, y)) return false;
            if (!IsDefense(At(x, y))) return false;
            _cells[Idx(x, y)] = Cell.Empty;
            Save();
            return true;
        }

        public static int PathLength(Side side)
        {
            Load();
            int n = Size;
            var seen = new bool[Stride * Stride];
            var qx = new int[Stride * Stride];
            var qy = new int[Stride * Stride];
            var qd = new int[Stride * Stride];
            int head = 0, tail = 0;

            void Enq(int x, int y, int d)
            {
                if (!InBounds(x, y)) return;
                int i = Idx(x, y);
                if (seen[i]) return;
                if (!Walkable(_cells[i])) return;
                seen[i] = true;
                qx[tail] = x;
                qy[tail] = y;
                qd[tail] = d;
                tail++;
            }

            if (side == Side.북)
                for (int x = 0; x < n; x++) Enq(x, 0, 0);
            else if (side == Side.남)
                for (int x = 0; x < n; x++) Enq(x, n - 1, 0);
            else if (side == Side.서)
                for (int y = 0; y < n; y++) Enq(0, y, 0);
            else
                for (int y = 0; y < n; y++) Enq(n - 1, y, 0);

            int[] dx = { 0, 1, 0, -1 };
            int[] dy = { -1, 0, 1, 0 };
            while (head < tail)
            {
                int x = qx[head];
                int y = qy[head];
                int d = qd[head];
                head++;
                if (EstateStore.Reached(x, y)) return d;
                for (int k = 0; k < 4; k++)
                    Enq(x + dx[k], y + dy[k], d + 1);
            }
            return -1;
        }

        public static Side InvaderSide()
        {
            int best = int.MaxValue;
            var pick = Side.북;
            for (int i = 0; i < Sides.Length; i++)
            {
                int n = PathLength(Sides[i]);
                if (n < 0 || n >= best) continue;
                best = n;
                pick = Sides[i];
            }
            return pick;
        }

        public static int InvaderPath() => PathLength(InvaderSide());

        public static bool OnInvaderPath(int x, int y)
        {
            Load();
            if (!InBounds(x, y) || !Walkable(At(x, y))) return false;
            int total = InvaderPath();
            if (total < 0) return false;
            int fromEdge = DistFromSide(InvaderSide(), x, y);
            int toStore = DistToStore(x, y);
            if (fromEdge < 0 || toStore < 0) return false;
            return fromEdge + toStore == total;
        }

        static int DistFromSide(Side side, int tx, int ty)
        {
            int n = Size;
            var seen = new bool[Stride * Stride];
            var qx = new int[Stride * Stride];
            var qy = new int[Stride * Stride];
            var qd = new int[Stride * Stride];
            int head = 0, tail = 0;
            void Enq(int x, int y, int d)
            {
                if (!InBounds(x, y)) return;
                int i = Idx(x, y);
                if (seen[i] || !Walkable(_cells[i])) return;
                seen[i] = true;
                qx[tail] = x; qy[tail] = y; qd[tail] = d;
                tail++;
            }
            if (side == Side.북)
                for (int x = 0; x < n; x++) Enq(x, 0, 0);
            else if (side == Side.남)
                for (int x = 0; x < n; x++) Enq(x, n - 1, 0);
            else if (side == Side.서)
                for (int y = 0; y < n; y++) Enq(0, y, 0);
            else
                for (int y = 0; y < n; y++) Enq(n - 1, y, 0);
            int[] dx = { 0, 1, 0, -1 };
            int[] dy = { -1, 0, 1, 0 };
            while (head < tail)
            {
                int x = qx[head], y = qy[head], d = qd[head];
                head++;
                if (x == tx && y == ty) return d;
                for (int k = 0; k < 4; k++)
                    Enq(x + dx[k], y + dy[k], d + 1);
            }
            return -1;
        }

        static int DistToStore(int sx, int sy)
        {
            var seen = new bool[Stride * Stride];
            var qx = new int[Stride * Stride];
            var qy = new int[Stride * Stride];
            var qd = new int[Stride * Stride];
            int head = 0, tail = 0;
            void Enq(int x, int y, int d)
            {
                if (!InBounds(x, y)) return;
                int i = Idx(x, y);
                if (seen[i] || !Walkable(_cells[i])) return;
                seen[i] = true;
                qx[tail] = x; qy[tail] = y; qd[tail] = d;
                tail++;
            }
            Enq(sx, sy, 0);
            int[] dx = { 0, 1, 0, -1 };
            int[] dy = { -1, 0, 1, 0 };
            while (head < tail)
            {
                int x = qx[head], y = qy[head], d = qd[head];
                head++;
                if (EstateStore.Reached(x, y)) return d;
                for (int k = 0; k < 4; k++)
                    Enq(x + dx[k], y + dy[k], d + 1);
            }
            return -1;
        }

        static bool WouldSeal(int x, int y)
        {
            var prev = _cells[Idx(x, y)];
            _cells[Idx(x, y)] = Cell.Wall;
            bool open = false;
            for (int i = 0; i < Sides.Length; i++)
            {
                if (PathLength(Sides[i]) >= 0) { open = true; break; }
            }
            _cells[Idx(x, y)] = prev;
            return !open;
        }

        public static void SetCellForTest(int x, int y, Cell c)
        {
            Load();
            if (!InBounds(x, y)) return;
            _cells[Idx(x, y)] = c;
            Save();
        }

        public static void SeedQaIfRequested()
        {
            string raw = Environment.GetEnvironmentVariable("QA_ESTATE_GRID");
            if (raw != "1" && !string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase))
                return;
            if (_qaSeeded) return;
            ResetForTest();
            _qaSeeded = true;
            EstateDefense.ResetForTest();
            EstateDefense.SetLevelForTest(EstateDefense.Kind.성벽, 3);
            _loaded = true;
            ApplyDefault();
            _cells[Idx(2, 1)] = Cell.Wall;
            _cells[Idx(3, 1)] = Cell.Wall;
            _cells[Idx(4, 1)] = Cell.Wall;
            Save();
        }

        static void ApplyDefault()
        {
            for (int i = 0; i < _cells.Length; i++) _cells[i] = Cell.Empty;
            _cells[Idx(KeepX, KeepY)] = Cell.Keep;
            _cells[Idx(StoreX, StoreY)] = Cell.Warehouse;
            _cells[Idx(MineX, MineY)] = Cell.Mine;
            PlaceIfEmpty(SmithX, SmithY, Cell.Smith);
            PlaceIfEmpty(AuctionX, AuctionY, Cell.Auction);
            PlaceIfEmpty(MausoleumX, MausoleumY, Cell.Mausoleum);
            PlaceIfEmpty(BarracksX, BarracksY, Cell.Barracks);
        }

        static void PlaceIfEmpty(int x, int y, Cell c)
        {
            if (!InBounds(x, y)) return;
            if (_cells[Idx(x, y)] == Cell.Empty) _cells[Idx(x, y)] = c;
        }

        /// <summary>옛 세이브에 허브 4동이 없으면 빈 칸에 앉힌다. 있으면 그대로.</summary>
        public static void EnsureHubBuildings()
        {
            Load();
            EnsureOne(Cell.Smith, SmithX, SmithY);
            EnsureOne(Cell.Auction, AuctionX, AuctionY);
            EnsureOne(Cell.Mausoleum, MausoleumX, MausoleumY);
            EnsureOne(Cell.Barracks, BarracksX, BarracksY);
        }

        static void EnsureOne(Cell c, int px, int py)
        {
            if (Count(c) > 0) return;
            if (InBounds(px, py) && _cells[Idx(px, py)] == Cell.Empty)
            {
                _cells[Idx(px, py)] = c;
                Save();
                return;
            }
            // 폴백은 해금된 논리 격자 안에서만 — 잠긴 칸(확장 전)에 허브를 앉히지 않는다.
            int n = Size;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                if (_cells[Idx(x, y)] != Cell.Empty) continue;
                _cells[Idx(x, y)] = c;
                Save();
                return;
            }
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            ApplyDefault();
            string raw = PlayerPrefs.GetString(K_CELLS, "");
            if (string.IsNullOrEmpty(raw)) return;
            var parts = raw.Split(',');
            int max = (int)Cell.Barracks;
            if (parts.Length == _cells.Length)
            {
                // 현행 포맷: 물리 배열(Stride×Stride)을 순서대로 저장했다 — 그대로 복원.
                for (int i = 0; i < _cells.Length; i++)
                {
                    if (!int.TryParse(parts[i], out int v)) continue;
                    if (v < 0 || v > max) continue;
                    _cells[i] = (Cell)v;
                }
            }
            else if (parts.Length == BaseSize * BaseSize)
            {
                // 옛 세이브(8×8, stride 8)를 16-stride 좌표로 이관 — 확장 전 배치 보존.
                for (int oy = 0; oy < BaseSize; oy++)
                for (int ox = 0; ox < BaseSize; ox++)
                {
                    if (!int.TryParse(parts[oy * BaseSize + ox], out int v)) continue;
                    if (v < 0 || v > max) continue;
                    _cells[Idx(ox, oy)] = (Cell)v;
                }
            }
            else
            {
                return; // 알 수 없는 길이 — 기본 배치 유지.
            }
            EnsureHubBuildings();
        }

        static void Save()
        {
            var parts = new string[_cells.Length];
            for (int i = 0; i < _cells.Length; i++)
                parts[i] = ((int)_cells[i]).ToString();
            PlayerPrefs.SetString(K_CELLS, string.Join(",", parts));
            PlayerPrefs.Save();
        }

        public static void ResetForTest()
        {
            PlayerPrefs.DeleteKey(K_CELLS);
            PlayerPrefs.Save();
            // 확장을 초기(8×8)로 고정한다 — 기존 영지 SelfCheck들이 논리 폭 8을 전제로
            // 도는데, 앞선 테스트가 탑 층을 올려 놓으면 격자가 커져 오판한다. 부지 확장을
            // 검증하는 테스트만 EstateExpansion.ResetForTest로 이 고정을 푼다.
            EstateExpansion.ForceSizeForTest = BaseSize;
            ApplyDefault();
            _loaded = false;
            _qaSeeded = false;
        }

        public static void ForgetInMemoryForTest()
        {
            _loaded = false;
        }
    }
}
