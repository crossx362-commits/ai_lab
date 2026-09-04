using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 연체 3회 파산 — 영지 건물 전체 −1레벨 · 비장착 아이템 30% 압류(§18-5).
    /// 처분 대금은 빚에 넣고, 남은 분만 지갑. 경매 7일 정지는 GameState가 이미 건다.
    /// QA_NO면 강등·압류를 안 한다.
    /// </summary>
    public static class BankruptcySeize
    {
        public const int SeizePercent = 30;
        public const long HideCopper = 2_400;
        public const long GearBaseCopper = 12_000;
        public const long GearEnhanceCopper = 2_000;
        public const string EnvShow = "QA_BANKRUPT_SEIZE";
        public const string EnvNo = "QA_NO_BANKRUPT_SEIZE";

        const string K_ON = "ats.bankrupt.seize.on";
        const string K_KEEP_FROM = "ats.bankrupt.seize.keepFrom";
        const string K_KEEP_TO = "ats.bankrupt.seize.keepTo";
        const string K_DEF_FROM = "ats.bankrupt.seize.defFrom";
        const string K_DEF_TO = "ats.bankrupt.seize.defTo";
        const string K_GEAR = "ats.bankrupt.seize.gear";
        const string K_ITEM = "ats.bankrupt.seize.item";
        const string K_COPPER = "ats.bankrupt.seize.copper";
        const string K_PAID = "ats.bankrupt.seize.paid";

        static bool _loaded;
        static bool _qaSeeded;
        static bool _applied;
        static int _keepFrom;
        static int _keepTo;
        static int _defFrom;
        static int _defTo;
        static int _gearTaken;
        static int _itemTaken;
        static long _copper;
        static long _paid;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool Applied { get { Load(); return _applied; } }
        public static bool DidDowngrade { get { Load(); return _applied && _keepFrom > _keepTo; } }
        public static bool DidSeize { get { Load(); return _applied && (_gearTaken + _itemTaken > 0); } }
        public static int KeepFrom { get { Load(); return _keepFrom; } }
        public static int KeepTo { get { Load(); return _keepTo; } }
        public static int DefFrom { get { Load(); return _defFrom; } }
        public static int DefTo { get { Load(); return _defTo; } }
        public static int GearTaken { get { Load(); return _gearTaken; } }
        public static int ItemTaken { get { Load(); return _itemTaken; } }
        public static long Copper { get { Load(); return _copper; } }
        public static long Paid { get { Load(); return _paid; } }

        public static int TakeCount(int have)
        {
            if (have <= 0) return 0;
            return have * SeizePercent / 100;
        }

        public static long SaleCopper(GearItem gear)
        {
            if (gear == null) return 0;
            return GearBaseCopper + Math.Max(0, gear.Enhance) * GearEnhanceCopper;
        }

        public static long SaleCopper(Economy.LifeItem item)
        {
            switch (item)
            {
                case Economy.LifeItem.CraftHide: return HideCopper;
                case Economy.LifeItem.CraftFang: return 3_600;
                case Economy.LifeItem.EnhanceStone: return 8_000;
                case Economy.LifeItem.RevivalTea: return 40_000;
                case Economy.LifeItem.RebornStone: return 40_000;
                case Economy.LifeItem.ScrollOfReturn: return 8_000;
                default: return HideCopper;
            }
        }

        /// <summary>ApplyBankruptcy가 읽는다. QA_NO면 건물·가방을 안 건드린다.</summary>
        public static void Apply()
        {
            Load();
            if (Blocked) return;

            int keepFrom = EstateBuild.KeepLevel;
            EstateBuild.DowngradeOne();
            int keepTo = EstateBuild.KeepLevel;
            int defFrom = EstateDefense.TotalLevel();
            EstateDefense.DowngradeAll();
            int defTo = EstateDefense.TotalLevel();

            long copper = 0;
            int gearTaken = 0;
            int itemTaken = 0;

            var bag = Equipment.Unequipped();
            bag.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            int takeGear = TakeCount(bag.Count);
            for (int i = 0; i < takeGear; i++)
            {
                var g = bag[i];
                long price = SaleCopper(g);
                if (!Equipment.TryRemove(g.Id)) continue;
                copper += price;
                gearTaken++;
            }

            foreach (Economy.LifeItem it in Enum.GetValues(typeof(Economy.LifeItem)))
            {
                if (it == Economy.LifeItem.SpecialJobToken) continue;
                int have = GameState.Bag.GetCount(it);
                int take = TakeCount(have);
                if (take <= 0) continue;
                if (!GameState.Consume(it, take)) continue;
                copper += SaleCopper(it) * take;
                itemTaken += take;
            }

            _applied = true;
            _keepFrom = keepFrom;
            _keepTo = keepTo;
            _defFrom = defFrom;
            _defTo = defTo;
            _gearTaken = gearTaken;
            _itemTaken = itemTaken;
            _copper = copper;
            _paid = GameState.RepayFromIncome(copper);
            long leftover = copper - _paid;
            if (leftover > 0) GameState.Grant(leftover);
            Save();
        }

        public static string KeepLine()
        {
            if (Blocked) return "파산 강등 없음";
            return "파산 강등 −1(§18-5)";
        }

        public static string ItemLine()
        {
            if (Blocked) return "비장착 압류 없음";
            if (_applied && _copper > 0)
                return $"비장착 30% 압류 · {Economy.FormatCurrency(_copper)} 상환(§18-5)";
            return "비장착 30% 압류(§18-5)";
        }

        public static string Line()
        {
            if (Blocked) return "파산 압류 없음";
            if (_applied && _copper > 0)
                return $"건물 −1레벨 · 비장착 30% 압류 · {Economy.FormatCurrency(_copper)} 상환(§18-5)";
            return "건물 −1레벨 · 비장착 30% 압류(§18-5)";
        }

        public static bool ShowOnHub
        {
            get
            {
                if (Blocked) return false;
                if (Environment.GetEnvironmentVariable(EnvShow) == "1") return true;
                return Applied;
            }
        }

        /// <summary>시각 QA. QA_BANKRUPT_SEIZE=1이면 본성3·방어2·가죽10·흉갑10으로 연체 3회를 탄다.</summary>
        public static void SeedQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(EnvShow) != "1") return;
            if (Blocked) return;
            if (_qaSeeded) return;
            RacePrefs.Set(RaceId.인간);
            WorldStar.ResetForTest();
            GameState.ResetAll();
            _qaSeeded = true;
            EstateBuild.SetLevelForTest(3);
            for (int i = 0; i < EstateDefense.All.Length; i++)
                EstateDefense.SetLevelForTest(EstateDefense.All[i], 2);
            GameState.SetTowerFloorForTest(30);
            if (GameState.Bag.GetCount(Economy.LifeItem.CraftHide) < 10)
                GameState.Gain(Economy.LifeItem.CraftHide, 10 - GameState.Bag.GetCount(Economy.LifeItem.CraftHide));
            while (Equipment.Unequipped().Count < 10)
                Equipment.AddUnequippedForTest(Equipment.LeatherArmorRecipe);
            if (GameState.Wallet.Copper < 200_000)
                GameState.Grant(200_000 - GameState.Wallet.Copper);
            long now = 2_000_000L;
            GameState.Borrow(30_000, now);
            GameState.RefreshSanctions(now + Economy.LoanTermHours * 3600L * 3 + 1);
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _applied = PlayerPrefs.GetInt(K_ON, 0) == 1;
            _keepFrom = PlayerPrefs.GetInt(K_KEEP_FROM, 0);
            _keepTo = PlayerPrefs.GetInt(K_KEEP_TO, 0);
            _defFrom = PlayerPrefs.GetInt(K_DEF_FROM, 0);
            _defTo = PlayerPrefs.GetInt(K_DEF_TO, 0);
            _gearTaken = PlayerPrefs.GetInt(K_GEAR, 0);
            _itemTaken = PlayerPrefs.GetInt(K_ITEM, 0);
            long.TryParse(PlayerPrefs.GetString(K_COPPER, "0"), out _copper);
            long.TryParse(PlayerPrefs.GetString(K_PAID, "0"), out _paid);
        }

        static void Save()
        {
            PlayerPrefs.SetInt(K_ON, _applied ? 1 : 0);
            PlayerPrefs.SetInt(K_KEEP_FROM, _keepFrom);
            PlayerPrefs.SetInt(K_KEEP_TO, _keepTo);
            PlayerPrefs.SetInt(K_DEF_FROM, _defFrom);
            PlayerPrefs.SetInt(K_DEF_TO, _defTo);
            PlayerPrefs.SetInt(K_GEAR, _gearTaken);
            PlayerPrefs.SetInt(K_ITEM, _itemTaken);
            PlayerPrefs.SetString(K_COPPER, _copper.ToString());
            PlayerPrefs.SetString(K_PAID, _paid.ToString());
            PlayerPrefs.Save();
        }

        public static void ResetForTest()
        {
            PlayerPrefs.DeleteKey(K_ON);
            PlayerPrefs.DeleteKey(K_KEEP_FROM);
            PlayerPrefs.DeleteKey(K_KEEP_TO);
            PlayerPrefs.DeleteKey(K_DEF_FROM);
            PlayerPrefs.DeleteKey(K_DEF_TO);
            PlayerPrefs.DeleteKey(K_GEAR);
            PlayerPrefs.DeleteKey(K_ITEM);
            PlayerPrefs.DeleteKey(K_COPPER);
            PlayerPrefs.DeleteKey(K_PAID);
            PlayerPrefs.Save();
            _applied = false;
            _keepFrom = _keepTo = _defFrom = _defTo = 0;
            _gearTaken = _itemTaken = 0;
            _copper = _paid = 0;
            _loaded = false;
            _qaSeeded = false;
        }

        public static void ForgetInMemoryForTest()
        {
            _loaded = false;
        }
    }
}
