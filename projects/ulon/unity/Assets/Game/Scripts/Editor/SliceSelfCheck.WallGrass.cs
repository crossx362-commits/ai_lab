using System;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **암벽에 풀이 흘러내리지 않는다**(검수 랩 ⑨ — 상식 모순 규칙). 랩 ⑧에서 투영을 고쳐
        /// 암석 결은 살아났는데, 그 옆으로 **초록·누런 띠가 커튼처럼** 남았다. 투영이 아니라 도포다.
        ///
        /// 이 자가 묻는 것은 「바위가 있나」가 아니라 **「벽에 풀이 붙어 있나」**다. 60°를 넘는 자리에서
        /// 풀 계열(짙은 풀 + 마른 풀) 합을 읽는다 — 랩 ⑤·⑥에서 배운 대로 **계열 합**이다.
        /// 경사는 **원장의 8m 자**로 잰다(`WorldSplat.MacroSlopeTan`) — 굽는 쪽과 자의 길이가 갈리면
        /// 자는 초록인데 화면은 딴것이 된다. 60°는 규칙이 상한을 완전히 푸는 65°의 바로 아래다:
        /// 규칙이 다 푸는 자리만 재면 「규칙이 도는가」를 물을 뿐이라, 문턱 언저리를 함께 봐야
        /// **화면에 남는 커튼**을 잡는다.
        /// </summary>
        const float WallSlopeDeg = 60f;          // 큰 경사가 이 각을 넘으면 「벽」이다(규칙이 다 푸는 65°의 바로 아래)
        const float WallGrassWeightMax = 0.20f;  // 한 자리에서 풀이 이만큼 넘으면 「풀 붙은 자리」
        const float WallGrassShareMax = 0.05f;   // 벽 표본 중 그런 자리가 차지해도 되는 몫

        static void AssertWallGrass()
        {
            AssertWallGrassNegativeControl();

            var terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
                throw new InvalidOperationException("지형이 없습니다 — 암벽 풀을 검사할 수 없습니다.");
            var data = terrain.terrainData;
            int ar = data.alphamapResolution;
            var alpha = data.GetAlphamaps(0, 0, ar, ar);

            float tanLimit = Mathf.Tan(WallSlopeDeg * Mathf.Deg2Rad);
            int wall = 0, grassy = 0;
            float worstUnflagged = 0f, worst = 0f;
            for (float x = -145f; x <= 145f; x += 2.5f)
                for (float z = -145f; z <= 145f; z += 2.5f)
                {
                    if (WorldSplat.MacroSlopeTan(x, z) < tanLimit)
                        continue;
                    wall++;
                    float g = Sample(alpha, ar, x, z, WorldSplat.Grass) + Sample(alpha, ar, x, z, WorldSplat.DryGrass);
                    worst = Mathf.Max(worst, g);
                    if (g > WallGrassWeightMax)
                        grassy++;
                    else
                        worstUnflagged = Mathf.Max(worstUnflagged, g);   // 안 걸린 것 중 가장 아슬아슬한 값
                }
            if (wall < 100)
                throw new InvalidOperationException("큰 경사 60°↑ 표본이 " + wall + "곳뿐입니다 — 이 자는 아무것도 재지 않았습니다.");

            float share = grassy / (float)wall;
            Debug.Log("[Ulon] §8.2 암벽 풀 — 큰 경사 60°↑ 표본 " + wall + "곳 · 풀 붙은 자리 " +
                      (share * 100f).ToString("0.0") + "% (상한 " + (WallGrassShareMax * 100f).ToString("0") +
                      "%) · 가장 짙은 풀 " + worst.ToString("0.00") + " · 안 걸린 것 중 최대 " +
                      worstUnflagged.ToString("0.00") + " (한 자리 상한 " + WallGrassWeightMax.ToString("0.00") + ")");

            string verdict = WallGrassVerdict(share);
            if (verdict != null)
                throw new InvalidOperationException(verdict);
        }

        /// <summary>판정만 하는 자리 — 표본과 떼어 놓아야 반대쪽 한계를 그냥 부를 수 있다.</summary>
        static string WallGrassVerdict(float grassyShare)
        {
            if (grassyShare > WallGrassShareMax)
                return "큰 경사 60°를 넘는 자리의 " + (grassyShare * 100f).ToString("0.0") + "%에 풀이 붙어 있습니다 — 상한 " +
                       (WallGrassShareMax * 100f).ToString("0") + "%. 수직 암벽에 초록 커튼이 흘러내립니다(§8.2 상식 모순).";
            return null;
        }

        static void AssertWallGrassNegativeControl()
        {
            if (WallGrassVerdict(0.4f) == null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 벽 40%에 풀이 붙었는데도 통과했습니다.");
            if (WallGrassVerdict(0.01f) != null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 벽이 깨끗한데 자가 걸렸습니다.");
            // 규칙 자체도 양쪽을 본다 — 완만한 데서는 상한을 안 풀고(중턱 풀 유지), 벽에서는 다 푼다.
            if (WorldSplat.WallRockFromTan(Mathf.Tan(45f * Mathf.Deg2Rad)) > 0.0001f)
                throw new InvalidOperationException("반대쪽 한계 실패 — 큰 경사 45° 비탈에서 중턱 풀 상한이 풀렸습니다(둥근 흙산이 맨바위가 된다).");
            if (WorldSplat.WallRockFromTan(Mathf.Tan(70f * Mathf.Deg2Rad)) < 0.9999f)
                throw new InvalidOperationException("반대쪽 한계 실패 — 큰 경사 70° 벽에서 중턱 풀 상한이 남았습니다.");
        }
    }
}
