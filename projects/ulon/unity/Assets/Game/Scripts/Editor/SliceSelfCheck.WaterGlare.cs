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
        /// 상한은 **재서** 잡았다. 옛 눈금(화면 전체)에서는 `15` 0.02 · `35` 0.19 · `14` 0.00에
        /// 상한 0.5%였다. **열린 물로 좁힌 새 눈금에서는 수가 훨씬 작다** — 실측 `15` 0.00 ·
        /// `35` 0.02 · `14` 0.00, NC(옛 매끄러움 0.85) `15` 0.16%. 그래서 상한도 **0.08%**로
        /// 다시 골랐다(실측의 네 배, NC의 절반). 옛 0.5%를 그대로 뒀으면 **NC가 통과해**
        /// 자가 무력해진다 — 대상을 바꾸면 상한도 반드시 다시 잰다.
        /// **반짝임을 0으로 만드는 것은 답이 아니므로**(물이 죽는다) 이 자는 하한을 두지 않는다 —
        /// 「물이 물로 보이나」는 눈이 판정한다.
        ///
        /// **대상을 좁혔다**(2026-09-11, 물 재작업 1단계): 물가 거품이 들어오자 이 자가 `15`에서
        /// 0.93%로 울었는데 그 흰 픽셀은 정반사 구멍이 아니라 **호수를 두른 거품 띠**였다 —
        /// 이번 랩이 일부러 넣은 그림이다. 자를 끄지 않고 **원래 뜻으로 좁힌다**: 흰 구멍은
        /// 열린 물 한가운데서 나므로 **수심 1m 이상인 물 픽셀만** 센다
        /// (`ExposureCensus.OpenWaterWhiteShare`). 물가 거품은 제 자가 따로 본다.
        ///
        /// 렌더가 필요하므로 `-nographics` 셀프체크가 아니라 **`QaShots.Run` 끝**에서 돈다.
        /// NC: 매끄러움을 옛 0.85로 되돌리면 물어야 한다.
        /// </summary>
        const float WaterWhiteMax = 0.0008f;   // 열린 물 눈금 — 실측 0.00·0.02·0.00%, NC 0.16%(2026-09-11)
        const float LakeSeaLumaMax = 1.35f;   // 같은 깊이 구간끼리 — 실측 1.12, NC 1.81(2026-09-11 재조준)

        public static void AssertWaterNotBlownOut()
        {
            string report = "";
            foreach (string shot in new[] { "15_lake_river", "35_fishing", "14_world_vista" })
            {
                float w = ExposureCensus.OpenWaterWhiteShare(shot, float.NaN);
                if (w < 0f)
                    throw new InvalidOperationException("수면 반사 — 샷 " + shot +
                        "을 목록에서 못 찾았습니다. **못 재는 자를 초록불로 남기지 않는다.**");
                report += " · " + shot + " " + (w * 100f).ToString("0.00") + "%";
                if (w > WaterWhiteMax)
                    throw new InvalidOperationException("샷 " + shot + "의 수면이 화면의 " +
                        (w * 100f).ToString("0.00") + "%나 흰 포화입니다(상한 " +
                        (WaterWhiteMax * 100f).ToString("0.00") + "%) — 물에 구멍이 뚫린 것처럼 보입니다. " +
                        "수면 재질 매끄러움을 보십시오.");
            }
            Debug.Log("[Ulon] 수면 반사 통과(열린 물) — 상한 " + (WaterWhiteMax * 100f).ToString("0.00") + "%" + report);

            // **한 화면에서 같은 물이 두 물감이면 안 된다**(검수 판정 2026-09-10). 포화가 0이어도
            // 호수가 바다보다 두 배 밝으면 우유빛 웅덩이로 읽힌다 — 포화 자는 그걸 못 본다.
            //
            // **대상을 바꿨다**(검수 판정 2026-09-11, 물 재작업 1단계): 물이 깊이에 따라 다른 색이
            // 된 뒤로 「호수 전체 대 바다 전체」는 **목표를 결함으로 읽는다**(호수는 얕고 바다는
            // 깊으니 색이 다른 것이 맞다). 앞으로는 **같은 깊이 구간의 물끼리** 견준다.
            // 상한도 새 눈금에서 다시 골랐다 — 실측 **1.12**, NC(옛 매끄러움 0.55) 1.81, 0에서 0.97.
            // **1.35**는 그 사이다.
            // 이 자가 못 보는 것: 두 물이 **같은 깊이를 가진 자리가 없으면** 아무 말도 못 한다.
            // 지금 표본이 서는 구간은 1.5~3.0m 하나뿐이다(바다 바닥은 온통 2.6m, 호수 얕은 데는
            // 바다에 짝이 없다) — 셈이 구간별 표본 수를 같이 찍는 이유다.
            float ratio = ExposureCensus.LakeSeaLumaRatioByDepth(float.NaN, out string toneDetail);
            if (ratio < 0f)
                throw new InvalidOperationException("수면 색차 — 같은 깊이 구간에 호수·바다 표본이 " +
                    "모자랍니다(" + toneDetail + "). **못 재는 자를 초록불로 남기지 않는다.**");
            Debug.Log("[Ulon] 수면 색차(같은 깊이끼리) — 가장 나쁜 비 " + ratio.ToString("0.00") +
                      " (상한 " + LakeSeaLumaMax.ToString("0.00") + ") · " + toneDetail);
            if (ratio > LakeSeaLumaMax)
                throw new InvalidOperationException("호수가 바다보다 " + ratio.ToString("0.00") +
                    "배 밝습니다(상한 " + LakeSeaLumaMax.ToString("0.00") +
                    ") — 한 화면에서 같은 물이 두 물감으로 보입니다.");
            float ncRatio = ExposureCensus.LakeSeaLumaRatioByDepth(0.55f, out _);
            if (ncRatio <= LakeSeaLumaMax)
                throw new InvalidOperationException("수면 색차 네거티브 컨트롤 실패 — 매끄러움 0.55에서 " +
                    ncRatio.ToString("0.00") + "배로 통과합니다. 자가 무력합니다.");
            Debug.Log("[Ulon] 수면 색차 네거티브 컨트롤 통과 — 0.55에서 " + ncRatio.ToString("0.00") + "배로 걸린다");

            float nc = ExposureCensus.OpenWaterWhiteShare("15_lake_river", 0.85f);
            if (nc <= WaterWhiteMax)
                throw new InvalidOperationException("수면 반사 네거티브 컨트롤 실패 — 매끄러움을 옛 값(0.85)으로 " +
                    "되돌렸는데도 " + (nc * 100f).ToString("0.00") + "%로 통과합니다. 자가 무력합니다.");
            Debug.Log("[Ulon] 수면 반사 네거티브 컨트롤 통과 — 옛 매끄러움 0.85에서 " +
                      (nc * 100f).ToString("0.00") + "%로 걸린다");
        }
    }
}
