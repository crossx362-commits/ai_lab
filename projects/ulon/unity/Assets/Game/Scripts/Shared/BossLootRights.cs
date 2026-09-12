namespace Ulon.Shared
{
    /// <summary>
    /// 기획 §18.11 — 보스/정예 시체는 기여자·파티 우선권 후 공개.
    /// 창 길이는 플레이어 시체와 같이 <see cref="Ulon.Server.CorpseNode.DefaultExclusiveSeconds"/>.
    /// 수치는 기획서에 없어 원작 loot rights ≈2분(uo.com wiki)을 따른다. 원작 합격은 보강 전 부여하지 않는다.
    /// </summary>
    public static class BossLootRights
    {
        /// <summary>네거티브 컨트롤 — true면 창 중에도 누구나 룻한다.</summary>
        public static bool NcOpen;

        public static bool ExclusiveFor(string mobId)
        {
            if (NcOpen)
                return false;
            return MobCatalog.IsBoss(mobId);
        }
    }
}
