using System;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// 월드맵이 지형을 실제로 읽는가 — 단색 자리표시면 물·마을이 같은 색이 된다.
        /// HUD와 같은 `WorldMapBake`를 부른다.
        /// </summary>
        static void AssertWorldMapBake()
        {
            int n = 96;
            var px = new Color32[n * n];
            WorldMapBake.Fill(px, n);
            int water = 0, land = 0;
            for (int i = 0; i < px.Length; i++)
            {
                if (WorldMapBake.IsWater(px[i])) water++;
                else land++;
            }
            if (water == 0)
                throw new InvalidOperationException("월드맵에 물이 0픽셀입니다 — 칠하지 않았거나 전부 뭍입니다.");
            if (land == 0)
                throw new InvalidOperationException("월드맵에 뭍이 0픽셀입니다 — 전부 물로 칠했습니다.");
            float waterShare = water / (float)px.Length;
            if (waterShare < 0.06f)
                throw new InvalidOperationException("월드맵 물 비율이 " + waterShare.ToString("0.00") +
                                                    "입니다(하한 0.06) — 섬 바깥 바다가 안 보입니다.");
            if (!WorldMapBake.IsWater(WorldMapBake.ColorOf(WorldTerrain.LakeX, WorldTerrain.LakeZ)))
                throw new InvalidOperationException("호수 원장 자리가 월드맵에서 물이 아닙니다.");
            if (WorldMapBake.IsWater(WorldMapBake.ColorOf(0f, 0f)))
                throw new InvalidOperationException("마을(0,0)이 월드맵에서 물입니다.");
            Debug.Log("[Ulon] 월드맵 통과 — " + n + "² 물 " + water + " · 뭍 " + land +
                      " · 한 변 " + WorldTerrain.Span.ToString("0") + "m");
        }
    }
}
