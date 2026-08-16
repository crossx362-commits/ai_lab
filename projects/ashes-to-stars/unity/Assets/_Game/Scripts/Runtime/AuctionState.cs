using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 로컬 경매장(§12·§18-3). 다른 유저 서버가 아니라 이 기기 장이다.
    /// 드랍·제작만 등록. 등록 2%·체결 8%는 소각. 연체·부채면 GameState가 문을 잠근다.
    /// </summary>
    public static class AuctionState
    {
        public const int MaxMine = 10;
        public const int ListHours = 24;
        public const double ListFeeRate = 0.02;
        public const double SaleFeeRate = 0.08;
        const string K_LOTS = "ats.auction.lots";

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

        public static IReadOnlyList<Lot> Lots { get { Load(); return _lots; } }

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

        static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public static long ListFee(long price) => Math.Max(1, (long)(price * ListFeeRate));
        public static long SaleFee(long price) => Math.Max(1, (long)(price * SaleFeeRate));
        public static int MineCount
        {
            get
            {
                Load();
                int n = 0;
                for (int i = 0; i < _lots.Count; i++)
                    if (!_lots[i].Npc) n++;
                return n;
            }
        }

        static void RestockNpc(long now)
        {
            _lots.RemoveAll(L => L.Npc && L.Until > 0 && now > L.Until);
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
                    Until = now + ListHours * 3600,
                });
            }
            if (npc == 0)
            {
                Add("EnhanceStone", "강화석", false, 1, 8_000);
                Add("CraftHide", "사냥 가죽", false, 3, 2_400);
                Add("RevivalTea", "부활초", false, 1, 40_000);
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
            if (WhyCannotTrade() != null || price <= 0) return false;
            if (MineCount >= MaxMine) return false;
            var g = Equipment.Find(gearId);
            if (g == null) return false;
            long fee = ListFee(price);
            if (!GameState.Pay(fee)) return false;
            if (!Equipment.TryRemove(gearId))
            {
                GameState.Earn(fee);
                return false;
            }
            _lots.Add(new Lot
            {
                Id = Guid.NewGuid().ToString("N"),
                Npc = false,
                Gear = true,
                Key = g.RecipeId + "|" + g.Enhance,
                Label = g.Name + (g.Enhance > 0 ? " +" + g.Enhance : ""),
                Qty = 1,
                Price = price,
                Until = Now() + ListHours * 3600,
            });
            Save();
            return true;
        }

        public static bool TryListItem(Economy.LifeItem item, int qty, long price)
        {
            Load();
            if (WhyCannotTrade() != null || qty <= 0 || price <= 0) return false;
            if (MineCount >= MaxMine) return false;
            if (item == Economy.LifeItem.SpecialJobToken) return false;
            long fee = ListFee(price);
            if (!GameState.Pay(fee)) return false;
            if (!GameState.Consume(item, qty))
            {
                GameState.Earn(fee);
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
                Until = Now() + ListHours * 3600,
            });
            Save();
            return true;
        }

        public static bool TryBuy(string id)
        {
            Load();
            if (WhyCannotTrade() != null) return false;
            Lot lot = null;
            for (int i = 0; i < _lots.Count; i++)
                if (_lots[i].Id == id) { lot = _lots[i]; break; }
            if (lot == null || lot.Price <= 0) return false;
            if (!lot.Npc) return false;
            if (!GameState.Pay(lot.Price)) return false;
            if (!Grant(lot))
            {
                GameState.Earn(lot.Price);
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
            PlayerPrefs.Save();
            _lots.Clear();
            _loaded = false;
        }

        public static void ForgetInMemoryForTest()
        {
            _lots.Clear();
            _loaded = false;
        }
    }
}
