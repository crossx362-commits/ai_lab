namespace Ulon.Shared
{
    public static class DuelRules
    {
        public const float InviteRange = 4f;
        public const float AcceptRange = 6f;
    }

    public sealed class DuelRequest
    {
        public bool Ghost;
        public bool HasTarget = true;
        public bool SameAsSelf;
        public bool TargetEnemy;
        public bool TargetAvatar = true;
        public bool AlreadyDueling;
        public bool TargetBusy;
        public bool HasPending;
        public bool PendingIsMe;
        public bool InDuel;
        public float Distance;
        public float Range = DuelRules.InviteRange;
    }

    public static class DuelResolve
    {
        public static AttackResult Invite(DuelRequest req)
        {
            if (req == null)
                return Fail("bad_request");
            if (!req.HasTarget || req.SameAsSelf)
                return Fail("no_target");
            if (req.Ghost)
                return Fail("ghost");
            if (req.TargetEnemy)
                return Fail("enemy");
            if (!req.TargetAvatar)
                return Fail("not_avatar");
            if (req.AlreadyDueling || req.TargetBusy)
                return Fail("busy");
            if (req.Distance > req.Range)
                return Fail("range");
            return new AttackResult { Applied = true, Hit = true };
        }

        public static AttackResult Accept(DuelRequest req)
        {
            if (req == null)
                return Fail("bad_request");
            if (!req.HasPending || !req.PendingIsMe)
                return Fail("no_invite");
            if (req.Ghost)
                return Fail("ghost");
            if (req.AlreadyDueling)
                return Fail("busy");
            if (req.Distance > req.Range)
                return Fail("range");
            return new AttackResult { Applied = true, Hit = true };
        }

        public static AttackResult End(DuelRequest req)
        {
            if (req == null)
                return Fail("bad_request");
            if (!req.InDuel)
                return Fail("no_duel");
            return new AttackResult { Applied = true, Hit = true };
        }

        public static bool FieldDuel(bool attackerAvatar, bool targetAvatar, bool mutuallyDueling, float ax, float az, float tx, float tz)
        {
            if (!attackerAvatar || !targetAvatar || !mutuallyDueling)
                return false;
            if (GuardZone.Contains(ax, az) || GuardZone.Contains(tx, tz))
                return false;
            return true;
        }

        static AttackResult Fail(string reason) => new AttackResult { FailReason = reason };
    }
}
