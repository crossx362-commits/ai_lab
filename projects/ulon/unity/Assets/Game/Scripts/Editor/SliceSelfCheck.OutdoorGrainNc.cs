using System;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **텍셀 자와 둑 자의 네거티브 컨트롤을 한 판에 묶는다**(검수 승인 2026-09-10).
        ///
        /// 둘은 각자 지형을 **두 번씩**(NC로 한 번, 되돌리려고 한 번) 구웠다. 굽기 한 판이 약 2분이라
        /// `qa_shots`가 8분 길어져 있었다. 두 조건(`NoiseRes = 128`·`GravelPatternOverride = 1`)은
        /// 서로 다른 겹을 건드리므로 **한 번에 같이 걸고 두 자를 나란히** 잴 수 있다 — 4회 → 2회.
        ///
        /// **조건 하나**(검수): 그 한 판에서 **두 자가 각각 제 이유로 빨간불**이어야 한다.
        /// 한쪽이 다른 쪽 때문에 물리면 그건 NC가 아니라 사고다. 그래서
        /// **어느 자가 얼마로 물렸는지 따로 찍고**, 홑 조건일 때의 실측을 옆에 같이 남긴다:
        /// 텍셀 홑 **1.79px** · 둑 홑 **0.65~0.67**. 묶은 판의 값이 이와 크게 다르면
        /// 서로 끌어당긴 것이니 묶음을 되돌리고 4회로 두어야 한다.
        ///
        /// 되돌리기는 `finally`에서 한 번만 굽는다(두 값을 같이 원장값으로 되돌린다).
        /// </summary>
        public static void AssertOutdoorGrainNegatives()
        {
            int keepRes = VisualSliceBuilder.NoiseRes;
            int keepGravel = VisualSliceBuilder.GravelPatternOverride;
            float ncTexel, ncBank;
            try
            {
                VisualSliceBuilder.NoiseRes = 128;                 // 텍셀 자가 물려야 하는 조건
                VisualSliceBuilder.GravelPatternOverride = 1;      // 둑 자가 물려야 하는 조건
                VisualSliceBuilder.EnsureVillageTerrain();         // 굽기 1회
                UnityEditor.AssetDatabase.SaveAssets();
                ncTexel = OutdoorCensus.MeasureGrain("64_river_bend").Median;
                ncBank = OutdoorCensus.BankLumpiness();
            }
            finally
            {
                VisualSliceBuilder.NoiseRes = keepRes;
                VisualSliceBuilder.GravelPatternOverride = keepGravel;
                VisualSliceBuilder.EnsureVillageTerrain();         // 굽기 2회(되돌리기)
                UnityEditor.AssetDatabase.SaveAssets();
            }

            Debug.Log("[Ulon] 묶은 네거티브 컨트롤 — 텍셀 " + ncTexel.ToString("0.00") +
                      "px(홑 조건 1.79px) · 둑 r(1) " + ncBank.ToString("0.00") + "(홑 조건 0.65~0.67)");

            if (ncTexel <= TexelPxMax)
                throw new InvalidOperationException("텍셀 네거티브 컨트롤 실패 — 옛 128px로 다시 구웠는데도 " +
                    ncTexel.ToString("0.00") + "px로 통과합니다. 자가 무력합니다.");
            Debug.Log("[Ulon] 텍셀 네거티브 컨트롤 통과 — 옛 128px에서 " + ncTexel.ToString("0.00") + "px로 걸린다");

            if (ncBank <= BankLumpMax)
                throw new InvalidOperationException("둑 잔결 네거티브 컨트롤 실패 — 옛 무늬로 다시 구웠는데도 " +
                    ncBank.ToString("0.00") + "로 통과합니다. 자가 무력합니다.");
            Debug.Log("[Ulon] 둑 잔결 네거티브 컨트롤 통과 — 옛 무늬에서 " + ncBank.ToString("0.00") + "로 걸린다");
        }
    }
}
