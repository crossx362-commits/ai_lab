namespace Ulon.Shared
{
    /// <summary>
    /// 기획 §18.6 Interruption + 원작 UO: 시전 중 이동하면 주문이 끊긴다.
    /// 피격 중단은 Combat 경로. 여기는 이동만.
    /// 출처: ServUO/RunUO Spell.OnCasterMoving — 이동 시 Disturb.
    /// https://www.servuo.dev/threads/disturbing-a-spell.12340/
    /// </summary>
    public static class CastMove
    {
        /// <summary>네거티브 컨트롤 — true면 이동해도 시전이 안 끊긴다(옛 동작).</summary>
        public static bool NcOpen;

        public static bool BreaksOnMove => !NcOpen;
    }
}
