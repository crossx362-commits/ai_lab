using System;

namespace AshesToStars
{
    /// <summary>
    /// 보스 주 드랍은 고급 장비(§10-8·§11). 제작품은 일반.
    /// 옛 보상은 가죽·목숨 아이템만 주고 장비 칸이 0이었다.
    /// QA_NO면 옛 0. 정예 일반은 이 칸 아님. 랜덤 옵션은 GearOpt.
    /// </summary>
    public static class GearDrop
    {
        public const string EnvShow = "QA_GEAR_DROP";
        public const string EnvNo = "QA_NO_GEAR_DROP";
        public const GearGrade BossGrade = GearGrade.Uncommon;

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

        public static bool Applies(Economy.DropSource source) =>
            source == Economy.DropSource.FieldDungeonBoss
            || source == Economy.DropSource.Tower5Boss
            || source == Economy.DropSource.Tower10Boss
            || source == Economy.DropSource.RaidDungeon;

        public static GearGrade GradeOf(Economy.DropSource source) =>
            Applies(source) ? BossGrade : GearGrade.Common;

        public static string Line() =>
            Blocked ? "드랍 장비 없음" : "보스 고급 장비(§10-8)";

        public static string Format(GearItem gear) =>
            gear == null ? "" : Equipment.DisplayName(gear) + "(§10-8)";

        public static string PickRecipe(ref Rng rng)
        {
            var list = Equipment.Recipes;
            if (list == null || list.Length == 0) return Equipment.LeatherArmorRecipe;
            return list[rng.Next(list.Length)].Id;
        }

        /// <summary>보스 승리 1판에 고급 1개. 가방이 가득이면 null.</summary>
        public static GearItem Apply(Economy.DropSource source, ref Rng rng)
        {
            _lastLine = "";
            if (Blocked) return null;
            if (!Applies(source)) return null;
            if (!BagSlots.CanAddGear()) return null;
            var gear = Equipment.TryGrantDrop(PickRecipe(ref rng), GradeOf(source));
            _lastLine = Format(gear);
            return gear;
        }

        /// <summary>시각 QA. QA_GEAR_DROP=1이면 가방에 고급 흉갑.</summary>
        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            var bag = Equipment.Unequipped();
            for (int i = 0; i < bag.Count; i++)
            {
                if (bag[i].Grade == BossGrade && bag[i].RecipeId == Equipment.LeatherArmorRecipe)
                {
                    _lastLine = Format(bag[i]);
                    return;
                }
            }
            var gear = Equipment.TryGrantDrop(Equipment.LeatherArmorRecipe, BossGrade);
            _lastLine = Format(gear);
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
            _lastLine = "";
        }
    }
}
