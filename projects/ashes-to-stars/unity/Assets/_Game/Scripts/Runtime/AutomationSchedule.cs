namespace AshesToStars
{
    /// <summary>
    /// 필드 자동사냥의 조건부 지시를 한 곳에서 판정한다.
    /// 저체력은 기존 이탈 캐스트를 끝까지 기다리고, 가방 포화 귀환은 영지에서
    /// 빈 칸이 생길 때 같은 필드 사냥을 한 번 재개한다.
    /// </summary>
    public static class AutomationSchedule
    {
        public enum Directive { None, ReturnForLowHp, ReturnForBagFull }

        static bool _pendingFieldResortie;

        public static bool PendingFieldResortie => _pendingFieldResortie;

        public static Directive EvaluateBattle(float deltaTime, float lowestHpRatio)
        {
            bool watchLowHp = LowHpReturn.ShouldWatch(GameFlow.Kind, GameFlow.ReturnTo);
            if (LowHpReturn.Tick(deltaTime, lowestHpRatio, watchLowHp) == LowHpReturn.Phase.Left)
                return Directive.ReturnForLowHp;

            if (!BagFullReturn.ShouldReturn(GameFlow.Kind, GameFlow.ReturnTo))
                return Directive.None;

            _pendingFieldResortie = true;
            return Directive.ReturnForBagFull;
        }

        /// <summary>영지에서 가방 칸이 생긴 뒤, 포화로 끊긴 필드 사냥을 한 번 재개한다.</summary>
        public static bool TryResumeFieldSortie()
        {
            if (!_pendingFieldResortie || BagSlots.Free() <= 0) return false;
            _pendingFieldResortie = false;
            GameFlow.GoBattle(GameFlow.Field);
            return true;
        }

        public static void ResetForTest()
        {
            _pendingFieldResortie = false;
        }
    }
}
