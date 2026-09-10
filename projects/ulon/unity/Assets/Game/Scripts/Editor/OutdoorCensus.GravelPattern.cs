using UnityEngine;

namespace Ulon.Editor
{
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// **자갈 무늬 후보를 나란히 굽고 나란히 본다**(검수 지시 2026-09-10, 처방 단계).
        ///
        /// 셈이 범인을 지목했다(`RunBankBlob`): `64` 왼쪽 둑은 **MineGravel 72.6%**이고, 그 겹의
        /// 무늬를 지우면 상자 안 잔결이 6.88 → 3.58로 **반이 날아간다**. 해를 꺼도 상대 대비는
        /// 그대로였고(4.6% → 4.3%) 도포 뒤집힘도 2.8%뿐이라 **음영도 섞임도 아니다.**
        /// 화면 자기상관은 8px에서 −0.32로 골이 진다 = 주기 약 32cm ≈ 무늬 1의 **사선 층리 간격**.
        ///
        /// 후보 셋을 **같은 자리·같은 자로** 잰다:
        ///   1 = 지금(층리 있는 암석 무늬) · 6 = 층리를 빼고 칸을 보간으로 부드럽게(검수 처방 ①②)
        ///   5 = 자갈 제 무늬(검수 처방 ③ — 돌 알갱이 + 사이 그늘, 줄이 안 선다)
        /// 축은 셋이다: 상자 잔결 RMS · **평균 밝기**(자갈 톤 게이트가 지키는 값이라 흔들면 안 된다)
        /// · 자기상관 골의 깊이와 자리(줄무늬가 남았는지).
        ///
        /// 자갈 겹은 **광산(`20`)·부두 절개면(`63`)에도 같이 깔린다** — 그 둘도 같이 찍어 본다.
        /// </summary>
        public static void RunGravelPattern()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            int[] candidates = { 1, 6, 5 };
            string[] shots = { "64_river_bend", "20_mine", "63_pier_cutface" };
            string outDir = System.IO.Path.Combine(Application.dataPath, "../../builds/qa/gravel");
            System.IO.Directory.CreateDirectory(outDir);

