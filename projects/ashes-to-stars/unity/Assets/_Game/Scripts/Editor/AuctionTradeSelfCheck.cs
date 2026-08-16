using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>경매는 드랍·제작만 받고 칭호·명예는 거절. QA_NO면 증표를 옛처럼 막음(§12).</summary>
    public static class AuctionTradeSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Auction Trade Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(AuctionTrade.EnvShow);
            string no = Environment.GetEnvironmentVariable(AuctionTrade.EnvNo);
            string fee = Environment.GetEnvironmentVariable(AuctionState.EnvShow);
            string buy = Environment.GetEnvironmentVariable(AuctionState.EnvShowBuyLock);
            string expire = Environment.GetEnvironmentVariable(AuctionState.EnvShowExpire);
            Environment.SetEnvironmentVariable(AuctionTrade.EnvShow, null);
            Environment.SetEnvironmentVariable(AuctionTrade.EnvNo, null);
            Environment.SetEnvironmentVariable(AuctionState.EnvShow, null);
            Environment.SetEnvironmentVariable(AuctionState.EnvShowBuyLock, null);
            Environment.SetEnvironmentVariable(AuctionState.EnvShowExpire, null);
            Environment.SetEnvironmentVariable("QA_LOAN_OVERDUE", null);

            GameState.ResetAll();
            AuctionState.ResetForTest();
            AuctionTrade.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            GameState.SetTowerFloorForTest(30);
            GameState.Grant(500_000);

            Check(AuctionTrade.CanList(Economy.LifeItem.CraftHide), "가죽은 등록");
            Check(AuctionTrade.CanList(Economy.LifeItem.RevivalTea), "부활초는 등록");
            Check(AuctionTrade.CanList(Economy.LifeItem.RebornStone), "환생석은 등록");
            Check(AuctionTrade.CanList(Economy.LifeItem.SpecialJobToken), "증표는 등록(§12 드랍)");
            Check(AuctionTrade.CanList(Economy.LifeItem.EnhanceStone), "강화석은 등록");
            Check(AuctionTrade.CanList(Economy.LifeItem.ScrollOfReturn), "두루마리는 등록");
            Check(!AuctionTrade.CanListBound(AuctionTrade.TitleKind), "칭호 귀속");
            Check(!AuctionTrade.CanListBound(AuctionTrade.SkinKind), "스킨 귀속");
            Check(!AuctionTrade.CanListBound(AuctionTrade.HonorKind), "명예 귀속");
            Check(!AuctionState.TryListBound(AuctionTrade.TitleKind), "칭호 등록 거부");
            Check(AuctionTrade.WhyCannotListBound(AuctionTrade.TitleKind).Contains("귀속"),
                $"칭호 사유 (실제 {AuctionTrade.WhyCannotListBound(AuctionTrade.TitleKind)})");
            Check(AuctionTrade.TradeLine().Contains("드랍") && AuctionTrade.TradeLine().Contains("귀속"),
                $"문구 드랍·귀속 (실제 {AuctionTrade.TradeLine()})");
            Check(AuctionTrade.ListPrice(Economy.LifeItem.SpecialJobToken) == AuctionTrade.TokenPrice,
                $"증표 등록가 {AuctionTrade.TokenPrice}");

            GameState.Gain(Economy.LifeItem.SpecialJobToken, 1);
            GameState.Gain(Economy.LifeItem.CraftHide, 2);
            long gold = GameState.Wallet.Copper;
            int tokens = GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken);
            Check(AuctionState.TryListItem(Economy.LifeItem.SpecialJobToken, 1, AuctionTrade.TokenPrice),
                "증표 등록 성공");
            Check(GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken) == tokens - 1,
                "등록이 증표를 뺀다");
            long feePaid = gold - GameState.Wallet.Copper;
            Check(feePaid == AuctionState.ListFee(AuctionTrade.TokenPrice) && feePaid > 0,
                $"증표 수수료 (실제 {feePaid})");
            Check(AuctionState.MineCount == 1, "내 등록 1건");
            Check(FirstMine() != null && FirstMine().Key == "SpecialJobToken",
                $"롯 키가 증표 (실제 {FirstMine()?.Key})");

            Check(AuctionTrade.TryFirstBag(out var first, out int qty)
                  && first == Economy.LifeItem.CraftHide && qty == 1,
                $"다음 가방은 가죽 (실제 {first} ×{qty})");

            Check(GameState.Bag.GetCount(Economy.LifeItem.RevivalTea) == 0, "부활초 없음");
            Check(!AuctionState.TryListItem(Economy.LifeItem.RevivalTea, 1, 40_000),
                "없는 부활초는 등록 실패");
            GameState.Gain(Economy.LifeItem.RevivalTea, 1);
            Check(AuctionState.TryListItem(Economy.LifeItem.RevivalTea, 1, 40_000), "부활초 등록");
            Check(AuctionState.MineCount == 2, "증표+부활초 2건");

            AuctionState.ForgetInMemoryForTest();
            Check(AuctionState.MineCount == 2, "재기동 뒤에도 증표 롯이 남는다");
            bool sawToken = false;
            foreach (var lot in AuctionState.Lots)
                if (!lot.Npc && lot.Key == "SpecialJobToken") sawToken = true;
            Check(sawToken, "재기동 증표 롯");

            GameState.ResetAll();
            AuctionState.ResetForTest();
            AuctionTrade.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            GameState.SetTowerFloorForTest(30);
            GameState.Grant(100_000);
            GameState.Gain(Economy.LifeItem.SpecialJobToken, 1);
            GameState.Gain(Economy.LifeItem.CraftHide, 1);
            Environment.SetEnvironmentVariable(AuctionTrade.EnvNo, "1");
            Check(AuctionTrade.Blocked, "QA_NO면 차단");
            Check(!AuctionTrade.CanList(Economy.LifeItem.SpecialJobToken), "차단하면 증표 거부");
            Check(AuctionTrade.CanList(Economy.LifeItem.CraftHide), "차단해도 가죽은 등록");
            Check(!AuctionState.TryListItem(Economy.LifeItem.SpecialJobToken, 1, AuctionTrade.TokenPrice),
                "차단하면 증표 등록 실패");
            Check(GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken) == 1, "차단은 증표를 안 뺀다");
            Check(AuctionState.TryListItem(Economy.LifeItem.CraftHide, 1, 2_400), "차단해도 가죽 등록");
            Check(AuctionTrade.TradeLine().Contains("옛"),
                $"차단 문구 (실제 {AuctionTrade.TradeLine()})");
            Environment.SetEnvironmentVariable(AuctionTrade.EnvNo, null);

            GameState.ResetAll();
            AuctionState.ResetForTest();
            AuctionTrade.ResetForTest();
            Environment.SetEnvironmentVariable(AuctionTrade.EnvShow, "1");
            AuctionTrade.SeedQaIfRequested();
            Check(GameState.TowerFloor >= 30, $"시드 30층 (실제 {GameState.TowerFloor})");
            Check(GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken) >= 1, "시드 증표");
            Check(EstateScreen.AuctionHubLockReason() == null, "시드면 경매장이 열린다");
            Check(AuctionTrade.TryFirstBag(out var seeded, out _)
                  && seeded == Economy.LifeItem.SpecialJobToken,
                $"시드 첫 가방 증표 (실제 {seeded})");
            Check(AuctionTrade.TradeLine().Contains("§12"), "시드 문구 §12");
            Environment.SetEnvironmentVariable(AuctionTrade.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string auction = File.ReadAllText(Path.Combine(runtime, "AuctionState.cs"));
            string estate = File.ReadAllText(Path.Combine(runtime, "EstateScreen.cs"));
            Check(auction.Contains("AuctionTrade.CanList"),
                "TryListItem이 CanList를 읽는다");
            Check(!auction.Contains("item == Economy.LifeItem.SpecialJobToken) return false"),
                "옛 증표 거절이 TryListItem에 없다");
            Check(estate.Contains("AuctionTrade.TradeLine")
                  && estate.Contains("AuctionTrade.TryFirstBag")
                  && estate.Contains("AuctionTrade.SeedQaIfRequested"),
                "영지가 문구·가방·시드를 읽는다");

            _ = nameof(AuctionTrade.CanList);
            _ = nameof(AuctionTrade.CanListBound);
            _ = nameof(AuctionTrade.TradeLine);
            _ = nameof(AuctionTrade.TryFirstBag);
            _ = nameof(AuctionState.TryListBound);
            _ = nameof(AuctionTrade.SeedQaIfRequested);

            Environment.SetEnvironmentVariable(AuctionTrade.EnvShow, show);
            Environment.SetEnvironmentVariable(AuctionTrade.EnvNo, no);
            Environment.SetEnvironmentVariable(AuctionState.EnvShow, fee);
            Environment.SetEnvironmentVariable(AuctionState.EnvShowBuyLock, buy);
            Environment.SetEnvironmentVariable(AuctionState.EnvShowExpire, expire);
            AuctionTrade.ResetForTest();
            AuctionState.ResetForTest();
            GameState.ResetAll();

            if (_fail > 0)
            {
                Debug.LogError("[AuctionTradeSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("AuctionTradeSelfCheck FAIL " + _fail);
            }
            Debug.Log("[AuctionTradeSelfCheck] PASS\n" + _log);
        }

        static AuctionState.Lot FirstMine()
        {
            foreach (var lot in AuctionState.Lots)
                if (!lot.Npc) return lot;
            return null;
        }
    }
}
