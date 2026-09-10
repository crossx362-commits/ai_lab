using System;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **수면 무늬가 일정 간격으로 되풀이되지 않는가**(검수 판정 2026-09-10).
        ///
        /// 바로 앞 랩이 세운 자(`AssertWaterNotStamped` — 여덟 방향 상관 최대−최소)는
        /// **어느 쪽으로 쏠렸나**만 묻는다. 그래서 **방향 있는 규칙 무늬**를 그냥 통과시켰다:
        /// 채택본이 0.69로 합격했는데 근접(`64_river_bend`)에서 물이 **사선 직조 천**으로 읽혔다.
        /// 원장 `c12231ed` — 「쏠림」과 「규칙성」은 다른 축이다. 이건 그 두 번째 축이다.
        ///
        /// 눈금은 `OutdoorCensus.WaterRepeatPeak()` — lag 5~40 창에서 **국소 최대인** 자기상관의
        /// 최대. 비탈(결 자체)은 원점 쪽 이웃이 늘 더 높아 걸러지고, 주기마다 **되살아난** 자리만
        /// 남는다(자를 한 번 잘못 지은 전말은 그 파일에).
        ///
        /// 실측 **옛 무늬 7 = 0.705 · 채택본 9 = 0.073**. 상한 **0.30**은 그 사이다.
        /// NC는 옛 무늬로 **수면을 실제로 다시 구워** 확인한다(지형은 안 굽는다 — 몇 초).
        ///
        /// **기존 자 셋(수면 색차·포화·방향 쏠림)은 건드리지 않았다** — 새 축만 더한다.
        /// </summary>
        const float WaterRepeatMax = 0.30f;

        public static void AssertWaterNotWoven()
        {
            float p = OutdoorCensus.WaterRepeatPeak();
            Debug.Log("[Ulon] 수면 되풀이 — 봉우리 " + p.ToString("0.00") +
                      " (상한 " + WaterRepeatMax.ToString("0.00") + ")");
            if (p > WaterRepeatMax)
                throw new InvalidOperationException("수면 무늬의 되풀이 봉우리가 " + p.ToString("0.00") +
                    "입니다(상한 " + WaterRepeatMax.ToString("0.00") +
                    ") — 같은 알갱이가 일정 간격으로 되돌아와 물이 아니라 짜인 천으로 읽힙니다.");

            int keep = VisualSliceBuilder.WaterPatternOverride;
            float nc;
            try
            {
                VisualSliceBuilder.WaterPatternOverride = 7;   // 옛 무늬(128칸 문지름 — 사선 직조)
                VisualSliceBuilder.RebuildWater();
                nc = OutdoorCensus.WaterRepeatPeak();
            }
            finally
            {
                VisualSliceBuilder.WaterPatternOverride = keep;
                VisualSliceBuilder.RebuildWater();
            }
            if (nc <= WaterRepeatMax)
                throw new InvalidOperationException("수면 되풀이 네거티브 컨트롤 실패 — 옛 무늬로 다시 구웠는데도 " +
                    nc.ToString("0.00") + "로 통과합니다. 자가 무력합니다.");
            Debug.Log("[Ulon] 수면 되풀이 네거티브 컨트롤 통과 — 옛 무늬에서 " + nc.ToString("0.00") + "로 걸린다");
        }
    }
}
