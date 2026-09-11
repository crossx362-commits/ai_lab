using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// **검수 가설 「잔주름 0.6m」을 셈에 건다**(2026-09-11). 가설: 완경사 물가에서 `HeightAt`에
        /// 파장 0.6m쯤의 주름이 있어 수위선을 그 파장으로 지그재그시킨다 — 격자를 흔들어도 안 따라오고
        /// 렌더를 2배로 하면 px가 2배 되는 것(세계에 붙음)이 둘 다 이 가설과 맞다.
        ///
        /// **재굽기가 필요 없다** — 원장 함수를 직접 훑는다. 물가를 가로지르는 선을 1cm로 찍고,
        /// 1차 추세(경사)를 뺀 뒤 파장 0.15~6m를 훑어 **어느 파장에 힘이 몰리는지** 본다.
        /// 0.6m에 봉우리가 서면 가설이 맞고, 없으면 **셈이 가설을 기각한다**.
        ///
        /// **이 자가 못 보는 것**: 「화면에서 그 주름이 톱니로 읽히느냐」는 여기서 안 정한다 —
        /// 진폭이 있어도 경사로 나눈 가로 흔들림이 화면 1px 아래면 눈에 안 보인다. 그 환산도 같이 찍는다.
        /// </summary>
        public static void RunShoreRipple()
        {
            float sea = WorldTerrain.SeaLevel;
            // 물가를 가로지르는 선 셋 — 바다(+x 방위) · 호수 좌하 · 강 남안.
            Probe("바다", 1f, 0f, FindWaterline(1f, 0f, 100f, 200f));
            Probe("호수", -1f, -1f, FindLakeLine());
            Probe("강", 0f, 1f, FindRiverLine());
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(0);
        }

        static Vector2 FindWaterline(float dx, float dz, float from, float to)
        {
            float len = Mathf.Sqrt(dx * dx + dz * dz);
            dx /= len; dz /= len;
            for (float r = from; r <= to; r += 0.05f)
                if (WorldTerrain.HeightAt(dx * r, dz * r) < WorldTerrain.SeaLevel)
                    return new Vector2(dx * r, dz * r);
            return new Vector2(dx * from, dz * from);
        }

        static Vector2 FindLakeLine()
        {
            float sea = WorldTerrain.SeaLevel;
            for (float r = WorldTerrain.LakeRadius + 8f; r >= 1f; r -= 0.05f)
            {
                float x = WorldTerrain.LakeX - r * 0.707f, z = WorldTerrain.LakeZ - r * 0.707f;
                if (WorldTerrain.HeightAt(x, z) < sea) return new Vector2(x, z);
            }
            return new Vector2(WorldTerrain.LakeX, WorldTerrain.LakeZ);
        }

        static Vector2 FindRiverLine()
        {
            float sea = WorldTerrain.SeaLevel;
            float x = WorldTerrain.RiverFromX - 30f;
            float cz = WorldTerrain.RiverZ + Mathf.Sin((x - WorldTerrain.RiverFromX) * 0.06f) * 6f;
            for (float d = 0.5f; d <= 30f; d += 0.05f)
                if (WorldTerrain.HeightAt(x, cz + d) >= sea) return new Vector2(x, cz + d);
            return new Vector2(x, cz);
        }

        /// <summary>물가 점에서 (dx,dz) 방향으로 ±4m를 1cm로 찍어 주름의 파장을 본다.</summary>
        static void Probe(string what, float dx, float dz, Vector2 shore)
        {
            float len = Mathf.Sqrt(dx * dx + dz * dz);
            dx /= len; dz /= len;
            const float Step = 0.01f, Half = 4f;
            int n = Mathf.RoundToInt(Half * 2f / Step);
            var h = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = -Half + i * Step;
                h[i] = WorldTerrain.HeightAt(shore.x + dx * t, shore.y + dz * t);
            }
            // 1차 추세(경사)를 뺀다 — 남는 것이 주름이다.
            float sx = 0f, sy = 0f, sxx = 0f, sxy = 0f;
            for (int i = 0; i < n; i++) { float x = i * Step; sx += x; sy += h[i]; sxx += x * x; sxy += x * h[i]; }
            float slope = (n * sxy - sx * sy) / Mathf.Max(1e-6f, n * sxx - sx * sx);
            float inter = (sy - slope * sx) / n;
            var r = new float[n];
            float rms = 0f;
            for (int i = 0; i < n; i++) { r[i] = h[i] - (inter + slope * (i * Step)); rms += r[i] * r[i]; }
            rms = Mathf.Sqrt(rms / n);

            // 파장 훑기 — 각 파장의 진폭(실수/허수 합)을 잰다.
            float bestLam = 0f, bestAmp = 0f;
            string top = "";
            for (float lam = 0.15f; lam <= 6f; lam += 0.01f)
            {
                float re = 0f, im = 0f;
                for (int i = 0; i < n; i++)
                {
                    float ph = 2f * Mathf.PI * (i * Step) / lam;
                    re += r[i] * Mathf.Cos(ph); im += r[i] * Mathf.Sin(ph);
                }
                float amp = 2f * Mathf.Sqrt(re * re + im * im) / n;
                if (amp > bestAmp) { bestAmp = amp; bestLam = lam; }
                if (Mathf.Abs(lam - 0.6f) < 0.005f)
                    top = " · **0.60m에서의 진폭 " + (amp * 1000f).ToString("0.00") + "mm**";
            }
            // 화면에서 읽히려면: 가로 흔들림 = 진폭 / 경사.
            float horiz = bestAmp / Mathf.Max(1e-4f, Mathf.Abs(slope));
            Debug.Log("[잔주름] " + what + " 물가(" + shore.x.ToString("0.0") + "," + shore.y.ToString("0.0") +
                      ") — 경사 tan " + Mathf.Abs(slope).ToString("0.000") + " · 추세 뺀 주름 RMS " +
                      (rms * 1000f).ToString("0.0") + "mm · **가장 센 파장 " + bestLam.ToString("0.00") +
                      "m(진폭 " + (bestAmp * 1000f).ToString("0.0") + "mm)**" + top +
                      " · 그 진폭이 만드는 물가 가로 흔들림 " + horiz.ToString("0.00") + "m");
        }

        /// <summary>
        /// **`65_sea_close`가 드러낸 결함 둘을 셈에 건다**(검수 2026-09-11, 원인 이름은 안 붙인다).
        ///   ⓐ 거품 띠의 **회색·검정 얼룩** — 그림자를 끄고 켜 나란히 찍는다(반투명 물이 그림자를
        ///     받는 것은 이미 확인됐고, 그 디더가 흰 거품 위에서 재처럼 보일 수 있다).
        ///   ⓑ 화면을 가로지르는 **직선 단** — 그 줄이 몇 번째 행인지 찾고, **그 행의 수심**을 카메라
        ///     레이로 풀어 찍는다. `_DepthMax`(색이 더 안 어두워지는 깊이)와 같으면 범인은 색 상한이고,
        ///     램프 3토막의 경계와 같으면 범인은 지형이다. **셈이 가른다.**
        /// </summary>
        public static void RunSeaCloseProbe()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            var td = Object.FindFirstObjectByType<Terrain>().terrainData;
            float c = td.size.x / (td.heightmapResolution - 1);
            float ac = td.size.x / td.alphamapResolution;

            ProbeTag = "sc_on";
            ScreenEdge(c, ac, "65_sea_close");
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            var keep = new LightShadows[lights.Length];
            for (int i = 0; i < lights.Length; i++) { keep[i] = lights[i].shadows; lights[i].shadows = LightShadows.None; }
            try
            {
                ProbeTag = "sc_off";
                ScreenEdge(c, ac, "65_sea_close");
            }
            finally { for (int i = 0; i < lights.Length; i++) lights[i].shadows = keep[i]; }

            // ⓒ **물을 통째로 끈다** — 거품만 꺼서는 못 가른다(물의 얕은 색이 여전히 경계를 그린다).
            // 물을 치우면 남는 것은 지형뿐이다: 그래도 같은 자리가 톱니면 **뭍 쪽**, 매끈하면 **물 쪽**이다.
            var water = GameObject.Find(VisualSliceBuilder.WaterObject);
            if (water == null) Debug.LogWarning("[근접물] 수면 오브젝트를 못 찾음 — ⓒ 건너뜀");
            else
            {
                bool was = water.activeSelf;
                try
                {
                    water.SetActive(false);
                    ProbeTag = "w_off";
                    ScreenEdge(c, ac, "15_lake_river");
                    ScreenEdge(c, ac, "65_sea_close");
                    ScreenEdge(c, ac, "64_river_bend");
                }
                finally { water.SetActive(was); }
            }

            // ⓑ 직선 단 — 화면 가운데 열의 색이 세로로 가장 크게 뛰는 행을 찾고 그 자리의 수심을 푼다.
            DepthBand();
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(0);
        }

        static void DepthBand()
        {
            var shots = QaShots.BuildShots();
            int idx = -1;
            for (int i = 0; i < shots.Length; i++)
                if (QaShots.NameOf(shots[i]) == "65_sea_close") { idx = i; break; }
            if (idx < 0) { Debug.LogWarning("[근접물] 65_sea_close를 못 찾음"); return; }
            QaShots.EyeOf(shots[idx], out Vector3 eye, out Vector3 look);

            const int W = 1280, H = 720;
            var camGo = new GameObject("SeaCloseCam");
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

            // 가운데 32열 평균으로 세로 프로파일을 만들고, 이웃 행의 차가 가장 큰 자리를 찾는다.
            var prof = new float[H];
            for (int j = 0; j < H; j++)
            {
                float s = 0f;
                for (int i = W / 2 - 16; i < W / 2 + 16; i++) { var p = px[j * W + i]; s += (p.r + p.g + p.b) / 3f; }
                prof[j] = s / 32f;
            }
            int bestJ = -1; float bestD = 0f;
            for (int j = 1; j < H; j++)
            {
                float d = Mathf.Abs(prof[j] - prof[j - 1]);
                if (d > bestD) { bestD = d; bestJ = j; }
            }
            string detail = "";
            for (int k = -2; k <= 2; k++)
            {
                int j = Mathf.Clamp(bestJ + k, 0, H - 1);
                detail += " " + (bestJ + k) + ":" + prof[j].ToString("0");
            }
            // 그 행의 수심 — 카메라 레이를 수면 평면과 만나게 하고, 그 자리의 지형 높이를 뺀다.
            float dist = DistAtRow(cam, H, bestJ);
            float depth = DepthAtRow(cam, W, H, bestJ);
            float depthAbove = DepthAtRow(cam, W, H, Mathf.Max(0, bestJ - 12));
            float depthBelow = DepthAtRow(cam, W, H, Mathf.Min(H - 1, bestJ + 12));
            var mat = FindWaterMatForProbe();
            float dmax = mat != null && mat.HasProperty("_DepthMax") ? mat.GetFloat("_DepthMax") : -1f;
            Debug.Log("[근접물] 가로 단 — **그 행까지 거리 " + dist.ToString("0") + "m** · 안개 시작 " +
                      RenderSettings.fogStartDistance.ToString("0") + "m(끝 " +
                      RenderSettings.fogEndDistance.ToString("0") + "m · 켜짐 " + RenderSettings.fog + ")");
            Debug.Log("[근접물] 가로 단 — 화면 " + bestJ + "행(아래에서)에서 밝기가 " + bestD.ToString("0.0") +
                      " 뛴다(둘레" + detail + ") · **그 자리 수심 " + depth.ToString("0.00") + "m**" +
                      "(12행 위 " + depthAbove.ToString("0.00") + "m · 12행 아래 " + depthBelow.ToString("0.00") +
                      "m) · 물 재질의 `_DepthMax` " + dmax.ToString("0.00") + "m · 지형 램프 경계는 " +
                      "BeachEnd " + WorldTerrain.BeachEndM.ToString("0") + "m / CoastEnd " +
                      WorldTerrain.CoastEnd.ToString("0") + "m");

            cam.targetTexture = null; RenderTexture.active = null;
            Object.DestroyImmediate(camGo); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
        }

        static float DistAtRow(Camera cam, int H, int row)
        {
            var ray = cam.ViewportPointToRay(new Vector3(0.5f, row / (float)H, 0f));
            float sea = WorldTerrain.SeaLevel;
            if (ray.direction.y >= -1e-4f) return -1f;
            float t = (sea - ray.origin.y) / ray.direction.y;
            return t;
        }

        static float DepthAtRow(Camera cam, int W, int H, int row)
        {
            var ray = cam.ViewportPointToRay(new Vector3(0.5f, row / (float)H, 0f));
            float sea = WorldTerrain.SeaLevel;
            if (ray.direction.y >= -1e-4f) return -1f;                 // 수면을 안 만난다
            float t = (sea - ray.origin.y) / ray.direction.y;
            var p = ray.origin + ray.direction * t;
            return sea - WorldTerrain.HeightAt(p.x, p.z);
        }

        static Material FindWaterMatForProbe()
        {
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                var m = r.sharedMaterial;
                if (m != null && m.shader != null && m.shader.name.Contains("StylizedWater")) return m;
            }
            return null;
        }
    }
}
