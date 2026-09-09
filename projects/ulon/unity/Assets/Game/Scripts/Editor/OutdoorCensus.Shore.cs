using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// **물가 모래띠의 「수평 폭」을 센다**(곁가지 「물가 다듬기」, 2026-09-09).
        ///
        /// 화면에서 본 것: `15_lake_river`에서 잔디가 물에 거의 수직으로 잘린다. 코드에 후보가 있었다 —
        /// 띠를 **높이차**로만 깔면 폭이 둑 경사에 반비례한다. 그래서 재는 것도 높이가 아니라
        /// **물가에서 걸어 나간 수평 거리**다: 물가를 찾아 바깥으로 0.25m씩 걸으며 모래가 0.5 아래로
        /// 떨어지는 자리까지의 거리.
        ///
        /// 두 규칙을 **같은 자리에서 나란히** 잰다(옛 높이 규칙 = 반대쪽 한계 표본). 고치기 전 값을
        /// 따로 찍어 둘 필요가 없게 — 한 판에 전후가 같이 나온다.
        ///
        /// **이 자가 못 보는 것**: 화면이다. 폭이 넓어도 모래 색이 잔디와 비슷하면 눈에는 그대로다 —
        /// 판정은 `15_lake_river`·`14_world_vista`가 한다.
        /// </summary>
        public static void RunShoreBand()
        {
            Debug.Log("[Census] 물가 띠 — 물가에서 바깥으로 걸어 모래가 0.5 아래로 떨어지는 거리(m)");
            BandAround("호수", WorldTerrain.LakeX, WorldTerrain.LakeZ, 24, true);
            BandAround("바다(섬 둘레)", 0f, 0f, 24, false);
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        /// <summary>한 중심에서 방위별로 물가를 찾아 띠 폭을 잰다 — 물가를 못 찾은 방위는 세지 않고 적는다.</summary>
        static void BandAround(string tag, float cx, float cz, int bearings, bool centerIsWater)
        {
            int n = 0, thinNew = 0, thinOld = 0, missing = 0;
            float sumNew = 0f, sumOld = 0f, minNew = 99f, maxNew = 0f, minOld = 99f, maxOld = 0f;
            for (int i = 0; i < bearings; i++)
            {
                float ang = i * Mathf.PI * 2f / bearings;
                float dx = Mathf.Cos(ang), dz = Mathf.Sin(ang);
                if (!FindWaterline(cx, cz, dx, dz, centerIsWater, out float wxs, out float wzs)) { missing++; continue; }
                // 육지는 **물가 반대쪽**에 있다 — 호수는 바깥, 바다는 안쪽.
                float lx = centerIsWater ? dx : -dx, lz = centerIsWater ? dz : -dz;
                float bNew = BandWidth(wxs, wzs, lx, lz, false);
                float bOld = BandWidth(wxs, wzs, lx, lz, true);
                n++;
                sumNew += bNew; sumOld += bOld;
                minNew = Mathf.Min(minNew, bNew); maxNew = Mathf.Max(maxNew, bNew);
                minOld = Mathf.Min(minOld, bOld); maxOld = Mathf.Max(maxOld, bOld);
                if (bNew < 1.0f) thinNew++;
                if (bOld < 1.0f) thinOld++;
            }
            if (n == 0)
            {
                Debug.Log("[Census] " + tag + " — 물가를 찾은 방위 0개(" + missing + "개 실패) — 잰 것이 없다");
                return;
            }
            Debug.Log("[Census] " + tag + " — 방위 " + n + "개(물가 못 찾음 " + missing + ") · **새 규칙 평균 " +
                      (sumNew / n).ToString("0.0") + "m**(" + minNew.ToString("0.0") + "~" + maxNew.ToString("0.0") +
                      ", 1m 미만 " + thinNew + "곳) · 옛 높이 규칙 평균 " + (sumOld / n).ToString("0.0") + "m(" +
                      minOld.ToString("0.0") + "~" + maxOld.ToString("0.0") + ", 1m 미만 " + thinOld + "곳)");
        }

        /// <summary>
        /// 중심에서 바깥으로 걸어 **물과 뭍이 갈리는 첫 자리**를 물가로 삼는다.
        /// 호수는 중심이 물이라 「뭍이 나오는 자리」, 바다는 중심이 뭍이라 「물이 나오는 자리」다.
        /// 중심이 기대와 다르면(호수가 말랐다거나) **재지 않고 실패로 적는다** — 엉뚱한 자리를 재느니 낫다.
        /// </summary>
        static bool FindWaterline(float cx, float cz, float dx, float dz, bool centerIsWater, out float wx, out float wz)
        {
            wx = wz = 0f;
            bool centerWet = WorldTerrain.HeightAt(cx, cz) < WorldTerrain.SeaLevel;
            if (centerWet != centerIsWater)
                return false;
            for (float d = 0f; d < 160f; d += 0.25f)
            {
                float x = cx + dx * d, z = cz + dz * d;
                bool wet = WorldTerrain.HeightAt(x, z) < WorldTerrain.SeaLevel;
                if (wet != centerWet)
                {
                    wx = x; wz = z;
                    return true;
                }
            }
            return false;
        }

        /// <summary>물가에서 바깥으로 걸으며 모래가 0.5 아래로 떨어질 때까지의 수평 거리.</summary>
        static float BandWidth(float wx, float wz, float dx, float dz, bool oldRule)
        {
            const float Step = 0.25f, Cap = 30f;
            for (float d = 0f; d < Cap; d += Step)
            {
                float x = wx + dx * d, z = wz + dz * d;
                float sand = oldRule ? WorldSplat.ShoreSandHeightAt(x, z) : WorldSplat.ShoreSandAt(x, z);
                if (sand < 0.5f)
                    return d;
            }
            return Cap;
        }
    }
}
