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
        /// 도크 없는 화면이 바닥 왼쪽에 그리는 「ESC — 뒤로」 힌트를 표시할지.
        /// ⚠️ 전투는 W3Party 파티 카드가 바닥 전체를 소유해 이 힌트가 1번 카드 체력바와
        ///    겹쳐 회색 글씨가 흰 체력 수치를 뭉갠다(2026-08-19 실측 qa_boss.png). ESC 키
        ///    자체는 입력 처리라 그대로 살아 있으니, 전투에서는 힌트만 끈다.
        /// </summary>
        protected virtual bool ShowEscHint => true;

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
        /// <summary>제목판. 118px면 720p 본문이 카드 한 줄도 못 채운다. 슬림은 HubHeader.</summary>
        public static float HeaderH => HubHeader.H;
        public static float BodyTop => HubHeader.BodyTop;
        public const float BodyPadX = 36f;
        protected const float RowH = 64f, RowGap = 12f, RowBtnW = 360f;
        // 본문 행 그리드의 세로 피치·행 높이. **기본은 76/64로 기존 화면과 동일**(RowH+RowGap).
        // 밀도가 높은 패널(캐릭터 속성 탭)만 이 둘을 컴팩트 값으로 낮춰, 같은 index 그리드로
        // 더 많은 행을 넘침 없이 담는다 — 개별 커스텀 버튼은 private DrawAtlasButton 탓에 못
        // 만드므로, 공유 Row/Locked/Info가 읽는 이 필드를 조절하는 것이 최소 변경이다. 값을
        // 바꾼 화면은 반드시 그린 뒤 되돌린다(RowPitch=RowH+RowGap; RowHt=RowH).
        protected float RowPitch = RowH + RowGap;
        protected float RowHt = RowH;

        public const string EnvNoCompactInfoFit = "QA_NO_COMPACT_INFO_FIT";

        /// <summary>
        /// 48px 미만 정보 칸의 글자 영역. 예전 고정 20px는 22px 픽셀 폰트의 한글
        /// 아랫획을 잘랐다. 패널 안쪽 6px만 남겨 실제 높이를 쓰고, QA_NO는 비교 샷용 옛 칸이다.
        /// </summary>
        public static Rect CompactInfoTextRect(Rect panel)
        {
            bool blocked = System.Environment.GetEnvironmentVariable(EnvNoCompactInfoFit) == "1";
            return blocked
                ? new Rect(panel.x + 16f, panel.y + 8f, panel.width - 32f, 20f)
                : new Rect(panel.x + 16f, panel.y + 6f, panel.width - 32f,
                    Mathf.Max(20f, panel.height - 12f));
        }

        GUIStyle _h1, _h2, _h1Slim, _h2Slim, _btn, _btnLeft, _small, _navLabel, _panel, _cardTitle, _tab;
        // 아이콘 없는 카드(예: 타이틀 「종료」)를 형제 카드와 어긋나 보이지 않게 중앙 정렬로 그린다.
        GUIStyle _cardTitleC, _h2C;
        Texture2D _bg, _line, _accent, _scrim;

        static readonly Color Ink = new Color(0.93f, 0.94f, 0.98f);
        static readonly Color Dim = new Color(0.62f, 0.65f, 0.75f);
        static readonly Color Gold = new Color(0.95f, 0.79f, 0.42f);

        protected virtual void Awake()
        {
            Application.runInBackground = true;
            EnsureCamera();
            DebugAutoPilot.BootstrapIfRequested();
            LocalPlayKit.ApplyIfNeeded();
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
            StarterSecond.Tick(Time.unscaledDeltaTime);
            HuntSchedule.Tick(Time.unscaledDeltaTime);
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
            _h1 = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, normal = { textColor = Ink } };
            _h2 = new GUIStyle(GUI.skin.label) { fontSize = UiPages.CardSubFont, wordWrap = true, normal = { textColor = Dim } };
            _h1Slim = new GUIStyle(_h1) { fontSize = UiPages.SlimTitleFont };
            _h2Slim = new GUIStyle(_h2) { fontSize = UiPages.SlimSubFont };
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            _btnLeft = new GUIStyle(_btn) { alignment = TextAnchor.MiddleLeft, fontSize = 20, padding = new RectOffset(4, 8, 0, 0) };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, normal = { textColor = Dim } };
            _cardTitle = new GUIStyle(_h1) { fontSize = UiPages.CardTitleFont, alignment = TextAnchor.MiddleLeft };
            _cardTitleC = new GUIStyle(_cardTitle) { alignment = TextAnchor.MiddleCenter };
            _h2C = new GUIStyle(_h2) { alignment = TextAnchor.MiddleCenter };
            _tab = new GUIStyle(_small)
            {
                fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Ink },
            };
            // 하단 탭은 아이콘과 이름을 세로로 나눈다. 기본 label은 좌측 정렬이라
            // 아이콘 아래 이름이 제각각 밀려 보이므로, 탭 전용으로 가운데 정렬한다.
            _navLabel = new GUIStyle(_small) { alignment = TextAnchor.UpperCenter };
            _panel = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true, normal = { textColor = Gold } };
            _bg = Solid(new Color(0.05f, 0.05f, 0.08f));
            _line = Solid(new Color(1f, 1f, 1f, 0.10f));
            _accent = Solid(new Color(0.95f, 0.79f, 0.42f, 0.85f));
            _scrim = Solid(new Color(0.04f, 0.04f, 0.06f, 0.38f));
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
                    // 가독성 막은 유지하되 배경 그림이 죽지 않게 한 겹만.
                }
                else GUI.DrawTexture(new Rect(0, 0, REF_W, REF_H), _bg);
            }

            if (ShowHeader)
            {
                HubHeader.SeedQaIfRequested();
                float h = HeaderH;
                // 제목판 — 배경을 안 깔 때는 글자가 묻히지 않게 살짝 어둡게 받쳐 준다
                if (!OpaqueBackground) GUI.DrawTexture(new Rect(0, 0, REF_W, h), _scrim);
                GUI.DrawTexture(new Rect(0, 0, REF_W, h), _line);
                GUI.DrawTexture(new Rect(0, h - 2, REF_W, 2), _accent);
                var icon = HubHeader.IconRect();
                bool atlas = !string.IsNullOrEmpty(HeaderIcon)
                    && UiAtlas.DrawFit(icon, HeaderIcon);
                var titleStyle = HubHeader.Blocked ? _h1 : _h1Slim;
                var subStyle = HubHeader.Blocked ? _h2 : _h2Slim;
                string shownSub = HubHeader.ShowQa ? HubHeader.Line() : Subtitle;
                UiPages.LabelClip(HubHeader.TitleRect(atlas), Title, titleStyle);
                if (!string.IsNullOrEmpty(shownSub))
                    UiPages.LabelClip(HubHeader.SubtitleRect(atlas), shownSub, subStyle);
            }

            float bottom = ShowBottomBar ? UiPages.NavReserve : 36f;
            bool buttonPreview = UiAtlas.QaShowButtonStates && ShowHeader;
            bool rarityPreview = ShowRarityPreview;
            bool bossHpPreview = ShowBossHpPreview;
            float previewH = 0f;
            if (buttonPreview) previewH += RowH + 12f;
            if (rarityPreview) previewH += 80f;
            if (bossHpPreview) previewH += 132f;
            BodyNav.SeedQaIfRequested();
            var body = new Rect(BodyPadX, BodyTop, REF_W - BodyPadX * 2f,
                REF_H - BodyTop - bottom - previewH);
            // NavReserve=80이면 yMax=640인데 내비 플레이트는 636이라 하단 금테가 4px 겹친다.
            // 화면마다 Hud.NavGap을 복제하지 않고 여기서 한 번 자른다. QA_NO면 옛 640.
            Body(BodyNav.Fit(body, ShowBottomBar));
            float previewY = REF_H - bottom;
            if (bossHpPreview)
            {
                previewY -= 124f;
                DrawBossHpPreview(new Rect(48, previewY, 720f, 116f));
                previewY -= 8f;
            }
            if (rarityPreview)
            {
                previewY -= 72f;
                DrawRarityPreview(new Rect(48, previewY, 720f, 72f));
                previewY -= 8f;
            }
            if (buttonPreview)
                DrawButtonStatePreview(new Rect(48, previewY - RowH, 600f, RowH));

            if (ShowBottomBar) BottomBar();
            else
            {
                // 도크가 없는 화면만 바닥에 ESC를 둔다. 허브는 도크 왼쪽 빈 칸이 소비처다.
                // 전투는 파티 카드가 바닥을 소유하므로 힌트를 끈다(ShowEscHint=false).
                if (ShowEscHint)
                {
                    if (!OpaqueBackground) GUI.DrawTexture(new Rect(0, REF_H - 34, 360, 34), _scrim);
                    // 타이틀(루트)에서 ESC는 뒤가 없어 `GameFlow.Quit()`이다(Update 참조). 그
                    // 화면에서 「뒤로」라 적으면 ESC로 게임이 닫히는 걸 「돌아간다」로 오해한다.
                    string escHint = Title == "재와 별" ? "ESC — 종료" : "ESC — 뒤로";
                    UiPages.LabelClip(new Rect(48, REF_H - 28, 280, 22), escHint, _small);
                }
            }
            Overlay();

            GUI.matrix = saved;
        }

        protected virtual void Overlay() { }

        /// <summary>등급 견본은 캐릭터 상세처럼 빈 칸이 있는 화면만. 대장간에 띄우면 명부를 덮는다.</summary>
        protected virtual bool ShowRarityPreview => false;

        /// <summary>보스 HP 견본은 탑처럼 보스전이 다음인 화면만. 영지에 띄우면 건물 줄을 덮는다.</summary>
        protected virtual bool ShowBossHpPreview => false;

        protected abstract void Body(Rect r);

        void BottomBar()
        {
            int n = GameFlow.BottomBar.Length;
            var tiles = UiPages.NavDock(n, REF_W, REF_H);
            float used = tiles[n - 1].xMax - tiles[0].x;
            var plate = new Rect(tiles[0].x - 10f, tiles[0].y - 6f, used + 20f, UiPages.NavTileH + 12f);
            if (!UiAtlas.DrawSliced(plate, "panel", 12f, new Color(1f, 1f, 1f, 0.72f)))
                GUI.DrawTexture(new Rect(0, tiles[0].y - 8f, REF_W, 1), _line);

            // ESC는 도크 왼쪽 빈 칸. 아래에 한 줄을 더 깔면 본문이 28px 죽는다.
            // ⚠️ 영지(허브 루트)에서 ESC는 GameFlow.Go(Estate) = 제자리라 갈 곳이 없다(Update 참조).
            //    그 화면에서 「ESC — 영지로」라 적으면 눌러서 어딘가 간다고 오해한다(타이틀 「ESC — 종료」와 동형).
            //    실제로 영지로 이동하는 다른 허브(필드·탑·월드맵·캐릭터)에서만 표시한다.
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != GameFlow.Estate)
                UiPages.LabelClip(new Rect(16f, tiles[0].y + 24f, Mathf.Max(40f, tiles[0].x - 24f), 22f),
                    "ESC — 영지로", _small);

            for (int i = 0; i < n; i++)
            {
                var (scene, label) = GameFlow.BottomBar[i];
                bool here = scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                var r = tiles[i];
                if (here) GUI.DrawTexture(new Rect(r.x + 12f, r.y - 4f, r.width - 24f, 3f), _accent);
                GUI.enabled = !here;
                DrawAtlasButton(r, null);
                // 현재 탭도 아이콘·라벨은 선명하게 — GUI.enabled=false의 비활성 알파가 활성 탭
                // 글자를 회색으로 지워 위 강조선(「여기가 현재」)과 정반대로 읽혔다(2026-08-24 실측).
                // 클릭 금지는 아래 !here 가드가 담당한다. QA_NO_DOCK_ACTIVE_BRIGHT=1이면 옛 경로.
                if (!(here && System.Environment.GetEnvironmentVariable("QA_NO_DOCK_ACTIVE_BRIGHT") == "1"))
                    GUI.enabled = true;
                string icon = UiPages.NavIcon(scene);
                UiAtlas.DrawFit(new Rect(r.center.x - 18f, r.y + 4f, 36f, 36f), icon);
                UiPages.LabelClip(new Rect(r.x + 2f, r.y + 42f, r.width - 4f, 24f), label, _navLabel);
                if (!here && GUI.Button(r, GUIContent.none, GUIStyle.none)) GameFlow.Go(scene);
                GUI.enabled = true;
            }
        }

        /// <summary>텍스트와 클릭 판정은 IMGUI에 남기고, 배경만 새 픽셀아트 아틀라스로 교체한다.</summary>
        void DrawAtlasButton(Rect r, string label, bool locked = false, string iconKey = null, float leftPad = 0f,
                             bool? forceHover = null, bool? forcePressed = null, GearGrade? rarity = null)
        {
            bool hover = forceHover ?? (!locked && r.Contains(Event.current.mousePosition));
            bool pressed = forcePressed ?? (hover && Input.GetMouseButton(0));
            Color? tint = locked ? new Color(1f, 1f, 1f, 0.42f) : null;
            string btnKey = UiAtlas.ButtonKey(hover, pressed);
            if (!UiAtlas.DrawSliced(r, btnKey, 12f, tint)
                && !UiAtlas.Draw(r, btnKey, tint))
                GUI.Box(r, GUIContent.none);
            var inner = UiAtlas.ContentRect(r, btnKey, 2f);
            float ih = Mathf.Min(44f, inner.height);
            var iconRect = new Rect(inner.x, inner.y + (inner.height - ih) * 0.5f, ih, ih);
            if (rarity.HasValue)
                UiAtlas.DrawRarity(new Rect(iconRect.x - 4, iconRect.y - 4, ih + 8f, ih + 8f),
                    rarity.Value, tint);
            bool hasIcon = ItemAtlas.DrawHud(iconRect, iconKey, tint);
            float pad = hasIcon ? ih + 8f : leftPad;
            if (!string.IsNullOrEmpty(label))
            {
                var prev = GUI.color;
                if (locked) GUI.color = new Color(1f, 1f, 1f, 0.55f);
                var lr = pad > 0f
                    ? new Rect(inner.x + pad, inner.y, Mathf.Max(8f, inner.width - pad), inner.height)
                    : inner;
                UiPages.LabelClip(lr, label, pad > 0f ? _btnLeft : _btn);
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

        /// <summary>qa_shot에 마우스가 없어 보스전 한 판을 기다리지 않고 프레임·경계선을 본다.</summary>
        void DrawBossHpPreview(Rect origin)
        {
            Styles();
            var samples = UiAtlas.BossHpSamples;
            float h = 28f, gap = 8f;
            for (int i = 0; i < samples.Length; i++)
            {
                var (current, max, phases, label) = samples[i];
                var bar = new Rect(origin.x, origin.y + i * (h + gap), origin.width - 160f, h);
                UiAtlas.DrawBossHp(bar, current, max, phases);
                UiPages.LabelClip(new Rect(bar.xMax + 10f, bar.y + 4f, 150f, 22f), label, _small);
            }
        }

        /// <summary>qa_shot에 마우스가 없어 5등급이 한 화면에 안 모인다. 견본만 나란히 그린다.</summary>
        void DrawRarityPreview(Rect origin)
        {
            Styles();
            var samples = UiAtlas.RaritySamples;
            float cell = Mathf.Min(88f, origin.width / samples.Length);
            for (int i = 0; i < samples.Length; i++)
            {
                var (grade, label) = samples[i];
                var frame = new Rect(origin.x + i * cell, origin.y, 56f, 56f);
                UiAtlas.DrawRarity(frame, grade);
                ItemAtlas.DrawHud(new Rect(frame.x + 10f, frame.y + 10f, 36f, 36f), "sword");
                UiPages.LabelClip(new Rect(frame.x, frame.yMax - 2f, cell - 8f, 18f), label, _small);
            }
        }

        protected Rect RowButtonRect(Rect r, int index) =>
            new Rect(r.x, r.y + index * RowPitch, RowBtnW, RowHt);

        protected Rect RowDescRect(Rect r, int index) =>
            new Rect(r.x + RowBtnW + 24, r.y + index * RowPitch, r.width - RowBtnW - 24, RowHt);

        protected void Hint(Rect r, string text)
        {
            Styles();
            // 자르지 말고 줄여서 넣는다 — 배너 높이를 줄이면 18px 글꼴이 바로 반토막 난다.
            UiPages.LabelFit(r, text, _h2);
        }

        /// <summary>본문 버튼 한 줄. 왼쪽에 버튼, 오른쪽에 설명(근거 조문).</summary>
        protected bool Row(Rect r, int index, string label, string desc = "", string iconKey = null, float leftPad = 0f,
                           GearGrade? rarity = null)
        {
            Styles();
            var br = RowButtonRect(r, index);
            if (br.yMax > r.yMax) return false;              // 영역을 넘으면 그리지 않는다

            DrawAtlasButton(br, label, iconKey: iconKey, leftPad: leftPad, rarity: rarity);
            bool hit = GUI.Button(br, GUIContent.none, GUIStyle.none);
            if (!string.IsNullOrEmpty(desc))
                UiPages.LabelClip(new Rect(br.xMax + 24, br.y + 8, r.width - RowBtnW - 24, RowHt - 12), desc, _h2);
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
        protected void Locked(Rect r, int index, string label, string why, string iconKey = null,
                              GearGrade? rarity = null)
        {
            Styles();
            var br = RowButtonRect(r, index);
            if (br.yMax > r.yMax) return;

            DrawAtlasButton(br, label, locked: true, iconKey: iconKey, rarity: rarity);
            UiPages.LabelClip(new Rect(br.xMax + 24, br.y + 8, r.width - RowBtnW - 24, RowHt - 12),
                      "잠김 — " + why, _small);
        }

        /// <summary>페이지 탭. 선택한 인덱스를 돌려준다.</summary>
        protected int DrawTabs(Rect r, string[] names, int selected)
        {
            Styles();
            if (names == null || names.Length == 0) return 0;
            float w = Mathf.Min(140f, (r.width - UiPages.TabGap * (names.Length - 1)) / names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                var t = new Rect(r.x + i * (w + UiPages.TabGap), r.y, w, UiPages.TabH);
                bool on = i == selected;
                string tabKey = UiAtlas.ButtonKey(false, on);
                UiAtlas.DrawSliced(t, tabKey, 8f,
                    on ? (Color?)null : new Color(1f, 1f, 1f, 0.62f));
                UiPages.LabelClip(UiAtlas.ContentRect(t, tabKey, 2f), names[i], _tab);
                if (GUI.Button(t, GUIContent.none, GUIStyle.none)) selected = i;
            }
            return selected;
        }

        /// <summary>허브 카드. 잠기면 클릭되지 않고 사유를 카드 안에 적는다.</summary>
        /// <param name="alpha">1 미만이면 카드가 반투명해져 뒤가 비친다(전투 중 보너스 선택).</param>
        protected bool DrawCard(Rect card, string title, string sub, string iconKey = null, bool locked = false,
                                float alpha = 1f, bool center = false)
        {
            Styles();
            var tint = locked ? new Color(1f, 1f, 1f, 0.55f) : new Color(1f, 1f, 1f, 0.94f);
            if (alpha < 1f) tint.a *= alpha;
            string chrome = UiPages.CardChrome(card);
            // 금테 두께는 CardLayout이 글씨 칸을 낼 때 쓰는 값과 **같아야** 한다(UiPages.CardPad).
            if (!UiAtlas.DrawSliced(card, chrome, 16f, tint, UiPages.CardPad(card)))
                UiAtlas.Draw(card, chrome, tint);
            bool hasIcon = !string.IsNullOrEmpty(iconKey);
            UiPages.CardLayout(card, hasIcon, out var icon, out var titleR, out var subR);
            if (hasIcon) UiAtlas.DrawFit(icon, iconKey, tint);
            bool slim = UiPages.IsSlimCard(card);
            // 아이콘 없는 카드만 중앙 정렬 허용 — 아이콘 칸이 비어 좌측에 죽은 여백이 생기는 것을 막는다.
            bool centerText = center && !hasIcon && !slim;
            // 자르지 말고 줄여서 넣는다 — 잠긴 카드는 「잠김 — 」이 붙어 한 줄이 더 는다.
            var savedColor = GUI.color;
            if (alpha < 1f) GUI.color = new Color(1f, 1f, 1f, alpha);
            UiPages.LabelFit(titleR, title, centerText ? _cardTitleC : slim ? _h1Slim : _cardTitle);
            UiPages.LabelFit(subR, locked ? "잠김 — " + sub : sub,
                centerText ? _h2C : slim ? _h2Slim : locked ? _small : _h2);
            GUI.color = savedColor;
            if (locked) return false;
            return GUI.Button(card, GUIContent.none, GUIStyle.none);
        }

        /// <summary>경고·결과처럼 아래 두 장을 고르는 줄. 본문 Info는 위에 그대로 둔다.</summary>
        protected bool DrawChoice(Rect r, string okTitle, string okSub, string okIcon,
                                  string noTitle, string noSub, string noIcon, out bool cancelled)
        {
            cancelled = false;
            float h = Mathf.Min(168f, Mathf.Max(100f, r.height * 0.42f));
            var cells = UiPages.Grid(new Rect(r.x, r.yMax - h, r.width, h), 2, 1, 16f);
            bool ok = DrawCard(cells[0], okTitle, okSub, okIcon);
            if (DrawCard(cells[1], noTitle, noSub, noIcon)) cancelled = true;
            return ok;
        }

        /// <summary>본문 안의 정보 한 줄(버튼 아님).</summary>
        protected void Info(Rect r, int index, string text)
        {
            var panel = new Rect(r.x - 12, r.y + index * RowPitch, r.width + 24, RowHt);
            if (panel.yMax > r.yMax) return;
            InfoAt(panel, text);
        }

        /// <summary>
        /// 긴 안내를 칸 안에서 접어 넣는다. Info→InfoAt은 슬림 칸 inner 높이 20px +
        /// LabelClip이라 마법사 SkillDescLine이 「빙결: 광」에서 우측이 잘렸다.
        /// 높이는 남은 본문과 최소 두 줄(52) 중 작은 쪽. 소비한 행 수를 돌려 다음
        /// index가 겹치지 않게 한다(0이면 그릴 자리 없음).
        /// </summary>
        protected int InfoWrap(Rect r, int index, string text) => InfoWrap(r, index, text, 52f);

        /// <summary>
        /// <paramref name="minH"/>로 최소 높이를 줄 수 있다. 속성 탭 컴팩트 피치(46/40)에서
        /// 기본 52를 쓰면 ceil(52/46)=2칸이 되어 초필 줄이 밀린다 — 한 행이면 RowHt.
        /// </summary>
        protected int InfoWrap(Rect r, int index, string text, float minH)
        {
            Styles();
            if (string.IsNullOrEmpty(text)) return 0;
            float y = r.y + index * RowPitch;
            float remain = r.yMax - y;
            if (remain < 24f) return 0;
            float h = Mathf.Min(remain, Mathf.Max(RowHt, minH));
            var panel = new Rect(r.x - 12, y, r.width + 24, h);
            if (!UiAtlas.DrawSliced(panel, "panel", 14f, new Color(1f, 1f, 1f, 0.92f)))
                UiAtlas.Draw(panel, "panel", new Color(1f, 1f, 1f, 0.92f));
            var inner = new Rect(panel.x + 16f, panel.y + 6f,
                panel.width - 32f, Mathf.Max(16f, panel.height - 12f));
            UiPages.LabelFit(inner, text, _panel);
            return Mathf.Max(1, Mathf.CeilToInt((h + 0.01f) / RowPitch));
        }

        /// <summary>이미 계산된 칸에 안내만 그린다. 경매 슬림 도크가 읽는다.</summary>
        protected void InfoAt(Rect panel, string text) => InfoAt(panel, text, 0f);

        /// <summary>칸에 안내를 그리되, 글씨 시작 x를 <paramref name="absTextLeft"/>로
        /// **절대 좌표** 고정한다(0이면 기본 content 칸). 결과 화면 보상 줄처럼 왼쪽에
        /// 아이콘을 겹쳐 그릴 때, 폰트 공백폭에 기대지 않고 아이콘 폭만큼 글씨를 확실히
        /// 밀어 첫 글자가 아이콘에 먹히지 않게 한다.</summary>
        protected void InfoAt(Rect panel, string text, float absTextLeft)
        {
            Styles();
            if (panel.yMax > REF_H) return;
            if (!UiAtlas.DrawSliced(panel, "panel", 14f, new Color(1f, 1f, 1f, 0.92f)))
                UiAtlas.Draw(panel, "panel", new Color(1f, 1f, 1f, 0.92f));
            // 슬림 칸도 실제 안쪽 높이를 써야 22px 픽셀 폰트의 한글 아랫획이 잘리지 않는다.
            // 전폭 64 Info는 아틀라스 ContentRect를 유지한다.
            var inner = panel.height < 48f
                ? CompactInfoTextRect(panel)
                : UiAtlas.ContentRect(panel, "panel", 2f);
            if (absTextLeft > inner.x)
                inner = new Rect(absTextLeft, inner.y, Mathf.Max(4f, inner.xMax - absTextLeft), inner.height);
            UiPages.LabelClip(inner, text, _panel);
        }
    }
}
