using System;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **산 표면과 해안선**(검수 랩 ⑥ → ⑪에서 ①을 갈아 끼웠다).
        ///
        /// ①이 원래 묻던 것은 「절벽이 **한 겹**인가」였고, 그 이유는 **「한 겹이면 평면 투영이 늘린
        /// 세로 스미어가 그대로 보인다」**였다. 그 스미어는 랩 ⑧에서 투영을 고쳐 없어졌다 —
        /// **자의 이유가 사라지면 그 자는 다른 것을 재고 있는 척하는 것뿐이다.** 실제로 이 자는
        /// 「한 겹 2%」로 초록인데 화면의 암벽은 **한 톤**이었다(자는 가중치를 보고 눈은 색을 본다).
        ///
        /// 그래서 같은 자리에서 묻는 것을 바꾼다 — **「두 겹이 덩이로 갈렸나」**. 가중치가 섞여 있는
        /// 것이 아니라 **밝은 암면 덩이와 어두운 절벽 덩이가 각각 자기 자리를 차지**해야 눈에 톤이
        /// 갈려 보인다(랩 ⑤ 잔디에서 배운 그것: 개체마다 굴리면 눈에서 평균이 난다).
        /// 경사는 원장의 자로 잰다(`WorldSplat.MacroSlopeTan`).
        ///
        /// ②는 그대로 — 「모래가 있나」가 아니라 **「폭이 자리마다 다른가」**.
        /// </summary>
        const float CliffChunkShareMin = 0.20f;   // 밝은 덩이·어두운 덩이가 각각 이만큼은 차지해야 한다
        const float CliffBlurShareMax = 0.55f;    // 어느 쪽도 아닌 중간값이 이보다 많으면 화면에선 한 톤이다
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

            // ① 두 겹이 덩이로 갈렸나.
            int steep = 0, lightChunk = 0, darkChunk = 0, blur = 0;
            float steepest = 0f;
            for (float x = -145f; x <= 145f; x += 2.5f)
                for (float z = -145f; z <= 145f; z += 2.5f)
                {
                    float h = WorldTerrain.HeightAt(x, z);
                    if (h < WorldTerrain.LandBase + 6f)
                        continue;
                    float tan = WorldSplat.MacroSlopeTan(x, z);
                    if (tan < 1.0f)          // 큰 경사 45° 아래는 암면이 아니다
                        continue;
                    float r = Sample(alpha, ar, x, z, WorldSplat.Rock);
                    float c = Sample(alpha, ar, x, z, WorldSplat.CliffDark);
                    if (r + c < 0.5f)        // 바위가 우세하지 않은 자리는 이 물음의 대상이 아니다
                        continue;
                    steep++;
                    steepest = Mathf.Max(steepest, Mathf.Atan(tan) * Mathf.Rad2Deg);
                    float lightShare = r / (r + c);
                    if (lightShare > 0.75f)
                        lightChunk++;
                    else if (lightShare < 0.25f)
                        darkChunk++;
                    else
                        blur++;
                }
            if (steep < 100)
                throw new InvalidOperationException("암면 표본이 " + steep + "곳뿐입니다 — 이 자는 아무것도 재지 않았습니다.");
            float lightShareAll = lightChunk / (float)steep;
            float darkShareAll = darkChunk / (float)steep;
            float blurShareAll = blur / (float)steep;
            Debug.Log("[Ulon] §8.2 암면 덩이 — 표본 " + steep + "곳 · 가장 가파른 " + steepest.ToString("0") +
                      "° · 밝은 덩이 " + (lightShareAll * 100f).ToString("0") + "% · 어두운 덩이 " +
                      (darkShareAll * 100f).ToString("0") + "% · 중간값 " + (blurShareAll * 100f).ToString("0") +
                      "% (덩이 각 하한 " + (CliffChunkShareMin * 100f).ToString("0") + "%, 중간값 상한 " +
                      (CliffBlurShareMax * 100f).ToString("0") + "%)");

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

            string verdict = RidgeShoreVerdict(lightShareAll, darkShareAll, blurShareAll, wmax - wmin, wavg);
            if (verdict != null)
                throw new InvalidOperationException(verdict);
        }

        /// <summary>판정만 하는 자리 — 표본과 떼어 놓아야 반대쪽 한계를 그냥 부를 수 있다.</summary>
        static string RidgeShoreVerdict(float lightChunkShare, float darkChunkShare, float blurShare,
                                        float shoreSpan, float shoreAvg)
        {
            if (lightChunkShare < CliffChunkShareMin || darkChunkShare < CliffChunkShareMin)
                return "암면이 한 톤입니다 — 밝은 덩이 " + (lightChunkShare * 100f).ToString("0") + "% · 어두운 덩이 " +
                       (darkChunkShare * 100f).ToString("0") + "%, 각각 최소 " + (CliffChunkShareMin * 100f).ToString("0") +
                       "%가 자기 자리를 차지해야 합니다(§8.2 — 가중치가 섞이는 것과 눈에 갈려 보이는 것은 다르다).";
            if (blurShare > CliffBlurShareMax)
                return "암면 표본의 " + (blurShare * 100f).ToString("0") + "%가 두 겹의 중간값입니다 — 상한 " +
                       (CliffBlurShareMax * 100f).ToString("0") + "%. 자리마다 반반이면 화면에서는 한 톤입니다(§8.2).";
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
            if (RidgeShoreVerdict(0.95f, 0.02f, 0.03f, 6f, 3f) == null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 암면이 밝은 한 톤인데도 통과했습니다.");
            if (RidgeShoreVerdict(0.02f, 0.95f, 0.03f, 6f, 3f) == null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 암면이 어두운 한 톤인데도 통과했습니다.");
            if (RidgeShoreVerdict(0.22f, 0.22f, 0.56f, 6f, 3f) == null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 자리마다 반반인데도 통과했습니다.");
            if (RidgeShoreVerdict(0.35f, 0.35f, 0.30f, 0.5f, 3f) == null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 해안 폭이 어디나 같은데도 통과했습니다.");
            if (RidgeShoreVerdict(0.35f, 0.35f, 0.30f, 6f, 0.2f) == null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 모래가 사라졌는데도 통과했습니다.");
            if (RidgeShoreVerdict(0.35f, 0.35f, 0.30f, 6f, 3f) != null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 덩이로 갈린 암면·들쭉날쭉한 해안인데 자가 걸렸습니다.");
        }
    }
}
