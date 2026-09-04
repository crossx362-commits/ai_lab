namespace Ulon.Shared
{
    /// <summary>
    /// Fame/Karma/Notoriety reputation label beside the character name.
    /// Separate from SkillTitles job/rank titles (GAME_DESIGN 18.7).
    /// </summary>
    public static class ReputationTitles
    {
        public const int FameFamous = 100;

        public static string Of(int notoriety, int fame, int karma = 0)
        {
            if (notoriety >= NotorietyId.Murderer)
                return "살인자";
            if (notoriety == NotorietyId.Criminal)
                return "범죄자";
            if (fame >= FameFamous)
                return "유명인";
            // karma reserved for later bands; unused in title 1
            _ = karma;
            return "";
        }

        public static string Of(int notoriety, int fame)
        {
            return Of(notoriety, fame, 0);
        }
    }
}
