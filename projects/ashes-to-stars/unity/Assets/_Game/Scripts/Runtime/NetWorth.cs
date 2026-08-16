using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 대출 한도의 순자산(§18-5). 장비+영지 평가액이 정의만 있고
    /// <see cref="GameState.LoanLimit"/>는 지갑만 보고 있었다.
    /// 평가는 파산 처분가·본성/방어 건설비. 새 시세는 안 만든다.
    /// QA_NO면 옛 지갑만.
    /// </summary>
    public static class NetWorth
    {
        public const string EnvShow = "QA_NET_WORTH";
        public const string EnvNo = "QA_NO_NET_WORTH";
        public const int QaKeep = 3;

        static bool _qaSeeded;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>지갑 + 장비 + 영지. QA_NO면 지갑만.</summary>
        public static long Assets()
        {
            long wallet = GameState.Wallet.Copper;
            if (Blocked) return wallet;
            return wallet + GearCopper() + EstateCopper();
        }

        public static long GearCopper()
        {
            var all = Equipment.All;
            long sum = 0;
            for (int i = 0; i < all.Count; i++)
                sum += BankruptcySeize.SaleCopper(all[i]);
            return sum;
        }

        public static long KeepCopper()
        {
            int lv = EstateBuild.KeepLevel;
            long sum = 0;
            for (int i = 1; i < lv; i++)
                sum += EstateBuild.UpgradeCost(i);
            return sum;
        }

        public static long DefenseCopper()
        {
            long sum = 0;
            for (int i = 0; i < EstateDefense.All.Length; i++)
            {
                var k = EstateDefense.All[i];
                int lv = EstateDefense.Level(k);
                for (int from = 0; from < lv; from++)
                    sum += EstateDefense.UpgradeCost(from);
            }
            return sum;
        }

        public static long EstateCopper() => KeepCopper() + DefenseCopper();

        public static long ExtraCopper() => GearCopper() + EstateCopper();

        public static string Line()
        {
            if (Blocked) return "순자산 지갑만(옛)";
            return $"순자산 {Economy.FormatCurrency(Assets())} · 한도 {Economy.FormatCurrency(GameState.LoanLimit)}(§18-5)";
        }

        public static bool ShowOnHub
        {
            get
            {
                if (Blocked) return false;
                string raw = Environment.GetEnvironmentVariable(EnvShow);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static void SeedQaIfRequested()
        {
            if (_qaSeeded || !ShowOnHub) return;
            GameState.ResetAll();
            _qaSeeded = true;
            EstateBuild.SetLevelForTest(QaKeep);
            Equipment.AddUnequippedForTest(Equipment.LeatherArmorRecipe);
            if (GameState.TowerFloor < 30)
                GameState.SetTowerFloorForTest(30);
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
        }
    }
}
