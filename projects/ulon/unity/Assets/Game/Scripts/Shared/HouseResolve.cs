namespace Ulon.Shared
{
    public sealed class HouseRequest
    {
        public bool PlotExists = true;
        public bool Occupied;
        public bool ActorOwnsHouse;
        public float Distance;
        public float Range = HousingPlot.InteractRange;
        public bool Ghost;
        public int Gold;
        public string ActorCharacterId = "";
        public string ActorAccountId = "";
        public string OwnerCharacterId = "";
        public string OwnerAccountId = "";
        public bool HasBackpackItem;
        public bool ChestHasItem;
        public bool PublicHouse;
        public bool VendorSlotFull;
        public bool VendorHasItem;
        public int VendorPrice;
    }

    public static class HouseResolve
    {
        public static bool IsOwner(HouseRequest req)
        {
            if (req == null || string.IsNullOrEmpty(req.ActorCharacterId) || string.IsNullOrEmpty(req.OwnerCharacterId))
                return false;
            return req.ActorCharacterId == req.OwnerCharacterId;
        }

        public static AttackResult Claim(HouseRequest req)
        {
            if (req == null)
                return Fail("bad_request");
            if (!req.PlotExists)
                return Fail("no_plot");
            if (req.Ghost)
                return Fail("ghost");
            if (req.Distance > req.Range)
                return Fail("range");
            if (req.Occupied)
                return Fail("occupied");
            if (req.ActorOwnsHouse)
                return Fail("already_owns");
            if (string.IsNullOrEmpty(req.ActorCharacterId))
                return Fail("no_character");
            if (req.Gold < HousingPlot.ClaimGold)
                return Fail("gold");
            return new AttackResult { Applied = true, Hit = true, Damage = HousingPlot.ClaimGold };
        }

        public static AttackResult Lockdown(HouseRequest req)
        {
            if (req == null)
                return Fail("bad_request");
            if (!req.PlotExists)
                return Fail("no_plot");
            if (req.Ghost)
                return Fail("ghost");
            if (req.Distance > req.Range)
                return Fail("range");
            if (!req.Occupied)
                return Fail("unclaimed");
            if (!IsOwner(req))
                return Fail("not_owner");
            if (!req.HasBackpackItem)
                return Fail("empty_bag");
            return new AttackResult { Applied = true, Hit = true };
        }

        public static AttackResult SecureTake(HouseRequest req)
        {
            if (req == null)
                return Fail("bad_request");
            if (!req.PlotExists)
                return Fail("no_plot");
            if (req.Ghost)
                return Fail("ghost");
            if (req.Distance > req.Range)
                return Fail("range");
            if (!req.Occupied)
                return Fail("unclaimed");
            if (!IsOwner(req))
                return Fail("not_owner");
            if (!req.ChestHasItem)
                return Fail("empty_chest");
            return new AttackResult { Applied = true, Hit = true };
        }

        public static AttackResult ListVendor(HouseRequest req)
        {
            if (req == null)
                return Fail("bad_request");
            if (!req.PlotExists)
                return Fail("no_plot");
            if (req.Ghost)
                return Fail("ghost");
            if (req.Distance > req.Range)
                return Fail("range");
            if (!req.Occupied)
                return Fail("unclaimed");
            if (!IsOwner(req))
                return Fail("not_owner");
            if (!req.HasBackpackItem)
                return Fail("empty_bag");
            if (req.VendorSlotFull)
                return Fail("vendor_full");
            if (req.VendorPrice <= 0)
                return Fail("no_price");
            return new AttackResult { Applied = true, Hit = true, Damage = req.VendorPrice };
        }

        public static AttackResult BuyVendor(HouseRequest req)
        {
            if (req == null)
                return Fail("bad_request");
            if (!req.PlotExists)
                return Fail("no_plot");
            if (req.Ghost)
                return Fail("ghost");
            if (req.Distance > req.Range)
                return Fail("range");
            if (!req.Occupied)
                return Fail("unclaimed");
            if (!req.PublicHouse)
                return Fail("private");
            if (IsOwner(req))
                return Fail("owner");
            if (!req.VendorHasItem)
                return Fail("empty_vendor");
            if (req.VendorPrice <= 0)
                return Fail("no_price");
            if (req.Gold < req.VendorPrice)
                return Fail("gold");
            return new AttackResult { Applied = true, Hit = true, Damage = req.VendorPrice };
        }

        static AttackResult Fail(string reason) => new AttackResult { FailReason = reason };
    }
}
