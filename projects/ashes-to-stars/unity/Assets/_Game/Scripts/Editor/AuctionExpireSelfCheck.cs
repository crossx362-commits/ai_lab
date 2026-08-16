using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>경매 등록 24시간 뒤 유찰. 물건은 돌아오고 수수료는 소각. QA_NO면 만료 안 함(§18-3).</summary>
    public static class AuctionExpireSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Auction Expire Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(AuctionState.EnvShowExpire);
            string no = Environment.GetEnvironmentVariable(AuctionState.EnvNoExpire);
            string fee = Environment.GetEnvironmentVariable(AuctionState.EnvShow);
            string buy = Environment.GetEnvironmentVariable(AuctionState.EnvShowBuyLock);
            Environment.SetEnvironmentVariable(AuctionState.EnvShowExpire, null);
            Environment.SetEnvironmentVariable(AuctionState.EnvNoExpire, null);
            Environment.SetEnvironmentVariable(AuctionState.EnvShow, null);
            Environment.SetEnvironmentVariable(AuctionState.EnvShowBuyLock, null);
            Environment.SetEnvironmentVariable("QA_LOAN_OVERDUE", null);

            GameState.ResetAll();
            AuctionState.ResetForTest();
            LifeSystem.ResetAll();
            RacePrefs.Set(RaceId.인간);

            Check(AuctionState.ListHours == 24, "등록 24시간");
            Check(AuctionState.ExpireSeconds == 24 * 3600L, "유찰 초 = 24시간");
            Check(AuctionState.ExpireLine().Contains("24시간")
                  && AuctionState.ExpireLine().Contains("§18-3"),
                $"문구 24시간·§18-3 (실제 {AuctionState.ExpireLine()})");

            GameState.SetTowerFloorForTest(30);
            GameState.Grant(100_000);
            GameState.Gain(Economy.LifeItem.CraftHide, 2);
            long gold = GameState.Wallet.Copper;
            int hides = GameState.Bag.GetCount(Economy.LifeItem.CraftHide);
            Check(AuctionState.TryListItem(Economy.LifeItem.CraftHide, 1, 2_400), "가죽 등록");
            long feePaid = gold - GameState.Wallet.Copper;
            Check(feePaid == AuctionState.ListFee(2_400) && feePaid > 0, "등록 수수료가 빠진다");
            Check(GameState.Bag.GetCount(Economy.LifeItem.CraftHide) == hides - 1, "등록이 가죽을 뺀다");
            Check(AuctionState.MineCount == 1, "내 등록 1건");
            Check(AuctionState.MineLine().Contains("1/10")
                  && AuctionState.MineLine().Contains("24시간"),
                $"내 등록 줄 1/10·24시간 (실제 {AuctionState.MineLine()})");

            AuctionState.Lot mine = FirstMine();
            Check(mine != null && mine.Until > 0, "Until이 있다");
            long until = mine != null ? mine.Until : 0;
            Check(AuctionState.LotTimeLine(mine, until - AuctionState.ExpireSeconds).Contains("24시간"),
                $"직후 남은 24시간 (실제 {AuctionState.LotTimeLine(mine, until - AuctionState.ExpireSeconds)})");

            AuctionState.NowUnix = () => until - 1;
            Check(AuctionState.MineCount == 1, "만료 1초 전 유지");
            Check(GameState.Bag.GetCount(Economy.LifeItem.CraftHide) == hides - 1,
                "만료 1초 전 가죽은 장에 있다");

            AuctionState.NowUnix = () => until + 1;
            Check(AuctionState.MineCount == 0, "Lots/MineCount가 유찰을 읽는다");
            Check(FirstMine() == null, "유찰 뒤 내 등록이 없다");
            Check(GameState.Bag.GetCount(Economy.LifeItem.CraftHide) == hides,
                "유찰하면 가죽이 돌아온다");
            Check(GameState.Wallet.Copper == gold - feePaid, "유찰해도 수수료는 안 돌아온다");
            Check(AuctionState.MineLine().Contains("0/10"),
                $"유찰 뒤 0/10 (실제 {AuctionState.MineLine()})");

            AuctionState.NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            GameState.ResetAll();
            AuctionState.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            GameState.SetTowerFloorForTest(30);
            GameState.Grant(100_000);
            GameState.Gain(Economy.LifeItem.CraftHide, 2);
            Check(AuctionState.TryListItem(Economy.LifeItem.CraftHide, 1, 2_400), "재기동 검사용 등록");
            mine = FirstMine();
            until = mine != null ? mine.Until : 0;
            AuctionState.ForgetInMemoryForTest();
            AuctionState.NowUnix = () => until + 1;
            Check(AuctionState.MineCount == 0, "재기동 뒤에도 유찰을 읽는다");
            Check(GameState.Bag.GetCount(Economy.LifeItem.CraftHide) == 2,
                "재기동 유찰도 가죽을 되돌린다");

            AuctionState.NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            GameState.ResetAll();
            AuctionState.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            GameState.SetTowerFloorForTest(30);
            GameState.Grant(100_000);
            GameState.Gain(Economy.LifeItem.CraftHide, 2);
            Check(AuctionState.TryListItem(Economy.LifeItem.CraftHide, 1, 2_400), "차단 검사용 등록");
            mine = FirstMine();
            until = mine != null ? mine.Until : 0;
            Environment.SetEnvironmentVariable(AuctionState.EnvNoExpire, "1");
            Check(AuctionState.ExpireBlocked, "QA_NO면 차단");
            AuctionState.NowUnix = () => until + 1;
            Check(AuctionState.MineCount == 1, "차단하면 24시간+1초에도 유지");
            Check(GameState.Bag.GetCount(Economy.LifeItem.CraftHide) == 1,
                "차단하면 가죽이 안 돌아온다");
            Environment.SetEnvironmentVariable(AuctionState.EnvNoExpire, null);
            Check(AuctionState.MineCount == 0, "차단을 끄면 바로 유찰");

            AuctionState.NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            GameState.ResetAll();
            AuctionState.ResetForTest();
            Environment.SetEnvironmentVariable(AuctionState.EnvShowExpire, "1");
            AuctionState.SeedExpireQaIfRequested();
            Check(GameState.TowerFloor >= 30, $"시드 30층 (실제 {GameState.TowerFloor})");
            Check(AuctionState.MineCount == 1, "시드 내 등록 1건");
            Check(AuctionState.MineLine().Contains("24시간"), "시드 화면 문구 24시간");
            Check(EstateScreen.AuctionHubLockReason() == null, "시드면 경매장이 열린다");
            Environment.SetEnvironmentVariable(AuctionState.EnvShowExpire, null);

            _ = nameof(AuctionState.SweepExpired);
            _ = nameof(AuctionState.ExpireLine);
            _ = nameof(AuctionState.MineLine);
            _ = nameof(AuctionState.LotTimeLine);
            _ = nameof(AuctionState.SeedExpireQaIfRequested);

            Environment.SetEnvironmentVariable(AuctionState.EnvShowExpire, show);
            Environment.SetEnvironmentVariable(AuctionState.EnvNoExpire, no);
            Environment.SetEnvironmentVariable(AuctionState.EnvShow, fee);
            Environment.SetEnvironmentVariable(AuctionState.EnvShowBuyLock, buy);
            AuctionState.ResetForTest();
            GameState.ResetAll();

            if (_fail > 0)
            {
                Debug.LogError("[AuctionExpireSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("AuctionExpireSelfCheck FAIL " + _fail);
            }
            Debug.Log("[AuctionExpireSelfCheck] PASS\n" + _log);
        }

        static AuctionState.Lot FirstMine()
        {
            foreach (var lot in AuctionState.Lots)
                if (!lot.Npc) return lot;
            return null;
        }
    }
}
