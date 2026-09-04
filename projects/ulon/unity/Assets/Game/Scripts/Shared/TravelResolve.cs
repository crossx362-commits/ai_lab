namespace Ulon.Shared
{
    public sealed class TravelRequest
    {
        public float Distance;
        public float Range = TravelGate.InteractRange;
        public bool Ghost;
        public int Gold;
        public int GoldCost = TravelGate.GoldCost;
        public bool HasGate = true;
        public bool InCombat;
        public bool HasMark;
    }

    public static class TravelResolve
    {
        public static AttackResult Gate(TravelRequest req)
        {
            if (req == null)
                return Fail("bad_request");
            if (!req.HasGate)
                return Fail("no_gate");
            if (req.Ghost)
                return Fail("ghost");
            if (req.Distance > req.Range)
                return Fail("range");
            if (req.Gold < req.GoldCost)
                return Fail("gold");
            return new AttackResult { Applied = true, Hit = true, Damage = req.GoldCost };
        }

        public static AttackResult Mark(TravelRequest req)
        {
            if (req == null)
                return Fail("bad_request");
            if (req.Ghost)
                return Fail("ghost");
            if (req.InCombat)
                return Fail("combat");
            if (req.Gold < req.GoldCost)
                return Fail("gold");
            return new AttackResult { Applied = true, Hit = true, Damage = req.GoldCost };
        }

        public static AttackResult Recall(TravelRequest req)
        {
            if (req == null)
                return Fail("bad_request");
            if (req.Ghost)
                return Fail("ghost");
            if (req.InCombat)
                return Fail("combat");
            if (!req.HasMark)
                return Fail("no_mark");
            return new AttackResult { Applied = true, Hit = true };
        }

        static AttackResult Fail(string reason) => new AttackResult { FailReason = reason };
    }
}
