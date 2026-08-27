using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// V4 즉시계속 — 일시정지 오버레이 중앙 카드만(docs/plans/V4_LOOP_BOUNDARY.md §3).
    /// 관문② 설문과 같은 자리. 본문·내비에 안 섞는다. Cap 문자열 없음.
    /// 사람 표본·삭제 판정·경계 로그는 여기 없다(사람·개발 칸).
    /// </summary>
    public static class V4ContinueOverlay
    {
        public const string EnvShow = "QA_V4_CONTINUE";
        public const string EnvGo = "QA_V4_CONTINUE_GO";
        public const string EnvNo = "QA_NO_V4_CONTINUE";
        public const string TitleText = "바로 이어서";
        public const string BodyText = "다음으로 바로 갈 수 있다.";
        public const string ContinueText = "계속";
        public const string StopText = "그만";

        public const float RefW = 1280f;
        public const float RefH = 720f;

        static bool _open;
        static bool _seeded;
        static string _last;
        static Texture2D _dim;
        static GUIStyle _title;
        static GUIStyle _body;
        static GUIStyle _btn;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool Open => _open && !Blocked;
        public static string LastChoice => _last ?? "";

        public static void Show()
        {
            if (Blocked) return;
            _open = true;
        }

        public static void Hide()
        {
            _open = false;
        }

        public static void SeedQaIfRequested()
        {
            if (_seeded) return;
            if (Blocked) return;
            string show = Environment.GetEnvironmentVariable(EnvShow);
            string go = Environment.GetEnvironmentVariable(EnvGo);
            bool wantShow = show == "1" || string.Equals(show, "true", StringComparison.OrdinalIgnoreCase);
            bool wantGo = go == "1" || string.Equals(go, "true", StringComparison.OrdinalIgnoreCase);
            if (!wantShow && !wantGo) return;
            _seeded = true;
            if (wantGo)
            {
                _open = false;
                _last = ContinueText;
                return;
            }
            _open = true;
        }

        public static void ResetForTest()
        {
            _open = false;
            _seeded = false;
            _last = null;
        }

        public static void Draw()
        {
            SeedQaIfRequested();
            if (!Open) return;
            Ensure();
            var dim = new Rect(0f, 0f, RefW, RefH);
            GUI.DrawTexture(dim, _dim);
            var card = new Rect((RefW - 520f) * 0.5f, (RefH - 240f) * 0.5f, 520f, 240f);
            if (!UiAtlas.DrawSliced(card, "panel", 16f, new Color(1f, 1f, 1f, 0.96f)))
                UiAtlas.Draw(card, "panel", new Color(1f, 1f, 1f, 0.96f));
            var titleR = new Rect(card.x + 28f, card.y + 24f, card.width - 56f, 40f);
            var bodyR = new Rect(card.x + 28f, titleR.yMax + 8f, card.width - 56f, 48f);
            UiPages.LabelFit(titleR, TitleText, _title);
            UiPages.LabelFit(bodyR, BodyText, _body);
            var goR = new Rect(card.x + 36f, card.yMax - 72f, 200f, 44f);
            var stopR = new Rect(card.xMax - 236f, card.yMax - 72f, 200f, 44f);
            DrawChoice(goR, ContinueText, true);
            DrawChoice(stopR, StopText, false);
            Eat(dim);
        }

        static void DrawChoice(Rect r, string label, bool cont)
        {
            UiAtlas.DrawSliced(r, UiAtlas.ButtonKey(false, false), 8f);
            UiPages.LabelFit(UiAtlas.ContentRect(r, "button", 2f), label, _btn);
            var e = Event.current;
            if (e != null && e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            {
                e.Use();
                _last = label;
                _open = false;
                _ = cont;
            }
        }

        static void Eat(Rect dim)
        {
            var e = Event.current;
            if (e == null) return;
            if (e.type != EventType.MouseDown && e.type != EventType.MouseUp) return;
            if (!dim.Contains(e.mousePosition)) return;
            e.Use();
        }

        static void Ensure()
        {
            if (_dim == null)
            {
                _dim = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                _dim.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.62f));
                _dim.Apply(false, true);
            }
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
            };
            _title.normal.textColor = new Color(1f, 0.94f, 0.72f, 1f);
            _body = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                clipping = TextClipping.Clip,
                wordWrap = true,
            };
            _body.normal.textColor = new Color(0.92f, 0.88f, 0.78f, 1f);
            _btn = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
            };
            _btn.normal.textColor = new Color(1f, 0.96f, 0.84f, 1f);
        }
    }
}
