using UnityEngine;

namespace AshesToStars
{
    /// <summary>전투 이펙트가 캐릭터·몹·보스보다 뒤로 숨지 않게 전면 정렬값을 고정한다.</summary>
    public static class FxPoolSelfCheck
    {
        public static void Run()
        {
            Debug.Assert(FxPool.FrontSortingOrder > 1100,
                "[FxPool] 전면 이펙트 정렬값은 몬스터·보스 깊이 정렬(최대 1100)보다 커야 한다");
            Debug.Log("[FxPoolSelfCheck] PASS");
        }
    }
}
