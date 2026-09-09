using System;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **근접에서 텍셀이 눈에 보이면 안 된다**(검수 지시 2026-09-10).
        ///
        /// `64` 근접의 각짐을 셈으로 갈랐더니(`be7893de`) 하이트맵·알파맵 격자는 무죄였고 범인은
        /// **텍셀**이었다 — 지형 텍스처가 전부 128px이라 텍셀 하나가 화면 1.79px(중앙값)로 보였다.
        /// 눈금은 검수가 정했다: **텍셀 하나가 화면 몇 px인가**, 1px 이하면 눈에서 사라진다.
        ///
        /// 상한 **1.0px**은 그 문장 그대로다. 128 → 1.79px, 256 → 0.89px(채택), 512 → 0.45px.
        ///
        /// **조망 쪽에도 바닥을 둔다.** 해상도를 올리면 원경 무늬가 밉맵에 뭉개진다(실측:
        /// `14` 이웃 밝기차 128에서 3.03 · 256에서 2.46 · 512에서 2.21). 근접을 고치다 원경을 잃으면
        /// 수리가 아니라 맞바꿈이다 — 2.0을 바닥으로 둔다.
        ///
        /// 렌더가 필요하므로 `-nographics` 셀프체크가 아니라 **`QaShots.Run` 끝**에서 돈다.
        /// NC는 **텍스처를 실제로 128로 다시 구워** 물리는지 본다(자의 나눗셈만 뒤집으면 굽는
        /// 경로가 끊겨도 초록불이다 — NC는 결함을 만든 조건을 전부 되돌려야 한다).
        /// </summary>
        const float TexelPxMax = 1.0f;
        const float VistaContrastMin = 2.0f;

        public static void AssertTexelNotBlocky()
        {
            var near = OutdoorCensus.MeasureGrain("64_river_bend");
            Debug.Log("[Ulon] 텍셀 크기 — 64_river_bend 중앙값 " + near.Median.ToString("0.00") +
                      "px · 상위10% " + near.P90.ToString("0.00") + "px · 1px 넘는 자리 " +
                      near.OverShare.ToString("0.0") + "% (상한 " + TexelPxMax.ToString("0.0") + "px)");
            if (near.Median > TexelPxMax)
                throw new InvalidOperationException("근접 화면에서 텍셀 하나가 " +
                    near.Median.ToString("0.00") + "px로 보입니다(상한 " + TexelPxMax.ToString("0.0") +
                    "px) — 바닥이 격자로 읽힙니다. 무늬 텍스처 해상도(VisualSliceBuilder.NoiseRes)를 보십시오.");

            var vista = OutdoorCensus.MeasureGrain("14_world_vista");
            Debug.Log("[Ulon] 원경 무늬 세기 — 14_world_vista 이웃 밝기차 " +
                      vista.Contrast.ToString("0.00") + " (하한 " + VistaContrastMin.ToString("0.0") + ")");
            if (vista.Contrast < VistaContrastMin)
                throw new InvalidOperationException("조망에서 지형 무늬가 " + vista.Contrast.ToString("0.00") +
                    "까지 뭉개졌습니다(하한 " + VistaContrastMin.ToString("0.0") +
                    ") — 근접을 고치다 원경을 잃으면 수리가 아니라 맞바꿈입니다.");

            int keep = VisualSliceBuilder.NoiseRes;
            float ncMedian;
            try
            {
                VisualSliceBuilder.NoiseRes = 128;
                VisualSliceBuilder.EnsureVillageTerrain();
                UnityEditor.AssetDatabase.SaveAssets();
                ncMedian = OutdoorCensus.MeasureGrain("64_river_bend").Median;
            }
            finally
            {
                VisualSliceBuilder.NoiseRes = keep;
                VisualSliceBuilder.EnsureVillageTerrain();
                UnityEditor.AssetDatabase.SaveAssets();
            }
            if (ncMedian <= TexelPxMax)
                throw new InvalidOperationException("텍셀 네거티브 컨트롤 실패 — 옛 128px로 다시 구웠는데도 " +
                    ncMedian.ToString("0.00") + "px로 통과합니다. 자가 무력합니다.");
            Debug.Log("[Ulon] 텍셀 네거티브 컨트롤 통과 — 옛 128px에서 " + ncMedian.ToString("0.00") + "px로 걸린다");
        }
    }
}
