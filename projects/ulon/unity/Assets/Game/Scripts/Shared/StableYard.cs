namespace Ulon.Shared
{
    public static class StableYard
    {
        public const string Object = "Stable";
        public const string PolesObject = "StablePoles";
        public const string DisplayName = "마구간지기";
        public const float X = 8.8f;
        public const float Z = -8.4f;
        public const float InteractRange = 2.6f;
        public const int GoldCost = 2;
    }

    [System.Serializable]
    public sealed class StableSnapshot
    {
        public string CharacterId = "";
        public string PetId = "";
        public int ControlSlots = 1;
        public string DisplayName = "";
    }
}
