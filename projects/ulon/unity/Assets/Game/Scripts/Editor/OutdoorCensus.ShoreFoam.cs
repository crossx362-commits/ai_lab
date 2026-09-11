using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// **물가에 깊이 페이드/거품이 있나** — 물 재작업 1단계가 세운 자(검수 조건 2026-09-11).
        ///
        /// 무엇을 재나: 같은 화면의 **물 픽셀만** 골라 수심으로 두 무리로 가르고 밝기 차를 낸다.
        ///   얕은 띠 = 수심 0.05~0.5m(물가) · 열린 물 = 수심 2m 이상
        /// 물가가 열린 물보다 밝지 않으면 깊이 페이드도 거품도 화면에 없는 것이다.
        /// **밝기 차와 흰 몫을 같이** 낸다(아래 `ShoreFoamStats` 참조) — 밝기 차는 거품 색을
        /// 바꿔도 안 무너지지만 **거품이 사라진 것을 못 본다**, 흰 몫은 그 반대다. 한 수로 둘을
        /// 재려던 첫 판이 정확히 그 구멍을 냈다(거품을 꺼도 48.8 → 43.4).
        ///
        /// 물 픽셀 고르는 법은 색차 자와 **같다**(광선을 수면 평면에 쏘고 지형에 먼저 막히면 버린다) —
        /// 두 벌을 두면 자와 셈이 다른 세계를 본다. 수심은 화면색이 아니라 **원장 지형 함수**에서
        /// 얻는다(`WorldTerrain.HeightAt`).
        ///
        /// **이 자가 못 보는 것**: 거품의 모양과 굵기다. 띠가 곧은 테두리처럼 그려져도 밝기 차는
        /// 같게 나온다 — 합격선은 눈이고, 이 자는 「없어졌다」를 잡는다.
        /// </summary>
        internal static float ShoreFoamContrast(string shotName, out string detail)
        {
            return ShoreFoamStats(shotName, out _, out detail);
        }

        /// <summary>
        /// 물가의 두 성질을 한 렌더에서 같이 낸다 — 반환값은 **밝기 차**(깊이 페이드), `whiteShare`는
        /// 얕은 띠에서 **거의 흰 픽셀의 몫**(거품). 둘을 따로 두는 이유는 **네거티브 컨트롤이 갈리기**
        /// 때문이다: 거품만 껐을 때 밝기 차는 48.8 → 43.4로 거의 안 떨어진다(대부분이 깊이 색의 몫이라
        /// 그렇다). 밝기 차 하나만 보면 **거품이 통째로 사라져도 초록불**이다. 흰 몫이 그 구멍을 막는다.
        /// </summary>
        internal static float ShoreFoamStats(string shotName, out float whiteShare, out string detail)
        {
            return ShoreFoamStats(shotName, out whiteShare, out _, out detail);
        }

        /// <summary>
        /// **급경사 물가는 흰 몫의 대상이 아니다**(1.7b, 검수 지시 「하한을 낮추지 말고 표본을 갈라라」).
        /// 얕은 띠 픽셀 중 **원장 바닥 기울기가 가파른 자리**는 정답이 「그 자리엔 거품이 없다」라
        /// 하한으로 물을 대상이 아니다 — 그런데 분모에는 들어가 있어 완경사 물의 몫을 끌어내린다.
        /// 갈라낸 표본 수를 `excluded`로 돌려주고, 어느 샷에서도 0이면 **죽은 예외**로 실패시킨다
        /// (아무것도 안 거르는 예외는 자를 무르게 만들 뿐이다).
        /// </summary>
        internal static float ShoreFoamStats(string shotName, out float whiteShare, out int excluded, out string detail)
        {
            detail = "";
            whiteShare = -1f;
            excluded = 0;
            const float SteepShoreDeg = 35f;
            var shots = QaShots.BuildShots();
            int idx = -1;
            for (int i = 0; i < shots.Length; i++)
                if (QaShots.NameOf(shots[i]) == shotName) { idx = i; break; }
            if (idx < 0) return -1f;
            QaShots.EyeOf(shots[idx], out Vector3 eye, out Vector3 look);

            const int W = 1280, H = 720;
            var camGo = new GameObject("_foamCam");
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

                double sShallow = 0, sOpen = 0;
                int nShallow = 0, nOpen = 0, nWhite = 0, nGentle = 0;
                float y = WorldTerrain.SeaLevel;
                for (int j = 0; j < H; j += 2)
                    for (int i = 0; i < W; i += 2)
                    {
                        var ray = cam.ScreenPointToRay(new Vector3(i, j, 0f));
                        if (ray.direction.y >= -0.001f) continue;
                        float t = (y - ray.origin.y) / ray.direction.y;
                        if (t <= 0f || t > 400f) continue;              // 수평선은 물이 아니다
                        var p = ray.origin + ray.direction * t;
                        if (Physics.Raycast(ray.origin, ray.direction, out RaycastHit hit, t - 0.5f) &&
                            hit.distance < t - 0.5f) continue;          // 뭍에 막힌 픽셀
                        float d = WorldTerrain.SeaLevel - WorldTerrain.HeightAt(p.x, p.z);
                        var c = px[j * W + i];
                        float lum = (c.r + c.g + c.b) / 3f;
                        if (d > 0.05f && d <= 0.5f)
                        {
                            sShallow += lum; nShallow++;
                            // **급경사 물가는 흰 몫의 대상이 아니다** — 분모에서도 뺀다.
                            if (BottomSlopeDeg(p.x, p.z) >= SteepShoreDeg) { excluded++; continue; }
                            nGentle++;
                            // 거품은 **흰색**이다 — 물빛이 아무리 밝아도 여기까지는 안 온다.
                            if (c.r >= 215 && c.g >= 215 && c.b >= 215) nWhite++;
                        }
                        else if (d >= 2.0f) { sOpen += lum; nOpen++; }
                    }
                if (nShallow < 40 || nOpen < 40 || nGentle < 20)
                {
                    detail = "표본 얕은 " + nShallow + "px(완경사 " + nGentle + ") · 열린 " + nOpen + "px — 못 잽니다";
                    return -1f;
                }
                float a = (float)(sShallow / nShallow), b = (float)(sOpen / nOpen);
                whiteShare = (float)nWhite / nGentle;
                detail = "얕은 띠 " + a.ToString("0.0") + "(" + nShallow + "px 중 완경사 " + nGentle +
                         "px·급경사 " + excluded + "px 뺌, 흰 몫 " +
                         (whiteShare * 100f).ToString("0.0") + "%) · 열린 물 " +
                         b.ToString("0.0") + "(" + nOpen + "px)";
                return a - b;
            }
            finally
            {
                cam.targetTexture = null; RenderTexture.active = null;
                Object.DestroyImmediate(camGo); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            }
        }

        /// <summary>물가 대비를 샷별로 재고, **거품을 끈 판**을 나란히 찍는다(네거티브 컨트롤).</summary>
        public static void RunShoreFoam()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            // **다시 굽고 시작한다** — 디스크의 씬에는 깊이 요구 부품이 없을 수 있고, 그러면
            // 깊이 텍스처가 안 구워져 물이 「어디서나 깊은 색」으로 렌더된다. 첫 판에 이걸
            // 안 해서 NC 세 판이 **소수점까지 같은 값**을 냈다(조건을 바꿨는데 화면이 안 바뀌면
            // 그 조건이 안 닿은 것이다 — 자를 의심하기 전에 배선을 의심하라).
            VisualSliceBuilder.RebuildWater();
            var mat = FindWaterMaterial();
            if (mat == null) { Debug.LogError("[물가거품] 수면 재질을 못 찾았습니다."); return; }
            float f1 = mat.HasProperty("_FoamDepthSteep") ? mat.GetFloat("_FoamDepthSteep") : -1f;
            float dm = mat.HasProperty("_DepthMax") ? mat.GetFloat("_DepthMax") : -1f;

            foreach (string shot in new[] { "64_river_bend", "63_pier_cutface", "15_lake_river", "14_world_vista", "65_sea_close" })
            {
                float v = ShoreFoamStats(shot, out _, out string det);
                Debug.Log("[물가거품] " + shot + " — 대비 " + v.ToString("0.0") + " · " + det);
            }

            if (f1 >= 0f)
            {
                mat.SetFloat("_FoamDepthSteep", 0f);
                foreach (string shot in new[] { "64_river_bend", "63_pier_cutface" })
                {
                    float v = ShoreFoamStats(shot, out _, out string det);
                    Debug.Log("[물가거품] NC 거품끔 " + shot + " — 대비 " + v.ToString("0.0") + " · " + det);
                }
                mat.SetFloat("_DepthMax", 0.01f);   // 깊이 색까지 끄면 물가 대비가 남나(둘째 NC)
                foreach (string shot in new[] { "64_river_bend" })
                {
                    float v = ShoreFoamStats(shot, out _, out string det);
                    Debug.Log("[물가거품] NC 거품·깊이색 둘 다 끔 " + shot + " — 대비 " + v.ToString("0.0") + " · " + det);
                }
                mat.SetFloat("_FoamDepthSteep", f1);
                mat.SetFloat("_DepthMax", dm);
            }

            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }
    }
}
