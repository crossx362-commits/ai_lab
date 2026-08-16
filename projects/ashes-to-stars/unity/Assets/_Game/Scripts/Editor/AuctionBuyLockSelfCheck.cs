using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>경매장은 해금 후 7일 판매만·구매 불가. QA_NO면 구매가 열린다(§18-3·§18-14).</summary>
    public static class AuctionBuyLockSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Auction Buy Lock Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(AuctionState.EnvShowBuyLock);
            string no = Environment.GetEnvironmentVariable(AuctionState.EnvNoBuyLock);
            string fee = Environment.GetEnvironmentVariable(AuctionState.EnvShow);
            Environment.SetEnvironmentVariable(AuctionState.EnvShowBuyLock, null);
            Environment.SetEnvironmentVariable(AuctionState.EnvNoBuyLock, null);
            Environment.SetEnvironmentVariable(AuctionState.EnvShow, null);
            Environment.SetEnvironmentVariable("QA_LOAN_OVERDUE", null);

            GameState.ResetAll();
            AuctionState.ResetForTest();
            LifeSystem.ResetAll();
            RacePrefs.Set(RaceId.인간);

            Check(AuctionState.BuyLockDays == 7, "잠금 7일");
            Check(AuctionState.BuyLockSeconds == 7 * 24 * 3600, "잠금 초 = 7일");
            Check(AuctionState.CanBuy(), "시계 없으면 구매 가능(옛 저장 호환)");
            Check(string.IsNullOrEmpty(AuctionState.BuyLockLine()), "시계 없으면 문구 없음");

            GameState.SetTowerFloorForTest(EstateScreen.AuctionUnlockFloor);
            GameState.Grant(100_000);
            Check(AuctionState.CanBuy(), "SetTowerFloor는 시계를 안 찍는다");
            var npc = FirstNpc();
            Check(npc != null, "NPC 장이 선다");
            Check(AuctionState.TryBuy(npc.Id), "시계 없으면 NPC 구매");

            GameState.ResetAll();
            AuctionState.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            GameState.SetTowerFloorForTest(29);
            GameState.Grant(100_000);
            GameState.Gain(Economy.LifeItem.CraftHide, 2);
            Check(GameState.TowerFloor == 29, $"Clear 전 29층 (실제 {GameState.TowerFloor})");
            GameState.ClearFloor(29);
            Check(GameState.TowerFloor >= 30, $"29층 클리어면 30층 (실제 {GameState.TowerFloor})");
            Check(AuctionState.OpenedAt > 0, $"ClearFloor가 시계를 찍는다 (실제 {AuctionState.OpenedAt})");
            Check(!AuctionState.CanBuy(), "해금 직후 구매 잠금");
            Check(AuctionState.BuyLockLeft() > AuctionState.BuyLockSeconds - 5,
                $"남은 초 ≈ 7일 (실제 {AuctionState.BuyLockLeft()})");
            Check(AuctionState.BuyLockLine().Contains("7일")
                  && AuctionState.BuyLockLine().Contains("§18-3"),
                $"문구 7일·§18-3 (실제 {AuctionState.BuyLockLine()})");
            Check(EstateScreen.AuctionHubLockReason() == null, "허브 문은 열린다");
            npc = FirstNpc();
            Check(npc != null && !AuctionState.TryBuy(npc.Id), "잠금 중 NPC 구매 거부");
            Check(AuctionState.WhyCannotBuy() != null
                  && AuctionState.WhyCannotBuy().Contains("구매 잠금"),
                $"WhyCannotBuy 잠금 (실제 {AuctionState.WhyCannotBuy()})");
            long gold = GameState.Wallet.Copper;
            int hides = GameState.Bag.GetCount(Economy.LifeItem.CraftHide);
            Check(AuctionState.TryListItem(Economy.LifeItem.CraftHide, 1, 2_400),
                "잠금 중에도 판매(등록)는 된다");
            Check(GameState.Wallet.Copper < gold, "등록 수수료는 빠진다");
            Check(GameState.Bag.GetCount(Economy.LifeItem.CraftHide) == hides - 1, "등록이 가죽을 뺀다");

            AuctionState.ForgetInMemoryForTest();
            Check(!AuctionState.CanBuy(), "재기동 뒤에도 잠금");
            Check(AuctionState.BuyLockLine().Contains("7일"), "재기동 문구 유지");

            long opened = AuctionState.OpenedAt;
            AuctionState.NowUnix = () => opened + AuctionState.BuyLockSeconds - 1;
            Check(!AuctionState.CanBuy(), "만료 1초 전 거부");
            AuctionState.NowUnix = () => opened + AuctionState.BuyLockSeconds + 1;
            Check(AuctionState.CanBuy(), "만료 1초 후 구매 가능");
            Check(string.IsNullOrEmpty(AuctionState.BuyLockLine()), "만료 뒤 문구 없음");
            npc = FirstNpc();
            Check(npc != null && AuctionState.TryBuy(npc.Id), "만료 뒤 NPC 구매");
            AuctionState.NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            GameState.ResetAll();
            AuctionState.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            GameState.SetTowerFloorForTest(30);
            GameState.Grant(100_000);
            AuctionState.SetOpenedAtForTest(AuctionState.NowUnix());
            Environment.SetEnvironmentVariable(AuctionState.EnvNoBuyLock, "1");
            Check(AuctionState.BuyLockBlocked, "QA_NO면 차단");
            Check(AuctionState.CanBuy(), "차단하면 잠금 0");
            npc = FirstNpc();
            Check(npc != null && AuctionState.TryBuy(npc.Id), "차단하면 구매");
            Environment.SetEnvironmentVariable(AuctionState.EnvNoBuyLock, null);

            GameState.ResetAll();
            AuctionState.ResetForTest();
            Environment.SetEnvironmentVariable(AuctionState.EnvShowBuyLock, "1");
            AuctionState.SeedBuyLockQaIfRequested();
            Check(GameState.TowerFloor >= 30, $"시드 30층 (실제 {GameState.TowerFloor})");
            Check(!AuctionState.CanBuy(), "시드는 잠금");
            Check(AuctionState.BuyLockLine().Contains("7일"), "시드 화면 문구 7일");
            Check(EstateScreen.AuctionHubLockReason() == null, "시드면 경매장이 열린다");
            Environment.SetEnvironmentVariable(AuctionState.EnvShowBuyLock, null);

            _ = nameof(AuctionState.NoteUnlock);
            _ = nameof(AuctionState.CanBuy);
            _ = nameof(AuctionState.BuyLockLine);
            _ = nameof(AuctionState.WhyCannotBuy);
            _ = nameof(AuctionState.SeedBuyLockQaIfRequested);
            _ = nameof(AuctionState.TryBuy);

            Environment.SetEnvironmentVariable(AuctionState.EnvShowBuyLock, show);
            Environment.SetEnvironmentVariable(AuctionState.EnvNoBuyLock, no);
            Environment.SetEnvironmentVariable(AuctionState.EnvShow, fee);
            AuctionState.ResetForTest();
            GameState.ResetAll();

            if (_fail > 0)
            {
                Debug.LogError("[AuctionBuyLockSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("AuctionBuyLockSelfCheck FAIL " + _fail);
            }
            Debug.Log("[AuctionBuyLockSelfCheck] PASS\n" + _log);
        }

        static AuctionState.Lot FirstNpc()
        {
            foreach (var lot in AuctionState.Lots)
                if (lot.Npc) return lot;
            return null;
        }
    }
}
