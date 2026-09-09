using System;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **산 표면과 해안선**(검수 랩 ⑥). 세는 도구가 증상을 수치로 줬다:
        /// 30°를 넘는 절벽 표본 406곳 중 **159곳(39%)이 지표 한 겹**이고(늘어난 무늬가 그대로 보인다),
        /// 해안 모래띠는 방위 16곳이 **1.5~4.5m**로 사실상 균일했다(해안선이 아니라 두른 띠다).
        ///
        /// 이 자가 묻는 것은 「바위가 있나」가 아니라 **「절벽이 한 겹인가」**, 그리고
        /// 「모래가 있나」가 아니라 **「폭이 자리마다 다른가」**다.
        /// </summary>
        const float CliffOneToneMax = 0.30f;    // 급경사 표본 중 한 겹뿐이어도 되는 몫의 상한
        const float ShoreWidthSpanMin = 3.0f;   // 가장 넓은 곳 − 가장 좁은 곳(m)
        const float ShoreWidthAvgMin = 0.8f;    // 그래도 모래가 사라지면 안 된다

        static void AssertRidgeAndShore()
        {
            AssertRidgeShoreNegativeControl();

            var terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
                throw new InvalidOperationException("지형이 없습니다 — 산·해안을 검사할 수 없습니다.");
            var data = terrain.terrainData;
            int ar = data.alphamapResolution;
            var alpha = data.GetAlphamaps(0, 0, ar, ar);

            // ① 절벽이 한 겹인가.
            int steep = 0, oneTone = 0;
            float steepest = 0f;
            for (float x = -140f; x <= 140f; x += 5f)
                for (float z = -140f; z <= 140f; z += 5f)
                {
                    float h = WorldTerrain.HeightAt(x, z);
                    if (h < WorldTerrain.LandBase + 6f)
                        continue;
                    float dh = Mathf.Max(Mathf.Abs(WorldTerrain.HeightAt(x + 2f, z) - h),
                                         Mathf.Abs(WorldTerrain.HeightAt(x, z + 2f) - h));
                    float slope = Mathf.Atan2(dh, 2f) * Mathf.Rad2Deg;
                    if (slope < 30f)
                        continue;
                    steep++;
                    steepest = Mathf.Max(steepest, slope);
                    int tones = 0;
                    for (int L = 0; L < data.alphamapLayers; L++)
                        if (Sample(alpha, ar, x, z, L) > 0.12f)
                            tones++;
                    if (tones <= 1)
                        oneTone++;
                }
            if (steep < 20)
                throw new InvalidOperationException("급경사 표본이 " + steep + "곳뿐입니다 — 이 자는 아무것도 재지 않았습니다.");
            float oneToneShare = oneTone / (float)steep;
            Debug.Log("[Ulon] §8.2 절벽 얼룩 — 30°↑ 표본 " + steep + "곳 · 가장 가파른 " + steepest.ToString("0") +
                      "° · 한 겹뿐 " + (oneToneShare * 100f).ToString("0") + "% (상한 " + (CliffOneToneMax * 100f).ToString("0") + "%)");

            // ② 해안 모래띠 폭이 자리마다 다른가 — 지형이 아니라 **칠해진 모래**로 잰다.
            float wmin = 999f, wmax = 0f, wsum = 0f;
            int wn = 0;
            for (float a = 0f; a < 360f; a += 15f)
            {
                float ca = Mathf.Cos(a * Mathf.Deg2Rad), sa = Mathf.Sin(a * Mathf.Deg2Rad);
                float shore = -1f;
                for (float d = 40f; d < 150f; d += 0.5f)
                    if (WorldTerrain.HeightAt(ca * d, sa * d) < WorldTerrain.SeaLevel)
                    { shore = d; break; }
                if (shore < 0f)
                    continue;
                float width = 0f;
                for (float d = shore; d > shore - 30f; d -= 0.5f)
                {
                    if (Sample(alpha, ar, ca * d, sa * d, WorldSplat.Sand) < 0.5f)
                        break;
                    width = shore - d;
                }
                wn++;
                wsum += width;
                wmin = Mathf.Min(wmin, width);
                wmax = Mathf.Max(wmax, width);
            }
            if (wn < 8)
                throw new InvalidOperationException("해안 표본이 " + wn + "곳뿐입니다 — 이 자는 아무것도 재지 않았습니다.");
            float wavg = wsum / wn;
            Debug.Log("[Ulon] §8.2 해안 모래띠 — 방위 " + wn + "곳 평균 " + wavg.ToString("0.0") + "m · " +
                      wmin.ToString("0.0") + "~" + wmax.ToString("0.0") + "m (폭 차이 하한 " + ShoreWidthSpanMin +
                      "m, 평균 하한 " + ShoreWidthAvgMin + "m)");

            string verdict = RidgeShoreVerdict(oneToneShare, wmax - wmin, wavg);
            if (verdict != null)
                throw new InvalidOperationException(verdict);
        }

        /// <summary>판정만 하는 자리 — 표본과 떼어 놓아야 반대쪽 한계를 그냥 부를 수 있다.</summary>
        static string RidgeShoreVerdict(float cliffOneToneShare, float shoreSpan, float shoreAvg)
        {
            if (cliffOneToneShare > CliffOneToneMax)
                return "절벽 표본의 " + (cliffOneToneShare * 100f).ToString("0") + "%가 지표 한 겹입니다 — 상한 " +
                       (CliffOneToneMax * 100f).ToString("0") + "%. 평면 투영이 늘린 무늬가 그대로 세로 스미어로 보입니다(§8.2).";
            if (shoreSpan < ShoreWidthSpanMin)
                return "해안 모래띠의 넓은 곳과 좁은 곳 차이가 " + shoreSpan.ToString("0.0") + "m입니다 — 최소 " +
                       ShoreWidthSpanMin + "m. 폭이 어디나 같으면 해안선이 아니라 두른 띠로 읽힙니다(§8.2).";
            if (shoreAvg < ShoreWidthAvgMin)
                return "해안 모래띠 평균이 " + shoreAvg.ToString("0.0") + "m입니다 — 최소 " + ShoreWidthAvgMin +
                       "m. 폭을 흔들다가 모래를 아예 없앴습니다(§8.2).";
            return null;
        }

        static void AssertRidgeShoreNegativeControl()
        {
            if (RidgeShoreVerdict(0.9f, 6f, 3f) == null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 절벽 90%가 한 겹인데도 통과했습니다.");
            if (RidgeShoreVerdict(0.1f, 0.5f, 3f) == null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 해안 폭이 어디나 같은데도 통과했습니다.");
            if (RidgeShoreVerdict(0.1f, 6f, 0.2f) == null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 모래가 사라졌는데도 통과했습니다.");
            if (RidgeShoreVerdict(0.1f, 6f, 3f) != null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 갈린 절벽·들쭉날쭉한 해안인데 자가 걸렸습니다.");
        }
    }
}
