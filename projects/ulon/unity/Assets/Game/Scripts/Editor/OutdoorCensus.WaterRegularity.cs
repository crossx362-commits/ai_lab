using UnityEngine;

namespace Ulon.Editor
{
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// **수면 무늬가 「일정 간격으로 되풀이되는가」**를 재는 자(검수 지시 2026-09-10).
        ///
        /// 이미 있는 `Anisotropy`(여덟 방향 상관 최대−최소)는 **어느 쪽으로 쏠렸나**만 묻는다.
        /// 방향 있는 **규칙** 무늬 — 사선으로 짜인 천 — 는 쏠림이 크므로 그 자를 그냥 통과한다
        /// (채택본 0.69로 합격했는데 근접에서 직조 천으로 읽혔다, 원장 `c12231ed`).
        /// 그래서 **다른 축**을 더한다: 먼 자리에서 상관이 **되살아나는가**.
        ///
        /// 흰 잡음이든 문지른 잡음이든 상관은 결 길이를 넘으면 0으로 죽고 다시 안 산다.
        /// 격자·직조처럼 주기가 있는 무늬는 주기마다 상관이 1 가까이 되살아난다 —
        /// 그 **되살아난 봉우리 높이**가 눈금이다.
        ///
        /// **자를 한 번 잘못 지었다**(먼저 적어 둔다): 처음엔 lag 8~32 창의 **최댓값**을 썼는데,
        /// 채택본이 0.581 @(6,7)로 제일 높게 나왔다 — 그건 되풀이가 아니라 **7칸 문지름의 꼬리**다.
        /// 원점에서 단조로 흘러내리는 비탈의 끝자락을 봉우리로 읽은 것이다. 그래서 조건을 바꿨다:
        /// **국소 최대만 센다** — 이웃 여덟 자리보다 높아야 봉우리다. 비탈은 늘 원점 쪽 이웃이
        /// 더 높으므로 걸러지고, 주기마다 **되살아난** 자리만 남는다.
        ///
        /// 창은 lag **5~40텍셀**. 5 미만은 결 자체고, 40은 화면에서 되풀이로 읽히는 상한이다
        /// (타일 37.5m·텍셀 14.6cm 기준 약 5.9m). 텍스처가 128자 눈금을 두 번 깔아 lag 128에서는
        /// 정확히 1이 되지만 그건 창 밖이다.
        /// </summary>
        public static float WaterRepeatPeak()
        {
            var mat = FindWaterMaterial();
            if (mat == null || mat.mainTexture == null)
                throw new System.InvalidOperationException("수면 재질/텍스처를 찾지 못했습니다 — 자가 헛돕니다.");
            var src = mat.mainTexture as Texture2D;
            if (src == null)
                throw new System.InvalidOperationException("수면 텍스처가 Texture2D가 아닙니다 — 자가 헛돕니다.");
            var tex = MakeReadable(src);
            string detail;
            float v = RepeatPeak(tex, out detail);
            Object.DestroyImmediate(tex);
            return v;
        }

        /// <summary>
        /// lag 5~40 창에서 **되살아난(국소 최대인) 자기상관의 최대**. 0에 가까우면 되풀이가 없고,
        /// 1에 가까우면 그 간격마다 같은 알갱이가 그대로 다시 온다.
        /// </summary>
        static float RepeatPeak(Texture2D tex, out string detail)
        {
            const int Lo = 5, Hi = 40;
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

            // 창보다 한 칸 넓게 지도를 짓는다 — 창 가장자리 자리도 이웃 여덟을 다 봐야 한다.
            int Ext = Hi + 1, D = Ext * 2 + 1;
            var map = new float[D * D];
            // 표본은 4텍셀 걸러 훑는다 — 256×256 전수를 lag마다 돌면 너무 느리다.
            for (int oy = 0; oy <= Ext; oy++)
                for (int ox = -Ext; ox <= Ext; ox++)
                {
                    if (oy == 0 && ox < 0) continue;
                    double s = 0; int n = 0;
                    for (int y = 0; y < H; y += 4)
                        for (int x = 0; x < W; x += 4)
                        {
                            int x2 = ((x + ox) % W + W) % W, y2 = ((y + oy) % H + H) % H;
                            s += (lum[y * W + x] - m) * (lum[y2 * W + x2] - m); n++;
                        }
                    float r = (float)(s / n / v);
                    map[(oy + Ext) * D + (ox + Ext)] = r;
                    map[(-oy + Ext) * D + (-ox + Ext)] = r;   // r(d) = r(−d)
                }

            float best = 0f; int bx = 0, by = 0;
            double sum = 0; int cnt = 0;
            for (int oy = -Hi; oy <= Hi; oy++)
                for (int ox = 0; ox <= Hi; ox++)
                {
                    if (ox == 0 && oy < 0) continue;
                    int len2 = ox * ox + oy * oy;
                    if (len2 < Lo * Lo || len2 > Hi * Hi) continue;
                    float r = map[(oy + Ext) * D + (ox + Ext)];
                    sum += r; cnt++;
                    bool isPeak = true;
                    for (int ky = -1; ky <= 1 && isPeak; ky++)
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            if (kx == 0 && ky == 0) continue;
                            if (map[(oy + ky + Ext) * D + (ox + kx + Ext)] > r) { isPeak = false; break; }
                        }
                    if (isPeak && r > best) { best = r; bx = ox; by = oy; }
                }
            float bg = cnt > 0 ? (float)(sum / cnt) : 0f;
            detail = " 봉우리 " + best.ToString("0.000") + " @(" + bx + "," + by + ") · 배경 " + bg.ToString("0.000");
            return best;
        }

        /// <summary>후보 무늬별로 되풀이 봉우리를 나란히 찍어 본다(사람이 고르라고).</summary>
        public static void RunWaterRegularity()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            int keep = VisualSliceBuilder.WaterPatternOverride;
            try
            {
                foreach (int p in new[] { 0, 7, 9 })
                {
                    VisualSliceBuilder.WaterPatternOverride = p;
                    VisualSliceBuilder.RebuildWater();
                    var mat = FindWaterMaterial();
                    var tex = MakeReadable(mat.mainTexture as Texture2D);
                    string dR, dA;
                    float rep = RepeatPeak(tex, out dR);
                    float ani = Anisotropy(tex, 4, out dA);
                    Debug.Log("[수면규칙성] 무늬 " + p + " — 되풀이" + dR +
                              " · 방향 갈림 " + ani.ToString("0.000"));
                    Object.DestroyImmediate(tex);
                }
            }
            finally
            {
                VisualSliceBuilder.WaterPatternOverride = keep;
                VisualSliceBuilder.RebuildWater();
            }
        }

        /// <summary>후보 무늬를 **같은 자리에서 나란히** 찍는다 — 합격선은 눈이다.</summary>
        public static void RunWaterCandidateShots()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            string outDir = System.IO.Path.Combine(Application.dataPath, "../../builds/qa/water");
            System.IO.Directory.CreateDirectory(outDir);
            int keep = VisualSliceBuilder.WaterPatternOverride;
            try
            {
                foreach (int p in new[] { 7, 9 })
                {
                    VisualSliceBuilder.WaterPatternOverride = p;
                    VisualSliceBuilder.RebuildWater();
                    UnityEditor.AssetDatabase.SaveAssets();
                    foreach (var sh in new[] { "64_river_bend", "63_pier_cutface", "14_world_vista" })
                        ShotTo(sh, System.IO.Path.Combine(outDir, sh + "_cand" + p + ".png"));
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
