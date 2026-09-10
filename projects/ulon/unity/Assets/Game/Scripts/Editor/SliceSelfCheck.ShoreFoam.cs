using System;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **물가 경계에 깊이 페이드와 거품이 있나**(검수 조건 2026-09-11, 물 재작업 1단계).
        ///
        /// 두 수를 같이 본다 — **네거티브 컨트롤이 갈리기 때문**이다:
        ///   ①**밝기 차**(얕은 띠 − 열린 물) = 깊이 페이드. 실측 `64` **63.5** · `15` **83.8**.
        ///     거품만 꺼도 거의 안 떨어진다(대부분 깊이 색의 몫) — 이 수 하나로는
        ///     **거품이 통째로 사라져도 초록불**이다. 깊이 색까지 끄면 **7.3**으로 무너진다.
        ///   ②**얕은 띠의 흰 몫**(RGB 모두 215↑) = 거품. 실측 `64` **18.6%** · `15` **39.8%**.
        ///     거품을 끄면 **0.5%**로 무너진다. ①의 구멍을 이 수가 막는다.
        ///     (1.5단계로 띠를 넓히기 전 값은 6.2%·27.6%였다 — 하한 2%는 그때 고른 값이고
        ///      넓힌 뒤에도 NC와의 사이가 그대로라 그대로 둔다.)
        /// 상한이 아니라 **하한**이다 — 이 자는 「없어졌다」를 잡는다.
        ///
        /// 대상에서 **`63`(부두)은 뺐다**: 얕은 띠가 화면 저 끝이라 열린 물 표본이 760px뿐이고
        /// 대비가 3.5로 뜬다. 못 재는 자리를 상한에 넣으면 자가 그 자리에 맞춰 무력해진다
        /// (수직면 쪽 거품은 눈으로 본다 — `builds/qa/water/63_pier_cutface_look.png`).
        ///
        /// 렌더가 필요하므로 **`QaShots.Run` 끝**에서 돈다.
        /// </summary>
        ///
        /// **1.6단계 — 흰 몫에 상한이 붙었다**(검수 반려 2026-09-11). 하한만 있던 이 자는
        /// **반대쪽 실패를 통과시켰다**: 1.5단계의 `15`가 39.8%로 초록불이었는데 화면은
        /// **흰 도넛**(물이 아니라 눈 덮인 웅덩이)이었다. 하한만 있는 자는 「없어졌다」만 잡고
        /// 「너무 많다」는 못 잡는다 — 구간으로 물린다.
        /// 1.6 실측: `64` **3.2%** · `15` **4.2%**(대비 46.7·53.1). NC 둘 다 문다 —
        /// 거품을 끄면 0.5%(하한 아래), 띠를 1.5단계처럼 벌리면 상한 위로 나간다.
        const float ShoreFadeMin = 25.0f;     // 실측 46.7·53.1 대 NC 7.3
        const float ShoreFoamMin = 0.02f;     // 실측 3.2%·4.2% 대 NC(거품 끔) 0.5%
        const float ShoreFoamMax = 0.15f;     // 실측 3.2%·4.2% 대 NC(띠 벌림) 1.5단계의 18.6%·39.8%

        public static void AssertShoreFoam()
        {
            // **깊이 요구 부품이 씬에 없으면 물은 조용히 「어디서나 깊은 색」이 된다** — 그 판은
            // 화면이 틀렸는데 자는 초록불이다. 먼저 부품부터 확인한다.
            var water = GameObject.Find(VisualSliceBuilder.WaterObject);
            if (water == null || water.GetComponent<Ulon.Client.WaterDepthCamera>() == null)
                throw new InvalidOperationException("수면에 깊이 요구 부품(WaterDepthCamera)이 없습니다 — " +
                    "깊이 텍스처가 안 구워져 물이 어디서나 깊은 색으로 그려집니다.");

            foreach (string shot in new[] { "64_river_bend", "15_lake_river" })
            {
                float fade = OutdoorCensus.ShoreFoamStats(shot, out float white, out string det);
                if (fade < 0f)
                    throw new InvalidOperationException("물가 " + shot + " — " + det +
                        ". **못 재는 자를 초록불로 남기지 않는다.**");
                Debug.Log("[Ulon] 물가 " + shot + " — 깊이 페이드 " + fade.ToString("0.0") +
                          "(하한 " + ShoreFadeMin.ToString("0.0") + ") · 거품 흰 몫 " +
                          (white * 100f).ToString("0.0") + "%(하한 " + (ShoreFoamMin * 100f).ToString("0.0") +
                          "%) · " + det);
                if (fade < ShoreFadeMin)
                    throw new InvalidOperationException("샷 " + shot + "의 물가가 열린 물보다 " +
                        fade.ToString("0.0") + "밖에 안 밝습니다(하한 " + ShoreFadeMin.ToString("0.0") +
                        ") — 깊이에 따른 물빛이 화면에서 사라졌습니다.");
                if (white < ShoreFoamMin)
                    throw new InvalidOperationException("샷 " + shot + "의 물가 흰 몫이 " +
                        (white * 100f).ToString("0.0") + "%뿐입니다(하한 " + (ShoreFoamMin * 100f).ToString("0.0") +
                        "%) — 물가 거품이 화면에서 사라졌습니다.");
                if (white > ShoreFoamMax)
                    throw new InvalidOperationException("샷 " + shot + "의 물가 흰 몫이 " +
                        (white * 100f).ToString("0.0") + "%입니다(상한 " + (ShoreFoamMax * 100f).ToString("0.0") +
                        "%) — 띠가 아니라 **흰 웅덩이**입니다. 물이 물로 안 읽힙니다.");
            }

            // NC 둘 — **끄는 길을 실제로 밟아** 자가 우는지 본다(되돌리기는 `finally`가 짝으로 진다).
            var mat = FindWaterMat();
            if (mat == null || !mat.HasProperty("_FoamDepthSteep"))
                throw new InvalidOperationException("수면 재질에 거품 속성이 없습니다 — NC를 못 겁니다.");
            float f1 = mat.GetFloat("_FoamDepthSteep"), dm = mat.GetFloat("_DepthMax");
            try
            {
                mat.SetFloat("_FoamDepthSteep", 0f);
                OutdoorCensus.ShoreFoamStats("64_river_bend", out float ncWhite, out _);
                if (ncWhite >= ShoreFoamMin)
                    throw new InvalidOperationException("물가 거품 네거티브 컨트롤 실패 — 거품을 껐는데도 " +
                        "흰 몫이 " + (ncWhite * 100f).ToString("0.0") + "%로 통과합니다. 자가 무력합니다.");
                mat.SetFloat("_DepthMax", 0.01f);
                float ncFade = OutdoorCensus.ShoreFoamStats("64_river_bend", out _, out _);
                if (ncFade >= ShoreFadeMin)
                    throw new InvalidOperationException("물가 깊이 페이드 네거티브 컨트롤 실패 — 깊이 색까지 " +
                        "껐는데도 " + ncFade.ToString("0.0") + "로 통과합니다. 자가 무력합니다.");
                mat.SetFloat("_DepthMax", dm);
                // **상한 쪽 NC** — 띠를 1.5단계처럼 벌리면(문턱 1.2m·덮음 1.0) 자가 울어야 한다.
                // 상한을 넣고도 그 판이 통과하면 이 자는 흰 도넛을 또 놓친다.
                float a0 = mat.HasProperty("_FoamMaxAlpha") ? mat.GetFloat("_FoamMaxAlpha") : -1f;
                mat.SetFloat("_FoamDepthSteep", 1.2f);
                if (a0 >= 0f) mat.SetFloat("_FoamMaxAlpha", 1.0f);
                OutdoorCensus.ShoreFoamStats("15_lake_river", out float ncWide, out _);
                if (a0 >= 0f) mat.SetFloat("_FoamMaxAlpha", a0);
                if (ncWide <= ShoreFoamMax)
                    throw new InvalidOperationException("물가 거품 상한 네거티브 컨트롤 실패 — 띠를 " +
                        "벌렸는데도 흰 몫이 " + (ncWide * 100f).ToString("0.0") + "%로 상한 안에 있습니다. " +
                        "상한이 무력합니다.");
                Debug.Log("[Ulon] 물가 네거티브 컨트롤 통과 — 거품 끄면 흰 몫 " +
                          (ncWhite * 100f).ToString("0.0") + "% · 깊이 색까지 끄면 페이드 " + ncFade.ToString("0.0") +
                          " · 띠를 벌리면 흰 몫 " + (ncWide * 100f).ToString("0.0") + "%");
            }
            finally
            {
                mat.SetFloat("_FoamDepthSteep", f1);
                mat.SetFloat("_DepthMax", dm);
            }
        }

        /// <summary>수면 재질 — 이름이 아니라 **수면 오브젝트가 실제로 쓰는 것**을 집는다.</summary>
        static Material FindWaterMat()
        {
            var go = GameObject.Find(VisualSliceBuilder.WaterObject);
            var rend = go != null ? go.GetComponent<Renderer>() : null;
            return rend != null ? rend.sharedMaterial : null;
        }
    }
}
