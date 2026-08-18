using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 로컬 경매장(§12·§18-3). 다른 유저 서버가 아니라 이 기기 장이다.
    /// 드랍·제작만 등록(증표·환생석 포함). 칭호·스킨·명예는 귀속. 등록 2%·체결 8%는 소각.
    /// 계정 종족이 인간이면 총 10%→7%(등록 1.4%·체결 5.6%, §18-9).
    /// 해금 후 7일은 판매만·구매 불가(§18-3·§18-14). QA_NO면 구매가 열린다.
    /// 등록 24시간 뒤 유찰 — 물건은 돌아오고 수수료는 소각(§18-3). QA_NO면 만료 안 함.
    /// </summary>
    public static class AuctionState
    {
        public const int MaxMine = 10;
        public const int ListHours = 24;
        public const long ExpireSeconds = ListHours * 3600L;
        public const int BuyLockDays = 7;
        public const long BuyLockSeconds = BuyLockDays * 24 * 3600;
        public const double ListFeeRate = 0.02;
        public const double SaleFeeRate = 0.08;
        public const double DefaultFeePercent = 10;
        public const double HumanFeePercent = 7;
        public const string EnvShow = "QA_AUCTION_FEE";
        public const string EnvNo = "QA_NO_AUCTION_FEE";
        public const string EnvShowBuyLock = "QA_AUCTION_BUY_LOCK";
        public const string EnvNoBuyLock = "QA_NO_AUCTION_BUY_LOCK";
        public const string EnvShowExpire = "QA_AUCTION_EXPIRE";
        public const string EnvNoExpire = "QA_NO_AUCTION_EXPIRE";
        const string K_LOTS = "ats.auction.lots";
        const string K_OPENED = "ats.auction.openedAt";

        /// <summary>SelfCheck가 7일 창을 밀 때만. 기본은 UTC now.</summary>
        public static Func<long> NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>SelfCheck가 수수료를 고정할 때만. 0이면 계정 종족·에셋을 본다.</summary>
        public static float ForceFeePercent;

        public sealed class Lot
        {
            public string Id;
            public bool Npc;
            public bool Gear;
            public string Key;   // gearId 또는 LifeItem 이름
            public string Label;
            public int Qty = 1;
            public long Price;
            public long Until;
        }

        static readonly List<Lot> _lots = new List<Lot>();
        static bool _loaded;
        static bool _openedLoaded;
        static long _openedAt;
        static bool _qaBuyLockSeeded;
        static bool _qaExpireSeeded;

        public static IReadOnlyList<Lot> Lots
        {
            get
            {
                Load();
                SweepExpired();
                return _lots;
            }
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _lots.Clear();
            string raw = PlayerPrefs.GetString(K_LOTS, "");
            if (!string.IsNullOrEmpty(raw))
            {
                foreach (string line in raw.Split('\n'))
                {
                    if (string.IsNullOrEmpty(line)) continue;
                    string[] p = line.Split('\t');
                    if (p.Length < 8) continue;
                    _lots.Add(new Lot
                    {
                        Id = p[0],
                        Npc = p[1] == "1",
                        Gear = p[2] == "1",
                        Key = p[3],
                        Label = p[4],
                        Qty = int.TryParse(p[5], out int q) ? q : 1,
                        Price = long.TryParse(p[6], out long pr) ? pr : 0,
                        Until = long.TryParse(p[7], out long u) ? u : 0,
                    });
                }
            }
            RestockNpc(Now());
        }

        static void Save()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _lots.Count; i++)
            {
                var L = _lots[i];
                if (i > 0) sb.Append('\n');
                sb.Append(L.Id).Append('\t').Append(L.Npc ? "1" : "0").Append('\t')
                    .Append(L.Gear ? "1" : "0").Append('\t').Append(L.Key).Append('\t')
                    .Append(L.Label.Replace('\t', ' ')).Append('\t').Append(L.Qty).Append('\t')
                    .Append(L.Price).Append('\t').Append(L.Until);
            }
            PlayerPrefs.SetString(K_LOTS, sb.ToString());
            PlayerPrefs.Save();
        }

        static long Now() => NowUnix();

        public static bool BuyLockBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNoBuyLock);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static long OpenedAt { get { LoadOpened(); return _openedAt; } }

        static void LoadOpened()
        {
            if (_openedLoaded) return;
            _openedLoaded = true;
            long.TryParse(PlayerPrefs.GetString(K_OPENED, "0"), out _openedAt);
            if (_openedAt < 0) _openedAt = 0;
        }

        static void SaveOpened()
        {
            PlayerPrefs.SetString(K_OPENED, _openedAt.ToString());
            PlayerPrefs.Save();
        }

        /// <summary>탑이 30층에 처음 닿으면 7일 시계를 찍는다. 이미 찍혀 있으면 안 덮는다.</summary>
        public static void NoteUnlock(int floor)
        {
            if (floor < EstateScreen.AuctionUnlockFloor) return;
            LoadOpened();
            if (_openedAt > 0) return;
            _openedAt = Now();
            SaveOpened();
        }

        public static void SetOpenedAtForTest(long unix)
        {
            _openedLoaded = true;
            _openedAt = Math.Max(0, unix);
            SaveOpened();
        }

        public static long BuyLockLeft(long now = 0)
        {
            if (BuyLockBlocked) return 0;
            LoadOpened();
            if (_openedAt <= 0) return 0;
            if (now <= 0) now = Now();
            long left = _openedAt + BuyLockSeconds - now;
            return left > 0 ? left : 0;
        }

        public static bool CanBuy(long now = 0) => BuyLockLeft(now) <= 0;

        public static string BuyLockLine(long now = 0)
        {
            long left = BuyLockLeft(now);
            if (left <= 0) return "";
            long days = left / 86400;
            long hours = (left % 86400) / 3600;
            return $"신규 계정 구매 잠금 {days}일 {hours}시간(§18-3) — 판매만 가능";
        }

        public static string WhyCannotBuy()
        {
            string trade = WhyCannotTrade();
            if (trade != null) return trade;
            if (!CanBuy()) return BuyLockLine();
            return null;
        }

        public static bool FeeBlocked =>
            Environment.GetEnvironmentVariable(EnvNo) == "1";

        /// <summary>§18-9 인간 경매 수수료 10%→7%. 에셋이 없으면 표로 폴백한다.</summary>
        public static double FeePercent()
        {
            if (FeeBlocked) return DefaultFeePercent;
            if (ForceFeePercent > 0f) return ForceFeePercent;
            try
            {
                var races = Resources.LoadAll<RaceDef>("races");
                RaceId id = RacePrefs.Get();
                for (int i = 0; i < races.Length; i++)
                {
                    if (races[i] != null && races[i].Id == id && races[i].경매수수료 > 0f)
                        return races[i].경매수수료;
                }
            }
            catch
            {
                // 배치 검사 중 에셋 DB가 비면 표로 간다.
            }
            return RacePrefs.Get() == RaceId.인간 ? HumanFeePercent : DefaultFeePercent;
        }

        public static double ListRate() => FeePercent() * 0.002;
        public static double SaleRate() => FeePercent() * 0.008;
        public static long ListFee(long price)
        {
            long p = Math.Max(1L, (long)Math.Round(FeePercent()));
            return Math.Max(1, price * p * 2 / 1000);
        }
        public static long SaleFee(long price)
        {
            long p = Math.Max(1L, (long)Math.Round(FeePercent()));
            return Math.Max(1, price * p * 8 / 1000);
        }

        public static string FeeLine()
        {
            string list = FormatPct(ListRate());
            string sale = FormatPct(SaleRate());
            if (Math.Abs(FeePercent() - HumanFeePercent) < 1e-9)
                return $"인간 수수료 7% — 등록 {list}·체결 {sale} 소각(§18-9)";
            return $"등록 {list}·체결 {sale} 소각(§18-3)";
        }

        static string FormatPct(double rate)
        {
            double pct = rate * 100.0;
            if (Math.Abs(pct - Math.Round(pct)) < 1e-6)
                return ((int)Math.Round(pct)).ToString() + "%";
            return pct.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>시각 QA. QA_AUCTION_FEE=1이면 인간·30층·가죽으로 경매장을 연다.</summary>
        public static void SeedQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(EnvShow) != "1") return;
            RacePrefs.Set(RaceId.인간);
            GameState.SetTowerFloorForTest(30);
            if (GameState.Wallet.Copper < 50_000)
                GameState.Grant(50_000);
            if (GameState.Bag.GetCount(Economy.LifeItem.CraftHide) < 1)
                GameState.Gain(Economy.LifeItem.CraftHide, 2);
            StarterSecond.ResetForTest();
        }

        /// <summary>시각 QA. QA_AUCTION_EXPIRE=1이면 가죽 1건이 24시간 등록으로 선다.</summary>
        public static void SeedExpireQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(EnvShowExpire) != "1") return;
            if (ExpireBlocked) return;
            if (_qaExpireSeeded) return;
            _qaExpireSeeded = true;
            RacePrefs.Set(RaceId.인간);
            GameState.SetTowerFloorForTest(EstateScreen.AuctionUnlockFloor);
            SetOpenedAtForTest(Now() - BuyLockSeconds - 1);
            if (GameState.Wallet.Copper < 50_000)
                GameState.Grant(50_000);
            if (GameState.Bag.GetCount(Economy.LifeItem.CraftHide) < 1)
                GameState.Gain(Economy.LifeItem.CraftHide, 2);
            StarterSecond.ResetForTest();
            Load();
            RestockNpc(Now());
            if (MineCount == 0)
                TryListItem(Economy.LifeItem.CraftHide, 1, 2_400);
        }

        /// <summary>시각 QA. QA_AUCTION_BUY_LOCK=1이면 해금 직후라 구매가 잠긴 장을 연다.</summary>
        public static void SeedBuyLockQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(EnvShowBuyLock) != "1") return;
            if (BuyLockBlocked) return;
            if (_qaBuyLockSeeded) return;
            _qaBuyLockSeeded = true;
            RacePrefs.Set(RaceId.인간);
            GameState.SetTowerFloorForTest(EstateScreen.AuctionUnlockFloor);
            SetOpenedAtForTest(Now());
            if (GameState.Wallet.Copper < 50_000)
                GameState.Grant(50_000);
            if (GameState.Bag.GetCount(Economy.LifeItem.CraftHide) < 1)
                GameState.Gain(Economy.LifeItem.CraftHide, 2);
            StarterSecond.ResetForTest();
            Load();
            RestockNpc(Now());
        }

        public static int MineCount
        {
            get
            {
                Load();
                SweepExpired();
                int n = 0;
                for (int i = 0; i < _lots.Count; i++)
                    if (!_lots[i].Npc) n++;
                return n;
            }
        }

        public static bool ExpireBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNoExpire);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>등록 24시간이 지나면 내 물건은 돌아오고 수수료는 그대로 소각(§18-3).</summary>
        public static int SweepExpired(long now = 0)
        {
            Load();
            if (now <= 0) now = Now();
            int n = 0;
            for (int i = _lots.Count - 1; i >= 0; i--)
            {
                var lot = _lots[i];
                if (lot.Until <= 0 || now <= lot.Until) continue;
                if (!lot.Npc && ExpireBlocked) continue;
                if (!lot.Npc && !Grant(lot)) continue;
                _lots.RemoveAt(i);
                n++;
            }
            if (n > 0) Save();
            return n;
        }

        public static string ExpireLine()
            => $"등록 {ListHours}시간 · 유찰 시 수수료 소각(§18-3)";

        public static string MineLine()
            => $"내 등록 {MineCount}/{MaxMine} · {ExpireLine()}";

        public static string LotTimeLine(Lot lot, long now = 0)
        {
            if (lot == null || lot.Until <= 0) return "";
            if (now <= 0) now = Now();
            long left = lot.Until - now;
            if (left <= 0) return "유찰";
            long hours = left / 3600;
            long mins = (left % 3600) / 60;
            if (hours >= 24 && mins == 0) return $"남은 {hours}시간";
            if (hours >= 1) return $"남은 {hours}시간 {mins}분";
            return $"남은 {mins}분";
        }

        static void RestockNpc(long now)
        {
            SweepExpired(now);
            int npc = 0;
            for (int i = 0; i < _lots.Count; i++)
                if (_lots[i].Npc) npc++;
            if (npc >= 4) return;
            void Add(string key, string label, bool gear, int qty, long price)
            {
                _lots.Add(new Lot
                {
                    Id = "npc-" + key + "-" + now,
                    Npc = true,
                    Gear = gear,
                    Key = key,
                    Label = label,
                    Qty = qty,
                    Price = price,
                    Until = now + ExpireSeconds,
                });
            }
            if (npc == 0)
            {
                Add("EnhanceStone", "강화석", false, 1, 8_000);
                Add("CraftHide", "사냥 가죽", false, 3, 2_400);
                Add("RevivalTea", "부활초", false, 1, LifePrice.Floor(Economy.LifeItem.RevivalTea));
                Add("CraftFang", "송곳니", false, 5, 18_000);
            }
            Save();
        }

        public static string WhyCannotTrade()
        {
            if (!GameState.CanUseAuction())
                return GameState.AuctionBlockReason();
            return null;
        }

        public static bool TryListGear(string gearId, long price)
        {
            Load();
            SweepExpired();
            if (WhyCannotTrade() != null || price <= 0) return false;
            if (MineCount >= MaxMine) return false;
            var g = Equipment.Find(gearId);
            if (g == null) return false;
            long fee = ListFee(price);
            if (!GameState.Pay(fee)) return false;
            if (!Equipment.TryRemove(gearId))
            {
                GameState.Grant(fee);
                return false;
            }
            _lots.Add(new Lot
            {
                Id = Guid.NewGuid().ToString("N"),
                Npc = false,
                Gear = true,
                Key = GearOpt.Pack(g),
                Label = g.Name + (g.Enhance > 0 ? " +" + g.Enhance : ""),
                Qty = 1,
                Price = price,
                Until = Now() + ExpireSeconds,
            });
            Save();
            return true;
        }

        public static bool TryListItem(Economy.LifeItem item, int qty, long price)
        {
            Load();
            SweepExpired();
            if (WhyCannotTrade() != null || qty <= 0 || price <= 0) return false;
            if (MineCount >= MaxMine) return false;
            if (!AuctionTrade.CanList(item)) return false;
            if (LifePrice.BelowFloor(item, price)) return false;
            if (LifePrice.AboveCeil(item, price)) return false;
            if (TokenPrice.BelowFloor(item, price)) return false;
            if (TokenPrice.AboveCeil(item, price)) return false;
            long fee = ListFee(price);
            if (!GameState.Pay(fee)) return false;
            if (!GameState.Consume(item, qty))
            {
                GameState.Grant(fee);
                return false;
            }
            _lots.Add(new Lot
            {
                Id = Guid.NewGuid().ToString("N"),
                Npc = false,
                Gear = false,
                Key = item.ToString(),
                Label = GameState.Label(item) + (qty > 1 ? " ×" + qty : ""),
                Qty = qty,
                Price = price,
                Until = Now() + ExpireSeconds,
            });
            Save();
            return true;
        }

        /// <summary>칭호·스킨·명예. CanListBound가 거부하면 등록하지 않는다(§12).</summary>
        public static bool TryListBound(string kind)
        {
            if (!AuctionTrade.CanListBound(kind)) return false;
            return false;
        }

        public static bool TryBuy(string id)
        {
            Load();
            if (WhyCannotBuy() != null) return false;
            Lot lot = null;
            for (int i = 0; i < _lots.Count; i++)
                if (_lots[i].Id == id) { lot = _lots[i]; break; }
            if (lot == null || lot.Price <= 0) return false;
            if (!lot.Npc) return false;
            if (!GameState.Pay(lot.Price)) return false;
            if (!Grant(lot))
            {
                GameState.Grant(lot.Price);
                return false;
            }
            _lots.Remove(lot);
            Save();
            return true;
        }

        public static bool TryCancel(string id)
        {
            Load();
            Lot lot = null;
            for (int i = 0; i < _lots.Count; i++)
                if (_lots[i].Id == id) { lot = _lots[i]; break; }
            if (lot == null || lot.Npc) return false;
            if (!Grant(lot)) return false;
            _lots.Remove(lot);
            Save();
            return true;
        }

        static bool Grant(Lot lot)
        {
            if (lot.Gear)
            {
                if (lot.Npc)
                    return GameState.Gain(Economy.LifeItem.EnhanceStone, 1);
                return Equipment.RestoreListed(lot.Key, lot.Label);
            }
            if (!Enum.TryParse(lot.Key, out Economy.LifeItem item)) return false;
            return GameState.Gain(item, lot.Qty);
        }

        public static void ResetForTest()
        {
            PlayerPrefs.DeleteKey(K_LOTS);
            PlayerPrefs.DeleteKey(K_OPENED);
            PlayerPrefs.Save();
            _lots.Clear();
            _loaded = false;
            _openedLoaded = false;
            _openedAt = 0;
            _qaBuyLockSeeded = false;
            _qaExpireSeeded = false;
            AuctionTrade.ResetForTest();
            NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public static void ForgetInMemoryForTest()
        {
            _lots.Clear();
            _loaded = false;
            _openedLoaded = false;
            _openedAt = 0;
        }
    }
}
