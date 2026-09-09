using System;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **수면이 「되풀이 도장」이 아니라 물로 보이는가**(검수 판정 2026-09-10).
        ///
        /// 물은 잔풀 잡음(무늬 0)을 쓰고 있었다. 그 안의 `x*y` 항이 큰 고리를 만들어, 넓게 깔면
        /// **똑같은 원형 메달이 바둑판으로** 늘어섰다. 남의 무늬를 빌려 쓴 자리였다(둑 자갈과 같은 병).
        ///
        /// **자를 세 번 잘못 지었다**(먼저 적어 둔다):
        /// ① *화면에서 되풀이 주기 재기* — `64_river_bend`가 후보 A/B/C에서 수치가 소수점까지 같은데
        ///    **화면은 완전히 달랐다.** 그 자리에서 수면을 못 보고 있었다.
        /// ② *성긴 눈금 뭉갬* — 무늬 0 0.06 대 무늬 7 0.46으로 **눈과 반대**를 가리켰다.
        /// ③ *lag 16 방향 갈림* — 사인 무늬에서는 갈랐지만(0.22 대 0.49), 채택본(문지른 낟알)에서는
        ///    0.08로 떨어져 **무늬가 바뀌자 자가 죽었다.** 눈금을 결의 길이(lag 4)로 옮겨 살렸다.
        ///
        /// **무늬도 네 번 갈아엎었다.** 사인 파열 4겹 → 휜 4겹 → 3겹 → 칸 값 잡음. 넷 다 멀리서
        /// 바다가 **십자 격자(옷감)**가 됐다 — 교차하는 파열도, 칸을 쓰는 잡음도 격자를 만든다.
        /// 범인이 무늬임은 **수면만 단색으로 구워** 확인했다(`builds/qa/water/14_flatwater.png`:
        /// 격자가 사라진다). 채택본은 칸도 파열도 없이 **낟알을 대각선으로 문지른** 것이다.
        ///
        /// 자는 **lag 4텍셀 여덟 방향 상관의 최대−최소**. 문지른 방향만 상관이 남아 크게 갈린다.
        /// 실측 **무늬 0 = 0.15 · 채택본 = 0.69**. 하한 **0.40**은 그 사이다.
        /// NC는 옛 무늬로 **수면을 실제로 다시 구워** 확인한다(지형은 안 굽는다 — 몇 초).
        ///
        /// 이미 있는 **수면 색차·포화 자는 건드리지 않았다** — 이건 새 축이다.
        /// </summary>
        const float WaterFlowMin = 0.40f;

        public static void AssertWaterNotStamped()
        {
            float f = OutdoorCensus.WaterFlowiness();
            Debug.Log("[Ulon] 수면 결 — 방향 갈림 " + f.ToString("0.00") +
                      " (하한 " + WaterFlowMin.ToString("0.00") + ")");
            if (f < WaterFlowMin)
                throw new InvalidOperationException("수면 무늬의 방향 갈림이 " + f.ToString("0.00") +
                    "입니다(하한 " + WaterFlowMin.ToString("0.00") +
                    ") — 결이 사방으로 같아 물이 아니라 되풀이 도장으로 읽힙니다.");

            int keep = VisualSliceBuilder.WaterPatternOverride;
            float nc;
            try
            {
                VisualSliceBuilder.WaterPatternOverride = 0;   // 옛 무늬(잔풀 잡음 — 메달 격자)
                VisualSliceBuilder.RebuildWater();
                nc = OutdoorCensus.WaterFlowiness();
            }
            finally
            {
                VisualSliceBuilder.WaterPatternOverride = keep;
                VisualSliceBuilder.RebuildWater();
            }
            if (nc >= WaterFlowMin)
                throw new InvalidOperationException("수면 결 네거티브 컨트롤 실패 — 옛 무늬로 다시 구웠는데도 " +
                    nc.ToString("0.00") + "로 통과합니다. 자가 무력합니다.");
            Debug.Log("[Ulon] 수면 결 네거티브 컨트롤 통과 — 옛 무늬에서 " + nc.ToString("0.00") + "로 걸린다");
        }
    }
}
