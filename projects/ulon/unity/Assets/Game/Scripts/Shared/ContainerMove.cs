namespace Ulon.Shared
{
    /// <summary>
    /// 가방·은행 한 칸 이동 원장(기획 §18.13 Drag &amp; Drop).
    /// HUD·서버·게이트가 같이 읽는다. 판정은 서버(<c>TryDepositOne</c>/<c>TryWithdrawOne</c>).
    /// </summary>
    public static class ContainerMove
    {
        /// <summary>네거티브 컨트롤 — 켜면 한 칸 입출금이 전부 거절된다.</summary>
        public static bool NcDenyAll;

        public const string Bag = "bag";
        public const string Bank = "bank";
        public const string Pouch = "pouch";
    }
}
