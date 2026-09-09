using System;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **수면이 흰 구멍으로 타지 않는가**(검수 관찰 ⓐ, 2026-09-10).
        ///
        /// 호수를 원장 크기로 판 뒤 `15`의 흰 포화가 0.32 → **1.60%**(가장 큰 덩어리 3,635px)가 됐고
        /// 화면에서 **물에 구멍이 뚫린 것처럼** 보였다. 후보를 나란히 재서 갈랐다 — 정반사 하나다
        /// (매끄러움 0이면 0.00, 금속기 0은 1.68로 무관, 태양을 끄면 0.00). 재질 매끄러움을
        /// 0.85 → **0.55**로 내렸고 그 상태를 못박는다.
        ///
        /// 상한은 **재서** 잡았다: 고친 뒤 `15` 0.02 · `35` 0.19 · `14` 0.00이다. **0.5%**는 그 위 여유다.
        /// **반짝임을 0으로 만드는 것은 답이 아니므로**(물이 죽는다) 이 자는 하한을 두지 않는다 —
        /// 「물이 물로 보이나」는 눈이 판정한다.
        ///
        /// 렌더가 필요하므로 `-nographics` 셀프체크가 아니라 **`QaShots.Run` 끝**에서 돈다.
        /// NC: 매끄러움을 옛 0.85로 되돌리면 물어야 한다.
        /// </summary>
        const float WaterWhiteMax = 0.005f;

        public static void AssertWaterNotBlownOut()
        {
            string report = "";
            foreach (string shot in new[] { "15_lake_river", "35_fishing", "14_world_vista" })
            {
                float w = ExposureCensus.WhiteShareWithGloss(shot, float.NaN);
                if (w < 0f)
                    throw new InvalidOperationException("수면 반사 — 샷 " + shot +
                        "을 목록에서 못 찾았습니다. **못 재는 자를 초록불로 남기지 않는다.**");
                report += " · " + shot + " " + (w * 100f).ToString("0.00") + "%";
                if (w > WaterWhiteMax)
                    throw new InvalidOperationException("샷 " + shot + "의 수면이 화면의 " +
                        (w * 100f).ToString("0.00") + "%나 흰 포화입니다(상한 " +
                        (WaterWhiteMax * 100f).ToString("0.0") + "%) — 물에 구멍이 뚫린 것처럼 보입니다. " +
                        "수면 재질 매끄러움을 보십시오.");
            }
            Debug.Log("[Ulon] 수면 반사 통과 — 상한 " + (WaterWhiteMax * 100f).ToString("0.0") + "%" + report);

            float nc = ExposureCensus.WhiteShareWithGloss("15_lake_river", 0.85f);
            if (nc <= WaterWhiteMax)
                throw new InvalidOperationException("수면 반사 네거티브 컨트롤 실패 — 매끄러움을 옛 값(0.85)으로 " +
                    "되돌렸는데도 " + (nc * 100f).ToString("0.00") + "%로 통과합니다. 자가 무력합니다.");
            Debug.Log("[Ulon] 수면 반사 네거티브 컨트롤 통과 — 옛 매끄러움 0.85에서 " +
                      (nc * 100f).ToString("0.00") + "%로 걸린다");
        }
    }
}
