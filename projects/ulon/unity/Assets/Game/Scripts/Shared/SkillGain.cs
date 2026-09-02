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
}
