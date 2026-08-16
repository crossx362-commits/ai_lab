using System;
using System.Text;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>로컬 경매장·침략 본게임이 문 안에서 실제로 도는가.</summary>
    public static class AuctionInvasionSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            Environment.SetEnvironmentVariable("QA_LOAN_OVERDUE", null);
            RaceId oldRace = RacePrefs.Get();
            RacePrefs.Set(RaceId.엘프);
            GameState.ResetAll();
            LifeSystem.ResetAll();
            AuctionState.ResetForTest();
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            WorldStar.ResetForTest();

            Check(EstateScreen.AuctionHubLockReason() != null,
                "1층이면 경매장이 층으로 잠긴다");
            GameState.SetTowerFloorForTest(30);
            Check(EstateScreen.AuctionHubLockReason() == null,
                "30층·무부채면 경매장이 열린다");

            GameState.Grant(100_000);
            Check(AuctionState.Lots.Count >= 4, $"NPC 장이 선다 (실제 {AuctionState.Lots.Count})");
            var npc = AuctionState.Lots[0];
            long before = GameState.Wallet.Copper;
            int stones = GameState.Bag.GetCount(Economy.LifeItem.EnhanceStone);
            Check(AuctionState.TryBuy(npc.Id), "NPC 강화석 구매");
            Check(GameState.Wallet.Copper == before - npc.Price, "구매가 지갑에서 빠진다");
            Check(GameState.Bag.GetCount(Economy.LifeItem.EnhanceStone) == stones + 1
                  || npc.Key != "EnhanceStone",
                "산 물건이 가방에 들어온다");

            GameState.Gain(Economy.LifeItem.CraftHide, 2);
            long fee = AuctionState.ListFee(2_400);
            long gold2 = GameState.Wallet.Copper;
            int hides = GameState.Bag.GetCount(Economy.LifeItem.CraftHide);
            Check(AuctionState.TryListItem(Economy.LifeItem.CraftHide, 1, 2_400), "가죽 등록");
            Check(GameState.Wallet.Copper == gold2 - fee, "등록 2%가 소각된다");
            Check(GameState.Bag.GetCount(Economy.LifeItem.CraftHide) == hides - 1, "등록이 가죽을 뺀다");
            Check(AuctionState.MineCount == 1, "내 등록 1건");
            string mineId = null;
            foreach (var L in AuctionState.Lots)
                if (!L.Npc) { mineId = L.Id; break; }
            Check(AuctionState.TryCancel(mineId), "등록 취소");
            Check(GameState.Bag.GetCount(Economy.LifeItem.CraftHide) == hides, "취소하면 가죽이 돌아온다");
            Check(GameState.Wallet.Copper == gold2 - fee, "취소해도 수수료는 안 돌아온다");

            GameState.Grant(100_000);
            Check(GameState.Borrow(10_000), "부채를 지면");
            Check(AuctionState.WhyCannotTrade() != null
                  && !AuctionState.TryListItem(Economy.LifeItem.CraftHide, 1, 2_400),
                "부채 중에는 등록·구매가 거부된다(§18-5)");
            GameState.Grant(GameState.Debt * 2);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            AuctionState.ResetForTest();
            InvasionState.ResetForTest();
            GameState.SetTowerFloorForTest(30);
            Check(!InvasionState.TryBegin(), "골드 0이면 출정 비용에서 거부");
            GameState.Grant(200_000);
            long sortie = InvasionState.SortieCost();
            long gold3 = GameState.Wallet.Copper;
            Check(InvasionState.TryBegin(), "출정 비용을 내고 침략이 시작된다");
            Check(GameState.Wallet.Copper == gold3 - sortie, "출정 비용 차감");
            Check(InvasionState.Pending, "침략 대기");
            long loot = InvasionState.Settle(true);
            Check(loot > 0 && !InvasionState.Pending, "승리 약탈이 정산된다");
            Check(GameState.Wallet.Copper == gold3 - sortie + loot, "약탈이 지갑에 들어온다");
            Check(InvasionState.ShieldActive, "정산 직후 보호막 12시간(§15)");
            Check(!InvasionState.TryBegin(), "보호막 중 재출정 거부");

            long t0 = InvasionState.NowUnix();
            InvasionState.NowUnix = () => t0 + InvasionState.GuardSeconds + 1;
            Check(!InvasionState.ShieldActive, "12시간 뒤 보호막이 끝난다");
            Check(InvasionState.TryBegin(), "보호막이 끝나면 두 번째 출정");
            long gold4 = GameState.Wallet.Copper;
            InvasionState.Settle(false);
            Check(GameState.Wallet.Copper == gold4 - InvasionState.DefeatCost()
                  || GameState.Wallet.Copper <= gold4,
                "패배 시 추가 소모");
            InvasionState.NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            Environment.SetEnvironmentVariable("QA_LOAN_OVERDUE", "2");
            GameState.ResetAll();
            Check(!GameFlow.TryGoInvasion() && !InvasionState.TryBegin(),
                "연체 2회면 침략 본게임에 못 들어간다");
            Environment.SetEnvironmentVariable("QA_LOAN_OVERDUE", null);

            RacePrefs.Set(oldRace);
            GameState.ResetAll();
            Check(_fail == 0, $"전항 통과 (실패 {_fail})");
            Debug.Log(_log.ToString());
            if (_fail > 0)
                throw new InvalidOperationException($"[AuctionInvasionSelfCheck] FAIL {_fail}\n{_log}");
            Debug.Log("[AuctionInvasionSelfCheck] PASS");
        }
    }
}
