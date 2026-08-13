using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 모든 플레이 화면의 공통 껍데기 — 제목줄, §16 하단 고정바, ESC 처리.
    /// 각 화면은 Body()만 구현하면 된다.
    /// </summary>
    public abstract class GameScreen : MonoBehaviour
    {
        /// <summary>화면 제목. 상단에 그린다.</summary>
        protected abstract string Title { get; }
        /// <summary>이 화면이 무엇을 하는 곳인지 한 줄 설명(기획서 근거를 적어 둔다).</summary>
        protected virtual string Subtitle => "";
        /// <summary>하단바를 그릴지. 타이틀·전투·결과는 안 그린다.</summary>
        protected virtual bool ShowBottomBar => true;

        GUIStyle _h1, _h2, _btn, _small;
        protected const float BarH = 64f;

        protected virtual void Awake()
        {
            // 검증 빌드와 같은 규칙 — 창이 포커스를 잃어도 계속 돌아야 자동 점검이 가능하다
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
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.09f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            go.transform.position = new Vector3(0, 0, -10);
        }

        protected virtual void Update()
        {
            // ESC — 타이틀에서는 종료, 그 밖에서는 허브로. "빠져나갈 길이 없다"를 만들지 않는다.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Title == "재와 별") GameFlow.Quit();
                else GameFlow.Go(GameFlow.Estate);
            }
        }

        void Styles()
        {
            _h1 ??= new GUIStyle(GUI.skin.label) { fontSize = 30, normal = { textColor = Color.white } };
            _h2 ??= new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = new Color(.72f, .74f, .82f) } };
            _btn ??= new GUIStyle(GUI.skin.button) { fontSize = 16 };
            _small ??= new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = new Color(.55f, .57f, .65f) } };
        }

        void OnGUI()
        {
            Styles();
            GUI.Label(new Rect(24, 18, 900, 40), Title, _h1);
            if (!string.IsNullOrEmpty(Subtitle))
                GUI.Label(new Rect(26, 58, 1000, 24), Subtitle, _h2);

            Body(new Rect(24, 92, Screen.width - 48, Screen.height - 92 - (ShowBottomBar ? BarH + 16 : 56)));

            if (ShowBottomBar) BottomBar();
            GUI.Label(new Rect(24, Screen.height - 24, 700, 20),
                      ShowBottomBar ? "ESC — 영지로" : "ESC — 뒤로", _small);
        }

        /// <summary>화면 본문. rect는 제목줄과 하단바를 뺀 영역이다.</summary>
        protected abstract void Body(Rect r);

        void BottomBar()
        {
            Styles();
            float y = Screen.height - BarH - 8;
            float w = (Screen.width - 48) / GameFlow.BottomBar.Length;
            for (int i = 0; i < GameFlow.BottomBar.Length; i++)
            {
                var (scene, label) = GameFlow.BottomBar[i];
                bool here = scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                GUI.enabled = !here;   // 지금 화면은 눌러도 의미가 없다
                if (GUI.Button(new Rect(24 + i * w, y, w - 6, BarH), here ? $"[{label}]" : label, _btn))
                    GameFlow.Go(scene);
                GUI.enabled = true;
            }
        }

        /// <summary>본문 안에서 쓰는 버튼 한 줄. 반환값이 true면 눌린 것.</summary>
        protected bool Row(Rect r, int index, string label, string desc = "")
        {
            Styles();
            float h = 44f, gap = 8f;
            var br = new Rect(r.x, r.y + index * (h + gap), 260, h);
            bool hit = GUI.Button(br, label, _btn);
            if (!string.IsNullOrEmpty(desc))
                GUI.Label(new Rect(br.xMax + 14, br.y + 12, r.width - 300, 24), desc, _h2);
            return hit;
        }
    }
}
