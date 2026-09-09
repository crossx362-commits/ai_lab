using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// **`64` 왼쪽 강둑이 왜 바둑판인가 — 이름부터 센다**(검수 지시 2026-09-10).
        ///
        /// 텍셀 랩(`05376e3e`)이 해상도를 128→256으로 올려 풀·물의 잔결을 부드럽게 했는데도
        /// 그 둑은 통째로 바둑판이다. 세 해상도 판이 그 자리에서 똑같았으니 **텍셀이 아니라 무늬**다.
        /// 유력한 이름은 pattern 1의 6칸 해시지만 **확정하지 않는다** — 검수 지시대로 순서는
        /// ⓐ그 자리를 **무슨 겹이 칠하는지** ⓑ그 겹의 무늬가 **실제로 몇 cm 주기인지**
        /// ⓒ화면에서 그 주기가 **몇 px로 보이는지**다.
        ///
        /// ⓑ는 무늬 코드를 읽지 않고 **텍스처 자체의 자기상관**으로 잰다 — 생성기가 어떻게
        /// 생겼든 「반복 덩어리의 주기」는 그림에 남는다. 코드를 읽고 「6칸이니 21cm겠지」로
        /// 적는 것은 셈이 아니라 짐작이다.
        ///
        /// **이 자가 못 보는 것**: 「무엇으로 바꿔야 흙으로 읽히나」는 못 잰다. 여기까지는
        /// 범인을 이름 대는 데까지다.
        /// </summary>
        public static void RunBankBlob()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain == null) { Debug.LogError("[둑무늬] 지형 없음"); return; }
            var data = terrain.terrainData;
            var layers = data.terrainLayers;

            // ⓑ 겹마다 **제 그림에서** 반복 주기를 잰다(가로 자기상관).
            foreach (var tl in layers)
            {
                var tx = tl != null ? tl.diffuseTexture : null;
                if (tx == null) continue;
                float mPerTexel = tl.tileSize.x / tx.width;
                Debug.Log("[둑무늬] ⓑ " + tl.name + " — " + tx.width + "px · 타일 " +
                          tl.tileSize.x.ToString("0.0") + "m · 텍셀 " + (mPerTexel * 100f).ToString("0.0") + "cm");
                int lag = TexturePeriod(tx, out float _);
                Debug.Log("[둑무늬]   상관 길이 " + lag + "텍셀 = **" +
                          (lag * mPerTexel * 100f).ToString("0") + "cm** (덩어리 크기)");
            }

            var shots = QaShots.BuildShots();
            int idx = -1;
            for (int i = 0; i < shots.Length; i++)
                if (QaShots.NameOf(shots[i]) == "64_river_bend") { idx = i; break; }
            if (idx < 0) { Debug.LogError("[둑무늬] 64_river_bend 없음"); return; }
            QaShots.EyeOf(shots[idx], out Vector3 eye, out Vector3 look);

            const int W = 1280, H = 720;
            var camGo = new GameObject("BankBlobCam");
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

            // ⓐ **왼쪽 둑이 무슨 겹인가.** 화면 상자는 바둑판이 보이는 자리다(아래에서 센 y 좌표).
            const int X0 = 150, X1 = 620, Y0 = 300, Y1 = 590;
            int ar = data.alphamapResolution;
            var maps = data.GetAlphamaps(0, 0, ar, ar);
            Vector3 tpos = terrain.transform.position;
            var share = new Dictionary<string, int>();
            int hit = 0;
            double mPerPxSum = 0; int mn = 0;
            for (int j = Y0; j < Y1; j += 4)
            {
                for (int i = X0; i < X1; i += 4)
                {
                    var r1 = cam.ScreenPointToRay(new Vector3(i, j, 0f));
                    if (!Physics.Raycast(r1, out RaycastHit h1, 500f)) continue;
                    if (!(h1.collider is TerrainCollider)) continue;
                    var r2 = cam.ScreenPointToRay(new Vector3(i + 1, j, 0f));
                    if (Physics.Raycast(r2, out RaycastHit h2, 500f) && h2.collider is TerrainCollider)
                    {
                        float step = Vector3.Distance(h1.point, h2.point);
                        if (step > 0.0001f && step < 2f) { mPerPxSum += step; mn++; }
                    }
                    float u = (h1.point.x - tpos.x) / data.size.x;
                    float v = (h1.point.z - tpos.z) / data.size.z;
                    int axi = Mathf.Clamp(Mathf.RoundToInt(u * (ar - 1)), 0, ar - 1);
                    int azi = Mathf.Clamp(Mathf.RoundToInt(v * (ar - 1)), 0, ar - 1);
                    int best = 0; float bw = -1f;
                    for (int L = 0; L < layers.Length; L++)
                        if (maps[azi, axi, L] > bw) { bw = maps[azi, axi, L]; best = L; }
                    string nm = layers[best] != null ? layers[best].name : "?";
                    share[nm] = share.TryGetValue(nm, out int cnt) ? cnt + 1 : 1;
                    hit++;
                }
            }
            float mPerPx = mn > 0 ? (float)(mPerPxSum / mn) : 0f;
            string tally = "";
            foreach (var kv in share)
                tally += " · " + kv.Key + " " + (100f * kv.Value / Mathf.Max(1, hit)).ToString("0.0") + "%";
            Debug.Log("[둑무늬] ⓐ 왼쪽 둑 상자(" + X0 + "~" + X1 + "," + Y0 + "~" + Y1 + ") 표본 " +
                      hit + "곳 — 1등 층 비율" + tally + " · 화면 한 px = " +
                      (mPerPx * 100f).ToString("0.0") + "cm");

            // ⓒ **화면에서 그 덩어리가 몇 px 주기인가.** 밝기 가로 자기상관.
            int screenLag = ScreenPeriod(px, W, X0, X1, Y0, Y1, out float _);
            Debug.Log("[둑무늬] ⓒ 화면 덩어리 크기 — 상관 길이 " + screenLag + "px = 세계 " +
                      (screenLag * mPerPx * 100f).ToString("0") + "cm");

            // ⓓ **범인을 이름 대기 전에 판별 테스트.** 같은 카메라에서 조건만 바꿔 나란히 렌더하고
            // 상자 안 **잔결의 세기**(21px 이동평균을 뺀 나머지의 RMS)를 잰다. 셈 ⓒ의 상관 길이는
            // 비탈의 **밝기 기울기**에 눌려 1.5m로 나왔다 — 자가 큰 것에 눌리면 작은 것을 못 본다.
            //   A 지금 · B 자갈 겹을 무늬 없이 다시 구움 · C 해 끔(법선 음영 제거)
            // 첫 판은 B를 `TerrainLayer.diffuseTexture` 교체로 했는데 **평균 밝기까지 똑같이 나왔다** —
            // 안 먹은 것이다. 조건이 화면을 안 바꾸면 그 조건은 아무 말도 못 한다. 그래서 **굽는 길**로 한다.
            var sun = BankSun();
            float rmsA = RoiRms(cam, rt, W, H, X0, X1, Y0, Y1, out float meanA);
            VisualSliceBuilder.FlatLayerForCensus = "MineGravel";
            VisualSliceBuilder.EnsureVillageTerrain();
            UnityEditor.AssetDatabase.SaveAssets();
            float rmsB = RoiRms(cam, rt, W, H, X0, X1, Y0, Y1, out float meanB);
            VisualSliceBuilder.FlatLayerForCensus = "";
            VisualSliceBuilder.EnsureVillageTerrain();
            UnityEditor.AssetDatabase.SaveAssets();
            bool sunWas = sun != null && sun.enabled;
            if (sun != null) sun.enabled = false;
            float rmsC = RoiRms(cam, rt, W, H, X0, X1, Y0, Y1, out float meanC);
            if (sun != null) sun.enabled = sunWas;
            Debug.Log("[둑무늬] ⓓ 판별 — A 지금 " + rmsA.ToString("0.00") + "(밝기 " + meanA.ToString("0.0") +
                      ") · B 자갈 무늬 지움 " + rmsB.ToString("0.00") + "(" + meanB.ToString("0.0") +
                      ") · C 해 끔 " + rmsC.ToString("0.00") + "(" + meanC.ToString("0.0") +
                      ") — 밝기가 같은 조건은 안 먹은 것이다");

            // ⓔ **잔결만 남기고 다시 잰 덩어리 크기.** 이동평균을 빼면 비탈 음영이 빠지고
            // 무늬·법선만 남는다. 이 길이를 **높이 셀**·**텍셀**과 견주면 이름이 나온다.
            cam.Render();
            RenderTexture.active = rt;
            var tex2 = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex2.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex2.Apply();
            RenderTexture.active = null;
            int hpLag = ScreenPeriod(tex2.GetPixels32(), W, X0, X1, Y0, Y1, out float _, true);
            var td = terrain.terrainData;
            float cellPx = (td.size.x / (td.heightmapResolution - 1)) / Mathf.Max(0.0001f, mPerPx);
            float gravelTexelPx = 0f;
            var gl = System.Array.Find(td.terrainLayers, L => L != null && L.name == "MineGravel");
            if (gl != null && gl.diffuseTexture != null)
                gravelTexelPx = (gl.tileSize.x / gl.diffuseTexture.width) / Mathf.Max(0.0001f, mPerPx);
            Debug.Log("[둑무늬] ⓔ 잔결 덩어리 — 상관 길이 " + hpLag + "px = 세계 " +
                      (hpLag * mPerPx * 100f).ToString("0") + "cm · 견줌: 높이 셀 " +
                      cellPx.ToString("0.0") + "px · 자갈 텍셀 " + gravelTexelPx.ToString("0.00") + "px");
            Object.DestroyImmediate(tex2);

            // ⓕ **도포가 칸마다 뒤집히지 않는가.** 자갈(어둠)과 모래(밝음)가 이웃 칸에서 서로
            // 자리를 바꾸면 화면은 두 색 바둑판이 된다 — 무늬가 아니라 **섞임**이 범인인 경우다.
            // 물가 모래는 사면 25~34°에서 자갈과 몫을 나눠 갖는데(`WorldSplat.ShoreSlopeLimit`),
            // 둑이 그 구간이면 경사의 작은 흔들림이 그대로 밝기 뒤집기가 된다.
            int gi = System.Array.FindIndex(td.terrainLayers, L => L != null && L.name == "MineGravel");
            int si = System.Array.FindIndex(td.terrainLayers, L => L != null && L.name == "ShoreSand");
            if (gi >= 0 && si >= 0)
            {
                // 상자가 덮는 세계 사각형을 알파맵 칸으로 훑는다(레이캐스트로 모은 범위).
                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                for (int j = Y0; j < Y1; j += 8)
                    for (int i = X0; i < X1; i += 8)
                    {
                        if (!Physics.Raycast(cam.ScreenPointToRay(new Vector3(i, j, 0f)), out RaycastHit hh, 500f)) continue;
                        if (!(hh.collider is TerrainCollider)) continue;
                        minX = Mathf.Min(minX, hh.point.x); maxX = Mathf.Max(maxX, hh.point.x);
                        minZ = Mathf.Min(minZ, hh.point.z); maxZ = Mathf.Max(maxZ, hh.point.z);
                    }
                int ax0 = Mathf.Clamp(Mathf.FloorToInt((minX - tpos.x) / td.size.x * (ar - 1)), 0, ar - 2);
                int ax1 = Mathf.Clamp(Mathf.CeilToInt((maxX - tpos.x) / td.size.x * (ar - 1)), 0, ar - 1);
                int az0 = Mathf.Clamp(Mathf.FloorToInt((minZ - tpos.z) / td.size.z * (ar - 1)), 0, ar - 2);
                int az1 = Mathf.Clamp(Mathf.CeilToInt((maxZ - tpos.z) / td.size.z * (ar - 1)), 0, ar - 1);
                double jump = 0; int jn = 0, flip = 0;
                for (int z = az0; z <= az1; z++)
                    for (int x = ax0; x < ax1; x++)
                    {
                        float g0 = maps[z, x, gi], g1 = maps[z, x + 1, gi];
                        float s0 = maps[z, x, si], s1 = maps[z, x + 1, si];
                        jump += Mathf.Abs(g1 - g0); jn++;
                        if ((g0 > s0) != (g1 > s1)) flip++;
                    }
                Debug.Log("[둑무늬] ⓕ 도포 뒤집힘 — 칸 " + jn + "쌍 · 이웃 칸 자갈 몫 차 평균 " +
                          (jn > 0 ? (jump / jn).ToString("0.000") : "—") + " · **1등이 뒤집히는 칸 " +
                          (jn > 0 ? (100f * flip / jn).ToString("0.0") : "—") + "%** · 알파맵 칸은 화면 " +
                          ((td.size.x / ar) / Mathf.Max(0.0001f, mPerPx)).ToString("0.0") + "px");
            }

            cam.targetTexture = null; RenderTexture.active = null;
            Object.DestroyImmediate(camGo); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(0);
        }

        /// <summary>가장 센 방향광 — 해를 끄는 판별 조건에 쓴다.</summary>
        static Light BankSun()
        {
            Light best = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && (best == null || l.intensity > best.intensity))
                    best = l;
            return best;
        }

        /// <summary>
        /// 상자 안 **잔결의 세기** — 21px 이동평균(저주파: 비탈 음영·그림자)을 뺀 나머지의 RMS.
        /// 무늬가 화면에서 얼마나 도드라지는지를 큰 밝기 기울기와 분리해 잰다.
        /// </summary>
        static float RoiRms(Camera cam, RenderTexture rt, int W, int H, int x0, int x1, int y0, int y1)
        {
            return RoiRms(cam, rt, W, H, x0, x1, y0, y1, out float _);
        }

        static float RoiRms(Camera cam, RenderTexture rt, int W, int H, int x0, int x1, int y0, int y1, out float mean)
        {
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            var px = tex.GetPixels32();
            const int K = 10;                 // 이동평균 반폭 → 21px
            double acc = 0; int n = 0;
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0 + K; x < x1 - K; x++)
                {
                    double m = 0;
                    for (int k = -K; k <= K; k++)
                    {
                        var q = px[y * W + x + k];
                        m += (q.r + q.g + q.b) / 3.0;
                    }
                    m /= (2 * K + 1);
                    var c = px[y * W + x];
                    double e = (c.r + c.g + c.b) / 3.0 - m;
                    acc += e * e; n++;
                }
            }
            double lumSum = 0; int ln = 0;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                { var q = px[y * W + x]; lumSum += (q.r + q.g + q.b) / 3.0; ln++; }
            mean = ln > 0 ? (float)(lumSum / ln) : 0f;
            Object.DestroyImmediate(tex);
            return n > 0 ? Mathf.Sqrt((float)(acc / n)) : 0f;
        }

        /// <summary>텍스처 가로 자기상관의 첫 봉우리 — 「몇 텍셀마다 같은 무늬가 오나」.</summary>
        static int TexturePeriod(Texture2D tx, out float strength)
        {
            strength = 0f;
            Texture2D readable = MakeReadable(tx);
            if (readable == null) return 0;
            int w = readable.width, h = readable.height;
            var p = readable.GetPixels32();
            var lum = new float[w * h];
            double mean = 0;
            for (int i = 0; i < w * h; i++) { lum[i] = (p[i].r + p[i].g + p[i].b) / 3f; mean += lum[i]; }
            mean /= w * h;
            for (int i = 0; i < w * h; i++) lum[i] -= (float)mean;
            double var0 = 0;
            for (int i = 0; i < w * h; i++) var0 += lum[i] * lum[i];
            var0 /= w * h;
            // **곡선을 통째로 찍는다.** 봉우리 하나만 고르면 「가장 작은 lag」이 늘 이긴다
            // (자기상관은 0에서 단조로 떨어진다) — 그건 주기가 아니라 **감쇠**다.
            string curve = "";
            int half = 0;
            for (int lag = 1; lag <= w / 3; lag++)
            {
                double acc = 0; int n = 0;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x + lag < w; x++)
                    { acc += lum[y * w + x] * lum[y * w + x + lag]; n++; }
                float r = n > 0 && var0 > 0 ? (float)(acc / n / var0) : 0f;
                if (half == 0 && r < 0.5f) half = lag;
                if (lag == 1 || lag == 2 || lag == 4 || lag == 6 || lag == 8 || lag == 12 ||
                    lag == 16 || lag == 24 || lag == 32 || lag == 48 || lag == 64)
                    curve += " " + lag + ":" + r.ToString("0.00");
            }
            strength = half;                 // 상관 길이(덩어리 반지름) — 텍셀 단위
            Debug.Log("[둑무늬]   r(lag)" + curve);
            Object.DestroyImmediate(readable);
            return half;
        }

        /// <summary>임포터 설정과 무관하게 읽으려고 RenderTexture로 한 번 옮긴다.</summary>
        static Texture2D MakeReadable(Texture2D src)
        {
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var dst = new Texture2D(src.width, src.height, TextureFormat.RGB24, false);
            dst.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            dst.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return dst;
        }

        /// <summary>화면 상자 안 밝기의 가로 자기상관 첫 봉우리(px).</summary>
        static int ScreenPeriod(Color32[] px, int W, int x0, int x1, int y0, int y1, out float strength)
        {
            return ScreenPeriod(px, W, x0, x1, y0, y1, out strength, false);
        }

        static int ScreenPeriod(Color32[] px, int W, int x0, int x1, int y0, int y1, out float strength, bool highPass)
        {
            strength = 0f;
            int w = x1 - x0;
            var lum = new List<float[]>();
            for (int y = y0; y < y1; y++)
            {
                var row = new float[w];
                double m = 0;
                for (int x = 0; x < w; x++) { row[x] = (px[y * W + x0 + x].r + px[y * W + x0 + x].g + px[y * W + x0 + x].b) / 3f; m += row[x]; }
                m /= w;
                // 줄마다 평균을 빼야 **조명 기울기**가 주기로 잡히지 않는다.
                for (int x = 0; x < w; x++) row[x] -= (float)m;
                if (highPass)
                {
                    // 21px 이동평균을 한 번 더 빼면 비탈 음영이 빠지고 잔결만 남는다.
                    var hp = new float[w];
                    const int K = 10;
                    for (int x = 0; x < w; x++)
                    {
                        double a2 = 0; int c2 = 0;
                        for (int k = -K; k <= K; k++)
                        { int q = x + k; if (q < 0 || q >= w) continue; a2 += row[q]; c2++; }
                        hp[x] = row[x] - (float)(a2 / c2);
                    }
                    row = hp;
                }
                lum.Add(row);
            }
            double var0 = 0; int vn = 0;
            foreach (var row in lum)
                for (int x = 0; x < w; x++) { var0 += row[x] * row[x]; vn++; }
            var0 = vn > 0 ? var0 / vn : 0;
            string curve = "";
            int half = 0;
            for (int lag = 1; lag < 80; lag++)
            {
                double acc = 0; int n = 0;
                foreach (var row in lum)
                    for (int x = 0; x + lag < w; x++) { acc += row[x] * row[x + lag]; n++; }
                float r = n > 0 && var0 > 0 ? (float)(acc / n / var0) : 0f;
                if (half == 0 && r < 0.5f) half = lag;
                if (lag <= 8 || lag == 12 || lag == 16 || lag == 24 || lag == 32 || lag == 48 || lag == 64)
                    curve += " " + lag + ":" + r.ToString("0.00");
            }
            Debug.Log("[둑무늬]   화면 r(lag)" + curve);
            strength = half;
            return half;
        }
    }
}
