using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **수면이 「되풀이 도장」으로 읽히는가**를 재는 자(검수 지시 2026-09-10).
    ///
    /// 화면에서 되풀이 주기를 재려던 첫 판은 실패했다 — `64_river_bend`는 후보 A/B/C에서
    /// 수치가 소수점까지 같은데 **화면은 완전히 달랐다**(메달 격자 → 흐르는 물). 그 자는
    /// 그 자리에서 수면 텍스처를 못 보고 있었다. 그래서 자를 **텍스처 쪽**으로 옮겼다.
    ///
    /// 「성긴 눈금으로 뭉갠 덩어리 도드라짐」(`Stampiness`)도 지어 봤지만 **눈과 반대**를 가리켜
    /// 게이트로는 못 쓴다 — 후보를 볼 때 참고로만 찍는다. 게이트가 쓰는 것은
    /// **`Anisotropy` — 결이 방향을 가지는가**다(`SliceSelfCheck.WaterFlow`에 전말).
    /// </summary>
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// 수면 무늬의 **결이 갈리는 정도**(게이트 `AssertWaterNotStamped`가 쓴다).
        /// 클수록 흐르는 물, 0에 가까울수록 사방이 같은 격자 도장.
        /// </summary>
        public static float WaterFlowiness()
        {
            var mat = FindWaterMaterial();
            if (mat == null || mat.mainTexture == null)
                throw new System.InvalidOperationException("수면 재질/텍스처를 찾지 못했습니다 — 자가 헛돕니다.");
            var src = mat.mainTexture as Texture2D;
            if (src == null)
                throw new System.InvalidOperationException("수면 텍스처가 Texture2D가 아닙니다 — 자가 헛돕니다.");
            var tex = MakeReadable(src);
            string detail;
            float v = Anisotropy(tex, 4, out detail);   // 결의 길이가 4텍셀쯤이다
            Object.DestroyImmediate(tex);
            return v;
        }

        /// <summary>
        /// 텍스처를 <paramref name="n"/>×<paramref name="n"/> 칸으로 뭉갠 뒤,
        /// 칸 평균의 표준편차를 원본 표준편차로 나눈다 —
        /// **큰 덩어리가 있으면 뭉개도 살아남고, 잔결뿐이면 사라진다.**
        /// </summary>
        static float Stampiness(Texture2D tex, int n)
        {
            int W = tex.width, H = tex.height;
            var px = tex.GetPixels32();
            var lum = new float[W * H];
            double m = 0;
            for (int i = 0; i < px.Length; i++) { lum[i] = (px[i].r + px[i].g + px[i].b) / 3f; m += lum[i]; }
            m /= px.Length;
            double vAll = 0;
            for (int i = 0; i < lum.Length; i++) { double d = lum[i] - m; vAll += d * d; }
            vAll /= lum.Length;
            if (vAll < 1e-6) return 0f;

            var cell = new double[n * n];
            var cnt = new int[n * n];
            for (int y = 0; y < H; y++)
            {
                int cy = y * n / H;
                for (int x = 0; x < W; x++)
                {
                    int c = cy * n + x * n / W;
                    cell[c] += lum[y * W + x]; cnt[c]++;
                }
            }
            double vc = 0;
            for (int c = 0; c < cell.Length; c++)
            {
                double a = cnt[c] > 0 ? cell[c] / cnt[c] : m;
                double d = a - m; vc += d * d;
            }
            vc /= cell.Length;
            return (float)(System.Math.Sqrt(vc) / System.Math.Sqrt(vAll));
        }

        /// <summary>
        /// **결이 방향을 가지는가**. lag <paramml/>만큼 떨어진 이웃과의 상관을 여덟 방향으로 재서
        /// 가장 큰 값과 가장 작은 값의 차를 돌려준다. 격자로 늘어선 도장은 사방이 같아 0에 가깝고,
        /// 흐르는 물결은 결을 따라 크게 갈린다.
        /// </summary>
        static float Anisotropy(Texture2D tex, int lag, out string detail)
        {
            int W = tex.width, H = tex.height;
            var px = tex.GetPixels32();
            var lum = new float[W * H];
            double m = 0;
            for (int i = 0; i < px.Length; i++) { lum[i] = (px[i].r + px[i].g + px[i].b) / 3f; m += lum[i]; }
            m /= px.Length;
            double v = 0;
            for (int i = 0; i < lum.Length; i++) { double d = lum[i] - m; v += d * d; }
            v /= lum.Length;
            if (v < 1e-6) { detail = "(평탄)"; return 0f; }

            int[] dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
            int[] dy = { 0, 1, 1, 1, 0, -1, -1, -1 };
            float lo = 9f, hi = -9f; detail = "";
            for (int k = 0; k < 8; k++)
            {
                double s = 0; int n = 0;
                int ox = dx[k] * lag, oy = dy[k] * lag;
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        int x2 = ((x + ox) % W + W) % W, y2 = ((y + oy) % H + H) % H;
                        s += (lum[y * W + x] - m) * (lum[y2 * W + x2] - m); n++;
                    }
                float r = (float)(s / n / v);
                detail += " " + r.ToString("0.00");
                if (r < lo) lo = r; if (r > hi) hi = r;
            }
            return hi - lo;
        }

        static Material FindWaterMaterial()
        {
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                var mm = r.sharedMaterial;
                if (mm != null && mm.name.StartsWith("SeaWater")) return mm;
            }
            return null;
        }

        /// <summary>후보 무늬별로 자를 찍어 본다(사람이 고르라고).</summary>
        public static void RunWaterStamp()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            int keep = VisualSliceBuilder.WaterPatternOverride;
            try
            {
                foreach (int p in new[] { 0, 7 })
                {
                    VisualSliceBuilder.WaterPatternOverride = p;
                    VisualSliceBuilder.RebuildWater();
                    var mat = FindWaterMaterial();
                    var tex = MakeReadable(mat.mainTexture as Texture2D);
                    Debug.Log("[수면도장] 무늬 " + p +
                              " — 16칸 " + Stampiness(tex, 16).ToString("0.000") +
                              " · 8칸 " + Stampiness(tex, 8).ToString("0.000") +
                              " · 4칸 " + Stampiness(tex, 4).ToString("0.000"));
                    foreach (int lag in new[] { 4, 8, 16 })
                    {
                        string d;
                        float an = Anisotropy(tex, lag, out d);
                        Debug.Log("[수면결] 무늬 " + p + " lag " + lag + " — 방향 갈림 " +
                                  an.ToString("0.000") + " ·" + d);
                    }
                    Object.DestroyImmediate(tex);
                }
            }
            finally
            {
                VisualSliceBuilder.WaterPatternOverride = keep;
                VisualSliceBuilder.RebuildWater();
            }
        }
    }
}
