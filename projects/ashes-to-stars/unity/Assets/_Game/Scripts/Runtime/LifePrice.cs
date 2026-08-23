using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 목숨 아이템 경매 시세 하한·상한(§4·§18-4).
    /// 두루마리 2~4 / 부활초 3~8 / 환생석 150~300 G/h.
    /// 옛 ListPrice는 T1 환생석을 20골드로 팔아 "경매장에서도 싸지 않다"가 거짓이었다.
    /// QA_NO면 옛 고정가·상한 없음. ListPrice·TryListItem·NPC가 읽는다.
    /// </summary>
    public static class LifePrice
    {
        public const string EnvShow = "QA_LIFE_PRICE";
        public const string EnvNo = "QA_NO_LIFE_PRICE";
        public const float ScrollHours = 2f;
        public const float TeaHours = 3f;
        public const float StoneHours = 150f;
        public const float ScrollCeilHours = 4f;
        public const float TeaCeilHours = 8f;
        public const float StoneCeilHours = 300f;
        public const long OldTea = 40_000;
        public const long OldScroll = 25_000;
        public const long OldStone = 200_000;

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

        /// <summary>표의 G/h 하한. 재료·증표는 0 — 이 칸이 아니다.</summary>
        public static float Hours(Economy.LifeItem item) => item switch
        {
            Economy.LifeItem.ScrollOfReturn => ScrollHours,
            Economy.LifeItem.RevivalTea => TeaHours,
            Economy.LifeItem.RebornStone => StoneHours,
            _ => 0f,
        };

        public static long Copper(float hours, int tier)
        {
            if (hours <= 0f) return 0;
            int t = tier;
            if (t < 0) t = 0;
            if (t >= Economy.TierRevenueMultiplier.Length)
                t = Economy.TierRevenueMultiplier.Length - 1;
            double raw = hours * (double)Economy.TierRevenueMultiplier[t] * Economy.COPPER_PER_GOLD;
            long n = (long)System.Math.Round(raw);
            return n < 1 ? 1 : n;
        }

        public static long OldCopper(Economy.LifeItem item) => item switch
        {
            Economy.LifeItem.RevivalTea => OldTea,
            Economy.LifeItem.ScrollOfReturn => OldScroll,
            Economy.LifeItem.RebornStone => OldStone,
            _ => 0,
        };

        /// <summary>선택 티어의 하한. QA_NO면 옛 고정가.</summary>
        public static long Floor(Economy.LifeItem item)
        {
            float h = Hours(item);
            if (h <= 0f) return 0;
            if (Blocked) return OldCopper(item);
            return Copper(h, GameState.Tier);
        }

        public static bool BelowFloor(Economy.LifeItem item, long price)
        {
            long floor = Floor(item);
            return floor > 0 && price < floor;
        }

        /// <summary>표의 G/h 상한. 재료·증표는 0 — 이 칸이 아니다.</summary>
        public static float CeilHoursOf(Economy.LifeItem item) => item switch
        {
            Economy.LifeItem.ScrollOfReturn => ScrollCeilHours,
            Economy.LifeItem.RevivalTea => TeaCeilHours,
            Economy.LifeItem.RebornStone => StoneCeilHours,
            _ => 0f,
        };

        /// <summary>선택 티어의 상한. QA_NO면 상한 없음(옛).</summary>
        public static long Ceil(Economy.LifeItem item)
        {
            float h = CeilHoursOf(item);
            if (h <= 0f) return 0;
            if (Blocked) return long.MaxValue;
            return Copper(h, GameState.Tier);
        }

        public static bool AboveCeil(Economy.LifeItem item, long price)
        {
            long ceil = Ceil(item);
            return ceil > 0 && ceil < long.MaxValue && price > ceil;
        }

        public static string Line()
        {
            if (Blocked) return "목숨 시세 옛 고정가";
            long stone = Floor(Economy.LifeItem.RebornStone);
            long hi = Ceil(Economy.LifeItem.RebornStone);
            return $"목숨 시세 하한 · 환생석 {EstateStatusHud.ShortCopper(stone)} · 상한 {EstateStatusHud.ShortCopper(hi)}(§18-4)";
        }

        /// <summary>시각 QA. 30층으로 장을 열고 T1을 골라 150골드가 보이게 한다.</summary>
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
            if (GameState.Wallet.Copper < 50_000)
                GameState.Grant(50_000);
            if (GameState.Bag.GetCount(Economy.LifeItem.RebornStone) < 1)
                GameState.Gain(Economy.LifeItem.RebornStone, 1);
            StarterSecond.ResetForTest();
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
        }
    }
}
