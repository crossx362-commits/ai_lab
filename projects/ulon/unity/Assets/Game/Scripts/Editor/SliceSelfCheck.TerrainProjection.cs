using System;
using System.Collections.Generic;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **늘어남 자체를 잰다**(검수 조건, 랩 ⑧). 「절벽이 한 겹인가」는 결과의 대리 지표였다 —
        /// 두 겹으로 갈라도 화면의 세로줄은 남았다. 이 자가 재는 것은 **무늬 주기가 경사면에서
        /// 세로로 몇 배 늘어나는가**다.
        ///
        /// 재는 법: 지형 법선 n에서 재질이 실제로 쓰는 혼합 가중치 bw를 그대로 다시 만들고
        /// (셰이더와 같은 식: pow(|n|, _TriSharp) 정규화, _PlanarOnly면 XZ 한 축),
        /// 표면 위 무늬 밀도 = bw·|n| 를 구한다. 늘어남 = 1/그 값.
        /// 평면 투영이면 1/|n.y| — 82° 절벽에서 7배다. 삼면이면 어떤 법선에서도 1.73을 못 넘는다.
        ///
        /// **반대쪽 한계는 같은 표본으로 잰다**(수치를 지어내지 않는다): 같은 자리들을 평면 투영으로
        /// 다시 재서 반드시 상한을 넘어야 한다. 안 넘으면 지형이 완만해 이 자가 아무것도 증명하지
        /// 못한 것이므로 실패다.
        /// </summary>
        const float StretchP95Max = 2.2f;       // 경사면 무늬 늘어남 p95 상한(배)
        const float PlanarProofRatio = 1.3f;    // 평면 투영은 상한의 이 배는 넘어야 「증명」이다

        static void AssertTerrainProjection()
        {
            AssertTerrainProjectionNegativeControl();

            var terrain = Terrain.activeTerrain;
            if (terrain == null)
                throw new InvalidOperationException("지형이 없습니다 — 투영을 검사할 수 없습니다.");
            var mat = terrain.materialTemplate;
            if (mat == null || mat.shader == null || mat.shader.name != VisualSliceBuilder.TriplanarShader)
                throw new InvalidOperationException("지형 재질이 " + (mat == null ? "없습니다" : mat.shader == null ? "셰이더가 없습니다" : mat.shader.name) +
                                                    " — 삼면 투영 재질(" + VisualSliceBuilder.TriplanarShader + ")이 꽂혀 있어야 합니다.");
            float sharp = mat.GetFloat("_TriSharp");
            float planarOnly = mat.GetFloat("_PlanarOnly");

            var real = new List<float>();
            var planar = new List<float>();
            float steepest = 0f;
            for (float x = -145f; x <= 145f; x += 2.5f)
                for (float z = -145f; z <= 145f; z += 2.5f)
                {
                    Vector3 n = TerrainNormal(x, z);
                    float slope = Mathf.Acos(Mathf.Clamp01(n.y)) * Mathf.Rad2Deg;
                    if (slope < 40f)
                        continue;
                    steepest = Mathf.Max(steepest, slope);
                    real.Add(Stretch(n, sharp, planarOnly));
                    planar.Add(Stretch(n, sharp, 1f));
                }
            if (real.Count < 200)
                throw new InvalidOperationException("40°↑ 표본이 " + real.Count + "곳뿐입니다 — 이 자는 아무것도 재지 않았습니다.");

            float p95 = Percentile(real, 0.95f), worst = Percentile(real, 1f);
            float planarP95 = Percentile(planar, 0.95f);
            Debug.Log("[Ulon] §8.2 지형 투영 늘어남 — 40°↑ 표본 " + real.Count + "곳 · 가장 가파른 " +
                      steepest.ToString("0") + "° · p95 " + p95.ToString("0.00") + "배 · 최악 " +
                      worst.ToString("0.00") + "배 (상한 " + StretchP95Max.ToString("0.0") +
                      ") · 같은 자리 평면 투영이면 p95 " + planarP95.ToString("0.00") + "배");

            if (planarP95 < StretchP95Max * PlanarProofRatio)
                throw new InvalidOperationException("반대쪽 한계 실패 — 같은 표본을 평면 투영으로 재도 p95가 " +
                                                    planarP95.ToString("0.00") + "배뿐입니다. 이 지형은 이 자를 증명하지 못합니다.");
            string verdict = TerrainStretchVerdict(p95);
            if (verdict != null)
                throw new InvalidOperationException(verdict);
        }

        /// <summary>판정만 하는 자리 — 표본과 떼어 놓아야 반대쪽 한계를 그냥 부를 수 있다.</summary>
        static string TerrainStretchVerdict(float stretchP95)
        {
            if (stretchP95 > StretchP95Max)
                return "경사면 무늬가 p95 " + stretchP95.ToString("0.00") + "배 늘어났습니다 — 상한 " +
                       StretchP95Max.ToString("0.0") + "배. 지형 도포가 평면으로 투영되고 있습니다(§8.2).";
            return null;
        }

        /// <summary>셰이더와 같은 식으로 혼합 가중치를 만들고 표면 무늬 밀도의 역수를 돌려준다.</summary>
        static float Stretch(Vector3 n, float sharp, float planarOnly)
        {
            var a = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
            var bw = new Vector3(Mathf.Pow(a.x, sharp), Mathf.Pow(a.y, sharp), Mathf.Pow(a.z, sharp));
            float s = Mathf.Max(bw.x + bw.y + bw.z, 1e-4f);
            bw /= s;
            float p = Mathf.Clamp01(planarOnly);
            bw = Vector3.Lerp(bw, new Vector3(0f, 1f, 0f), p);
            return 1f / Mathf.Max(bw.x * a.x + bw.y * a.y + bw.z * a.z, 1e-4f);
        }

        static Vector3 TerrainNormal(float x, float z)
        {
            const float d = 1.5f;
            float hx = WorldTerrain.HeightAt(x + d, z) - WorldTerrain.HeightAt(x - d, z);
            float hz = WorldTerrain.HeightAt(x, z + d) - WorldTerrain.HeightAt(x, z - d);
            return new Vector3(-hx, 2f * d, -hz).normalized;
        }

        static float Percentile(List<float> values, float q)
        {
            var copy = new List<float>(values);
            copy.Sort();
            int i = Mathf.Clamp(Mathf.RoundToInt(q * (copy.Count - 1)), 0, copy.Count - 1);
            return copy[i];
        }

        static void AssertTerrainProjectionNegativeControl()
        {
            // ㉠ 판정 자체: 늘어난 값은 걸리고, 안 늘어난 값은 안 걸린다.
            if (TerrainStretchVerdict(7.2f) == null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 무늬가 7배 늘어났는데도 통과했습니다.");
            if (TerrainStretchVerdict(1.5f) != null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 늘어나지 않았는데 자가 걸렸습니다.");
            // ㉡ 재는 식 자체: 82° 벽에서 평면 투영은 7배 넘게, 삼면 투영은 2배 아래여야 한다.
            var wall = new Vector3(Mathf.Sin(82f * Mathf.Deg2Rad), Mathf.Cos(82f * Mathf.Deg2Rad), 0f).normalized;
            float sPlanar = Stretch(wall, 4f, 1f), sTri = Stretch(wall, 4f, 0f);
            if (sPlanar < 7f)
                throw new InvalidOperationException("반대쪽 한계 실패 — 82° 벽인데 평면 투영 늘어남이 " + sPlanar.ToString("0.00") + "배뿐입니다.");
            if (sTri > 2f)
                throw new InvalidOperationException("반대쪽 한계 실패 — 82° 벽에서 삼면 투영이 " + sTri.ToString("0.00") + "배 늘어났습니다.");
        }
    }
}
