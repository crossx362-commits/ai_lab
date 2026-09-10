using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// **거품 띠가 화면에서 어디까지 퍼졌나** — 1.5단계 반려의 셈(검수 판정 2026-09-11).
        ///
        /// 왜 새 자가 필요했나: 게이트(`ShoreFoamStats`)는 **수심 0.05~0.5m 띠 안에서만** 흰 몫을
        /// 센다. 그 자로는 「띠가 그 바깥까지 퍼졌다」를 **원리상 못 본다** — 검수가 `15`에서 본
        /// **흰 도넛**은 게이트 표본 바깥에서 벌어진 일이었다. 하한만 있고 상한이 없던 것보다
        /// 앞선 문제가 이것이다: **재는 자리 자체가 좁았다.**
        ///
        /// 그래서 여기서는 **물 픽셀 전부**를 물마다 갈라 센다(호수·바다·강). 축 셋:
        ///   ①그 물의 흰 몫(면적 대비) ②흰 픽셀의 **수심 분포**(어디까지 하얀가)
        ///   ③그 자리 바닥의 **원장 기울기**(`HeightAt` 유한차분) — 셰이더가 쓰는 화면 법선이
        ///     아니라 지형 함수에서 직접 얻는다. 둘이 다르면 그 차이가 곧 원인이다.
        ///
        /// 바다 해안 거품은 **근거리(`63`)와 원거리(`15`)를 따로** 센다 — 검수가 「`63`은 전에도
        /// 있었고 `15`는 여전히 없다」고 갈랐다. 한 수로 뭉치면 그 구분이 사라진다.
        /// </summary>
        public static void RunFoamExtent()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            VisualSliceBuilder.RebuildWater();   // 깊이 요구 부품 없는 씬에서 재면 전부 헛수다

            // **가로폭 후보를 한 판에 쓸어 잰다.** 상한(`_FoamDepthSteep`)을 쓸어 봤더니 세 값이
            // **소수점까지 같았다** — 폭을 정직하게 만든 순간 상한이 더 이상 물지 않기 때문이다
            // (호수 바닥 8°면 문턱은 2.0×tan8° = 0.28m로 상한 0.30 아래다). 상한은 이제 벽 쪽
            // 안전장치일 뿐이고, **띠의 굵기를 정하는 것은 가로폭**이다. 0은 네거티브 컨트롤이다.
            var mat = FindWaterMaterial();
            if (mat == null) { Debug.LogError("[거품폭] 수면 재질을 못 찾았습니다."); return; }
            Debug.Log("[거품폭] 재질 " + mat.name + " · 셰이더 " + mat.shader.name);
            float keep = mat.GetFloat("_FoamWidthM");
            try
            {
                foreach (float wm in new[] { 0f, 2f, 4f, 6f })
                {
                    mat.SetFloat("_FoamWidthM", wm);
                    foreach (string shot in new[] { "15_lake_river", "64_river_bend", "63_pier_cutface" })
                    {
                        FoamAreaShares(shot, out _, out _, out _, out string det);
                        Debug.Log("[거품폭] 가로폭 " + wm.ToString("0.0") + "m · " + shot + " — " + det);
                    }
                }
            }
            finally { mat.SetFloat("_FoamWidthM", keep); }

            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        /// <summary>물 종류 — 원장 상수로 가른다(화면색이 아니라 좌표로).</summary>
        static string WaterBody(float x, float z)
        {
            float dLake = Mathf.Sqrt((x - WorldTerrain.LakeX) * (x - WorldTerrain.LakeX) +
                                     (z - WorldTerrain.LakeZ) * (z - WorldTerrain.LakeZ));
            if (dLake <= WorldTerrain.LakeRadius + WorldTerrain.ShoreRamp) return "호수";
            if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) >= WorldTerrain.MountainEnd) return "바다";
            return "강";
        }

        /// <summary>원장 지형의 국소 기울기(도) — 셰이더가 쓰는 화면 법선과 대조할 값.</summary>
        static float BottomSlopeDeg(float x, float z)
        {
            const float e = 0.75f;
            float dx = (WorldTerrain.HeightAt(x + e, z) - WorldTerrain.HeightAt(x - e, z)) / (2f * e);
            float dz = (WorldTerrain.HeightAt(x, z + e) - WorldTerrain.HeightAt(x, z - e)) / (2f * e);
            return Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// **물마다 「면적 대비 흰 몫」** — 게이트가 상한을 물릴 축이다(1.6단계).
        /// 표본이 60px 미만인 물은 −1로 낸다(못 재는 자리를 초록불로 남기지 않는다).
        /// </summary>
        /// <summary>
        /// **거품 픽셀은 「밝고 **덜 파랗다**」로 가린다**(1.6단계, 두 번 고쳐 앉힌 자).
        ///
        /// ①옛 자는 「RGB 모두 215↑」였다. 거품 덮음에 상한(0.75)을 두자 거품의 밝기 천장이 같이
        ///   내려가 **자가 대상을 통째로 놓쳤다**(호수 18.9% → 0.2%, 화면엔 띠가 있는데).
        /// ②그래서 「열린 물보다 35 밝다」로 바꿔 봤더니 이번엔 **깊이 색까지 걸렸다** —
        ///   얕은 물은 **설계상** 밝다(`64` 바다 34.8%, 흰 픽셀 평균 수심 0.51m). 밝기만으로는
        ///   「얕아서 밝은 물」과 거품이 안 갈린다.
        /// 갈리는 성질은 **색**이다: 물은 파랑이 빨강보다 훨씬 세고(얕은 물빛 r/b ≈ 0.43),
        /// 거품은 흰색에 가까워 그 비가 1에 가깝다. 그래서 **r ≥ 0.80·b 이면서 열린 물보다
        /// 20 이상 밝은** 픽셀만 거품으로 센다. 덮음 상한·조명이 바뀌어도 같은 것을 잰다.
        /// 기준선은 **그 물의 깊은 쪽**(수심 1.2m↑)이다 — 호수는 가장 깊어야 1.8m라 2m로 잡으면
        /// 호수가 통째로 「못 재는 물」이 된다(그 판을 찍었다).
        /// </summary>
        const float FoamLumOverOpen = 20f;
        const float FoamRedOverBlue = 0.80f;
        const float OpenWaterDepth = 1.2f;

        internal static bool FoamAreaShares(string shotName, out float lake, out float sea,
                                            out float river, out string detail)
        {
            lake = sea = river = -1f;
            detail = "";
            var shots = QaShots.BuildShots();
            int idx = -1;
            for (int i = 0; i < shots.Length; i++)
                if (QaShots.NameOf(shots[i]) == shotName) { idx = i; break; }
            if (idx < 0) { detail = shotName + " 없음"; return false; }
            QaShots.EyeOf(shots[idx], out Vector3 eye, out Vector3 look);

            const int W = 1280, H = 720;
            var camGo = new GameObject("_foamExtentCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 500f;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            camGo.transform.position = eye;
            camGo.transform.LookAt(look);
            try
            {
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                var px = tex.GetPixels32();

                string[] bodies = { "호수", "바다", "강" };
                var nAll = new int[3];
                var nWhite = new int[3];
                var whiteDepthSum = new double[3];
                var whiteDepthMax = new float[3];
                var whiteSlopeSum = new double[3];
                // 열린 물(수심 2m↑)의 평균 밝기 — 거품 판정의 기준선이다. 두 번 훑지 않으려고
                // 표본을 모아 두고 뒤에서 센다(광선 쏘기가 이 셈의 값비싼 부분이다).
                var sBody = new System.Collections.Generic.List<byte>(240000);
                var sDepth = new System.Collections.Generic.List<float>(240000);
                var sLum = new System.Collections.Generic.List<float>(240000);
                var sPale = new System.Collections.Generic.List<bool>(240000);
                var sX = new System.Collections.Generic.List<float>(240000);
                var sZ = new System.Collections.Generic.List<float>(240000);
                float y = WorldTerrain.SeaLevel;

                for (int j = 0; j < H; j += 2)
                    for (int i = 0; i < W; i += 2)
                    {
                        var ray = cam.ScreenPointToRay(new Vector3(i, j, 0f));
                        if (ray.direction.y >= -0.001f) continue;
                        float t = (y - ray.origin.y) / ray.direction.y;
                        if (t <= 0f || t > 400f) continue;
                        var p = ray.origin + ray.direction * t;
                        if (Physics.Raycast(ray.origin, ray.direction, out RaycastHit hit, t - 0.5f) &&
                            hit.distance < t - 0.5f) continue;
                        float d = WorldTerrain.SeaLevel - WorldTerrain.HeightAt(p.x, p.z);
                        if (d <= 0.02f) continue;                     // 물이 아니다
                        string body = WaterBody(p.x, p.z);
                        int b = body == "호수" ? 0 : body == "바다" ? 1 : 2;
                        var c = px[j * W + i];
                        sBody.Add((byte)b); sDepth.Add(d);
                        sLum.Add((c.r + c.g + c.b) / 3f);
                        sPale.Add(c.r >= FoamRedOverBlue * c.b);
                        sX.Add(p.x); sZ.Add(p.z);
                    }

                // 기준선 — 물마다 열린 물의 평균 밝기. 열린 물 표본이 없으면 그 물은 못 잰다.
                var openSum = new double[3];
                var openN = new int[3];
                for (int k = 0; k < sBody.Count; k++)
                    if (sDepth[k] >= OpenWaterDepth) { openSum[sBody[k]] += sLum[k]; openN[sBody[k]]++; }
                for (int k = 0; k < sBody.Count; k++)
                {
                    int b = sBody[k];
                    nAll[b]++;
                    if (openN[b] < 30) continue;
                    float baseLum = (float)(openSum[b] / openN[b]);
                    if (sPale[k] && sLum[k] >= baseLum + FoamLumOverOpen)
                    {
                        nWhite[b]++;
                        whiteDepthSum[b] += sDepth[k];
                        if (sDepth[k] > whiteDepthMax[b]) whiteDepthMax[b] = sDepth[k];
                        whiteSlopeSum[b] += BottomSlopeDeg(sX[k], sZ[k]);
                    }
                }

                var outShare = new float[3];
                for (int b = 0; b < 3; b++)
                {
                    if (nAll[b] < 60 || openN[b] < 30) { outShare[b] = -1f; continue; }
                    float share = (float)nWhite[b] / nAll[b];
                    outShare[b] = share;
                    string extra = nWhite[b] > 0
                        ? " 흰 평균 수심 " + (whiteDepthSum[b] / nWhite[b]).ToString("0.00") +
                          "m·최대 " + whiteDepthMax[b].ToString("0.00") +
                          "m·그 자리 원장 기울기 " + (whiteSlopeSum[b] / nWhite[b]).ToString("0.0") + "°"
                        : " 흰 없음";
                    detail += bodies[b] + " " + (share * 100f).ToString("0.0") + "%(물 " + nAll[b] +
                              "px 중 " + nWhite[b] + "px," + extra + ") · ";
                }
                lake = outShare[0]; sea = outShare[1]; river = outShare[2];
                return true;
            }
            finally
            {
                cam.targetTexture = null; RenderTexture.active = null;
                Object.DestroyImmediate(camGo); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            }
        }
    }
}
