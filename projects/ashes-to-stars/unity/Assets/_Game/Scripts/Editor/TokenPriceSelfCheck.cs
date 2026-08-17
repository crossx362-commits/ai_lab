using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>증표 경매 시세 하한. QA_NO면 옛 25골드(§18-4).</summary>
    public static class TokenPriceSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Token Price Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(TokenPrice.EnvShow);
            string no = Environment.GetEnvironmentVariable(TokenPrice.EnvNo);
            string trade = Environment.GetEnvironmentVariable(AuctionTrade.EnvShow);
            string life = Environment.GetEnvironmentVariable(LifePrice.EnvShow);
            Environment.SetEnvironmentVariable(TokenPrice.EnvShow, null);
            Environment.SetEnvironmentVariable(TokenPrice.EnvNo, null);
            Environment.SetEnvironmentVariable(AuctionTrade.EnvShow, null);
            Environment.SetEnvironmentVariable(LifePrice.EnvShow, null);

            GameState.ResetAll();
            AuctionState.ResetForTest();
            AuctionTrade.ResetForTest();
            TokenPrice.ResetForTest();
            LifePrice.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            GameState.SetTowerFloorForTest(1);
            GameState.TrySelectTier(0);
            GameState.Grant(3_000_000);

            Check(Mathf.Approximately(TokenPrice.Hours, 200f), "증표 200 G/h");
            Check(TokenPrice.Floor(Economy.LifeItem.CraftHide) == 0, "가죽은 이 칸 아님");
            Check(TokenPrice.Floor(Economy.LifeItem.RebornStone) == 0, "환생석은 LifePrice 칸");
            Check(TokenPrice.Floor(Economy.LifeItem.SpecialJobToken) == 2_000_000,
                $"T1 증표 200골드 (실제 {TokenPrice.Floor(Economy.LifeItem.SpecialJobToken)})");
            Check(LifePrice.Copper(200f, 0) == 2_000_000, "T1 200 G/h = 200골드");
            long t5 = LifePrice.Copper(200f, 4);
            Check(t5 >= 13_100_000 && t5 <= 13_110_000,
                $"T5 증표 ≈1311골드 (실제 {t5})");
            long t6 = LifePrice.Copper(200f, 5);
            Check(t6 >= 20_970_000 && t6 <= 20_980_000,
                $"T6 증표 ≈2097골드 (실제 {t6})");
            Check(AuctionTrade.ListPrice(Economy.LifeItem.SpecialJobToken) == 2_000_000,
                $"ListPrice T1 증표 (실제 {AuctionTrade.ListPrice(Economy.LifeItem.SpecialJobToken)})");
            Check(AuctionTrade.ListPrice(Economy.LifeItem.CraftHide) == 2_400, "가죽은 옛 값");
            Check(AuctionTrade.ListPrice(Economy.LifeItem.RebornStone) == 1_500_000,
                "환생석 150골드는 그대로");

            GameState.Gain(Economy.LifeItem.SpecialJobToken, 1);
            Check(!AuctionState.TryListItem(Economy.LifeItem.SpecialJobToken, 1, TokenPrice.OldCopper),
                "옛 25골드는 거절");
            Check(GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken) == 1, "거절은 가방을 안 뺀다");
            Check(!AuctionState.TryListItem(Economy.LifeItem.SpecialJobToken, 1, 1_999_999),
                "하한 −1 거절");
            Check(AuctionState.TryListItem(Economy.LifeItem.SpecialJobToken, 1, 2_000_000),
                "하한 정각은 등록");
            Check(AuctionState.MineCount == 1, "내 등록 1건");
            Check(TokenPrice.Line().Contains("200골드") && TokenPrice.Line().Contains("§18-4"),
                $"줄 200골드 (실제 {TokenPrice.Line()})");

            GameState.ResetAll();
            AuctionState.ResetForTest();
            AuctionTrade.ResetForTest();
            TokenPrice.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            GameState.SetTowerFloorForTest(51);
            GameState.TrySelectTier(4);
            GameState.Grant(20_000_000);
            GameState.Gain(Economy.LifeItem.SpecialJobToken, 1);
            long t5Price = LifePrice.Copper(200f, 4);
            Check(AuctionTrade.ListPrice(Economy.LifeItem.SpecialJobToken) == t5Price,
                $"T5 ListPrice (실제 {AuctionTrade.ListPrice(Economy.LifeItem.SpecialJobToken)})");
            Check(!AuctionState.TryListItem(Economy.LifeItem.SpecialJobToken, 1, 2_000_000),
                "T5에서 T1 하한은 거절");
            Check(AuctionState.TryListItem(Economy.LifeItem.SpecialJobToken, 1, t5Price),
                "T5 하한은 등록");

            GameState.ResetAll();
            AuctionState.ResetForTest();
            AuctionTrade.ResetForTest();
            GameState.SetTowerFloorForTest(1);
            GameState.TrySelectTier(0);
            Environment.SetEnvironmentVariable(TokenPrice.EnvNo, "1");
            Check(TokenPrice.Blocked, "QA_NO면 차단");
            Check(AuctionTrade.ListPrice(Economy.LifeItem.SpecialJobToken) == TokenPrice.OldCopper,
                $"차단하면 옛 25골드 (실제 {AuctionTrade.ListPrice(Economy.LifeItem.SpecialJobToken)})");
            GameState.Grant(300_000);
            GameState.Gain(Economy.LifeItem.SpecialJobToken, 1);
            Check(AuctionState.TryListItem(Economy.LifeItem.SpecialJobToken, 1, TokenPrice.OldCopper),
                "차단하면 옛 가로 등록");
            Check(TokenPrice.Line().Contains("옛"),
                $"차단 문구 (실제 {TokenPrice.Line()})");
            Environment.SetEnvironmentVariable(TokenPrice.EnvNo, null);

            GameState.ResetAll();
            AuctionState.ResetForTest();
            AuctionTrade.ResetForTest();
            TokenPrice.ResetForTest();
            Environment.SetEnvironmentVariable(TokenPrice.EnvShow, "1");
            Environment.SetEnvironmentVariable(AuctionTrade.EnvShow, "1");
            AuctionTrade.SeedQaIfRequested();
            Check(GameState.TowerFloor >= 30, $"시드 30층 (실제 {GameState.TowerFloor})");
            Check(GameState.Tier == 0, $"시드 T1 (실제 {GameState.Tier})");
            Check(GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken) >= 1, "시드 증표");
            Check(AuctionTrade.TryFirstBag(out var seeded, out _)
                  && seeded == Economy.LifeItem.SpecialJobToken,
                $"시드 첫 가방 증표 (실제 {seeded})");
            Check(AuctionTrade.ListPrice(Economy.LifeItem.SpecialJobToken) == 2_000_000,
                "시드 ListPrice 200골드");
            Check(AuctionTrade.TradeLine().Contains("200골드"),
                $"시드 문구 (실제 {AuctionTrade.TradeLine()})");
            Check(EstateScreen.AuctionHubLockReason() == null, "시드면 경매장이 열린다");
            Environment.SetEnvironmentVariable(TokenPrice.EnvShow, null);
            Environment.SetEnvironmentVariable(AuctionTrade.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string auctionSrc = File.ReadAllText(Path.Combine(runtime, "AuctionState.cs"));
            string tradeSrc = File.ReadAllText(Path.Combine(runtime, "AuctionTrade.cs"));
            Check(auctionSrc.Contains("TokenPrice.BelowFloor"),
                "TryListItem이 TokenPrice.BelowFloor를 읽는다");
            Check(tradeSrc.Contains("TokenPrice.Floor") && tradeSrc.Contains("TokenPrice.SeedQaIfRequested"),
                "ListPrice·시드가 TokenPrice를 읽는다");

            _ = nameof(TokenPrice.Floor);
            _ = nameof(TokenPrice.BelowFloor);
            _ = nameof(TokenPrice.Line);
            _ = nameof(AuctionTrade.ListPrice);
            _ = nameof(AuctionState.TryListItem);

            Environment.SetEnvironmentVariable(TokenPrice.EnvShow, show);
            Environment.SetEnvironmentVariable(TokenPrice.EnvNo, no);
            Environment.SetEnvironmentVariable(AuctionTrade.EnvShow, trade);
            Environment.SetEnvironmentVariable(LifePrice.EnvShow, life);
            TokenPrice.ResetForTest();
            AuctionTrade.ResetForTest();
            AuctionState.ResetForTest();
            GameState.ResetAll();

            if (_fail > 0)
            {
                Debug.LogError("[TokenPriceSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("TokenPriceSelfCheck FAIL " + _fail);
            }
            Debug.Log("[TokenPriceSelfCheck] PASS\n" + _log);
        }
    }
}
