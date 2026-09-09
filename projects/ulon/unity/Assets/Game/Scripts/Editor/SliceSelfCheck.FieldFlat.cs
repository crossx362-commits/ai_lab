using System;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **밭은 평평한 자리에 있어야 한다**(`18_meadow` 화면, 2026-09-09).
        ///
        /// 뙈기가 비탈에 앉으면 울타리와 이랑이 언덕을 타고 넘어 「경작지」가 아니라 「비탈에 친 울타리」로
        /// 읽힌다. 자는 원장(`FarmPlots.Base`)의 자리에서 **고저차**를 묻는다(상한 2.5m) —
        /// 씬을 훑지 않고 원장을 읽는 이유는, 굽는 쪽과 재는 쪽이 같은 답을 봐야 하기 때문이다.
        ///
        /// NC는 `FarmPlots.FlattenDisabled`로 **땅 고르기만 끄고** 같은 자를 다시 돌린다:
        /// 끄면 6.2m·4.3m이라 물어야 한다. 안 물면 이 자는 평지든 비탈이든 통과시키고 있다.
        /// </summary>
        static void AssertFieldFlat()
        {
            var r = WorldRegions.Meadow;
            var resolved = FarmPlots.Base(r);
            string report = "";
            float worst = 0f;
            for (int i = 0; i < resolved.Length; i++)
            {
                float spread = FarmPlots.Spread(resolved[i].x, resolved[i].y, FarmPlots.Halves[i]);
                report += " · 뙈기 " + (i + 1) + " " + spread.ToString("0.0") + "m";
                if (spread > worst) worst = spread;
                if (spread > FarmPlots.SpreadMax)
                    throw new InvalidOperationException("농경지 뙈기 " + (i + 1) + "의 고저차가 " +
                        spread.ToString("0.0") + "m입니다(상한 " + FarmPlots.SpreadMax +
                        "m) — 밭이 비탈에 걸쳐 「경작지」로 안 읽힙니다.");
            }
            float ncWorst = 0f;
            FarmPlots.FlattenDisabled = true;
            try
            {
                for (int i = 0; i < resolved.Length; i++)
                    ncWorst = Mathf.Max(ncWorst, FarmPlots.Spread(resolved[i].x, resolved[i].y, FarmPlots.Halves[i]));
            }
            finally { FarmPlots.FlattenDisabled = false; }
            if (ncWorst <= FarmPlots.SpreadMax)
                throw new InvalidOperationException("밭 평탄도 네거티브 컨트롤 실패 — 땅 고르기를 꺼도 " +
                    "최악 고저차가 " + ncWorst.ToString("0.0") + "m라 통과합니다. 이 자는 아무것도 가르지 않습니다.");
            Debug.Log("[Ulon] 밭 평탄도 통과 — 최악 " + worst.ToString("0.0") + "m(상한 " + FarmPlots.SpreadMax +
                      "m)" + report + " · NC(땅 고르기 끔) 최악 " + ncWorst.ToString("0.0") + "m로 FAIL");
        }
    }
}
