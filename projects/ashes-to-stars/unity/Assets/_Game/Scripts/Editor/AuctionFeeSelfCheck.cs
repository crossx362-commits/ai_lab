using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>경매 수수료는 RaceDef.경매수수료를 읽는다. 인간 10%→7% · 나머지 10%(§3·§18-9).</summary>
    public static class AuctionFeeSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Auction Fee Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(AuctionState.EnvShow);
            string no = Environment.GetEnvironmentVariable(AuctionState.EnvNo);
            RaceId oldRace = RacePrefs.Get();
            float oldForce = AuctionState.ForceFeePercent;
            Environment.SetEnvironmentVariable(AuctionState.EnvShow, null);
            Environment.SetEnvironmentVariable(AuctionState.EnvNo, null);
            Environment.SetEnvironmentVariable("QA_LOAN_OVERDUE", null);
            AuctionState.ForceFeePercent = 0f;

            GameState.ResetAll();
            AuctionState.ResetForTest();

            RacePrefs.Set(RaceId.인간);
            Check(Math.Abs(AuctionState.FeePercent() - AuctionState.HumanFeePercent) < 1e-9,
                $"인간 총수수료 7 (실제 {AuctionState.FeePercent()})");
            Check(AuctionState.ListFee(10_000) == 140,
                $"인간 등록 1.4% = 140 (실제 {AuctionState.ListFee(10_000)})");
            Check(AuctionState.SaleFee(10_000) == 560,
                $"인간 체결 5.6% = 560 (실제 {AuctionState.SaleFee(10_000)})");
            Check(AuctionState.FeeLine().Contains("7%") && AuctionState.FeeLine().Contains("1.4%"),
                $"인간 문구 7%·1.4% (실제 {AuctionState.FeeLine()})");

            GameState.SetTowerFloorForTest(30);
            GameState.Earn(100_000);
            GameState.Gain(Economy.LifeItem.CraftHide, 2);
            long goldH = GameState.Wallet.Copper;
            Check(AuctionState.TryListItem(Economy.LifeItem.CraftHide, 1, 10_000), "인간 가죽 등록");
            Check(GameState.Wallet.Copper == goldH - 140,
                $"인간 등록이 140을 소각 (실제 {goldH - GameState.Wallet.Copper})");

            GameState.ResetAll();
            AuctionState.ResetForTest();
            RacePrefs.Set(RaceId.엘프);
            Check(Math.Abs(AuctionState.FeePercent() - AuctionState.DefaultFeePercent) < 1e-9,
                $"엘프 총수수료 10 (실제 {AuctionState.FeePercent()})");
            Check(AuctionState.ListFee(10_000) == 200,
                $"엘프 등록 2% = 200 (실제 {AuctionState.ListFee(10_000)})");
            Check(AuctionState.SaleFee(10_000) == 800,
                $"엘프 체결 8% = 800 (실제 {AuctionState.SaleFee(10_000)})");
            Check(AuctionState.FeeLine().Contains("2%") && AuctionState.FeeLine().Contains("8%"),
                $"엘프 문구 2%·8% (실제 {AuctionState.FeeLine()})");
            Check(!AuctionState.FeeLine().Contains("인간"),
                "엘프 문구에 인간이 안 나온다");

            GameState.SetTowerFloorForTest(30);
            GameState.Earn(100_000);
            GameState.Gain(Economy.LifeItem.CraftHide, 2);
            long goldE = GameState.Wallet.Copper;
            Check(AuctionState.TryListItem(Economy.LifeItem.CraftHide, 1, 10_000), "엘프 가죽 등록");
            Check(GameState.Wallet.Copper == goldE - 200,
                $"엘프 등록이 200을 소각 (실제 {goldE - GameState.Wallet.Copper})");

            GameState.ResetAll();
            AuctionState.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            Environment.SetEnvironmentVariable(AuctionState.EnvNo, "1");
            Check(Math.Abs(AuctionState.FeePercent() - AuctionState.DefaultFeePercent) < 1e-9,
                "QA_NO_AUCTION_FEE면 인간도 10");
            Check(AuctionState.ListFee(10_000) == 200, "차단하면 등록 200");
            Environment.SetEnvironmentVariable(AuctionState.EnvNo, null);

            GameState.ResetAll();
            AuctionState.ResetForTest();
            Environment.SetEnvironmentVariable(AuctionState.EnvShow, "1");
            AuctionState.SeedQaIfRequested();
            Check(RacePrefs.Get() == RaceId.인간, "시드는 인간을 고른다");
            Check(GameState.TowerFloor >= 30, $"시드 30층 (실제 {GameState.TowerFloor})");
            Check(GameState.Bag.GetCount(Economy.LifeItem.CraftHide) >= 1, "시드 가죽");
            Check(AuctionState.FeeLine().Contains("7%"), "시드 화면 문구 7%");
            Check(EstateScreen.AuctionHubLockReason() == null, "시드면 경매장이 열린다");
            Environment.SetEnvironmentVariable(AuctionState.EnvShow, null);

            RacePrefs.Set(RaceId.인간);
            AuctionState.ForgetInMemoryForTest();
            Check(AuctionState.ListFee(10_000) == 140, "재기동 뒤에도 인간 1.4%");

            _ = nameof(AuctionState.FeePercent);
            _ = nameof(AuctionState.FeeLine);
            _ = nameof(AuctionState.SeedQaIfRequested);
            _ = nameof(RaceDef.경매수수료);

            Environment.SetEnvironmentVariable(AuctionState.EnvShow, show);
            Environment.SetEnvironmentVariable(AuctionState.EnvNo, no);
            AuctionState.ForceFeePercent = oldForce;
            RacePrefs.Set(oldRace);
            GameState.ResetAll();
            AuctionState.ResetForTest();

            if (_fail > 0)
            {
                Debug.LogError("[AuctionFeeSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("AuctionFeeSelfCheck FAIL " + _fail);
            }
            Debug.Log("[AuctionFeeSelfCheck] PASS\n" + _log);
        }
    }
}
