using System;
using System.Collections.Generic;

namespace AshesToStars
{
    /// <summary>
    /// 드랍 장비 랜덤 옵션(§11). 등급별 1~4개, 전설만 4.
    /// 제작품은 0 — 드랍은 운, 제작은 계획. QA_NO면 옛 0.
    /// 경매 등록·복원은 Pack/Parse를 읽는다.
    /// 체력은 강화와 같은 EnhanceHpPerLevel을 옵션 1개당 곱한다. 새 수치 없음.
    /// </summary>
    public static class GearOpt
    {
        public const string EnvShow = "QA_GEAR_OPT";
        public const string EnvShowList = "QA_GEAR_LIST";
        public const string EnvNo = "QA_NO_GEAR_OPT";
        public const int LegendaryCount = 4;
        public const int HeroicCount = 3;
        public const long ListQaPrice = 10_000;

        public static readonly string[] Names =
        {
            "생명", "수호", "강인", "견고",
        };

        static bool _qaSeeded;
        static bool _qaListSeeded;
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

        public static bool ShowListQa
        {
            get
            {
                if (Blocked) return false;
                string raw = Environment.GetEnvironmentVariable(EnvShowList);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static string LastLine => _lastLine ?? "";

        /// <summary>강화 +1과 같은 칸. 옵션 전용 숫자를 새로 만들지 않는다.</summary>
        public static float HpPerAffix => Equipment.EnhanceHpPerLevel;

        /// <summary>옵션 n개 → ×(1+n×2%). 막히거나 0개면 1.</summary>
        public static float HpMul(GearItem gear)
        {
            if (gear == null || Blocked) return 1f;
            int n = CountOf(gear);
            if (n <= 0) return 1f;
            return 1f + n * HpPerAffix;
        }

        public static string CombatLine(GearItem gear)
        {
            if (gear == null || Blocked || CountOf(gear) <= 0)
                return "옵션 체력 없음";
            return $"옵션 체력 ×{HpMul(gear):0.00}(§11)";
        }

        public static string CombatLine() =>
            Blocked ? "옵션 체력 없음" : "옵션이 체력을 올린다(§11)";

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

        public static string ListLine() =>
            Blocked ? "경매는 등급·옵션을 안 싣는다" : "경매도 옵션을 싣는다(§11)";

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

        /// <summary>경매 Key. QA_NO·옛 칸은 recipe|enhance만.</summary>
        public static string Pack(GearItem gear)
        {
            if (gear == null) return "";
            string rec = gear.RecipeId ?? "";
            if (Blocked) return rec + "|" + gear.Enhance;
            string aff = "";
            if (gear.Affixes != null && gear.Affixes.Length > 0)
            {
                var parts = new string[gear.Affixes.Length];
                for (int i = 0; i < gear.Affixes.Length; i++)
                    parts[i] = gear.Affixes[i].ToString();
                aff = string.Join(",", parts);
            }
            return rec + "|" + gear.Enhance + "|" + gear.Grade + "|" + aff;
        }

        /// <summary>옛 recipe|enhance는 일반·옵션 0. 막혀도 같은 옛 칸.</summary>
        public static bool Parse(string packed, out string recipeId, out int enhance,
            out GearGrade grade, out int[] affixes)
        {
            recipeId = "";
            enhance = 0;
            grade = GearGrade.Common;
            affixes = Array.Empty<int>();
            if (string.IsNullOrEmpty(packed)) return false;
            var parts = packed.Split('|');
            recipeId = parts[0] ?? "";
            if (string.IsNullOrEmpty(recipeId)) return false;
            if (parts.Length > 1) int.TryParse(parts[1], out enhance);
            if (Blocked || parts.Length < 3) return true;
            if (!Enum.TryParse(parts[2], out grade)) grade = GearGrade.Common;
            if (parts.Length < 4 || string.IsNullOrEmpty(parts[3])) return true;
            var ids = parts[3].Split(',');
            var list = new List<int>(ids.Length);
            for (int i = 0; i < ids.Length; i++)
            {
                if (int.TryParse(ids[i], out int id) && id >= 0)
                    list.Add(id);
            }
            if (list.Count > 0) affixes = list.ToArray();
            return true;
        }

        /// <summary>시각 QA. QA_GEAR_OPT=1이면 전설 흉갑에 옵션 4.</summary>
        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            var all = Equipment.All;
            GearItem gear = null;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Grade == GearGrade.Legendary
                    && all[i].RecipeId == Equipment.LeatherArmorRecipe
                    && CountOf(all[i]) == LegendaryCount)
                {
                    gear = all[i];
                    break;
                }
            }
            if (gear == null)
            {
                gear = Equipment.TryGrantDrop(Equipment.LeatherArmorRecipe, GearGrade.Legendary);
                if (gear == null) return;
                gear.Affixes = new[] { 0, 1, 2, 3 };
                Equipment.Flush();
            }
            var roster = LifeSystem.GetCharacters();
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i] == null || roster[i].IsDeleted) continue;
                if (Equipment.TryEquip(roster[i], gear.Id)) break;
            }
            _lastLine = Format(gear);
        }

        /// <summary>시각 QA. QA_GEAR_LIST=1이면 전설을 등록했다가 되돌려 옵션 4가 남는다.</summary>
        public static void SeedListQaIfRequested()
        {
            if (!ShowListQa) return;
            if (_qaListSeeded) return;
            _qaListSeeded = true;
            GameState.SetTowerFloorForTest(EstateScreen.AuctionUnlockFloor);
            if (GameState.Wallet.Copper < 50_000)
                GameState.Grant(50_000);
            StarterSecond.ResetForTest();
            var all = Equipment.All;
            GearItem have = null;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Grade == GearGrade.Legendary
                    && all[i].RecipeId == Equipment.LeatherArmorRecipe
                    && CountOf(all[i]) == LegendaryCount)
                {
                    have = all[i];
                    break;
                }
            }
            if (have == null)
            {
                have = Equipment.TryGrantDrop(Equipment.LeatherArmorRecipe, GearGrade.Legendary);
                if (have == null) return;
                have.Affixes = new[] { 0, 1, 2, 3 };
                Equipment.Flush();
            }
            string id = have.Id;
            if (!AuctionState.TryListGear(id, ListQaPrice)) return;
            string lotId = "";
            var lots = AuctionState.Lots;
            for (int i = 0; i < lots.Count; i++)
            {
                if (lots[i] != null && !lots[i].Npc && lots[i].Gear)
                {
                    lotId = lots[i].Id;
                    break;
                }
            }
            if (string.IsNullOrEmpty(lotId)) return;
            AuctionState.TryCancel(lotId);
            var bag = Equipment.Unequipped();
            for (int i = 0; i < bag.Count; i++)
            {
                if (bag[i].RecipeId == Equipment.LeatherArmorRecipe
                    && bag[i].Grade == GearGrade.Legendary)
                {
                    _lastLine = Format(bag[i]);
                    return;
                }
            }
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
            _qaListSeeded = false;
            _lastLine = "";
        }
    }
}
