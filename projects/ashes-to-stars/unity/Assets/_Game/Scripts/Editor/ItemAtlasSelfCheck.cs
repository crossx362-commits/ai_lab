using UnityEngine;

namespace AshesToStars
{
    /// <summary>성장·보상 아이템 아틀라스가 Resources에서 읽히고, 등록 조각이 원본 안에 있는지 검사한다.</summary>
    public static class ItemAtlasSelfCheck
    {
        public static void Run()
        {
            Debug.Assert(ItemAtlas.IsReady, "[ItemAtlasSelfCheck] 아이템 아틀라스를 Resources/ui에서 읽지 못했다");

            foreach (var key in ItemAtlas.RequiredKeys)
            {
                var rect = ItemAtlas.RectFor(key);
                Debug.Assert(rect.width > 0 && rect.height > 0,
                    $"[ItemAtlasSelfCheck] {key}: 빈 영역");
                Debug.Assert(rect.xMin >= 0 && rect.yMin >= 0 &&
                             rect.xMax <= ItemAtlas.Width && rect.yMax <= ItemAtlas.Height,
                    $"[ItemAtlasSelfCheck] {key}: 아틀라스 밖 영역 {rect}");
            }

            Debug.Log("[ItemAtlasSelfCheck] PASS");
        }
    }
}
