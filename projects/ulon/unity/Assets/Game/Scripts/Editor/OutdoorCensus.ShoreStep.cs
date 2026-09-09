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
        /// ⓒ **화면에서 직접** — `64_river_bend`를 렌더해 열마다 물↔뭍 경계를 찾고, 그 경계 y가
        /// **가로로 몇 픽셀이나 같은 값에 머무는지** 잰다. 계단 하나가 화면에서 몇 px인지가 답이다.
        /// 셀 크기를 그 거리에서의 화면 픽셀로 환산해 나란히 찍는다 — 둘이 맞으면 범인은 격자다.
        /// </summary>
        static void ScreenEdge(float cell, float alphaCell)
        {
            var shots = QaShots.BuildShots();
            int idx = -1;
            for (int i = 0; i < shots.Length; i++)
                if (QaShots.NameOf(shots[i]) == "64_river_bend") { idx = i; break; }
            if (idx < 0) { Debug.LogWarning("[물가결] 64_river_bend를 못 찾음"); return; }
            QaShots.EyeOf(shots[idx], out Vector3 eye, out Vector3 look);

            const int W = 1280, H = 720;
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
            Debug.Log("[물가결] ⓒ 화면 경계 — 표본 " + valid + "열 · 평평한 구간 " + runs + "개 · 평균 " +
                      (runs > 0 ? (sum / (float)runs).ToString("0.0") : "—") + "px · 가장 긴 것 " + longest +
                      "px · 이 거리(" + dist.ToString("0") + "m)에서 높이 셀은 " + (cell / mPerPx).ToString("0") +
                      "px · 도포 셀은 " + (alphaCell / mPerPx).ToString("0") + "px");

            Debug.Log("[물가결] ⓓ 모래↔잔디 경계 — 표본 " + sValid + "열 · 평평한 구간 " + sRuns +
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
