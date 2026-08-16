using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    public static class BattleBackgroundAtlasSelfCheck
    {
        public static void Run()
        {
            Debug.Assert(BattleBackgroundAtlas.IsReady, "[BattleBackgroundAtlasSelfCheck] 전투 배경을 읽지 못했다");
            foreach (var key in BattleBackgroundAtlas.RequiredKeys)
            {
                var rect = BattleBackgroundAtlas.RectFor(key);
                Debug.Assert(rect.width > 0 && rect.height > 0, $"[BattleBackgroundAtlasSelfCheck] {key}: 빈 영역");
                Debug.Assert(rect.xMin >= 0 && rect.yMin >= 0 && rect.xMax <= BattleBackgroundAtlas.Width && rect.yMax <= BattleBackgroundAtlas.Height,
                    $"[BattleBackgroundAtlasSelfCheck] {key}: 아틀라스 밖 영역");
            }
            Debug.Log("[BattleBackgroundAtlasSelfCheck] PASS");
        }
    }
}
