using System;
using System.Text;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>건설 단축 — 골드 15%/h · 재료 2% · 남은 시간의 50% 상한(§13-2·§18-12).</summary>
    public static class EstateRushSelfCheck
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
            Environment.SetEnvironmentVariable("QA_NO_RUSH", null);
            Environment.SetEnvironmentVariable("QA_ESTATE_RUSH", null);
            Environment.SetEnvironmentVariable("QA_ESTATE_KEEP_FAST", null);
            Environment.SetEnvironmentVariable("QA_ESTATE_DEFENSE_FAST", null);
            GameState.ResetAll();

            Check(Math.Abs(EstateRush.GoldPerHourShare - 0.15) < 1e-9, "골드는 시간당 비용의 15%");
            Check(Math.Abs(EstateRush.MaterialShare - 0.02) < 1e-9, "재료 1개는 남은 시간의 2%");
            Check(Math.Abs(EstateRush.RemainFloor - 0.5) < 1e-9, "상한은 남은 원 시간의 50%");
            Check(EstateRush.FloorRemain(300) == 150, "5분 공사는 최소 150초");
            Check(EstateRush.FloorRemain(86400) == 43200, "24시간은 최소 12시간");
            Check(EstateRush.Rushable(300, 300) == 150, "시작 직후 150초까지 당길 수 있다");
            Check(EstateRush.Rushable(150, 300) == 0, "바닥에 닿으면 0");
            Check(EstateRush.Rushable(200, 300) == 50, "200초가 남으면 50초만");
            Check(EstateRush.GoldCost(3600, 100_000) == 15_000, "1시간 = 비용의 15%");
            Check(EstateRush.GoldCost(150, 120_000) == 750, "본성 1→2 150초 = 750쿠퍼");
            Check(EstateRush.MaterialCut(300, 1) == 6, "300초의 2% = 6초");
            Check(EstateRush.MaterialCut(300, 25) == 150, "25장이면 150초");
            Check(EstateRush.IsFamilyMaterial(Economy.LifeItem.CraftHide), "가죽은 계열 재료");
            Check(EstateRush.IsFamilyMaterial(Economy.LifeItem.CraftDemonite), "마정석도 계열");
            Check(!EstateRush.IsFamilyMaterial(Economy.LifeItem.EnhanceStone), "강화석은 단축 재료 아님");
            Check(EstateRush.IsForbidden(Economy.LifeItem.RevivalTea), "부활초 금지");
            Check(EstateRush.IsForbidden(Economy.LifeItem.RebornStone), "환생석 금지");
            Check(EstateRush.IsForbidden(Economy.LifeItem.ScrollOfReturn), "두루마리 금지");

            long now = 1_700_000_000;
            EstateBuild.ResetForTest();
            EstateBuild.NowUnix = () => now;
            GameState.Earn(EstateBuild.UpgradeCost(1));
            Check(EstateBuild.TryStartKeep(), "본성 공사 시작");
            Check(EstateBuild.RemainingSeconds() == 300, "본성 1→2는 300초");
            Check(EstateBuild.RushableSeconds() == 150, "단축 가능 150초");
            Check(EstateBuild.GoldCostToFloor() == 750, "바닥까지 골드 750");
            Check(EstateBuild.WhyCannotRushGold() != null, "공사비만 내면 단축 골드가 없다");

            GameState.Earn(750);
            long gold = GameState.Wallet.Copper;
            Check(EstateBuild.WhyCannotRushGold() == null, "골드만 있으면 단축 가능");
            Check(EstateBuild.TryRushGold(), "골드로 150초를 당긴다");
            Check(EstateBuild.RemainingSeconds() == 150, "단축 뒤 150초가 남는다");
            Check(GameState.Wallet.Copper == gold - 750, "750쿠퍼가 빠진다");
            Check(EstateBuild.KeepLevel == 1 && EstateBuild.KeepBusy, "레벨은 그대로다");
            Check(!EstateBuild.TryRushGold(), "바닥에서 골드 단축 거부");
            Check(GameState.Wallet.Copper == gold - 750, "거부면 골드 불변");

            EstateBuild.ResetForTest();
            GameState.ResetAll();
            EstateBuild.NowUnix = () => now;
            GameState.Earn(EstateBuild.UpgradeCost(1));
            Check(EstateBuild.TryStartKeep(), "재료 단축용 공사");
            GameState.Gain(Economy.LifeItem.CraftHide, 1);
            Check(EstateBuild.TryRushMaterial(Economy.LifeItem.CraftHide, 1), "가죽 1장");
            Check(EstateBuild.RemainingSeconds() == 294, "2% = 6초");
            Check(GameState.Bag.GetCount(Economy.LifeItem.CraftHide) == 0, "가죽이 빠진다");

            GameState.Gain(Economy.LifeItem.RevivalTea, 1);
            Check(!EstateBuild.TryRushMaterial(Economy.LifeItem.RevivalTea, 1), "부활초 거부");
            Check(GameState.Bag.GetCount(Economy.LifeItem.RevivalTea) == 1, "부활초 미소모");
            GameState.Gain(Economy.LifeItem.EnhanceStone, 1);
            Check(!EstateBuild.TryRushMaterial(Economy.LifeItem.EnhanceStone, 1), "강화석 거부");
            Check(GameState.Bag.GetCount(Economy.LifeItem.EnhanceStone) == 1, "강화석 미소모");

            EstateBuild.ResetForTest();
            GameState.ResetAll();
            EstateBuild.NowUnix = () => now;
            GameState.Earn(EstateBuild.UpgradeCost(1));
            EstateBuild.TryStartKeep();
            now += 160;
            Check(EstateBuild.RemainingSeconds() == 140, "160초가 지나면 140초");
            Check(EstateBuild.RushableSeconds() == 0, "이미 바닥 아래라 단축 0");
            Check(!EstateBuild.TryRushGold(), "기다린 뒤에는 골드 단축 거부");

            EstateBuild.ResetForTest();
            GameState.ResetAll();
            EstateBuild.NowUnix = () => now;
            GameState.Earn(EstateBuild.UpgradeCost(1));
            EstateBuild.TryStartKeep();
            GameState.Earn(750);
            EstateBuild.TryRushGold();
            long left = EstateBuild.RemainingSeconds();
            GameState.ForgetInMemoryForTest();
            EstateBuild.ForgetInMemoryForTest();
            EstateBuild.NowUnix = () => now;
            Check(EstateBuild.RemainingSeconds() == left && left == 150,
                "재기동 뒤에도 단축이 남는다");

            Environment.SetEnvironmentVariable("QA_NO_RUSH", "1");
            EstateBuild.ResetForTest();
            GameState.ResetAll();
            EstateBuild.NowUnix = () => now;
            GameState.Earn(EstateBuild.UpgradeCost(1) + 10_000);
            EstateBuild.TryStartKeep();
            Check(!EstateBuild.TryRushGold(), "QA_NO_RUSH=1이면 골드 거부");
            Check(EstateBuild.RemainingSeconds() == 300, "남은 시간 불변");
            Environment.SetEnvironmentVariable("QA_NO_RUSH", null);

            GameState.ResetAll();
            EstateDefense.ResetForTest();
            EstateBuild.ResetForTest();
            EstateDefense.NowUnix = () => now;
            GameState.SetTowerFloorForTest(20);
            GameState.Earn(EstateDefense.UpgradeCost(0));
            Check(EstateDefense.TryStart(EstateDefense.Kind.화살탑), "화살탑 공사");
            Check(Math.Abs(EstateDefense.RemainingSeconds() - 120) <= 1, "화살탑 1→는 120초");
            long defRush = EstateDefense.RushableSeconds();
            Check(defRush == 60, "화살탑 단축 가능 60초");
            long defPay = EstateDefense.GoldCostToFloor();
            GameState.Earn(defPay);
            Check(EstateDefense.TryRushGold(), "방어도 같은 단축을 쓴다");
            Check(EstateDefense.RemainingSeconds() == 60, "방어도 50%가 남는다");
            Check(!EstateDefense.TryRushGold(), "방어도 바닥에서 거부");

            GameState.ResetAll();
            EstateBuild.ResetForTest();
            Environment.SetEnvironmentVariable("QA_ESTATE_RUSH", "1");
            EstateBuild.SeedRushQaIfRequested();
            Check(EstateBuild.KeepBusy && EstateBuild.RushableSeconds() == 150,
                "QA 시드는 본성 공사 중·단축 가능");
            Check(GameState.Wallet.Copper >= EstateBuild.GoldCostToFloor(),
                "QA 시드에 골드 단축 비용이 있다");
            Check(GameState.Bag.GetCount(Economy.LifeItem.CraftHide) >= 1,
                "QA 시드에 가죽이 있다");
            Environment.SetEnvironmentVariable("QA_ESTATE_RUSH", null);

            _ = nameof(EstateRush.GoldCost);
            _ = nameof(EstateRush.MaterialCut);
            _ = nameof(EstateBuild.TryRushGold);
            _ = nameof(EstateBuild.TryRushMaterial);
            _ = nameof(EstateDefense.TryRushGold);
            _ = nameof(EstateDefense.TryRushMaterial);

            GameState.ResetAll();
            if (_fail > 0)
            {
                Debug.LogError("[EstateRushSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("EstateRushSelfCheck FAIL " + _fail);
            }
            Debug.Log("[EstateRushSelfCheck] PASS\n" + _log);
        }
    }
}
