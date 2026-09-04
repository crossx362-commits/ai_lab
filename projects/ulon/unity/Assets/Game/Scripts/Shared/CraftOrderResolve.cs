namespace Ulon.Shared
{
    public static class CraftOrderRules
    {
        public const string DefaultItem = ItemCatalog.IronSword;
        public const int Amount = 1;
        public const int GoldReward = 10;
        public const float InteractRange = 2.4f;
        public const float SkillDifficulty = 5f;
    }

    public sealed class CraftOrderRequest
    {
        public bool Ghost;
        public bool HasStation;
        public float Distance;
        public float Range = CraftOrderRules.InteractRange;
        public string ActiveOrder = "";
        public string OfferItem = CraftOrderRules.DefaultItem;
        public bool HasMatchingCrafted;
    }

    public static class CraftOrderResolve
    {
        public static AttackResult Accept(CraftOrderRequest req)
        {
            if (req == null)
                return Fail("bad_request");
            if (req.Ghost)
                return Fail("ghost");
            if (!req.HasStation)
                return Fail("no_station");
            if (req.Distance > req.Range)
                return Fail("range");
            if (!string.IsNullOrEmpty(req.ActiveOrder))
                return Fail("already");
            if (string.IsNullOrEmpty(req.OfferItem))
                return Fail("no_offer");
            return new AttackResult { Applied = true, Hit = true };
        }

        public static AttackResult TurnIn(CraftOrderRequest req)
        {
            if (req == null)
                return Fail("bad_request");
            if (req.Ghost)
                return Fail("ghost");
            if (!req.HasStation)
                return Fail("no_station");
            if (req.Distance > req.Range)
                return Fail("range");
            if (string.IsNullOrEmpty(req.ActiveOrder))
                return Fail("no_order");
            if (!req.HasMatchingCrafted)
                return Fail("wrong_item");
            return new AttackResult { Applied = true, Hit = true, Damage = CraftOrderRules.GoldReward };
        }

        static AttackResult Fail(string reason) => new AttackResult { FailReason = reason };
    }
}
