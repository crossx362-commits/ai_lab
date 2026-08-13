using UnityEngine;

namespace AshesToStars
{
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

        /// <summary>
        /// 화면 전체를 불투명 배경으로 덮을지.
        /// ⚠️ 전투처럼 **카메라가 그린 장면을 보여줘야 하는 화면은 반드시 false**다.
        ///    IMGUI는 카메라 렌더 결과 **위에** 그려지므로, 배경을 깔면 전투가 통째로 가려진다.
        ///    실제로 그래서 "검은 화면만 나온다"는 보고가 나왔다(2026-08-13) —
        ///    전투는 정상 작동 중이었고 가려져 있었을 뿐이다.
        /// </summary>
        protected virtual bool OpaqueBackground => true;

        // 기준 해상도 — 모든 좌표는 이 안에서 계산한다
        protected const float REF_W = 1280f, REF_H = 720f;
        protected const float BarH = 76f;

        GUIStyle _h1, _h2, _btn, _small, _panel;
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
            _small = new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = Dim } };
            _panel = new GUIStyle(GUI.skin.label) { fontSize = 17, normal = { textColor = Gold } };
            _bg = Solid(new Color(0.05f, 0.05f, 0.08f));
            _line = Solid(new Color(1f, 1f, 1f, 0.10f));
            _accent = Solid(new Color(0.95f, 0.79f, 0.42f, 0.85f));
            _scrim = Solid(new Color(0.03f, 0.03f, 0.05f, 0.72f));
        }

        void OnGUI()
        {
            Styles();

            // 기준 해상도로 스케일 — 비율은 유지하고 남는 쪽은 가운데 정렬한다
            float s = Mathf.Min(Screen.width / REF_W, Screen.height / REF_H);
            var offset = new Vector3((Screen.width - REF_W * s) * 0.5f, (Screen.height - REF_H * s) * 0.5f, 0);
            var saved = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(s, s, 1f));

            if (OpaqueBackground) GUI.DrawTexture(new Rect(0, 0, REF_W, REF_H), _bg);

            // 제목판 — 배경을 안 깔 때는 글자가 묻히지 않게 살짝 어둡게 받쳐 준다
            if (!OpaqueBackground) GUI.DrawTexture(new Rect(0, 0, REF_W, 118), _scrim);
            GUI.DrawTexture(new Rect(0, 0, REF_W, 118), _line);
            GUI.DrawTexture(new Rect(0, 116, REF_W, 2), _accent);
            GUI.Label(new Rect(48, 22, REF_W - 96, 56), Title, _h1);
            if (!string.IsNullOrEmpty(Subtitle))
                GUI.Label(new Rect(50, 78, REF_W - 100, 30), Subtitle, _h2);

            float bottom = ShowBottomBar ? BarH + 34f : 44f;
            Body(new Rect(48, 152, REF_W - 96, REF_H - 152 - bottom));

            if (ShowBottomBar) BottomBar();
            // 배경을 안 까는 화면(전투)에서는 밝은 바닥 위에 글씨가 놓여 안 읽힌다 —
            // 안내 문구 뒤에도 판을 받친다(오너 지적 "글씨가 안보인다고")
            if (!OpaqueBackground) GUI.DrawTexture(new Rect(0, REF_H - 34, 360, 34), _scrim);
            GUI.Label(new Rect(48, REF_H - 28, 900, 22),
                      ShowBottomBar ? "ESC — 영지로" : "ESC — 뒤로", _small);

            GUI.matrix = saved;
        }

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
                if (GUI.Button(r, label, _btn)) GameFlow.Go(scene);
                GUI.enabled = true;
            }
        }

        /// <summary>본문 버튼 한 줄. 왼쪽에 버튼, 오른쪽에 설명(근거 조문).</summary>
        protected bool Row(Rect r, int index, string label, string desc = "")
        {
            Styles();
            const float h = 58f, gap = 14f, bw = 300f;
            var br = new Rect(r.x, r.y + index * (h + gap), bw, h);
            if (br.yMax > r.yMax) return false;              // 영역을 넘으면 그리지 않는다

            bool hit = GUI.Button(br, label, _btn);
            if (!string.IsNullOrEmpty(desc))
                GUI.Label(new Rect(br.xMax + 24, br.y + 8, r.width - bw - 24, h - 12), desc, _h2);
            return hit;
        }

        /// <summary>본문 안의 정보 한 줄(버튼 아님).</summary>
        protected void Info(Rect r, int index, string text)
        {
            Styles();
            const float h = 58f, gap = 14f;
            GUI.Label(new Rect(r.x, r.y + index * (h + gap) + 14, r.width, 30), text, _panel);
        }
    }
}
