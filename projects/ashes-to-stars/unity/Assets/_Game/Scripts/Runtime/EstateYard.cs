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
    /// 두 손가락으로 벌려 확대한다 — 휠과 같은 SetZoom. QA_YARD_PINCH 는 ApplyPinch 시드.
    /// </summary>
    public static class EstateYard
    {
        public const float InspectorH = 86f;
        public const float PaletteH = 68f;
        public const float ScreenW = 1280f;
        public const float ScreenH = 720f;
        public const float TileAspect = 0.52f;
        public const float RoofHead = 1.15f;
        /// <summary>세로 맞춤에만 쓰는 지붕 여유. RoofHead(1.15)는 배치 계산용으로 남긴다.</summary>
        public const float FitHead = 0.05f;
        public const float OldTileCap = 88f;
        public const string EnvNo = "QA_NO_YARD_FILL";
        public const string EnvShowPan = "QA_YARD_PAN";
        public const string EnvNoPan = "QA_NO_YARD_PAN";
        public const string EnvShowZoom = "QA_YARD_ZOOM";
        public const string EnvNoZoom = "QA_NO_YARD_ZOOM";
        public const string EnvShowPinch = "QA_YARD_PINCH";
        public const float QaPanX = 180f;
        public const float QaPanY = 48f;
        public const float QaZoom = 1.50f;
        public const float QaPinchFrom = 100f;
        public const float QaPinchTo = 150f;
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
                return On(pan) || On(zoom) || ShowPinchQa;
            }
        }

        public static bool ShowPinchQa =>
            On(Environment.GetEnvironmentVariable(EnvShowPinch));

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
        static float _pinch0;

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
            if (ShowPinchQa && ZoomEnabled)
                return "마을을 두 손가락으로 벌려 확대한다. 집을 누르면 들어간다(§16)";
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
                // 클래시 오브 클랜처럼 **마을이 화면을 채워야 한다**(오너 2026-08-18).
                // 옛 식은 지붕 여유를 1.15칸이나 세로 맞춤에 넣어, 8칸 기준 판이 화면 폭의
                // 72%에서 멈췄다(실측: tw=86.6px, 판 693px / 화면 960px). 지붕은 판 위쪽
                // 헤더 영역으로 넘어가도 되는 장식이라 맞춤 계산에서 뺀다 — 넘치는 만큼은
                // 이미 있는 끌어 보기(Pan)가 담당한다.
                // 오너 지시(2026-08-18) "바닥은 최대한 넓게" — 세로 여유를 더 줄여
                // 가로 폭이 맞춤을 지배하게 한다. 넘치는 세로는 끌어 보기가 담당한다.
                tw = Mathf.Min(area.width / n, area.height / (n * TileAspect + FitHead));
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

        /// <summary>두 손가락 거리 비로 칸을 키운다. 휠 HandleZoom 과 같은 SetZoom.</summary>
        public static float ApplyPinch(float fromDist, float toDist)
        {
            if (!ZoomEnabled) return Zoom;
            if (fromDist <= 1f) return Zoom;
            SetZoom(_zoom * (toDist / fromDist));
            return Zoom;
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
            bool pinch = ShowPinchQa && ZoomEnabled;
            if (!pan && !zoom && !pinch) return;
            _qaSeeded = true;
            if (pan) _pan = new Vector2(QaPanX, QaPanY);
            if (zoom) _zoom = QaZoom;
            if (pinch) ApplyPinch(QaPinchFrom, QaPinchTo);
        }

        public static void ResetForTest()
        {
            _pan = Vector2.zero;
            _zoom = 1f;
            _down = false;
            _dragging = false;
            _qaSeeded = false;
            _pinch0 = 0f;
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
            HandlePinch(area);

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

        static void HandlePinch(Rect area)
        {
            if (!ZoomEnabled) return;
            if (Input.touchCount != 2)
            {
                _pinch0 = 0f;
                return;
            }
            var a = Input.GetTouch(0).position;
            var b = Input.GetTouch(1).position;
            var mid = new Vector2((a.x + b.x) * 0.5f, Screen.height - (a.y + b.y) * 0.5f);
            if (!area.Contains(mid))
            {
                _pinch0 = 0f;
                return;
            }
            float d = Vector2.Distance(a, b);
            if (_pinch0 <= 1f)
            {
                _pinch0 = d;
                return;
            }
            ApplyPinch(_pinch0, d);
            SetPan(area, _pan);
            _pinch0 = d;
        }

        /// <summary>칸 하나가 담는 지면 폭(유닛). 크기표 값을 화면 픽셀로 바꾸는 환산 기준이다.</summary>
        const float TileUnits = 4.2f;

        static Rect BuildingBox(Vector2 p, float tw, float th, EstateGrid.Cell cell)
        {
            // ⚠️ 옛 식은 **칸 너비 × 손으로 맞춘 배율**(Keep 1.12·Arrow 0.58…)로 크기를 정하고
            //    높이는 텍스처 종횡비에서 끌어왔다. 크기표(`art/prop_scale.json`)를 아예 안 봤다 —
            //    오늘 전투 유닛에서 고친 것과 **같은 계열의 버그**(크기 진실이 여러 곳에 갈라짐).
            //    그 결과 실측: 탑은 종횡비 0.66(높고 좁음)인데 배율 0.58까지 겹쳐 난쟁이가 됐고,
            //    경매장은 종횡비 1.02라 같은 배율에서도 훨씬 커 보였다. 건물끼리 키가 안 맞았다.
            //    크기표는 **목표 높이(유닛)**를 정의하므로 높이를 표에서 받고 너비는 종횡비로 낸다
            //    (SpriteBank·FieldDecor가 쓰는 방식과 같다). 표에 없으면 옛 배율로 폴백.
            var tex = PropTex(PropOf(cell));
            string prop = PropOf(cell);
            float units = string.IsNullOrEmpty(prop) ? 0f : FieldDecor.Units(prop, 0f);

            float bw, bh;
            if (units > 0f && tex != null && tex.height > 0)
            {
                bh = units * (tw / TileUnits);
                bw = bh * ((float)tex.width / tex.height);
            }
            else
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
                bw = tw * wide;
                bh = tex != null && tex.width > 0
                    ? bw * ((float)tex.height / tex.width)
                    : (cell == EstateGrid.Cell.Keep ? th * 2.55f : th * 2.05f);
            }

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
            _grass = LoadGroundTex("estate_grass", new Color(0.36f, 0.52f, 0.28f, 0.92f), new Color(0.22f, 0.34f, 0.16f, 0.95f));
            _path = LoadGroundTex("estate_path", new Color(0.72f, 0.52f, 0.28f, 0.95f), new Color(0.48f, 0.30f, 0.12f, 1f));
            _plot = LoadGroundTex("estate_plot", new Color(0.42f, 0.36f, 0.26f, 0.95f), new Color(0.28f, 0.22f, 0.14f, 1f));
            _sel = Diamond(new Color(0.95f, 0.82f, 0.28f, 0.35f), new Color(1f, 0.86f, 0.30f, 0.95f));
        }

        static Texture2D LoadGroundTex(string name, Color fill, Color edge)
        {
            // 2026-08-18: `Assets/Editor/TextureImportRules.cs`가 "/ground/" 아래 전체에
            // seamless 타일링 바닥 규칙(alphaSource=None, wrapMode=Repeat)을 일괄 적용하고
            // 있었다 — 이 다이아몬드 타일은 알파로 마름모 실루엣을 자르는 컷아웃 텍스처라
            // 알파가 통째로 버려지면 타일 바깥(투명해야 할 영역)이 불투명 검정으로 렌더된다
            // (PNG 자체의 알파·RGB는 정상이었다). `ground/estate/` 하위만 alphaSource=
            // FromInput·wrapMode=Clamp로 예외 처리해 해결 — 원인이 EstateYard.cs가 아니라
            // 임포트 규칙 쪽이었다는 점에 주의.
            var tex = Resources.Load<Texture2D>("ground/estate/" + name);
            if (tex != null) return tex;
            Debug.LogWarning("[영지] 바닥 텍스처 누락: ground/estate/" + name + " — 절차 생성 단색으로 대체");
            return Diamond(fill, edge);
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
