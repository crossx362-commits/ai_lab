namespace Ulon.Shared
{
    public struct MobDefinition
    {
        public string Id;
        public string DisplayName;
        public float MaxHp;
        public float Height;
    }

    public static class MobCatalog
    {
        public const string Skeleton = "skeleton";
        public const string Bandit = "bandit";
        public const int KindCount = 2;

        public static bool TryGet(string id, out MobDefinition definition)
        {
            if (id == Skeleton)
            {
                definition = new MobDefinition
                {
                    Id = Skeleton,
                    DisplayName = "스켈레톤",
                    MaxHp = 30f,
                    Height = 1.55f
                };
                return true;
            }
            if (id == Bandit)
            {
                definition = new MobDefinition
                {
                    Id = Bandit,
                    DisplayName = "도적",
                    MaxHp = 45f,
                    Height = 1.75f
                };
                return true;
            }
            definition = default(MobDefinition);
            return false;
        }

        public static float MaxHpOf(string id)
        {
            return TryGet(id, out MobDefinition definition) ? definition.MaxHp : 30f;
        }

        public static string DisplayNameOf(string id)
        {
            return TryGet(id, out MobDefinition definition) ? definition.DisplayName : "스켈레톤";
        }

        public static float HeightOf(string id)
        {
            return TryGet(id, out MobDefinition definition) ? definition.Height : 1.55f;
        }

        public static bool IsKnown(string id)
        {
            return TryGet(id, out _);
        }
    }
}
