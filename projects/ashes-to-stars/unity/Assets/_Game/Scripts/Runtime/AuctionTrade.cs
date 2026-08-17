using System;

namespace AshesToStars
{
    /// <summary>
    /// 경매 등록 품목(§12). 드랍·제작만 팔고 칭호·스킨·명예는 귀속.
    /// 옛 코드는 증표만 막아서 드랍 품목을 거절했다. QA_NO면 그 옛 거절로 돌아간다.
    /// 목숨 아이템 등록가는 LifePrice 하한(§4·§18-4).
    /// 증표 등록가는 TokenPrice 하한 200 G/h(§18-4).
    /// </summary>
    public static class AuctionTrade
    {
        public const string EnvShow = "QA_AUCTION_TRADE";
        public const string EnvNo = "QA_NO_AUCTION_TRADE";
        public const string TitleKind = "title";
        public const string SkinKind = "skin";
        public const string HonorKind = "honor";
        public const long OldTokenPrice = 250_000;

        static bool _qaSeeded;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>드랍·제작 LifeItem만. QA_NO면 증표를 옛처럼 막는다.</summary>
        public static bool CanList(Economy.LifeItem item)
        {
            if (Blocked && item == Economy.LifeItem.SpecialJobToken) return false;
            return true;
        }

        /// <summary>1인 클리어 칭호·스킨·명예. 해내서 받은 것은 못 판다(§12).</summary>
        public static bool CanListBound(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return false;
            if (kind == TitleKind || kind == SkinKind || kind == HonorKind) return false;
            return false;
        }

        public static string WhyCannotList(Economy.LifeItem item)
        {
            if (CanList(item)) return null;
            return "귀속 — 해내서 받은 것은 못 판다(§12)";
        }

        public static string WhyCannotListBound(string kind)
        {
            if (CanListBound(kind)) return null;
            return "귀속 — 칭호·스킨·명예는 못 판다(§12)";
        }

        public static string TradeLine()
        {
            if (LifePrice.ShowQa && !LifePrice.Blocked) return LifePrice.Line();
            if (TokenPrice.ShowQa && !TokenPrice.Blocked) return TokenPrice.Line();
            if (Blocked) return "증표 등록 잠김 — 옛 거절";
            return "드랍·제작만 거래 · 칭호·명예는 귀속(§12)";
        }

        public static long ListPrice(Economy.LifeItem item)
        {
            if (LifePrice.Hours(item) > 0f) return LifePrice.Floor(item);
            if (item == Economy.LifeItem.SpecialJobToken) return TokenPrice.Floor(item);
            return item switch
            {
                Economy.LifeItem.RevivalTea => LifePrice.OldTea,
                Economy.LifeItem.ScrollOfReturn => LifePrice.OldScroll,
                Economy.LifeItem.RebornStone => LifePrice.OldStone,
                Economy.LifeItem.SpecialJobToken => OldTokenPrice,
                Economy.LifeItem.AdvancementMaterial => 5_000,
                Economy.LifeItem.EnhanceStone => 8_000,
                Economy.LifeItem.CraftHide => 2_400,
                Economy.LifeItem.CraftFang => 3_600,
                Economy.LifeItem.CraftBone => 3_000,
                Economy.LifeItem.CraftPart => 3_000,
                Economy.LifeItem.CraftCrystal => 3_000,
                Economy.LifeItem.CraftDemonite => 3_000,
                _ => 2_400,
            };
        }

        static readonly Economy.LifeItem[] BagOrder =
        {
            Economy.LifeItem.SpecialJobToken,
            Economy.LifeItem.RebornStone,
            Economy.LifeItem.RevivalTea,
            Economy.LifeItem.ScrollOfReturn,
            Economy.LifeItem.EnhanceStone,
            Economy.LifeItem.AdvancementMaterial,
            Economy.LifeItem.CraftHide,
            Economy.LifeItem.CraftFang,
            Economy.LifeItem.CraftBone,
            Economy.LifeItem.CraftPart,
            Economy.LifeItem.CraftCrystal,
            Economy.LifeItem.CraftDemonite,
        };

        /// <summary>등록 줄에 쓸 첫 가방 품목. CanList가 거부하면 건너뛴다.</summary>
        public static bool TryFirstBag(out Economy.LifeItem item, out int qty)
        {
            item = Economy.LifeItem.CraftHide;
            qty = 0;
            for (int i = 0; i < BagOrder.Length; i++)
            {
                var it = BagOrder[i];
                if (!CanList(it)) continue;
                int n = GameState.Bag.GetCount(it);
                if (n <= 0) continue;
                item = it;
                qty = 1;
                return true;
            }
            return false;
        }

        /// <summary>시각 QA. QA_AUCTION_TRADE=1이면 30층·증표. QA_LIFE_PRICE면 환생석. QA_TOKEN_PRICE면 증표 200골드.</summary>
        public static void SeedQaIfRequested()
        {
            LifePrice.SeedQaIfRequested();
            TokenPrice.SeedQaIfRequested();
            if (Environment.GetEnvironmentVariable(EnvShow) != "1") return;
            if (Blocked) return;
            if (LifePrice.ShowQa && !LifePrice.Blocked) return;
            if (TokenPrice.ShowQa && !TokenPrice.Blocked) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            RacePrefs.Set(RaceId.인간);
            GameState.SetTowerFloorForTest(EstateScreen.AuctionUnlockFloor);
            AuctionState.SetOpenedAtForTest(
                DateTimeOffset.UtcNow.ToUnixTimeSeconds() - AuctionState.BuyLockSeconds - 1);
            if (GameState.Wallet.Copper < 50_000)
                GameState.Grant(50_000);
            if (GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken) < 1)
                GameState.Gain(Economy.LifeItem.SpecialJobToken, 1);
            StarterSecond.ResetForTest();
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
            LifePrice.ResetForTest();
            TokenPrice.ResetForTest();
        }
    }
}
