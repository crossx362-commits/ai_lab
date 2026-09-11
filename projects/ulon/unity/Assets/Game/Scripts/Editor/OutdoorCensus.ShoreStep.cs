using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// **물가 경계가 계단처럼 각지나 — 원장 탓인가 구운 격자 탓인가**(검수 관찰, 2026-09-10:
        /// 강 근접 샷에서 하이트맵 격자가 그대로 보인다).
        ///
        /// 이름을 붙이기 전에 **두 자를 나란히** 댄다:
        ///   ⓐ **원장**(`WorldTerrain.HeightAt`) — 연속 함수다. 여기서 계단이 나오면 규칙이 각진 것이다.
        ///   ⓑ **구운 지형**(`Terrain.SampleHeight`) — 513×513 격자에 굽고 보간한 것이다.
        /// 강 중심선을 따라 촘촘히(0.1m) 물가를 찾아 그 z를 늘어놓고, **같은 값이 이어지는 구간의
        /// 길이**를 잰다. 그 길이가 셀 크기와 같으면 범인은 격자다.
        ///
        /// **첫 판은 이 자로 계단을 못 잡았다**(원장 0.240m · 구운 것 0.232m로 거의 같음).
        /// 이유가 자에 있었다: `SampleHeight`는 격자를 **보간한 연속면**을 돌려주므로 화면에 보이는
        /// **메시 삼각형의 계단**을 못 본다. 「두 자가 같다」는 「계단이 없다」가 아니라 **둘 다 같은
        /// 것을 못 본다**였다. 그래서 아래 ⓒ를 붙였다 — **화면에서 직접** 물↔뭍 경계를 따라가며 잰다.
        ///
        /// **이 자가 못 보는 것**: 「고쳐야 하나」는 셀 크기를 줄이는 비용(메모리·굽는 시간)과
        /// 맞바꾸는 판단이라 이 자가 정하지 않는다.
        /// </summary>
        public static void RunShoreStep()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain == null)
            {
                Debug.LogError("[물가결] 지형을 못 찾았습니다.");
                if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(1);
                return;
            }
            var data = terrain.terrainData;
            float cell = data.size.x / (data.heightmapResolution - 1);
            float alphaCell = data.size.x / data.alphamapResolution;
            Debug.Log("[물가결] 격자 — 하이트맵 " + data.heightmapResolution + "² · 한 변 " +
                      data.size.x.ToString("0") + "m · **높이 셀 " + cell.ToString("0.000") +
                      "m** · 알파맵 " + data.alphamapResolution + "² · **도포 셀 " +
                      alphaCell.ToString("0.000") + "m**");

            float sea = WorldTerrain.SeaLevel;
            var ledger = new System.Collections.Generic.List<float>();
            var baked = new System.Collections.Generic.List<float>();
            for (float x = WorldTerrain.LakeX - WorldTerrain.LakeRadius - 5f; x >= -125f; x -= 0.1f)
            {
                float cz = WorldTerrain.RiverZ + Mathf.Sin((x - WorldTerrain.RiverFromX) * 0.06f) * 6f;
                ledger.Add(EdgeZ(x, cz, sea, false, terrain));
                baked.Add(EdgeZ(x, cz, sea, true, terrain));
            }
            Debug.Log("[물가결] 원장(연속 함수) — " + StepStats(ledger, 0.1f));
            Debug.Log("[물가결] 구운 지형(SampleHeight) — " + StepStats(baked, 0.1f));
            Debug.Log("[물가결] 셀 크기와 견줌 — 계단 길이가 " + cell.ToString("0.000") +
                      "m 언저리면 범인은 **격자**이고, 원장 쪽에도 같은 계단이 있으면 **규칙**이다");
            // ⓔ **지형 텍스처의 낟알** — 경계가 격자를 안 타는데도 화면이 각져 보이면 남는 후보는
            // 무늬다. 텍스처 한 픽셀이 세계에서 몇 cm인지 재면 근접에서 블록으로 읽히는지 알 수 있다.
            string grain = "";
            foreach (var tl in data.terrainLayers)
            {
                var tx = tl != null ? tl.diffuseTexture : null;
                if (tx == null) continue;
                float mPerTexel = tl.tileSize.x / tx.width;
                grain += " · " + tl.name + " " + tx.width + "px/" + tl.tileSize.x.ToString("0") + "m=" +
                         (mPerTexel * 100f).ToString("0") + "cm";
            }
            Debug.Log("[물가결] ⓔ 텍스처 낟알 —" + grain);
            ScreenEdge(cell, alphaCell);
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        /// <summary>
        /// **축을 하나씩 실제로 재굽는다**(검수 지시 2026-09-11). 「가로 해상도를 바꿔도 노치가
        /// 그대로였다」는 앞선 판별은 **다른 자**(`MeasureShoreEdge`)로 잰 것이라 그 자가 못 보는
        /// 것을 못 봤을 수 있다. 여기서는 같은 자(ⓒ 화면 경계)를 **해상도만 바꿔 가며** 댄다.
        ///
        /// 읽는 법: 평평한 구간 길이가 **셀 px에 비례**하면 범인은 격자(메시 면)다. 셀을 절반·두 배로
        /// 해도 안 변하면 격자가 아니라 **높이값 자체의 계단**(양자화·스냅)이다.
        /// </summary>
        public static void RunShoreStepRes()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            int keep = VisualSliceBuilder.HeightResOverride;
            try
            {
                foreach (int res in new[] { 513, 257, 1025 })
                {
                    VisualSliceBuilder.HeightResOverride = res;
                    VisualSliceBuilder.EnsureVillageTerrain();
                    UnityEditor.AssetDatabase.SaveAssets();
                    var td = Object.FindFirstObjectByType<Terrain>().terrainData;
                    float c = td.size.x / (td.heightmapResolution - 1);
                    float ac = td.size.x / td.alphamapResolution;
                    Debug.Log("[물가결] ── 하이트맵 " + td.heightmapResolution + "² · 셀 " +
                              c.ToString("0.000") + "m ──");
                    ScreenEdge(c, ac, "64_river_bend");
                    ScreenEdge(c, ac, "15_lake_river");
                }
            }
            finally
            {
                VisualSliceBuilder.HeightResOverride = keep;
                VisualSliceBuilder.EnsureVillageTerrain();
                UnityEditor.AssetDatabase.SaveAssets();
                Debug.Log("[물가결] 원장 해상도로 되돌렸다.");
            }
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(0);
        }

        /// <summary>
        /// **도포(알파맵) 격자 축** — 높이 격자를 1/2·2배로 굽어도 ⓒ가 안 변했고, 톱니의 **월드 크기**는
        /// 0.6m 언저리로 고정이었다(렌더를 2배로 하면 px만 배가). 0.6m짜리 격자가 하나 더 있다:
        /// **도포 셀 0.586m**(알파맵 512²). 얕은 물은 반투명이라 **바닥 도포가 비쳐** 물 색을 정하므로
        /// 물↔뭍 경계가 도포 격자를 탈 수 있다. 여기서 도포만 256·1024로 굽어 가른다.
        /// </summary>
        public static void RunShoreStepAlpha()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            int keep = VisualSliceBuilder.AlphaResOverride;
            try
            {
                foreach (int res in new[] { 512, 256, 1024 })
                {
                    VisualSliceBuilder.AlphaResOverride = res;
                    VisualSliceBuilder.EnsureVillageTerrain();
                    UnityEditor.AssetDatabase.SaveAssets();
                    var td = Object.FindFirstObjectByType<Terrain>().terrainData;
                    float c = td.size.x / (td.heightmapResolution - 1);
                    float ac = td.size.x / td.alphamapResolution;
                    ProbeTag = "h_alpha" + td.alphamapResolution;
                    Debug.Log("[물가결] ── 알파맵 " + td.alphamapResolution + "² · 도포 셀 " +
                              ac.ToString("0.000") + "m ──");
                    ScreenEdge(c, ac, "64_river_bend");
                    ScreenEdge(c, ac, "15_lake_river");
                }
            }
            finally
            {
                VisualSliceBuilder.AlphaResOverride = keep;
                VisualSliceBuilder.EnsureVillageTerrain();
                UnityEditor.AssetDatabase.SaveAssets();
                Debug.Log("[물가결] 원장 도포 해상도로 되돌렸다.");
            }
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(0);
        }

        /// <summary>
        /// **경사 하한 축** — 도포 격자가 톱니를 정한다는 것까지는 갈렸다(도포 셀 2배 → 톱니 1.7배,
        /// 높이 셀은 4배를 오가도 불변). 그러면 **도포 규칙 중 무엇이 칸마다 튀느냐**가 다음 물음이다.
        /// 후보는 `ShoreSandRaw`의 「걸어온 거리」 — 백사장 경사(0.056)가 하한(0.05)에 거의 붙어 있어
        /// 나누기가 잡음을 증폭한다. 하한만 바꿔 가며 굽고 ⓒ를 잰다.
        /// </summary>
        public static void RunShoreStepBank()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            try
            {
                foreach (float floor in new[] { 0.05f, 0.14f, 0.30f })
                {
                    WorldSplat.BankTanFloorOverride = floor;
                    VisualSliceBuilder.EnsureVillageTerrain();
                    UnityEditor.AssetDatabase.SaveAssets();
                    var td = Object.FindFirstObjectByType<Terrain>().terrainData;
                    float c = td.size.x / (td.heightmapResolution - 1);
                    float ac = td.size.x / td.alphamapResolution;
                    ProbeTag = "i_bank" + floor.ToString("0.00");
                    Debug.Log("[물가결] ── 경사 하한 tan " + floor.ToString("0.00") + " ──");
                    ScreenEdge(c, ac, "64_river_bend");
                    ScreenEdge(c, ac, "15_lake_river");
                }
            }
            finally
            {
                WorldSplat.BankTanFloorOverride = -1f;
                VisualSliceBuilder.EnsureVillageTerrain();
                UnityEditor.AssetDatabase.SaveAssets();
                Debug.Log("[물가결] 원장 하한으로 되돌렸다.");
            }
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(0);
        }

        /// <summary>이번 판이 무슨 조건이었는지 — 뜨는 PNG 이름에 붙는다.</summary>
        static string ProbeTag = "base";

        /// <summary>
        /// **격자가 기각된 뒤의 두 축**(2026-09-11). 셀을 1/2·2배로 굽어도 ⓒ가 안 변했으니
        /// 톱니는 **지형 격자가 아니다**. 남은 후보를 하나씩 끈다:
        ///   ⓕ **렌더 해상도** — 2배로 렌더해 톱니 px가 **배가되면 세계에 붙은 것**, 그대로면
        ///      **화면에 붙은 것**(디더·후처리·셰이더의 화면 좌표 잡음)이다.
        ///   ⓖ **거품 잡음** — 1.7b·1.7c에서 내가 물가에 넣은 월드 잡음(`_FoamBreak`·`_FoamWobbleM`)을
        ///      끄고 잰다. 내가 만든 것이면 여기서 톱니가 준다. **처방을 짜기 전에 내 것부터 의심한다.**
        /// </summary>
        public static void RunShoreStepAxes()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            var td = Object.FindFirstObjectByType<Terrain>().terrainData;
            float c = td.size.x / (td.heightmapResolution - 1);
            float ac = td.size.x / td.alphamapResolution;

            ProbeTag = "f_res2x";
            Debug.Log("[물가결] ── ⓕ 렌더 2배(2560×1440) ──");
            ScreenEdge(c, ac, "64_river_bend", 2560, 1440);
            ScreenEdge(c, ac, "15_lake_river", 2560, 1440);

            Material water = null;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                var m = r.sharedMaterial;
                if (m != null && m.shader != null && m.shader.name.Contains("StylizedWater")) { water = m; break; }
            }
            if (water == null) { Debug.LogWarning("[물가결] 물 머티리얼을 못 찾음 — ⓖ 건너뜀"); }
            else
            {
                float kBreak = water.GetFloat("_FoamBreak"), kWob = water.GetFloat("_FoamWobbleM");
                float kMax = water.GetFloat("_FoamMaxAlpha");
                try
                {
                    water.SetFloat("_FoamBreak", 0f); water.SetFloat("_FoamWobbleM", 0f);
                    ProbeTag = "g1_nonoise";
                    Debug.Log("[물가결] ── ⓖ1 물가 잡음 끔(_FoamBreak=0 · _FoamWobbleM=0) ──");
                    ScreenEdge(c, ac, "64_river_bend");
                    ScreenEdge(c, ac, "15_lake_river");
                    water.SetFloat("_FoamMaxAlpha", 0f);
                    ProbeTag = "g2_nofoam";
                    Debug.Log("[물가결] ── ⓖ2 거품 자체 끔(_FoamMaxAlpha=0) ──");
                    ScreenEdge(c, ac, "64_river_bend");
                    ScreenEdge(c, ac, "15_lake_river");
                }
                finally
                {
                    water.SetFloat("_FoamBreak", kBreak); water.SetFloat("_FoamWobbleM", kWob);
                    water.SetFloat("_FoamMaxAlpha", kMax);
                }
            }
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(0);
        }

        /// <summary>
        /// ⓒ **화면에서 직접** — `64_river_bend`를 렌더해 열마다 물↔뭍 경계를 찾고, 그 경계 y가
        /// **가로로 몇 픽셀이나 같은 값에 머무는지** 잰다. 계단 하나가 화면에서 몇 px인지가 답이다.
        /// 셀 크기를 그 거리에서의 화면 픽셀로 환산해 나란히 찍는다 — 둘이 맞으면 범인은 격자다.
        /// </summary>
        static void ScreenEdge(float cell, float alphaCell, string shot = "64_river_bend", int W = 1280, int H = 720)
        {
            var shots = QaShots.BuildShots();
            int idx = -1;
            for (int i = 0; i < shots.Length; i++)
                if (QaShots.NameOf(shots[i]) == shot) { idx = i; break; }
            if (idx < 0) { Debug.LogWarning("[물가결] " + shot + "를 못 찾음"); return; }
            QaShots.EyeOf(shots[idx], out Vector3 eye, out Vector3 look);

            var camGo = new GameObject("ShoreStepCam");
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
            // **눈으로도 본다** — 셈만 보면 자가 못 보는 것을 나도 못 본다(비추적 폴더).
            string probeDir = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, "../../builds/qa/probe"));
            System.IO.Directory.CreateDirectory(probeDir);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(probeDir,
                shot + "_" + W + "_" + ProbeTag + ".png"), tex.EncodeToPNG());

            // 열마다 아래에서 위로 훑어 **물이 끝나는 첫 자리**(경계). 물은 파랑이 우세한 픽셀이다.
            var edge = new int[W];
            for (int i = 0; i < W; i++)
            {
                edge[i] = -1;
                bool sawWater = false;
                for (int j = 0; j < H; j++)
                {
                    var c = px[j * W + i];
                    bool wet = c.b > c.r + 20 && c.b > c.g;
                    if (wet) sawWater = true;
                    else if (sawWater) { edge[i] = j; break; }
                }
            }
            // **모래↔잔디 경계**도 나란히 — 화면에서 각져 보이는 것이 물가인지 도포 경계인지 가른다.
            var sandEdge = new int[W];
            for (int i = 0; i < W; i++)
            {
                sandEdge[i] = -1;
                bool sawSand = false;
                for (int j = H - 1; j >= 0; j--)
                {
                    var c = px[j * W + i];
                    bool sand = c.r > 150 && c.g > 150 && c.b > 120 && c.g < c.r + 25 && c.b < c.g;
                    bool grass = c.g > c.r + 15 && c.g > c.b + 15;
                    if (sand) sawSand = true;
                    else if (sawSand && grass) { sandEdge[i] = j; break; }
                }
            }
            int sRuns = 0, sLongest = 0, sSum = 0, sRun = 1, sValid = 0;
            for (int i = 1; i < W; i++)
            {
                if (sandEdge[i] < 0 || sandEdge[i - 1] < 0) continue;
                sValid++;
                if (sandEdge[i] == sandEdge[i - 1]) sRun++;
                else { sRuns++; sSum += sRun; if (sRun > sLongest) sLongest = sRun; sRun = 1; }
            }
            if (sRun > 1) { sRuns++; sSum += sRun; if (sRun > sLongest) sLongest = sRun; }

            int runs = 0, longest = 0, sum = 0, run = 1, valid = 0;
            for (int i = 1; i < W; i++)
            {
                if (edge[i] < 0 || edge[i - 1] < 0) continue;
                valid++;
                if (edge[i] == edge[i - 1]) run++;
                else { runs++; sum += run; if (run > longest) longest = run; run = 1; }
            }
            if (run > 1) { runs++; sum += run; if (run > longest) longest = run; }
            float dist = (look - eye).magnitude;
            float mPerPx = 2f * dist * Mathf.Tan(55f * 0.5f * Mathf.Deg2Rad) * 16f / 9f / W;
            Debug.Log("[물가결] ⓒ " + shot + " 화면 경계 — 표본 " + valid + "열 · 평평한 구간 " + runs + "개 · 평균 " +
                      (runs > 0 ? (sum / (float)runs).ToString("0.0") : "—") + "px · 가장 긴 것 " + longest +
                      "px · 이 거리(" + dist.ToString("0") + "m)에서 높이 셀은 " + (cell / mPerPx).ToString("0") +
                      "px · 도포 셀은 " + (alphaCell / mPerPx).ToString("0") + "px");

            Debug.Log("[물가결] ⓓ " + shot + " 모래↔잔디 경계 — 표본 " + sValid + "열 · 평평한 구간 " + sRuns +
                      "개 · 평균 " + (sRuns > 0 ? (sSum / (float)sRuns).ToString("0.0") : "—") +
                      "px · 가장 긴 것 " + sLongest + "px");

            cam.targetTexture = null; RenderTexture.active = null;
            Object.DestroyImmediate(camGo); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
        }

        /// <summary>이 x에서 강 남쪽 물가의 z — 중심선에서 +z로 나가며 물이 끝나는 자리.</summary>
        static float EdgeZ(float x, float cz, float sea, bool useBaked, Terrain terrain)
        {
            for (float d = 0.5f; d <= 30f; d += 0.05f)
            {
                float z = cz + d;
                float h = useBaked ? terrain.SampleHeight(new Vector3(x, 0f, z)) : WorldTerrain.HeightAt(x, z);
                if (h >= sea) return z;
            }
            return float.NaN;
        }

        /// <summary>
        /// 값 수열에서 **같은 값이 이어지는 구간**의 길이 통계 — 계단의 크기다.
        /// 「같다」는 0.02m 안쪽으로 본다(부동소수 흔들림은 계단이 아니다).
        /// </summary>
        static string StepStats(System.Collections.Generic.List<float> zs, float dx)
        {
            int runs = 0;
            float longest = 0f, sum = 0f, run = dx;
            int valid = 0;
            for (int i = 1; i < zs.Count; i++)
            {
                if (float.IsNaN(zs[i]) || float.IsNaN(zs[i - 1])) continue;
                valid++;
                if (Mathf.Abs(zs[i] - zs[i - 1]) < 0.02f) run += dx;
                else
                {
                    runs++;
                    sum += run;
                    if (run > longest) longest = run;
                    run = dx;
                }
            }
            if (run > dx) { runs++; sum += run; if (run > longest) longest = run; }
            return "표본 " + valid + "곳 · 계단 " + runs + "개 · 평균 길이 " +
                   (runs > 0 ? (sum / runs).ToString("0.000") : "—") + "m · 가장 긴 것 " +
                   longest.ToString("0.000") + "m";
        }
    }
}
