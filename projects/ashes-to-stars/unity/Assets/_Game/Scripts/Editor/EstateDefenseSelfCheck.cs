using System;
using System.Text;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>방어 건물 4종이 §13-2·§18-12·§13-5와 같은지.</summary>
    public static class EstateDefenseSelfCheck
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
            Environment.SetEnvironmentVariable("QA_ESTATE_DEFENSE", null);
            Environment.SetEnvironmentVariable("QA_ESTATE_DEFENSE_FAST", null);
            Environment.SetEnvironmentVariable("QA_NO_DEFENSE", null);
            EstateDefense.ResetForTest();
            EstateBuild.ResetForTest();
            DefenseState.ResetForTest();
            WorldStar.ResetForTest();
            GameState.ResetAll();

            Check(Math.Abs(EstateDefense.UpgradeSeconds(1) - 120.0) < 0.01,
                "1→2는 본성 5분의 40% = 2분(§18-12)");
            Check(Math.Abs(EstateDefense.UpgradeSeconds(3) - EstateBuild.UpgradeSeconds(3) * 0.4) < 0.01,
                "3→4도 본성의 40%");
            Check(EstateDefense.UpgradeCost(1) == EstateBuild.UpgradeCost(1) * 40 / 100,
                "비용도 본성의 40%");
            Check(EstateDefense.Level(EstateDefense.Kind.화살탑) == 0, "시작은 0");

            Check(!EstateDefense.TryStart(EstateDefense.Kind.화살탑),
                "19층·골드 없으면 안 선다");
            GameState.SetTowerFloorForTest(19);
            GameState.Grant(EstateDefense.UpgradeCost(1));
            Check(!EstateDefense.TryStart(EstateDefense.Kind.화살탑),
                "19층은 거부(20층부터)");
            GameState.SetTowerFloorForTest(20);
            Check(!EstateDefense.TryStart(EstateDefense.Kind.마법탑),
                "화살탑 없이 마법탑은 거부(순차)");

            long gold = GameState.Wallet.Copper;
            long now = 1_700_000_000;
            EstateDefense.NowUnix = () => now;
            Check(EstateDefense.TryStart(EstateDefense.Kind.화살탑), "20층·비용을 내면 화살탑이 시작한다");
            Check(GameState.Wallet.Copper == gold - EstateDefense.UpgradeCost(1), "공사비가 빠진다");
            Check(EstateDefense.Busy && EstateDefense.Level(EstateDefense.Kind.화살탑) == 0,
                "끝나기 전엔 레벨이 그대로다");
            Check(!EstateDefense.TryStart(EstateDefense.Kind.화살탑), "공사 중엔 다시 못 올린다");
            now += 119;
            Check(EstateDefense.Level(EstateDefense.Kind.화살탑) == 0, "119초엔 아직 0");
            now += 2;
            Check(EstateDefense.Level(EstateDefense.Kind.화살탑) == 1, "시간이 되면 수령 없이 1이 된다");
            Check(!EstateDefense.Busy, "끝나면 슬롯이 비한다");

            Check(EstateDefense.TryStart(EstateDefense.Kind.마법탑) == false
                  || EstateDefense.WhyCannotStart(EstateDefense.Kind.마법탑) == null,
                "화살탑 1이면 마법탑 사유가 순차가 아니다");
            GameState.Grant(EstateDefense.UpgradeCost(0));
            Check(EstateDefense.TryStart(EstateDefense.Kind.마법탑), "화살탑 다음에 마법탑");
            now += 200;
            Check(EstateDefense.Level(EstateDefense.Kind.마법탑) == 1, "마법탑도 자동 적용");
            Check(!EstateDefense.TryStart(EstateDefense.Kind.함정), "성벽 없이 함정은 거부");

            Check(!EstateDefense.TryStart(EstateDefense.Kind.화살탑),
                "본성 1이면 화살탑 2는 거부");

            EstateDefense.GarrisonCount = () => 1;
            Check(EstateDefense.EfficiencyPercent() == 100, "수비가 있으면 효율 100");
            Check(EstateDefense.CutPercent() == 10, "화살탑1+마법탑1·수비 = 약탈 -10%");
            EstateDefense.GarrisonCount = () => 0;
            Check(EstateDefense.EfficiencyPercent() == 50, "수비 0이면 효율 50(§13-5)");
            Check(EstateDefense.CutPercent() == 5, "같은 건물이 수비 없으면 -5%");
            Check(EstateDefense.ApplyToLoot(1000) == 950, "1000 약탈이 950으로 줄어든다");

            Environment.SetEnvironmentVariable("QA_NO_DEFENSE", "1");
            Check(EstateDefense.CutPercent() == 0 && EstateDefense.ApplyToLoot(1000) == 1000,
                "QA_NO_DEFENSE면 감소 0");
            Environment.SetEnvironmentVariable("QA_NO_DEFENSE", null);

            long raw = 10_000;
            EstateDefense.GarrisonCount = () => 1;
            long guarded = EstateDefense.ApplyToLoot(raw);
            EstateDefense.GarrisonCount = () => 0;
            long empty = EstateDefense.ApplyToLoot(raw);
            Check(guarded < raw && empty < raw && empty > guarded,
                "수비가 있으면 약탈이 더 줄어든다(§13-5)");

            GameState.SetTowerFloorForTest(30);
            GameState.Grant(200_000);
            DefenseState.ResetForTest();
            EstateDefense.GarrisonCount = () => 0;
            long withDef = InvasionState.LootCopper();
            EstateDefense.SetLevelForTest(EstateDefense.Kind.화살탑, 0);
            EstateDefense.SetLevelForTest(EstateDefense.Kind.마법탑, 0);
            EstateDefense.ForgetInMemoryForTest();
            EstateDefense.ResetForTest();
            EstateDefense.GarrisonCount = () => 0;
            long noDef = InvasionState.LootCopper();
            Check(noDef > withDef, "침략 약탈이 방어 건물을 소비한다");

            _ = nameof(EstateDefense.Tick);
            _ = nameof(EstateDefense.TryStart);
            _ = nameof(EstateDefense.ApplyToLoot);
            _ = nameof(EstateDefense.SeedQaIfRequested);

            EstateDefense.ResetForTest();
            EstateBuild.ResetForTest();
            GameState.ResetAll();
            if (_fail > 0)
            {
                Debug.LogError("[EstateDefenseSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("EstateDefenseSelfCheck FAIL " + _fail);
            }
            Debug.Log("[EstateDefenseSelfCheck] PASS\n" + _log);
        }
    }
}
