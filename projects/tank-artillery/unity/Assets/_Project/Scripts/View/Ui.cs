// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §8 UI — HUDController·PowerBarUI·TeamStatusUI 계열이 쓸 그리기 도구.
//
// 왜 코드로 그리나: 이 프로젝트는 지형(§7)도 탱크(§9)도 에셋 없이 코드로 만든다.
// UI 만 스프라이트를 들이면 빌드·리포지토리·플랫폼 조건이 거기서만 갈린다.
// 흰 1x1 텍스처 한 장 + `GUI.color` 곱 + `GUI.DrawTexture` 면 바·패널·테두리가 전부 나온다.
//
// ⚠️ 이모지·특수 글리프를 쓰지 마라 — 기본 폰트에 글리프가 없어 □ 로 찍힌다(§2-9-11 에서 실제로 겪었다).
//    화살표 같은 도형이 필요하면 여기 `Tri`/`Arrow` 처럼 **도형으로 그려라.**
//
// ⚠️ 색을 각 화면에서 직접 적지 마라. 팔레트는 이 파일이 단일 소스다 —
//    같은 값을 여러 곳에 적으면 반드시 어긋난다(SpawnTeams 의 종별 색에서 이미 겪은 실패, §2-9-1).

using UnityEngine;

namespace Tankfall.View
{
    public static class Ui
    {
        // ── 팔레트 ──────────────────────────────────────────────
        // 팀 색은 탱크 강조색(SpawnTeams)과 같은 계열을 쓴다 — HUD 의 파랑이 화면의 파랑과 다르면 팀이 안 읽힌다.
        public static readonly Color Ally    = new Color(0.50f, 0.69f, 1.00f);
        public static readonly Color Enemy   = new Color(1.00f, 0.54f, 0.50f);
        public static readonly Color Ink     = new Color(0.93f, 0.95f, 0.98f);
        public static readonly Color Dim     = new Color(0.62f, 0.66f, 0.72f);
        public static readonly Color Panel   = new Color(0.05f, 0.07f, 0.10f, 0.78f);
        public static readonly Color Border  = new Color(1.00f, 1.00f, 1.00f, 0.16f);
        public static readonly Color Slot    = new Color(1.00f, 1.00f, 1.00f, 0.09f);
        public static readonly Color Good    = new Color(0.45f, 0.85f, 0.45f);
        public static readonly Color Warn    = new Color(1.00f, 0.83f, 0.47f);
        public static readonly Color Bad     = new Color(1.00f, 0.42f, 0.42f);
        public static readonly Color Power   = new Color(1.00f, 0.72f, 0.25f);
        public static readonly Color Gauge   = new Color(0.55f, 0.82f, 1.00f);
        public static readonly Color Mark    = new Color(0.55f, 1.00f, 0.55f);

        // ── 텍스처 ──────────────────────────────────────────────
        static Texture2D _white, _tri;

        public static Texture2D White
        {
            get
            {
                if (_white == null)
                {
                    _white = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                    _white.SetPixel(0, 0, Color.white);
                    _white.Apply();
                }
                return _white;
            }
        }

        /// <summary>위를 향하는 삼각형(알파). 바람 화살촉·선택 커서에 쓴다.</summary>
        public static Texture2D Tri
        {
            get
            {
                if (_tri == null)
                {
                    const int N = 32;
                    _tri = new Texture2D(N, N, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                    _tri.filterMode = FilterMode.Bilinear;
                    var px = new Color32[N * N];
                    for (int y = 0; y < N; y++)
                        for (int x = 0; x < N; x++)
                        {
                            // y=0 이 아래. 위로 갈수록 좁아지는 이등변삼각형.
                            float halfW = (N - 1 - y) * 0.5f;
                            float d = Mathf.Abs(x - (N - 1) * 0.5f);
                            float a = Mathf.Clamp01(halfW - d + 0.5f);     // 가장자리 1px 안티에일리어싱
                            px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255));
                        }
                    _tri.SetPixels32(px);
                    _tri.Apply();
                }
                return _tri;
            }
        }

        // ── 도형 ────────────────────────────────────────────────
        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, White);
            GUI.color = old;
        }

        public static void Frame(Rect r, Color c, float t = 1f)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        /// <summary>배경 + 테두리. HUD 패널의 기본 바탕.</summary>
        public static void Box(Rect r, Color? bg = null, Color? border = null)
        {
            Fill(r, bg ?? Panel);
            Frame(r, border ?? Border);
        }

        /// <summary>왼쪽부터 차는 가로 바. frac 은 0~1 로 잘라 쓴다.</summary>
        public static void Bar(Rect r, float frac, Color fill, Color? bg = null, Color? border = null)
        {
            Fill(r, bg ?? Slot);
            float w = Mathf.Clamp01(frac) * r.width;
            if (w > 0f) Fill(new Rect(r.x, r.y, w, r.height), fill);
            if (border.HasValue) Frame(r, border.Value);
        }

        /// <summary>바 위의 세로 표시선(나이스샷 마크 등). at 은 0~1.</summary>
        public static void Tick(Rect r, float at, Color c, float w = 2f)
        {
            float x = r.x + Mathf.Clamp01(at) * r.width - w * 0.5f;
            Fill(new Rect(x, r.y - 2f, w, r.height + 4f), c);
        }

        /// <summary>바를 n칸으로 끊어 보여준다(눈금). 파워 20/40/60/80 을 눈으로 읽게 하는 용도.</summary>
        public static void Ticks(Rect r, int n, Color c)
        {
            for (int i = 1; i < n; i++)
                Fill(new Rect(r.x + r.width * i / n, r.y, 1f, r.height), c);
        }

        /// <summary>angleDeg = 0 이 위(북), 시계 방향. 길이는 rect 높이에 맞춘다.</summary>
        public static void Arrow(Vector2 center, float angleDeg, float len, float thick, Color c)
        {
            var old = GUI.color; var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(angleDeg, center);
            GUI.color = c;
            float headH = Mathf.Min(len * 0.45f, thick * 3f);
            // 축(꼬리 → 머리 밑). 위쪽이 머리다.
            GUI.DrawTexture(new Rect(center.x - thick * 0.5f, center.y - len * 0.5f + headH * 0.6f, thick, len - headH * 0.6f), White);
            GUI.DrawTexture(new Rect(center.x - headH * 0.55f, center.y - len * 0.5f, headH * 1.1f, headH), Tri);
            GUI.color = old; GUI.matrix = m;
        }

        // ── 글자 ────────────────────────────────────────────────
        static GUIStyle _s;
        public static GUIStyle Style(int size, Color c, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false)
        {
            if (_s == null) _s = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = false };
            _s.fontSize = size;
            _s.alignment = anchor;
            _s.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            _s.normal.textColor = c;
            return _s;
        }

        public static void Text(Rect r, string s, int size, Color c, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false)
            => GUI.Label(r, s, Style(size, c, anchor, bold));

        /// <summary>글자 뒤에 옅은 그림자를 깔아 밝은 지형 위에서도 읽히게 한다.</summary>
        public static void TextShadow(Rect r, string s, int size, Color c, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false)
        {
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), s, Style(size, new Color(0f, 0f, 0f, 0.65f), anchor, bold));
            GUI.Label(r, s, Style(size, c, anchor, bold));
        }

        /// <summary>체력 비율에 따른 색. 6개 유닛을 한눈에 훑을 때 숫자보다 색이 먼저 읽힌다.</summary>
        public static Color HpColor(float frac)
            => frac > 0.5f ? Good : frac > 0.25f ? Warn : Bad;
    }
}
