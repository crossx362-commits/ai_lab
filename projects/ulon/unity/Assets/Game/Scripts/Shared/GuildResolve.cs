namespace Ulon.Shared
{
    public static class GuildRules
    {
        public const int NameMin = 1;
        public const int NameMax = 12;
        public const int GoldCost = 25;
        public const float InviteRange = 4f;
        public const float AcceptRange = 6f;
    }

    public sealed class GuildRequest
    {
        public string Name = "";
        public bool Ghost;
        public bool AlreadyInGuild;
        public int Gold;
        public int GoldCost = GuildRules.GoldCost;
        public float Distance;
        public float Range = GuildRules.InviteRange;
        public bool HasTarget = true;
        public bool TargetEnemy;
        public bool TargetInGuild;
        public bool IsLeader = true;
        public bool HasGuild;
        public bool HasPending;
        public bool PendingIsMe;
        public bool SameAsSelf;
    }

    public static class GuildResolve
    {
        public static string NormalizeName(string name)
        {
            return string.IsNullOrWhiteSpace(name) ? "" : name.Trim();
        }

        public static bool ValidName(string name)
        {
            int n = name == null ? 0 : name.Length;
            return n >= GuildRules.NameMin && n <= GuildRules.NameMax;
        }

        public static AttackResult Create(GuildRequest req)
        {
            if (req == null)
                return Fail("bad_request");
            if (req.Ghost)
                return Fail("ghost");
            if (req.AlreadyInGuild)
                return Fail("already");
            string name = NormalizeName(req.Name);
            if (!ValidName(name))
                return Fail("name");
            if (req.Gold < req.GoldCost)
                return Fail("gold");
            return new AttackResult { Applied = true, Hit = true, Damage = req.GoldCost };
        }

        public static AttackResult Invite(GuildRequest req)
        {
            if (req == null)
                return Fail("bad_request");
            if (!req.HasTarget || req.SameAsSelf)
                return Fail("no_target");
            if (req.Ghost)
                return Fail("ghost");
            if (!req.HasGuild)
                return Fail("no_guild");
            if (!req.IsLeader)
                return Fail("not_leader");
            if (req.TargetEnemy)
                return Fail("enemy");
            if (req.TargetInGuild)
                return Fail("already");
            if (req.Distance > req.Range)
                return Fail("range");
            return new AttackResult { Applied = true, Hit = true };
        }

        public static AttackResult Accept(GuildRequest req)
        {
            if (req == null)
                return Fail("bad_request");
            if (!req.HasGuild || !req.HasPending)
                return Fail("no_guild");
            if (!req.PendingIsMe)
                return Fail("no_invite");
            if (req.Ghost)
                return Fail("ghost");
            if (req.AlreadyInGuild)
                return Fail("already");
            if (req.Distance > req.Range)
                return Fail("range");
            return new AttackResult { Applied = true, Hit = true };
        }

        public static AttackResult Leave(GuildRequest req)
        {
            if (req == null)
                return Fail("bad_request");
            if (!req.HasGuild)
                return Fail("no_guild");
            return new AttackResult { Applied = true, Hit = true };
        }

        static AttackResult Fail(string reason) => new AttackResult { FailReason = reason };
    }

    public sealed class GuildWarRequest
    {
        public bool Ghost;
        public bool HasGuild;
        public bool IsLeader = true;
        public bool HasTargetGuild = true;
        public bool SameGuild;
        public bool AlreadyWar;
        public bool AtWar;
    }

    public static class GuildWarResolve
    {
        public static AttackResult Declare(GuildWarRequest req)
        {
            if (req == null)
                return Fail("bad_request");
            if (req.Ghost)
                return Fail("ghost");
            if (!req.HasGuild)
                return Fail("no_guild");
            if (!req.IsLeader)
                return Fail("not_leader");
            if (!req.HasTargetGuild)
                return Fail("no_target");
            if (req.SameGuild)
                return Fail("same_guild");
            if (req.AlreadyWar)
                return Fail("already");
            return new AttackResult { Applied = true, Hit = true };
        }

        public static AttackResult Peace(GuildWarRequest req)
        {
            if (req == null)
                return Fail("bad_request");
            if (req.Ghost)
                return Fail("ghost");
            if (!req.HasGuild)
                return Fail("no_guild");
            if (!req.IsLeader)
                return Fail("not_leader");
            if (!req.AtWar)
                return Fail("no_war");
            return new AttackResult { Applied = true, Hit = true };
        }

        public static bool FieldWar(bool attackerAvatar, bool targetAvatar, string aGuildId, string bGuildId, string aWarWith, string bWarWith, float ax, float az, float tx, float tz)
        {
            if (!attackerAvatar || !targetAvatar)
                return false;
            if (string.IsNullOrEmpty(aGuildId) || string.IsNullOrEmpty(bGuildId) || aGuildId == bGuildId)
                return false;
            if (aWarWith != bGuildId || bWarWith != aGuildId)
                return false;
            if (GuardZone.Contains(ax, az) || GuardZone.Contains(tx, tz))
                return false;
            return true;
        }

        static AttackResult Fail(string reason) => new AttackResult { FailReason = reason };
    }
}
