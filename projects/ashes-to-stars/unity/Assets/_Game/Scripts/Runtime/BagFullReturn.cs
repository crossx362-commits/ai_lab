namespace AshesToStars
{
    /// <summary>필드 자동사냥은 가방이 꽉 차면 더 이상 드랍을 버리지 않고 영지로 돌아간다.</summary>
    public static class BagFullReturn
    {
        public static bool ShouldReturn(GameFlow.BattleKind kind, string returnTo) =>
            kind == GameFlow.BattleKind.잡몹웨이브
            && returnTo == GameFlow.Field
            && !BagSlots.Blocked
            && BagSlots.Free() == 0;
    }
}
