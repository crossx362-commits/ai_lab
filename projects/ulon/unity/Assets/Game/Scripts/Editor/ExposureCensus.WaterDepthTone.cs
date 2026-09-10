using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    public static partial class ExposureCensus
    {
        /// <summary>
        /// **같은 깊이 구간의 물끼리** 호수와 바다를 견준다(검수 판정 2026-09-11).
        ///
        /// 왜 눈금을 바꿨나: 옛 자(`LakeSeaLumaRatio`)는 호수 전체 평균과 바다 전체 평균을 견줬다.
        /// 물이 **깊이에 따라 다른 색**이 된 지금 그 자는 목표를 결함으로 읽는다 — 호수는 얕고
        /// 바다는 깊으니 색이 다른 것이 **맞다**. 그래서 대상을 바꾼다: 수심을 구간으로 갈라
        /// **같은 구간 안에서만** 호수÷바다를 낸다. 구간이 같은데도 다르면 그때는 여전히 결함이다
        /// (「한 화면에서 같은 물이 두 물감」이라는 원래 뜻은 그대로 지킨다).
        ///
        /// 수심은 화면색이 아니라 **원장 지형 함수**에서 얻는다(`WorldTerrain.HeightAt`) — 색으로
        /// 수심을 재면 자가 제가 재려던 것을 되먹는다.
        ///
        /// 물가 거품이 얕은 구간을 희게 물들이므로 **0.6m 아래는 빼고** 잰다(거품은 제 자가 따로
        /// 본다: `OutdoorCensus.ShoreFoamContrast`). 반환값은 표본이 선 구간들 중 **가장 나쁜 비**다.
        /// </summary>
        internal static float LakeSeaLumaRatioByDepth(float gloss, out string detail)
        {
            detail = "";
            var shots = QaShots.BuildShots();
            int idx = -1;
            for (int i = 0; i < shots.Length; i++)
                if (QaShots.NameOf(shots[i]) == "15_lake_river") { idx = i; break; }
            if (idx < 0) return -1f;
            QaShots.EyeOf(shots[idx], out Vector3 eye, out Vector3 look);

            var camGo = new GameObject("WaterDepthToneCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 500f;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            camGo.transform.position = eye;
            camGo.transform.LookAt(look);

            var water = GameObject.Find(VisualSliceBuilder.WaterObject);
            var rend = water != null ? water.GetComponent<Renderer>() : null;
            var mat = rend != null ? rend.sharedMaterial : null;
            bool hasGloss = mat != null && mat.HasProperty("_Glossiness");
            float keep = hasGloss ? mat.GetFloat("_Glossiness") : 0f;
            if (hasGloss && !float.IsNaN(gloss)) mat.SetFloat("_Glossiness", gloss);
            try
            {
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                var px = tex.GetPixels32();

                // 구간 경계(m): 0.6~1.5 · 1.5~3.0 · 3.0~
                float[] lo = { 0.6f, 1.5f, 3.0f };
                float[] hi = { 1.5f, 3.0f, 999f };
                var sl = new double[3]; var ss = new double[3];
                var nl = new int[3]; var ns = new int[3];

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
                        int b = -1;
                        for (int k = 0; k < 3; k++) if (d >= lo[k] && d < hi[k]) { b = k; break; }
                        if (b < 0) continue;
                        var c = px[j * W + i];
                        float lum = (c.r + c.g + c.b) / 3f;
                        float dLake = new Vector2(p.x - WorldTerrain.LakeX, p.z - WorldTerrain.LakeZ).magnitude;
                        float dOrigin = new Vector2(p.x, p.z).magnitude;
                        if (dLake < WorldTerrain.LakeRadius - 3f) { sl[b] += lum; nl[b]++; }
                        else if (dOrigin > WorldTerrain.CoastEnd + 6f) { ss[b] += lum; ns[b]++; }
                    }

                float worst = -1f;
                for (int k = 0; k < 3; k++)
                {
                    string band = lo[k].ToString("0.0") + "~" + (hi[k] > 900f ? "" : hi[k].ToString("0.0")) + "m";
                    if (nl[k] < 50 || ns[k] < 50)
                    {
                        detail += band + " 표본 " + nl[k] + "/" + ns[k] + "px(건너뜀) · ";
                        continue;
                    }
                    float a = (float)(sl[k] / nl[k]), b2 = (float)(ss[k] / ns[k]);
                    float r = a / Mathf.Max(1f, b2);
                    detail += band + " 호수 " + a.ToString("0.0") + "(" + nl[k] + "px) ÷ 바다 " +
                              b2.ToString("0.0") + "(" + ns[k] + "px) = " + r.ToString("0.00") + " · ";
                    if (r > worst) worst = r;
                }
                return worst;
            }
            finally
            {
                if (hasGloss && !float.IsNaN(gloss)) mat.SetFloat("_Glossiness", keep);
                cam.targetTexture = null; RenderTexture.active = null;
                Object.DestroyImmediate(camGo); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// **열린 물의 흰 포화 몫**(화면 대비 %) — 흰 구멍 자의 새 대상(2026-09-11).
        ///
        /// 왜 대상을 좁혔나: 옛 자는 **화면 전체의 흰 픽셀**을 셌다. 물가 거품이 들어온 뒤로
        /// 그 자가 `15`에서 0.93%로 울었는데, 세어 보니 **호수를 두른 거품 띠**였다 —
        /// 자가 잡으려던 「정반사가 뚫은 흰 구멍」이 아니라 **이번 랩이 일부러 넣은 그림**이다.
        /// 자를 끄지 않고 **대상을 원래 뜻으로 좁힌다**: 흰 구멍은 **열린 물 한가운데**서 난다.
        /// 그래서 수심 1m 이상인 물 픽셀만 센다(물가 거품은 제 자가 따로 본다 —
        /// `OutdoorCensus.ShoreFoamStats`). 눈금은 그대로 「화면의 몇 %」다.
        ///
        /// `gloss`가 NaN이 아니면 그 매끄러움으로 재고 되돌린다(NC용).
        /// </summary>
        internal static float OpenWaterWhiteShare(string shotName, float gloss)
        {
            var shots = QaShots.BuildShots();
            int idx = -1;
            for (int i = 0; i < shots.Length; i++)
                if (QaShots.NameOf(shots[i]) == shotName) { idx = i; break; }
            if (idx < 0) return -1f;
            QaShots.EyeOf(shots[idx], out Vector3 eye, out Vector3 look);

            var camGo = new GameObject("OpenWaterWhiteCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 500f;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            camGo.transform.position = eye;
            camGo.transform.LookAt(look);

            var water = GameObject.Find(VisualSliceBuilder.WaterObject);
            var rend = water != null ? water.GetComponent<Renderer>() : null;
            var mat = rend != null ? rend.sharedMaterial : null;
            bool hasGloss = mat != null && mat.HasProperty("_Glossiness");
            float keep = hasGloss ? mat.GetFloat("_Glossiness") : 0f;
            if (hasGloss && !float.IsNaN(gloss)) mat.SetFloat("_Glossiness", gloss);
            try
            {
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                var px = tex.GetPixels32();

                int white = 0, sampled = 0;
                float y = WorldTerrain.SeaLevel;
                for (int j = 0; j < H; j += 2)
                    for (int i = 0; i < W; i += 2)
                    {
                        sampled++;
                        var ray = cam.ScreenPointToRay(new Vector3(i, j, 0f));
                        if (ray.direction.y >= -0.001f) continue;
                        float t = (y - ray.origin.y) / ray.direction.y;
                        if (t <= 0f || t > 400f) continue;
                        var p = ray.origin + ray.direction * t;
                        if (Physics.Raycast(ray.origin, ray.direction, out RaycastHit hit, t - 0.5f) &&
                            hit.distance < t - 0.5f) continue;
                        if (WorldTerrain.SeaLevel - WorldTerrain.HeightAt(p.x, p.z) < 1.0f) continue;
                        var c = px[j * W + i];
                        if (c.r >= 250 && c.g >= 250 && c.b >= 250) white++;
                    }
                return sampled > 0 ? (float)white / sampled : -1f;
            }
            finally
            {
                if (hasGloss && !float.IsNaN(gloss)) mat.SetFloat("_Glossiness", keep);
                cam.targetTexture = null; RenderTexture.active = null;
                Object.DestroyImmediate(camGo); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            }
        }

        /// <summary>구간별 색차를 찍어 본다 — 상한을 고르기 전에 눈금을 먼저 본다.</summary>
        public static void RunWaterDepthTone()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            VisualSliceBuilder.RebuildWater();   // 깊이 요구 부품이 붙은 물로 재야 한다(ShoreFoam 머리말 참조)
            foreach (float g in new[] { float.NaN, 0.55f, 0f })
            {
                float r = LakeSeaLumaRatioByDepth(g, out string det);
                Debug.Log("[물빛구간] 매끄러움 " + (float.IsNaN(g) ? "지금" : g.ToString("0.00")) +
                          " — 가장 나쁜 비 " + r.ToString("0.00") + " · " + det);
            }
            foreach (string shot in new[] { "15_lake_river", "35_fishing", "14_world_vista" })
                Debug.Log("[열린물흰몫] " + shot + " — 지금 " +
                          (OpenWaterWhiteShare(shot, float.NaN) * 100f).ToString("0.00") + "% · NC 매끄러움 0.85 " +
                          (OpenWaterWhiteShare(shot, 0.85f) * 100f).ToString("0.00") + "%");
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }
    }
}
