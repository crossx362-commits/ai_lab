using System;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **두 벽 평면이 함께 그리는 자리가 얼마나 되나**(검수 랩 ㉧).
        ///
        /// 삼면 투영은 비스듬한 면에서 동서 벽(z,y)과 남북 벽(x,y) **둘을 섞는다**. 두 무늬가 같은
        /// 자리에 겹치면 화면에는 **바둑판 격자**가 뜬다 — 얼룩보다 나쁘다(더러운 게 아니라
        /// **깨져** 보인다). 그리고 여기 있던 자 셋(결·낙하선·덩이)은 그것을 **아무도 못 봤다**:
        /// 셋 다 「무엇이 얼마나 칠해졌나」를 묻지 「무엇이 그것을 그렸나」를 안 물었기 때문이다.
        ///
        /// 그래서 이 자는 **그리는 쪽**을 잰다: 급경사 자리마다 두 벽 평면의 가중치를 구해
        /// **약한 쪽 몫**을 보고, 그것이 유의미한(=둘이 함께 그리는) 자리의 비율을 센다.
        /// 격자는 그 비율이 높은 자리에서만 뜬다.
        /// </summary>
        const float PlaneMixWeakMin = 0.25f;    // 약한 평면이 이만큼 넘게 섞이면 「둘이 함께 그리는 자리」
        // 하한이 아니라 **상한**이고, 값은 **실제로 재서** 잡았다: 격자가 떴던 날카로움 4에서 **10.1%**,
        // 좁힌 판(16)에서 **2.6%**. 9%는 그 사이다 — **격자가 뜬 판이 이 자를 못 지나간다.**
        // (처음엔 44.6%/18.5% 같은 값을 짐작으로 적었다가 실측이 한 자릿수로 나와 지웠다.
        //  **재기 전에 적은 수는 수가 아니다.**)
        //
        // **이 자의 한계를 밝혀 둔다**: 격자 자체가 아니라 **격자가 앉을 수 있는 면적**을 잰다.
        // 같은 날카로움에서 무늬를 눕히면(랩 ㉦의 잔 주기) 이 값은 그대로인데 화면에는 격자가 뜬다 —
        // 그 판은 이 자가 못 잡는다. 눕히는 축을 만질 때는 **여전히 화면을 봐야 한다.**
        const float PlaneMixShareMax = 0.09f;

        static void AssertPlaneMix()
        {
            AssertPlaneMixNegativeControl();

            float share = PlaneMixShare(WorldSplat.PlaneSharp, out int samples);
            if (samples < 100)
                throw new InvalidOperationException("급경사 표본이 " + samples + "곳뿐입니다 — 이 자는 아무것도 재지 않았습니다.");

            Debug.Log("[Ulon] §8.2 평면 섞임 — 급경사 표본 " + samples + "곳 · 두 벽 평면이 함께 그리는 자리 " +
                      (share * 100f).ToString("0.0") + "% (상한 " + (PlaneMixShareMax * 100f).ToString("0") +
                      "%) · 날카로움 " + WorldSplat.PlaneSharp.ToString("0") + " · 같은 표본을 날카로움 4로 재면 " +
                      (PlaneMixShare(4f, out _) * 100f).ToString("0.0") + "%");

            string verdict = PlaneMixVerdict(share);
            if (verdict != null)
                throw new InvalidOperationException(verdict);
        }

        /// <summary>판정만 하는 자리 — 표본과 떼어 놓아야 반대쪽 한계를 그냥 부를 수 있다.</summary>
        static string PlaneMixVerdict(float share)
        {
            if (share > PlaneMixShareMax)
                return "급경사 자리의 " + (share * 100f).ToString("0.0") + "%에서 두 벽 평면이 함께 무늬를 그립니다(상한 " +
                       (PlaneMixShareMax * 100f).ToString("0") + "%) — 그 자리에는 바둑판 격자가 뜹니다.";
            return null;
        }

        static float PlaneMixShare(float sharp, out int samples)
        {
            samples = 0;
            int mixed = 0;
            for (float x = -145f; x <= 145f; x += 2.5f)
                for (float z = -145f; z <= 145f; z += 2.5f)
                {
                    if (WorldSplat.MacroSlopeTan(x, z) < 1.0f)      // 45° 아래는 위에서 본 면이 그린다
                        continue;
                    var n = WorldSplat.SurfaceNormalAt(x, z);
                    float a = Mathf.Pow(Mathf.Abs(n.x), sharp);
                    float b = Mathf.Pow(Mathf.Abs(n.z), sharp);
                    float sum = a + b;
                    if (sum < 1e-9f)
                        continue;
                    samples++;
                    if (Mathf.Min(a, b) / sum > PlaneMixWeakMin)
                        mixed++;
                }
            return samples > 0 ? mixed / (float)samples : 0f;
        }

        static void AssertPlaneMixNegativeControl()
        {
            if (PlaneMixVerdict(0.30f) == null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 세 자리 중 하나가 겹쳐 그리는데도 통과했습니다.");
            if (PlaneMixVerdict(0.02f) != null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 겹침이 드문데 자가 걸렸습니다.");
            // **실제 파이프라인 반대쪽 한계**: 격자가 떴던 날카로움 4로 같은 표본을 재면 빨간불이어야 한다.
            float blunt = PlaneMixShare(4f, out int n);
            if (n < 100)
                throw new InvalidOperationException("반대쪽 한계 실패 — 표본이 " + n + "곳뿐입니다.");
            if (PlaneMixVerdict(blunt) == null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 격자가 뜬 날카로움 4(" + (blunt * 100f).ToString("0.0") +
                                                   "%)가 이 자를 통과합니다. 그러면 이 자는 격자를 재는 것이 아닙니다.");
        }
    }
}
