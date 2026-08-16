using UnityEngine;

namespace AshesToStars
{
    // ─────────────────────────────────────────────────────────────
    // 기획서 §2(코어 루프)·§16(UI 화면 구조)의 골격을 실제로 걸어다닐 수 있게 만든 것.
    // 지금은 각 화면이 "무엇을 하는 곳인가"와 이동만 담는다 — 콘텐츠는 수직 슬라이스에서 채운다.
    // ⚠️ 여기에 전투 로직·수치를 넣지 마라. 수치의 출처는 언제나 §18 기준표와 ScriptableObject다.
    //
    // 화면 구현체는 클래스마다 파일이 하나씩이다(TitleScreen.cs·FieldScreen.cs …).
    // 한 파일에 몰아넣으면 Unity가 씬의 컴포넌트를 떼어낸다 — 각 파일 머리 주석 참조.
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 모든 플레이 화면의 공통 껍데기 — 제목판, §16 하단 고정바, ESC 처리.
    /// 각 화면은 Body()만 구현하면 된다.
    ///
    /// 해상도 대응 (2026-08-13 오너 지적 "하나도 안 보임"):
    ///   IMGUI는 **픽셀 좌표 고정**이라 화면이 커질수록 UI가 상대적으로 작아진다.
    ///   1920×1080에서 돌리자 글자와 버튼이 좌상단 구석에 깨알같이 몰렸다.
    ///   그래서 기준 해상도(REF)를 정해 두고 GUI.matrix로 스케일한다 —
    ///   좌표는 항상 REF 기준으로 쓰면 되고, 어떤 해상도에서도 같은 비율로 보인다.
    /// </summary>
    public abstract class GameScreen : MonoBehaviour
    {
        protected abstract string Title { get; }
        protected virtual string Subtitle => "";
        protected virtual bool ShowBottomBar => true;
        /// <summary>전투처럼 자체 HUD가 상단을 소유하는 화면은 공통 제목판을 숨긴다.</summary>
        protected virtual bool ShowHeader => true;

        /// <summary>
        /// 화면 전체를 불투명 배경으로 덮을지.
        /// ⚠️ 전투처럼 **카메라가 그린 장면을 보여줘야 하는 화면은 반드시 false**다.
        ///    IMGUI는 카메라 렌더 결과 **위에** 그려지므로, 배경을 깔면 전투가 통째로 가려진다.
        ///    실제로 그래서 "검은 화면만 나온다"는 보고가 나왔다(2026-08-13) —
        ///    전투는 정상 작동 중이었고 가려져 있었을 뿐이다.
        /// </summary>
        protected virtual bool OpaqueBackground => true;

