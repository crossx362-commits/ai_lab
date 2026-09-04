namespace Ulon.Shared
{
    public sealed class StableRequest
    {
        public float Distance;
        public float Range = StableYard.InteractRange;
        public bool Ghost;
        public bool HasFollower;
        public bool HasStabled;
        public int Gold;
        public int GoldCost = StableYard.GoldCost;
        public int UsedSlots;
        public int ControlSlots = 1;
        public int FollowerCap = TameResolve.FollowerCap;
        public bool HasStable = true;
    }

    public static class StableResolve
    {
        public static AttackResult Park(StableRequest req)
        {
            if (req == null)
                return Fail("bad_request");
            if (!req.HasStable)
                return Fail("no_stable");
            if (req.Ghost)
                return Fail("ghost");
            if (req.Distance > req.Range)
                return Fail("range");
            if (!req.HasFollower)
                return Fail("no_pet");
            if (req.HasStabled)
                return Fail("already_stabled");
            if (req.Gold < req.GoldCost)
                return Fail("gold");
            return new AttackResult { Applied = true, Hit = true, Damage = req.GoldCost };
        }

        public static AttackResult Claim(StableRequest req)
        {
            if (req == null)
                return Fail("bad_request");
            if (!req.HasStable)
                return Fail("no_stable");
            if (req.Ghost)
                return Fail("ghost");
            if (req.Distance > req.Range)
                return Fail("range");
            if (!req.HasStabled)
                return Fail("empty");
            int slots = req.ControlSlots < 1 ? 1 : req.ControlSlots;
            if (req.UsedSlots + slots > req.FollowerCap)
                return Fail("no_slot");
            return new AttackResult { Applied = true, Hit = true };
        }

        static AttackResult Fail(string reason) => new AttackResult { FailReason = reason };
    }
}
