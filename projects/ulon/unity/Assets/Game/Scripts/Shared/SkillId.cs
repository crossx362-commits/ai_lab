namespace Ulon.Shared
{
    public enum SkillId
    {
        Swordsmanship = 0,
        Archery,
        Tactics,
        Parrying,
        Anatomy,
        Healing,
        Magery,
        EvaluateIntelligence,
        Meditation,
        MagicResist,
        Mining,
        Lumberjacking,
        Blacksmithing,
        Carpentry,
        Tailoring,
        Fishing,
        Count
    }

    public enum SkillLock
    {
        Up = 0,
        Down = 1,
        Locked = 2
    }

    public static class SkillNames
    {
        static readonly string[] Korean =
        {
            "검술", "궁술", "전술", "방패술", "해부학", "치유",
            "마법", "지능 평가", "명상", "마법 저항",
            "채광", "벌목", "대장장이", "목공", "재봉", "낚시"
        };

        public static string KoreanOf(SkillId id)
        {
            int i = (int)id;
            return i >= 0 && i < Korean.Length ? Korean[i] : id.ToString();
        }
    }

    public enum SpellId
    {
        Ember = 0,
        Mend = 1,
        Count
    }

    public static class SpellNames
    {
        static readonly string[] Korean = { "불씨", "봉합" };

        public static string KoreanOf(SpellId id)
        {
            int i = (int)id;
            return i >= 0 && i < Korean.Length ? Korean[i] : id.ToString();
        }
    }

    public static class SkillLockMarks
    {
        public static SkillLock Next(SkillLock state)
        {
            if (state == SkillLock.Up)
                return SkillLock.Down;
            if (state == SkillLock.Down)
                return SkillLock.Locked;
            return SkillLock.Up;
        }

        public static string Glyph(SkillLock state)
        {
            if (state == SkillLock.Down)
                return "↓";
            if (state == SkillLock.Locked)
                return "x";
            return "↑";
        }
    }

    public enum StatId
    {
        Str = 0,
        Dex = 1,
        Int = 2
    }

    public sealed class StatSet
    {
        public const int IndividualCap = 100;
        public const int TotalCap = 225;
        public const int DefaultStr = 30;
        public const int DefaultDex = 25;
        public const int DefaultInt = 25;

        int str = DefaultStr;
        int dex = DefaultDex;
        int intel = DefaultInt;
        SkillLock strLock = SkillLock.Up;
        SkillLock dexLock = SkillLock.Up;
        SkillLock intLock = SkillLock.Up;

        public int Str => str;
        public int Dex => dex;
        public int Int => intel;
        public int Total => str + dex + intel;

        public int Get(StatId id)
        {
            if (id == StatId.Str) return str;
            if (id == StatId.Dex) return dex;
            return intel;
        }

        public SkillLock GetLock(StatId id)
        {
            if (id == StatId.Str) return strLock;
            if (id == StatId.Dex) return dexLock;
            return intLock;
        }

        public void CycleLock(StatId id) => SetLock(id, SkillLockMarks.Next(GetLock(id)));

        public void SetLock(StatId id, SkillLock state)
        {
            if (id == StatId.Str) strLock = state;
            else if (id == StatId.Dex) dexLock = state;
            else intLock = state;
        }

        public void ForceSet(int s, int d, int i) => ForceSet(s, d, i, strLock, dexLock, intLock);

        public void ForceSet(int s, int d, int i, SkillLock sl, SkillLock dl, SkillLock il)
        {
            str = Clamp(s);
            dex = Clamp(d);
            intel = Clamp(i);
            strLock = sl;
            dexLock = dl;
            intLock = il;
        }

        public bool TryRaise(StatId id)
        {
            if (GetLock(id) != SkillLock.Up) return false;
            if (Get(id) >= IndividualCap) return false;
            if (Total >= TotalCap && !DrainDown(id)) return false;
            if (id == StatId.Str) str++;
            else if (id == StatId.Dex) dex++;
            else intel++;
            return true;
        }

        bool DrainDown(StatId except)
        {
            StatId[] order = { StatId.Str, StatId.Dex, StatId.Int };
            for (int i = 0; i < order.Length; i++)
            {
                StatId id = order[i];
                if (id == except || GetLock(id) != SkillLock.Down || Get(id) <= 1)
                    continue;
                if (id == StatId.Str) str--;
                else if (id == StatId.Dex) dex--;
                else intel--;
                return true;
            }
            return false;
        }

        public static StatId PrimaryOf(SkillId skill)
        {
            switch (skill)
            {
                case SkillId.Tactics:
                    return StatId.Str;
                case SkillId.Anatomy:
                    return StatId.Int;
                case SkillId.Archery:
                case SkillId.Parrying:
                case SkillId.Healing:
                case SkillId.Tailoring:
                case SkillId.Fishing:
                    return StatId.Dex;
                case SkillId.Magery:
                case SkillId.EvaluateIntelligence:
                case SkillId.Meditation:
                case SkillId.MagicResist:
                    return StatId.Int;
                default:
                    return StatId.Str;
            }
        }

        public static int MaxHpOf(int strength) => 20 + strength;

        public static int MaxManaOf(int intelligence) => 10 + intelligence;

        static int Clamp(int v)
        {
            if (v < 1) return 1;
            if (v > IndividualCap) return IndividualCap;
            return v;
        }
    }
}