        /// <summary>
        /// 제목 옆 아이콘. 매핑 없는 화면을 worldmap으로 숨기면 파티·결과가
        /// 월드맵처럼 읽힌다 — SelfCheck가 Party=null을 단언하는 이유.
        /// 허브 5칸만 아틀라스 조각이 화면과 같다. 없으면 아이콘 없이 제목만.
        /// </summary>
        protected virtual string HeaderIcon =>
            UiAtlas.HeaderKey(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

        /// <summary>
        /// 이 화면의 배경 그림(`Resources/bg/<이름>`). null이면 예전처럼 단색으로 칠한다.
        ///
        /// 전투 밖 화면 6종이 전부 **검은 배경에 글자 버튼**이었다(오너 질문 2026-08-15
        /// 「UI랑 배경은 언제 만드니」). 배경은 아이콘보다 싸고(화면당 1장) 효과가 크다 —
        /// 화면이 「여기가 어디인지」부터 말해준다.
        /// ⚠️ 그림이 없으면 **조용히 단색으로** 돌아간다. 아트가 늦게 와도 화면이 깨지지 않는다.
        /// </summary>
        protected virtual string BackgroundArt => null;

        // 기준 해상도 — 모든 좌표는 이 안에서 계산한다
        protected const float REF_W = 1280f, REF_H = 720f;
        protected const float BarH = 76f;
        protected const float RowH = 58f, RowGap = 14f, RowBtnW = 300f;

        GUIStyle _h1, _h2, _btn, _btnLeft, _small, _navLabel, _panel;
        Texture2D _bg, _line, _accent, _scrim;

        static readonly Color Ink = new Color(0.93f, 0.94f, 0.98f);
        static readonly Color Dim = new Color(0.62f, 0.65f, 0.75f);
        static readonly Color Gold = new Color(0.95f, 0.79f, 0.42f);

        protected virtual void Awake()
        {
            Application.runInBackground = true;
            EnsureCamera();
        }

        void EnsureCamera()
        {
            if (Camera.main != null) return;
            var go = new GameObject("Main Camera", typeof(Camera));
            go.tag = "MainCamera";
            var cam = go.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            go.transform.position = new Vector3(0, 0, -10);
        }

        protected virtual void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (!ShowBottomBar && Title == "재와 별") GameFlow.Quit();
                else GameFlow.Go(GameFlow.Estate);
            }
        }

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        void Styles()
        {
            if (_h1 != null) return;
            _h1 = new GUIStyle(GUI.skin.label) { fontSize = 46, fontStyle = FontStyle.Bold, normal = { textColor = Ink } };
            _h2 = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true, normal = { textColor = Dim } };
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            _btnLeft = new GUIStyle(_btn) { alignment = TextAnchor.MiddleLeft, fontSize = 20, padding = new RectOffset(4, 8, 0, 0) };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = Dim } };
            // 하단 탭은 아이콘과 이름을 세로로 나눈다. 기본 label은 좌측 정렬이라
            // 아이콘 아래 이름이 제각각 밀려 보이므로, 탭 전용으로 가운데 정렬한다.
            _navLabel = new GUIStyle(_small) { alignment = TextAnchor.UpperCenter };
            _panel = new GUIStyle(GUI.skin.label) { fontSize = 17, normal = { textColor = Gold } };
            _bg = Solid(new Color(0.05f, 0.05f, 0.08f));
            _line = Solid(new Color(1f, 1f, 1f, 0.10f));
            _accent = Solid(new Color(0.95f, 0.79f, 0.42f, 0.85f));
            _scrim = Solid(new Color(0.03f, 0.03f, 0.05f, 0.72f));
        }

        Texture2D _bgArt;
        bool _bgTried;

        /// <summary>배경 그림을 한 번만 찾아 기억한다. 없으면 다시 찾지 않는다.</summary>
        Texture2D BgTex()
        {
            if (_bgTried) return _bgArt;
            _bgTried = true;
            var key = BackgroundArt;
            if (!string.IsNullOrEmpty(key)) _bgArt = Resources.Load<Texture2D>("bg/" + key);
            return _bgArt;
        }

        void OnGUI()
        {
            Styles();

            // 기준 해상도로 스케일 — 비율은 유지하고 남는 쪽은 가운데 정렬한다
            float s = Mathf.Min(Screen.width / REF_W, Screen.height / REF_H);
            var offset = new Vector3((Screen.width - REF_W * s) * 0.5f, (Screen.height - REF_H * s) * 0.5f, 0);
            var saved = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(s, s, 1f));

            if (OpaqueBackground)
            {
                var art = BgTex();
                if (art != null)
                {
                    // 16:9 그림을 기준 해상도에 꽉 채운다. 그 위에 어두운 막을 한 겹 덮어
                    // **글자 가독성을 지킨다** — 배경이 아무리 좋아도 읽히지 않으면 손해다.
                    GUI.DrawTexture(new Rect(0, 0, REF_W, REF_H), art, ScaleMode.ScaleAndCrop);
                    GUI.DrawTexture(new Rect(0, 0, REF_W, REF_H), _scrim);
                }
                else GUI.DrawTexture(new Rect(0, 0, REF_W, REF_H), _bg);
            }

            if (ShowHeader)
            {
                // 제목판 — 배경을 안 깔 때는 글자가 묻히지 않게 살짝 어둡게 받쳐 준다
                if (!OpaqueBackground) GUI.DrawTexture(new Rect(0, 0, REF_W, 118), _scrim);
                GUI.DrawTexture(new Rect(0, 0, REF_W, 118), _line);
                GUI.DrawTexture(new Rect(0, 116, REF_W, 2), _accent);
                bool atlas = !string.IsNullOrEmpty(HeaderIcon)
                    && UiAtlas.Draw(new Rect(24, 18, 78, 78), HeaderIcon);
                GUI.Label(new Rect(atlas ? 124 : 48, 22, REF_W - (atlas ? 172 : 96), 56), Title, _h1);
                if (!string.IsNullOrEmpty(Subtitle))
                    GUI.Label(new Rect(50, 78, REF_W - 100, 30), Subtitle, _h2);
            }

            float bottom = ShowBottomBar ? BarH + 34f : 44f;
            bool preview = UiAtlas.QaShowButtonStates && ShowHeader;
            float previewH = preview ? RowH + 12f : 0f;
            Body(new Rect(48, 152, REF_W - 96, REF_H - 152 - bottom - previewH));
            if (preview)
                DrawButtonStatePreview(new Rect(48, REF_H - bottom - RowH, 600f, RowH));

            if (ShowBottomBar) BottomBar();
            // 배경을 안 까는 화면(전투)에서는 밝은 바닥 위에 글씨가 놓여 안 읽힌다 —
            // 안내 문구 뒤에도 판을 받친다(오너 지적 "글씨가 안보인다고")
            if (!OpaqueBackground) GUI.DrawTexture(new Rect(0, REF_H - 34, 360, 34), _scrim);
            GUI.Label(new Rect(48, REF_H - 28, 900, 22),
                      ShowBottomBar ? "ESC — 영지로" : "ESC — 뒤로", _small);
            Overlay();

            GUI.matrix = saved;
        }

        protected virtual void Overlay() { }

        protected abstract void Body(Rect r);

        void BottomBar()
        {
            float y = REF_H - BarH - 30f;
            GUI.DrawTexture(new Rect(0, y - 10, REF_W, 1), _line);

            int n = GameFlow.BottomBar.Length;
            float pad = 10f, w = (REF_W - 96 - pad * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                var (scene, label) = GameFlow.BottomBar[i];
                bool here = scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                var r = new Rect(48 + i * (w + pad), y, w, BarH);
                if (here) GUI.DrawTexture(new Rect(r.x, r.y - 4, r.width, 4), _accent);
                GUI.enabled = !here;
                // 탭 배경에는 이름을 그리지 않는다. 기존에는 버튼 중앙의 이름 위에
                // 아이콘을 덮어 그려서 "월드맵"처럼 글자가 반쯤 가려졌다.
                DrawAtlasButton(r, null);
                string icon = i switch
                {
                    0 => "territory",
                    1 => "field",
                    2 => "tower",
                    3 => "worldmap",
                    _ => "characters",
                };
                UiAtlas.Draw(new Rect(r.center.x - 22, r.y + 4, 44, 44), icon);
                GUI.Label(new Rect(r.x + 4, r.y + 53, r.width - 8, 19), label, _navLabel);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) GameFlow.Go(scene);
                GUI.enabled = true;
            }
        }

        /// <summary>텍스트와 클릭 판정은 IMGUI에 남기고, 배경만 새 픽셀아트 아틀라스로 교체한다.</summary>
        void DrawAtlasButton(Rect r, string label, bool locked = false, string iconKey = null, float leftPad = 0f,
                             bool? forceHover = null, bool? forcePressed = null)
        {
            bool hover = forceHover ?? (!locked && r.Contains(Event.current.mousePosition));
            bool pressed = forcePressed ?? (hover && Input.GetMouseButton(0));
            Color? tint = locked ? new Color(1f, 1f, 1f, 0.42f) : null;
            if (!UiAtlas.Draw(r, UiAtlas.ButtonKey(hover, pressed), tint))
                GUI.Box(r, GUIContent.none);
            bool hasIcon = ItemAtlas.DrawHud(new Rect(r.x + 8, r.y + 7, 44, 44), iconKey, tint);
            float pad = hasIcon ? 56f : leftPad;
            if (!string.IsNullOrEmpty(label))
            {
                var prev = GUI.color;
                if (locked) GUI.color = new Color(1f, 1f, 1f, 0.55f);
                var lr = pad > 0f ? new Rect(r.x + pad, r.y, r.width - pad - 8f, r.height) : r;
                GUI.Label(lr, label, pad > 0f ? _btnLeft : _btn);
                GUI.color = prev;
            }
        }

        /// <summary>
        /// qa_shot에는 마우스가 없다. QA_UI_STATES=1일 때만 보통·호버·눌림을 나란히 그린다.
        /// 플레이 화면에는 안 띄운다 — 견본이 실제 버튼처럼 읽히면 안 된다.
        /// </summary>
        void DrawButtonStatePreview(Rect origin)
        {
            Styles();
            const float w = 180f, gap = 12f;
            var samples = UiAtlas.ButtonStateSamples;
            for (int i = 0; i < samples.Length; i++)
            {
                var (hover, pressed, label) = samples[i];
                var br = new Rect(origin.x + i * (w + gap), origin.y, w, RowH);
                DrawAtlasButton(br, label, forceHover: hover, forcePressed: pressed);
            }
        }

        protected Rect RowButtonRect(Rect r, int index) =>
            new Rect(r.x, r.y + index * (RowH + RowGap), RowBtnW, RowH);

        protected Rect RowDescRect(Rect r, int index) =>
            new Rect(r.x + RowBtnW + 24, r.y + index * (RowH + RowGap), r.width - RowBtnW - 24, RowH);

        protected void Hint(Rect r, string text)
        {
            Styles();
            GUI.Label(r, text, _h2);
        }

        /// <summary>본문 버튼 한 줄. 왼쪽에 버튼, 오른쪽에 설명(근거 조문).</summary>
        protected bool Row(Rect r, int index, string label, string desc = "", string iconKey = null, float leftPad = 0f)
        {
            Styles();
            var br = RowButtonRect(r, index);
            if (br.yMax > r.yMax) return false;              // 영역을 넘으면 그리지 않는다

            DrawAtlasButton(br, label, iconKey: iconKey, leftPad: leftPad);
            bool hit = GUI.Button(br, GUIContent.none, GUIStyle.none);
            if (!string.IsNullOrEmpty(desc))
                GUI.Label(new Rect(br.xMax + 24, br.y + 8, r.width - RowBtnW - 24, RowH - 12), desc, _h2);
            return hit;
        }

        /// <summary>
        /// **아직 못 만든 기능**의 줄. 눌리지 않고, 왜 안 되는지 화면에서 말한다.
        ///
        /// 2026-08-15 기획서 대조 감사에서 본문이 `{ }`인 버튼 6개가 나왔다 — 누르면
        /// 아무 일도 안 일어난다. **미구현보다 나쁘다**: 사용자는 눌리는 버튼을 보고
        /// 되는 기능이라 믿고, 아무 반응이 없으면 게임이 고장났다고 읽는다.
        /// 만들 수 있으면 만들고, 못 만들면 **못 만들었다고 말하는 것**이 정직한 화면이다.
        /// </summary>
        protected void Locked(Rect r, int index, string label, string why, string iconKey = null)
        {
            Styles();
            var br = RowButtonRect(r, index);
            if (br.yMax > r.yMax) return;

            DrawAtlasButton(br, label, locked: true, iconKey: iconKey);
            GUI.Label(new Rect(br.xMax + 24, br.y + 8, r.width - RowBtnW - 24, RowH - 12),
                      // 이모지를 쓰지 않는다 — 기본 폰트에 자물쇠 글리프가 없어 □로 나온다(실측).
                      "잠김 — " + why, _small);
        }

        /// <summary>본문 안의 정보 한 줄(버튼 아님).</summary>
        protected void Info(Rect r, int index, string text)
        {
            Styles();
            var panel = new Rect(r.x - 12, r.y + index * (RowH + RowGap), r.width + 24, RowH);
            if (!UiAtlas.DrawSliced(panel, "panel", 14f, new Color(1f, 1f, 1f, 0.92f)))
                UiAtlas.Draw(panel, "panel", new Color(1f, 1f, 1f, 0.92f));
            GUI.Label(new Rect(r.x, r.y + index * (RowH + RowGap) + 14, r.width, 30), text, _panel);
        }
    }
}
