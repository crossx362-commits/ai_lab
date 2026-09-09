using System;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **강둑이 바둑판으로 읽히지 않는가**(검수 판정 2026-09-10 — 텍셀 랩을 닫고도 화면에 남은 것).
        ///
        /// 셈이 범인을 갈랐다(`OutdoorCensus.RunBankBlob`): 그 둑은 **MineGravel 72.6%**이고,
        /// 그 겹의 무늬만 지우면 잔결이 6.88 → 3.58로 **반이 날아갔다**. 해를 꺼도 상대 대비는
        /// 그대로였고(음영 아님) 도포 1등이 뒤집히는 칸은 2.8%뿐이었다(섞임 아님).
        /// 자갈이 **산 암벽 무늬(사선 층리)**를 쓰고 있던 것이 원인이라 제 무늬로 갈랐다.
        ///
        /// 자는 **잔결의 이웃 상관 r(1)** — 한 픽셀이 옆 픽셀과 얼마나 같은가. 덩어리로 뭉치면
        /// 높고, 낟알이면 낮다. 실측 무늬 1 **0.64** · 무늬 6 0.73 · **무늬 5 0.29**.
        /// 상한 **0.45**는 그 사이다. NC는 **옛 무늬로 실제로 다시 구워** 확인한다.
        ///
        /// 렌더가 필요하므로 `QaShots.Run` 끝에서 돈다.
        /// </summary>
        const float BankLumpMax = 0.45f;

        public static void AssertBankNotBlocky()
        {
            float r1 = OutdoorCensus.BankLumpiness();
            Debug.Log("[Ulon] 둑 잔결 — 64_river_bend 이웃 상관 r(1) " + r1.ToString("0.00") +
                      " (상한 " + BankLumpMax.ToString("0.00") + ")");
            if (r1 > BankLumpMax)
                throw new InvalidOperationException("강둑 잔결의 이웃 상관이 " + r1.ToString("0.00") +
                    "입니다(상한 " + BankLumpMax.ToString("0.00") +
                    ") — 바닥이 낟알이 아니라 덩어리(바둑판)로 읽힙니다. 자갈 겹 무늬를 보십시오.");

            int keep = VisualSliceBuilder.GravelPatternOverride;
            float nc;
            try
            {
                VisualSliceBuilder.GravelPatternOverride = 1;   // 옛 무늬(산 암벽 층리)
                VisualSliceBuilder.EnsureVillageTerrain();
                UnityEditor.AssetDatabase.SaveAssets();
                nc = OutdoorCensus.BankLumpiness();
            }
            finally
            {
                VisualSliceBuilder.GravelPatternOverride = keep;
                VisualSliceBuilder.EnsureVillageTerrain();
                UnityEditor.AssetDatabase.SaveAssets();
            }
            if (nc <= BankLumpMax)
                throw new InvalidOperationException("둑 잔결 네거티브 컨트롤 실패 — 옛 무늬로 다시 구웠는데도 " +
                    nc.ToString("0.00") + "로 통과합니다. 자가 무력합니다.");
            Debug.Log("[Ulon] 둑 잔결 네거티브 컨트롤 통과 — 옛 무늬에서 " + nc.ToString("0.00") + "로 걸린다");
        }
    }
}
