using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>전투 아이콘 시트가 Resources에서 읽히고 지정한 모든 아이콘이 원본 안에 있는지 검사한다.</summary>
    public static class CombatIconAtlasSelfCheck
    {
        public static void Run()
        {
            Debug.Assert(CombatIconAtlas.IsReady,
                "[CombatIconAtlasSelfCheck] 전투 아이콘 시트를 Resources/ui에서 읽지 못했다");

            foreach (var key in CombatIconAtlas.RequiredKeys)
            {
                var rect = CombatIconAtlas.RectFor(key);
                Debug.Assert(rect.width > 0 && rect.height > 0,
                    $"[CombatIconAtlasSelfCheck] {key}: 빈 영역");
                Debug.Assert(rect.xMin >= 0 && rect.yMin >= 0 &&
                             rect.xMax <= CombatIconAtlas.Width && rect.yMax <= CombatIconAtlas.Height,
                    $"[CombatIconAtlasSelfCheck] {key}: 아틀라스 밖 영역 {rect}");
            }

            Debug.Log("[CombatIconAtlasSelfCheck] PASS");
        }
    }
}
