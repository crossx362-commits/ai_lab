using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// **길이 비탈을 오르나 — 경사로 센다**(문 뒤 언덕 랩에서 화면으로 본 것, 2026-09-09).
        ///
        /// `07`·`09`·`11` 위쪽에 돌길 띠가 **언덕 정상까지** 곧게 뻗는다. 길 도포(`Routes` 스포크·광장)는
        /// 좌표만 보고 그어지므로 지형이 솟으면 그 위를 그대로 탄다 — 사람이 다니는 길은 급경사를 피한다.
        ///
        /// 재는 것: 길로 칠해진 자리의 **경사 분포**와, 25°(tan 0.466)를 넘는 길 칸의 비율.
        /// 문 뒤 언덕 위만 따로도 센다(그 자리가 원인이면 숫자로 갈린다).
        /// </summary>
        public static void RunRoadSlope()
        {
            Debug.Log("[Census] 길 경사 — 길로 칠해진 자리가 얼마나 가파른가");
            Sweep("세계 전체(1m 격자)", -170f, 170f, 2f);
            foreach (var d in EntranceGeom.All)
            {
                var front = EntranceGeom.Front(d.X, d.Z, d.Yaw);
                float cx = d.X - front.x * EntranceGeom.MoundOffset, cz = d.Z - front.y * EntranceGeom.MoundOffset;
                SweepBox(d.Root + " 언덕(반경 " + EntranceGeom.MoundRadius + "m)", cx, cz, EntranceGeom.MoundRadius, 1f);
            }
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        static void Sweep(string tag, float min, float max, float step)
        {
            Tally(tag, min, max, min, max, step);
        }

        static void SweepBox(string tag, float cx, float cz, float half, float step)
        {
            Tally(tag, cx - half, cx + half, cz - half, cz + half, step);
        }

        static void Tally(string tag, float x0, float x1, float z0, float z1, float step)
        {
            int road = 0, steep = 0;
            float worst = 0f, worstX = 0f, worstZ = 0f;
            for (float x = x0; x <= x1; x += step)
            for (float z = z0; z <= z1; z += step)
            {
                int layer = WorldSplat.CoverAt(x, z, out float w);
                if (w < 0.5f || (layer != WorldSplat.Road && layer != WorldSplat.Cobble))
                    continue;
                road++;
                float tan = WorldSplat.MacroSlopeTan(x, z);
                if (tan > 0.466f)
                    steep++;
                if (tan > worst) { worst = tan; worstX = x; worstZ = z; }
            }
            float deg = Mathf.Atan(worst) * Mathf.Rad2Deg;
            Debug.Log("[Census] " + tag + " — 길 칸 " + road + "개 · 25°↑ " + steep + "개(" +
                      (road > 0 ? (100f * steep / road).ToString("0.0") : "0") + "%) · 최급 " +
                      deg.ToString("0.0") + "° @(" + worstX.ToString("0") + "," + worstZ.ToString("0") + ")");
        }
    }
}
