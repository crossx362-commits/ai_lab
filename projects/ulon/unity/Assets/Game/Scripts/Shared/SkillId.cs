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
        Cooking,
        Fencing,
        Mace,
        Alchemy,
        Tracking,
        Musicianship,
        Peacemaking,
        Provocation,
        Hiding,
        Stealth,
        Lockpicking,
        AnimalLore,
        Veterinary,
        AnimalTaming,
        Inscription,
        Poisoning,
        DetectHidden,
        Camping,
        Stealing,
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
            "채광", "벌목", "대장장이", "목공", "재봉", "낚시", "요리", "창술", "둔기술", "연금술", "추적", "음악", "평화", "도발", "은신", "잠행", "자물쇠따기", "동물지식", "수의학", "조련", "각인", "독", "감지", "야영", "훔치기"
        };

        public static string KoreanOf(SkillId id)
        {
            int i = (int)id;
            return i >= 0 && i < Korean.Length ? Korean[i] : id.ToString();
        }
    }

    public static class SkillTitles
    {
        static readonly string[] Jobs =
        {
            "검사", "궁수", "전술가", "방패수", "해부학자", "치료사",
            "마법사", "평가사", "명상가", "저항사",
            "광부", "벌목꾼", "대장장이", "목수", "재봉사", "어부", "요리사", "창수", "둔기수", "연금술사", "추적자", "음악가", "평화사", "도발사", "은신자", "잠행자", "자물쇠공", "동물학자", "수의사", "조련사", "각인사", "독살자", "탐지자", "야영꾼", "도둑"
        };

        public static string JobOf(SkillId id)
        {
            int i = (int)id;
            return i >= 0 && i < Jobs.Length ? Jobs[i] : "";
        }

        public static string RankOf(float value)
        {
            if (value >= 99.999f) return "그랜드마스터";
            if (value >= 90f) return "대가";
            if (value >= 80f) return "달인";
            if (value >= 70f) return "전문가";
            if (value >= 60f) return "숙련";
            if (value >= 50f) return "견습";
            if (value >= 40f) return "수습";
            if (value >= 30f) return "초심자";
            return "";
        }

        public static SkillId Highest(SkillSet skills)
        {
            int best = 0;
            float bestV = -1f;
            for (int i = 0; i < (int)SkillId.Count; i++)
            {
                float v = skills.Get((SkillId)i);
                if (v > bestV + 0.0001f)
                {
                    bestV = v;
                    best = i;
                }
            }
            return (SkillId)best;
        }

        public static string Of(SkillSet skills)
        {
            if (skills == null)
                return "";
            SkillId id = Highest(skills);
            float v = skills.Get(id);
            if (v <= 0.0001f)
                return "";
            string job = JobOf(id);
            string rank = RankOf(v);
            return rank.Length == 0 ? job : rank + " " + job;
        }
    }

    public enum SpellId
    {
        Ember = 0,
        Mend = 1,
        Bolt = 2,
        Cleanse = 3,
        Ward = 4,
        Bind = 5,
        Weaken = 6,
        Spark = 7,
        Restore = 8,
        Blink = 9,
        Bless = 10,
        Count
    }

    public static class SpellNames
    {
        static readonly string[] Korean = { "불씨", "봉합", "벼락", "정화", "수호", "속박", "약화", "섬광", "회복", "도약", "축복" };

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
                case SkillId.Fencing:
                case SkillId.Parrying:
                case SkillId.Healing:
                case SkillId.Veterinary:
                case SkillId.AnimalTaming:
                case SkillId.Tailoring:
                case SkillId.Fishing:
                case SkillId.Cooking:
                case SkillId.Tracking:
                case SkillId.Musicianship:
                case SkillId.Peacemaking:
                case SkillId.Provocation:
                case SkillId.Hiding:
                case SkillId.Stealth:
                case SkillId.Lockpicking:
                case SkillId.Poisoning:
                case SkillId.DetectHidden:
                case SkillId.Camping:
                case SkillId.Stealing:
                    return StatId.Dex;
                case SkillId.Magery:
                case SkillId.EvaluateIntelligence:
                case SkillId.Meditation:
                case SkillId.MagicResist:
                case SkillId.Alchemy:
                case SkillId.AnimalLore:
                case SkillId.Inscription:
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
