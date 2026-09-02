#if UNITY_EDITOR
using AshesToStars;

/// <summary>AUTODEV_TASK:T0020</summary>
public static class AutoDev_T0020_Acceptance
{
    public static void Run()
    {
        GameState.ResetAll();
        LowHpReturn.ResetForTest();
        AutomationSchedule.ResetForTest();
        GameFlow.SetReturnForTest(GameFlow.Field);

        var hpStarting = AutomationSchedule.EvaluateBattle(0.1f, LowHpReturn.Threshold);
        AutoDevAssert.Equal(AutomationSchedule.Directive.None, hpStarting,
            "HP 임계치에서는 기존 3초 귀환 유예가 먼저 시작되어야 한다.");
        var hpReturn = AutomationSchedule.EvaluateBattle(LowHpReturn.LeaveSeconds, LowHpReturn.Threshold);
        AutoDevAssert.Equal(AutomationSchedule.Directive.ReturnForLowHp, hpReturn,
            "HP 임계치에서 자동 귀환 지시가 시작되어야 한다.");

        LowHpReturn.ResetForTest();
        AutomationSchedule.ResetForTest();
        while (BagSlots.Used() < BagSlots.Cap)
            Equipment.AddUnequippedForTest(Equipment.LeatherArmorRecipe);

        var bagReturn = AutomationSchedule.EvaluateBattle(0.1f, 1f);
        AutoDevAssert.Equal(AutomationSchedule.Directive.ReturnForBagFull, bagReturn,
            "가방이 가득 찬 필드 사냥은 영지 귀환 지시를 내려야 한다.");
        AutoDevAssert.True(AutomationSchedule.PendingFieldResortie,
            "가방 귀환 뒤 필드 재출전이 예약되어야 한다.");

        GameState.ResetAll();
        AutoDevAssert.True(AutomationSchedule.TryResumeFieldSortie(),
            "영지에서 가방 칸이 생기면 예약된 필드 재출전이 시작되어야 한다.");
        AutoDevAssert.Equal(GameFlow.Field, GameFlow.ReturnTo,
            "재출전은 필드 전투의 귀환 목적지를 설정해야 한다.");
        AutoDevAssert.False(AutomationSchedule.PendingFieldResortie,
            "재출전을 시작한 뒤 예약이 남아 있으면 안 된다.");

        AutomationSchedule.ResetForTest();
        LowHpReturn.ResetForTest();
        GameState.ResetAll();
    }
}
#endif
