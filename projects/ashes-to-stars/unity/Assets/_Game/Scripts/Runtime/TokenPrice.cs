using System;

namespace AshesToStars
{
    /// <summary>
    /// 특수 직업 증표 경매 시세 하한·상한(§18-4). 200~400 G/h.
    /// 하한은 200. 상한 400은 TryListItem이 읽는다. QA_NO면 옛 25골드·상한 없음.
    /// </summary>
    public static class TokenPrice
    {
        public const string EnvShow = "QA_TOKEN_PRICE";
        public const string EnvNo = "QA_NO_TOKEN_PRICE";
        public const float Hours = 200f;
        public const float CeilHours = 400f;
        public const long OldCopper = 250_000;

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
                string raw = Environment.GetEnvironmentVariable(EnvShow);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>선택 티어의 하한. QA_NO면 옛 25골드. 증표가 아니면 0.</summary>
        public static long Floor(Economy.LifeItem item)
        {
            if (item != Economy.LifeItem.SpecialJobToken) return 0;
            if (Blocked) return OldCopper;
            return LifePrice.Copper(Hours, GameState.Tier);
        }

        public static bool BelowFloor(Economy.LifeItem item, long price)
        {
            long floor = Floor(item);
            return floor > 0 && price < floor;
        }

        /// <summary>선택 티어의 상한. QA_NO면 상한 없음(옛).</summary>
        public static long Ceil(Economy.LifeItem item)
        {
            if (item != Economy.LifeItem.SpecialJobToken) return 0;
            if (Blocked) return long.MaxValue;
            return LifePrice.Copper(CeilHours, GameState.Tier);
        }

        public static bool AboveCeil(Economy.LifeItem item, long price)
        {
            long ceil = Ceil(item);
            return ceil > 0 && ceil < long.MaxValue && price > ceil;
        }

        public static string Line()
        {
            if (Blocked) return "증표 시세 옛 25골드";
            long lo = Floor(Economy.LifeItem.SpecialJobToken);
            long hi = Ceil(Economy.LifeItem.SpecialJobToken);
            return $"증표 시세 하한 {Economy.FormatCurrency(lo)} · 상한 {Economy.FormatCurrency(hi)}(§18-4)";
        }

        /// <summary>시각 QA. 30층으로 장을 열고 T1을 골라 200골드가 보이게 한다.</summary>
        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (Blocked) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            RacePrefs.Set(RaceId.인간);
            GameState.SetTowerFloorForTest(EstateScreen.AuctionUnlockFloor);
            GameState.TrySelectTier(0);
            AuctionState.SetOpenedAtForTest(
                DateTimeOffset.UtcNow.ToUnixTimeSeconds() - AuctionState.BuyLockSeconds - 1);
            if (GameState.Wallet.Copper < 80_000)
                GameState.Grant(80_000);
            if (GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken) < 1)
                GameState.Gain(Economy.LifeItem.SpecialJobToken, 1);
            StarterSecond.ResetForTest();
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
        }
    }
}
