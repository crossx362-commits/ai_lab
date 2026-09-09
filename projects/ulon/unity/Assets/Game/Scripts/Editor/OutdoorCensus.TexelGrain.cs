using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// **텍스처 해상도를 128·256·512로 나란히 재는 자**(검수 지시 2026-09-10).
        ///
        /// 앞선 셈(`be7893de`)이 물가 각짐의 범인을 **텍셀**로 지목했다 — 하이트맵·알파맵 격자는
        /// 화면에서 15px인데 경계는 2.2px이었고, 지형 텍스처가 전부 128px이라 텍셀 하나가 1.7~2.5px로
        /// 보였다. 그 처방을 고르는 자다.
        ///
        /// **눈금은 「텍셀 하나가 화면 몇 px인가」**다(검수가 정했다 — 1px 이하면 눈에서 사라진다).
        /// 화면 픽셀 하나가 세계에서 몇 m인지는 **이웃 픽셀 두 광선의 착지점 거리**로 잰다(비스듬히
        /// 누운 바닥에서 삼각함수로 유도하면 틀린다 — 실제로 쏴서 잰다). 텍셀 하나의 세계 크기는
        /// 그 자리에서 **1등 도포 층**의 `tileSize / 텍스처 폭`이다.
        ///
        /// 같은 표에 **비용**(픽셀 수·PNG 바이트)과 **원경 무늬 세기**를 같이 적는다 —
        /// 근접을 고치다 조망에서 무늬가 흐려지면 그건 수리가 아니라 맞바꿈이다(검수 지시 ③).
        ///
        /// **이 자가 못 보는 것**: 「무늬가 예쁜가」는 못 잰다. 텍셀이 안 보이게 되는 값을 고르는
        /// 데까지가 이 자의 일이고, 고른 판이 화면에서 나은지는 눈이 판정한다.
        /// </summary>
        public static void RunTexelGrain()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            int[] candidates = { 128, 256, 512 };
            string[] shots = { "64_river_bend", "14_world_vista", "16_mountain_ridge" };
            int keep = VisualSliceBuilder.NoiseRes;
            foreach (int res in candidates)
            {
                VisualSliceBuilder.NoiseRes = res;
                VisualSliceBuilder.EnsureVillageTerrain();
                UnityEditor.AssetDatabase.SaveAssets();
                UnityEditor.AssetDatabase.Refresh();
                long bytes = 0;
                var dir = new System.IO.DirectoryInfo(System.IO.Path.Combine(Application.dataPath, "Game/Art/Env"));
                foreach (var f in dir.GetFiles("*.png"))
                    bytes += f.Length;
                Debug.Log("[텍셀] === " + res + "px === PNG 합계 " + (bytes / 1024) + "KB · GPU 한 장 " +
                          (res * res * 3 / 1024) + "KB(RGB24) · 지형 10겹이면 " +
                          (res * res * 3L * 10 / 1024 / 1024f).ToString("0.00") + "MB");
                foreach (var name in shots)
                    MeasureShot(name, res);
            }
            VisualSliceBuilder.NoiseRes = keep;
            VisualSliceBuilder.EnsureVillageTerrain();
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log("[텍셀] 되돌림 — NoiseRes=" + keep);
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        /// <summary>한 화면에서 읽은 텍셀 크기 — 셈과 게이트가 **같은 자**를 쓴다.</summary>
        public struct Grain
        {
            public int Samples;      // 지형에 닿은 표본 수
            public float Median;     // 텍셀 하나가 화면 몇 px (1 이하면 눈에서 사라진다)
            public float P90;
            public float OverShare;  // 1px을 넘는 자리의 비율(%)
            public float Contrast;   // 이웃 픽셀 밝기차 — 무늬가 살아 있는가
        }

        public static Grain MeasureGrain(string shotName)
        {
            return MeasureShot(shotName, 0);
        }

        static Grain MeasureShot(string shotName, int res)
        {
            var all = QaShots.BuildShots();
            int idx = -1;
            for (int i = 0; i < all.Length; i++)
                if (QaShots.NameOf(all[i]) == shotName) { idx = i; break; }
            if (idx < 0) throw new System.InvalidOperationException("[텍셀] 화면 " + shotName + "이 원장에 없습니다 — 자가 죽은 채로 초록불이 되면 안 됩니다.");
            QaShots.EyeOf(all[idx], out Vector3 eye, out Vector3 look);

            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain == null) throw new System.InvalidOperationException("[텍셀] 지형을 못 찾았습니다.");
            var data = terrain.terrainData;
            var layers = data.terrainLayers;
            Vector3 tpos = terrain.transform.position;

            const int W = 1280, H = 720;
            var camGo = new GameObject("TexelGrainCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 500f;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            camGo.transform.position = eye;
            camGo.transform.LookAt(look);
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            var px = tex.GetPixels32();

            var sizes = new List<float>();
            int over = 0, samples = 0;
            double contrast = 0; int cn = 0;
            // 알파맵은 통째로 한 번만 읽는다(픽셀마다 GetAlphamaps를 부르면 수천 번 왕복한다).
            int ar = data.alphamapResolution;
            var maps = data.GetAlphamaps(0, 0, ar, ar);
            for (int j = 8; j < H - 8; j += 6)
            {
                for (int i = 8; i < W - 8; i += 6)
                {
                    var r1 = cam.ScreenPointToRay(new Vector3(i, j, 0f));
                    var r2 = cam.ScreenPointToRay(new Vector3(i + 1, j, 0f));
                    if (!Physics.Raycast(r1, out RaycastHit h1, 500f)) continue;
                    if (!(h1.collider is TerrainCollider)) continue;
                    if (!Physics.Raycast(r2, out RaycastHit h2, 500f)) continue;
                    if (!(h2.collider is TerrainCollider)) continue;
                    float mPerPx = Vector3.Distance(h1.point, h2.point);
                    if (mPerPx <= 0.0001f || mPerPx > 2f) continue;   // 지평선 근처는 발산한다

                    float u = (h1.point.x - tpos.x) / data.size.x;
                    float v = (h1.point.z - tpos.z) / data.size.z;
                    int ax = Mathf.Clamp(Mathf.RoundToInt(u * (ar - 1)), 0, ar - 1);
                    int az = Mathf.Clamp(Mathf.RoundToInt(v * (ar - 1)), 0, ar - 1);
                    int best = 0; float bw = -1f;
                    for (int L = 0; L < layers.Length; L++)
                        if (maps[az, ax, L] > bw) { bw = maps[az, ax, L]; best = L; }
                    var tl = layers[best];
                    var dt = tl != null ? tl.diffuseTexture : null;
                    if (dt == null) continue;
                    float mPerTexel = tl.tileSize.x / dt.width;
                    float pxPerTexel = mPerTexel / mPerPx;
                    sizes.Add(pxPerTexel);
                    samples++;
                    if (pxPerTexel > 1f) over++;

                    // 원경 무늬 세기 — 이웃 픽셀 밝기 차. 무늬가 뭉개지면 이 값이 떨어진다.
                    var c1 = px[j * W + i];
                    var c2 = px[j * W + i + 1];
                    contrast += Mathf.Abs((c1.r + c1.g + c1.b) - (c2.r + c2.g + c2.b)) / 3f;
                    cn++;
                }
            }
            sizes.Sort();
            if (samples < 500)
                throw new System.InvalidOperationException("[텍셀] " + shotName + "에서 지형 표본이 " +
                    samples + "곳뿐입니다 — 화면에 지형이 안 잡히면 이 자는 아무것도 못 봅니다.");
            var g = new Grain
            {
                Samples = samples,
                Median = sizes[sizes.Count / 2],
                P90 = sizes[(int)(sizes.Count * 0.9f)],
                OverShare = 100f * over / samples,
                Contrast = cn > 0 ? (float)(contrast / cn) : 0f,
            };
            if (res > 0)
            {
                Debug.Log("[텍셀] " + res + "px · " + shotName + " — 지형 표본 " + samples +
                          "곳 · **텍셀 크기 중앙값 " + g.Median.ToString("0.00") + "px** · 상위10% " +
                          g.P90.ToString("0.00") + "px · 1px 넘는 자리 " + g.OverShare.ToString("0.0") +
                          "% · 이웃 밝기차(무늬 세기) " + g.Contrast.ToString("0.00"));

                // **눈으로도 봐야 고른다** — 수치는 「텍셀이 몇 px인가」까지만 답한다. 후보마다 같은
                // 자리의 화면을 남겨 나란히 본다(`builds/qa/texel/`, git에 넣지 않는다).
                string outDir = System.IO.Path.Combine(Application.dataPath, "../../builds/qa/texel");
                System.IO.Directory.CreateDirectory(outDir);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(outDir, shotName + "_" + res + ".png"),
                                             tex.EncodeToPNG());
            }

            cam.targetTexture = null; RenderTexture.active = null;
            Object.DestroyImmediate(camGo); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            return g;
        }
    }
}
