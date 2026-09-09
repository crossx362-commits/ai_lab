using System;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **물가에 걸어 다닐 만한 띠가 있는가**(곁가지 「물가 다듬기」, 2026-09-09).
        ///
        /// 재는 것은 모래의 **양**이 아니라 **수평 폭**이다 — 띠를 높이차로 깔면 가파른 둑에서
        /// 폭이 0에 수렴해 잔디가 물에 수직으로 잘린다(§8.2). 실측으로 갈랐다: 옛 높이 규칙은
        /// 바다 둘레 방위 18곳에서 **0.8~4.3m**(평균 2.4)였고, 300m 섬에서 그 폭은 화면에서 실 한 줄이다.
        ///
        /// 자는 원장(`WorldSplat.ShoreSandAt`)을 읽는다 — 구운 알파맵이 아니라 규칙을 잰다.
        /// 그래서 「구웠나」가 아니라 「규칙이 옳은가」를 문다. 구운 결과는 도포 자들이 따로 본다.
        ///
        /// **이 자가 못 보는 것**: 모래 색과 화면이다 — 폭이 넓어도 색이 잔디와 비슷하면 눈에는
        /// 그대로다. 판정은 `15_lake_river`·`14_world_vista`가 한다.
        /// </summary>
        const float ShoreBandMin = 2.0f;      // 사람 한 걸음 반 — 이보다 좁으면 화면에서 실 한 줄이다
        const int ShoreBearings = 24;

        static void AssertShoreBand()
        {
            float sea = ShoreBandWorst(0f, 0f, false, false, out int seaN);
            float lake = ShoreBandWorst(WorldTerrain.LakeX, WorldTerrain.LakeZ, true, false, out int lakeN);
            if (seaN < 8 || lakeN < 8)
                throw new InvalidOperationException("물가를 찾은 방위가 바다 " + seaN + "곳 · 호수 " + lakeN +
                                                    "곳뿐입니다 — 이 자는 아무것도 재지 않았습니다.");
            Debug.Log("[Ulon] 물가 띠 — 바다 " + seaN + "방위 최소 " + sea.ToString("0.0") + "m · 호수 " + lakeN +
                      "방위 최소 " + lake.ToString("0.0") + "m (하한 " + ShoreBandMin.ToString("0.0") + "m)");

            // **반대쪽 한계**: 옛 높이 규칙으로 재면 빨간불이어야 한다. 아니면 이 자는 폭을 안 재는 것이다.
            float oldSea = ShoreBandWorst(0f, 0f, false, true, out _);
            if (oldSea >= ShoreBandMin)
                throw new InvalidOperationException("반대쪽 한계 실패 — 옛 높이 규칙(" + oldSea.ToString("0.0") +
                                                    "m)도 이 자를 통과합니다. 그러면 폭을 재는 자가 아닙니다.");

            if (sea < ShoreBandMin || lake < ShoreBandMin)
                throw new InvalidOperationException("물가 띠가 바다 " + sea.ToString("0.0") + "m · 호수 " +
                                                   lake.ToString("0.0") + "m입니다(하한 " + ShoreBandMin.ToString("0.0") +
                                                   "m) — 잔디가 물에 수직으로 잘려 보입니다.");
        }

        /// <summary>방위별 띠 폭 중 **가장 좁은 것**. 평균은 한 곳이 잘려도 안 운다.</summary>
        static float ShoreBandWorst(float cx, float cz, bool centerIsWater, bool oldRule, out int found)
        {
            found = 0;
            float worst = 99f;
            for (int i = 0; i < ShoreBearings; i++)
            {
                float ang = i * Mathf.PI * 2f / ShoreBearings;
                float dx = Mathf.Cos(ang), dz = Mathf.Sin(ang);
                if (!ShoreWaterline(cx, cz, dx, dz, centerIsWater, out float wx, out float wz))
                    continue;
                float lx = centerIsWater ? dx : -dx, lz = centerIsWater ? dz : -dz;
                found++;
                worst = Mathf.Min(worst, ShoreWidth(wx, wz, lx, lz, oldRule));
            }
            return found > 0 ? worst : 0f;
        }

        static bool ShoreWaterline(float cx, float cz, float dx, float dz, bool centerIsWater, out float wx, out float wz)
        {
            wx = wz = 0f;
            bool centerWet = WorldTerrain.HeightAt(cx, cz) < WorldTerrain.SeaLevel;
            if (centerWet != centerIsWater)
                return false;
            for (float d = 0f; d < 160f; d += 0.25f)
            {
                float x = cx + dx * d, z = cz + dz * d;
                if ((WorldTerrain.HeightAt(x, z) < WorldTerrain.SeaLevel) != centerWet)
                {
                    wx = x; wz = z;
                    return true;
                }
            }
            return false;
        }

        static float ShoreWidth(float wx, float wz, float dx, float dz, bool oldRule)
        {
            const float Step = 0.25f, Cap = 30f;
            for (float d = 0f; d < Cap; d += Step)
            {
                float x = wx + dx * d, z = wz + dz * d;
                // **폭은 「모래 폭」이 아니라 「전이대(잔디 아님) 폭」이다**(검수 판정 2026-09-09).
                // 안식각 상한을 넣자 급한 물가의 모래가 자갈(너덜)로 바뀌었고, 모래 한 겹만 읽던
                // 이 자가 **0.0m**라고 울었다 — 화면에서는 띠가 그대로 있는데 자만 눈이 멀었다.
                // 내가 만든 자였고 같은 병이었다: 겹이 갈리면 계열 합으로 읽어야 한다.
                float band = oldRule
                    ? WorldSplat.ShoreSandHeightAt(x, z)
                    : WorldSplat.ShoreSandAt(x, z) + WorldSplat.ShoreScreeAt(x, z);
                if (band < 0.5f)
                    return d;
            }
            return Cap;
        }
    }
}
