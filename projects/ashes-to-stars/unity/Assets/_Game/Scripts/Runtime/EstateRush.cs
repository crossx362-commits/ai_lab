using System;

namespace AshesToStars
{
    /// <summary>
    /// 건설 단축 수학(§13-2·§18-12). 유료 단축 없음.
    /// 골드=남은 1시간당 건물 비용의 15%. 계열 재료=1개당 남은 시간의 2%.
    /// 어떤 수단으로도 원 소요의 50% 밑으로는 못 당긴다.
    /// </summary>
    public static class EstateRush
    {
        public const double GoldPerHourShare = 0.15;
        public const double MaterialShare = 0.02;
        public const double RemainFloor = 0.5;

        public static readonly Economy.LifeItem[] FamilyMaterials =
        {
            Economy.LifeItem.CraftHide,
            Economy.LifeItem.CraftFang,
            Economy.LifeItem.CraftBone,
            Economy.LifeItem.CraftPart,
            Economy.LifeItem.CraftCrystal,
            Economy.LifeItem.CraftDemonite,
        };

        public static bool Disabled()
        {
            string raw = Environment.GetEnvironmentVariable("QA_NO_RUSH");
            return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
        }

        public static long FloorRemain(long originalSeconds)
        {
            if (originalSeconds <= 0) return 0;
            return (long)Math.Ceiling(originalSeconds * RemainFloor);
        }

        public static long Rushable(long remaining, long originalSeconds)
        {
            if (remaining <= 0) return 0;
            long floor = FloorRemain(originalSeconds);
            return remaining > floor ? remaining - floor : 0;
        }

        public static long GoldCost(long seconds, long buildingCost)
        {
            if (seconds <= 0 || buildingCost <= 0) return 0;
            double hours = seconds / 3600.0;
            long pay = (long)Math.Ceiling(hours * GoldPerHourShare * buildingCost);
            return pay < 1 ? 1 : pay;
        }

        public static long MaterialCut(long remaining, int count)
        {
            if (remaining <= 0 || count <= 0) return 0;
            return (long)Math.Floor(remaining * MaterialShare * count);
        }

        public static bool IsFamilyMaterial(Economy.LifeItem item)
        {
            for (int i = 0; i < FamilyMaterials.Length; i++)
                if (FamilyMaterials[i] == item) return true;
            return false;
        }

        public static bool IsForbidden(Economy.LifeItem item)
        {
            return item == Economy.LifeItem.RevivalTea
                || item == Economy.LifeItem.RebornStone
                || item == Economy.LifeItem.ScrollOfReturn
                || !IsFamilyMaterial(item);
        }

        public static Economy.LifeItem? FirstOwnedFamilyMaterial()
        {
            for (int i = 0; i < FamilyMaterials.Length; i++)
            {
                var it = FamilyMaterials[i];
                if (GameState.Bag.GetCount(it) > 0) return it;
            }
            return null;
        }
    }
}
