using System;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **입구 근접 화면이 타지 않는가**(검수 큐 2, 2026-09-09).
        ///
        /// 등불 점광이 3.2세기로 대낮 화면을 태워 `07`이 250↑ 포화 **29.4%**였다 — 잔디도 석재도
        /// 흰 덩어리라 결이 안 보였다. 세기를 재서 0.8로 내렸고(`ExposureCensus`), 그 상태를 못박는다.
        ///
        /// 상한은 **재서** 잡았다: 고친 뒤 `07` 3.3 · `09` 0.4 · `11` 1.4이고 야외 광각은 2.3/1.8이다.
        /// 상한 **8%**는 그 위 여유 — 산포·시각이 흔들려도 안 울되, 옛 상태(29.4)는 확실히 문다.
        ///
        /// 렌더가 필요하므로 `-nographics` 셀프체크가 아니라 **`QaShots.Run` 끝**에서 돈다.
        /// NC: 등불 세기를 옛 3.2로 되돌리면 물어야 한다.
        /// </summary>
        const float EntranceHotMax = 0.08f;

        public static void AssertEntranceNotBlownOut()
        {
            string report = "";
            float worst = 0f;
            foreach (string shot in new[] { "07_d1_entrance", "09_d2_entrance", "11_d3_entrance" })
            {
                float hot = ExposureCensus.HotShare(shot);
                if (hot < 0f)
                    throw new InvalidOperationException("입구 노출 — 샷 " + shot +
                        "을 목록에서 못 찾았습니다. **못 재는 자를 초록불로 남기지 않는다.**");
                report += " · " + shot + " " + (hot * 100f).ToString("0.0") + "%";
                if (hot > worst) worst = hot;
                if (hot > EntranceHotMax)
                    throw new InvalidOperationException("샷 " + shot + "이 화면의 " + (hot * 100f).ToString("0.0") +
                        "%나 포화입니다(상한 " + (EntranceHotMax * 100f).ToString("0") +
                        "%) — 돌·잔디 결이 흰 덩어리로 뭉갭니다. 입구 등불 세기를 보십시오.");
            }
            Debug.Log("[Ulon] 입구 근접 노출 통과 — 상한 " + (EntranceHotMax * 100f).ToString("0") + "%" + report);

            float nc = ExposureCensus.HotShareWithLampIntensity("07_d1_entrance", 3.2f);
            if (nc <= EntranceHotMax)
                throw new InvalidOperationException("입구 노출 네거티브 컨트롤 실패 — 등불을 옛 세기(3.2)로 " +
                    "되돌렸는데도 " + (nc * 100f).ToString("0.0") + "%로 통과합니다. 자가 무력합니다.");
            Debug.Log("[Ulon] 입구 노출 네거티브 컨트롤 통과 — 옛 세기 3.2에서 " + (nc * 100f).ToString("0.0") + "%로 걸린다");
        }
    }
}
