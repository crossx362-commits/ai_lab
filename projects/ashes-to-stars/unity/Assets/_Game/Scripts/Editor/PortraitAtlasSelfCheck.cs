using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>파티 초상화 시트가 읽히고, HUD에서 쓰는 초상화 영역이 원본을 벗어나지 않는지 검사한다.</summary>
    public static class PortraitAtlasSelfCheck
    {
        public static void Run()
        {
            Debug.Assert(PortraitAtlas.IsReady,
                "[PortraitAtlasSelfCheck] 파티 초상화 시트를 Resources/ui에서 읽지 못했다");

            foreach (var key in PortraitAtlas.RequiredKeys)
            {
                var rect = PortraitAtlas.RectFor(key);
                Debug.Assert(rect.width > 0 && rect.height > 0,
                    $"[PortraitAtlasSelfCheck] {key}: 빈 영역");
                Debug.Assert(rect.xMin >= 0 && rect.yMin >= 0 &&
                             rect.xMax <= PortraitAtlas.Width && rect.yMax <= PortraitAtlas.Height,
                    $"[PortraitAtlasSelfCheck] {key}: 아틀라스 밖 영역 {rect}");
            }

            Debug.Log("[PortraitAtlasSelfCheck] PASS");
        }
    }
}
