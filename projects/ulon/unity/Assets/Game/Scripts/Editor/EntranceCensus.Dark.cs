using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    /// <summary>
    /// **문구멍이 어둠으로 읽히나 — 밝기로 잰다**(검수 판정 2026-09-09).
    ///
    /// 눈을 진입로로 옮기자 문짝이 **앞면**을 보이게 됐고, 입구 등불 둘을 정면으로 받아 화면에서는
    /// 검정이 아니라 **어두운 갈색 나무 문짝**으로 읽혔다. 등불 자리와 조명은 안 건드린다
    /// (등불 대칭이 이 샷의 힘이고, 조명은 스물한 샷을 같이 흔든다) — **재질 하나**만 고친다.
    ///
    /// 그런데 「얼마나 어둡게」를 상수로 고르면 그건 취향이다. 그래서 **목표를 화면에서 정의한다**:
    /// 문구멍 픽셀의 밝기가 **바깥 지표 밝기의 몇 %**인가. 알베도는 그 목표에서 유도한다
    /// (밝기 ≈ 알베도 × 조도이므로, 목표/실측 비율만큼 곱하면 된다).
    ///
    /// **이 자가 못 보는 것**: 색상(갈색인지 회색인지)은 안 묻는다 — 밝기만 잰다. 색은 화면이 답한다.
    /// </summary>
    public static partial class EntranceCensus
    {
        /// <summary>화면 한 장을 **자와 같은 눈·같은 크기**로 렌더한다 — 픽셀 번호가 실루엣과 맞아야 한다.</summary>
        static Color[] RenderShot(Vector3 eye, Vector3 look)
        {
            var camGo = new GameObject("PortalDarkProbeCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;                 // ShotFov
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;
            camGo.transform.position = eye;
            camGo.transform.LookAt(look);
            var rt = new RenderTexture(ScreenW, ScreenH, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(ScreenW, ScreenH, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, ScreenW, ScreenH), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            var px = tex.GetPixels();
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
            return px;
        }

        /// <summary>
        /// 문구멍 밝기 ÷ 바깥 지표 밝기. **지표는 화면 아래 5분의 1**에서 띠를 뺀 자리로 잡는다
        /// (진입로에 선 눈이므로 그 자리는 언제나 발밑 땅이다).
        ///
        /// 실루엣은 위에서 아래로 세고(`Project`의 sy), 렌더 텍스처는 **아래에서 위로** 센다 —
        /// 뒤집어 맞춘다. 뒤집지 않으면 하늘을 문이라고 부른다(그래서 로그에 뒤집은 값도 같이 찍는다).
        /// </summary>
        public static bool ReadMouthDark(string rootName, float ex, float ez, float yaw,
                                         out float ratio, out float mouthLum, out float groundLum, out string what)
        {
            ratio = mouthLum = groundLum = 0f;
            if (!MouthBand(rootName, ex, ez, yaw, out Silhouette band, out Vector3 eye, out Vector3 look, out what))
                return false;
            // **띠 전체를 재면 문설주를 문이라고 부른다** — 띠는 두 기둥 「중심 사이」라 기둥 안쪽
            // 절반이 들어온다(첫 판이 그랬다: 재질을 바꿔도 50%→41%밖에 안 움직였는데, 움직이지
            // 않은 몫이 곧 돌기둥이었다). 그래서 **포털이 앞에 선 픽셀만** 센다 — 채움 자와 같은 규칙.
            var rootTr = GameObject.Find(rootName);
            var portalTr = rootTr == null ? null : FindChild(rootTr.transform, VisualSliceBuilder.EntrancePortalObject);
            var portalSil = portalTr == null ? new Silhouette() : Draw(portalTr, eye, look);
            // **앞을 무엇이 가리면 그 픽셀은 문이 아니다** — 기둥이 판 앞에 서는 자리가 있어서
            // 마스크만 믿으면 밝은 돌을 문이라고 부른다(첫 판 실측: 문구멍 최대 밝기 1.000).
            var allSil = rootTr == null ? new Silhouette() : Draw(rootTr.transform, eye, look);
            var px = RenderShot(eye, look);
            double mSum = 0.0, gSum = 0.0, flipSum = 0.0;
            float mMin = 9f, mMax = -1f;
            int mN = 0, gN = 0, flipN = 0;
            for (int r = 0; r < ScreenH; r++)
                for (int c = 0; c < ScreenW; c++)
                {
                    int mask = r * ScreenW + c;
                    int shot = (ScreenH - 1 - r) * ScreenW + c;
                    bool inBand = band.Depth[mask] < float.MaxValue &&
                                  portalSil.Depth[mask] < band.Depth[mask] + PortalNearBand &&
                                  allSil.Depth[mask] > portalSil.Depth[mask] - 0.05f;
                    if (inBand)
                    {
                        float v = px[shot].grayscale;
                        mSum += v; mN++;
                        mMin = Mathf.Min(mMin, v); mMax = Mathf.Max(mMax, v);
                        flipSum += px[mask].grayscale; flipN++;
                    }
                    else if (r > ScreenH * 4 / 5)
                    {
                        gSum += px[shot].grayscale; gN++;
                    }
                }
            if (mN == 0 || gN == 0) { what += " · 잰 픽셀이 없다"; return false; }
            mouthLum = (float)(mSum / mN);
            groundLum = (float)(gSum / gN);
            ratio = groundLum > 0.001f ? mouthLum / groundLum : 1f;
            what += " · 문구멍 " + mN + "px 밝기 " + mouthLum.ToString("0.000") + " · 발밑 " + gN + "px 밝기 " +
                    groundLum.ToString("0.000") + " · 문구멍 최소 " + mMin.ToString("0.000") + "/최대 " + mMax.ToString("0.000") + " · (뒤집어 재면 " + (flipSum / flipN).ToString("0.000") + ")";
            return true;
        }

        public static void RunMouthDark()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            DarkReport("07/D1", Dungeon1.RootObject, Dungeon1.EntranceX, Dungeon1.EntranceZ, Dungeon1.EntranceYaw);
            DarkReport("09/D2", Dungeon2.RootObject, Dungeon2.EntranceX, Dungeon2.EntranceZ, Dungeon2.EntranceYaw);
            DarkReport("11/D3", Dungeon3.RootObject, Dungeon3.EntranceX, Dungeon3.EntranceZ, Dungeon3.EntranceYaw);
            var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Art/Env/DungeonPortal.mat");
            int users = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var m in r.sharedMaterials)
                    if (m == mat) { users++; break; }
            Debug.Log("[문/밝기] 이 재질을 쓰는 렌더러 " + users + "개 — 입구 셋 말고 다른 곳에도 쓰이면 그것도 값이다");
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        static void DarkReport(string tag, string rootName, float ex, float ez, float yaw)
        {
            if (!ReadMouthDark(rootName, ex, ez, yaw, out float ratio, out float m, out float g, out string what))
            {
                Debug.Log("[문/밝기] " + tag + " — 못 쟀다: " + what);
                return;
            }
            Debug.Log("[문/밝기] " + tag + " — **발밑 대비 " + (ratio * 100f).ToString("0") + "%** · " + what);
        }
    }
}
