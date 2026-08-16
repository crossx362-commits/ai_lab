using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 영지 마을 전경. 카드 목록이 아니라 격자 위 건물을 눌러 관리한다.
    /// </summary>
    public static class EstateYard
    {
        public const float InspectorH = 86f;
        public const float PaletteH = 68f;

        static Texture2D _grass, _path, _plot, _sel;

        public static string IconOf(EstateGrid.Cell c) => c switch
        {
            EstateGrid.Cell.Keep => "territory",
            EstateGrid.Cell.Mine => "field",
            EstateGrid.Cell.Warehouse => "building_auction",
            EstateGrid.Cell.Smith => "building_smith",
            EstateGrid.Cell.Auction => "building_auction",
            EstateGrid.Cell.Mausoleum => "building_mausoleum",
            EstateGrid.Cell.Barracks => "building_barracks",
            EstateGrid.Cell.Arrow => "tower",
            EstateGrid.Cell.Magic => "field",
            EstateGrid.Cell.Wall => "territory",
            EstateGrid.Cell.Trap => "building_barracks",
            _ => null,
        };

        public static string LabelOf(EstateGrid.Cell c) => c switch
        {
            EstateGrid.Cell.Keep => "본성",
            EstateGrid.Cell.Mine => "광산",
            EstateGrid.Cell.Warehouse => "창고",
            EstateGrid.Cell.Smith => "대장간",
            EstateGrid.Cell.Auction => "경매장",
            EstateGrid.Cell.Mausoleum => "영묘",
            EstateGrid.Cell.Barracks => "수비대",
            EstateGrid.Cell.Arrow => "화살탑",
            EstateGrid.Cell.Magic => "마법탑",
            EstateGrid.Cell.Wall => "성벽",
            EstateGrid.Cell.Trap => "함정",
            _ => "빈 칸",
        };

        /// <summary>마을을 그리고 클릭한 칸을 돌려준다. 클릭 없으면 (-1,-1).</summary>
        public static bool Draw(Rect area, int selX, int selY, out int hitX, out int hitY)
        {
            hitX = -1;
            hitY = -1;
            EstateGrid.EnsureHubBuildings();
            EnsureTex();
            int n = EstateGrid.Size;
            float tw = Mathf.Min(88f, area.width / (n + 0.6f));
            float th = tw * 0.52f;
            float totalW = n * tw;
            float totalH = n * th;
            float fit = Mathf.Min(1f, Mathf.Min(area.width / totalW, area.height / (totalH + th)));
            tw *= fit;
            th *= fit;
            float ox = area.center.x;
            float oy = area.y + 6f;

            Vector2 Origin(int x, int y)
            {
                return new Vector2(
                    ox + (x - y) * tw * 0.5f - tw * 0.5f,
                    oy + (x + y) * th * 0.5f);
            }

            var mouse = Event.current.mousePosition;
            int best = -1;
            for (int s = 0; s < n * 2; s++)
            for (int x = 0; x < n; x++)
            {
                int y = s - x;
                if (y < 0 || y >= n) continue;
                var p = Origin(x, y);
                var tile = new Rect(p.x, p.y, tw, th);
                var cell = EstateGrid.At(x, y);
                bool onPath = EstateGrid.OnInvaderPath(x, y);
                var ground = cell == EstateGrid.Cell.Empty
                    ? (onPath ? _path : _grass)
                    : _plot;
                GUI.DrawTexture(tile, ground);
                if (selX == x && selY == y) GUI.DrawTexture(tile, _sel);
                if (InDiamond(mouse, tile) && x + y >= best)
                {
                    best = x + y;
                    hitX = x;
                    hitY = y;
                }
            }

            for (int s = 0; s < n * 2; s++)
            for (int x = 0; x < n; x++)
            {
                int y = s - x;
                if (y < 0 || y >= n) continue;
                var cell = EstateGrid.At(x, y);
                if (cell == EstateGrid.Cell.Empty) continue;
                var p = Origin(x, y);
                string icon = IconOf(cell);
                if (string.IsNullOrEmpty(icon)) continue;
                float bh = cell == EstateGrid.Cell.Keep ? th * 2.55f : th * 2.05f;
                float bw = tw * (cell == EstateGrid.Cell.Keep ? 1.2f : 0.98f);
                var box = new Rect(p.x + (tw - bw) * 0.5f, p.y + th * 0.18f - bh * 0.62f, bw, bh);
                UiAtlas.DrawFit(box, icon);
            }

            if (Event.current.type == EventType.MouseDown
                && Event.current.button == 0
                && area.Contains(mouse)
                && best >= 0)
            {
                Event.current.Use();
                return true;
            }
            hitX = -1;
            hitY = -1;
            return false;
        }

        static bool InDiamond(Vector2 p, Rect tile)
        {
            float nx = (p.x - tile.center.x) / (tile.width * 0.5f);
            float ny = (p.y - tile.center.y) / (tile.height * 0.5f);
            return Mathf.Abs(nx) + Mathf.Abs(ny) <= 1.05f;
        }

        static void EnsureTex()
        {
            if (_grass != null) return;
            _grass = Diamond(new Color(0.36f, 0.52f, 0.28f, 0.92f), new Color(0.22f, 0.34f, 0.16f, 0.95f));
            _path = Diamond(new Color(0.72f, 0.52f, 0.28f, 0.95f), new Color(0.48f, 0.30f, 0.12f, 1f));
            _plot = Diamond(new Color(0.42f, 0.36f, 0.26f, 0.95f), new Color(0.28f, 0.22f, 0.14f, 1f));
            _sel = Diamond(new Color(0.95f, 0.82f, 0.28f, 0.35f), new Color(1f, 0.86f, 0.30f, 0.95f));
        }

        static Texture2D Diamond(Color fill, Color edge)
        {
            const int w = 64, h = 32;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var px = new Color[w * h];
            float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = Mathf.Abs((x - cx) / cx) + Mathf.Abs((y - cy) / cy);
                if (u <= 0.86f) px[y * w + x] = fill;
                else if (u <= 1.02f) px[y * w + x] = Color.Lerp(fill, edge, (u - 0.86f) / 0.16f);
                else px[y * w + x] = Color.clear;
            }
            t.SetPixels(px);
            t.Apply(false, true);
            return t;
        }
    }
}
