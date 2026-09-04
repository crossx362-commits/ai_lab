namespace Ulon.Shared
{
    public static class Dungeon3
    {
        public const string Id = "dungeon3";
        public const string RootObject = "Dungeon3";
        public const string EntranceObject = "Dungeon3Entrance";
        public const string ExitObject = "Dungeon3Exit";
        public const string InteriorObject = "Dungeon3Interior";
        public const string MobObject = "DungeonRaider";
        public const string BossObject = "IronTyrant";
        // 남서. 가드존(반경 16) 밖이고 던전1 입구에서 15.9f, 던전2에서 33f,
        // 하우징 부지·문게이트·마구간 등 랜드마크에서 전부 6f 이상 떨어져 있다.
        public const float EntranceX = -14.0f;
        public const float EntranceZ = -18.0f;
        // 던전1이 (+80,+80), 던전2가 (-80,+80)을 쓴다. 남은 사분면.
        public const float InteriorX = 80f;
        public const float InteriorZ = -80f;
        public const float ExitX = 78.4f;
        public const float ExitZ = -78.2f;
        public const float MobX = 82.2f;
        public const float MobZ = -81.6f;
        public const float BossX = 83.8f;
        public const float BossZ = -83.2f;
        public const float LeaveX = -12.5f;
        public const float LeaveZ = -18.0f;
    }
}
