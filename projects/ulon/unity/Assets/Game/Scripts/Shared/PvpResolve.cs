namespace Ulon.Shared
{
    public static class PvpResolve
    {
        public const int MurdererThreshold = 5;

        public static bool OutdoorOpen(bool attackerAvatar, bool targetAvatar, bool attackerEnemy, bool targetEnemy, float ax, float az, float tx, float tz)
        {
            if (!attackerAvatar || !targetAvatar)
                return false;
            if (attackerEnemy || targetEnemy)
                return false;
            if (GuardZone.Contains(ax, az) || GuardZone.Contains(tx, tz))
                return false;
            return true;
        }

        public static bool ShouldFlagMurderer(int murderCount)
        {
            return murderCount >= MurdererThreshold;
        }
    }
}
