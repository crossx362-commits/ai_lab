using System;
using System.Text;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 대출 연체·파산이 경매장/침략 문을 실제로 잠그는가.
    /// 거래서버·침략 본게임·영지 생산·건물 레벨은 여기서 열지 않는다.
    /// </summary>
    public static class LoanSanctionSelfCheck
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
            string oldQa = Environment.GetEnvironmentVariable("QA_LOAN_OVERDUE");
            Environment.SetEnvironmentVariable("QA_LOAN_OVERDUE", null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            long now = 2_000_000L;
            const long Hour = 3600L;
            const long Term = Economy.LoanTermHours * Hour;

            Check(EstateScreen.AuctionUnlockFloor == WorldMapScreen.InvasionUnlockFloor
                  && EstateScreen.AuctionUnlockFloor == 30,
                "경매장·침략 해금 층이 같다(§12·§15)");
            Check(EstateScreen.AuctionHubLockReason(now) != null
                  && EstateScreen.AuctionHubLockReason(now).Contains("30층"),
                "1층이면 경매장이 층으로 잠긴다");
            Check(WorldMapScreen.InvasionHubLockReason(now) != null
                  && WorldMapScreen.InvasionHubLockReason(now).Contains("30층"),
                "1층이면 침략이 층으로 잠긴다");

            GameState.SetTowerFloorForTest(30);
            Check(EstateScreen.AuctionHubLockReason(now) == null, "30층·무부채면 경매장 문이 열린다");
            Check(WorldMapScreen.InvasionHubLockReason(now) == null, "30층·무연체면 침략 문이 열린다");

            GameState.Earn(100000);
            Check(GameState.Borrow(30000, now), "30층에서 한도까지 대출");
            Check(!GameState.CanUseAuction(now)
                  && GameState.AuctionBlockReason(now).Contains("부채"),
                "부채 보유 중 경매장 잠김(§18-5)");
            Check(GameState.CanInvade(now)
                  && WorldMapScreen.InvasionHubLockReason(now) == null,
                "연체 0이면 침략은 열린다(네거티브)");

            GameState.AccrueLoan(now + Term);
            long atDue = GameState.Debt;
            Check(GameState.OverdueCount == 0 && atDue == Economy.AccrueLoan(30000, 72),
                $"만기 정각은 연체 전·기본 금리 (부채 {atDue})");

            GameState.RefreshSanctions(now + Term + 1);
            Check(GameState.OverdueCount == 1, $"연체 1회 (실제 {GameState.OverdueCount})");
            Check(!GameState.CanUseAuction(now + Term + 1)
                  && GameState.AuctionBlockReason(now + Term + 1).Contains("연체"),
                "연체 1회면 경매장이 연체로 잠긴다(§12)");
            Check(GameState.CanInvade(now + Term + 1)
                  && WorldMapScreen.InvasionHubLockReason(now + Term + 1) == null,
                "연체 1회면 침략은 열린다(네거티브)");

            GameState.AccrueLoan(now + Term + Hour);
            long expect1p5 = Economy.AccrueLoan(atDue, 1, Economy.LoanOverdueInterestFactor);
            Check(GameState.Debt == expect1p5 && expect1p5 > atDue,
                $"연체 1회 이자는 ×1.5 (기대 {expect1p5}, 실제 {GameState.Debt})");

            GameState.RefreshSanctions(now + Term * 2 + 1);
            Check(GameState.OverdueCount == 2, $"연체 2회 (실제 {GameState.OverdueCount})");
            Check(!GameState.CanInvade(now + Term * 2 + 1)
                  && WorldMapScreen.InvasionHubLockReason(now + Term * 2 + 1).Contains("연체"),
                "연체 2회면 침략이 잠긴다(§18-5)");

            GameState.RefreshSanctions(now + Term * 3 + 1);
            Check(GameState.OverdueCount == 3, $"연체 3회 (실제 {GameState.OverdueCount})");
            Check(GameState.BankruptcyCount == 1, $"파산 1회 (실제 {GameState.BankruptcyCount})");
            Check(GameState.AuctionBanUntil == now + Term * 3 + 1
                  + Economy.LoanBankruptcyAuctionBanDays * 86400L,
                "파산하면 경매장 7일 정지");
            Check(GameState.AuctionBlockReason(now + Term * 3 + 1).Contains("파산"),
                "파산 사유가 경매장 잠금에 나온다");

            long repayAt = now + Term * 3 + 1;
            GameState.AccrueLoan(repayAt);
            GameState.Earn(GameState.Debt * 2);
            Check(GameState.Debt == 0 && GameState.OverdueCount == 0,
                $"상환 후 부채·연체 0 (부채 {GameState.Debt}, 연체 {GameState.OverdueCount})");
            Check(GameState.CanInvade(repayAt),
                "상환하면 침략이 다시 열린다");
            Check(!GameState.CanUseAuction(repayAt)
                  && GameState.AuctionBlockReason(repayAt).Contains("파산"),
                "상환해도 7일 정지는 남는다");
            Check(GameState.CanUseAuction(repayAt
                  + Economy.LoanBankruptcyAuctionBanDays * 86400L + 1),
                "7일이 지나면 경매장이 다시 열린다");

            Check(!GameState.Borrow(1000, repayAt),
                "재대출 유예 중에는 못 빌린다(§18-5)");
            long coolEnd = repayAt + Economy.LoanReloanCooldownDays * 86400L + 1;
            long half = Economy.LoanLimitCopper(GameState.Wallet.Copper, GameState.Tier, 1);
            Check(GameState.LoanLimit == half && half > 0 && half < GameState.Wallet.Copper * 3 / 10,
                $"파산 1회 한도가 30%보다 작다 (한도 {half}, 지갑 {GameState.Wallet.Copper})");
            Check(GameState.Borrow(half, coolEnd), "유예가 끝나면 절반 한도로 다시 빌린다");

            GameState.ForgetInMemoryForTest();
            Check(GameState.BankruptcyCount == 1 && GameState.Debt == half,
                "재기동 후에도 파산·부채가 남는다");

            GameState.ResetAll();
            GameState.SetTowerFloorForTest(30);
            GameState.Earn(100000);
            Check(GameState.Borrow(10000, now), "두 번째 시나리오 대출");
            GameState.RefreshSanctions(now + Term + 1);
            GameState.AccrueLoan(now + Term + 1);
            GameState.Earn(GameState.Debt * 2);
            Check(GameState.Debt == 0 && GameState.CanUseAuction(now + Term + 1)
                  && GameState.BankruptcyCount == 0,
                "연체 1회만 갚으면 경매장이 바로 열린다(파산 정지 없음)");

            Check(Economy.LoanInterestFactor(0, 0) == 1.0, "연체0·파산0 이자 배율 1");
            Check(Mathf.Approximately((float)Economy.LoanInterestFactor(1, 0), 1.5f),
                "연체1 이자 배율 1.5");

            Environment.SetEnvironmentVariable("QA_LOAN_OVERDUE", "2");
            GameState.ResetAll();
            Check(GameState.OverdueCount == 2 && GameState.TowerFloor >= 30,
                $"QA_LOAN_OVERDUE=2가 연체2·30층을 심는다 (연체 {GameState.OverdueCount}, 층 {GameState.TowerFloor})");
            Check(!GameState.CanInvade() && !GameState.CanUseAuction(),
                "QA 시드에서 경매·침략이 둘 다 잠긴다");
            Environment.SetEnvironmentVariable("QA_LOAN_OVERDUE", oldQa);

            GameState.ResetAll();
            Check(_fail == 0, $"전항 통과 (실패 {_fail})");
            Debug.Log(_log.ToString());
            if (_fail > 0)
                throw new InvalidOperationException($"[LoanSanctionSelfCheck] FAIL {_fail}\n{_log}");
            Debug.Log("[LoanSanctionSelfCheck] PASS");
        }
    }
}
