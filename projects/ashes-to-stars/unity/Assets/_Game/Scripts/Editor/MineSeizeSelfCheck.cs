using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>연체 2회면 영지 생산 100%가 빚으로 간다. QA_NO면 Earn 50%(§18-5).</summary>
    public static class MineSeizeSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Mine Seize Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(EstateMine.EnvShowSeize);
            string no = Environment.GetEnvironmentVariable(EstateMine.EnvNoSeize);
            Environment.SetEnvironmentVariable(EstateMine.EnvShowSeize, null);
            Environment.SetEnvironmentVariable(EstateMine.EnvNoSeize, null);
            Environment.SetEnvironmentVariable("QA_NO_MINE", null);
            Environment.SetEnvironmentVariable("QA_ESTATE_MINE", null);
            Environment.SetEnvironmentVariable(EstateMine.EnvShowRace, null);
            Environment.SetEnvironmentVariable(EstateMine.EnvNoRace, null);
            Environment.SetEnvironmentVariable("QA_LOAN_OVERDUE", null);

            GameState.ResetAll();
            WorldStar.ResetForTest();
            EstateMine.ResetForTest();
            RacePrefs.Set(RaceId.인간);

            long t1 = EstateMine.CopperPerHour();
            Check(t1 == 25L * Economy.COPPER_PER_SILVER, $"T1 광산 25실버 ({t1})");
            Check(EstateMine.SeizeOverdue == 2 && EstateMine.SeizePercent == 100,
                "연체 2회·압류 100%");
            Check(!EstateMine.Seized && EstateMine.SeizeLine().Contains("없음"),
                "빚 없으면 압류 없음");

            long now = 2_000_000L;
            const long Hour = 3600L;
            const long Term = Economy.LoanTermHours * Hour;

            GameState.SetTowerFloorForTest(30);
            Check(GameState.TrySelectTier(0), "T1 선택");
            GameState.Grant(100_000);
            Check(GameState.Borrow(30_000, now), "30층에서 대출");
            GameState.RefreshSanctions(now + Term + 1);
            Check(GameState.OverdueCount == 1, $"연체 1회 (실제 {GameState.OverdueCount})");
            long have = GameState.Wallet.Copper;
            if (have > 0) GameState.Pay(have);
            long debt1 = GameState.Debt;
            EstateMine.ResetForTest();
            EstateMine.NowUnix = () => now + Term + 1;
            Check(EstateMine.Tick() == 0, "연체1 첫 Tick은 기준점");
            now = now + Term + 1 + Hour;
            EstateMine.NowUnix = () => now;
            long half = EstateMine.Tick();
            Check(!EstateMine.Seized, "연체 1회는 압류가 아니다");
            Check(half == t1 && GameState.Wallet.Copper == t1 / 2,
                $"연체1 광산은 Earn 50% (소비 {half}, 지갑 {GameState.Wallet.Copper})");
            Check(GameState.Debt == debt1 - t1 / 2,
                $"연체1 빚도 50%만 ({GameState.Debt}, 기대 {debt1 - t1 / 2})");

            GameState.ResetAll();
            WorldStar.ResetForTest();
            EstateMine.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            now = 2_000_000L;
            GameState.SetTowerFloorForTest(30);
            Check(GameState.TrySelectTier(0), "연체2 T1");
            GameState.Grant(100_000);
            Check(GameState.Borrow(30_000, now), "연체2 시나리오 대출");
            GameState.RefreshSanctions(now + Term * 2 + 1);
            Check(GameState.OverdueCount == 2, $"연체 2회 (실제 {GameState.OverdueCount})");
            have = GameState.Wallet.Copper;
            if (have > 0) GameState.Pay(have);
            long debt2 = GameState.Debt;
            Check(debt2 >= 30_000 && GameState.Wallet.Copper == 0,
                $"지갑 0·빚 {debt2}");
            Check(EstateMine.Seized && EstateMine.SeizeLine().Contains("100%"),
                $"압류 문구 (실제 {EstateMine.SeizeLine()})");
            Check(EstateMine.SeizeLine().Contains("§18-5"), "문구 §18-5");

            EstateMine.ResetForTest();
            long tickAt = now + Term * 2 + 1;
            EstateMine.NowUnix = () => tickAt;
            Check(EstateMine.Tick() == 0, "연체2 첫 Tick은 기준점");
            tickAt += Hour;
            EstateMine.NowUnix = () => tickAt;
            long seized = EstateMine.Tick();
            Check(seized == t1, $"1시간 생산 전액이 소비됨 ({seized})");
            Check(GameState.Wallet.Copper == 0, $"지갑은 그대로 0 (실제 {GameState.Wallet.Copper})");
            Check(GameState.Debt == debt2 - t1,
                $"빚이 25실버 줄었다 ({GameState.Debt}, 기대 {debt2 - t1})");
            Check(EstateMine.LastSeized == t1, $"LastSeized {EstateMine.LastSeized}");
            Check(GameState.OverdueCount == 2, "빚이 남으면 연체 2회 유지");

            long hunt = GameState.Earn(10_000);
            Check(hunt == 10_000 && GameState.Wallet.Copper == 5_000,
                $"사냥 Earn은 여전히 50% (지갑 {GameState.Wallet.Copper})");

            GameState.ForgetInMemoryForTest();
            EstateMine.ForgetInMemoryForTest();
            Check(GameState.OverdueCount == 2 && GameState.Debt == debt2 - t1 - 5_000,
                $"재기동 뒤에도 연체2·빚 ({GameState.Debt})");
            Check(EstateMine.Seized, "재기동 뒤에도 압류");

            GameState.ResetAll();
            WorldStar.ResetForTest();
            EstateMine.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            now = 2_000_000L;
            GameState.SetTowerFloorForTest(30);
            GameState.TrySelectTier(0);
            GameState.Grant(100_000);
            GameState.Borrow(30_000, now);
            GameState.RefreshSanctions(now + Term * 2 + 1);
            have = GameState.Wallet.Copper;
            if (have > 0) GameState.Pay(have);
            GameState.RepayFromIncome(GameState.Debt - 1_000);
            Check(GameState.Debt == 1_000 && GameState.OverdueCount == 2,
                $"빚을 1000만 남긴다 (실제 {GameState.Debt})");
            EstateMine.ResetForTest();
            tickAt = now + Term * 2 + 1;
            EstateMine.NowUnix = () => tickAt;
            EstateMine.Tick();
            tickAt += Hour;
            EstateMine.NowUnix = () => tickAt;
            long leftover = EstateMine.Tick();
            Check(leftover == t1 && GameState.Debt == 0 && GameState.OverdueCount == 0,
                $"남은 생산은 지갑, 빚·연체 0 (소비 {leftover}, 지갑 {GameState.Wallet.Copper})");
            Check(GameState.Wallet.Copper == t1 - 1_000,
                $"지갑 1500 (실제 {GameState.Wallet.Copper})");

            GameState.ResetAll();
            WorldStar.ResetForTest();
            EstateMine.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            now = 2_000_000L;
            GameState.SetTowerFloorForTest(30);
            GameState.TrySelectTier(0);
            GameState.Grant(100_000);
            GameState.Borrow(30_000, now);
            GameState.RefreshSanctions(now + Term * 2 + 1);
            have = GameState.Wallet.Copper;
            if (have > 0) GameState.Pay(have);
            long cap = EstateBuild.WarehouseCapCopper();
            GameState.Wallet.TryAdd(cap);
            debt2 = GameState.Debt;
            EstateMine.ResetForTest();
            tickAt = now + Term * 2 + 1;
            EstateMine.NowUnix = () => tickAt;
            EstateMine.Tick();
            tickAt += Hour;
            EstateMine.NowUnix = () => tickAt;
            Check(EstateMine.Tick() == t1 && GameState.Wallet.Copper == cap,
                "창고가 가득이어도 압류는 간다");
            Check(GameState.Debt == debt2 - t1, $"가득 창고에서도 빚 −25실버 ({GameState.Debt})");
            Check(EstateMine.WastedCopper == 0, "압류분은 소멸이 아니다");

            Environment.SetEnvironmentVariable(EstateMine.EnvNoSeize, "1");
            GameState.ResetAll();
            WorldStar.ResetForTest();
            EstateMine.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            now = 2_000_000L;
            GameState.SetTowerFloorForTest(30);
            GameState.TrySelectTier(0);
            GameState.Grant(100_000);
            GameState.Borrow(30_000, now);
            GameState.RefreshSanctions(now + Term * 2 + 1);
            have = GameState.Wallet.Copper;
            if (have > 0) GameState.Pay(have);
            debt2 = GameState.Debt;
            Check(!EstateMine.Seized && EstateMine.SeizeLine().Contains("없음"),
                "QA_NO면 압류 없음");
            EstateMine.ResetForTest();
            tickAt = now + Term * 2 + 1;
            EstateMine.NowUnix = () => tickAt;
            EstateMine.Tick();
            tickAt += Hour;
            EstateMine.NowUnix = () => tickAt;
            Check(EstateMine.Tick() == t1 && GameState.Wallet.Copper == t1 / 2,
                $"차단하면 Earn 50% (지갑 {GameState.Wallet.Copper})");
            Check(GameState.Debt == debt2 - t1 / 2, "차단하면 빚도 50%만");
            Environment.SetEnvironmentVariable(EstateMine.EnvNoSeize, null);

            Environment.SetEnvironmentVariable(EstateMine.EnvShowSeize, "1");
            GameState.ResetAll();
            WorldStar.ResetForTest();
            EstateMine.ResetForTest();
            EstateMine.SeedSeizeQaIfRequested();
            Check(GameState.OverdueCount == 2, $"시드 연체 2 (실제 {GameState.OverdueCount})");
            Check(GameState.Wallet.Copper == 0, $"시드 지갑 0 (실제 {GameState.Wallet.Copper})");
            Check(GameState.Debt == 10_000 - t1, $"시드 빚 7500 (실제 {GameState.Debt})");
            Check(EstateMine.Seized && EstateMine.SeizeLine().Contains("100%"),
                $"시드 문구 (실제 {EstateMine.SeizeLine()})");
            Environment.SetEnvironmentVariable(EstateMine.EnvShowSeize, null);

            string mine = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/EstateMine.cs"));
            string estate = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/EstateScreen.cs"));
            Check(mine.Contains("RepayFromIncome"), "Tick이 RepayFromIncome을 읽는다");
            Check(estate.Contains("SeizeLine") && estate.Contains("EnvShowSeize"),
                "영지 현황이 압류 문구·시드를 읽는다");

            _ = nameof(GameState.RepayFromIncome);
            _ = nameof(GameState.SeedMineSeizeLoan);
            _ = nameof(EstateMine.Seized);
            _ = nameof(EstateMine.SeizeLine);
            _ = nameof(EstateMine.SeedSeizeQaIfRequested);

            Environment.SetEnvironmentVariable(EstateMine.EnvShowSeize, show);
            Environment.SetEnvironmentVariable(EstateMine.EnvNoSeize, no);
            WorldStar.ResetForTest();
            GameState.ResetAll();
            EstateMine.ResetForTest();

            if (_fail > 0)
            {
                Debug.LogError("[MineSeizeSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("MineSeizeSelfCheck FAIL " + _fail);
            }
            Debug.Log("[MineSeizeSelfCheck] PASS\n" + _log);
        }
    }
}
