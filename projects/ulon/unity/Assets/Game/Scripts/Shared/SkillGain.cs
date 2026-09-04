namespace Ulon.Shared
{
    public static class SkillGain
    {
        public const float SliceStep = 0.1f;

        public static bool TryRaise(SkillSet skills, SkillId id, float actionDifficulty, out float before, out float after, StatSet stats = null)
        {
            before = skills.Get(id);
            after = before;
            if (skills.GetLock(id) != SkillLock.Up)
                return false;
            if (before >= SkillSet.IndividualCap)
                return false;
            if (before >= actionDifficulty + 20f)
                return false;

            float next = before + SliceStep;
            if (!skills.TrySet(id, next))
                return false;
            after = skills.Get(id);
            if (after > before && stats != null)
                stats.TryRaise(StatSet.PrimaryOf(id));
            return after > before;
        }
    }

    public static class ExceptionalCraft
    {
        public static bool Force;
        public static int Seed;
        public const int UsesBonus = 4;
        public const int DamageBonus = 1;
        public const string PersistPrefix = "EX:";

        public static bool Roll(float skill)
        {
            if (Force)
                return true;
            float chance = skill / 100f;
            if (chance <= 0f)
                return false;
            if (chance >= 1f)
                return true;
            float roll;
            if (Seed != 0)
                roll = (float)new System.Random(Seed).NextDouble();
            else
                roll = UnityEngine.Random.value;
            return roll < chance;
        }

        public static int MaxUsesOf(ItemRecord rec)
        {
            int max = ItemCatalog.MaxUsesOf(rec.TemplateId);
            if (rec.Exceptional && max > 0)
                max += UsesBonus;
            return max;
        }

        public static string PackMaker(string makerId, bool exceptional)
        {
            string id = makerId ?? "";
            if (!exceptional)
                return id;
            if (id.StartsWith(PersistPrefix))
                return id;
            return PersistPrefix + id;
        }

        public static void UnpackMaker(string packed, out string makerId, out bool exceptional)
        {
            packed = packed ?? "";
            if (packed.StartsWith(PersistPrefix))
            {
                exceptional = true;
                makerId = packed.Substring(PersistPrefix.Length);
                return;
            }
            exceptional = false;
            makerId = packed;
        }
    }
}
