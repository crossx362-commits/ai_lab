using System;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **바다 해안이 백사장인가**(대장 판정 ⓐ 2026-09-11 — 「44°로 꺼지는 해안은 상식 모순,
        /// 실제 백사장은 1~5°」).
        ///
        /// 재는 것은 **수면 아래 얕은 물의 가로 폭**이다(`OutdoorCensus.CoastRampStats`) —
        /// 물가에서 **수심 0.5m까지 몇 미터**인가. 화면에서 「얕은 물 띠」로 읽히는 것이 그 폭이고,
        /// 원거리 조망에서 3px가 되려면 대략 6~10m가 필요하다(검수 조건).
        ///
        /// **상한도 문다**(검수 조건): 완만할수록 좋은 것이 아니다 — 끝없이 완만하면
        /// 섬이 **접시**가 된다. 구간으로 가둔다.
        ///
        /// **이 자가 못 보는 것**: 화면이다. 폭이 맞아도 색·거품이 없으면 눈에는 여전히 맨 선이다 —
        /// 판정은 `15_lake_river`·`14_world_vista`가 한다.
        /// </summary>
        const float BeachHalfMin = 6.0f;    // 수심 0.5m까지 가로 중앙값 하한(실측 7.3m, 옛 해안 0.5m)
        const float BeachHalfMax = 12.0f;   // 상한 — 이보다 넓으면 섬이 접시로 읽힌다
        const float BeachDegMin = 1.5f;     // 실제 백사장 1~5°(대장), 아래로는 물가가 안 정해진다
        const float BeachDegMax = 6.0f;     // 실측 3.4°, 옛 해안 48°

        static void AssertCoastRamp()
        {
            OutdoorCensus.CoastRampStats(out _, out float med, out float medOne, out float deg,
                                         out int n, out int excluded, out string det);
            if (n < 12)
                throw new InvalidOperationException("해안을 잰 방위가 " + n + "곳뿐입니다 — " + det +
                    ". **못 재는 자를 초록불로 남기지 않는다.**");
            Debug.Log("[Ulon] 해안 램프 — " + det + " (0.5m 폭 구간 " + BeachHalfMin.ToString("0.0") + "~" +
                      BeachHalfMax.ToString("0.0") + "m · 기울기 구간 " + BeachDegMin.ToString("0.0") + "~" +
                      BeachDegMax.ToString("0.0") + "°)");
            if (excluded == 0)
                throw new InvalidOperationException("강 하구 예외가 **한 곳도 안 걸렸습니다** — " +
                    "아무것도 안 거르는 예외는 자를 무르게 만들 뿐입니다.");
            if (med < BeachHalfMin)
                throw new InvalidOperationException("해안의 얕은 물 띠가 가로 " + med.ToString("0.0") +
                    "m뿐입니다(하한 " + BeachHalfMin.ToString("0.0") + "m) — 원거리 조망에서 맨 선으로 보입니다.");
            if (med > BeachHalfMax)
                throw new InvalidOperationException("해안의 얕은 물 띠가 가로 " + med.ToString("0.0") +
                    "m입니다(상한 " + BeachHalfMax.ToString("0.0") + "m) — 섬이 **접시**로 읽힙니다.");
            if (deg < BeachDegMin || deg > BeachDegMax)
                throw new InvalidOperationException("물가 기울기가 " + deg.ToString("0.0") +
                    "°입니다(구간 " + BeachDegMin.ToString("0.0") + "~" + BeachDegMax.ToString("0.0") + "°).");

            // **네거티브 컨트롤** — 옛 해안(산끝에서 10m 직하)으로 되돌리면 이 자가 울어야 한다.
            WorldTerrain.BeachDisabled = true;
            try
            {
                OutdoorCensus.CoastRampStats(out _, out float ncMed, out _, out float ncDeg,
                                             out int ncN, out _, out string ncDet);
                if (ncN >= 12 && ncMed >= BeachHalfMin && ncDeg <= BeachDegMax)
                    throw new InvalidOperationException("해안 램프 네거티브 컨트롤 실패 — 옛 해안으로 " +
                        "되돌렸는데도 통과합니다(" + ncDet + "). 자가 무력합니다.");
                Debug.Log("[Ulon] 해안 램프 네거티브 컨트롤 통과 — 옛 해안이면 " + ncDet);
            }
            finally { WorldTerrain.BeachDisabled = false; }
        }
    }
}
