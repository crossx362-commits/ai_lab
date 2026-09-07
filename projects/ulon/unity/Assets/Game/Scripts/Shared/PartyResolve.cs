namespace Ulon.Shared
{
    /// <summary>
    /// **파티 사거리 원장** — 초대·수락 거리를 한 곳에 둔다.
    ///
    /// 전에는 `OfflineWorld.TryPartyInvite`에 `4f`, `TryPartyAccept`에 `6f`가 **날숫자로** 박혀 있었고,
    /// HUD는 아예 거리를 안 보고 씬 이름(`GameObject.Find("Companion")`)으로 대상을 골랐다.
    /// 그래서 「무엇을 초대할 수 있나」와 「서버가 무엇을 받아 주나」가 서로 다른 자였다 —
    /// **재는 자와 맞추는 자는 하나여야 한다.**
    ///
    /// 값의 근거: 같은 「가까이 서서 말을 건다」류인 결투·길드 초대와 **같은 수**를 쓴다
    /// (`DuelResolve.InviteRange` 4m / `AcceptRange` 6m, `GuildResolve`도 동일).
    /// 임의로 고른 반경이 아니라 이미 있는 상호작용 사거리에서 유도한 것이다.
    /// </summary>
    public static class PartyResolve
    {
        /// <summary>초대할 수 있는 거리 — 결투·길드 초대와 같다.</summary>
        public const float InviteRange = 4f;
        /// <summary>수락할 수 있는 거리 — 초대 뒤 조금 움직여도 받아 주도록 초대보다 넉넉하다(결투·길드와 같다).</summary>
        public const float AcceptRange = 6f;
    }
}
