namespace Ulon.Shared
{
    /// <summary>
    /// 기획 §18.9 — 양쪽 Offer+Gold 확인 후 둘 다 Accept해야 완료.
    /// 이미 정산된 세션을 다시 적용하면 duplicate.
    /// </summary>
    public sealed class TradeSettleRequest
    {
        public bool Settled;
        public bool AcceptA;
        public bool AcceptB;
        public int GoldA;
        public int GoldB;
        public int HaveGoldA;
        public int HaveGoldB;
        public string OfferA = "";
        public string OfferB = "";
        public bool HaveItemA = true;
        public bool HaveItemB = true;
        public bool CanCarryA = true;
        public bool CanCarryB = true;
        public bool GhostA;
        public bool GhostB;
    }

    public static class TradeResolve
    {
        /// <summary>네거티브 컨트롤 — true면 정산된 세션도 다시 적용한다.</summary>
        public static bool NcAllowDuplicate;

        public static AttackResult Settle(TradeSettleRequest req)
        {
            if (req == null)
                return Fail("no_trade");
            if (req.GhostA || req.GhostB)
                return Fail("ghost");
            if (req.GoldA < 0 || req.GoldB < 0)
                return Fail("gold");
            if (req.Settled && !NcAllowDuplicate)
                return Fail("duplicate");
            if (!req.AcceptA || !req.AcceptB)
                return Fail("waiting");
            if (req.GoldA > req.HaveGoldA || req.GoldB > req.HaveGoldB)
                return Fail("gold");
            if (!string.IsNullOrEmpty(req.OfferA) && !req.HaveItemA)
                return Fail("missing");
            if (!string.IsNullOrEmpty(req.OfferB) && !req.HaveItemB)
                return Fail("missing");
            if (!req.CanCarryA || !req.CanCarryB)
                return Fail("overweight");
            return new AttackResult { Applied = true, Hit = true };
        }

        static AttackResult Fail(string reason) => new AttackResult { FailReason = reason };
    }
}
