using System;
using System.Collections.Generic;

namespace AshesToStars
{
    /// <summary>
    /// 드랍 장비 랜덤 옵션(§11). 등급별 1~4개, 전설만 4.
    /// 제작품은 0 — 드랍은 운, 제작은 계획. QA_NO면 옛 0.
    /// 전투 수치는 이 칸 아님. W3Party는 안 읽는다.
    /// </summary>
    public static class GearOpt
    {
        public const string EnvShow = "QA_GEAR_OPT";
        public const string EnvNo = "QA_NO_GEAR_OPT";
        public const int LegendaryCount = 4;
        public const int HeroicCount = 3;

        public static readonly string[] Names =
        {
            "생명", "수호", "강인", "견고",
        };

        static bool _qaSeeded;
        static string _lastLine = "";

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool ShowQa
        {
            get
            {
                if (Blocked) return false;
                string raw = Environment.GetEnvironmentVariable(EnvShow);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static string LastLine => _lastLine ?? "";

        public static int CountOf(GearGrade grade)
        {
            if (Blocked) return 0;
            switch (grade)
            {
                case GearGrade.Uncommon: return 2;
                case GearGrade.Rare: return 3;
                case GearGrade.Heroic: return HeroicCount;
                case GearGrade.Legendary: return LegendaryCount;
                default: return 1;
            }
        }

        public static int CountOf(GearItem gear) =>
            gear == null || gear.Affixes == null ? 0 : gear.Affixes.Length;

        public static string NameOf(int id) =>
            id >= 0 && id < Names.Length ? Names[id] : "";

        public static string Format(GearItem gear)
        {
            int n = CountOf(gear);
            if (n <= 0) return "";
            var parts = new List<string>(n);
            for (int i = 0; i < gear.Affixes.Length; i++)
            {
                string name = NameOf(gear.Affixes[i]);
                if (!string.IsNullOrEmpty(name)) parts.Add(name);
            }
            if (parts.Count == 0) return "";
            return $"옵션 {n} · " + string.Join(" · ", parts) + "(§11)";
        }

        public static string Line() =>
            Blocked ? "드랍 옵션 없음" : "드랍 옵션 1~4 · 전설만 4개(§11)";

        /// <summary>드랍만. 제작품은 호출하지 않는다. 막히면 칸을 비운다.</summary>
        public static void Apply(GearItem gear, ref Rng rng)
        {
            _lastLine = "";
            if (gear == null) return;
            if (Blocked)
            {
                gear.Affixes = Array.Empty<int>();
                return;
            }
            int n = CountOf(gear.Grade);
            if (n <= 0)
            {
                gear.Affixes = Array.Empty<int>();
                return;
            }
            if (n > Names.Length) n = Names.Length;
            var pool = new List<int>(Names.Length);
            for (int i = 0; i < Names.Length; i++) pool.Add(i);
            rng.Shuffle(pool);
            var pick = new int[n];
            for (int i = 0; i < n; i++) pick[i] = pool[i];
            gear.Affixes = pick;
            _lastLine = Format(gear);
        }

        public static void Apply(GearItem gear)
        {
            uint seed = 1u;
            if (gear != null && !string.IsNullOrEmpty(gear.Id))
                seed = (uint)gear.Id.GetHashCode();
            var rng = new Rng(seed);
            Apply(gear, ref rng);
        }

        /// <summary>시각 QA. QA_GEAR_OPT=1이면 전설 흉갑에 옵션 4.</summary>
        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            var all = Equipment.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Grade == GearGrade.Legendary
                    && all[i].RecipeId == Equipment.LeatherArmorRecipe
                    && CountOf(all[i]) == LegendaryCount)
                {
                    _lastLine = Format(all[i]);
                    return;
                }
            }
            var gear = Equipment.TryGrantDrop(Equipment.LeatherArmorRecipe, GearGrade.Legendary);
            if (gear == null) return;
            gear.Affixes = new[] { 0, 1, 2, 3 };
            Equipment.Flush();
            var roster = LifeSystem.GetCharacters();
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i] == null || roster[i].IsDeleted) continue;
                if (Equipment.TryEquip(roster[i], gear.Id)) break;
            }
            _lastLine = Format(gear);
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
            _lastLine = "";
        }
    }
}
