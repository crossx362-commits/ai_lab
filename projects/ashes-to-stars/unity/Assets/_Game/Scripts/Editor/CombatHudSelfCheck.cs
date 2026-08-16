using UnityEngine;

namespace AshesToStars
{
    /// <summary>전투 HUD가 전장과 서로의 안전 영역을 침범하지 않게 하는 계약 검사다.</summary>
    public static class CombatHudSelfCheck
    {
        public static void Run()
        {
            Debug.Assert(W3Party.CombatHudTopHeight <= 72f,
                "[전투 HUD] 상단 정보는 72px를 넘기면 전장을 가린다");
            Debug.Assert(W3Party.CombatHudBottomHeight >= 148f,
                "[전투 HUD] 하단 지휘 영역은 카드와 스킬을 모두 수용해야 한다");
            Debug.Assert(W3Party.CombatHudRewardMaxEntries == 3,
                "[전투 HUD] 보상 레인은 최대 세 항목만 보여야 한다");
            Debug.Assert(Mathf.Approximately(W3Party.CombatHudRewardLifetime, 2.2f),
                "[전투 HUD] 보상 문구는 오래 남아 전장을 가리면 안 된다");
            Debug.Log("[CombatHudSelfCheck] PASS");
        }
    }
}
