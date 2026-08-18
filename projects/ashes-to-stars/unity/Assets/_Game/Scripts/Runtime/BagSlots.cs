using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 계정 공용 가방 60칸(§11). 목숨 아이템은 종류당 1칸, 비장착 장비는 1개 1칸.
    /// 장착 6부위는 캐릭터에 있어 가방을 안 먹는다. QA_NO면 옛 무한.
    /// 골드 확장은 이 칸 아님.
    /// </summary>
    public static class BagSlots
    {
        public const int Cap = 60;
        public const string EnvShow = "QA_BAG_SLOTS";
        public const string EnvNo = "QA_NO_BAG_SLOTS";

        static bool _qaSeeded;

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

        public static int ItemStacks()
        {
            int n = 0;
            foreach (Economy.LifeItem it in Enum.GetValues(typeof(Economy.LifeItem)))
                if (GameState.Bag.GetCount(it) > 0) n++;
            return n;
        }

        public static int GearStacks() => Equipment.Unequipped().Count;

        public static int Used() => ItemStacks() + GearStacks();

        public static int Free()
        {
            if (Blocked) return int.MaxValue;
            int left = Cap - Used();
            return left > 0 ? left : 0;
        }

        public static bool CanAddGear() => Blocked || Used() < Cap;

        /// <summary>이미 있는 스택은 칸을 안 늘린다. 새 종류만 1칸.</summary>
        public static bool CanGain(Economy.LifeItem item, int amount = 1)
        {
            if (Blocked) return true;
            if (amount <= 0) return false;
            if (GameState.Bag.GetCount(item) > 0) return true;
            return Used() < Cap;
        }

        public static string WhyFull() =>
            $"가방이 가득하다 — {Used()}/{Cap}(§11)";

        public static string Line() =>
            Blocked ? "가방 칸 없음" : $"가방 {Used()}/{Cap}(§11)";

        /// <summary>시각 QA. QA_BAG_SLOTS=1이면 비장착 흉갑으로 60칸을 채운다.</summary>
        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            while (Used() < Cap)
            {
                if (Equipment.AddUnequippedForTest(Equipment.LeatherArmorRecipe) == null)
                    break;
            }
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
        }
    }
}