            foreach (int pat in candidates)
            {
                VisualSliceBuilder.GravelPatternOverride = pat;
                VisualSliceBuilder.EnsureVillageTerrain();
                UnityEditor.AssetDatabase.SaveAssets();
                UnityEditor.AssetDatabase.Refresh();
                foreach (var shot in shots)
                    GravelShot(shot, pat, outDir);
            }
            VisualSliceBuilder.GravelPatternOverride = -1;
            VisualSliceBuilder.EnsureVillageTerrain();
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log("[자갈무늬] 되돌림 — 원장 무늬로 다시 구움");
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(0);
        }

        /// <summary>
        /// **둑이 덩어리로 뭉쳐 있나** — `64` 둑 상자에서 잔결(21px 이동평균을 뺀 나머지)의
        /// **이웃 픽셀 상관 r(1)**. 값이 높을수록 한 픽셀과 옆 픽셀이 같아 **덩어리(바둑판)**이고,
        /// 낮을수록 낟알이다. 실측: 무늬 1(층리) 0.64 · 무늬 6(부드러운 얼룩) 0.73 · **무늬 5(자갈) 0.29**.
        /// 골의 깊이(줄무늬 세기)도 같은 방향이다(−0.32 · −0.47 · **−0.21**).
        /// 게이트와 셈이 **같은 자**를 쓴다.
        /// </summary>
        public static float BankLumpiness()
        {
            var all = QaShots.BuildShots();
            int idx = -1;
            for (int i = 0; i < all.Length; i++)
                if (QaShots.NameOf(all[i]) == "64_river_bend") { idx = i; break; }
            if (idx < 0) throw new System.InvalidOperationException("[둑무늬] 64_river_bend가 원장에 없습니다.");
            QaShots.EyeOf(all[idx], out Vector3 eye, out Vector3 look);

            const int W = 1280, H = 720;
            var camGo = new GameObject("BankGrainCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 500f;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            camGo.transform.position = eye;
            camGo.transform.LookAt(look);
            // **물을 끄고 잰다**(2026-09-11, 물 재작업 1단계). 이 자의 상자는 화면 고정 사각형이라
            // **강물이 그 안에 들어 있다** — 판별로 갈랐다(`RunBankWaterProbe`): 같은 판에서
            // 물 켬 0.52 · 물 끔 0.16. 물이 매끈해지자 이 자가 **둑과 무관하게** 물린 것이다.
            // 자를 느슨하게 하지 않고 **대상을 제 뜻으로 좁힌다**: 둑 무늬는 뭍의 성질이고,
            // 물이 덮은 픽셀은 둑이 아니다(물은 제 자들이 따로 본다).
            var waterGo = GameObject.Find(VisualSliceBuilder.WaterObject);
            var waterRend = waterGo != null ? waterGo.GetComponent<Renderer>() : null;
            bool waterWas = waterRend != null && waterRend.enabled;
            if (waterRend != null) waterRend.enabled = false;
            try { cam.Render(); }
            finally { if (waterRend != null) waterRend.enabled = waterWas; }
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            float r1 = Roi_R1(tex.GetPixels32(), W, 150, 620, 300, 590);
            cam.targetTexture = null; RenderTexture.active = null;
            Object.DestroyImmediate(camGo); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            return r1;
        }

        /// <summary>상자 안 잔결의 이웃 상관 r(1) — 고주파만 남기고 잰다.</summary>
        static float Roi_R1(Color32[] px, int W, int x0, int x1, int y0, int y1)
        {
            const int K = 10;
            double num = 0, den = 0;
            for (int y = y0; y < y1; y++)
            {
                int w = x1 - x0;
                var hp = new float[w];
                for (int i = 0; i < w; i++)
                {
                    double m = 0; int c = 0;
                    for (int k = -K; k <= K; k++)
                    {
                        int q = i + k; if (q < 0 || q >= w) continue;
                        var t = px[y * W + x0 + q];
                        m += (t.r + t.g + t.b) / 3.0; c++;
                    }
                    var s = px[y * W + x0 + i];
                    hp[i] = (float)((s.r + s.g + s.b) / 3.0 - m / c);
                }
                for (int i = 0; i + 1 < w; i++) num += hp[i] * hp[i + 1];
                for (int i = 0; i < w; i++) den += hp[i] * hp[i];
            }
            return den > 0 ? (float)(num / den) : 0f;
        }

        static void GravelShot(string shotName, int pat, string outDir)
        {
            var all = QaShots.BuildShots();
            int idx = -1;
            for (int i = 0; i < all.Length; i++)
                if (QaShots.NameOf(all[i]) == shotName) { idx = i; break; }
            if (idx < 0) { Debug.LogWarning("[자갈무늬] " + shotName + " 없음"); return; }
            QaShots.EyeOf(all[idx], out Vector3 eye, out Vector3 look);

            const int W = 1280, H = 720;
            var camGo = new GameObject("GravelCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 500f;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            camGo.transform.position = eye;
            camGo.transform.LookAt(look);

            // 상자는 `64`만 둑 자리를 쓰고, 나머지는 화면 가운데를 본다.
            int X0 = 150, X1 = 620, Y0 = 300, Y1 = 590;
            if (shotName != "64_river_bend") { X0 = 340; X1 = 940; Y0 = 180; Y1 = 540; }

            float rms = RoiRms(cam, rt, W, H, X0, X1, Y0, Y1, out float mean);
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            int lag = ScreenPeriod(tex.GetPixels32(), W, X0, X1, Y0, Y1, out float _, true);
            Debug.Log("[자갈무늬] 무늬 " + pat + " · " + shotName + " — 잔결 RMS " + rms.ToString("0.00") +
                      " · 평균 밝기 " + mean.ToString("0.0") + " · 잔결 상관 길이 " + lag + "px");
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(outDir, shotName + "_p" + pat + ".png"),
                                         tex.EncodeToPNG());

            cam.targetTexture = null; RenderTexture.active = null;
            Object.DestroyImmediate(camGo); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
        }
    }
}
