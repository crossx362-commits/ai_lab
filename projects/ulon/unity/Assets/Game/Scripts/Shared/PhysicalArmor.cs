using System;
using System.Collections.Generic;

namespace Ulon.Shared
{
    /// <summary>
    /// 기획 §18.3 마법은 단순 방어력으로 안 막음 — 물리 피해만 장비 AR로 깎는다.
    /// 합 = 가방 아이템 armor(원장 items.json). 맞으면 max(min, 피해−AR).
    /// 방패 막기(ParryChance)와 겹치지 않게 방패 AR은 0. 원작 Virtual Armor 공식은 비복제.
    /// </summary>
    public static class PhysicalArmor
    {
        public const string FileName = "physical_armor.json";
        public const int FallbackMinRemaining = 0;
        public const int FallbackPlate = 4;

        [Serializable]
        class File_
        {
            public int min_remaining;
            public string source;
        }

        static bool loaded;
        static int minRemaining = FallbackMinRemaining;
        static string source = "";
        static string loadError = "";

        /// <summary>네거티브 컨트롤 — true면 옛처럼 AR을 안 뺀다.</summary>
        public static bool NcOff;

        public static string LoadError
        {
            get { EnsureLoaded(); return loadError; }
        }

        public static string Source
        {
            get { EnsureLoaded(); return source; }
        }

        public static int FileMinRemaining
        {
            get { EnsureLoaded(); return minRemaining; }
        }

        public static int Of(IList<ItemRecord> items)
        {
            return ItemCatalog.ArmorOf(items);
        }

        public static int Apply(int damage, int armor)
        {
            if (NcOff)
                return damage;
            EnsureLoaded();
            if (damage <= 0)
                return damage;
            if (armor <= 0)
                return damage;
            int d = damage - armor;
            if (d < minRemaining)
                d = minRemaining;
            return d;
        }

        public static int Apply(int damage, IList<ItemRecord> items)
        {
            return Apply(damage, Of(items));
        }

        public static void Reload()
        {
            loaded = false;
            EnsureLoaded();
        }

        static void EnsureLoaded()
        {
            if (loaded)
                return;
            loaded = true;
            minRemaining = FallbackMinRemaining;
            source = "";
            loadError = "";
            if (!DataLedger.TryRead(FileName, out File_ parsed, out loadError))
                return;
            if (parsed.min_remaining >= 0)
                minRemaining = parsed.min_remaining;
            source = parsed.source ?? "";
        }
    }
}
