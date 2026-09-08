namespace Ulon.Shared
{
    public static class HousingPlot
    {
        public const string Id = "plot1";
        public const string RootObject = "HousingPlot";
        public const string StationObject = "HousePlotStation";
        public const string HouseObject = "House";
        public const string ChestObject = "HouseChest";
        public const string VendorObject = "HouseVendor";
        public const int VendorSlot = 100;
        public const float X = -20.4f * WorldScale.Kit;   // 모듈 좌표 × 킷 배율(랩 B)
        public const float Z = -16.8f * WorldScale.Kit;
        public const int ClaimGold = 25;
        public const float InteractRange = 2.4f;
    }
}
