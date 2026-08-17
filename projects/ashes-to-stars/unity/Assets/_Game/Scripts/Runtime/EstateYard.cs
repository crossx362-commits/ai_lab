using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 영지 마을 전경. 카드 목록이 아니라 격자 위 건물을 눌러 관리한다.
    /// 클래시 오브 클랜·쿠키런 킹덤처럼 마을이 화면을 채우고 HUD는 위에 얹는다.
    /// 칸 상한 88과 하단 카드 여백은 마름모를 작게 만든다 — QA_NO_YARD_FILL 이면 그 옛 길.
    /// 끌어 보기는 마을을 옮긴다 — QA_NO_YARD_PAN 이면 옛 고정 시점.
    /// 굴려 확대는 칸을 키운다 — QA_NO_YARD_ZOOM 이면 옛 배율 1.
    /// </summary>
    public static class EstateYard
    {
        public const float InspectorH = 86f;
        public const float PaletteH = 68f;
        public const float ScreenW = 1280f;
        public const float ScreenH = 720f;
        public const float TileAspect = 0.52f;
        public const float RoofHead = 1.15f;
        public const float OldTileCap = 88f;
        public const string EnvNo = "QA_NO_YARD_FILL";
        public const string EnvShowPan = "QA_YARD_PAN";
        public const string EnvNoPan = "QA_NO_YARD_PAN";
        public const string EnvShowZoom = "QA_YARD_ZOOM";
        public const string EnvNoZoom = "QA_NO_YARD_ZOOM";
        public const float QaPanX = 180f;
        public const float QaPanY = 48f;
        public const float QaZoom = 1.50f;
        public const float ZoomMin = 0.70f;
        public const float ZoomMax = 1.55f;
        public const float ZoomStep = 0.08f;
        public const float DragSlop = 8f;
        public const float PanShare = 0.40f;

        public static bool FillBlocked =>
            Environment.GetEnvironmentVariable(EnvNo) == "1";

        public static bool PanBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNoPan);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool ZoomBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNoZoom);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool ShowQa
        {
            get
            {
                string pan = Environment.GetEnvironmentVariable(EnvShowPan);
                string zoom = Environment.GetEnvironmentVariable(EnvShowZoom);
                return On(pan) || On(zoom);
            }
        }

        static bool On(string raw) =>
            raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);

        static Texture2D _grass, _path, _plot, _sel;
        static readonly Dictionary<string, Texture2D> Props = new Dictionary<string, Texture2D>();
        static Vector2 _pan;
        static float _zoom = 1f;
        static Vector2 _dragFrom;
        static Vector2 _panAtDrag;
        static bool _down;
        static bool _dragging;
        static bool _qaSeeded;

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

        /// <summary>
        /// 허브 칸이 쓰는 마을 프랍. FieldDecor 전용으로만 있으면 영지는 UI 아이콘 체스판이 된다.
        /// 본성·광산·창고·수비대·대장간·영묘·탑·경매장은 EstateBuildings 전용 그림(§16).
        /// </summary>
        public static string PropOf(EstateGrid.Cell c) => EstateBuildings.PropOf(c);

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

        public static Vector2 Pan => PanEnabled ? _pan : Vector2.zero;

        public static float Zoom => ZoomEnabled ? _zoom : 1f;

        public static bool PanEnabled => !FillBlocked && !PanBlocked;

        public static bool ZoomEnabled => !FillBlocked && !ZoomBlocked;

        public static string Line()
        {
            if (FillBlocked)
                return "마을에서 건물을 눌러 관리한다. 방어는 빈 칸에 놓는다(§13·§16)";
            if (PanEnabled && ZoomEnabled)
                return "마을을 끌어 보고 굴려 확대한다. 집을 누르면 들어간다(§16)";
            if (PanEnabled)
                return "마을을 끌어 본다. 집을 누르면 들어간다(§16)";
            if (ZoomEnabled)
                return "마을을 굴려 확대한다. 집을 누르면 들어간다(§16)";
            return "마을이 화면을 채운다. 집을 누르면 들어간다(§16)";
        }

        /// <summary>
        /// 마을이 차지하는 칸. 막히면 정보·팔레트 여백을 빼고, 아니면 화면 아래까지 채운다.
        /// </summary>
        public static Rect VillageRect(Rect page)
        {
            if (FillBlocked)
            {
                float top = 80f;
                float bottom = InspectorH + PaletteH + 10f;
                return new Rect(page.x, page.y + top, page.width,
                    Mathf.Max(120f, page.height - top - bottom));
            }
            float x = Mathf.Max(8f, page.x - 24f);
            float w = ScreenW - x - 8f;
            return new Rect(x, page.y, w, ScreenH - page.y);
        }

        public static float TileW(Rect area)
        {
            int n = Mathf.Max(1, EstateGrid.Size);
            float tw;
            if (FillBlocked)
                tw = Mathf.Min(OldTileCap, area.width / (n + 0.6f));
            else
                tw = Mathf.Min(area.width / n, area.height / (n * TileAspect + RoofHead));
            return tw * Zoom;
        }

        /// <summary>칸 크기. 끌어 보기 상한과 Origin이 같은 값을 쓴다.</summary>
        public static void TileSize(Rect area, out float tw, out float th)
        {
            int n = Mathf.Max(1, EstateGrid.Size);
            tw = TileW(area);
            th = tw * TileAspect;
            if (!FillBlocked) return;
            float totalW = n * tw;
            float totalH = n * th;
            float fit = Mathf.Min(1f, Mathf.Min(area.width / totalW, area.height / (totalH + th)));
            tw *= fit;
            th *= fit;
        }

        public static Vector2 MaxPan(Rect area)
        {
            TileSize(area, out float tw, out float th);
            int n = Mathf.Max(1, EstateGrid.Size);
            return new Vector2(n * tw * PanShare, n * th * PanShare);
        }

        public static Vector2 ClampPan(Rect area, Vector2 pan)
        {
            var max = MaxPan(area);
            return new Vector2(
                Mathf.Clamp(pan.x, -max.x, max.x),
                Mathf.Clamp(pan.y, -max.y, max.y));
        }

        public static void SetPan(Rect area, Vector2 pan)
        {
            _pan = PanEnabled ? ClampPan(area, pan) : Vector2.zero;
        }

        public static float ClampZoom(float zoom) =>
            Mathf.Clamp(zoom, ZoomMin, ZoomMax);

        public static void SetZoom(float zoom)
        {
            _zoom = ZoomEnabled ? ClampZoom(zoom) : 1f;
        }

        /// <summary>칸 왼쪽 위. 끌어 보기가 켜지면 Pan만큼 옮긴다.</summary>
        public static Vector2 TileOrigin(Rect area, int x, int y)
        {
            TileSize(area, out float tw, out float th);
            int n = Mathf.Max(1, EstateGrid.Size);
            Vector2 pan = Pan;
            float ox = area.center.x + pan.x;
            float oy = area.y + Mathf.Max(6f, (area.height - n * th) * 0.16f) + pan.y;
            return new Vector2(
                ox + (x - y) * tw * 0.5f - tw * 0.5f,
                oy + (x + y) * th * 0.5f);
        }

        public static void SeedQaIfRequested()
        {
            if (_qaSeeded) return;
            bool pan = On(Environment.GetEnvironmentVariable(EnvShowPan)) && PanEnabled;
            bool zoom = On(Environment.GetEnvironmentVariable(EnvShowZoom)) && ZoomEnabled;
            if (!pan && !zoom) return;
            _qaSeeded = true;
            if (pan) _pan = new Vector2(QaPanX, QaPanY);
            if (zoom) _zoom = QaZoom;
        }

        public static void ResetForTest()
        {
            _pan = Vector2.zero;
            _zoom = 1f;
            _down = false;
            _dragging = false;
            _qaSeeded = false;
        }

        /// <summary>마을을 그리고 클릭한 칸을 돌려준다. 클릭 없으면 (-1,-1).</summary>
        public static bool Draw(Rect area, int selX, int selY, out int hitX, out int hitY)
        {
            hitX = -1;
            hitY = -1;
            EstateGrid.EnsureHubBuildings();
            EnsureTex();
            int n = EstateGrid.Size;
            TileSize(area, out float tw, out float th);
            HandlePan(area);
            HandleZoom(area);

            Vector2 Origin(int x, int y) => TileOrigin(area, x, y);

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
                var box = BuildingBox(p, tw, th, cell);
                var tex = PropTex(PropOf(cell));
                if (tex != null)
                    GUI.DrawTexture(box, tex, ScaleMode.ScaleToFit, true);
                else
                {
                    string icon = IconOf(cell);
                    if (!string.IsNullOrEmpty(icon))
                        UiAtlas.DrawFit(box, icon);
                }
                if (selX == x && selY == y)
                    DrawName(box, LabelOf(cell));
                if (box.Contains(mouse) && x + y >= best)
                {
                    best = x + y;
                    hitX = x;
                    hitY = y;
                }
            }

            var ev = Event.current;
            bool click = PanEnabled
                ? ev.type == EventType.MouseUp && ev.button == 0 && !_dragging
                : ev.type == EventType.MouseDown && ev.button == 0;
            if (click && area.Contains(mouse) && best >= 0)
            {
                ev.Use();
                return true;
            }
            hitX = -1;
            hitY = -1;
            return false;
        }

        static void HandlePan(Rect area)
        {
            if (!PanEnabled)
            {
                _down = false;
                _dragging = false;
                return;
            }
            var ev = Event.current;
            if (ev == null) return;
            var mouse = ev.mousePosition;
            if (ev.type == EventType.MouseDown && ev.button == 0 && area.Contains(mouse))
            {
                _down = true;
                _dragging = false;
                _dragFrom = mouse;
                _panAtDrag = _pan;
                return;
            }
            if (!_down) return;
            if (ev.type == EventType.MouseDrag && ev.button == 0)
            {
                Vector2 d = mouse - _dragFrom;
                if (d.sqrMagnitude >= DragSlop * DragSlop)
                {
                    _dragging = true;
                    SetPan(area, _panAtDrag + d);
                    ev.Use();
                }
                return;
            }
            if (ev.type == EventType.MouseUp && ev.button == 0)
            {
                if (_dragging) ev.Use();
                _down = false;
            }
        }

        static void HandleZoom(Rect area)
        {
            if (!ZoomEnabled) return;
            var ev = Event.current;
            if (ev == null || ev.type != EventType.ScrollWheel) return;
            if (!area.Contains(ev.mousePosition)) return;
            SetZoom(_zoom - ev.delta.y * ZoomStep);
            SetPan(area, _pan);
            ev.Use();
        }

        static Rect BuildingBox(Vector2 p, float tw, float th, EstateGrid.Cell cell)
        {
            float wide = cell switch
            {
                EstateGrid.Cell.Keep => 1.12f,
                EstateGrid.Cell.Wall => 0.92f,
                EstateGrid.Cell.Trap => 0.72f,
                EstateGrid.Cell.Arrow => 0.58f,
                EstateGrid.Cell.Magic => 0.58f,
                EstateGrid.Cell.Auction => 0.78f,
                EstateGrid.Cell.Mausoleum => 0.70f,
                _ => 0.96f,
            };
            float bw = tw * wide;
            var tex = PropTex(PropOf(cell));
            float bh;
            if (tex != null && tex.width > 0)
                bh = bw * ((float)tex.height / tex.width);
            else
                bh = cell == EstateGrid.Cell.Keep ? th * 2.55f : th * 2.05f;
            float sit = cell == EstateGrid.Cell.Wall ? 0.55f : 0.42f;
            return new Rect(p.x + (tw - bw) * 0.5f, p.y + th * sit - bh, bw, bh);
        }

        static void DrawName(Rect box, string name)
        {
            var tag = new Rect(box.center.x - 36f, box.y - 16f, 72f, 16f);
            var bg = _sel;
            if (bg != null) GUI.DrawTexture(tag, bg);
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
            };
            style.normal.textColor = new Color(1f, 0.94f, 0.72f, 1f);
            GUI.Label(tag, name, style);
        }

        static bool InDiamond(Vector2 p, Rect tile)
        {
            float nx = (p.x - tile.center.x) / (tile.width * 0.5f);
            float ny = (p.y - tile.center.y) / (tile.height * 0.5f);
            return Mathf.Abs(nx) + Mathf.Abs(ny) <= 1.05f;
        }

        static Texture2D PropTex(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Props.TryGetValue(name, out var cached) && cached != null) return cached;
            var src = Resources.Load<Texture2D>("props/" + name);
            var keyed = KeyMagenta(src);
            Props[name] = keyed;
            return keyed;
        }

        static Texture2D KeyMagenta(Texture2D src)
        {
            if (src == null) return null;
            if (!src.isReadable) return src;
            int w = src.width, h = src.height;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var px = src.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.r > 0.75f && c.b > 0.75f && c.g < 0.28f)
                    px[i] = Color.clear;
            }
            t.SetPixels(px);
            t.Apply(false, true);
            return t;
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
